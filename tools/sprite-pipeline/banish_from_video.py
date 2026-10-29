"""Turns the Banish video (a violet shadow vortex on black that swirls, then collapses to a point) into frames.
  python banish_from_video.py vortex.mp4 [outdir]
vfx_banish_hit_<n>: 12 frames. The video opens already swirling, so the first three grow it in from small;
then four frames of the swirl and five of the collapse. Black is keyed out by brightness, with a soft round
mask at the edges. Frames stay hi-res; the game resamples them to its pixel size (PixelDensity.cs).
Needs ffmpeg + Pillow."""
import subprocess, sys, tempfile
import numpy as np
from PIL import Image

mp4 = sys.argv[1]
OUT = (sys.argv[2] if len(sys.argv) > 2 else "Assets/Resources/Art/Dusk") + "/"
TIMES = [0.1, 0.3, 0.5, 1.0, 1.8, 2.6, 3.4, 4.1, 4.4, 4.7, 5.0, 5.4]
GROW = [0.25, 0.55, 0.85, 1, 1, 1, 1, 1, 1, 1, 1, 1]
PX = 320

def grab(t):
    d = tempfile.mkdtemp()
    subprocess.run(["ffmpeg", "-v", "error", "-ss", f"{t:.3f}", "-i", mp4, "-frames:v", "1", d + "/f.png"], check=True)
    return np.asarray(Image.open(d + "/f.png").convert("RGB"), np.float32) / 255

def key(a):
    h, w = a.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w]
    r = np.hypot(xs - w / 2, ys - h / 2) / (w / 2)
    al = np.clip((a.max(2) - 0.05) / 0.7, 0, 1) * np.clip((1 - r) / 0.2, 0, 1)
    rgb = np.where(al[..., None] > 0.02, np.clip(a / np.maximum(al[..., None], 0.3), 0, 1), 0)
    return Image.fromarray((np.concatenate([rgb, al[..., None]], 2) * 255).astype(np.uint8), "RGBA")

for i, (t, g) in enumerate(zip(TIMES, GROW)):
    im = key(grab(t)).resize((PX, PX), Image.LANCZOS)
    s = max(2, round(PX * g))
    canvas = Image.new("RGBA", (PX, PX))
    canvas.paste(im.resize((s, s), Image.LANCZOS), ((PX - s) // 2, (PX - s) // 2))
    canvas.save(OUT + f"vfx_banish_hit_{i}.png")
print("ok")
