"""Wing-flap frames for a top-down flyer from two aligned cutouts (same canvas): body and wings.
The wings squash horizontally about the body's centre line; the body bobs a pixel or two.
Usage: python flap_2d.py body.png wings.png out_dir [frames]"""
import math, os, sys
from PIL import Image

body, wings, out = Image.open(sys.argv[1]).convert("RGBA"), Image.open(sys.argv[2]).convert("RGBA"), sys.argv[3]
n = int(sys.argv[4]) if len(sys.argv) > 4 else 8
os.makedirs(out, exist_ok=True)
bb = body.getchannel("A").getbbox(); cx = (bb[0] + bb[2]) / 2
W, H = wings.size
pad = H // 10
for k in range(n):
    p = 0.5 + 0.5 * math.cos(2 * math.pi * k / n)          # 1 = wings open, 0 = closed
    s = 0.3 + 0.7 * p
    fr = Image.new("RGBA", (W, H + 2 * pad), (0, 0, 0, 0))
    left, right = wings.crop((0, 0, int(cx), H)), wings.crop((int(cx), 0, W, H))
    lw, rw = max(1, int(left.width * s)), max(1, int(right.width * s))
    lift = int(pad * 0.4 * (1 - p))                          # wings rise a touch as they close
    fr.alpha_composite(left.resize((lw, H)), (int(cx) - lw, pad - lift))
    fr.alpha_composite(right.resize((rw, H)), (int(cx), pad - lift))
    bob = int(pad * 0.3 * math.sin(2 * math.pi * k / n))
    fr.alpha_composite(body, (0, pad + bob))
    fr.save(os.path.join(out, f"{k:02d}.png"))
print(n, "frames ->", out)
