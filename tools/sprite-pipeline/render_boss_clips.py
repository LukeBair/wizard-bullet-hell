"""Pose a rigged boss (rig_humanoid.py) through idle, walk, two-handed cast and death in all eight facings.
Usage: Blender -b rig/<name>.blend -P render_boss_clips.py -- out_dir mesh_name [res] [emit]
Writes out_dir/<clip>/<facing>/NN.png, which make_enemy_sheet.py packs into <clip>_<facing> clips."""
import math, os, sys
import bpy
import numpy as np
from mathutils import Matrix, Vector

args = sys.argv[sys.argv.index("--") + 1:]
out_dir, mesh_name = args[0], args[1]
RES_ARG = int(args[2]) if len(args) > 2 else 256
EMIT = float(args[3]) if len(args) > 3 else 0.8
os.makedirs(out_dir, exist_ok=True)

arm = bpy.data.objects["rig"]
mesh = bpy.data.objects[mesh_name]
RES, TILT, ORTHO = RES_ARG, math.radians(25), 1.25

# --- hem sway shape keys (rest-space offsets that the armature then carries) ---
co = np.empty(len(mesh.data.vertices) * 3)
mesh.data.vertices.foreach_get("co", co)
co = co.reshape(-1, 3)
s = np.clip((-0.04 - co[:, 2]) / 0.40, 0, 1) ** 1.6
mesh.shape_key_add(name="Basis")
for name, off in (
    ("sway_x", np.stack([s * 0.06, 0 * s, 0 * s], 1)),
    ("sway_y", np.stack([0 * s, s * 0.06, 0 * s], 1)),
    ("flare", np.stack([co[:, 0] * s * 0.35, co[:, 1] * s * 0.35, 0 * s], 1)),
):
    k = mesh.shape_key_add(name=name)
    k.slider_min, k.slider_max = -1.0, 1.0
    k.data.foreach_set("co", (co + off).reshape(-1))
keys = mesh.data.shape_keys.key_blocks

# --- scene ---
sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"
sc.render.film_transparent = True
sc.render.resolution_x = sc.render.resolution_y = RES
sc.render.image_settings.color_mode = "RGBA"
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
sun.data.energy = 5.0
sun.rotation_euler = (math.radians(45), 0, math.radians(-30))
sc.collection.objects.link(sun)
sc.world = bpy.data.worlds.new("w")
sc.world.use_nodes = True
bg = sc.world.node_tree.nodes["Background"]
bg.inputs[0].default_value = (0.6, 0.6, 0.65, 1)
bg.inputs[1].default_value = 0.8
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
cam.data.type = "ORTHO"
cam.data.ortho_scale = ORTHO
sc.collection.objects.link(cam)
sc.camera = cam
FACING = {"s": 0, "se": -45, "e": -90, "ne": -135, "n": 180, "nw": 135, "w": 90, "sw": 45}

# TRELLIS textures come out dark: feed the base colour into emission so the boss reads at sprite size.
for m in mesh.data.materials:
    if not (m and m.use_nodes):
        continue
    bsdf = next((n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if bsdf:
        bsdf.inputs["Metallic"].default_value = 0.0
        if EMIT > 0 and bsdf.inputs["Base Color"].links:
            m.node_tree.links.new(bsdf.inputs["Base Color"].links[0].from_socket, bsdf.inputs["Emission Color"])
            bsdf.inputs["Emission Strength"].default_value = EMIT


def update():
    bpy.context.view_layer.update()


def rot(name, axis, deg):
    pb = arm.pose.bones[name]
    update()
    h = arm.matrix_world @ pb.head
    M = Matrix.Translation(h) @ Matrix.Rotation(math.radians(deg), 4, axis) @ Matrix.Translation(-h)
    pb.matrix = M @ pb.matrix
    update()


def reset():
    arm.location = (0, 0, 0)
    arm.rotation_euler = (0, 0, 0)
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix()
    for k in ("sway_x", "sway_y", "flare"):
        keys[k].value = 0.0
    update()


def lerp_keys(t, ts, vs):
    return float(np.interp(t, ts, vs))


def pose(clip, i, n):
    """Pose frame i of n. Arms start out in TRELLIS's T-pose; `down` lowers them to the sides."""
    reset()
    down = 62.0
    sway_x = sway_y = flare = 0.0
    fwd = {"L": 0.0, "R": 0.0}     # arm swing about X: negative = forward
    spread = 0.0                   # extra outward lift of both arms
    elbow = {"L": 0.0, "R": 0.0}   # forearm bend forward
    wrist = {"L": 0.0, "R": 0.0}   # hand bend: negative = palm forward / fingers up
    if clip == "idle":
        u = 2 * math.pi * i / n
        rot("Chest", "X", 1.5 * math.sin(u))
        rot("Head", "X", 1.2 * math.sin(u + 0.8))
        sway_x, sway_y = 0.3 * math.sin(u), 0.2 * math.sin(u + 1.0)
        fwd["L"] = fwd["R"] = -4 + 3 * math.sin(u + 0.5)
        elbow["L"] = elbow["R"] = 20
        wrist["L"] = wrist["R"] = -10 + 8 * math.sin(u + 1.2)   # fingers drumming
    elif clip == "walk":
        # he glides: no steps under the robe, just a sway, a bob and the hands trailing
        p = 2 * math.pi * i / n
        arm.location.z = 0.01 * math.sin(2 * p)
        rot("Chest", "X", 4.0)
        rot("Head", "X", -3.0)
        rot("Hips", "Z", 4 * math.sin(p))
        fwd["L"], fwd["R"] = -10 + 10 * math.sin(p), -10 - 10 * math.sin(p)
        elbow["L"] = elbow["R"] = 25
        wrist["L"], wrist["R"] = 15 * math.sin(p + 1.0), -15 * math.sin(p + 1.0)
        sway_y = 0.8                                   # hem trails behind
        sway_x = 0.4 * math.sin(p)
        flare = 0.2 + 0.1 * math.cos(2 * p)
    elif clip == "cast":
        # both hands gather in front, rise, then fling open with palms out
        t = i / max(n - 1, 1)
        ks = [0, 0.25, 0.5, 0.7, 1.0]
        a = lerp_keys(t, ks, [-5, -40, -65, -30, -5])
        fwd["L"] = fwd["R"] = a
        spread = lerp_keys(t, ks, [0, -10, 35, 80, 0])
        elbow["L"] = elbow["R"] = lerp_keys(t, ks, [20, 70, 30, 5, 20])
        wrist["L"] = wrist["R"] = lerp_keys(t, ks, [0, 20, -55, -70, 0])
        rot("Chest", "X", lerp_keys(t, ks, [0, 6, -8, 4, 0]))
        rot("Head", "X", lerp_keys(t, ks, [0, 5, -10, 0, 0]))
        sway_y = lerp_keys(t, ks, [0, 0.3, -0.4, 0.6, 0])
        flare = lerp_keys(t, ks, [0.1, 0.2, 0.5, 0.7, 0.1])
    elif clip == "death":
        # recoils with arms thrown up, then crumples to his knees and slumps forward
        t = i / max(n - 1, 1)
        rec = lerp_keys(t, [0, 0.2, 0.4, 1.0], [0, 1, 0.5, 0])
        down = 62 - 85 * rec + 20 * max(0.0, t - 0.4)
        wrist["L"] = wrist["R"] = -50 * rec + 30 * max(0.0, t - 0.4)
        e = float(np.clip((t - 0.3) / 0.7, 0, 1))
        e = e * e * (3 - 2 * e)
        rot("Chest", "X", -18 * rec + 35 * e)
        rot("Head", "X", -20 * rec + 25 * e)
        fwd["L"] = fwd["R"] = -30 * e
        arm.location.z = -0.28 * e                      # robe folds as he drops
        sway_y = 0.5 * rec - 0.3 * e
        flare = 0.9 * e
    rot("UpperArm.L", "Y", down - spread)
    rot("UpperArm.R", "Y", -(down - spread))
    for s in ("L", "R"):
        if fwd[s]:
            rot(f"UpperArm.{s}", "X", fwd[s])
        if elbow[s]:
            rot(f"Forearm.{s}", "X", -elbow[s])
        if wrist[s]:
            rot(f"Hand.{s}", "X", wrist[s])
    keys["sway_x"].value, keys["sway_y"].value, keys["flare"].value = sway_x, sway_y, flare
    update()


def shoot(clip, facing, i):
    t = math.radians(FACING[facing])
    d = Vector((math.sin(t), -math.cos(t), 0))
    cam.location = (d * math.cos(TILT) + Vector((0, 0, math.sin(TILT)))) * 5
    cam.rotation_euler = (math.pi / 2 - TILT, 0, t)
    sc.render.filepath = os.path.join(out_dir, clip, facing, f"{i:02d}.png")
    bpy.ops.render.render(write_still=True)


CLIPS = [("idle", 4), ("walk", 8), ("cast", 8), ("death", 10)]
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="POSE")
for clip, n in CLIPS:
    for i in range(n):
        pose(clip, i, n)
        for facing in FACING:
            shoot(clip, facing, i)
print("[boss] done")
