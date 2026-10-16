"""Pack enemy frames into a sheet of px-square frames + Aseprite-style JSON.
frames_dir holds either the frames of one clip, or one subfolder per facing (s, se, e, ne, n, nw, w,
sw, as render_enemy_loop.py dirs=8 writes). Each facing is a row tagged <clip>_<facing>; the east row
is also tagged <clip>, which is what EnemyAnimator plays (it mirrors east for west).
Frames are trimmed to their shared bounding box, fit into px with a 1px margin and palette-reduced
together.
Usage: python make_enemy_sheet.py frames_dir out_dir name px colors frame_ms [clip]"""
import glob, json, os, sys
from PIL import Image

src, out, name, px, colors, ms = sys.argv[1:7]
px, colors, ms = int(px), int(colors), int(ms)
clip = sys.argv[7] if len(sys.argv) > 7 else "walk"
os.makedirs(out, exist_ok=True)
FACINGS = ["s", "se", "e", "ne", "n", "nw", "w", "sw"]
rows = [f for f in FACINGS if os.path.isdir(os.path.join(src, f))] or [""]
per_row = [sorted(glob.glob(os.path.join(src, r, "*.png"))) for r in rows]
n = len(per_row[0])
ims = [Image.open(f).convert("RGBA") for files in per_row for f in files]

# Shared bbox so the creature doesn't jitter between frames
l, t, r, b = 1e9, 1e9, 0, 0
for im in ims:
    bb = im.getchannel("A").point(lambda v: 255 if v > 30 else 0).getbbox()
    l, t, r, b = min(l, bb[0]), min(t, bb[1]), max(r, bb[2]), max(b, bb[3])
side = max(r - l, b - t)
inner = px - 2
frames = []
for im in ims:
    sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    sq.paste(im.crop((l, t, r, b)), ((side - (r - l)) // 2, side - (b - t)))  # feet on the bottom
    sm = sq.resize((inner, inner), Image.LANCZOS)
    fr = Image.new("RGBA", (px, px), (0, 0, 0, 0)); fr.paste(sm, (1, 1))
    frames.append((fr, fr.getchannel("A").point(lambda v: 255 if v > 110 else 0)))

mosaic = Image.new("RGB", (px * len(frames), px), (0, 0, 0))
for i, (im, a) in enumerate(frames):
    mosaic.paste(im.convert("RGB"), (i * px, 0), a)
pal = mosaic.quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGB")
sheet = Image.new("RGBA", (px * n, px * len(rows)), (0, 0, 0, 0))
pos = lambda i: ((i % n) * px, (i // n) * px)
for i, (im, a) in enumerate(frames):
    fr = pal.crop((i * px, 0, (i + 1) * px, px)).convert("RGBA"); fr.putalpha(a)
    sheet.paste(fr, pos(i))
sheet.save(os.path.join(out, name + ".png"))

tags = []
for r, f in enumerate(rows):
    span = {"from": r * n, "to": r * n + n - 1, "direction": "forward"}
    if f:
        tags.append({"name": f"{clip}_{f}", **span})
    if f in ("", "e"):
        tags.append({"name": clip, **span})
d = {"frames": [{"filename": str(i), "frame": {"x": pos(i)[0], "y": pos(i)[1], "w": px, "h": px}, "rotated": False,
                 "trimmed": False, "spriteSourceSize": {"x": 0, "y": 0, "w": px, "h": px},
                 "sourceSize": {"w": px, "h": px}, "duration": ms} for i in range(len(frames))],
     "meta": {"image": name + ".png", "format": "RGBA8888", "size": {"w": sheet.width, "h": sheet.height}, "scale": "1",
              "frameTags": tags}}
json.dump(d, open(os.path.join(out, name + ".json"), "w"), indent=1)
print(name, sheet.size, len(frames), "frames,", len(rows), "facings")
