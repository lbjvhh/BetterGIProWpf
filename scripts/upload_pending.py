#!/usr/bin/env python3
"""
P0-2: 把 C:\\better\\generated_scripts\\ 下的脚本批量上传到 GitHub
仓库 lbjvhh/BetterGIProWpf 的 generated_scripts/ 目录。

用法:
  set GITHUB_TOKEN=ghp_xxx
  python scripts/upload_pending.py

需要 token 有 repo 权限（classic PAT）。
上传成功后本地文件会移动到 uploaded/ 子目录。
"""
import base64
import os
import sys
import time
from pathlib import Path

import requests

REPO = "lbjvhh/BetterGIProWpf"
BRANCH = "main"
SRC = Path(r"C:\better\generated_scripts")
UPLOADED = SRC / "uploaded"
API = f"https://api.github.com/repos/{REPO}/contents"


def main():
    token = os.environ.get("GITHUB_TOKEN") or os.environ.get("GH_TOKEN")
    if not token:
        print("[!] 请先 set GITHUB_TOKEN=ghp_xxx（classic PAT, repo 权限）")
        sys.exit(1)

    if not SRC.exists():
        print(f"[!] 目录不存在: {SRC}")
        sys.exit(0)

    UPLOADED.mkdir(exist_ok=True)
    headers = {
        "Authorization": f"token {token}",
        "Accept": "application/vnd.github+json",
    }

    files = sorted(SRC.glob("auto_*.txt"))
    if not files:
        print("[i] 没有待上传脚本")
        return

    print(f"[i] 待上传 {len(files)} 个脚本")
    ok = 0
    for fp in files:
        content = fp.read_text(encoding="utf-8", errors="ignore")
        if len(content) < 50:
            print(f"  [skip] {fp.name} 内容过短")
            continue
        path = f"generated_scripts/{fp.name}"
        url = f"{API}/{path}"
        body = {
            "message": f"auto: upload {fp.name}",
            "content": base64.b64encode(content.encode("utf-8")).decode(),
            "branch": BRANCH,
        }
        r = requests.put(url, headers=headers, json=body, timeout=30)
        if r.status_code in (200, 201):
            print(f"  [ok] {fp.name}")
            ok += 1
            fp.rename(UPLOADED / fp.name)
        else:
            print(f"  [fail] {fp.name}: {r.status_code} {r.text[:200]}")
        time.sleep(0.5)

    print(f"[done] {ok}/{len(files)} 上传成功")


if __name__ == "__main__":
    main()
