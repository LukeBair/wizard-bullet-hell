"""Recolours a sprite sheet in place: hue shift (degrees), saturation and brightness multipliers.
  python recolor_sheet.py sheet.png HUE SAT VAL [out.png]
Used for the treant (autumn colours so it stands out on the mossy forest floor). Alpha and the normal map
are left alone. Needs Pillow + numpy."""
import sys
import numpy as np
from PIL import Image

src, hue, sat, val = sys.argv[1], float(sys.argv[2]), float(sys.argv[3]), float(sys.argv[4])
out = sys.argv[5] if len(sys.argv) > 5 else src
im = Image.open(src).convert("RGBA")
a = np.asarray(im)
hsv = np.asarray(im.convert("RGB").convert("HSV")).astype(np.float32)
hsv[..., 0] = (hsv[..., 0] + hue / 360 * 255) % 255
hsv[..., 1] = np.clip(hsv[..., 1] * sat, 0, 255)
hsv[..., 2] = np.clip(hsv[..., 2] * val, 0, 255)
rgb = np.asarray(Image.fromarray(hsv.astype(np.uint8), "HSV").convert("RGB"))
Image.fromarray(np.dstack([rgb, a[..., 3]]), "RGBA").save(out)
