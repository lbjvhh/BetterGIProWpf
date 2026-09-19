# -*- coding: utf-8 -*-
"""
原神专属 YOLO —— 合成数据集生成器
用程序化绘制的"原神风格"图标（类别形状/颜色模板）在合成背景上生成带 YOLO 标注的数据集。
无需手工标注：每张图自动生成 YOLO txt 标签（class cx cy w h 归一化）。
运行: python genshin_yolo_dataset.py [张数] [输出目录]
"""
import os, sys, math, random
from PIL import Image, ImageDraw, ImageFilter

# 原神元素类别（与 vision_server.GENSHIN_CLASSES 对齐）
CLASSES = [
    "角色", "敌人", "Boss", "宝箱", "传送锚点", "七天神像", "NPC",
    "采集物", "圣遗物", "武器", "材料", "任务", "技能", "HUD", "地图", "秘境",
]
N_CLS = len(CLASSES)

# 每个类别的图标绘制方案：(形状, 主色, 副色)
ICONS = {
    "角色":   ("human",  (66, 133, 244), (30, 60, 120)),
    "敌人":   ("triangle", (220, 60, 60), (120, 20, 20)),
    "Boss":   ("star",   (200, 40, 200), (90, 10, 90)),
    "宝箱":   ("rect",   (180, 120, 60), (90, 55, 25)),
    "传送锚点": ("ring",  (80, 180, 255), (20, 80, 160)),
    "七天神像": ("diamond", (80, 220, 200), (20, 110, 100)),
    "NPC":    ("human",  (255, 180, 80), (140, 90, 30)),
    "采集物": ("circle", (80, 200, 90), (20, 110, 40)),
    "圣遗物": ("diamond", (170, 90, 220), (80, 30, 120)),
    "武器":   ("bar",    (190, 190, 200), (90, 90, 100)),
    "材料":   ("circle", (240, 210, 60), (130, 110, 20)),
    "任务":   ("flag",   (90, 200, 120), (30, 110, 60)),
    "技能":   ("star",   (255, 170, 40), (140, 80, 10)),
    "HUD":    ("bar",    (120, 160, 255), (40, 60, 130)),
    "地图":   ("ring",   (140, 220, 140), (40, 120, 60)),
    "秘境":   ("portal", (120, 90, 220), (40, 25, 110)),
}


def draw_icon(draw, cx, cy, size, shape, color):
    """在 (cx,cy) 绘制 size 大小的图标。"""
    r = size / 2
    x0, y0, x1, y1 = cx - r, cy - r, cx + r, cy + r
    if shape == "circle":
        draw.ellipse([x0, y0, x1, y1], fill=color)
    elif shape == "rect":
        draw.rounded_rectangle([x0, y0, x1, y1], radius=size * 0.12, fill=color)
    elif shape == "triangle":
        draw.polygon([(cx, y0), (x0, y1), (x1, y1)], fill=color)
    elif shape == "diamond":
        draw.polygon([(cx, y0), (x1, cy), (cx, y1), (x0, cy)], fill=color)
    elif shape == "ring":
        draw.ellipse([x0, y0, x1, y1], outline=color, width=max(2, int(size * 0.12)))
    elif shape == "star":
        pts = []
        for i in range(10):
            ang = math.pi / 5 * i - math.pi / 2
            rr = r if i % 2 == 0 else r * 0.45
            pts.append((cx + rr * math.cos(ang), cy + rr * math.sin(ang)))
        draw.polygon(pts, fill=color)
    elif shape == "bar":
        draw.rounded_rectangle([x0, cy - r * 0.3, x1, cy + r * 0.3], radius=size * 0.08, fill=color)
    elif shape == "human":
        draw.ellipse([cx - r * 0.35, y0, cx + r * 0.35, cy - r * 0.1], fill=color)
        draw.rounded_rectangle([cx - r * 0.5, cy - r * 0.1, cx + r * 0.5, y1], radius=size * 0.15, fill=color)
    elif shape == "flag":
        draw.line([cx, y0, cx, y1], fill=color, width=max(2, int(size * 0.1)))
        draw.polygon([(cx, y0), (cx + r * 1.1, y0 + r * 0.5), (cx, y1 * 0.25)], fill=color)
    elif shape == "portal":
        draw.ellipse([x0, y0, x1, y1], outline=color, width=max(3, int(size * 0.15)))
        draw.ellipse([cx - r * 0.4, cy - r * 0.4, cx + r * 0.4, cy + r * 0.4], fill=color)


def gen_background(w, h):
    """原神风格渐变背景：天空/草地/洞窟/城镇随机。"""
    kind = random.choice(["sky", "grass", "cave", "town"])
    img = Image.new("RGB", (w, h))
    px = img.load()
    for y in range(h):
        for x in range(w):
            if kind == "sky":
                t = y / h
                c = (int(120 + 90 * (1 - t)), int(160 + 60 * (1 - t)), int(220 - 60 * t))
            elif kind == "grass":
                t = y / h
                c = (int(70 + 40 * t), int(150 - 40 * t), int(60 + 20 * t))
            elif kind == "cave":
                c = (60 + (x % 13), 55 + (y % 11), 70)
            else:
                c = (140, 130, 120)
            px[x, y] = c
    # 噪声纹理
    for _ in range(w * h // 8):
        x, y = random.randrange(w), random.randrange(h)
        r, g, b = px[x, y]
        d = random.randint(-12, 12)
        px[x, y] = (max(0, min(255, r + d)), max(0, min(255, g + d)), max(0, min(255, b + d)))
    return img


def gen_one(idx, out_dir, w=640, h=640):
    img = gen_background(w, h)
    draw = ImageDraw.Draw(img)
    labels = []
    n_obj = random.randint(1, 6)
    used = set()
    for _ in range(n_obj):
        cls_name = random.choice(CLASSES)
        shape, color, _ = ICONS[cls_name]
        size = random.randint(28, 110)
        for _try in range(12):
            cx = random.randint(size // 2 + 10, w - size // 2 - 10)
            cy = random.randint(size // 2 + 10, h - size // 2 - 10)
            key = (cx // 80, cy // 80)
            if key not in used:
                used.add(key)
                break
        draw_icon(draw, cx, cy, size, shape, color)
        cls_id = CLASSES.index(cls_name)
        labels.append(f"{cls_id} {cx / w:.5f} {cy / h:.5f} {size / w:.5f} {size / h:.5f}")
    img = img.filter(ImageFilter.GaussianBlur(0.4))
    img.save(os.path.join(out_dir, "images", f"gen_{idx:05d}.jpg"), quality=92)
    with open(os.path.join(out_dir, "labels", f"gen_{idx:05d}.txt"), "w") as f:
        f.write("\n".join(labels) + "\n")


def main():
    n = int(sys.argv[1]) if len(sys.argv) > 1 else 300
    out = sys.argv[2] if len(sys.argv) > 2 else r"C:\better\genshin_yolo\dataset"
    os.makedirs(os.path.join(out, "images"), exist_ok=True)
    os.makedirs(os.path.join(out, "labels"), exist_ok=True)
    for i in range(n):
        gen_one(i, out)
        if (i + 1) % 50 == 0:
            print(f"  {i+1}/{n} 张", flush=True)
    # data.yaml
    yaml = f"path: {out}\ntrain: images\nval: images\nnc: {N_CLS}\nnames:\n"
    for i, c in enumerate(CLASSES):
        yaml += f"  {i}: {c}\n"
    with open(os.path.join(out, "data.yaml"), "w", encoding="utf-8") as f:
        f.write(yaml)
    print(f"数据集就绪: {out}（{n} 张, {N_CLS} 类）")


if __name__ == "__main__":
    main()
