# 自托管 GPU Runner 注册指引（P0-5）

游戏实跑回归（`game-regression` job）需要自托管 runner（云端 runner 无 GPU/游戏环境）。

## 1. 注册 runner

1. 打开仓库 Settings → Actions → Runners → **New self-hosted runner**
2. 选择 **Windows x64**，按页面命令在本机执行（示例）：

```bash
# 下载并解压 actions-runner 到 C:\actions-runner
cd C:\actions-runner
./config.cmd --url https://github.com/lbjvhh/BetterGIProWpf --token <页面提供的TOKEN>
./run.cmd
```

3. 配置 runner 标签：`windows` 与 `gpu`（与 workflow `runs-on: [self-hosted, windows, gpu]` 匹配）

## 2. 前置环境（runner 机器）

- Python 3.11+（`C:\Users\as123\AppData\Local\Doubao\...\python.exe` 亦可）
- 项目依赖：`pip install -r nitrogen_bridge/requirements.txt`
- 原神客户端（离线模式即可，用于窗口捕获回归）
- 虚拟手柄驱动（ViGEmBus，若启用云手柄注入）

## 3. 触发方式

- 每次 push：仅云端 `unit-tests` + `dotnet-build`（云端 runner 自动跑）
- 每周日 02:00 UTC：完整 `game-regression`（需自托管 runner 在线）
- 手动：Actions → Regression → Run workflow

## 4. 验证

runner 注册成功后，在 Actions 页面能看到 `self-hosted` 标签；手动触发一次 workflow 即可看到 `game-regression` job 开始执行 `scripts/run_regression.py --videos test_videos/ --report report.json`。
