"""Regression: feed test videos through vision_server, emit JSON report."""
import sys, json, time, argparse, base64
from pathlib import Path
import requests
VISION = "http://127.0.0.1:5004"
def check():
    try: return requests.get(f"{VISION}/health", timeout=5).json().get("status") == "ok"
    except Exception: return False
def run_video(p, frames=30):
    import cv2
    cap = cv2.VideoCapture(str(p)); total = int(cap.get(cv2.CAP_PROP_FRAME_COUNT)); sample = max(1, total // frames)
    boxes, texts, n = [], [], 0; t0 = time.time()
    for i in range(0, total, sample):
        cap.set(cv2.CAP_PROP_POS_FRAMES, i); ok, fr = cap.read()
        if not ok: continue
        _, buf = cv2.imencode(".jpg", fr); b64 = base64.b64encode(buf).decode()
        try:
            y = requests.post(f"{VISION}/yolo", json={"image": b64}, timeout=10).json(); boxes.append(len(y.get("boxes", [])))
            o = requests.post(f"{VISION}/ocr", json={"image": b64}, timeout=10).json(); texts.append(o.get("text", "")); n += 1
        except Exception: pass
    cap.release(); dt = time.time() - t0
    return {"video": p.name, "frames": n, "avg_boxes": sum(boxes)/max(1, len(boxes)), "ocr_chars": sum(len(t) for t in texts), "fps": round(n/max(0.1, dt), 1)}
def main():
    ap = argparse.ArgumentParser(); ap.add_argument("--videos", required=True); ap.add_argument("--report", default="report.json"); a = ap.parse_args()
    if not check(): print("vision_server not ready"); sys.exit(2)
    vdir = Path(a.videos); vids = list(vdir.glob("*.mp4")) + list(vdir.glob("*.mkv"))
    if not vids: print("no videos"); sys.exit(3)
    results = [run_video(v) for v in vids]
    rep = {"timestamp": time.strftime("%Y-%m-%d %H:%M:%S"), "videos": len(vids), "results": results, "all_pass": all(r["frames"] > 0 for r in results)}
    json.dump(rep, open(a.report, "w", encoding="utf-8"), ensure_ascii=False, indent=2); print(f"report: {a.report}")
if __name__ == "__main__": main()
