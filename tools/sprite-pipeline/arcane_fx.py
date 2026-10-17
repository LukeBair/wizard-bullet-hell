"""Draws the Arcane Missile pixel art: a centred pulsing bolt head (4 frames) and an animated hit (8 frames).
Run with a Python that has Pillow:  python arcane_fx.py [outdir]
The bolt's trail is not drawn here: SpellVfx emits it as pixel particles so it matches the game's own."""
import math, sys
from PIL import Image

OUT = (sys.argv[1] if len(sys.argv) > 1 else "Assets/Resources/Art/Dusk") + "/"
PAL = [(255, 255, 255), (232, 214, 255), (190, 148, 255), (138, 84, 236), (90, 43, 176), (52, 22, 110)]

def plot(im, x, y, ci, a=255):
    if 0 <= x < im.width and 0 <= y < im.height:
        r, g, b = PAL[ci]; im.putpixel((x, y), (r, g, b, a))

def head(frame):
    n = 15; c = 7
    im = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    pulse = [0.0, 0.6, 1.0, 0.6][frame]
    r = [1.6 + pulse * 0.5, 2.8 + pulse * 0.6, 4.0 + pulse * 0.7, 5.2 + pulse * 0.8, 6.4 + pulse * 0.6]
    for y in range(n):
        for x in range(n):
            d = math.hypot(x - c, y - c)
            ang = math.atan2(y - c, x - c)
            # slow 4-point shimmer on the halo edge so it reads as energy, not a disc
            wob = 0.55 * math.sin(ang * 4 + frame * 1.57)
            if d <= r[0]: plot(im, x, y, 0)
            elif d <= r[1]: plot(im, x, y, 1)
            elif d <= r[2]: plot(im, x, y, 2)
            elif d <= r[3] + wob: plot(im, x, y, 3, 255)
            elif d <= r[4] + wob: plot(im, x, y, 4, 128)
    return im

def hit(frame):
    n = 41; c = 20
    im = Image.new("RGBA", (n, n), (0, 0, 0, 0))
    k = frame / 7.0
    ring_r = 4 + 12 * (1 - (1 - k) ** 2.2)
    thick = 4.4 - 3.2 * k
    flash = max(0.0, 1 - k * 2.0)
    for y in range(n):
        for x in range(n):
            d = math.hypot(x - c, y - c)
            ang = math.atan2(y - c, x - c)
            jag = 1.3 * math.sin(ang * 7 + frame * 2.3) + 0.8 * math.sin(ang * 3 - frame * 1.7)   # ragged edge, different each frame
            if flash > 0.05 and d <= 6.5 * flash: plot(im, x, y, 0)
            elif flash > 0.05 and d <= 9 * flash: plot(im, x, y, 1)
            elif d < ring_r - thick / 2 and k < 0.7:
                plot(im, x, y, 4, 128 if k < 0.45 else 0) if (x + y) % 2 == 0 or k < 0.3 else None   # soft inner glow
            rd = d - jag * (0.4 + 0.6 * k)
            if abs(rd - ring_r) <= thick / 2:
                t = (rd - ring_r) / (thick / 2)       # -1 inner .. +1 outer
                ci = 1 if (t < -0.2 and k < 0.4) else 2 if k < 0.6 else 3 if k < 0.85 else 4
                plot(im, x, y, ci, 255 if k < 0.6 or t < 0 else 128)
    # sparks fly outward past the ring
    for i in range(10):
        a = i * 0.628 + 0.3 * math.sin(i * 2.1)
        rr = ring_r + 2 + k * (4 + (i % 3) * 3)
        x = int(round(c + math.cos(a) * rr)); y = int(round(c + math.sin(a) * rr))
        if k < 0.95:
            plot(im, x, y, 0 if k < 0.35 else 2 if k < 0.7 else 3, 255 if k < 0.7 else 128)
    return im

for i in range(4): head(i).save(f"{OUT}vfx_arcane_head_{i}.png")
for i in range(8): hit(i).save(f"{OUT}vfx_arcane_hit_{i}.png")

# contact sheet, 6x, on forest green
sheet = Image.new("RGBA", (8 * 41 * 3, 41 * 3 + 15 * 3 + 8), (40, 62, 44, 255))
for i in range(8):
    f = hit(i).resize((41 * 3, 41 * 3), Image.NEAREST); sheet.alpha_composite(f, (i * 41 * 3, 0))
for i in range(4):
    f = head(i).resize((45, 45), Image.NEAREST); sheet.alpha_composite(f, (i * 60 + 5, 41 * 3 + 4))
sheet.save("/tmp/claude-501/arcane_sheet.png")
