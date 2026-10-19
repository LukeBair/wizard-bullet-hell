"""Turns two effect videos (a looping projectile on black, an impact on white) into the game's pixel frames.
  python vfx_from_video.py bolt.mp4 hit.mp4 <element> [outdir]
Env overrides: HIT_BG=black|white (what the hit video sits on, default white), BOLT_W, HIT_PX, HIT_SECS.
Writes vfx_<element>_bolt_<n>.png (head on the left) and vfx_<element>_hit_<n>.png: shrunk to the game's
pixel density, one shared 24-colour palette per effect, stepped alpha (0/128/255). Needs ffmpeg + Pillow."""
import glob, os, subprocess, sys, tempfile
import numpy as np
from PIL import Image, ImageDraw

bolt_mp4, hit_mp4, el = sys.argv[1:4]
OUT = (sys.argv[4] if len(sys.argv) > 4 else "Assets/Resources/Art/Dusk") + "/"
BOLT_W, BOLT_FRAMES, BOLT_START, BOLT_FPS = int(os.environ.get("BOLT_W", 44)), 12, 1.0, 12      # px wide, frames, seconds in, fps
HIT_PX, HIT_FRAMES, HIT_SECS = int(os.environ.get("HIT_PX", 56)), 12, float(os.environ.get("HIT_SECS", 3.5))
HIT_BG = os.environ.get("HIT_BG", "white")

def frames(mp4, start, secs, fps):
    d = tempfile.mkdtemp()
    subprocess.run(["ffmpeg", "-v", "error", "-ss", str(start), "-t", str(secs), "-i", mp4, "-vf", f"fps={fps}", d + "/%03d.png"], check=True)
    return [Image.open(f).convert("RGB") for f in sorted(glob.glob(d + "/*.png"))]

def from_black(im):                       # alpha = brightest channel, colour un-premultiplied
    a = np.asarray(im, np.float32) / 255; al = a.max(2, keepdims=True)
    return np.concatenate([np.where(al > 0.02, a / np.maximum(al, 1e-3), 0), al], 2)

def from_white(im):                       # alpha = how far from white, colour un-blended from white
    a = np.asarray(im, np.float32) / 255; al = np.clip(1 - a.min(2, keepdims=True), 0, 1)
    # a white-hot core is the same colour as the background: anything the outside can't reach is solid
    clear = al[..., 0] < 0.15
    reach = np.zeros_like(clear); reach[0, :] = reach[-1, :] = reach[:, 0] = reach[:, -1] = True
    reach &= clear
    while True:                           # grow the background in from the edges
        grown = (reach | np.roll(reach, 1, 0) | np.roll(reach, -1, 0) | np.roll(reach, 1, 1) | np.roll(reach, -1, 1)) & clear
        if (grown == reach).all(): break
        reach = grown
    inside = ~reach
    al = np.where(inside[..., None], 1.0, np.where(al < 0.15, 0, al))
    a = np.where(inside[..., None], np.minimum(a, 0.97), a)
    return np.concatenate([np.where(al > 0.02, (a - (1 - al)) / np.maximum(al, 1e-3), 0).clip(0, 1), al], 2)

def finish(arrs, width, pad=1):
    box = None
    for a in arrs:                        # one crop for every frame so the animation doesn't jitter
        ys, xs = np.where(a[..., 3] > 0.12)
        b = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
        box = b if box is None else (min(box[0], b[0]), min(box[1], b[1]), max(box[2], b[2]), max(box[3], b[3]))
    return box

def pixelate(arrs, box, size, square):
    w, h = box[2] - box[0], box[3] - box[1]
    if square:
        s = max(w, h); cx, cy = (box[0] + box[2]) // 2, (box[1] + box[3]) // 2
        box = (cx - s // 2, cy - s // 2, cx + s // 2, cy + s // 2); tw, th = size, size
    else:
        tw, th = size, max(1, round(size * h / w))
    out = []
    for a in arrs:
        rgba = Image.fromarray((np.clip(a, 0, 1) * 255).astype(np.uint8), "RGBA")
        prem = np.asarray(rgba, np.float32); prem[..., :3] *= prem[..., 3:] / 255      # shrink premultiplied, so glow doesn't halo
        im = Image.fromarray(prem.astype(np.uint8), "RGBA").crop(box).resize((tw, th), Image.BOX)
        out.append(np.asarray(im, np.float32))
    return out

def save(arrs, name):
    # shared palette from all frames' visible pixels
    px = np.concatenate([a[a[..., 3] > 40][:, :3] / np.maximum(a[a[..., 3] > 40][:, 3:] / 255, 1e-3) for a in arrs]).clip(0, 255).astype(np.uint8)
    pal = Image.fromarray(px.reshape(-1, 1, 3)).quantize(24, method=Image.MEDIANCUT, dither=Image.NONE)
    for i, a in enumerate(arrs):
        al = a[..., 3]; rgb = (a[..., :3] / np.maximum(al[..., None] / 255, 1e-3)).clip(0, 255).astype(np.uint8)
        q = np.asarray(Image.fromarray(rgb).quantize(palette=pal, dither=Image.NONE).convert("RGB"))
        step = np.where(al > 170, 255, np.where(al > 60, 128, 0)).astype(np.uint8)
        Image.fromarray(np.dstack([q, step]), "RGBA").save(f"{OUT}vfx_{el}_{name}_{i}.png")

bolt = [from_black(f) for f in frames(bolt_mp4, BOLT_START, BOLT_FRAMES / BOLT_FPS, BOLT_FPS)]
save(pixelate(bolt, finish(bolt, BOLT_W), BOLT_W, False), "bolt")
hf = frames(hit_mp4, 0, HIT_SECS, 24)
hf = [hf[int(i * len(hf) / HIT_FRAMES)] for i in range(HIT_FRAMES)]
hit = [(from_black if HIT_BG == "black" else from_white)(f) for f in hf]
save(pixelate(hit, finish(hit, HIT_PX), HIT_PX, True), "hit")
