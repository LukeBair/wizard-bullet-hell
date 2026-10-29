"""Render the wizard from the game's three facings with a tilted ortho camera.
Usage: Blender -b -P render_sprites.py -- model.glb out_dir [tilt_deg] [size]
Facings: front (down), side (right; game mirrors for left), back (up)."""
import math, os, sys
import bpy
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
src, out = a[0], a[1]
tilt = math.radians(float(a[2]) if len(a) > 2 else 25)
size = int(a[3]) if len(a) > 3 else 768
os.makedirs(out, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
ctr = (lo + hi) / 2
dim = max(hi - lo)

sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"
sc.render.film_transparent = True
sc.render.resolution_x = sc.render.resolution_y = size
sc.render.image_settings.color_mode = "RGBA"
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
sun.data.energy = 3.0
sun.rotation_euler = (math.radians(45), 0, math.radians(-30))
sc.collection.objects.link(sun)
sc.world = bpy.data.worlds.new("w")
sc.world.use_nodes = True
sc.world.node_tree.nodes["Background"].inputs[0].default_value = (0.6, 0.6, 0.65, 1)
sc.world.node_tree.nodes["Background"].inputs[1].default_value = 0.6

cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
cam.data.type = "ORTHO"
cam.data.ortho_scale = dim * 1.25
sc.collection.objects.link(cam)
sc.camera = cam
for name, ang in (("front", 0), ("side", -90), ("back", 180)):
    t = math.radians(ang)
    d = Vector((math.sin(t), -math.cos(t), 0))
    cam.location = ctr + (d * math.cos(tilt) + Vector((0, 0, math.sin(tilt)))) * dim * 4
    cam.rotation_euler = (math.pi / 2 - tilt, 0, t)
    sc.render.filepath = os.path.join(out, f"{name}_{size}.png")
    bpy.ops.render.render(write_still=True)
print("[sprites] done")
