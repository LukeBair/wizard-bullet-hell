"""Record where each frame's feet are, for cast shadows: adds "foot" (pixels from the bottom of the
frame up to the lowest opaque pixel) to every frame of Aseprite-style sheet JSON next to its PNG.
SpriteSheet reads it so SpriteShadow starts the shadow under the feet, not at the frame's padding.
Usage: python add_feet.py sheet.json [sheet.json ...]"""
import json, os, sys
from PIL import Image

for path in sys.argv[1:]:
    d = json.load(open(path))
    png = os.path.join(os.path.dirname(path), d["meta"].get("image") or os.path.basename(path)[:-5] + ".png")
    if not os.path.isfile(png):
        png = path[:-5] + ".png"
    im = Image.open(png).convert("RGBA")
    for f in d["frames"]:
        r = f["frame"]
        bb = im.crop((r["x"], r["y"], r["x"] + r["w"], r["y"] + r["h"])).getchannel("A").point(lambda v: 255 if v > 60 else 0).getbbox()
        f["foot"] = (r["h"] - bb[3]) if bb else 0
    json.dump(d, open(path, "w"), indent=1)
    print(os.path.basename(path), "feet added")
