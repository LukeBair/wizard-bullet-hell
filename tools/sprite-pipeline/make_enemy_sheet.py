"""Pack enemy frames into a sheet of px-square frames + Aseprite-style JSON.
frames_dir holds either the frames of one clip, or one subfolder per facing (s, se, e, ne, n, nw, w,
sw, as render_enemy_loop.py dirs=8 writes). Each facing is a row tagged <clip>_<facing>; the east row
is also tagged <clip>, which is what EnemyAnimator plays (it mirrors east for west).
Frames are trimmed to their shared bounding box, fit into px with a 1px margin and palette-reduced
together.
frames_dir can also hold one subfolder per clip (idle, walk, cast, death...), each with facing folders, as
render_boss_clips.py writes; every clip/facing becomes a <clip>_<facing> row. frame_ms is then either one
number or per-clip timings like walk=100,cast=70,death=90.
Usage: python make_enemy_sheet.py frames_dir out_dir name px colors frame_ms [clip]"""
import glob, json, os, sys
from PIL import Image

src, out, name, px, colors, ms_arg = sys.argv[1:7]
px, colors = int(px), int(colors)
clip = sys.argv[7] if len(sys.argv) > 7 else "walk"
ms_map = dict((k, int(v)) for k, v in (kv.split("=") for kv in ms_arg.split(","))) if "=" in ms_arg else {}
ms = int(ms_arg) if not ms_map else 90
os.makedirs(out, exist_ok=True)
FACINGS = ["s", "se", "e", "ne", "n", "nw", "w", "sw"]
clip_dirs = sorted(d for d in os.listdir(src) if os.path.isdir(os.path.join(src, d)) and d not in FACINGS)
CLIP_ORDER = ["idle", "walk", "cast", "hit", "death"]
clip_dirs.sort(key=lambda c: CLIP_ORDER.index(c) if c in CLIP_ORDER else 99)
rows = []   # (clip, facing, files)
for c in clip_dirs or [clip]:
    base = os.path.join(src, c) if clip_dirs else src
    for f in [f for f in FACINGS if os.path.isdir(os.path.join(base, f))] or [""]:
        rows.append((c, f, sorted(glob.glob(os.path.join(base, f, "*.png")))))
n = max(len(r[2]) for r in rows)
ims, slots = [], []   # slots: (row, column) of each frame
for ri, (_, _, files) in enumerate(rows):
    for ci, fpath in enumerate(files):
        ims.append(Image.open(fpath).convert("RGBA")); slots.append((ri, ci))

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
pos = lambda i: (slots[i][1] * px, slots[i][0] * px)
for i, (im, a) in enumerate(frames):
    fr = pal.crop((i * px, 0, (i + 1) * px, px)).convert("RGBA"); fr.putalpha(a)
    sheet.paste(fr, pos(i))
sheet.save(os.path.join(out, name + ".png"))

tags, durations, start = [], [], 0
for c, f, files in rows:
    span = {"from": start, "to": start + len(files) - 1, "direction": "forward"}
    if f:
        tags.append({"name": f"{c}_{f}", **span})
    if f in ("", "e"):
        tags.append({"name": c, **span})
    durations += [ms_map.get(c, ms)] * len(files)
    start += len(files)
d = {"frames": [{"filename": str(i), "frame": {"x": pos(i)[0], "y": pos(i)[1], "w": px, "h": px}, "rotated": False,
                 "trimmed": False, "spriteSourceSize": {"x": 0, "y": 0, "w": px, "h": px},
                 "sourceSize": {"w": px, "h": px}, "duration": durations[i]} for i in range(len(frames))],
     "meta": {"image": name + ".png", "format": "RGBA8888", "size": {"w": sheet.width, "h": sheet.height}, "scale": "1",
              "frameTags": tags}}
json.dump(d, open(os.path.join(out, name + ".json"), "w"), indent=1)
print(name, sheet.size, len(frames), "frames,", len(rows), "facings")
