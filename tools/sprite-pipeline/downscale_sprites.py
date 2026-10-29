"""Downscale renders to pixel-art size. Usage: python downscale_sprites.py out_dir [px] [colors]"""
import glob, os, sys
from PIL import Image

out = sys.argv[1]
px = int(sys.argv[2]) if len(sys.argv) > 2 else 48
colors = int(sys.argv[3]) if len(sys.argv) > 3 else 24
z = 4 if px <= 64 else 1  # preview zoom
sheet = Image.new("RGBA", (px * 3 * z, px * z), (0, 0, 0, 0))
for i, f in enumerate(sorted(glob.glob(os.path.join(out, "*_768.png")))):
    im = Image.open(f).convert("RGBA")
    bbox = im.getbbox()
    im = im.crop(bbox)
    s = (px - 2) / max(im.size)
    im = im.resize((max(1, round(im.width * s)), max(1, round(im.height * s))), Image.BOX)
    alpha = im.getchannel("A").point(lambda v: 255 if v > 128 else 0)
    q = im.convert("RGB").quantize(colors=colors, dither=Image.Dither.NONE).convert("RGBA")
    q.putalpha(alpha)
    canvas = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    canvas.alpha_composite(q, ((px - q.width) // 2, px - 1 - q.height))
    name = os.path.basename(f).split("_")[0]
    canvas.save(os.path.join(out, f"{name}_{px}.png"))
    sheet.alpha_composite(canvas.resize((px * z, px * z), Image.NEAREST), (i * px * z, 0))
sheet.save(os.path.join(out, f"preview_{px}.png"))
