"""Vision HTTP server 5004: YOLO + PaddleOCR PP-OCRv5 + faster-whisper."""
import base64, io, time, json, threading
import numpy as np
from PIL import Image
from http.server import BaseHTTPRequestHandler, HTTPServer

MODELS = r"C:\better\BetterGIProWpf-App\Models"
HTTP_PORT = 5004
_state = {"yolo": None, "ocr_det": None, "ocr_rec": None, "whisper": None, "dict": []}
_lock = threading.Lock()

def load_yolo():
    import onnxruntime as ort
    _state["yolo"] = ort.InferenceSession(f"{MODELS}\\yolo\\yolov8s.onnx", providers=["CPUExecutionProvider"])
    print("[models] YOLO", flush=True)

def load_ocr():
    import onnxruntime as ort
    _state["ocr_det"] = ort.InferenceSession(f"{MODELS}\\ppocrv5\\det\\inference.onnx", providers=["CPUExecutionProvider"])
    _state["ocr_rec"] = ort.InferenceSession(f"{MODELS}\\ppocrv5\\rec\\inference.onnx", providers=["CPUExecutionProvider"])
    with open(f"{MODELS}\\ppocrv5\\rec\\ppocr_keys_v1.txt", encoding="utf-8") as f:
        _state["dict"] = [l.rstrip("\n") for l in f.readlines()]
    print("[models] OCR", flush=True)

def load_whisper():
    from faster_whisper import WhisperModel
    _state["whisper"] = WhisperModel("small", device="cpu", compute_type="int8", download_root=f"{MODELS}\\whisper")
    print("[models] Whisper", flush=True)

def yolo_infer(img):
    s = _state["yolo"]; inp = s.get_inputs()[0].name; out = s.get_outputs()[0].name
    w, h = img.size; img640 = img.convert("RGB").resize((640, 640))
    arr = np.asarray(img640, dtype=np.float32) / 255.0; arr = arr.transpose(2, 0, 1)[None]
    t0 = time.time(); pred = s.run([out], {inp: arr})[0][0]; dt = (time.time() - t0) * 1000
    if pred.shape[0] < pred.shape[1]: pred = pred.T
    boxes, scores, classes = [], [], []
    for row in pred:
        conf = row[4:].max() if row.shape[0] > 4 else row[4]
        if conf < 0.45: continue
        x, y, ww, hh = row[0], row[1], row[2], row[3]
        boxes.append([x/640*w, y/640*h, (x+ww)/640*w, (y+hh)/640*h])
        scores.append(float(conf)); classes.append(int(row[4:].argmax()) if row.shape[0] > 4 else 0)
    return {"boxes": boxes, "scores": scores, "classes": classes, "ms": round(dt, 1)}

def ocr_infer(img):
    s_det, s_rec = _state["ocr_det"], _state["ocr_rec"]; rgb = img.convert("RGB"); w, h = rgb.size
    det_in = s_det.get_inputs()[0].name; det_out = s_det.get_outputs()[0].name
    det_img = rgb.resize((960, 960)); det_arr = np.asarray(det_img, dtype=np.float32) / 255.0; det_arr = det_arr.transpose(2, 0, 1)[None]
    t0 = time.time(); s_det.run([det_out], {det_in: det_arr})
    rec_in = s_rec.get_inputs()[0].name; rec_out = s_rec.get_outputs()[0].name
    std_h = 48; ratio = std_h / rgb.height; std_w = min(320, max(48, int(rgb.width * ratio)))
    rec_img = rgb.resize((std_w, std_h))
    arr = np.asarray(rec_img, dtype=np.float32) / 255.0; arr = (arr - 0.5) / 0.5; arr = arr[:, :, ::-1].copy(); arr = arr.transpose(2, 0, 1)[None]
    logits = s_rec.run([rec_out], {rec_in: arr})[0][0]; ids = logits.argmax(axis=-1)
    chars = []; prev = -1
    for i in ids:
        i = int(i)
        if i == 0: prev = i; continue
        if i == prev: continue
        if 1 <= i <= len(_state["dict"]): chars.append(_state["dict"][i - 1])
        prev = i
    return {"text": "".join(chars), "lines": [{"text": "".join(chars), "score": 0.9}], "ms": round((time.time()-t0)*1000, 1)}

def whisper_infer(wav):
    model = _state["whisper"]; p = r"C:\better\nitrogen_bridge\_tmp.wav"; open(p, "wb").write(wav)
    t0 = time.time(); segs, _ = model.transcribe(p, language="zh")
    out = [{"start": s.start, "end": s.end, "text": s.text} for s in segs]
    return {"text": " ".join(s["text"] for s in out), "segments": out, "ms": round((time.time()-t0)*1000, 1)}

class H(BaseHTTPRequestHandler):
    def log_message(self, *a): pass
    def _j(self, c, o): b = json.dumps(o, ensure_ascii=False).encode("utf-8"); self.send_response(c); self.send_header("Content-Type", "application/json; charset=utf-8"); self.send_header("Content-Length", str(len(b))); self.end_headers(); self.wfile.write(b)
    def do_GET(self):
        if self.path == "/health": self._j(200, {"status": "ok", "models": {k: v is not None for k, v in _state.items() if k != "dict"}})
        else: self._j(404, {"e": "nf"})
    def do_POST(self):
        try:
            n = int(self.headers.get("Content-Length", 0)); raw = json.loads(self.rfile.read(n).decode())
            if self.path == "/yolo":
                with _lock: self._j(200, yolo_infer(Image.open(io.BytesIO(base64.b64decode(raw["image"]))).convert("RGB")))
            elif self.path == "/ocr":
                with _lock: self._j(200, ocr_infer(Image.open(io.BytesIO(base64.b64decode(raw["image"]))).convert("RGB")))
            elif self.path == "/whisper":
                with _lock: self._j(200, whisper_infer(base64.b64decode(raw["audio"])))
            else: self._j(404, {"e": "nf"})
        except Exception as e: self._j(500, {"e": str(e)})

if __name__ == "__main__":
    print("[models] loading...", flush=True); load_yolo(); load_ocr(); load_whisper()
    print(f"[ready] HTTP {HTTP_PORT}", flush=True); HTTPServer(("127.0.0.1", HTTP_PORT), H).serve_forever()
