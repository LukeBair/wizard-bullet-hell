"""Turns the rune and combo videos (each an effect centred on black, seen from above) into the game's frames.
  python combos_from_video.py firebugs.mp4 steam.mp4 ward.mp4 deepfreeze.mp4 conductive.mp4 hexswarm.mp4 ants.mp4 [outdir]
  vfx_firebugs_<n>    14 frames of burning beetles bursting outward (the Firebugs combo)
  vfx_steam_<n>       14 frames of a steam cloud billowing out (Steam)
  vfx_deepfreeze_<n>  12 frames of ice shards snapping out (Deep Freeze)
  vfx_conductive_<n>  12 frames of lightning crackling over water (Conductive)
  vfx_hexswarm_<n>    14 frames of cursed beetles spiralling out (Hex Swarm)
  vfx_ants_<n>        14 frames of ants pouring out (a plain bug burst)
  vfx_ward_<n>        16-frame loop of the ward ring turning (the end is cross-faded into the start)
Black is keyed out by brightness, with a gain per video (the swarms are dim specks), and a soft round mask at the
edges. Frames stay hi-res; the game resamples them to its pixel size (PixelDensity.cs). Needs ffmpeg + Pillow."""
import subprocess, sys, tempfile
import numpy as np
from PIL import Image

firebugs, steam, ward, deepfreeze, conductive, hexswarm, ants = sys.argv[1:8]
OUT = (sys.argv[8] if len(sys.argv) > 8 else "Assets/Resources/Art/Dusk") + "/"
PX = 256

def grab(mp4, t):
    d = tempfile.mkdtemp()
    subprocess.run(["ffmpeg", "-v", "error", "-ss", f"{t:.3f}", "-i", mp4, "-frames:v", "1", d + "/f.png"], check=True)
    return np.asarray(Image.open(d + "/f.png").convert("RGB"), np.float32) / 255

def key(a, gain):
    h, w = a.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w]
    mask = np.clip((1 - np.hypot(xs - w / 2, ys - h / 2) / (w / 2)) / 0.2, 0, 1)
    al = np.clip((a.max(2) - 0.04) / gain, 0, 1) * mask
    rgb = np.where(al[..., None] > 0.02, np.clip(a / np.maximum(al[..., None], 0.3), 0, 1), 0)
    return Image.fromarray((np.concatenate([rgb, al[..., None]], 2) * 255).astype(np.uint8), "RGBA").resize((PX, PX), Image.LANCZOS)

def burst(mp4, name, start, end, n, gain):
    for i in range(n):
        key(grab(mp4, start + (end - start) * i / (n - 1)), gain).save(OUT + f"{name}_{i}.png")

burst(firebugs, "vfx_firebugs", 0.0, 3.0, 14, 0.6)
burst(steam, "vfx_steam", 0.0, 3.0, 14, 0.85)
burst(deepfreeze, "vfx_deepfreeze", 0.0, 2.4, 12, 0.7)
burst(conductive, "vfx_conductive", 0.0, 2.0, 12, 0.6)
burst(hexswarm, "vfx_hexswarm", 0.7, 4.0, 14, 0.15)
burst(ants, "vfx_ants", 0.7, 4.7, 14, 0.08)

# the ward ring: 1.0s to 3.0s, looped
N, FADE = 16, 5
raw = [grab(ward, 1.0 + i * 2.0 / N) for i in range(N + FADE)]
for i in range(N):
    f = raw[i]
    if i < FADE:                           # the loop's start blends in from where its end would carry on
        k = (i + 1) / (FADE + 1)
        f = f * k + raw[N + i] * (1 - k)
    key(f, 0.6).save(OUT + f"vfx_ward_{i}.png")
print("ok")
