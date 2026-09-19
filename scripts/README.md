# BetterGIProWpf — 云端脚本仓库（P0-2）

本目录是 BetterGIProWpf 的脚本云仓库，与软件内「脚本仓库」页联动：

- `scripts/combat/` — 战斗脚本（AutoFight TXT/JSON）
- `scripts/pathing/` — 地图追踪（waypoints JSON）
- `scripts/js/` — BetterGI JS 脚本（manifest.json + main.js）
- `scripts/tcg/` — 七圣召唤策略
- `index.json` — 脚本元数据索引（名称/作者/游戏版本/标签/分类/路径/评分）

## 上传脚本（软件内一键）

1. 在「本地脚本」页填写 GitHub Personal Access Token（classic，`repo` 权限）
2. 选择分类，点「上传脚本」
3. 软件调用 GitHub Contents API 上传文件并自动更新 `index.json`

> PAT 获取：GitHub → Settings → Developer settings → Personal access tokens → Tokens (classic) → Generate new token，勾选 `repo`。
