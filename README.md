# BetterGI Pro · 融合版

视频识别 → 脚本生成 → 自动执行 · 26+ 功能模块融合

## 项目说明

### 定位
基于 BetterGI（C# WPF）执行底座 + Python 推理服务（NitroGen / YOLO / PaddleOCR / faster-whisper），实现"看视频→出操作→自动玩"的端到端游戏自动化系统。

### 技术栈
- **C# WPF .NET 8**：主界面、调度器、脚本管理
- **Python HTTP 服务**：
  - `vision_server.py` (5004)：YOLO + PaddleOCR PP-OCRv5 + Whisper
  - `stream_bridge.py` (5005)：窗口捕获 + 输入注入 + 安全停机
  - `nitrogen_bridge_server.py` (5003)：NitroGen 推理桥接
- **模型**：ng.pt (1.88GB) / yolov8s.onnx (42MB) / PP-OCRv5 (170MB) / faster-whisper-small int8

### 核心链路
攻略视频 → WebView2 播放 → ContinuousRecognizer 每 500ms 抓帧 (2 FPS) → PaddleOCR 识别画面文字 → 关键词规则匹配 → 生成操作步骤 → 实时写 User/AutoFight/*.txt + User/ScriptGroup/*/main.js → 停止时写入知识库 + 复制到 generated_scripts/ 准备上传

---

## 操作说明

### 1. 启动服务
```
C:\better\BetterGIProWpf-App\BetterGIProWpf.exe
```
Python 服务需手动启动：
```
cd C:\better\nitrogen_bridge
python vision_server.py     # 5004（加载 ~30 秒）
python stream_bridge.py     # 5005
python nitrogen_bridge_server.py  # 5003（需先起 serve.py）
```

### 2. 导航结构（7 分组 15 项）

| 菜单 | 功能 |
|---|---|
| 首页/启动 | 服务状态检查、一键启动 |
| 攻略浏览器（2FPS识别） | 播放攻略视频，点"持续识别"自动抓帧 OCR，"停止并保存"生成脚本 |
| 知识库/问答/翻译 | TF-IDF 语义检索、6 类问题路由、免费翻译、装备识别、规则提取、跨游戏切换 |
| 操作执行 | 手动触发 BetterGI 脚本 |
| AI 队友（双轨） | 语音/文字指令 → stream_bridge inject；主轨脚本/辅轨 NitroGen 切换 |
| 策略教练 | 5 类实时建议（元素反应/体力/路线/技能/弱点）+ TTS + 音乐场景识别 |
| 识别层/问答 | 多模态游戏状态问答 |
| 脚本仓库/自适应 | GitHub 上游脚本订阅、异常诊断知识库、回放导出对比 |
| 自动化编排 | 定时任务 cron |
| 插件管理 | JS 脚本加载 |
| 游戏管理 | 窗口枚举、启动游戏 |
| 仪表盘/应急 | 1Hz CPU/内存/延迟监控、安全停机、网络检测、UI 变化检测 |
| 高光剪辑 | 真实帧录制 + ffmpeg 合成 |
| AI 设置 | OpenAI 兼容 API 配置 |
| 通用设置 | 拟人化强度、截图方式 |

### 3. 安全机制
- **紧急停机**：OCR 检测 30+ 风险词 → 500ms 内停注入
- **输入拟人化**：±15% 高斯抖动 + 10% 长停顿 200-800ms
- **卡死脱离**：方向键组合→跳跃→冲刺→开地图重置
- **网络检测**：连续两帧 MSE < 5 判定卡住
- **UI 变化检测**：基线帧 MSE 对比

### 4. 端口
| 端口 | 服务 |
|---|---|
| 5003 | NitroGen bridge |
| 5004 | vision_server (YOLO/OCR/Whisper) |
| 5005 | stream_bridge (grab/inject/safety) |
| 5555 | NitroGen ZMQ |

配置持久化：`%APPDATA%\BetterGIProWpf\config.json`
日志：`%APPDATA%\BetterGIProWpf\logs\app_yyyyMMdd.log`

### 5. GitHub
- 仓库：https://github.com/lbjvhh/BetterGIProWpf
- CI：`.github/workflows/regression.yml`

## 本轮基础设施完善（2026-09-20）

- **0 编译警告**：清理全部 CS8602/CS8604/CS8603/CS1998
- **AppLogger 文件日志**：崩溃可回溯
- **ServicePorts 配置集中**：端口/host 持久化到 config.json
- **CloudGenshinClient 真实帧捕获**：调用 stream_bridge /grab 抓 Moonlight 窗口
- **GenshinAdapter.DetectVersion()**：从进程 MainModule 读 FileVersion
- **ServiceError 错误码体系**：1001 服务未启动 / 1002 超时 / 1003 模型推理失败 / 1006 安全停机
- **测试 198 通过 / 0 失败**
