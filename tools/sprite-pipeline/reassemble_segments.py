"""Put a character back together from a segmentation export that has no full image, only its parts.
The parts are cropped to their own bounding boxes, so their positions are lost; the background
segment (the full canvas with a character-shaped hole) gives them back. Each part, largest first, is slid
over the canvas to where it best fills the hole that's still empty.
Usage: python reassemble_segments.py segments_dir out.png [background.png]
Background defaults to the first of black-background / white-background / transparent-background."""
import glob, os, sys
import numpy as np
from numpy.fft import irfft2, rfft2
from PIL import Image

src, out = sys.argv[1], sys.argv[2]
bgp = sys.argv[3] if len(sys.argv) > 3 else next(
    p for n in ("black-background", "white-background", "transparent-background")
    for p in [os.path.join(src, n + ".png")] if os.path.isfile(p))
bg = np.array(Image.open(bgp).convert("RGBA"))
hole = bg[..., 3] < 128
H, W = hole.shape
parts = [p for p in glob.glob(os.path.join(src, "**", "*.png"), recursive=True) if os.path.abspath(p) != os.path.abspath(bgp)]
parts = [(p, Image.open(p).convert("RGBA")) for p in parts]
parts = [(p, im) for p, im in parts if im.width <= W and im.height <= H]
parts.sort(key=lambda t: -(np.array(t[1])[..., 3] > 128).sum())

canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
filled = np.zeros((H, W), bool)
for p, im in parts:
    a = (np.array(im)[..., 3] > 128).astype(np.float32)
    h, w = a.shape
    if a.sum() < 50:
        continue
    # +1 for landing in empty hole, small reward for re-covering filled hole (parts overlap), -2 outside
    score = np.where(hole & ~filled, 1.0, np.where(hole, 0.3, -2.0)).astype(np.float32)
    c = irfft2(rfft2(score, s=(H + h, W + w)) * rfft2(a[::-1, ::-1], s=(H + h, W + w)), s=(H + h, W + w))
    c = c[h - 1:H, w - 1:W]
    y, x = np.unravel_index(np.argmax(c), c.shape)
    fit = c[y, x] / a.sum()
    if fit < 0.5:
        print(f"skip {os.path.relpath(p, src)} (fit {fit:.2f})")
        continue
    # paste underneath what's already there so big parts keep their detail on top of overlaps
    layer = Image.new("RGBA", (W, H), (0, 0, 0, 0)); layer.paste(im, (int(x), int(y)))
    canvas = Image.alpha_composite(layer, canvas)
    filled |= np.array(layer)[..., 3] > 128
    print(f"{os.path.relpath(p, src)} at {x},{y} fit {fit:.2f}")
canvas.save(out)
print(f"hole covered {(filled & hole).sum() / hole.sum():.0%} -> {out}")
