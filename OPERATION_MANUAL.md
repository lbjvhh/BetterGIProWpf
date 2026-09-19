# BetterGI Pro 操作手册（完整版）

## 目录
1. 安装与启动
2. 首页
3. 攻略浏览器（2FPS 持续识别）
4. 知识库 / 问答 / 翻译
5. 操作执行
6. AI 队友（双轨）
7. 策略教练
8. 识别层 / 问答
9. 脚本仓库 / 自适应
10. 自动化编排
11. 插件管理
12. 游戏管理
13. 仪表盘 / 应急
14. 高光剪辑
15. AI 设置
16. 通用设置
17. 安全机制
18. 常见问题

## 1. 安装与启动

### 目录
C:\better\BetterGIProWpf-App\ 发布版
├── BetterGIProWpf.exe
├── Models\ ng.pt / yolov8s.onnx / ppocrv5 / whisper
└── User\ AutoFight / ScriptGroup / Replays / knowledge.json

### 启动
1. 双击 BetterGIProWpf.exe
2. 如需真实模型：python vision_server.py (5004) + python stream_bridge.py (5005)

### 端口
5003 NitroGen / 5004 vision / 5005 stream / 5555 ZMQ

## 2. 首页
服务状态概览：5004/5005/5003 是否在线、模型加载状态

## 3. 攻略浏览器（📺）
- 输入 B 站/YouTube URL
- 点▶ 持续识别(2FPS)：每 500ms 抓 WebView2 帧 → OCR → 关键词匹配
- 点⏹ 停止并保存：写 AutoFight/*.txt + ScriptGroup/*/main.js + 入知识库 + 复制到 generated_scripts/

## 4. 知识库（📚）
- 语义检索（TF-IDF）
- 6 类游戏状态问答
- MyMemory 免费翻译
- 装备识别（YOLO）
- 游戏规则提取（15 关键词 × 5 类）
- 跨游戏切换（原神/鸣潮）
- 社区评分（GitHub Issues）
- 资源采集点识别

## 5. 操作执行（🎮）
手动触发 BetterGI 脚本

## 6. AI 队友（🛡）
- 自然语言指令 → GoalToKeys 映射
- 主轨：stream_bridge /inject
- 辅轨：NitroGen /predict
- 热键接管

## 7. 策略教练（🎯）
- 5 类建议：元素反应/体力/路线/技能/弱点
- TTS 语音播报
- 音乐场景识别（FFT 三分类）

## 8. 识别层/问答（🔍）
多模态游戏状态问答

## 9. 脚本仓库/自适应（📜）
- 上游脚本订阅（repo/js, pathing, combat, tcg）
- 异常诊断知识库（7 模式 × 策略）
- 回放导出对比

## 10. 自动化编排（🔁）
cron 定时任务

## 11. 插件管理（🧩）
JS 脚本加载

## 12. 游戏管理（🕹）
窗口枚举 + 启动游戏

## 13. 仪表盘/应急（📊）
- 1Hz CPU/内存/延迟指标
- 安全停机（30+ 风险词）
- UI 变化检测（基线 MSE）
- 网络检测（两帧 MSE < 5）

## 14. 高光剪辑（🎬）
真实帧录制 10s

## 15. AI 设置（🧠）
OpenAI 兼容 API

## 16. 通用设置（⚙）
拟人化强度 / 截图方式

## 17. 安全机制
- 紧急停机：30+ 风险词 → 500ms 停注入
- 输入拟人化：±15% 抖动 + 10% 长停顿
- 卡死脱离：方向键→跳跃→冲刺→地图重置
- 网络检测：帧 MSE
- UI 变化检测

## 18. 常见问题
- 识别无结果：检查 vision_server
- 注入无效：检查游戏窗口前台 + 安全软件
- 脚本位置：User/AutoFight
- GitHub 上传：GITHUB_TOKEN 环境变量
- 版本更新：重新设 UI 基线
- 封号风险：截图识别不读写内存，风险自负

## 附录
GitHub: https://github.com/lbjvhh/BetterGIProWpf
