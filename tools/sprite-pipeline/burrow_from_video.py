"""Turns the two Root Burrower videos (dirt on black) into the game's frames.
  python burrow_from_video.py trail.mp4 pop.mp4 [outdir]
  vfx_burrow_trail_<n>  12-frame loop of a stretch of churned-up dirt, seen side on as a band across the frame
                        (the first two seconds settle in, so it's cut from after them; the end is cross-faded
                        into the start). Stamped along the tunnel, turned to the way the mole is going.
  vfx_burrow_pop_<n>    16 frames, seen from above, of a dirt mound cracking open and bursting (the mole erupting)
Black is keyed out by brightness (the dirt is dark, so the gain is low), then the frames are baked to the game's
pixel style at their default size (pixelstyle.py: vfx.burrowTrailSize 1.4, the pop 1.5 * 2 * vfx.burrowPopSize 0.55).
Needs ffmpeg + Pillow."""
import os, subprocess, sys, tempfile
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pixelstyle import bake

trail, pop = sys.argv[1:3]
OUT = (sys.argv[3] if len(sys.argv) > 3 else "Assets/Resources/Art/Dusk") + "/"

def grab(mp4, t, crop):
    d = tempfile.mkdtemp()
    subprocess.run(["ffmpeg", "-v", "error", "-ss", f"{t:.3f}", "-i", mp4, "-frames:v", "1", d + "/f.png"], check=True)
    a = np.asarray(Image.open(d + "/f.png").convert("RGB"), np.float32) / 255
    return a[crop[1]:crop[3], crop[0]:crop[2]]

def key(a, gain, size, round_mask=True):
    h, w = a.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w]
    if round_mask: mask = np.clip((1 - np.hypot(xs - w / 2, ys - h / 2) / (w / 2)) / 0.2, 0, 1)
    else:                                  # an oval: the band's ends round off, and thin out towards them
        dx, dy = np.abs(xs - w / 2) / (w / 2), np.abs(ys - h / 2) / (h / 2)
        mask = np.clip((1 - np.hypot(dx, dy)) / 0.35, 0, 1) * (1 - 0.6 * dx)
    al = np.clip((a.max(2) - 0.04) / gain, 0, 1) * mask
    rgb = np.where(al[..., None] > 0.02, np.clip(a / np.maximum(al[..., None], 0.5), 0, 1), 0)
    return Image.fromarray((np.concatenate([rgb, al[..., None]], 2) * 255).astype(np.uint8), "RGBA").resize(size, Image.LANCZOS)

# the trail: a 280x200 stretch of the band, its ends rounded and faded so the stamps run into each other
N, FADE, START, END = 12, 4, 2.0, 4.0
step = (END - START) / N
raw = [grab(trail, START + i * step, (140, 180, 420, 380)) for i in range(N + FADE)]
frames = []
for i in range(N):
    f = raw[i]
    if i < FADE:                           # the loop's start blends in from where its end would carry on
        k = (i + 1) / (FADE + 1)
        f = f * k + raw[N + i] * (1 - k)
    frames.append(key(f, 0.3, (256, 184), round_mask=False))
for i, im in enumerate(bake(frames, 1.4, 6, 3, smooth=3)):   # three alpha steps keep the ends see-through
    im.save(OUT + f"vfx_burrow_trail_{i}.png")

# the pop: 2.0s (the mound) to 3.6s (burst wide open)
frames = [key(grab(pop, 2.0 + i * 0.1, (0, 0, 560, 560)), 0.3, (256, 256)) for i in range(16)]
for i, im in enumerate(bake(frames, 1.65, 6, 2, smooth=3)):
    im.save(OUT + f"vfx_burrow_pop_{i}.png")
print("ok")
