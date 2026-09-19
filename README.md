# BetterGIProWpf

BetterGI Pro WPF - 融合 NitroGen + PaddleOCR + YOLO + Whisper 的游戏自动化助手。

## 架构

- **C# WPF 前端**：主 UI、攻略浏览器、AI 队友、脚本管理
- **Python 推理服务**（vision_server.py :5004）：YOLO / PaddleOCR PP-OCRv5 / faster-whisper
- **Python 串流服务**（stream_bridge.py :5005）：窗口捕获 + 输入注入 + 安全停机
- **NitroGen 桥接**（nitrogen_bridge_server.py :5003）：视觉-动作模型推理

## 真实模型链路

| 模块 | 权重 | 延迟 |
|------|------|------|
| NitroGen | ng.pt 1.88GB | 2.0s |
| YOLO | yolov8s.onnx 42MB | 60ms |
| PaddleOCR | PP-OCRv5 server det+rec | 800ms |
| Whisper | faster-whisper small int8 | 1s |

## 功能

- 内置攻略浏览器（WebView2 + 抓帧 + 真实 OCR + NitroGen 动作生成）
- 语音指令（NAudio 录音 → whisper 识别）
- 紧急情况自动停机（OCR 检测风险词 → 500ms 停止输入）
- GitHub 脚本仓库直连（huiyadanli/bettergi-scripts-list）
- 回归测试视频集（5 类基准：跑图/战斗/采集/对话/传送）

## 构建

```bash
dotnet build BetterGIProWpf.csproj -c Release
dotnet run --project tests/SmokeTests.csproj -c Release
dotnet publish BetterGIProWpf.csproj -c Release -r win-x64 --self-contained
```
