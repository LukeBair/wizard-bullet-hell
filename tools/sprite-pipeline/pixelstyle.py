"""Bakes keyed video frames into the game's pixel style: shrunk to the texels they'll cover in game at their
default size (so PixelDensity leaves them alone), with one small palette shared by all of an effect's frames
and stepped alpha. Frames straight from the videos read as photos next to the 3D-rendered sprites.
Texels per world unit = art.refHeight / (2 * GameManager.CameraSize) / art.pxPerTexel = 1080 / 15 / 1.5."""
import numpy as np
from PIL import Image, ImageFilter

TPU = 1080 / 15 / 1.5

def bake(frames, units, colours, steps, smooth=0):
    """frames: RGBA images all the same size; units: their width in game (world units). Returns the baked frames."""
    w = max(1, round(units * TPU))
    h = max(1, round(w * frames[0].height / frames[0].width))
    small = []
    for im in frames:
        if smooth: im = im.filter(ImageFilter.MedianFilter(smooth))   # flattens photo grain before it becomes noise
        small.append(im.resize((w, h), Image.BOX))
    # one palette for the whole effect, from its frames side by side, so colours don't flicker frame to frame
    sheet = Image.new("RGB", (w * len(small), h))
    for i, im in enumerate(small):
        a = np.asarray(im).astype(np.float32)
        sheet.paste(Image.fromarray(np.where(a[..., 3:] > 64, a[..., :3], 0).astype(np.uint8)), (i * w, 0))
    pal = sheet.quantize(colours, method=Image.Quantize.MEDIANCUT)
    out = []
    for im in small:
        a = np.asarray(im).astype(np.float32)
        al = np.round(a[..., 3] / 255 * steps) / steps     # alpha in a few hard steps
        rgb = Image.fromarray(a[..., :3].astype(np.uint8)).quantize(palette=pal, dither=Image.Dither.NONE).convert("RGB")
        out.append(Image.fromarray(np.concatenate([np.asarray(rgb), (al[..., None] * 255).astype(np.uint8)], 2), "RGBA"))
    return out
