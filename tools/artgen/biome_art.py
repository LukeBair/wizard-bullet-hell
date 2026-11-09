"""Procedural ground + prop art for the zones that have no 3D-rendered set: Arcane Academy, Crystal Caves,
Witch's Hollow (Whispering Forest already has TRELLIS-rendered art). Same file layout the game loads for the
forest, so Arena only needs a per-zone table:
  ground_<zone>_base.png / ground_<zone>_patch_<i>_<v>.png   (via tools/sprite-pipeline/ground_prep.py)
  prop_<name>_<k>_hi.png (2x) and prop_<name>_<k>_lo.png, k = 0..5
Usage: python biome_art.py OUT_DIR [zone ...]      needs numpy + pillow. Deterministic (seeded)."""
import os, subprocess, sys, tempfile
import numpy as np
from PIL import Image, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
GROUND_PREP = os.path.join(HERE, "..", "sprite-pipeline", "ground_prep.py")
LIGHT = np.array([-0.5, -0.62, 0.6]); LIGHT /= np.linalg.norm(LIGHT)   # from the upper left, like the forest props


def C(*a): return np.array(a, np.float32)


def ramp_color(ramp, t):
    """t in 0..1 (any shape) -> RGB through the ramp, snapped to len(ramp)*2 steps for a pixel-art look."""
    ramp = np.array(ramp, np.float32)
    t = np.round(np.clip(t, 0, 1) * (len(ramp) * 2 - 2)) / (len(ramp) * 2 - 2) * (len(ramp) - 1)
    i = np.clip(np.floor(t).astype(int), 0, len(ramp) - 2)
    f = (t - i)[..., None]
    return ramp[i] * (1 - f) + ramp[i + 1] * f


# ---------- noise ----------
def vnoise(n, cells, rng, tile=True):
    g = rng.random((cells, cells)).astype(np.float32)
    big = np.tile(g, (3, 3)) if tile else g
    im = Image.fromarray(big, "F").resize((big.shape[1] * n // cells, big.shape[0] * n // cells), Image.BICUBIC)
    a = np.asarray(im)
    return a[n:2 * n, n:2 * n] if tile else a[:n, :n]


def fbm(n, rng, base=4, octaves=4, tile=True):
    acc, amp, tot = np.zeros((n, n), np.float32), 1.0, 0.0
    for o in range(octaves):
        acc += amp * vnoise(n, base * 2 ** o, rng, tile); tot += amp; amp *= 0.5
    return acc / tot


def voronoi(n, cells, rng, jitter=0.8):
    """Tileable: (distance to nearest, distance to second, id)."""
    pts = [((i + 0.5 + (rng.random() - .5) * jitter) * n / cells, (j + 0.5 + (rng.random() - .5) * jitter) * n / cells, i * cells + j)
           for i in range(cells) for j in range(cells)]
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    d1 = np.full((n, n), 1e9, np.float32); d2 = d1.copy(); ids = np.zeros((n, n), int)
    for px, py, pid in pts:
        for ox in (-n, 0, n):
            for oy in (-n, 0, n):
                d = np.hypot(x - px - ox, y - py - oy)
                closer = d < d1
                d2 = np.where(closer, d1, np.minimum(d2, d)); ids = np.where(closer, pid, ids); d1 = np.where(closer, d, d1)
    return d1, d2, ids


def light_from_height(h, k=1.0):
    gy, gx = np.gradient(h)
    nx, ny, nz = -gx * k, -gy * k, np.ones_like(h)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.clip((nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]) / ln, 0, 1), nz / ln


def speckle(img, rng, colors, count, size=1):
    n = img.shape[0]
    for _ in range(count):
        x, y = rng.integers(0, n, 2); c = colors[rng.integers(0, len(colors))]
        for dx in range(size):
            for dy in range(size): img[(y + dy) % n, (x + dx) % n] = c


def crack(h, rng, n, depth=0.5, length=60):
    x, y = rng.random() * n, rng.random() * n; a = rng.random() * 6.28
    for _ in range(length):
        a += (rng.random() - .5) * 0.9; x += np.cos(a) * 2; y += np.sin(a) * 2
        h[int(y) % n, int(x) % n] -= depth


# ---------- ground textures (256px, seamless) ----------
def ground_academy(rng, variant, n=256):
    """Flagstones laid in staggered rows: mortar, bevelled edges, per-stone tone, cracks."""
    h = np.zeros((n, n), np.float32); tone = np.zeros((n, n), np.float32)
    rows = 4; rh = n // rows
    for r in range(rows):
        x = rng.integers(0, 40)
        while x < n + 40:
            w = int(rng.integers(44, 84)); t = rng.random()
            x1 = min(x + w, n + 40) if x + w < n + 40 else n + 40
            xs = np.arange(x, x1) % n
            # inset gives a bevel: height rises from the mortar to a flat top
            ix = np.minimum(np.arange(x1 - x), np.arange(x1 - x)[::-1]).astype(np.float32)
            iy = np.minimum(np.arange(rh), np.arange(rh)[::-1]).astype(np.float32)
            bev = np.clip(np.minimum(ix[None, :], iy[:, None]) / 4.0, 0, 1)
            h[r * rh:(r + 1) * rh, xs] = bev; tone[r * rh:(r + 1) * rh, xs] = t
            x += w
    h = h + fbm(n, rng, 8, 3) * 0.35
    tone = 0.38 + tone * 0.3 + fbm(n, rng, 6, 3) * 0.3
    for _ in range([6, 14, 3][variant % 3]): crack(h, rng, n, 0.7)
    lit, _ = light_from_height(h, 2.2)
    v = np.clip(tone * 0.55 + lit * 0.5 + (h - h.mean()) * 0.25 - (h < 0.15) * 0.4, 0, 1)
    img = ramp_color([C(24, 21, 33), C(40, 35, 52), C(59, 53, 74), C(82, 75, 98), C(108, 100, 125)], v * 0.85)
    if variant == 1:    # moss and damp in the mortar
        m = fbm(n, rng, 5, 4); img = np.where((m > 0.6)[..., None], img * C(0.7, 1.15, 0.8), img)
    if variant == 2:    # dust drifts
        m = fbm(n, rng, 4, 3); img = img * (1 - 0.25 * (m > 0.55)[..., None]) + 0.25 * (m > 0.55)[..., None] * C(120, 112, 130)
    speckle(img, rng, [C(18, 15, 26), C(95, 88, 112)], 90)
    return img * 0.82


def ground_caves(rng, variant, n=256):
    """Cracked rock plates with dark seams, glints of crystal dust."""
    cells = [7, 9, 5][variant % 3]
    d1, d2, ids = voronoi(n, cells, rng, 0.9)
    seam = np.clip((d2 - d1) / 5.0, 0, 1)
    plate = np.array([rng.random() for _ in range(cells * cells)])[ids % (cells * cells)]
    h = seam * 0.6 + fbm(n, rng, 10, 4) * 0.5 + plate * 0.1
    lit, _ = light_from_height(h, 2.0)
    v = np.clip(0.2 + plate * 0.18 + lit * 0.32 + fbm(n, rng, 6, 4) * 0.25 - (seam < 0.25) * 0.2, 0, 1)
    img = ramp_color([C(14, 14, 22), C(26, 27, 38), C(40, 42, 58), C(58, 61, 82), C(80, 86, 110)], v)
    cols = [[C(110, 235, 255), C(60, 150, 190)], [C(190, 140, 255), C(120, 80, 190)], [C(110, 235, 255), C(190, 140, 255)]][variant % 3]
    speckle(img, rng, cols, [40, 60, 90][variant % 3])
    if variant == 1:    # wet sheen
        m = fbm(n, rng, 4, 3); img = np.where((m > 0.62)[..., None], img * C(0.8, 0.95, 1.25), img)
    return np.clip(img, 0, 255)


def ground_hollow(rng, variant, n=256):
    """Black swamp mud: moss clumps, standing puddles, roots, fallen leaves."""
    h = fbm(n, rng, 6, 5)
    lit, _ = light_from_height(h, 5.0)
    v = np.clip(0.25 + h * 0.35 + lit * 0.25, 0, 1)
    img = ramp_color([C(14, 17, 13), C(24, 29, 20), C(37, 45, 29), C(54, 66, 38), C(76, 90, 50)], v)
    moss = fbm(n, rng, 5, 4)
    thr = [0.58, 0.5, 0.66][variant % 3]
    img = np.where((moss > thr)[..., None], ramp_color([C(16, 28, 14), C(26, 46, 20), C(42, 70, 28), C(62, 92, 36)], (moss - thr) * 5 + 0.25 * h), img)
    if variant == 1:    # puddles: dark water with a cold sheen on the lit edge
        w = fbm(n, rng, 3, 3); mask = w > 0.58
        gy, gx = np.gradient(w); sheen = np.clip(-(gx + gy) * 40, 0, 1)
        water = ramp_color([C(10, 15, 16), C(18, 30, 30), C(46, 70, 66)], 0.15 + sheen * 0.8)
        img = np.where(mask[..., None], water, img)
    for _ in range([8, 5, 14][variant % 3]):       # roots
        x, y, a = rng.random() * n, rng.random() * n, rng.random() * 6.28
        for _ in range(50):
            a += (rng.random() - .5) * 0.5; x += np.cos(a) * 2; y += np.sin(a) * 2
            for dx, dy, c in ((0, 0, C(30, 24, 18)), (-1, -1, C(60, 48, 32))): img[int(y + dy) % n, int(x + dx) % n] = c
    speckle(img, rng, [C(90, 62, 30), C(60, 80, 30), C(120, 255, 110)], 55)
    return img


GROUND = {"academy": ground_academy, "caves": ground_caves, "hollow": ground_hollow}


def make_ground(out, zone, seed):
    rng = np.random.default_rng(seed)
    d = tempfile.mkdtemp()
    paths = []
    for v in range(3):
        p = os.path.join(d, f"t{v}.png"); Image.fromarray(np.clip(GROUND[zone](rng, v), 0, 255).astype(np.uint8)).save(p); paths.append(p)
    # ground_prep.py's seamless() blends the base tile with a shifted copy; ours already tile, so it only softens it.
    subprocess.run([sys.executable, GROUND_PREP, out, zone, "256", *paths], check=True)


# ---------- props: heightfield + material -> shaded sprite ----------
class Canvas:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.H = np.zeros((h, w), np.float32); self.mat = np.zeros((h, w), np.int8); self.emit = np.zeros((h, w), np.float32)
        self.y, self.x = np.mgrid[0:h, 0:w].astype(np.float32)

    def noise(self, cells, rng):
        return vnoise(max(self.w, self.h), cells, rng, False)[:self.h, :self.w]

    def stamp(self, mask, height, mat=0, emit=None):
        up = mask & (height >= self.H - 0.001)
        self.H = np.where(up, height, self.H); self.mat = np.where(up, mat, self.mat)
        if emit is not None: self.emit = np.where(up, emit, self.emit)

    def blob(self, cx, cy, rx, ry, hgt, noise=0.0, rng=None, mat=0):
        d = ((self.x - cx) / rx) ** 2 + ((self.y - cy) / ry) ** 2
        if rng is not None and noise: d = d * (1 + noise * (vnoise(max(self.w, self.h), 6, rng, False)[:self.h, :self.w] - 0.5))
        self.stamp(d < 1, np.sqrt(np.clip(1 - d, 0, 1)) * hgt, mat)

    def stroke(self, p0, p1, r0, r1, hscale=1.0, mat=0, steps=None):
        steps = steps or int(max(2, np.hypot(p1[0] - p0[0], p1[1] - p0[1]) * 1.5))
        for t in np.linspace(0, 1, steps):
            r = r0 + (r1 - r0) * t; cx, cy = p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t
            d = np.hypot(self.x - cx, self.y - cy) / r
            self.stamp(d < 1, np.sqrt(np.clip(1 - d * d, 0, 1)) * r * hscale + 0.001, mat)

    def render(self, ramps, rng, grain=0.12, emit_color=None):
        M = self.H > 0.0005
        lit, nz = light_from_height(self.H, 0.9)
        tex = fbm(max(self.w, self.h), rng, 10, 3, False)[:self.h, :self.w]
        occl = np.clip(1 - np.exp(-(self.H) * 0.5), 0, 1)
        t = np.clip(0.12 + lit * 0.62 + (tex - 0.5) * grain * 2 + occl * 0.18, 0, 1)
        t = t * np.clip(0.55 + (self.H.shape[0] - self.y) / self.H.shape[0] * 0.0 + 0.45 * np.clip((self.H.shape[0] - self.y) / 24, 0, 1) * 0 + 0.45, 0, 1)
        img = np.zeros((self.h, self.w, 3), np.float32)
        for i, ramp in enumerate(ramps):
            img = np.where((self.mat == i)[..., None], ramp_color(ramp, t), img)
        if emit_color is not None:
            e = self.emit[..., None]; img = img * (1 - e) + emit_color(self, t) * e
        # dark rim where the shape meets the background, so it reads on any floor
        er = Image.fromarray((M * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(3))
        rim = M & (np.asarray(er) < 128)
        img = np.where(rim[..., None], img * 0.45, img)
        out = np.zeros((self.h, self.w, 4), np.uint8); out[..., :3] = np.clip(img, 0, 255); out[..., 3] = M * 255
        return Image.fromarray(out, "RGBA")


def save_prop(out, name, k, im):
    # hi is the 2x master; lo is the same art at half size (hard alpha), like the forest props
    a = im.crop(im.getbbox()); pad = Image.new("RGBA", (a.width + 4, a.height + 4)); pad.paste(a, (2, 2))
    hi = pad
    lo = hi.resize((max(1, hi.width // 2), max(1, hi.height // 2)), Image.LANCZOS)
    al = lo.getchannel("A").point(lambda v: 255 if v > 110 else 0); lo.putalpha(al)
    hi.save(os.path.join(out, f"prop_{name}_{k}_hi.png")); lo.save(os.path.join(out, f"prop_{name}_{k}_lo.png"))


STONE = [C(22, 19, 30), C(44, 39, 58), C(74, 68, 92), C(112, 104, 132), C(150, 142, 170)]
MOSS = [C(14, 28, 14), C(30, 54, 24), C(56, 92, 36), C(90, 130, 56)]
CAVE = [C(16, 17, 26), C(32, 34, 50), C(52, 56, 78), C(80, 86, 114), C(112, 120, 150)]
BARK = [C(14, 15, 12), C(30, 32, 25), C(52, 54, 42), C(80, 82, 66), C(112, 112, 92)]
SWAMP_MOSS = [C(16, 26, 14), C(34, 54, 22), C(64, 92, 34), C(104, 130, 52)]


def column_full(rng):
    c = Canvas(76, 190); cx = 38
    top, base = 28, 172
    # shaft with fluting
    sx = (c.x - cx) / 21.0
    flute = 1 - 0.12 * (np.abs(np.sin(sx * 3.6)))
    c.stamp((np.abs(sx) < 1) & (c.y > top) & (c.y < base), np.sqrt(np.clip(1 - sx ** 2, 0, 1)) * 18 * flute, 0)
    # plinth + capital: stacked slabs, each lit as a box
    for y0, y1, hw, hh in ((base, 188, 31, 9), (160, base + 1, 26, 9), (top - 4, top + 6, 25, 7), (10, top - 3, 32, 10), (0, 12, 36, 7)):
        bx = np.clip(1 - (np.abs(c.x - cx) / hw) ** 6, 0, 1); by = np.clip(np.minimum(c.y - y0, y1 - c.y) / 3.0, 0, 1)
        c.stamp((np.abs(c.x - cx) < hw) & (c.y >= y0) & (c.y < y1), hh * np.clip(by * 1.2, 0.2, 1) * (0.7 + 0.3 * bx), 0)
    # volutes
    for s in (-1, 1): c.blob(cx + s * 27, 18, 6, 6, 9, mat=0)
    # chips, a crack, a patch of moss at the foot
    for _ in range(int(rng.integers(3, 7))):
        c.blob(rng.integers(8, 68), rng.integers(30, 170), rng.integers(2, 4), rng.integers(2, 5), -1.0)
    c.H = np.maximum(c.H, 0)
    mm = (c.y > 140) & (c.noise(5, rng) > 0.55)
    c.mat = np.where(mm & (c.H > 0), 1, c.mat)
    return c.render([STONE, MOSS], rng)


def column_broken(rng):
    c = Canvas(110, 128); cx = 44
    topy = int(rng.integers(50, 80)); tilt = rng.uniform(-0.4, 0.4)
    sx = (c.x - cx) / 21.0
    jag = topy + (c.x - cx) * tilt + (c.noise(12, rng) - 0.5) * 14
    c.stamp((np.abs(sx) < 1) & (c.y > jag) & (c.y < 118), np.sqrt(np.clip(1 - sx ** 2, 0, 1)) * 18 * (1 - 0.12 * np.abs(np.sin(sx * 3.6))), 0)
    bx = np.clip(1 - (np.abs(c.x - cx) / 30) ** 6, 0, 1)
    c.stamp((np.abs(c.x - cx) < 30) & (c.y >= 116) & (c.y < 127), 8 * (0.7 + 0.3 * bx), 0)
    c.blob(cx + 41, 112, 18, 12, 11, 0.3, rng); c.blob(cx - 38, 117, 12, 8, 8, 0.3, rng)           # fallen blocks
    c.stamp((np.abs(c.x - (cx + 40)) < 12) & (c.y > 100) & (c.y < 108), 6, 0)
    mm = c.noise(5, rng) > 0.58
    c.mat = np.where(mm & (c.y > 85) & (c.H > 0), 1, c.mat)
    return c.render([STONE, MOSS], rng)


def rubble(rng):
    c = Canvas(110, 70)
    for _ in range(int(rng.integers(5, 9))):
        cx, cy = rng.integers(14, 96), rng.integers(30, 60); rx, ry = rng.integers(8, 20), rng.integers(6, 13)
        a = rng.uniform(-0.5, 0.5); dx, dy = c.x - cx, c.y - cy
        u, v = dx * np.cos(a) + dy * np.sin(a), -dx * np.sin(a) + dy * np.cos(a)
        bevel = np.clip(np.minimum(rx - np.abs(u), ry - np.abs(v)) / 3.0, 0, 1)
        c.stamp((np.abs(u) < rx) & (np.abs(v) < ry), bevel * (5 + 3 * rng.random()) + 0.5, 0)
    mm = c.noise(4, rng) > 0.62
    c.mat = np.where(mm & (c.H > 0), 1, c.mat)
    return c.render([STONE, MOSS], rng)


def crystal_cluster(rng, k):
    c = Canvas(120, 190); base = 176; hue = k % 2
    n = int(rng.integers(4, 8)); spec = []
    for i in range(n):
        h = rng.uniform(70, 165) * (1.0 if i == 0 else 0.7); w = rng.uniform(10, 17); x0 = 60 + rng.uniform(-30, 30); lean = rng.uniform(-0.28, 0.28)
        if i == 0: x0, lean, h = 60, rng.uniform(-.1, .1), 160
        spec.append((h, w, x0, lean))
    for h, w, x0, lean in sorted(spec, key=lambda s: s[0]):    # short ones first so tall ones sit behind... then taller overwrite
        ty = base - h; tx = x0 + lean * h
        # axis from foot to tip; hexagonal prism: width tapers in the last 25% to a point
        t = np.clip((base - c.y) / max(h, 1), 0, 1.2)
        axis = x0 + (tx - x0) * np.clip(t, 0, 1)
        half = w * np.where(t < 0.75, 1.0, np.clip((1.0 - t) / 0.25, 0, 1)) * 0.5 * 2
        u = (c.x - axis) / np.maximum(half, 0.5)
        m = (np.abs(u) < 1) & (c.y > ty) & (c.y < base + 4)
        facet = np.where(u < -0.1, 0.6, np.where(u < 0.35, 1.0, 0.45))    # three flat facets
        c.stamp(m, (6 + 5 * facet) + c.y * 0.01 + h * 0.01, hue, np.clip(0.35 + 0.65 * (1 - t) * 0.0 + (u < 0.35) * 0.0 + 0.3 * facet, 0, 1))
    c.blob(40, 180, 18, 7, 6, 0.6, rng, mat=2); c.blob(82, 179, 20, 8, 7, 0.6, rng, mat=2)   # rock at the foot
    def emit_col(cv, t):
        pal = [C(40, 215, 255), C(150, 245, 255)] if True else None
        pal2 = [C(150, 90, 255), C(220, 180, 255)]
        lo_, hi_ = [(C(30, 150, 200), C(150, 245, 255)), (C(110, 60, 200), C(225, 185, 255))][hue]
        out = lo_ + (hi_ - lo_) * np.clip(t[..., None] * 1.3, 0, 1)
        return out
    ramps = [[C(18, 90, 140), C(40, 170, 215), C(110, 230, 255), C(210, 252, 255)], [C(60, 30, 120), C(120, 70, 200), C(180, 130, 245), C(240, 215, 255)], CAVE]
    return c.render(ramps, rng, grain=0.03)


def stalagmite(rng):
    c = Canvas(88, 190); cx = 44; base = 180
    for layer in range(3):
        top = int(rng.integers(20, 60)) + layer * 35; off = (layer - 1) * 12 + rng.uniform(-6, 6); w = [26, 20, 16][layer] + rng.uniform(-3, 3)
        t = np.clip((base - c.y) / (base - top), 0, 1)
        half = w * (1 - t) ** 0.8 + 1.0
        edge = c.noise(14, rng) - 0.5
        u = (c.x - cx - off - edge * 7) / half
        m = (np.abs(u) < 1) & (c.y > top) & (c.y < base + 3)
        strata = 0.5 + 0.5 * np.sin(c.y * 0.35 + layer * 2)
        c.stamp(m, np.sqrt(np.clip(1 - u * u, 0, 1)) * half * 0.9 + strata * 0.8, 2)
    c.blob(cx - 20, 183, 14, 6, 5, 0.6, rng, mat=2); c.blob(cx + 20, 183, 16, 7, 6, 0.6, rng, mat=2)
    return c.render([CAVE, CAVE, CAVE], rng, grain=0.16)


def rockpile_cave(rng):
    c = Canvas(112, 74)
    for _ in range(int(rng.integers(3, 6))):
        cx, cy = rng.integers(20, 92), rng.integers(34, 60); rx, ry = rng.integers(14, 26), rng.integers(10, 20)
        c.blob(cx, cy, rx, ry, rng.uniform(8, 14), 0.5, rng, mat=0)
    # glowing vein flecks, hung on the lit-ish areas
    em = (c.noise(9, rng) > 0.7).astype(np.float32) * (c.H > 2)
    c.emit = em
    cc = [C(110, 235, 255), C(190, 140, 255)][int(rng.integers(0, 2))]
    return c.render([CAVE], rng, grain=0.18, emit_color=lambda cv, t: np.broadcast_to(cc, (cv.h, cv.w, 3)))


def swamp_tree(rng):
    c = Canvas(230, 280); cx = 115
    # trunk: leaning S curve; roots flare at the base
    pts = [(cx + rng.uniform(-6, 6), 270), (cx + rng.uniform(-14, 14), 220), (cx + rng.uniform(-18, 18), 170), (cx + rng.uniform(-10, 10), 130)]
    for a, b, r0, r1 in zip(pts, pts[1:], (22, 17, 12), (17, 12, 8)): c.stroke(a, b, r0, r1, 0.9, 0)
    for s in (-1, 1): c.stroke((cx, 260), (cx + s * rng.uniform(36, 52), 276), 10, 3, 0.9, 0)
    # twisted branches (recursive) and dripping moss at the tips
    def branch(p, ang, ln, r, d):
        if d == 0 or ln < 8: return
        q = (p[0] + np.cos(ang) * ln, p[1] + np.sin(ang) * ln)
        c.stroke(p, q, r, max(r * 0.65, 1.6), 0.9, 0)
        branch(q, ang + rng.uniform(0.2, 0.75), ln * 0.72, r * 0.65, d - 1)
        branch(q, ang - rng.uniform(0.2, 0.75), ln * 0.72, r * 0.65, d - 1)
        if d <= 2 and rng.random() < 0.8:
            for _ in range(int(rng.integers(1, 3))):
                mx = q[0] + rng.uniform(-6, 6); c.stroke((mx, q[1]), (mx + rng.uniform(-3, 3), q[1] + rng.uniform(20, 55)), 2.2, 0.8, 1.0, 1)
    top = pts[-1]
    branch(top, -1.57 + rng.uniform(-.5, .5), 42, 8, 4)
    branch((pts[2][0], pts[2][1]), -0.5 + rng.uniform(-.4, .2), 50, 6, 3)
    branch((pts[2][0], pts[2][1]), -2.6 + rng.uniform(-.2, .4), 50, 6, 3)
    mm = (c.noise(7, rng) > 0.6) & (c.mat == 0) & (c.H > 0)
    c.mat = np.where(mm, 1, c.mat)
    return c.render([BARK, SWAMP_MOSS], rng, grain=0.2)


def stump(rng):
    c = Canvas(100, 80); cx = 50
    c.stamp((np.abs(c.x - cx) < 32 + 3 * np.sin(c.y * 0.3)) & (c.y > 30) & (c.y < 76), 12 * np.sqrt(np.clip(1 - ((c.x - cx) / 36) ** 2, 0, 1)) + 2, 0)
    c.blob(cx, 32, 31, 11, 8, 0.2, rng, mat=0)                      # the cut top
    c.stamp(((c.x - cx) / 22) ** 2 + ((c.y - 32) / 7) ** 2 < 1, 11, 1 if rng.random() < 0.7 else 0)   # moss cap
    for s in (-1, 1): c.stroke((cx + s * 28, 72), (cx + s * rng.uniform(38, 46), 78), 7, 2, 0.9, 0)
    return c.render([BARK, SWAMP_MOSS], rng, grain=0.2)


def moss_rock(rng):
    c = Canvas(110, 76)
    for _ in range(int(rng.integers(2, 4))):
        c.blob(rng.integers(26, 84), rng.integers(38, 56), rng.integers(16, 28), rng.integers(12, 20), rng.uniform(9, 15), 0.5, rng, 0)
    _, nz = light_from_height(c.H, 0.9)
    c.mat = np.where((nz > 0.8) & (c.H > 0), 1, c.mat)
    return c.render([[C(18, 20, 16), C(38, 42, 32), C(66, 70, 54), C(98, 102, 80)], SWAMP_MOSS], rng, grain=0.2)


PROPS = {
    "academy": [("column_a", lambda r, k: column_full(r)), ("column_b", lambda r, k: column_broken(r)), ("rubble_a", lambda r, k: rubble(r))],
    "caves": [("crystal_a", crystal_cluster), ("stalagmite_a", lambda r, k: stalagmite(r)), ("rockpile_a", lambda r, k: rockpile_cave(r))],
    "hollow": [("tree_swamp", lambda r, k: swamp_tree(r)), ("stump_a", lambda r, k: stump(r)), ("mossrock_a", lambda r, k: moss_rock(r))],
}


def make_props(out, zone, seed):
    for pi, (name, fn) in enumerate(PROPS[zone]):
        for k in range(6):
            rng = np.random.default_rng(seed * 1000 + pi * 10 + k)
            save_prop(out, name, k, fn(rng, k))


if __name__ == "__main__":
    out = sys.argv[1]; zones = sys.argv[2:] or list(GROUND)
    os.makedirs(out, exist_ok=True)
    for i, z in enumerate(zones):
        make_ground(out, z, 11 + i); make_props(out, z, 5 + i); print("done", z)
