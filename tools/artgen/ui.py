# Draws the 9-slice UI textures (panel, button, bar frame, spell slot) for the pixel styles.
# Run: python3 tools/artgen/ui.py   (needs Pillow). Borders must match UI.cs (panel/button 5, bar 3, slot 4).
from PIL import Image
import os

def hexc(h, a=255):
    return (int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16), a)

STYLES = {
    'Dusk': dict(outline='#141120', light='#3e5a4e', rim='#2b2f4a', fill='#1d1a2b', face='#2b2f4a',
                 trough='#141120', stud='#f0d2bc'),
    'Crt': dict(outline='#000000', light='#8b9bb4', rim='#5a6988', fill='#262b44', face='#3a4466',
                trough='#181425', stud='#feae34'),
}

def frame(n, c, fill, fill_alpha=255, top_light=True):
    """Rounded 1px outline, a 1px bevel ring (lit on top/left) and a flat centre."""
    im = Image.new('RGBA', (n, n), (0, 0, 0, 0))
    px = im.load()
    for y in range(n):
        for x in range(n):
            edge = min(x, y, n - 1 - x, n - 1 - y)
            corner = (x in (0, n - 1)) and (y in (0, n - 1))
            if corner:
                continue
            if edge == 0:
                px[x, y] = hexc(c['outline'])
            elif edge == 1:
                lit = top_light and (y == 1 or x == 1) and not (y == n - 2 or x == n - 2)
                px[x, y] = hexc(c['light'] if lit else c['rim'])
            else:
                px[x, y] = hexc(fill, fill_alpha)
    # fill in the inner corners of the outline so the rounding reads as one pixel
    for x, y in ((1, 1), (n - 2, 1), (1, n - 2), (n - 2, n - 2)):
        px[x, y] = hexc(c['outline'])
    return im, px

def studs(px, n, c, inset):
    for x, y in ((inset, inset), (n - 1 - inset, inset), (inset, n - 1 - inset), (n - 1 - inset, n - 1 - inset)):
        px[x, y] = hexc(c['stud'])

def panel(c):
    im, px = frame(16, c, c['fill'], 242)
    studs(px, 16, c, 3)
    return im

def button(c):
    im, px = frame(16, c, c['face'])
    # Two-pixel dark lip along the bottom so it reads as pressable.
    for x in range(2, 14):
        px[x, 13] = hexc(c['outline'])
        px[x, 12] = hexc(c['trough'])
    for x in range(2, 14):
        px[x, 2] = hexc(c['light'])
    return im

def bar(c):
    im, px = frame(8, c, c['trough'], 230, top_light=False)
    return im

def slot(c):
    im, px = frame(12, c, c['fill'], 220)
    studs(px, 12, c, 2)
    return im

root = os.path.join(os.path.dirname(__file__), '..', '..', 'Assets', 'Resources', 'Art')
for style, c in STYLES.items():
    for name, fn in (('panel', panel), ('button', button), ('bar', bar), ('slot', slot)):
        path = os.path.join(root, style, f'ui_{name}.png')
        fn(c).save(path)
        print(path)
