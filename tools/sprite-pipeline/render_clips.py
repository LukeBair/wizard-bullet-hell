"""Pose the rigged wizard for every frame of the game's clips and render them.
Usage: Blender -b rig/wizard_rigged.blend -P render_clips.py -- out_dir [only_clip]
Writes out_dir/<index>_<clip>.png (768px, transparent), index = frame number in the sheet."""
import math, os, sys
import bpy
import numpy as np
from mathutils import Matrix, Vector

args = sys.argv[sys.argv.index("--") + 1:]
out_dir = args[0]
only = args[1] if len(args) > 1 else None
os.makedirs(out_dir, exist_ok=True)

arm = bpy.data.objects["rig"]
mesh = bpy.data.objects["wizard"]
RES, TILT, ORTHO = 768, math.radians(25), 1.2

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
sun.data.energy = 3.0
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
FACING = {"": 0, "_side": -90, "_up": 180, "_diag": -45, "_updiag": -135}


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
    """Apply the pose for frame i of n of a base clip (idle/walk/cast/hit/death)."""
    reset()
    down = 65.0
    sway_x = sway_y = flare = 0.0
    arms = {"L": 0.0, "R": 0.0}  # extra forward(+)/back swing in degrees about X
    if clip == "idle":
        u = 2 * math.pi * i / max(n, 1)
        rot("Chest", "X", 1.2 * math.sin(u))
        rot("Head", "X", 1.0 * math.sin(u + 0.8))
        sway_x, sway_y = 0.3 * math.sin(u), 0.15 * math.sin(u + 1.0)
        arms["L"] = arms["R"] = 2.0 * math.sin(u + 0.5)
    elif clip == "walk":
        p = 2 * math.pi * i / n
        rot("Thigh.L", "X", 22 * math.sin(p))
        rot("Thigh.R", "X", -22 * math.sin(p))
        arms["L"], arms["R"] = -12 * math.sin(p), 12 * math.sin(p)
        arm.location.z = -0.012 * (1 - abs(math.cos(p)))
        rot("Chest", "X", 2.0)
        sway_y = -0.55 * math.sin(p - 0.8)
        sway_x = 0.25 * math.sin(2 * p)
        flare = 0.25 + 0.15 * math.cos(2 * p)
    elif clip == "cast":
        t = i / max(n - 1, 1)
        ang = lerp_keys(t, [0, 0.2, 0.45, 0.7, 1.0], [0, -15, 85, 70, 0])  # forward-raise of right arm
        rot("Chest", "X", lerp_keys(t, [0, 0.45, 1.0], [0, 7, 0]))
        rot("Head", "X", lerp_keys(t, [0, 0.45, 1.0], [0, -4, 0]))
        arms["R"] = -ang
        sway_y = lerp_keys(t, [0, 0.2, 0.5, 1.0], [0, 0.4, -0.5, 0])
        flare = 0.2
    elif clip == "hit":
        t = i / max(n - 1, 1)
        lean = lerp_keys(t, [0, 0.5, 1.0], [5, 14, 6])
        rot("Chest", "X", -lean)
        rot("Head", "X", -lean * 0.7)
        down = lerp_keys(t, [0, 0.5, 1.0], [65, 52, 62])
        sway_y = lerp_keys(t, [0, 0.5, 1.0], [0.3, 0.9, 0.2])
    elif clip == "death":
        t = i / max(n - 1, 1)
        e = t * t * (3 - 2 * t)
        theta = 88.0 * e
        down = 65 - 25 * e
        arm.rotation_euler = (-math.radians(theta), 0, 0)
        P = Vector((0, 0, -0.5))
        arm.location = P - Matrix.Rotation(-math.radians(theta), 3, "X") @ P
        sway_y = -0.4 * e
        flare = 0.3 * e
    # arms down + swing (apply after body so they follow the chest)
    rot("UpperArm.L", "Y", down)
    rot("UpperArm.R", "Y", -down)
    for side in ("L", "R"):
        if arms[side]:
            rot(f"UpperArm.{side}", "X", arms[side])
    if clip == "cast":
        rot("Forearm.R", "X", arms["R"] * 0.5)
    keys["sway_x"].value, keys["sway_y"].value, keys["flare"].value = sway_x, sway_y, flare
    update()


def shoot(index, name, facing):
    t = math.radians(FACING[facing])
    d = Vector((math.sin(t), -math.cos(t), 0))
    cam.location = (d * math.cos(TILT) + Vector((0, 0, math.sin(TILT)))) * 5
    cam.rotation_euler = (math.pi / 2 - TILT, 0, t)
    sc.render.filepath = os.path.join(out_dir, f"{index:02d}_{name}{facing}.png")
    bpy.ops.render.render(write_still=True)


# (clip, facing, frames) in the game's sheet order
LAYOUT = [
    ("idle", "", 4), ("idle", "_side", 1), ("idle", "_up", 1),
    ("walk", "", 6), ("walk", "_side", 6), ("walk", "_up", 6),
    ("cast", "", 7), ("cast", "_side", 7), ("cast", "_up", 7),
    ("hit", "", 3), ("death", "", 7),
    # diagonals appended after the original 55 frames so their indices don't move
    ("idle", "_diag", 1), ("idle", "_updiag", 1),
    ("walk", "_diag", 6), ("walk", "_updiag", 6),
    ("cast", "_diag", 7), ("cast", "_updiag", 7),
]
idx = 0
bpy.ops.object.mode_set(mode="POSE") if False else None
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="POSE")
for clip, facing, n in LAYOUT:
    for i in range(n):
        if only is None or only == clip + facing:
            pose(clip, i, 4 if clip == "idle" and facing == "" else n if clip != "idle" else 4)
            shoot(idx, clip, facing)
        idx += 1
print("[clips] done", idx, "frames")
