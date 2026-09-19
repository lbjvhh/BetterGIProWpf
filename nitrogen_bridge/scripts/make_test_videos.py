"""Generate 5 baseline test videos (walk/combat/collect/dialog/teleport)."""
import cv2, numpy as np
from pathlib import Path
OUT = Path(r"C:\better\nitrogen_bridge\test_videos"); OUT.mkdir(parents=True, exist_ok=True)
W, H, FPS, SEC = 1280, 720, 15, 8; FOURCC = cv2.VideoWriter_fourcc(*"mp4v")
def make(name, scenes):
    vw = cv2.VideoWriter(str(OUT / f"{name}.mp4"), FOURCC, FPS, (W, H)); per = SEC * FPS // len(scenes)
    for text, bg in scenes:
        for _ in range(per):
            img = np.full((H, W, 3), bg, dtype=np.uint8)
            cv2.putText(img, text, (80, 360), cv2.FONT_HERSHEY_SIMPLEX, 1.8, (255, 255, 255), 3, cv2.LINE_AA); vw.write(img)
    vw.release(); print(f"{name}.mp4")
make("01_walk", [("Running: Windrise", (30, 60, 30)), ("Path to Statue", (40, 80, 40))])
make("02_combat", [("Combat: Slime", (60, 30, 30)), ("Elemental Burst Q", (80, 40, 40))])
make("03_collect", [("Collect: Crystal", (40, 60, 80)), ("Pick up ore", (50, 70, 90))])
make("04_dialog", [("Talk: NPC", (30, 30, 60)), ("Quest accepted", (40, 40, 80))])
make("05_teleport", [("Teleport: Anchors", (60, 60, 30)), ("Loading...", (80, 80, 40))])
