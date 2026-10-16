"""Render front/side previews of a GLB. Usage: Blender -b -P render_preview.py -- model.glb out_prefix"""
import math, sys
import bpy
from mathutils import Vector

src, prefix = sys.argv[sys.argv.index("--") + 1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
ctr, size = (lo + hi) / 2, max((hi - lo))
sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
sun.data.energy = 3.0
sun.rotation_euler = (math.radians(50), 0, math.radians(30))
sc.collection.objects.link(sun)
sc.render.resolution_x = sc.render.resolution_y = 900
sc.world = bpy.data.worlds.new("w"); sc.world.color = (0.6, 0.6, 0.65)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
cam.data.type = "ORTHO"; cam.data.ortho_scale = size * 1.15
sc.collection.objects.link(cam); sc.camera = cam
for name, ang in (("front", 0), ("left45", -45), ("right45", 45), ("side", 90), ("back", 180)):
    a = math.radians(ang)
    cam.location = ctr + Vector((math.sin(a), -math.cos(a), 0)) * size * 3
    cam.rotation_euler = (math.radians(90), 0, a)
    sc.render.filepath = f"{prefix}_{name}.png"
    bpy.ops.render.render(write_still=True)
cam.location = ctr + Vector((0, 0, size * 3)); cam.rotation_euler = (0, 0, 0)
sc.render.filepath = f"{prefix}_top.png"
bpy.ops.render.render(write_still=True)
print("dims", hi - lo)
