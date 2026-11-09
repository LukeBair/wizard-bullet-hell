"""Generates pixel-art app icon options in the Dusk palette. Run: python3 make_icons.py <out_dir> (needs pillow)."""
import math, random, sys, os
from PIL import Image, ImageDraw

def hx(v): return ((v >> 16) & 255, (v >> 8) & 255, v & 255, 255)
GROUND, NAVY, SAGE, DSAGE, CREAM, CORAL, WINE = map(hx, (0x1d1a2b, 0x2b2f4a, 0x6f9b7a, 0x3e5a4e, 0xf0d2bc, 0xe5555f, 0x8c3446))
DWINE = hx(0x5a2230)
N = 64

def canvas(bg=GROUND):
    im = Image.new("RGBA", (N, N), bg); return im, ImageDraw.Draw(im)

BAYER = ((0, 8, 2, 10), (12, 4, 14, 6), (3, 11, 1, 9), (15, 7, 13, 5))

def dither_sky(d, top, bottom, y0=0, y1=N):
    for y in range(y0, y1):
        t = (y - y0) / max(1, y1 - y0 - 1)
        for x in range(N):
            d.point((x, y), fill=bottom if t * 16 > BAYER[y % 4][x % 4] else top)

def stars(d, seed, n=22, ymax=40):
    r = random.Random(seed)
    for _ in range(n):
        x, y = r.randrange(2, N - 2), r.randrange(2, ymax)
        d.point((x, y), fill=CREAM)
        if r.random() < 0.25:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)): d.point((x + dx, y + dy), fill=CREAM)

def hat(d, cx, base_y, w=20, h=26, lean=5, brim=1.35):
    """Pointy wizard hat: wine cone with bent tip, coral band, wide brim."""
    hw = w // 2
    tip = (cx + lean, base_y - h)
    mid = (cx + lean // 2 + 1, base_y - h // 2)
    d.polygon([(cx - hw, base_y), (cx + hw, base_y), (mid[0] + 3, mid[1]), (tip[0] + 2, tip[1] + 4), tip, (mid[0] - 4, mid[1] - 2)], fill=WINE)
    d.polygon([(cx - hw, base_y), (cx - hw // 3, base_y), (mid[0] - 4, mid[1] - 2), tip], fill=DWINE)
    d.polygon([(tip[0] - 1, tip[1] - 2), (tip[0] + 3, tip[1] + 1), (tip[0] + 1, tip[1] + 4)], fill=WINE)
    bh = 4
    d.rectangle([cx - hw + 1, base_y - bh, cx + hw - 1, base_y - 1], fill=CORAL)
    d.rectangle([cx - 2, base_y - bh, cx + 2, base_y - 1], fill=CREAM)  # buckle
    d.rectangle([cx - 1, base_y - bh + 1, cx + 1, base_y - 2], fill=CORAL)
    bw = int(hw * brim)
    d.ellipse([cx - bw, base_y - 3, cx + bw, base_y + 4], fill=WINE)
    d.ellipse([cx - bw + 2, base_y - 3, cx + bw - 2, base_y + 1], fill=DWINE)
    d.rectangle([cx - hw + 1, base_y - bh, cx + hw - 1, base_y - 1], fill=CORAL)
    d.rectangle([cx - 2, base_y - bh, cx + 2, base_y - 1], fill=CREAM)
    d.rectangle([cx - 1, base_y - bh + 1, cx + 1, base_y - 2], fill=CORAL)

def bullet(d, x, y, r=1, col=CORAL, core=CREAM):
    d.ellipse([x - r, y - r, x + r, y + r], fill=col)
    if r >= 2: d.point((x, y), fill=core)

def moon(d, cx, cy, r, col=CREAM, bite=None):
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=col)
    if bite: d.ellipse([bite[0] - bite[2], bite[1] - bite[2], bite[0] + bite[2], bite[1] + bite[2]], fill=bite[3])

def finish(im, name, out):
    big = im.resize((1024, 1024), Image.NEAREST)
    big.save(os.path.join(out, name + ".png")); return big

def opt1(out):
    im, d = canvas(); dither_sky(d, GROUND, NAVY, 0, 46); stars(d, 3)
    moon(d, 38, 25, 15)
    d.ellipse([30, 16, 36, 22], fill=hx(0xd9b8a2)); d.ellipse([44, 28, 48, 32], fill=hx(0xd9b8a2))
    d.ellipse([-10, 50, 80, 90], fill=DSAGE); d.ellipse([-20, 55, 50, 95], fill=SAGE)
    hat(d, 26, 47, 22, 30, 6)
    for i, (x, y) in enumerate(((50, 8), (56, 14), (12, 12))): bullet(d, x, y, 1)
    return finish(im, "icon_1_moonlit_hat", out)

def opt2(out):
    im, d = canvas(); dither_sky(d, GROUND, hx(0x241f38), 0, N)
    cx, cy = 32, 32
    # glow behind hat
    for r, c in ((26, NAVY), (19, hx(0x3a3358))): d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=c)
    # spiral barrage
    for ring, (R, n, off, r, col) in enumerate(((29, 14, 0.0, 2, CORAL), (23, 11, 0.3, 1, CREAM), (17, 8, 0.1, 1, CORAL))):
        for i in range(n):
            a = 2 * math.pi * i / n + off
            bullet(d, round(cx + R * math.cos(a)), round(cy + R * math.sin(a)), r, col)
    hat(d, 31, 40, 20, 26, 5)
    return finish(im, "icon_2_barrage", out)

def opt3(out):
    im, d = canvas(NAVY); 
    for r in range(30, 0, -6): d.ellipse([32 - r, 32 - r, 32 + r, 32 + r], fill=GROUND if (r // 6) % 2 else hx(0x242843))
    pts = lambda R, rot: [(32 + R * math.cos(rot + k * math.pi / 3), 32 + R * math.sin(rot + k * math.pi / 3)) for k in range(6)]
    p = pts(25, -math.pi / 2)
    for tri in ([p[0], p[2], p[4]], [p[1], p[3], p[5]]):
        d.line(tri + [tri[0]], fill=CREAM, width=1)
    d.ellipse([7, 7, 57, 57], outline=SAGE)
    for p in pts(25, -math.pi / 2): bullet(d, round(p[0]), round(p[1]), 2)
    d.regular_polygon((32, 32, 9), 6, rotation=0, fill=CORAL); d.regular_polygon((32, 32, 5), 6, fill=CREAM)
    d.point((32, 32), fill=CORAL)
    return finish(im, "icon_3_sigil", out)

def opt4(out):
    im, d = canvas(DSAGE); dither_sky(d, GROUND, DSAGE, 0, 64)
    # forest silhouettes
    r = random.Random(7)
    for x in range(-4, 70, 9):
        h = r.randrange(14, 26); d.polygon([(x, 64), (x + 9, 64), (x + 4, 64 - h - 14)], fill=hx(0x16202a))
    moon(d, 50, 12, 7, CREAM)
    # face: beard + glowing eyes under a big hat
    d.polygon([(18, 40), (46, 40), (44, 52), (32, 63), (20, 52)], fill=CREAM)
    d.polygon([(18, 40), (26, 40), (24, 55), (20, 52)], fill=hx(0xd9b8a2))
    d.rectangle([22, 36, 42, 44], fill=GROUND)
    for ex in (25, 36): d.rectangle([ex, 39, ex + 3, 41], fill=CORAL); d.point((ex + 1, 39), fill=CREAM)
    hat(d, 32, 33, 26, 28, 7, 1.15)
    for x, y in ((8, 24), (56, 30), (12, 50)): bullet(d, x, y, 1)
    return finish(im, "icon_4_elder", out)

if __name__ == "__main__":
    out = sys.argv[1]; os.makedirs(out, exist_ok=True)
    ims = [f(out) for f in (opt1, opt2, opt3, opt4)]
    sheet = Image.new("RGBA", (4 * 532, 532), (24, 24, 28, 255))
    for i, im in enumerate(ims):
        t = im.resize((512, 512), Image.NEAREST)
        m = Image.new("L", (512, 512), 0); ImageDraw.Draw(m).rounded_rectangle([0, 0, 511, 511], 112, fill=255)
        sheet.paste(t, (10 + i * 532, 10), m)
    sheet.save(os.path.join(out, "preview.png"))
