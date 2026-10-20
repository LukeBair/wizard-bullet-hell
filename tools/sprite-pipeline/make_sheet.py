"""Pack rendered frames into the game's sheet layout (8 cols) + Aseprite-style JSON.
Usage: python make_sheet.py clips_dir out_dir px colors game_sheet.json
colors=0 keeps full colour and soft alpha (for big sheets the game resamples, PixelDensity.cs)."""
import glob, json, os, sys
from PIL import Image

clips, out, px, colors, ref = sys.argv[1], sys.argv[2], int(sys.argv[3]), int(sys.argv[4]), sys.argv[5]
os.makedirs(out, exist_ok=True)
files = sorted(glob.glob(os.path.join(clips, "*.png")))
frames = []
for f in files:
    im = Image.open(f).convert("RGBA").resize((px, px), Image.BOX)
    a = im.getchannel("A") if colors == 0 else im.getchannel("A").point(lambda v: 255 if v > 110 else 0)
    frames.append((im, a))

# One shared palette for the whole sheet; transparent pixels filled with a robe tone
filler = (40, 10, 25)
mosaic = Image.new("RGB", (px * len(frames), px), filler)
for i, (im, a) in enumerate(frames):
    base = Image.new("RGB", (px, px), filler)
    if colors == 0: base = im.convert("RGB")   # keep the edge colours; alpha is applied below
    else: base.paste(im.convert("RGB"), (0, 0), a)
    mosaic.paste(base, (i * px, 0))
pal = mosaic if colors == 0 else mosaic.quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGB")

cols = 8
rows = (len(frames) + cols - 1) // cols
sheet = Image.new("RGBA", (cols * px, rows * px), (0, 0, 0, 0))
for i, (im, a) in enumerate(frames):
    fr = pal.crop((i * px, 0, (i + 1) * px, px)).convert("RGBA")
    fr.putalpha(a)
    sheet.paste(fr, ((i % cols) * px, (i // cols) * px))
name = f"player_sheet_{px}"
sheet.save(os.path.join(out, name + ".png"))

d = json.load(open(ref))
import copy
# Diagonal clips are appended after the game's original 55 frames; they reuse the matching clip's timings.
base = d["frames"][:55]
NEW = [("idle_diag", 55, 55, 4), ("idle_updiag", 56, 56, 4),
       ("walk_diag", 57, 62, 6), ("walk_updiag", 63, 68, 6),
       ("cast_diag", 69, 75, 24), ("cast_updiag", 76, 82, 24)]
if len(frames) > 55:
    tag_t = d["meta"]["frameTags"][0]
    d["frames"] = base
    for tname, a, b, src in NEW:
        for k in range(b - a + 1):
            fr = copy.deepcopy(base[0])
            fr["filename"] = str(a + k)
            fr["duration"] = base[src + k]["duration"] if tname.startswith(("walk", "cast")) else base[src]["duration"]
            d["frames"].append(fr)
        t = copy.deepcopy(tag_t)
        t["name"], t["from"], t["to"] = tname, a, b
        d["meta"]["frameTags"].append(t)
for i, fr in enumerate(d["frames"]):
    fr["frame"] = {"x": (i % cols) * px, "y": (i // cols) * px, "w": px, "h": px}
    fr["spriteSourceSize"] = {"x": 0, "y": 0, "w": px, "h": px}
    fr["sourceSize"] = {"w": px, "h": px}
d["meta"]["size"] = {"w": cols * px, "h": rows * px}
d["meta"]["image"] = name + ".png"
json.dump(d, open(os.path.join(out, name + ".json"), "w"), indent=1)
print(name, sheet.size, len(frames), "frames,", colors, "colors")
