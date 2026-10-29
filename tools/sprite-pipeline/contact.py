"""Contact sheet of rendered frames: python contact.py out.png thumb frames..."""
import sys
from PIL import Image
out, th, files = sys.argv[1], int(sys.argv[2]), sys.argv[3:]
sh = Image.new("RGBA", (th * len(files), th), (110, 110, 120, 255))
for i, f in enumerate(files):
    im = Image.open(f).convert("RGBA").resize((th, th), Image.LANCZOS)
    sh.alpha_composite(im, (i * th, 0))
sh.convert("RGB").save(out)
