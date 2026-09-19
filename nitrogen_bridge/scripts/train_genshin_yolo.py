# -*- coding: utf-8 -*-
"""
原神专属 YOLO —— 训练脚本
用 genshin_yolo_dataset.py 生成的合成数据集训练 YOLOv8n（原神图标检测），导出 ONNX 供 vision_server 加载。
- 无 GPU：CPU 训练（小 epoch 可跑，推荐 GPU 机跑全量）
- 依赖: pip install ultralytics
用法:
  python train_genshin_yolo.py [--data 数据集目录] [--epochs 50] [--export 1]
输出: C:\\better\\genshin_yolo\\runs\\best.pt → best.onnx
"""
import os, sys, argparse, shutil


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--data", default=r"C:\better\genshin_yolo\dataset")
    ap.add_argument("--epochs", type=int, default=50)
    ap.add_argument("--imgsz", type=int, default=640)
    ap.add_argument("--export", type=int, default=1)
    args = ap.parse_args()

    data_yaml = os.path.join(args.data, "data.yaml")
    if not os.path.exists(data_yaml):
        print(f"[!] 缺少 {data_yaml}，先运行 genshin_yolo_dataset.py")
        return 1

    try:
        from ultralytics import YOLO
    except ImportError:
        print("[!] 未安装 ultralytics，请执行: pip install ultralytics")
        return 1

    # 优先本地预训练权重，缺失则从零训练（合成数据从零也收敛）
    pt = r"C:\better\genshin_yolo\yolov8n.pt"
    if not os.path.exists(pt):
        try:
            print("[*] 下载 yolov8n.pt 预训练权重…")
            import urllib.request
            urllib.request.urlretrieve(
                "https://github.com/ultralytics/assets/releases/download/v8.3.0/yolov8n.pt", pt)
        except Exception as e:
            print(f"[*] 下载失败（{e}），将从零训练")
            pt = "yolov8n.yaml"

    model = YOLO(pt)
    print(f"[*] 训练 {args.epochs} epochs (imgsz={args.imgsz}, device=auto)…")
    model.train(
        data=data_yaml, epochs=args.epochs, imgsz=args.imgsz,
        project=r"C:\better\genshin_yolo\runs", name="genshin", exist_ok=True,
        patience=20, batch=8, workers=0, verbose=True,
    )

    best = os.path.join(r"C:\better\genshin_yolo\runs\genshin", "weights", "best.pt")
    if not os.path.exists(best):
        print("[!] 训练未产出 best.pt")
        return 1

    if args.export:
        print("[*] 导出 ONNX…")
        m = YOLO(best)
        m.export(format="onnx", imgsz=args.imgsz, opset=17)
        onnx_path = best.replace(".pt", ".onnx")
        dst = r"C:\better\BetterGIProWpf-App\Models\yolo\genshin_yolov8n.onnx"
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        shutil.copy(onnx_path, dst)
        print(f"[✓] 原神专属模型已导出 → {dst}")

    print("[✓] 训练完成")
    return 0


if __name__ == "__main__":
    sys.exit(main())
