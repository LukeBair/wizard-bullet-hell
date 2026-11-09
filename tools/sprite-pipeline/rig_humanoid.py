"""Build a humanoid armature for a TRELLIS character and skin it by bone distance.
Usage: Blender -b -P rig_humanoid.py -- model.glb out.blend [bones.json] [mesh_name] [yaw_deg]
yaw turns the model first, for TRELLIS models that don't face -Y (the treant needs 90).
bones.json maps bone name -> [head, tail, parent, radius] in the model's space (1.0 tall, centred on the
origin, Z-up, facing -Y), with the same bone names as rig_wizard.py so render_boss_clips.py can pose it.
Without it, the wizard's table below is used."""
import json, math, sys
import bpy
import numpy as np
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:]
src, out = argv[:2]
table = json.load(open(argv[2])) if len(argv) > 2 else None
# optional "_rigid": [{"bone": "Hand.L", "boxes": [{"min": [x,y,z], "max": [x,y,z]}], "tubes": [[a, b, radius]], "grip_z": z, "offset": [x,y,z],
# "down": 62}, ...] in the bones json: vertices inside any box/tube follow one bone rigidly (a held staff). With "grip_z" and a Hand.* bone the box's
# geometry is also moved so the point at that height sits in the hand once the arm has dropped by "down" degrees
# (the staff then stands upright in the lowered hand and follows the arm).
rigid = table.pop("_rigid", []) if table else []
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
mesh = [o for o in bpy.context.scene.objects if o.type == "MESH"][0]
mesh.name = argv[3] if len(argv) > 3 else "wizard"
bpy.context.view_layer.objects.active = mesh
if len(argv) > 4:
    import math
    from mathutils import Matrix
    mesh.matrix_world = Matrix.Rotation(math.radians(float(argv[4])), 4, "Z") @ mesh.matrix_world
bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)

# name: (head, tail, parent, radius)
B = {
    "Hips":   ((0, 0, -0.02), (0, 0, 0.05), None, 0.16),
    "Spine":  ((0, 0, 0.05), (0, 0, 0.16), "Hips", 0.15),
    "Chest":  ((0, 0, 0.16), (0, 0, 0.26), "Spine", 0.16),
    "Neck":   ((0, 0, 0.26), (0, 0, 0.31), "Chest", 0.07),
    "Head":   ((0, 0, 0.31), (0, 0, 0.46), "Neck", 0.14),
}
for side, sx in (("L", 1), ("R", -1)):
    B[f"UpperArm.{side}"] = ((sx * 0.09, 0, 0.22), (sx * 0.27, 0, 0.22), "Chest", 0.12)
    B[f"Forearm.{side}"] = ((sx * 0.27, 0, 0.22), (sx * 0.40, 0, 0.22), f"UpperArm.{side}", 0.12)
    B[f"Hand.{side}"] = ((sx * 0.40, 0, 0.22), (sx * 0.47, 0, 0.22), f"Forearm.{side}", 0.08)
    B[f"Thigh.{side}"] = ((sx * 0.07, 0, -0.03), (sx * 0.07, 0, -0.27), "Hips", 0.11)
    B[f"Shin.{side}"] = ((sx * 0.07, 0, -0.27), (sx * 0.07, 0, -0.46), f"Thigh.{side}", 0.10)
    B[f"Foot.{side}"] = ((sx * 0.07, 0, -0.46), (sx * 0.07, -0.10, -0.49), f"Shin.{side}", 0.08)

if table:
    B = {n: (tuple(v[0]), tuple(v[1]), v[2], v[3]) for n, v in table.items()}

arm_data = bpy.data.armatures.new("rig")
arm = bpy.data.objects.new("rig", arm_data)
bpy.context.scene.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="EDIT")
for n, (h, t, p, r) in B.items():
    b = arm_data.edit_bones.new(n)
    b.head, b.tail = Vector(h), Vector(t)
    if p:
        b.parent = arm_data.edit_bones[p]
        b.use_connect = Vector(h) == Vector(B[p][1])
bpy.ops.object.mode_set(mode="OBJECT")

# Distance-based skin weights
verts = np.empty(len(mesh.data.vertices) * 3, dtype=np.float64)
mesh.data.vertices.foreach_get("co", verts)
verts = verts.reshape(-1, 3)
names = list(B)
D = np.empty((len(verts), len(names)))
for i, n in enumerate(names):
    h, t, _, r = B[n]
    h, t = np.array(h), np.array(t)
    d = t - h
    u = np.clip(((verts - h) @ d) / (d @ d), 0, 1)
    D[:, i] = np.linalg.norm(verts - (h + u[:, None] * d), axis=1) / r
W = 1.0 / (D + 0.05) ** 4
keep = np.argsort(-W, axis=1)[:, :4]
mask = np.zeros_like(W, dtype=bool)
np.put_along_axis(mask, keep, True, axis=1)
W = np.where(mask, W, 0)
W /= W.sum(axis=1, keepdims=True)

for box in rigid:
    inside = np.zeros(len(verts), dtype=bool)
    for bx in box.get("boxes", []):
        inside |= np.all((verts >= np.array(bx["min"])) & (verts <= np.array(bx["max"])), axis=1)
    for a, b, r in box.get("tubes", []):     # [point a, point b, radius]: a thin straight shaft
        a, b = np.array(a, float), np.array(b, float)
        u = np.clip(((verts - a) @ (b - a)) / ((b - a) @ (b - a)), 0, 1)
        inside |= np.linalg.norm(verts - (a + u[:, None] * (b - a)), axis=1) < r
    # cut the faces that join the shaft to the body, or moving it drags long strips across the robe
    import bmesh
    bm = bmesh.new(); bm.from_mesh(mesh.data); bm.faces.ensure_lookup_table()
    cut = [f for f in bm.faces if 0 < sum(inside[v.index] for v in f.verts) < len(f.verts)]
    bmesh.ops.delete(bm, geom=cut, context="FACES_ONLY")
    bm.to_mesh(mesh.data); bm.free()
    print("[rig] cut", len(cut), "faces")
    if "grip_z" in box:
        side = box["bone"].split(".")[1]
        S = np.array(B[f"UpperArm.{side}"][0], float)
        hand = (np.array(B[box["bone"]][0], float) + np.array(B[box["bone"]][1], float)) / 2
        sgn = 1 if side == "L" else -1
        from mathutils import Matrix as M3
        Rn = np.array(M3.Rotation(math.radians(sgn * box["down"]), 3, "Y"))
        Rp = np.array(M3.Rotation(math.radians(-sgn * box["down"]), 3, "Y"))
        hold = S + Rn @ (hand - S) + np.array(box.get("offset", [0, 0, 0]))
        P = verts[inside]
        near = P[np.abs(P[:, 2] - box["grip_z"]) < 0.02]
        grip = np.array([np.median(near[:, 0]), np.median(near[:, 1]), box["grip_z"]])
        P = (P - grip + hold - S) @ Rp.T + S
        verts[inside] = P
        mesh.data.vertices.foreach_set("co", verts.reshape(-1))
        mesh.data.update()
    W[inside] = 0
    W[inside, names.index(box["bone"])] = 1.0
    print("[rig] rigid", box["bone"], int(inside.sum()), "verts")

groups = {n: mesh.vertex_groups.new(name=n) for n in names}
for vi in range(len(verts)):
    for j in np.nonzero(W[vi])[0]:
        if W[vi, j] > 0.01:
            groups[names[j]].add([vi], float(W[vi, j]), "REPLACE")

mod = mesh.modifiers.new("Armature", "ARMATURE")
mod.object = arm
mesh.parent = arm
bpy.ops.wm.save_as_mainfile(filepath=out)
print("[rig] saved", out, "bones:", len(B))
