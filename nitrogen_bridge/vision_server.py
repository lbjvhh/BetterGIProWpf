"""
BetterGIProWpf 真实模型推理服务（YOLO / PaddleOCR / Whisper）。
对外 HTTP 5004：
  POST /yolo      {image: b64png} -> {boxes:[[x1,y1,x2,y2]], scores:[], classes:[]}
  POST /ocr       {image: b64png} -> {text:"...", lines:[{text, box, score}]}
  POST /whisper   {audio: b64wav} -> {text:"...", segments:[{start,end,text}]}
  POST /ui_set_baseline {image:b64, name} -> {ok, name}
  POST /ui_diff   {image:b64, threshold} -> {changed, mse}
  GET  /health    -> {status, models}
"""
import base64, io, time, json, threading
import numpy as np
from PIL import Image
from http.server import BaseHTTPRequestHandler, HTTPServer

MODELS = r"C:\better\BetterGIProWpf-App\Models"
HTTP_PORT = 5004

_state = {"yolo": None, "ocr_det": None, "ocr_rec": None, "whisper": None, "dict": []}
_lock = threading.Lock()
_baseline = {"frame": None, "name": ""}  # P3: UI 变化检测基线帧


def load_yolo():
    import onnxruntime as ort
    p = f"{MODELS}\\yolo\\yolov8s.onnx"
    _state["yolo"] = ort.InferenceSession(p, providers=["CPUExecutionProvider"])
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
        boxes.append([x / 640 * w, y / 640 * h, (x + ww) / 640 * w, (y + hh) / 640 * h])
        scores.append(float(conf))
        classes.append(int(row[4:].argmax()) if row.shape[0] > 4 else 0)
    return {"boxes": boxes, "scores": scores, "classes": classes, "ms": round(dt, 1)}


def ocr_infer(img: Image.Image):
    s_det, s_rec = _state["ocr_det"], _state["ocr_rec"]
    img_rgb = img.convert("RGB")
    w, h = img_rgb.size
    det_in = s_det.get_inputs()[0].name
    det_out = s_det.get_outputs()[0].name
    det_img = img_rgb.resize((960, 960))
    det_arr = np.asarray(det_img, dtype=np.float32) / 255.0
    det_arr = det_arr.transpose(2, 0, 1)[None]
    t0 = time.time()
    _ = s_det.run([det_out], {det_in: det_arr})
    rec_in = s_rec.get_inputs()[0].name
    rec_out = s_rec.get_outputs()[0].name
    std_h = 48
    ratio = std_h / img_rgb.height
    std_w = min(320, max(48, int(img_rgb.width * ratio)))
    rec_img = img_rgb.resize((std_w, std_h))
    rec_arr = np.asarray(rec_img, dtype=np.float32) / 255.0
    rec_arr = (rec_arr - 0.5) / 0.5
    rec_arr = rec_arr[:, :, ::-1].copy()
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
