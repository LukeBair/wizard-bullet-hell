"""Trim rendered prop variants (render_prop.py) to the prop, scale them to their in-game pixel height,
palette-reduce the colour and copy colour + normal map into the game as prop_<name>_<k>_<tier>(.png,
_n.png): tier "hi" at the given height (the 128px Character art) and "lo" at half (the 64px one).
Usage: python trim_props.py renders_dir game_art_dir name=height_px [name=height_px ...]"""
import glob, os, sys
from PIL import Image

src, dst = sys.argv[1], sys.argv[2]
heights = dict((k, int(v)) for k, v in (a.split("=") for a in sys.argv[3:]))
for name, h in heights.items():
    for col in sorted(glob.glob(os.path.join(src, f"{name}_[0-9].png"))):
        k = os.path.basename(col)[len(name) + 1:-4]
        c = Image.open(col).convert("RGBA")
        n = Image.open(col[:-4] + "_n.png").convert("RGBA")
        bb = c.getchannel("A").point(lambda v: 255 if v > 30 else 0).getbbox()
        c0, n0 = c.crop(bb), n.crop(bb)
        for tier, th, colors in (("hi", h, 48), ("lo", h // 2, 32)):
            w = max(1, round(c0.width * th / c0.height))
            c, n = c0.resize((w, th), Image.LANCZOS), n0.resize((w, th), Image.LANCZOS)
            a = c.getchannel("A").point(lambda v: 255 if v > 110 else 0)
            rgb = Image.new("RGB", c.size, (0, 0, 0)); rgb.paste(c.convert("RGB"), (0, 0), a)
            rgb = rgb.quantize(colors=colors, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.NONE).convert("RGBA")
            rgb.putalpha(a)
            n.putalpha(a)
            rgb.save(os.path.join(dst, f"prop_{name}_{k}_{tier}.png"))
            n.save(os.path.join(dst, f"prop_{name}_{k}_{tier}_n.png"))
            print(f"prop_{name}_{k}_{tier}", rgb.size)
