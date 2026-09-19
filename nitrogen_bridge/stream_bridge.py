"""
串流/云游戏桥接服务（HTTP 5005）。
零账号依赖：捕获任意窗口（Moonlight/Sunshine/云原神网页/游戏窗口），暴露帧流供 vision_server/NitroGen 用。
输入注入通过 Win32 SendInput，不依赖串流软件内部通道。

接口：
  POST /grab   {hwnd?:int, region?:{x,y,w,h}} -> {frame_b64, width, height, ms}
  POST /inject {keys:["w","a"], mouse:{dx,dy}, delay_ms:30} -> {ok}
  GET  /windows -> {windows:[{hwnd,title,pid}]}
"""
import base64, io, time, json, ctypes
import numpy as np
from PIL import Image
from http.server import BaseHTTPRequestHandler, HTTPServer
import win32gui, win32ui, win32con, win32process

HTTP_PORT = 5005
VISION_URL = "http://127.0.0.1:5004"

RISK_KEYWORDS = [
    # 原有关键词
    "封禁", "违规", "踢下线", "账号异常", "检测到异常", "禁止登录",
    "ban", "suspicious", "违规操作", "限制登录", "mihoyo shield",
    "security violation", "账号冻结", "登录失败",
    # 模块16 扩展：网络/崩溃/反作弊/弹窗
    "网络连接失败", "与服务器断开", "连接超时", "游戏崩溃",
    "未响应", "程序错误", "debug", "crash",
    "反作弊", "检测到第三方", "外挂", "辅助工具",
    "错误代码", "错误码", "err", "disconnect",
    "更新维护", "服务器维护", "无法连接",
]

_safety = {"emergency": False, "reason": "", "since": 0}


def check_safety():
    """抓全屏 OCR 检测风险词。命中则设紧急停机。"""
    global _safety
    if _safety["emergency"]:
        return False, _safety["reason"]
    try:
        img = grab_window(None)
        b = io.BytesIO(); img.save(b, "JPEG", quality=60)
        b64 = base64.b64encode(b.getvalue()).decode()
        req_body = json.dumps({"image": b64}).encode()
        import urllib.request
        r = urllib.request.urlopen(urllib.request.Request(
            f"{VISION_URL}/ocr", data=req_body,
            headers={"Content-Type": "application/json"}), timeout=2)
        ocr_text = json.loads(r.read()).get("text", "").lower()
        for kw in RISK_KEYWORDS:
            if kw.lower() in ocr_text:
                _safety = {"emergency": True, "reason": f"检测到风险词: {kw}",
                           "since": time.time()}
                print(f"[SAFETY] 紧急停机: {kw}", flush=True)
                return False, _safety["reason"]
    except Exception:
        pass  # OCR 不可用不阻断
    return True, ""


def enum_windows():
    out = []
    def cb(hwnd, _):
        if win32gui.IsWindowVisible(hwnd):
            t = win32gui.GetWindowText(hwnd)
            if t:
                _, pid = win32process.GetWindowThreadProcessId(hwnd)
                out.append({"hwnd": int(hwnd), "title": t, "pid": int(pid)})
    win32gui.EnumWindows(cb, None)
    return out


def grab_window(hwnd=None, region=None):
    """抓窗口/区域画面。hwnd=None 抓全屏。"""
    if hwnd:
        rect = win32gui.GetWindowRect(int(hwnd))
        x0, y0, x1, y1 = rect
        w, h = x1 - x0, y1 - y0
    else:
        user32 = ctypes.windll.user32
        w, h = user32.GetSystemMetrics(0), user32.GetSystemMetrics(1)
        x0, y0 = 0, 0

    if region:
        x0 += region.get("x", 0); y0 += region.get("y", 0)
        w = region.get("w", w); h = region.get("h", h)

    hwnd_dc = win32gui.GetWindowRect(0) if not hwnd else None
    # 用 PrintWindow（后台窗口也能抓）
    hwnd_target = int(hwnd) if hwnd else 0
    if hwnd_target:
        # 切前台（后台进程可能被前台锁拒绝，失败不致命——PrintWindow/BitBlt 不依赖前台）
        try:
            win32gui.SetForegroundWindow(hwnd_target)
            time.sleep(0.05)
        except Exception:
            pass
        rect = win32gui.GetWindowRect(hwnd_target)
        x0, y0, x1, y1 = rect
        w, h = x1 - x0, y1 - y0

    mfc_dc = win32ui.CreateDCFromHandle(win32gui.GetWindowDC(hwnd_target))
    save_dc = mfc_dc.CreateCompatibleDC()
    bmp = win32ui.CreateBitmap()
    bmp.CreateCompatibleBitmap(mfc_dc, w, h)
    save_dc.SelectObject(bmp)
    PW_RENDERFULLCONTENT = 0x00000002
    result = ctypes.windll.user32.PrintWindow(hwnd_target, save_dc.GetSafeHdc(), PW_RENDERFULLCONTENT)
    bmpinfo = bmp.GetInfo()
    bmpstr = bmp.GetBitmapBits(True)
    img = Image.frombuffer("RGB", (bmpinfo["bmWidth"], bmpinfo["bmHeight"]),
                           bmpstr, "raw", "BGRX", 0, 1)
    win32gui.DeleteObject(bmp.GetHandle())
    save_dc.DeleteDC(); mfc_dc.DeleteDC()
    win32gui.ReleaseDC(hwnd_target, win32gui.GetWindowDC(hwnd_target))
    # PrintWindow 对 DX11 渲染常返回黑帧 → 自动回退 BitBlt（窗口化/无边框有效）
    import numpy as _np
    if _np.asarray(img).mean() < 2.0:
        try:
            wdc = win32gui.GetWindowDC(hwnd_target)
            mdc = win32ui.CreateDCFromHandle(wdc)
            sdc = mdc.CreateCompatibleDC()
            bmp2 = win32ui.CreateBitmap()
            bmp2.CreateCompatibleBitmap(mdc, w, h)
            sdc.SelectObject(bmp2)
            ctypes.windll.gdi32.BitBlt(sdc.GetSafeHdc(), 0, 0, w, h,
                                       wdc, 0, 0, 0x00CC0020)  # SRCCOPY
            bstr = bmp2.GetBitmapBits(True)
            img = Image.frombuffer("RGB", (bmpinfo["bmWidth"], bmpinfo["bmHeight"]),
                                   bstr, "raw", "BGRX", 0, 1)
            win32gui.DeleteObject(bmp2.GetHandle())
            sdc.DeleteDC(); mdc.DeleteDC()
            win32gui.ReleaseDC(hwnd_target, wdc)
        except Exception:
            pass
    return img


KEYMAP = {
    "w": 0x57, "a": 0x41, "s": 0x53, "d": 0x44,
    "space": 0x20, "shift": 0xA0, "ctrl": 0xA2,
    "q": 0x51, "e": 0x45, "r": 0x52, "f": 0x46,
    "m": 0x4D, "esc": 0x1B, "tab": 0x09,
}


def inject(keys, mouse=None, delay_ms=30):
    """注入按键+鼠标移动。P2 拟人化：
    - 按键间隔泊松-高斯混合抖动（基础 ±15%，10% 概率长停顿 200-800ms）
    - 按住时间随机 20-80ms
    - 鼠标移动分多步，每步 ±1px 微抖动
    """
    ok, reason = check_safety()
    if not ok:
        return False, reason
    import random
    for k in keys or []:
        vk = KEYMAP.get(k.lower())
        if vk is None: continue
        ctypes.windll.user32.keybd_event(vk, 0, 0, 0)
        time.sleep(random.uniform(0.02, 0.08))  # 按住时间
        ctypes.windll.user32.keybd_event(vk, 0, 2, 0)
        base = delay_ms / 1000.0
        jitter = random.gauss(0, base * 0.15)
        if random.random() < 0.10:
            jitter += random.uniform(0.2, 0.8)
        time.sleep(max(0.01, base + jitter))
    if mouse:
        dx, dy = mouse.get("dx", 0), mouse.get("dy", 0)
        steps = max(5, min(40, int((abs(dx) + abs(dy)) / 20)))
        sx, sy = dx / steps, dy / steps
        for i in range(steps):
            jx = sx + random.uniform(-1, 1)
            jy = sy + random.uniform(-1, 1)
            ctypes.windll.user32.mouse_event(0x0001, int(jx), int(jy), 0, 0)
            time.sleep(random.uniform(0.005, 0.015))
    return True, ""


class H(BaseHTTPRequestHandler):
    def log_message(self, *a): pass
    def _json(self, code, obj):
        b = json.dumps(obj, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(b)))
        self.end_headers()
        self.wfile.write(b)

    def do_GET(self):
        if self.path == "/windows":
            try: self._json(200, {"windows": enum_windows()})
            except Exception as e:
                import traceback; print("[windows err]", traceback.format_exc(), flush=True)
                self._json(500, {"e": str(e)})
        elif self.path == "/safety_status":
            self._json(200, {"emergency": _safety["emergency"],
                             "reason": _safety["reason"],
                             "since": _safety["since"]})
        else: self._json(404, {})

    def do_POST(self):
        try:
            n = int(self.headers.get("Content-Length", 0))
            req = json.loads(self.rfile.read(n).decode("utf-8"))
            t0 = time.time()
            if self.path == "/grab":
                img = grab_window(req.get("hwnd"), req.get("region"))
                b = io.BytesIO(); img.save(b, "JPEG", quality=80)
                self._json(200, {"frame_b64": base64.b64encode(b.getvalue()).decode(),
                                 "width": img.width, "height": img.height,
                                 "ms": round((time.time() - t0) * 1000, 1)})
            elif self.path == "/inject":
                ok, reason = inject(req.get("keys", []), req.get("mouse"), req.get("delay_ms", 30))
                self._json(200, {"ok": ok, "emergency": not ok, "reason": reason})
            elif self.path == "/unstuck":
                # P2: 卡死自适应脱离——依次尝试方向键+跳跃+冲刺
                strategies = [
                    ["w", "a", "w", "d"],
                    ["space"],
                    ["shift", "w"],
                    ["s", "s"],
                    ["m"],  # 打开地图再关，重置视角
                ]
                results = []
                for s in strategies:
                    ok, reason = inject(s, delay_ms=150)
                    results.append({"keys": s, "ok": ok, "reason": reason})
                    time.sleep(0.3)
                self._json(200, {"strategies": results})
            elif self.path == "/audio_classify":
                # P3: 音乐场景识别——FFT 高频/低频能量比
                wav = base64.b64decode(req.get("audio", ""))
                import wave, io as _io
                wr = wave.open(_io.BytesIO(wav), "rb")
                nframes = wr.getnframes()
                rate = wr.getframerate()
                raw = wr.readframes(nframes)
                wr.close()
                samples = np.frombuffer(raw, dtype=np.int16).astype(np.float32) / 32768.0
                fft = np.abs(np.fft.rfft(samples))
                freqs = np.fft.rfftfreq(len(samples), 1.0 / rate)
                # 高频 > 2000Hz，低频 < 500Hz
                mask_high = freqs >= 2000
                mask_low = (freqs >= 100) & (freqs < 500)
                e_high = float(np.sum(fft[mask_high])) if mask_high.any() else 0
                e_low = float(np.sum(fft[mask_low])) if mask_low.any() else 0
                total = e_high + e_low + 1e-9
                ratio = e_high / total
                rms = float(np.sqrt(np.mean(samples ** 2)))
                if ratio > 0.45 and rms > 0.05:
                    scene = "combat"
                elif rms < 0.01:
                    scene = "menu"
                elif ratio < 0.25:
                    scene = "explore"
                else:
                    scene = "explore"
                self._json(200, {"scene": scene, "confidence": round(ratio, 2),
                                 "energy_high": round(e_high, 1), "energy_low": round(e_low, 1),
                                 "rms": round(rms, 4)})
            elif self.path == "/network_check":
                # 模块24: 网络波动检测——连续抓两帧对比变化率
                f1 = grab_window()
                time.sleep(0.5)
                f2 = grab_window()
                a1 = np.asarray(f1.convert("RGB").resize((160, 90)), dtype=np.float32)
                a2 = np.asarray(f2.convert("RGB").resize((160, 90)), dtype=np.float32)
                mse = float(np.mean((a1 - a2) ** 2))
                # MSE < 5 判定画面卡住（疑似网络问题）
                stuck = mse < 5.0
                self._json(200, {"mse": round(mse, 2), "stuck": stuck,
                                 "elapsed": 0.5, "action": "wait_reconnect" if stuck else "ok"})
            elif self.path == "/safety_reset":
                _safety["emergency"] = False
                _safety["reason"] = ""
                self._json(200, {"ok": True})
            elif self.path == "/safety_status":
                self._json(200, {"emergency": _safety["emergency"],
                                 "reason": _safety["reason"],
                                 "since": _safety["since"]})
            else:
                self._json(404, {})
        except Exception as e:
            self._json(500, {"e": str(e)})


if __name__ == "__main__":
    import win32process
    print(f"[stream-bridge] HTTP {HTTP_PORT} (窗口捕获+输入注入)")
    HTTPServer(("127.0.0.1", HTTP_PORT), H).serve_forever()
