"""Open the rigged blend, pose the arms down, render front/side. Usage: Blender -b rigged.blend -P pose_test.py -- out_prefix [arm_deg]"""
import math, sys
import bpy
from mathutils import Matrix, Vector

prefix = sys.argv[sys.argv.index("--") + 1]
deg = float(sys.argv[sys.argv.index("--") + 2]) if len(sys.argv) > sys.argv.index("--") + 2 else 65
arm = bpy.data.objects["rig"]
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="POSE")


def rot(name, axis, angle):
    pb = arm.pose.bones[name]
    bpy.context.view_layer.update()
    h = arm.matrix_world @ pb.head
    M = Matrix.Translation(h) @ Matrix.Rotation(angle, 4, axis) @ Matrix.Translation(-h)
    pb.matrix = M @ pb.matrix
    bpy.context.view_layer.update()


rot("UpperArm.L", "Y", math.radians(deg))
rot("UpperArm.R", "Y", -math.radians(deg))
bpy.ops.object.mode_set(mode="OBJECT")

sc = bpy.context.scene
sc.render.engine = "BLENDER_EEVEE"
sc.render.resolution_x = sc.render.resolution_y = 900
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
sun.data.energy = 3.0
sun.rotation_euler = (math.radians(50), 0, math.radians(30))
sc.collection.objects.link(sun)
sc.world = bpy.data.worlds.new("w")
sc.world.use_nodes = True
sc.world.node_tree.nodes["Background"].inputs[0].default_value = (0.3, 0.3, 0.33, 1)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
cam.data.type = "ORTHO"
cam.data.ortho_scale = 1.3
sc.collection.objects.link(cam)
sc.camera = cam
for name, ang in (("front", 0), ("side", -90)):
    t = math.radians(ang)
    cam.location = Vector((math.sin(t), -math.cos(t), 0)) * 4
    cam.rotation_euler = (math.pi / 2, 0, t)
    sc.render.filepath = f"{prefix}_{name}.png"
    bpy.ops.render.render(write_still=True)
