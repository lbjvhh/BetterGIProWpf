"""
串流/云游戏桥接服务（HTTP 5005）。
窗口捕获 + 输入注入 + 安全停机（OCR 检测风险词）。
"""
import base64, io, time, json, ctypes
import numpy as np
from PIL import Image
from http.server import BaseHTTPRequestHandler, HTTPServer
import win32gui, win32ui, win32process

HTTP_PORT = 5005
VISION_URL = "http://127.0.0.1:5004"

RISK_KEYWORDS = [
    "封禁", "违规", "踢下线", "账号异常", "检测到异常", "禁止登录",
    "ban", "suspicious", "违规操作", "限制登录", "mihoyo shield",
    "security violation", "账号冻结", "登录失败",
]

_safety = {"emergency": False, "reason": "", "since": 0}


def check_safety():
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
    except Exception: pass
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
    hwnd_target = int(hwnd) if hwnd else 0
    if hwnd_target:
        win32gui.SetForegroundWindow(hwnd_target)
        time.sleep(0.05)
        rect = win32gui.GetWindowRect(hwnd_target)
        x0, y0, x1, y1 = rect
        w, h = x1 - x0, y1 - y0
    mfc_dc = win32ui.CreateDCFromHandle(win32gui.GetWindowDC(hwnd_target))
    save_dc = mfc_dc.CreateCompatibleDC()
    bmp = win32ui.CreateBitmap()
    bmp.CreateCompatibleBitmap(mfc_dc, w, h)
    save_dc.SelectObject(bmp)
    PW_RENDERFULLCONTENT = 0x00000002
    ctypes.windll.user32.PrintWindow(hwnd_target, save_dc.GetSafeHdc(), PW_RENDERFULLCONTENT)
    bmpinfo = bmp.GetInfo()
    bmpstr = bmp.GetBitmapBits(True)
    img = Image.frombuffer("RGB", (bmpinfo["bmWidth"], bmpinfo["bmHeight"]),
                           bmpstr, "raw", "BGRX", 0, 1)
    win32gui.DeleteObject(bmp.GetHandle())
    save_dc.DeleteDC(); mfc_dc.DeleteDC()
    win32gui.ReleaseDC(hwnd_target, win32gui.GetWindowDC(hwnd_target))
    return img


KEYMAP = {
    "w": 0x57, "a": 0x41, "s": 0x53, "d": 0x44,
    "space": 0x20, "shift": 0xA0, "ctrl": 0xA2,
    "q": 0x51, "e": 0x45, "r": 0x52, "f": 0x46,
    "m": 0x4D, "esc": 0x1B, "tab": 0x09,
}


def inject(keys, mouse=None, delay_ms=30):
    ok, reason = check_safety()
    if not ok: return False, reason
    import random
    for k in keys or []:
        vk = KEYMAP.get(k.lower())
        if vk is None: continue
        ctypes.windll.user32.keybd_event(vk, 0, 0, 0)
        time.sleep((delay_ms + random.uniform(-3, 3)) / 1000)
        ctypes.windll.user32.keybd_event(vk, 0, 2, 0)
    if mouse:
        dx, dy = mouse.get("dx", 0), mouse.get("dy", 0)
        ctypes.windll.user32.mouse_event(0x0001, dx, dy, 0, 0)
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
            except Exception as e: self._json(500, {"e": str(e)})
        elif self.path == "/safety_status":
            self._json(200, {"emergency": _safety["emergency"],
                             "reason": _safety["reason"], "since": _safety["since"]})
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
            elif self.path == "/safety_reset":
                _safety["emergency"] = False; _safety["reason"] = ""
                self._json(200, {"ok": True})
            elif self.path == "/safety_status":
                self._json(200, {"emergency": _safety["emergency"],
                                 "reason": _safety["reason"], "since": _safety["since"]})
            else: self._json(404, {})
        except Exception as e: self._json(500, {"e": str(e)})


if __name__ == "__main__":
    print(f"[stream-bridge] HTTP {HTTP_PORT}")
    HTTPServer(("127.0.0.1", HTTP_PORT), H).serve_forever()
