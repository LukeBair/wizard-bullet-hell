"""Render a sprite's normal map alongside its colour frame (imported by the Blender render scripts).
The normal pass swaps every material for one that emits the camera-space normal as colour,
(x right, y up, z toward the camera) * 0.5 + 0.5 (Blender camera space flipped in z), the layout Unity's 2D lights read from _NormalMap,
with colour management off so the values land in the PNG unchanged.

    import normal_pass
    np_ = normal_pass.NormalPass(scene, [mesh])
    ... render colour ...
    np_.render(path)          # same camera and pose, writes the normal map
"""
import bpy


def _material():
    m = bpy.data.materials.get("NormalPass")
    if m:
        return m
    m = bpy.data.materials.new("NormalPass")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    geo = nt.nodes.new("ShaderNodeNewGeometry")
    xf = nt.nodes.new("ShaderNodeVectorTransform")
    xf.vector_type, xf.convert_from, xf.convert_to = "NORMAL", "WORLD", "CAMERA"
    scale = nt.nodes.new("ShaderNodeVectorMath")
    scale.operation = "MULTIPLY_ADD"
    scale.inputs[1].default_value = (0.5, 0.5, -0.5)    # Blender camera space has z pointing away from the viewer
    scale.inputs[2].default_value = (0.5, 0.5, 0.5)
    emit = nt.nodes.new("ShaderNodeEmission")
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    nt.links.new(geo.outputs["Normal"], xf.inputs["Vector"])
    nt.links.new(xf.outputs["Vector"], scale.inputs[0])
    nt.links.new(scale.outputs["Vector"], emit.inputs["Color"])
    nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
    return m


class NormalPass:
    def __init__(self, scene, meshes):
        self.scene = scene
        self.meshes = meshes
        self.mat = _material()

    def render(self, path):
        sc = self.scene
        saved = [[s.material for s in o.material_slots] for o in self.meshes]
        view, look = sc.view_settings.view_transform, sc.view_settings.look
        world = sc.world
        for o in self.meshes:
            for s in o.material_slots:
                s.material = self.mat
        sc.view_settings.view_transform = "Raw"
        sc.world = None                       # no ambient light leaking into the emission
        old = sc.render.filepath
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        sc.render.filepath = old
        sc.world = world
        sc.view_settings.view_transform, sc.view_settings.look = view, look
        for o, mats in zip(self.meshes, saved):
            for s, m in zip(o.material_slots, mats):
                s.material = m
