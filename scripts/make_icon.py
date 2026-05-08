#!/usr/bin/env python3
"""生成马卡龙糖果风格的 macOS 应用图标。

输出 1024x1024 PNG，再由 make_icns.sh 打成 .icns。
设计：圆角方形（粉→紫→薄荷渐变）+ 中央糖果造型（红蓝白）+ 顶部 "MKV" 字。
"""

from PIL import Image, ImageDraw, ImageFont, ImageFilter
import os
import sys

SIZE = 1024
OUT = sys.argv[1] if len(sys.argv) > 1 else "build/icon.png"
os.makedirs(os.path.dirname(OUT) or ".", exist_ok=True)


def make_gradient(w, h, top, mid, bot):
    img = Image.new("RGB", (w, h), top)
    px = img.load()
    for y in range(h):
        if y < h / 2:
            t = y / (h / 2)
            r = int(top[0] + (mid[0] - top[0]) * t)
            g = int(top[1] + (mid[1] - top[1]) * t)
            b = int(top[2] + (mid[2] - top[2]) * t)
        else:
            t = (y - h / 2) / (h / 2)
            r = int(mid[0] + (bot[0] - mid[0]) * t)
            g = int(mid[1] + (bot[1] - mid[1]) * t)
            b = int(mid[2] + (bot[2] - mid[2]) * t)
        for x in range(w):
            px[x, y] = (r, g, b)
    return img


def rounded_mask(w, h, radius):
    m = Image.new("L", (w, h), 0)
    d = ImageDraw.Draw(m)
    d.rounded_rectangle((0, 0, w, h), radius=radius, fill=255)
    return m


def draw_candy(canvas, cx, cy, body_size):
    """画马卡龙糖果：椭圆主体 + 两侧扭结包装纸 + 高光"""
    d = ImageDraw.Draw(canvas, "RGBA")

    # 阴影
    shadow = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    body_rect_shadow = (cx - body_size, cy - body_size * 0.42 + 20,
                        cx + body_size, cy + body_size * 0.42 + 20)
    sd.ellipse(body_rect_shadow, fill=(110, 80, 130, 80))
    shadow = shadow.filter(ImageFilter.GaussianBlur(radius=18))
    canvas.alpha_composite(shadow)

    # 包装纸（左侧三角扭结）
    pink = (255, 168, 192, 255)
    pink_dark = (230, 130, 160, 255)
    twist_w = body_size * 0.55
    twist_h = body_size * 0.42

    # 左 twist
    left_pts = [
        (cx - body_size * 0.95, cy),                    # 尖端
        (cx - body_size + 5, cy - twist_h * 0.85),      # 上中
        (cx - body_size * 0.42, cy - twist_h * 0.18),   # 与主体相接（上）
        (cx - body_size * 0.42, cy + twist_h * 0.18),   # 与主体相接（下）
        (cx - body_size + 5, cy + twist_h * 0.85),      # 下中
    ]
    d.polygon(left_pts, fill=pink)

    # 左 twist 折痕（深粉描线）
    d.line([(cx - body_size + 5, cy - twist_h * 0.85),
            (cx - body_size * 0.6, cy - twist_h * 0.10)],
           fill=pink_dark, width=6)
    d.line([(cx - body_size + 5, cy + twist_h * 0.85),
            (cx - body_size * 0.6, cy + twist_h * 0.10)],
           fill=pink_dark, width=6)

    # 右 twist（镜像）
    right_pts = [
        (cx + body_size * 0.95, cy),
        (cx + body_size - 5, cy - twist_h * 0.85),
        (cx + body_size * 0.42, cy - twist_h * 0.18),
        (cx + body_size * 0.42, cy + twist_h * 0.18),
        (cx + body_size - 5, cy + twist_h * 0.85),
    ]
    d.polygon(right_pts, fill=pink)
    d.line([(cx + body_size - 5, cy - twist_h * 0.85),
            (cx + body_size * 0.6, cy - twist_h * 0.10)],
           fill=pink_dark, width=6)
    d.line([(cx + body_size - 5, cy + twist_h * 0.85),
            (cx + body_size * 0.6, cy + twist_h * 0.10)],
           fill=pink_dark, width=6)

    # 主体（椭圆）— 渐变模拟（实际是分层椭圆）
    body_rect = (cx - body_size * 0.62, cy - body_size * 0.42,
                 cx + body_size * 0.62, cy + body_size * 0.42)

    # 底色：从浅粉到中粉的简单分层
    layers = [
        ((255, 200, 220), 0),
        ((255, 180, 205), 8),
        ((255, 160, 190), 16),
    ]
    for color, inset in layers:
        rect = (body_rect[0] + inset, body_rect[1] + inset,
                body_rect[2] - inset, body_rect[3] - inset)
        d.ellipse(rect, fill=color + (255,))

    # 主体描边
    d.ellipse(body_rect, outline=(220, 130, 160, 255), width=4)

    # 高光（左上小椭圆，白色淡）
    hl_w = body_size * 0.32
    hl_h = body_size * 0.16
    hl_cx = cx - body_size * 0.18
    hl_cy = cy - body_size * 0.15
    d.ellipse((hl_cx - hl_w / 2, hl_cy - hl_h / 2,
               hl_cx + hl_w / 2, hl_cy + hl_h / 2),
              fill=(255, 255, 255, 200))

    # 中央小亮点
    sp_size = body_size * 0.05
    d.ellipse((cx - sp_size, cy - sp_size * 0.6,
               cx + sp_size, cy + sp_size * 0.6),
              fill=(255, 255, 255, 230))


def main():
    # 渐变背景
    bg = make_gradient(SIZE, SIZE,
                       top=(255, 226, 234),     # 樱花粉
                       mid=(232, 222, 248),     # 薰衣草
                       bot=(212, 238, 226))     # 薄荷

    # 圆角裁剪
    mask = rounded_mask(SIZE, SIZE, radius=int(SIZE * 0.225))
    canvas = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    canvas.paste(bg, (0, 0), mask)

    # 顶部柔光
    glow = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.ellipse((-int(SIZE * 0.2), -int(SIZE * 0.55),
                int(SIZE * 1.2), int(SIZE * 0.55)),
               fill=(255, 255, 255, 70))
    glow = glow.filter(ImageFilter.GaussianBlur(radius=40))
    canvas.alpha_composite(glow)

    # 糖果
    draw_candy(canvas, cx=SIZE // 2, cy=int(SIZE * 0.55), body_size=int(SIZE * 0.30))

    # 顶部 "MKV" 字
    try:
        font_path = "/System/Library/Fonts/Helvetica.ttc"
        font = ImageFont.truetype(font_path, int(SIZE * 0.13))
    except Exception:
        font = ImageFont.load_default()
    d = ImageDraw.Draw(canvas)
    text = "MKV"
    bbox = d.textbbox((0, 0), text, font=font)
    tw = bbox[2] - bbox[0]
    th = bbox[3] - bbox[1]
    tx = (SIZE - tw) // 2
    ty = int(SIZE * 0.18) - bbox[1]
    # 文字阴影
    d.text((tx + 3, ty + 4), text, font=font, fill=(110, 80, 130, 110))
    # 主文字
    d.text((tx, ty), text, font=font, fill=(74, 74, 90, 255))

    canvas.save(OUT)
    print(f"✓ wrote {OUT} ({SIZE}x{SIZE})")


if __name__ == "__main__":
    main()
