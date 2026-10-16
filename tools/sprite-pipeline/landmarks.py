import bpy, sys
import numpy as np
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=sys.argv[sys.argv.index("--")+1])
ms=[o for o in bpy.context.scene.objects if o.type=="MESH"]
print("meshes", [(o.name, len(o.data.vertices), len(o.data.polygons)) for o in ms])
o=ms[0]
v=np.array([o.matrix_world@x.co for x in o.data.vertices])
print("bbox", v.min(0), v.max(0))
zmin,zmax=v[:,2].min(),v[:,2].max(); H=zmax-zmin
for f in np.linspace(0,1,21):
    z=zmin+f*H; s=v[(abs(v[:,2]-z)<H*0.01)]
    if len(s): print(f"z={f:.2f} n={len(s):6d} x[{s[:,0].min():.2f},{s[:,0].max():.2f}] y[{s[:,1].min():.2f},{s[:,1].max():.2f}]")
