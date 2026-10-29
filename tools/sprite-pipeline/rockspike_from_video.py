"""Turns the stone spike video (a spike on black that erupts, holds with glowing cracks, then crumbles) into
the game's frames: vfx_rockspike_rise_<n>, vfx_rockspike_hold_<n> and vfx_rockspike_crumble_<n>.
  python rockspike_from_video.py spike.mp4 [outdir]
Black is keyed out by brightness; the lit floor and the floor reflection are faded out with a soft mask
around the spike's base. Frames stay hi-res; the game resamples them to its pixel size (PixelDensity.cs).
Needs ffmpeg + Pillow."""
import glob, subprocess, sys, tempfile
import numpy as np
from PIL import Image

mp4 = sys.argv[1]
OUT = (sys.argv[2] if len(sys.argv) > 2 else "Assets/Resources/Art/Dusk") + "/"
FPS = 12
CROP = (160, 60, 800, 880)            # x0, y0, x1, y1 in the 960x960 video: the spike and its dust
BASE_X, GROUND_Y = 480, 850           # spike base; below the ground line is the floor's reflection
OUT_W = 200                           # frame width; height keeps the crop's aspect
PARTS = {"rise": (0, 11, 8), "hold": (18, 36, 6), "crumble": (54, 72, 8)}   # first frame, last frame, count (at FPS)

d = tempfile.mkdtemp()
subprocess.run(["ffmpeg", "-v", "error", "-i", mp4, "-vf", f"fps={FPS}", d + "/%03d.png"], check=True)
raw = sorted(glob.glob(d + "/*.png"))

h, w = 960, 960
ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
side = np.clip((300 - np.abs(xs - BASE_X)) / 120, 0, 1)                  # sand to the sides fades out
under = np.clip((GROUND_Y + 30 - ys) / 30, 0, 1)                          # the reflection under the floor goes
floor = 1 - np.clip((ys - (GROUND_Y - 90)) / 90, 0, 1) * np.clip((np.abs(xs - BASE_X) - 150) / 120, 0, 1)   # flat sand away from the base
mask = side * under * floor
# Sand and dust on the floor away from the spike go thin and dark, so the game's own ground shows through
band = np.clip((ys - (GROUND_Y - 220)) / 140, 0, 1)                       # low part of the frame
column = np.clip((120 - np.abs(xs - BASE_X)) / 70, 0, 1)                  # the spike itself and the rubble at its foot
groundness = band * (1 - column)

def key(path):
    a = np.asarray(Image.open(path).convert("RGB"), np.float32) / 255
    m = a.max(2)
    al = np.clip((m - 0.06) / 0.22, 0, 1) * mask
    rgb = np.where(al[..., None] > 0.02, np.clip(a / np.maximum(al[..., None], 0.35), 0, 1), 0)
    hot = np.clip((a[..., 0] - a[..., 2] - 0.35) / 0.2, 0, 1)                # the glowing cracks keep their light
    g = groundness * (1 - hot)
    al = al * (1 - 0.6 * g)
    rgb = rgb * (1 - 0.4 * g)[..., None]
    im = Image.fromarray((np.concatenate([rgb, al[..., None]], 2) * 255).astype(np.uint8), "RGBA").crop(CROP)
    return im.resize((OUT_W, round(OUT_W * im.height / im.width)), Image.LANCZOS)

for name, (first, last, n) in PARTS.items():
    for k in range(n):
        i = round(first + (last - first) * k / max(1, n - 1))
        key(raw[i]).save(f"{OUT}vfx_rockspike_{name}_{k}.png")
        print(name, k, raw[i][-7:])
