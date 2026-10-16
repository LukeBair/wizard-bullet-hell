"""Render a looping enemy animation from a TRELLIS GLB with no armature: vertices are moved directly
with numpy each frame, so any creature works as long as its parts can be picked out by position.
Usage: Blender -b -P render_enemy_loop.py -- model.glb out_dir mode [key=value ...]
Modes:
  quad   four-legged trot. Legs are the vertices below leg=<fraction of height> (default 0.42),
         split front/back along the body's long axis and left/right across it; diagonal pairs swing
         together. swing=<deg> (default 24).
  flap   winged flyer. Wings are the vertices further than body=<fraction of half-width> (default
         0.45) from the centre along the widest horizontal axis (the wingspan); they rotate up and
         down about the body's edge.
         flap=<deg> (default 35).
  biped  two-legged walk for upright characters facing the camera at az 0 (TRELLIS's default). Legs
         are the vertices below leg=<fraction> (default 0.32), split left/right; they swing forward
         and back in turn while the body sways. swing=<deg> (default 22), sway=<deg> (default 4).
  squash bouncing blob: squashes and stretches about its base and hops. amount=<fraction> (0.12).
  still  no deformation, just the bob (for a turntable check).
Common: frames=<n> (8), az=<camera azimuth deg, 0 = model front> (-90 = side), tilt=<deg> (25),
        dirs=8 (render all eight facings into out_dir/<s|se|e|ne|n|nw|w|sw>/; az is then ignored),
        res=<px> (256), bob=<fraction of height> (0.02), light=<sun strength> (4),
        emit=<strength> (0; feeds the base colour into emission, for ghosts and to lift dark models)."""
import math, os, sys
import bpy
import numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
src, out, mode = a[0], a[1], a[2]
opt = dict(kv.split("=") for kv in a[3:])
N = int(opt.get("frames", 8))
AZ = math.radians(float(opt.get("az", -90)))
TILT = math.radians(float(opt.get("tilt", 25)))
RES = int(opt.get("res", 256))
os.makedirs(out, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
mesh = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
bpy.context.view_layer.objects.active = mesh
mesh.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
nv = len(mesh.data.vertices)
co = np.empty(nv * 3)
mesh.data.vertices.foreach_get("co", co)
co = co.reshape(-1, 3)
lo, hi = co.min(0), co.max(0)
ctr = (lo + hi) / 2
height = hi[2] - lo[2]

# Long horizontal axis of the body (PCA on x, y)
xy = co[:, :2] - co[:, :2].mean(0)
evals, evecs = np.linalg.eigh(xy.T @ xy)
long_ax = np.array([*evecs[:, 1], 0.0])
side_ax = np.array([*evecs[:, 0], 0.0])


def rotate(p, pivot, axis, ang):
    """Rodrigues rotation of points p (n,3) about an axis through pivot; ang is per-point (n,)."""
    k = axis / np.linalg.norm(axis)
    v = p - pivot
    c, s = np.cos(ang)[:, None], np.sin(ang)[:, None]
    return pivot + v * c + np.cross(k, v) * s + k * (v @ k)[:, None] * (1 - c)


if mode == "quad":
    zb = lo[2] + float(opt.get("leg", 0.42)) * height
    w_leg = np.clip((zb - co[:, 2]) / (0.08 * height), 0, 1)            # soft hip blend
    along = (co - ctr) @ long_ax
    across = (co - ctr) @ side_ax
    front = along > 0
    left = across > 0
    # each leg's hip: mean position of its vertices, raised to the belly line
    legs = []
    for f in (True, False):
        for l in (True, False):
            sel = (front == f) & (left == l) & (w_leg > 0)
            hip = co[sel].mean(0) if sel.any() else ctr.copy()
            hip[2] = zb
            phase = 0.0 if f == l else math.pi                          # trot: diagonal pairs
            legs.append(((front == f) & (left == l), hip, phase))
    swing = math.radians(float(opt.get("swing", 24)))
elif mode == "biped":
    zb = lo[2] + float(opt.get("leg", 0.32)) * height
    w_leg = np.clip((zb - co[:, 2]) / (0.10 * height), 0, 1)
    xaxis = np.array([1.0, 0, 0])
    left = co[:, 0] > ctr[0]
    hips = []
    for l in (True, False):
        sel = left == l
        hip = co[sel & (w_leg > 0)].mean(0) if (sel & (w_leg > 0)).any() else ctr.copy()
        hip[2] = zb
        hips.append((sel, hip, 0.0 if l else math.pi))
    swing = math.radians(float(opt.get("swing", 22)))
    sway = math.radians(float(opt.get("sway", 4)))
elif mode == "squash":
    amt = float(opt.get("amount", 0.12))
elif mode == "flap":
    rel = (co - ctr) @ long_ax
    half = np.abs(rel).max()
    edge = float(opt.get("body", 0.45)) * half
    w_wing = np.clip((np.abs(rel) - edge) / (0.15 * half), 0, 1)
    flap = math.radians(float(opt.get("flap", 35)))

EMIT = float(opt.get("emit", 0))
for m in mesh.data.materials:
    if not (m and m.use_nodes):
        continue
    nt = m.node_tree
    bsdf = next((n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if not bsdf:
        continue
    bsdf.inputs["Metallic"].default_value = 0.0
    if EMIT > 0:
        if bsdf.inputs["Base Color"].links:
            nt.links.new(bsdf.inputs["Base Color"].links[0].from_socket, bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = EMIT

sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"
sc.render.film_transparent = True
sc.render.resolution_x = sc.render.resolution_y = RES
sc.render.image_settings.color_mode = "RGBA"
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
sun.data.energy = float(opt.get("light", 4))
sun.rotation_euler = (math.radians(45), 0, math.radians(-30))
sc.collection.objects.link(sun)
sc.world = bpy.data.worlds.new("w")
sc.world.use_nodes = True
sc.world.node_tree.nodes["Background"].inputs[0].default_value = (0.6, 0.6, 0.65, 1)
sc.world.node_tree.nodes["Background"].inputs[1].default_value = 1.0
dim = float(max(hi - lo))
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
cam.data.type = "ORTHO"
cam.data.ortho_scale = dim * 1.3
sc.collection.objects.link(cam)
sc.camera = cam

# Facing -> camera azimuth. The model's front faces the camera at 0, which reads as walking down
# the screen (south); -90 shows its right side, so it walks east.
DIRS = {"s": 0, "se": -45, "e": -90, "ne": -135, "n": 180, "nw": 135, "w": 90, "sw": 45}
views = [(k, math.radians(v)) for k, v in DIRS.items()] if opt.get("dirs") == "8" else [("", AZ)]


def aim(az):
    d = Vector((math.sin(az), -math.cos(az), 0))
    cam.location = Vector(ctr) + (d * math.cos(TILT) + Vector((0, 0, math.sin(TILT)))) * dim * 4
    cam.rotation_euler = (math.pi / 2 - TILT, 0, az)

bob_amp = float(opt.get("bob", 0.02)) * height
for k in range(N):
    t = 2 * math.pi * k / N
    p = co.copy()
    if mode == "quad":
        for sel, hip, ph in legs:
            ang = swing * math.sin(t + ph) * w_leg[sel]
            p[sel] = rotate(p[sel], hip, side_ax, ang)
        p[:, 2] += bob_amp * abs(math.sin(t))
    elif mode == "biped":
        for sel, hip, ph in hips:
            ang = swing * math.sin(t + ph) * w_leg[sel]
            p[sel] = rotate(p[sel], hip, xaxis, ang)
        base = np.array([ctr[0], ctr[1], lo[2]])
        p = rotate(p, base, np.array([0, 1.0, 0]), np.full(nv, sway * math.sin(t)))   # side-to-side rock
        p[:, 2] += bob_amp * abs(math.cos(t))
    elif mode == "squash":
        sq = amt * math.sin(t)
        p[:, 2] = lo[2] + (p[:, 2] - lo[2]) * (1 + sq)
        p[:, :2] = ctr[:2] + (p[:, :2] - ctr[:2]) * (1 - sq * 0.5)
        p[:, 2] += 3 * bob_amp * max(0.0, math.sin(t))
    elif mode == "flap":
        for sgn in (1, -1):
            sel = (np.sign(rel) == sgn) & (w_wing > 0)
            pivot = ctr + long_ax * sgn * edge
            ang = sgn * flap * math.sin(t) * w_wing[sel]
            p[sel] = rotate(p[sel], pivot, side_ax, ang)
        p[:, 2] += bob_amp * math.sin(t + math.pi / 2)
    else:
        p[:, 2] += bob_amp * math.sin(t)
    mesh.data.vertices.foreach_set("co", p.reshape(-1))
    mesh.data.update()
    for name, az in views:
        aim(az)
        sc.render.filepath = os.path.join(out, name, f"{k:02d}.png")
        bpy.ops.render.render(write_still=True)
print("[enemy] rendered", N, "frames x", len(views), "facings", mode, "->", out)
