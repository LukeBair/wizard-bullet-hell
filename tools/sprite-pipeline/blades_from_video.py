"""Turns the Spectral Blades videos (a crescent blade spinning in place on black, a slash on black) into frames.
  python blades_from_video.py blade.mp4 slash.mp4 [outdir]
vfx_blades_spin_<n>: one full turn of the blade in 12 steps (the game picks the step from its spin angle).
vfx_blades_hit_<n>: the slash, hurried through its long sparkle fade. Black is keyed out by brightness and a
soft round mask hides the haze at the frame edges. Frames stay hi-res; the game resamples them to its pixel
size (PixelDensity.cs). Needs ffmpeg + Pillow."""
import subprocess, sys, tempfile
import numpy as np
from PIL import Image

blade_mp4, slash_mp4 = sys.argv[1:3]
OUT = (sys.argv[3] if len(sys.argv) > 3 else "Assets/Resources/Art/Dusk") + "/"

def grab(mp4, t):
    d = tempfile.mkdtemp()
    subprocess.run(["ffmpeg", "-v", "error", "-ss", f"{t:.3f}", "-i", mp4, "-frames:v", "1", d + "/f.png"], check=True)
    return np.asarray(Image.open(d + "/f.png").convert("RGB"), np.float32) / 255

def key(a, floor, edge):
    h, w = a.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w]
    r = np.hypot(xs - w / 2, ys - h / 2) / (w / 2)
    mask = np.clip((1 - r) / (1 - edge), 0, 1)              # fades to nothing at the frame's edge
    al = np.clip((a.max(2) - floor) / (0.8 - floor), 0, 1) * mask
    rgb = np.where(al[..., None] > 0.02, np.clip(a / np.maximum(al[..., None], 0.3), 0, 1), 0)
    return Image.fromarray((np.concatenate([rgb, al[..., None]], 2) * 255).astype(np.uint8), "RGBA")

# one turn: 2.0s to 4.75s of the blade video (66 frames at 24fps), centre crop
C = (180, 180, 780, 780)
for i in range(12):
    t = 2.0 + 2.75 * i / 12
    key(grab(blade_mp4, t)[C[1]:C[3], C[0]:C[2]], 0.08, 0.75).resize((256, 256), Image.LANCZOS).save(OUT + f"vfx_blades_spin_{i}.png")

# the slash: quick through the flash, then the sparkles drift off
TIMES = [0.0, 0.08, 0.17, 0.27, 0.4, 0.55, 0.75, 1.0, 1.3, 1.65, 2.05, 2.5]
for i, t in enumerate(TIMES):
    key(grab(slash_mp4, t), 0.12, 0.7).resize((320, 320), Image.LANCZOS).save(OUT + f"vfx_blades_hit_{i}.png")
print("ok")
