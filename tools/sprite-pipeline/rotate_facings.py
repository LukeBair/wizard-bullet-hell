"""Eight facings for a top-down sprite by rotating its frames in the image plane (fine for flyers seen
from above, like the moth; not for anything with a visible side).
Usage: python rotate_facings.py frames_dir out_dir [faces=n]   (faces = the way the source frames point)"""
import glob, os, sys
from PIL import Image

src, out = sys.argv[1], sys.argv[2]
faces = sys.argv[3] if len(sys.argv) > 3 else "n"
CCW = {"n": 0, "nw": 45, "w": 90, "sw": 135, "s": 180, "se": -135, "e": -90, "ne": -45}
for f, ang in CCW.items():
    os.makedirs(os.path.join(out, f), exist_ok=True)
    for p in sorted(glob.glob(os.path.join(src, "*.png"))):
        im = Image.open(p).convert("RGBA")
        side = int((im.width ** 2 + im.height ** 2) ** 0.5) + 2       # same canvas for every facing
        sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
        sq.paste(im, ((side - im.width) // 2, (side - im.height) // 2))
        sq.rotate(ang - CCW[faces], Image.BICUBIC).save(os.path.join(out, f, os.path.basename(p)))
print("8 facings ->", out)
