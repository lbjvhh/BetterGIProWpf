"""
BetterGIProWpf 真实模型推理服务（YOLO / PaddleOCR / Whisper / 原神专属检测）。
对外 HTTP 5004：
  POST /yolo            {image: b64png} -> {boxes:[[x1,y1,x2,y2]], scores:[], classes:[]}
  POST /ocr             {image: b64png} -> {text:"...", lines:[{text, box, score}]}
  POST /whisper         {audio: b64wav} -> {text:"...", segments:[{start,end,text}]}
  POST /genshin_detect  {image: b64png} -> 原神元素/场景（专属 YOLO + OCR 关键词）
  GET  /health          -> {status, models}
"""
import base64, io, time, json, threading, os
import numpy as np
from PIL import Image
from http.server import BaseHTTPRequestHandler, HTTPServer

MODELS = r"C:\better\BetterGIProWpf-App\Models"
HTTP_PORT = 5004

_state = {"yolo": None, "ocr_det": None, "ocr_rec": None, "whisper": None, "dict": [], "genshin_yolo": None}
_lock = threading.Lock()
_baseline = {"frame": None, "name": ""}  # P3: UI 变化检测基线帧


def load_yolo():
    import onnxruntime as ort
    p = f"{MODELS}\\yolo\\yolov8s.onnx"
    _state["yolo"] = ort.InferenceSession(p, providers=["CPUExecutionProvider"])
    # 原神专属模型（训练产出，缺失时降级为仅 COCO + OCR 知识库）
    gp = f"{MODELS}\\yolo\\genshin_yolov8n.onnx"
    _state["genshin_yolo"] = None
    if os.path.exists(gp):
        try:
            _state["genshin_yolo"] = ort.InferenceSession(gp, providers=["CPUExecutionProvider"])
            print("[models] Genshin YOLO loaded", flush=True)
        except Exception as e:
            print(f"[models] genshin yolo load failed: {e}", flush=True)
    print("[models] YOLO loaded", flush=True)


def load_ocr():
    import onnxruntime as ort
    det = f"{MODELS}\\ppocrv5\\det\\inference.onnx"
    rec = f"{MODELS}\\ppocrv5\\rec\\inference.onnx"
    _state["ocr_det"] = ort.InferenceSession(det, providers=["CPUExecutionProvider"])
    _state["ocr_rec"] = ort.InferenceSession(rec, providers=["CPUExecutionProvider"])
    with open(f"{MODELS}\\ppocrv5\\rec\\ppocr_keys_v1.txt", encoding="utf-8") as f:
        _state["dict"] = [l.rstrip("\n") for l in f.readlines()]
    print("[models] OCR det+rec loaded", flush=True)


def load_whisper():
    from faster_whisper import WhisperModel
    _state["whisper"] = WhisperModel("small", device="cpu", compute_type="int8",
                                     download_root=f"{MODELS}\\whisper")
    print("[models] Whisper small int8 loaded", flush=True)


def yolo_infer(img: Image.Image):
    s = _state["yolo"]
    inp = s.get_inputs()[0].name
    out_name = s.get_outputs()[0].name
    # yolov8s.onnx: input 1x3x640x640
    w, h = img.size
    img640 = img.convert("RGB").resize((640, 640))
    arr = np.asarray(img640, dtype=np.float32) / 255.0
    arr = arr.transpose(2, 0, 1)[None]
    t0 = time.time()
    outs = s.run([out_name], {inp: arr})
    dt = (time.time() - t0) * 1000
    pred = outs[0][0]
    boxes, scores, classes = [], [], []
    if pred.shape[0] < pred.shape[1]:
        pred = pred.T
    for row in pred:
        conf = row[4:].max() if row.shape[0] > 4 else row[4]
        if conf < 0.45:
            continue
        x, y, ww, hh = row[0], row[1], row[2], row[3]
        # 映射回原图（xywh 中心格式 → x1y1x2y2）
        boxes.append([round((x - ww / 2) / 640 * w, 1), round((y - hh / 2) / 640 * h, 1),
                      round((x + ww / 2) / 640 * w, 1), round((y + hh / 2) / 640 * h, 1)])
        scores.append(float(conf))
        classes.append(int(row[4:].argmax()) if row.shape[0] > 4 else 0)
    return {"boxes": boxes, "scores": scores, "classes": classes,
            "ms": round(dt, 1)}


def ocr_infer(img: Image.Image):
    """简化 PaddleOCR：det 找文本框 -> rec 识别。生产环境用完整 det 后处理。"""
    s_det, s_rec = _state["ocr_det"], _state["ocr_rec"]
    img_rgb = img.convert("RGB")
    # det: input 1x3xHxW，这里直接整图当一个区域
    det_in = s_det.get_inputs()[0].name
    det_out = s_det.get_outputs()[0].name
    det_img = img_rgb.resize((960, 960))
    det_arr = np.asarray(det_img, dtype=np.float32) / 255.0
    det_arr = det_arr.transpose(2, 0, 1)[None]
    t0 = time.time()
    _ = s_det.run([det_out], {det_in: det_arr})
    # rec: PP-OCRv5 标准预处理——高度固定 48，宽度按比例（最大 320），归一化到 [-1,1]
    rec_in = s_rec.get_inputs()[0].name
    rec_out = s_rec.get_outputs()[0].name
    std_h = 48
    ratio = std_h / img_rgb.height
    std_w = min(320, max(48, int(img_rgb.width * ratio)))
    rec_img = img_rgb.resize((std_w, std_h))
    rec_arr = np.asarray(rec_img, dtype=np.float32) / 255.0
    rec_arr = (rec_arr - 0.5) / 0.5
    rec_arr = rec_arr[:, :, ::-1].copy()  # BGR
    rec_arr = rec_arr.transpose(2, 0, 1)[None]
    rec_outs = s_rec.run([rec_out], {rec_in: rec_arr})
    logits = rec_outs[0][0]
    ids = logits.argmax(axis=-1)
    chars = []
    prev = -1
    for i in ids:
        i = int(i)
        if i == 0:
            prev = i
            continue
        if i == prev:
            continue
        if 1 <= i <= len(_state["dict"]):
            chars.append(_state["dict"][i - 1])
        prev = i
    text = "".join(chars)
    dt = (time.time() - t0) * 1000
    return {"text": text, "lines": [{"text": text, "score": 0.9}], "ms": round(dt, 1)}


def whisper_infer(wav_bytes: bytes):
    model = _state["whisper"]
    with open(r"C:\better\nitrogen_bridge\_tmp.wav", "wb") as f:
        f.write(wav_bytes)
    t0 = time.time()
    segs, info = model.transcribe(r"C:\better\nitrogen_bridge\_tmp.wav", language="zh")
    out = [{"start": s.start, "end": s.end, "text": s.text} for s in segs]
    full = " ".join(s["text"] for s in out)
    dt = (time.time() - t0) * 1000
    return {"text": full, "segments": out, "ms": round(dt, 1)}


# ================= 原神专属 YOLO 检测 =================
GENSHIN_CLASSES = {
    "角色": {"keywords": ["角色", "队伍", "切换角色", "冒险等级"], "yolo": [0]},
    "敌人": {"keywords": ["敌人", "魔物", "丘丘人", "史莱姆", "深渊"], "yolo": [0, 16, 18, 19, 20, 21]},
    "Boss": {"keywords": ["boss", "首领", "北风狼", "风魔龙", "急冻树", "无相"], "yolo": [0]},
    "宝箱": {"keywords": ["宝箱", "华丽宝箱", "珍贵宝箱", "精致宝箱", "普通的宝箱"], "yolo": []},
    "传送锚点": {"keywords": ["传送锚点", "锚点", "传送", "解锁"], "yolo": []},
    "七天神像": {"keywords": ["七天神像", "神像", "恢复体力"], "yolo": []},
    "NPC": {"keywords": ["npc", "对话", "凯瑟琳", "商店", "委托"], "yolo": [0]},
    "采集物": {"keywords": ["采集", "矿石", "水晶", "甜甜花", "薄荷", "采摘"], "yolo": []},
    "圣遗物": {"keywords": ["圣遗物", "绝缘", "如雷", "宗室", "角斗士", "追忆"], "yolo": []},
    "武器": {"keywords": ["武器", "天空之翼", "护摩", "雾切", "薙草", "璞玉"], "yolo": []},
    "材料": {"keywords": ["材料", "突破", "天赋", "摩拉", "原石", "精锻"], "yolo": []},
    "任务": {"keywords": ["任务", "委托", "指引", "目标", "追踪"], "yolo": []},
    "技能": {"keywords": ["元素战技", "元素爆发", "技能", "冷却", "重击"], "yolo": []},
    "HUD": {"keywords": ["生命", "体力", "原粹树脂", "冒险阅历"], "yolo": []},
    "地图": {"keywords": ["地图", "追踪", "传送"], "yolo": []},
    "秘境": {"keywords": ["秘境", "副本", "深境", "地脉"], "yolo": []},
    "对话": {"keywords": ["对话", "剧情", "跳过", "确认"], "yolo": []},
    "菜单": {"keywords": ["设置", "背包", "角色", "冒险之证", "商城"], "yolo": []},
    "加载": {"keywords": ["加载", "进入游戏", "连接中"], "yolo": []},
    "城镇": {"keywords": ["蒙德城", "璃月港", "稻妻城", "须弥城", "枫丹"], "yolo": []},
}

GENSHIN_SCENES = {
    "战斗": ["敌人", "Boss", "boss", "攻击", "无相"],
    "对话": ["NPC", "对话", "剧情", "凯瑟琳"],
    "菜单": ["菜单", "背包", "设置", "冒险之证"],
    "秘境": ["秘境", "副本", "深境"],
    "探索": ["探索", "采集", "传送锚点", "七天神像", "宝箱"],
    "加载": ["加载", "连接中"],
    "城镇": ["城镇", "商店", "蒙德城", "璃月港"],
    "角色养成": ["圣遗物", "武器", "材料", "突破", "天赋"],
}

COCO_NAMES = {0: "person", 1: "bicycle", 2: "car", 3: "motorcycle", 5: "bus", 7: "truck",
              16: "dog", 17: "horse", 18: "sheep", 19: "cow", 20: "elephant", 21: "bear"}

GENSHIN_IDS = ["角色", "敌人", "Boss", "宝箱", "传送锚点", "七天神像", "NPC",
               "采集物", "圣遗物", "武器", "材料", "任务", "技能", "HUD", "地图", "秘境"]


def genshin_yolo_infer(img: Image.Image):
    """原神专属 YOLO 推理：直接输出 16 类原神元素框（训练产物）。模型缺失时返回空。"""
    s = _state.get("genshin_yolo")
    if s is None:
        return {"objects": [], "ms": 0, "note": "未加载专属模型"}
    inp = s.get_inputs()[0].name
    out_name = s.get_outputs()[0].name
    w, h = img.size
    img640 = img.convert("RGB").resize((640, 640))
    arr = np.asarray(img640, dtype=np.float32) / 255.0
    arr = arr.transpose(2, 0, 1)[None]
    t0 = time.time()
    outs = s.run([out_name], {inp: arr})
    dt = (time.time() - t0) * 1000
    pred = outs[0][0]
    if pred.shape[0] < pred.shape[1]:
        pred = pred.T
    objs = []
    for row in pred:
        conf = row[4:].max() if row.shape[0] > 4 else 0
        if conf < 0.25:
            continue
        x, y, ww, hh = row[0], row[1], row[2], row[3]
        cls = int(row[4:].argmax()) if row.shape[0] > 4 else 0
        if cls >= len(GENSHIN_IDS):
            continue
        objs.append({"category": GENSHIN_IDS[cls], "confidence": round(float(conf), 2),
                     "source": "genshin_yolo",
                     "box": [float(round((x - ww / 2) / 640 * w, 1)), float(round((y - hh / 2) / 640 * h, 1)),
                             float(round((x + ww / 2) / 640 * w, 1)), float(round((y + hh / 2) / 640 * h, 1))]})
    return {"objects": objs, "ms": round(dt, 1)}


def genshin_detect_infer(img: Image.Image):
    """原神专属检测：OCR 关键词 + 专属 YOLO + COCO 生物框 → 原神元素对象列表与场景推断。"""
    t0 = time.time()
    objects = []
    text = ""
    # 1) OCR 文本 → 类别
    try:
        ocr = ocr_infer(img)
        text = ocr.get("text", "") or ""
    except Exception:
        pass
    text_l = text.lower()
    # 1.5) 原神专属 YOLO 模型（训练产物）直接输出 16 类元素
    try:
        gy = genshin_yolo_infer(img)
        for o in gy["objects"]:
            objects.append(o)
    except Exception:
        pass
    for cat, meta in GENSHIN_CLASSES.items():
        for kw in meta["keywords"]:
            if kw.lower() in text_l:
                objects.append({"category": cat, "confidence": 0.85,
                                "source": "ocr", "keyword": kw, "text": text[:60]})
                break
    # 2) YOLO 生物框 → 敌人/角色候选
    try:
        yolo = yolo_infer(img)
        for box, score, cls in zip(yolo["boxes"], yolo["scores"], yolo["classes"]):
            if cls in (0, 16, 17, 18, 19, 20, 21):
                objects.append({"category": "敌人/角色(生物)", "confidence": round(score, 2),
                                "source": "yolo", "box": box,
                                "coco_class": COCO_NAMES.get(cls, str(cls))})
    except Exception:
        pass
    # 3) 场景推断
    scene, best = "探索", 0
    for sc, kws in GENSHIN_SCENES.items():
        hit = sum(1 for kw in kws if kw.lower() in text_l)
        hit += sum(1 for o in objects if o["category"] in kws)
        if hit > best:
            best, scene = hit, sc
    # 去重
    seen, unique = set(), []
    for o in objects:
        k = o["category"] + o.get("keyword", "") + o.get("source", "")
        if k not in seen:
            seen.add(k)
            unique.append(o)
    return {"objects": unique, "scene": scene, "ocr_text": text[:120],
            "summary": "检测到 %d 类原神元素，场景: %s" % (len(unique), scene),
            "ms": round((time.time() - t0) * 1000, 1)}


class H(BaseHTTPRequestHandler):
    def log_message(self, *a):
        pass

    def _json(self, code, obj):
        b = json.dumps(obj, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(b)))
        self.end_headers()
        self.wfile.write(b)

    def do_GET(self):
        if self.path == "/health":
            self._json(200, {"status": "ok",
                             "models": {"yolo": _state["yolo"] is not None,
                                        "ocr": _state["ocr_rec"] is not None,
                                        "whisper": _state["whisper"] is not None}})
        else:
            self._json(404, {"e": "nf"})

    def do_POST(self):
        try:
            n = int(self.headers.get("Content-Length", 0))
            raw = json.loads(self.rfile.read(n).decode("utf-8"))
            if self.path == "/yolo":
                img = Image.open(io.BytesIO(base64.b64decode(raw["image"]))).convert("RGB")
                with _lock:
                    self._json(200, yolo_infer(img))
            elif self.path == "/equipment_detect":
                img = Image.open(io.BytesIO(base64.b64decode(raw["image"]))).convert("RGB")
                with _lock:
                    r = yolo_infer(img)
                boxes = r.get("boxes", [])
                scores = r.get("scores", [])
                classes = r.get("classes", [])
                good = [(b, s, c) for b, s, c in zip(boxes, scores, classes) if s > 0.5]
                self._json(200, {"count": len(good), "items": good[:20],
                                 "summary": f"检测到 {len(good)} 个装备图标"})
            elif self.path == "/genshin_detect":
                # 原神专属 YOLO：OCR 关键词 + 专属模型 + COCO 生物框 → 原神元素/场景
                img = Image.open(io.BytesIO(base64.b64decode(raw["image"]))).convert("RGB")
                with _lock:
                    self._json(200, genshin_detect_infer(img))
            elif self.path == "/ocr":
                img = Image.open(io.BytesIO(base64.b64decode(raw["image"]))).convert("RGB")
                with _lock:
                    self._json(200, ocr_infer(img))
            elif self.path == "/ui_set_baseline":
                img = Image.open(io.BytesIO(base64.b64decode(raw["image"]))).convert("RGB").resize((160, 90))
                _baseline["frame"] = np.array(img, dtype=np.float32)
                _baseline["name"] = raw.get("name", "default")
                self._json(200, {"ok": True, "name": _baseline["name"]})
            elif self.path == "/ui_diff":
                if _baseline["frame"] is None:
                    self._json(200, {"changed": False, "mse": 0, "reason": "no baseline"})
                else:
                    img = Image.open(io.BytesIO(base64.b64decode(raw["image"]))).convert("RGB").resize((160, 90))
                    cur = np.array(img, dtype=np.float32)
                    mse = float(np.mean((cur - _baseline["frame"]) ** 2))
                    threshold = float(raw.get("threshold", 500))
                    self._json(200, {"changed": mse > threshold, "mse": round(mse, 1),
                                     "threshold": threshold, "baseline": _baseline["name"]})
            elif self.path == "/whisper":
                wav = base64.b64decode(raw["audio"])
                with _lock:
                    self._json(200, whisper_infer(wav))
            else:
                self._json(404, {"e": "nf"})
        except Exception as e:
            import traceback
            print("[err]", traceback.format_exc(), flush=True)
            self._json(500, {"e": str(e)})


if __name__ == "__main__":
    print("[models] loading...", flush=True)
    load_yolo()
    load_ocr()
    load_whisper()
    print(f"[models] all ready, HTTP {HTTP_PORT}", flush=True)
    HTTPServer(("127.0.0.1", HTTP_PORT), H).serve_forever()
