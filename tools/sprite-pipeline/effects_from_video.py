"""Turns the general effect videos (each centred on black) into the game's frames.
  python effects_from_video.py death.mp4 levelup.mp4 evolve.mp4 pickup.mp4 slam.mp4 burning.mp4 chilled.mp4 wet.mp4 infested.mp4 [outdir]
  vfx_death_<n>     12 frames of a violet smoke puff bursting and thinning (an enemy dying)
  vfx_levelup_<n>   14 frames of a gold ring spreading with sparkles rising (level up)
  vfx_evolve_<n>    14 frames of an arcane spiral winding in and flaring (a spell evolving)
  vfx_pickup_<n>    10 frames of a star glint popping and shrinking (an XP gem picked up)
  vfx_slam_<n>      12 frames of a ring of rock and dust bursting out (a boss's ground slam)
  vfx_burning_<n>, vfx_chilled_<n>, vfx_wet_<n>, vfx_infested_<n>
                    16-frame loops shown on foes with that status (each end is cross-faded into its start)
Keyed and baked like combos_from_video.py: black keyed out by brightness with a gain per video, a soft round mask,
then pixelstyle.bake at the default size in world units. Needs ffmpeg + Pillow."""
import os, subprocess, sys, tempfile
import numpy as np
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pixelstyle import bake

death, levelup, evolve, pickup, slam, burning, chilled, wet, infested = sys.argv[1:10]
OUT = (sys.argv[10] if len(sys.argv) > 10 else "Assets/Resources/Art/Dusk") + "/"
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

def save(name, frames, units, colours, steps=3, smooth=0):
    for i, im in enumerate(bake(frames, units, colours, steps, smooth)):
        im.save(OUT + f"{name}_{i}.png")

def burst(mp4, name, start, end, n, gain, units, colours, smooth=0):
    save(name, [key(grab(mp4, start + (end - start) * i / (n - 1)), gain) for i in range(n)], units, colours, smooth=smooth)

def loop(mp4, name, start, length, gain, units, colours, smooth=0, n=16, fade=5):
    raw = [grab(mp4, start + i * length / n) for i in range(n + fade)]
    frames = []
    for i in range(n):
        f = raw[i]
        if i < fade:                       # the loop's start blends in from where its end would carry on
            k = (i + 1) / (fade + 1)
            f = f * k + raw[n + i] * (1 - k)
        frames.append(key(f, gain))
    save(name, frames, units, colours, smooth=smooth)

burst(death, "vfx_death", 0.0, 3.3, 12, 0.35, 1.6, 6, smooth=3)    # a 0.4-radius foe at vfx.deathSize 2
burst(levelup, "vfx_levelup", 0.0, 4.0, 14, 0.6, 3.0, 8)
burst(evolve, "vfx_evolve", 0.5, 4.5, 14, 0.6, 2.5, 8)
burst(pickup, "vfx_pickup", 0.3, 4.3, 10, 0.5, 0.6, 4)
burst(slam, "vfx_slam", 0.0, 4.0, 12, 0.6, 2.4, 6, smooth=3)      # a 1.2-radius boss at vfx.slamSize 1
loop(burning, "vfx_burning", 1.0, 2.0, 0.6, 1.0, 8)
loop(chilled, "vfx_chilled", 1.0, 2.0, 0.6, 1.2, 6, smooth=3)
loop(wet, "vfx_wet", 2.5, 2.0, 0.5, 1.0, 6)
loop(infested, "vfx_infested", 1.0, 2.0, 0.4, 1.0, 6)
