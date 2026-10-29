"""Render a static prop (tree, rock, shrine...) from the game's 25-degree camera as a few variants, each
the model turned to a different angle, with a matching normal map per variant.
Usage: Blender -b -P render_prop.py -- model.glb out_dir name [variants=6] [spread=360] [res=256] [emit=0.6] [yaw=0]
The variants are evenly spaced turns across `spread` degrees.
Writes out_dir/<name>_<k>.png and <name>_<k>_n.png, and <name>_<k>_shadow.png: the shadow the model
casts on the ground from the game's default sun (lighting.sunAngle 160, 40 degrees up), rendered
in Cycles with the prop itself hidden from the camera, from the same camera so it lines up exactly.
sun=<deg> and sunElev=<deg> change the sun."""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import normal_pass
from mathutils import Matrix, Vector

a = sys.argv[sys.argv.index("--") + 1:]
src, out, name = a[0], a[1], a[2]
opt = dict(kv.split("=") for kv in a[3:])
SPREAD = float(opt.get("spread", 360))
N, RES, EMIT, YAW = int(opt.get("variants", 6)), int(opt.get("res", 256)), float(opt.get("emit", 0.6)), float(opt.get("yaw", 0))
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
cam.data.ortho_scale = dim * 1.5   # room for the diagonal as it turns
sc.collection.objects.link(cam)
sc.camera = cam
tilt = math.radians(25)
cam.location = Vector((0, -math.cos(tilt), math.sin(tilt))) * dim * 4
cam.rotation_euler = (math.pi / 2 - tilt, 0, 0)
normals = normal_pass.NormalPass(sc, [mesh])

# --- shadow pass: a white ground plane under the prop, lit only by the game's sun ---
SUN = math.radians(float(opt.get("sun", 160)))
ELEV = math.radians(float(opt.get("sunElev", 40)))
bpy.ops.mesh.primitive_plane_add(size=dim * 60)   # the tilted camera sees a long way back across the ground
ground = bpy.context.active_object
ground_mat = bpy.data.materials.new("ground")
ground_mat.use_nodes = True
ground_mat.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (1, 1, 1, 1)
ground_mat.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1.0
ground.data.materials.append(ground_mat)
ground.hide_render = True
shadow_sun = bpy.data.objects.new("shadow_sun", bpy.data.lights.new("shadow_sun", "SUN"))
shadow_sun.data.energy = 3.0
shadow_sun.data.angle = math.radians(1.5)   # slightly soft edge
sc.collection.objects.link(shadow_sun)
toward = Vector((math.cos(SUN) * math.cos(ELEV), math.sin(SUN) * math.cos(ELEV), math.sin(ELEV)))   # from the ground to the sun
shadow_sun.rotation_euler = (-toward).to_track_quat("-Z", "Y").to_euler()
shadow_sun.hide_render = True


# Colour pass lighting: a key light from the same direction as the game's sun, with shadows, over a
# dim fill, so branches, roots and rock faces shade each other and the structure reads. The old soft
# front light stays as a weak fill.
key = bpy.data.objects.new("key", bpy.data.lights.new("key", "SUN"))
key.data.energy = float(opt.get("key", 5))
key.data.angle = math.radians(3)
key.rotation_euler = shadow_sun.rotation_euler.copy()
sc.collection.objects.link(key)
sun.data.energy = float(opt.get("fill", 0.8))
sc.world.node_tree.nodes["Background"].inputs[1].default_value = float(opt.get("ambient", 0.5))


def render_shadow(path):
    """Ground plane + sun only; the prop casts its shadow but isn't seen. Writes a greyscale-in-alpha PNG."""
    engine, transparent, world = sc.render.engine, sc.render.film_transparent, sc.world
    sc.render.engine = "CYCLES"
    sc.cycles.samples = 24
    sc.render.film_transparent = True   # anything past the plane comes out transparent, not "in shadow"
    sc.world = None
    ground.hide_render = shadow_sun.hide_render = False
    sun.hide_render = True
    key.hide_render = True
    ground.location = (0, 0, lo.z - ctr.z + 0.001 * dim)
    mesh.visible_camera = False
    # twice the frame at twice the resolution: same pixels per unit and centre, room for the shadow
    cam.data.ortho_scale *= 2
    sc.render.resolution_x = sc.render.resolution_y = RES * 2
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    mesh.visible_camera = True
    cam.data.ortho_scale /= 2
    sc.render.resolution_x = sc.render.resolution_y = RES
    ground.hide_render = shadow_sun.hide_render = True
    sun.hide_render = False
    key.hide_render = False
    sc.render.engine, sc.render.film_transparent, sc.world = engine, transparent, world

for k in range(N):
    turn = math.radians(YAW + k * SPREAD / max(N, 1))
    mesh.matrix_world = Matrix.Rotation(turn, 4, "Z") @ Matrix.Translation(-ctr) @ base
    sc.render.filepath = os.path.join(out, f"{name}_{k}.png")
    bpy.ops.render.render(write_still=True)
    normals.render(os.path.join(out, f"{name}_{k}_n.png"))
    render_shadow(os.path.join(out, f"{name}_{k}_shadow.png"))
print("[prop] done", name, N)
