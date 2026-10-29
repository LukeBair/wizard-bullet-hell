"""Turns the last six spell videos (each an effect centred on black, seen from above) into the game's frames.
  python utility_from_video.py rejuvenate.mp4 mirror.mp4 enthrall.mp4 cyclone.mp4 veil.mp4 overgrowth.mp4 hearts.mp4 [outdir]
  vfx_rejuv_<n>       12 frames of the heal burst, grown in from small, then hurried through its spread;
                      squashed to an oval so it lies flat on the ground under the wizard
  vfx_hearts_<n>      16-frame loop of hearts rising in a column (a charmed foe; tall frames)
  vfx_mirror_<n>      16 frames of the shard turning, played forward then back so it loops (it never comes round)
  vfx_enthrall_<n>    16-frame loops: none of these come back around by themselves, so the last few frames
  vfx_cyclone_<n>       of each stretch are cross-faded into its start
  vfx_veil_<n>
  vfx_overgrowth_rise_<n>  10 frames of the vines sprouting, then
  vfx_overgrowth_hold_<n>   8 looping frames of them holding
Black is keyed out by brightness (gain per video: the vines are dark), with a soft round mask at the edges.
Frames stay hi-res; the game resamples them to its pixel size (PixelDensity.cs). Needs ffmpeg + Pillow."""
import subprocess, sys, tempfile
import numpy as np
from PIL import Image

rejuv, mirror, enthrall, cyclone, veil, overgrowth, hearts = sys.argv[1:8]
OUT = (sys.argv[8] if len(sys.argv) > 8 else "Assets/Resources/Art/Dusk") + "/"
PX = 256

def grab(mp4, t, crop):
    d = tempfile.mkdtemp()
    subprocess.run(["ffmpeg", "-v", "error", "-ss", f"{t:.3f}", "-i", mp4, "-frames:v", "1", d + "/f.png"], check=True)
    a = np.asarray(Image.open(d + "/f.png").convert("RGB"), np.float32) / 255
    return a[crop[1]:crop[3], crop[0]:crop[2]]

def key(a, gain, size=(PX, PX), round_mask=True):
    h, w = a.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w]
    if round_mask: mask = np.clip((1 - np.hypot(xs - w / 2, ys - h / 2) / (w / 2)) / 0.2, 0, 1)
    else: mask = np.clip(np.minimum(np.minimum(xs, w - 1 - xs), np.minimum(ys, h - 1 - ys)) / 40, 0, 1)   # soft frame edges
    al = np.clip((a.max(2) - 0.04) / gain, 0, 1) * mask
    rgb = np.where(al[..., None] > 0.02, np.clip(a / np.maximum(al[..., None], 0.3), 0, 1), 0)
    return Image.fromarray((np.concatenate([rgb, al[..., None]], 2) * 255).astype(np.uint8), "RGBA").resize(size, Image.LANCZOS)

def grown(im, g):                          # shrunk to g of its size, centred
    s = max(2, round(PX * g))
    c = Image.new("RGBA", (PX, PX)); c.paste(im.resize((s, s), Image.LANCZOS), ((PX - s) // 2, (PX - s) // 2))
    return c

def loop(mp4, name, start, end, n, fade, crop, gain, **kw):
    step = (end - start) / n
    raw = [grab(mp4, start + i * step, crop) for i in range(n + fade)]
    for i in range(n):
        f = raw[i]
        if i < fade:                       # the loop's start blends in from where its end would carry on
            k = (i + 1) / (fade + 1)
            f = f * k + raw[n + i] * (1 - k)
        key(f, gain, **kw).save(OUT + f"{name}_{i}.png")

FULL = (0, 0, 960, 960)
# the heal burst: grows in, then spreads out (the game fades it)
TIMES = [0.0, 0.15, 0.35, 0.6, 0.9, 1.3, 1.8, 2.4, 3.1, 3.9, 4.8, 5.8]
GROW = [0.35, 0.65, 0.9, 1, 1, 1, 1, 1, 1, 1, 1, 1]
for i, (t, g) in enumerate(zip(TIMES, GROW)):
    grown(key(grab(rejuv, t, FULL), 0.75), g).resize((PX, PX * 55 // 100), Image.LANCZOS).save(OUT + f"vfx_rejuv_{i}.png")

# the mirror shard: 1.0s to 3.0s forward, then back
C = (190, 180, 770, 760)
fwd = [key(grab(mirror, 1.0 + i * 0.25, C), 0.6) for i in range(9)]
for i, im in enumerate(fwd + fwd[-2:0:-1]):
    im.save(OUT + f"vfx_mirror_{i}.png")

loop(enthrall, "vfx_enthrall", 2.0, 4.0, 16, 5, (180, 180, 780, 780), 0.75)
loop(cyclone, "vfx_cyclone", 2.0, 3.5, 16, 5, (170, 170, 790, 790), 0.7)
loop(veil, "vfx_veil", 2.0, 4.0, 16, 5, (130, 130, 830, 830), 0.7)
loop(hearts, "vfx_hearts", 2.0, 4.0, 16, 5, (300, 0, 660, 960), 0.7, size=(96, 256), round_mask=False)

# vines: sprouting, then holding
for i in range(10):
    key(grab(overgrowth, i * 0.33, FULL), 0.35).save(OUT + f"vfx_overgrowth_rise_{i}.png")
loop(overgrowth, "vfx_overgrowth_hold", 4.0, 5.6, 8, 3, FULL, 0.35)
print("ok")
