# BetterGI Pro WPF

.NET 8 WPF + Python 推理服务的原神融合自动化助手。

## 架构

- `BetterGIProWpf/` — C# WPF 主程序（26 功能模块 + 插件系统）
- `nitrogen_bridge/` — Python HTTP 推理服务
  - `vision_server.py` (5004): YOLOv8s + PaddleOCR PP-OCRv5 + faster-whisper
  - `nitrogen_bridge_server.py` (5003): HTTP→ZMQ bridge to NitroGen serve.py
  - `stream_bridge.py` (5005): window capture + input inject + safety OCR

## 本地模型权重（不推 GitHub，放发布目录 Models/）

- `Models/ng.pt` — NitroGen 1.88GB
- `Models/yolo/yolov8s.onnx` — 42MB
- `Models/ppocrv5/{det,rec}/inference.onnx` — 170MB
- `Models/ppocrv5/rec/ppocr_keys_v1.txt` — 18383 chars
- `Models/whisper/` — faster-whisper-small int8

## CI

`.github/workflows/regression.yml`：push 跑 Python 单测 + .NET build；周日跑自托管 GPU 实跑回归。
