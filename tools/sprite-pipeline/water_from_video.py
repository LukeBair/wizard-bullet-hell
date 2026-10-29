"""Turns the Tidal Lance videos (a water jet streaming across black, a splash on black) into the game's frames.
  python water_from_video.py beam.mp4 splash.mp4 [outdir]
vfx_water_beam_<n>: a strip of the jet that tiles end to end (the ends are cross-faded together) and loops
in time (the last frames fade into the first). vfx_water_hit_<n>: the splash, made to start as a speck and
grow in fast, then run quickly through the long fade. Frames stay hi-res; the game resamples them to its
pixel size (PixelDensity.cs). Needs ffmpeg + Pillow."""
import glob, subprocess, sys, tempfile
import numpy as np
from PIL import Image

beam_mp4, hit_mp4 = sys.argv[1:3]
OUT = (sys.argv[3] if len(sys.argv) > 3 else "Assets/Resources/Art/Dusk") + "/"

def grab(mp4, times):
    out = []
    for t in times:
        d = tempfile.mkdtemp()
        subprocess.run(["ffmpeg", "-v", "error", "-ss", f"{t:.3f}", "-i", mp4, "-frames:v", "1", d + "/f.png"], check=True)
        out.append(np.asarray(Image.open(d + "/f.png").convert("RGB"), np.float32) / 255)
    return out

def from_black(a):                        # alpha = brightest channel (a little floor cut), colour un-premultiplied
    al = np.clip((a.max(2) - 0.05) / 0.85, 0, 1)
    rgb = np.where(al[..., None] > 0.02, np.clip(a / np.maximum(al[..., None], 0.3), 0, 1), 0)
    return np.concatenate([rgb, al[..., None]], 2)

def save(arr, name, size):
    Image.fromarray((arr * 255).astype(np.uint8), "RGBA").resize(size, Image.LANCZOS).save(OUT + name)

# ---- beam: 12 looping frames of a tileable strip ----
Y0, Y1, OVERLAP, N, LOOP = 330, 630, 160, 12, 4
raw = [from_black(f[Y0:Y1]) for f in grab(beam_mp4, [1.0 + i / 12 for i in range(N + LOOP)])]
frames = []
for i in range(N):
    f = raw[i]
    if i < LOOP:                          # the loop's last frames fade into its first
        k = i / LOOP
        f = f * k + raw[i + N] * (1 - k)
    w = f.shape[1]
    t = (np.arange(OVERLAP) / OVERLAP)[None, :, None]
    tile = f[:, : w - OVERLAP].copy()
    tile[:, :OVERLAP] = f[:, :OVERLAP] * t + f[:, w - OVERLAP :] * (1 - t)   # right end runs into the left one
    frames.append(tile)
bw = frames[0].shape[1]

def fade_out(f, length):                  # the jet thins and fades away over `length` px at its right end
    h, w = f.shape[:2]
    d = (w - 1 - np.arange(w, dtype=np.float32))[None, :]
    g = f.copy(); g[..., 3] *= np.clip(d / length, 0, 1) ** 1.5
    return g

def wedge(f, length, at_start):           # the jet narrows to a point over `length` px at one end
    h, w = f.shape[:2]
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    d = xs if at_start else (w - 1 - xs)  # distance from the pointed end
    half = np.clip(d / length, 0, 1) * h / 2
    keep = np.clip((half - np.abs(ys - h / 2)) / 12 + 0.5, 0, 1) * np.clip(d / (length * 0.35), 0, 1) ** 0.5
    g = f.copy(); g[..., 3] *= keep
    return g

for i, f in enumerate(frames):
    size = (bw // 2, (Y1 - Y0) // 2)
    save(f, f"vfx_water_beam_{i}.png", size)
    save(wedge(f, (Y1 - Y0) * 1.3, True), f"vfx_water_beamstart_{i}.png", size)   # at the wizard
    save(fade_out(f, (bw) * 0.8), f"vfx_water_beamtip_{i}.png", size)            # the front fades away

# ---- splash: grows in from a speck, then hurries through the fade ----
TIMES = [0.0, 0.12, 0.25, 0.4, 0.6, 0.85, 1.2, 1.7, 2.3, 3.0, 3.8, 4.7]
GROW = [0.12, 0.3, 0.52, 0.72, 0.87, 0.96, 1, 1, 1, 1, 1, 1]
PX = 384
for i, (t, g) in enumerate(zip(TIMES, GROW)):
    im = Image.fromarray((from_black(grab(hit_mp4, [t])[0]) * 255).astype(np.uint8), "RGBA").resize((PX, PX), Image.LANCZOS)
    s = max(2, round(PX * g))
    canvas = Image.new("RGBA", (PX, PX))
    canvas.paste(im.resize((s, s), Image.LANCZOS), ((PX - s) // 2, (PX - s) // 2))
    canvas.save(OUT + f"vfx_water_hit_{i}.png")
print("beam", bw // 2, "x", (Y1 - Y0) // 2, "| hit", PX)
