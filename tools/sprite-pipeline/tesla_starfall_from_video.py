"""Turns the Tesla Totem and Starfall videos (each an effect centred on black, 560x560) into the game's frames.
  python tesla_starfall_from_video.py totem1.mp4 totem2.mp4 totem3.mp4 shock.mp4 star1.mp4 star2.mp4 star3.mp4 [outdir]
  vfx_totem1_<n> .. vfx_totem3_<n>  12-frame idle loops of the tower at each tier (last frames cross-faded into the first)
  vfx_shock_<n>                     12 frames of the circle shock (tier III), one shot
  vfx_star1_<n> .. vfx_star3_<n>    12 frames of the falling star / meteor, head at the lower left, played once as it falls
Black is keyed out by brightness: the towers are dark stone, so they get a low floor; the glowing effects a higher one.
A soft round mask hides the haze at the frame edges. Frames stay the video's full frame (so the world size is one
number per effect, vfx.totemSize1..3 / shockSize / starSize1..3) and the game resamples them to its pixel size
(PixelDensity.cs). Needs ffmpeg + Pillow + numpy."""
import glob, subprocess, sys, tempfile
import numpy as np
from PIL import Image

args = sys.argv[1:8]
OUT = (sys.argv[8] if len(sys.argv) > 8 else "Assets/Resources/Art/Dusk") + "/"
PX, N, LOOP = 256, 12, 4

def frames(mp4):
    d = tempfile.mkdtemp()
    subprocess.run(["ffmpeg", "-v", "error", "-i", mp4, "-vf", "fps=24", d + "/f%04d.png"], check=True)
    return sorted(glob.glob(d + "/f*.png"))

def key(path, floor, top, edge):
    a = np.asarray(Image.open(path).convert("RGB"), np.float32) / 255
    h, w = a.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w]
    r = np.hypot(xs - w / 2, ys - h / 2) / (w / 2)
    al = np.clip((a.max(2) - floor) / (top - floor), 0, 1) * np.clip((1 - r) / edge, 0, 1)
    rgb = np.where(al[..., None] > 0.02, np.clip(a / np.maximum(al[..., None], 0.3), 0, 1), 0)
    return np.concatenate([rgb, al[..., None]], 2)

def save(arr, name):
    Image.fromarray((arr * 255).astype(np.uint8), "RGBA").resize((PX, PX), Image.LANCZOS).save(OUT + name)

def loop(mp4, name, start, end, floor, top, edge):
    fs = frames(mp4)
    idx = lambda t: min(len(fs) - 1, int(t * 24))
    step = (end - start) / N
    raw = [key(fs[idx(start + i * step)], floor, top, edge) for i in range(N + LOOP)]
    for i in range(N):
        f = raw[i]
        if i < LOOP:                                  # blend in from where the loop's end would carry on
            k = (i + 1) / (LOOP + 1)
            f = f * k + raw[N + i] * (1 - k)
        save(f, f"{name}_{i}.png")

def once(mp4, name, start, end, floor, top, edge):
    fs = frames(mp4)
    for i in range(N):
        t = start + (end - start) * i / (N - 1)
        save(key(fs[min(len(fs) - 1, int(t * 24))], floor, top, edge), f"{name}_{i}.png")

t1, t2, t3, shock, s1, s2, s3 = args
loop(t1, "vfx_totem1", 1.0, 4.5, 0.03, 0.10, 0.25)
loop(t2, "vfx_totem2", 1.0, 4.5, 0.03, 0.10, 0.25)
loop(t3, "vfx_totem3", 1.0, 4.5, 0.03, 0.10, 0.2)
once(shock, "vfx_shock", 0.0, 4.6, 0.05, 0.6, 0.15)
once(s1, "vfx_star1", 0.0, 5.5, 0.03, 0.5, 0.2)
once(s2, "vfx_star2", 0.0, 5.5, 0.05, 0.7, 0.2)
once(s3, "vfx_star3", 0.0, 3.6, 0.05, 0.7, 0.2)
print("ok")
