"""
游戏实跑回归：输入测试视频集，逐帧过 vision_server，输出报告 JSON。
用法：python run_regression.py --videos test_videos/ --report report.json
"""
import os, sys, json, time, argparse, base64, io
from pathlib import Path
import requests

VISION = "http://127.0.0.1:5004"
STREAM = "http://127.0.0.1:5005"

def check_health():
    try:
        r = requests.get(f"{VISION}/health", timeout=5).json()
        return r.get("status") == "ok"
    except Exception as e:
        print(f"vision_server 未就绪: {e}")
        return False


def run_video(video_path: Path, frames=30):
    import cv2
    cap = cv2.VideoCapture(str(video_path))
    total = int(cap.get(cv2.CAP_PROP_FRAME_COUNT))
    sample = max(1, total // frames)
    boxes, texts = [], []
    t0 = time.time()
    n = 0
    for i in range(0, total, sample):
        cap.set(cv2.CAP_PROP_POS_FRAMES, i)
        ok, frame = cap.read()
        if not ok: continue
        _, buf = cv2.imencode(".jpg", frame)
        b64 = base64.b64encode(buf).decode()
        try:
            y = requests.post(f"{VISION}/yolo", json={"image": b64}, timeout=10).json()
            boxes.append(len(y.get("boxes", [])))
            o = requests.post(f"{VISION}/ocr", json={"image": b64}, timeout=10).json()
            texts.append(o.get("text", ""))
            n += 1
        except Exception: pass
    cap.release()
    dt = time.time() - t0
    return {
        "video": video_path.name,
        "frames_processed": n,
        "avg_yolo_boxes": sum(boxes) / max(1, len(boxes)),
        "ocr_chars": sum(len(t) for t in texts),
        "fps": round(n / max(0.1, dt), 1),
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--videos", required=True)
    ap.add_argument("--report", default="report.json")
    args = ap.parse_args()
    if not check_health():
        print("vision_server 未就绪，退出")
        sys.exit(2)
    vdir = Path(args.videos)
    videos = list(vdir.glob("*.mp4")) + list(vdir.glob("*.mkv"))
    if not videos:
        print(f"无视频: {vdir}")
        sys.exit(3)
    results = []
    for v in videos:
        print(f"跑 {v.name} ...")
        results.append(run_video(v))
    report = {
        "timestamp": time.strftime("%Y-%m-%d %H:%M:%S"),
        "videos": len(videos),
        "results": results,
        "all_pass": all(r["frames_processed"] > 0 for r in results),
    }
    with open(args.report, "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)
    print(f"报告: {args.report} (pass={report['all_pass']})")


if __name__ == "__main__":
    main()
