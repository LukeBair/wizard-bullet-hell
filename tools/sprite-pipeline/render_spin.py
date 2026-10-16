"""Turntable loop for small floating props (the Headmaster's orbiting tome): the model is tipped toward
the camera, spins a full turn about the vertical and bobs, rendered from the game's 25-degree camera.
Usage: Blender -b -P render_spin.py -- model.glb out_dir [frames=16] [tip=55] [res=128] [emit=0.8]"""
import math, os, sys
import bpy
from mathutils import Matrix, Vector

a = sys.argv[sys.argv.index("--") + 1:]
src, out = a[0], a[1]
opt = dict(kv.split("=") for kv in a[2:])
N, TIP, RES, EMIT = int(opt.get("frames", 16)), float(opt.get("tip", 55)), int(opt.get("res", 128)), float(opt.get("emit", 0.8))
os.makedirs(out, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
mesh = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
mesh.parent = None
pts = [mesh.matrix_world @ Vector(c) for c in mesh.bound_box]
ctr = sum(pts, Vector()) / 8
dim = max((max(p[i] for p in pts) - min(p[i] for p in pts)) for i in range(3))
base = Matrix.Translation(-ctr) @ mesh.matrix_world
for m in mesh.data.materials:
    bsdf = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None) if m and m.use_nodes else None
    if bsdf and EMIT > 0 and bsdf.inputs["Base Color"].links:
        m.node_tree.links.new(bsdf.inputs["Base Color"].links[0].from_socket, bsdf.inputs["Emission Color"])
        bsdf.inputs["Emission Strength"].default_value = EMIT
sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"
sc.render.film_transparent = True
sc.render.resolution_x = sc.render.resolution_y = RES
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
sun.data.energy = 4.0
sun.rotation_euler = (math.radians(45), 0, math.radians(-30))
sc.collection.objects.link(sun)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
cam.data.type = "ORTHO"
cam.data.ortho_scale = dim * 1.35
sc.collection.objects.link(cam)
sc.camera = cam
tilt = math.radians(25)
cam.location = Vector((0, -math.cos(tilt), math.sin(tilt))) * dim * 4
cam.rotation_euler = (math.pi / 2 - tilt, 0, 0)
for k in range(N):
    u = 2 * math.pi * k / N
    mesh.matrix_world = (Matrix.Translation((0, 0, 0.04 * dim * math.sin(2 * u)))
                         @ Matrix.Rotation(u, 4, "Z")
                         @ Matrix.Rotation(math.radians(TIP) + 0.15 * math.sin(u), 4, "X") @ base)
    sc.render.filepath = os.path.join(out, f"{k:02d}.png")
    bpy.ops.render.render(write_still=True)
print("[spin] done", N)
