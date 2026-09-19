# BetterGI Pro · 融合版

视频识别 → 脚本生成 → 自动执行 · 26+ 功能模块融合

## 项目说明

### 定位
基于 BetterGI（C# WPF）执行底座 + Python 推理服务（NitroGen / YOLO / PaddleOCR / faster-whisper），实现"看视频→出操作→自动玩"的端到端游戏自动化系统。

### 技术栈
- C# WPF .NET 8：主界面、调度器、脚本管理
- Python HTTP 服务：vision_server (5004) / stream_bridge (5005) / nitrogen_bridge (5003)
- 模型：ng.pt (1.88GB) / yolov8s.onnx / PP-OCRv5 / faster-whisper-small int8

### 核心链路
攻略视频 → WebView2 → ContinuousRecognizer 每 500ms 抓帧 → PaddleOCR → 关键词匹配 → 实时写脚本 → 停止时入知识库 + 上传

## 操作说明

### 导航（7 分组 15 项）
1. 🏠 首页/启动
2. 📺 攻略浏览器（2FPS识别）：播放视频，点"持续识别"自动抓帧 OCR，"停止并保存"生成脚本
3. 📚 知识库/问答/翻译：TF-IDF 检索、6 类问答、MyMemory 免费翻译、装备识别、规则提取、跨游戏切换
4. 🎮 操作执行：手动触发脚本
5. 🛡 AI 队友（双轨）：语音/文字指令 → inject；主轨脚本/辅轨 NitroGen
6. 🎯 策略教练：5 类建议 + TTS + 音乐识别
7. 🔍 识别层/问答：多模态状态问答
8. 📜 脚本仓库/自适应：GitHub 订阅、异常诊断、回放导出对比
9. 🔁 自动化编排：cron 定时任务
10. 🧩 插件管理：JS 脚本加载
11. 🕹 游戏管理：窗口枚举、启动游戏
12. 📊 仪表盘/应急：1Hz 监控、安全停机、网络检测、UI 变化检测
13. 🎬 高光剪辑：真实帧录制
14. 🧠 AI 设置：OpenAI 兼容 API
15. ⚙ 通用设置：拟人化强度、截图方式

### 核心工作流
1. 攻略浏览器 → 输入 URL → 持续识别(2FPS) → 停止并保存
2. 生成 User/AutoFight/*.txt + User/ScriptGroup/*/main.js
3. 步骤入知识库，脚本复制到 generated_scripts/ 待上传

### 安全机制
- 紧急停机：30+ 风险词 → 500ms 停注入
- 输入拟人化：±15% 抖动 + 10% 长停顿
- 卡死脱离：方向键→跳跃→冲刺
- 网络检测：两帧 MSE < 5 判定卡住

### 端口
5003 NitroGen / 5004 vision / 5005 stream / 5555 ZMQ
