"""Render a static prop (tree, rock, shrine...) from the game's 25-degree camera as a few variants, each
the model turned to a different angle, with a matching normal map per variant.
Usage: Blender -b -P render_prop.py -- model.glb out_dir name [variants=3] [res=256] [emit=0.6] [yaw=0]
Writes out_dir/<name>_<k>.png and <name>_<k>_n.png, trimmed to the prop with its base at the bottom."""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import normal_pass
from mathutils import Matrix, Vector

a = sys.argv[sys.argv.index("--") + 1:]
src, out, name = a[0], a[1], a[2]
opt = dict(kv.split("=") for kv in a[3:])
N, RES, EMIT, YAW = int(opt.get("variants", 3)), int(opt.get("res", 256)), float(opt.get("emit", 0.6)), float(opt.get("yaw", 0))
os.makedirs(out, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
mesh = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
mesh.parent = None
base = mesh.matrix_world.copy()
pts = [base @ Vector(c) for c in mesh.bound_box]
lo = Vector([min(p[i] for p in pts) for i in range(3)])
hi = Vector([max(p[i] for p in pts) for i in range(3)])
ctr = (lo + hi) / 2
dim = max(hi - lo)
for m in mesh.data.materials:
    bsdf = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None) if m and m.use_nodes else None
    if bsdf:
        bsdf.inputs["Metallic"].default_value = 0.0
        if EMIT > 0 and bsdf.inputs["Base Color"].links:
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
sc.world = bpy.data.worlds.new("w")
sc.world.use_nodes = True
sc.world.node_tree.nodes["Background"].inputs[0].default_value = (0.6, 0.6, 0.65, 1)
sc.world.node_tree.nodes["Background"].inputs[1].default_value = 0.8
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
cam.data.type = "ORTHO"
cam.data.ortho_scale = dim * 1.15
sc.collection.objects.link(cam)
sc.camera = cam
tilt = math.radians(25)
cam.location = Vector((0, -math.cos(tilt), math.sin(tilt))) * dim * 4
cam.rotation_euler = (math.pi / 2 - tilt, 0, 0)
normals = normal_pass.NormalPass(sc, [mesh])

for k in range(N):
    turn = math.radians(YAW + k * 360.0 / max(N, 1) * 0.35)   # modest turns so every variant shows its good side
    mesh.matrix_world = Matrix.Rotation(turn, 4, "Z") @ Matrix.Translation(-ctr) @ base
    sc.render.filepath = os.path.join(out, f"{name}_{k}.png")
    bpy.ops.render.render(write_still=True)
    normals.render(os.path.join(out, f"{name}_{k}_n.png"))
print("[prop] done", name, N)
