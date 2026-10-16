"""Prepare ground textures for the in-game mixer (Arena.BuildGround): a seamless base tile plus
soft-edged "patches" of every texture that the game scatters on top at random scales and rotations,
so the floor never shows a repeat.
Usage: python ground_prep.py out_dir zone tile_px tex1 [tex2 ...]
Writes ground_<zone>_base.png (tex1 made seamless) and ground_<zone>_patch_<i>_<v>.png (each texture
under two different cloud-shaped alpha masks)."""
import os, sys
import numpy as np
from PIL import Image, ImageFilter

out, zone, px = sys.argv[1], sys.argv[2], int(sys.argv[3])
texs = [Image.open(p).convert("RGB") for p in sys.argv[4:]]
rng = np.random.default_rng(7)


def square(im, size):
    s = min(im.size)
    return im.crop(((im.width - s) // 2, (im.height - s) // 2, (im.width + s) // 2, (im.height + s) // 2)).resize((size, size), Image.LANCZOS)


def seamless(im):
    """Blend the image with itself shifted by half, weighted so each edge comes from the shifted copy."""
    a = np.asarray(im).astype(np.float32)
    b = np.roll(np.roll(a, a.shape[0] // 2, 0), a.shape[1] // 2, 1)
    n = a.shape[0]
    t = np.abs(np.linspace(-1, 1, n))
    w = np.clip((np.maximum(t[:, None], t[None, :]) - 0.55) / 0.45, 0, 1)[..., None]   # 1 at the edges
    return Image.fromarray((a * (1 - w) + b * w).astype(np.uint8))


def cloud_mask(n, seed):
    r = np.random.default_rng(seed)
    acc = np.zeros((n, n), np.float32)
    for octave, amp in ((4, 1.0), (8, 0.5), (16, 0.25)):
        g = Image.fromarray((r.random((octave, octave)) * 255).astype(np.uint8)).resize((n, n), Image.BICUBIC)
        acc += amp * np.asarray(g, np.float32) / 255
    acc /= 1.75
    y, x = np.mgrid[0:n, 0:n] / (n - 1) * 2 - 1
    radial = np.clip(1 - np.sqrt(x * x + y * y), 0, 1)          # fades to nothing at the rim
    m = np.clip((acc * 0.9 + radial * 1.1 - 0.75) * 3.0, 0, 1) * np.clip(radial * 3, 0, 1)
    return Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(n / 64))


os.makedirs(out, exist_ok=True)
seamless(square(texs[0], px)).save(os.path.join(out, f"ground_{zone}_base.png"))
for i, t in enumerate(texs):
    tile = square(t, px)
    for v in range(2):
        p = tile.convert("RGBA")
        p.putalpha(cloud_mask(px, 100 * i + v))
        p.save(os.path.join(out, f"ground_{zone}_patch_{i}_{v}.png"))
print("ground", zone, len(texs), "textures")
