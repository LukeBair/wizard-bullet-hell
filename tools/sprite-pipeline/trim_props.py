"""Trim rendered prop variants (render_prop.py) to the prop, scale them to their in-game pixel height,
palette-reduce the colour and copy colour + normal map into the game as prop_<name>_<k>_<tier>(.png,
_n.png): tier "hi" at the given height (the 128px Character art) and "lo" at half (the 64px one).
Usage: python trim_props.py renders_dir game_art_dir name=height_px [name=height_px ...]"""
import glob, json, os, sys
import numpy as np
from PIL import Image

src, dst = sys.argv[1], sys.argv[2]
heights = dict((k, int(v)) for k, v in (a.split("=") for a in sys.argv[3:]))
PIVOT_Y = 0.08                                  # ArtStyle.Prop's pivot, as a fraction of the height
pivots_path = os.path.join(dst, "prop_shadows.json")
pivots = json.load(open(pivots_path)) if os.path.isfile(pivots_path) else {}
for name, h in heights.items():
    for col in sorted(glob.glob(os.path.join(src, f"{name}_[0-9].png"))):
        k = os.path.basename(col)[len(name) + 1:-4]
        c = Image.open(col).convert("RGBA")
        n = Image.open(col[:-4] + "_n.png").convert("RGBA")
        bb = c.getchannel("A").point(lambda v: 255 if v > 30 else 0).getbbox()
        c0, n0 = c.crop(bb), n.crop(bb)
        # baked shadow (render_prop.py): rendered at 2x frame, so offset by half the colour frame
        sh_path = col[:-4] + "_shadow.png"
        shadow = None
        if os.path.isfile(sh_path):
            L = np.asarray(Image.open(sh_path).convert("L"), np.float32)
            dark = np.clip((1 - L / max(L.max(), 1)) * 1.4, 0, 1)        # 1 = full shadow
            shadow = Image.fromarray((dark * 255).astype(np.uint8))
            half = Image.open(col).width // 2
            px, py = (bb[0] + bb[2]) / 2 + half, bb[3] - PIVOT_Y * (bb[3] - bb[1]) + half
            sbb = shadow.point(lambda v: 255 if v > 12 else 0).getbbox()
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
            if shadow is not None and sbb:
                f = th / c0.height                                      # same scale as the colour sprite
                s = shadow.crop(sbb)
                s = s.resize((max(1, round(s.width * f)), max(1, round(s.height * f))), Image.LANCZOS)
                out = Image.new("RGBA", s.size, (0, 0, 0, 0)); out.putalpha(s)
                out.save(os.path.join(dst, f"prop_{name}_{k}_{tier}_shadow.png"))
                pivots[f"prop_{name}_{k}_{tier}_shadow"] = [round((px - sbb[0]) / (sbb[2] - sbb[0]), 4), round((sbb[3] - py) / (sbb[3] - sbb[1]), 4)]
            print(f"prop_{name}_{k}_{tier}", rgb.size)
# one entry per line ("name": [x, y]), which ArtStyle.PropShadow reads without a JSON library
with open(pivots_path, "w") as fh:
    fh.write("{\n" + ",\n".join(f' "{k}": [{v[0]}, {v[1]}]' for k, v in sorted(pivots.items())) + "\n}\n")
