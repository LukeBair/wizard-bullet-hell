"""Turns the Comet Trail video (a patch of fire burning on black) into a looping set of frames.
  python flame_from_video.py fire.mp4 [outdir]
vfx_flame_<n>: 12 frames of one loop (2.1s to 4.6s of the video, where it comes back around), the last few
cross-faded into the first so the loop doesn't pop. Black is keyed out by brightness, with a soft round mask
at the edges. Frames stay hi-res; the game resamples them to its pixel size (PixelDensity.cs).
Needs ffmpeg + Pillow."""
import subprocess, sys, tempfile
import numpy as np
from PIL import Image

mp4 = sys.argv[1]
OUT = (sys.argv[2] if len(sys.argv) > 2 else "Assets/Resources/Art/Dusk") + "/"
START, END, N, LOOP = 51 / 24, 110 / 24, 12, 4      # the loop in seconds, frames out, frames cross-faded
CROP = (210, 200, 750, 740)                          # the fire and the embers above it
PX = 192

def grab(t):
    d = tempfile.mkdtemp()
    subprocess.run(["ffmpeg", "-v", "error", "-ss", f"{t:.3f}", "-i", mp4, "-frames:v", "1", d + "/f.png"], check=True)
    a = np.asarray(Image.open(d + "/f.png").convert("RGB"), np.float32) / 255
    return a[CROP[1]:CROP[3], CROP[0]:CROP[2]]

def key(a):
    h, w = a.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w]
    r = np.hypot(xs - w / 2, ys - h / 2) / (w / 2)
    al = np.clip((a.max(2) - 0.05) / 0.75, 0, 1) * np.clip((1 - r) / 0.3, 0, 1)
    rgb = np.where(al[..., None] > 0.02, np.clip(a / np.maximum(al[..., None], 0.3), 0, 1), 0)
    return np.concatenate([rgb, al[..., None]], 2)

step = (END - START) / N
raw = [grab(START + i * step) for i in range(N + LOOP)]   # plus the frames that follow the loop's end
for i in range(N):
    f = raw[i]
    if i < LOOP:                                     # the loop's start blends in from where its end would carry on
        k = (i + 1) / (LOOP + 1)
        f = f * k + raw[N + i] * (1 - k)
    Image.fromarray((key(f) * 255).astype(np.uint8), "RGBA").resize((PX, PX), Image.LANCZOS).save(OUT + f"vfx_flame_{i}.png")
print("ok")
