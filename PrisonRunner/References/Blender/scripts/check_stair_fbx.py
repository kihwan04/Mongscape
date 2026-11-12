"""Re-import the exported stair FBXs and print Unity-space bounds.

Run: blender --background --factory-startup --python check_stair_fbx.py
Import uses the same axes as the export (forward 'Z', up 'Y'), so Blender coords come back as authored
(X=ux, Y=uz, Z=uy); Unity-space bounds are printed as (x, y=Zb, z=Yb).
The importer honours the axis metadata stored in the FBX, so the file itself says: Y up, chunk along +Z.
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector
import stair_common as sc

fbx_dir = sc.repo_paths()["fbx_dir"]
for name in ("MineStair", "ZooStair"):
    for fw, up, label in (("Z", "Y", "round-trip"),):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=os.path.join(fbx_dir, name + ".fbx"), axis_forward=fw, axis_up=up)
        objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
        pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
        lo = [min(p[i] for p in pts) for i in range(3)]
        hi = [max(p[i] for p in pts) for i in range(3)]
        tris = sum(len(o.data.polygons) for o in objs)
        mats = {m.name for o in objs for m in o.data.materials if m}
        print("%s [%s]: meshes=%d tris=%d mats=%d origin=%s" % (name, label, len(objs), tris, len(mats), [tuple(round(v, 3) for v in o.location) for o in objs]))
        print("   Blender bbox x %.2f..%.2f y %.2f..%.2f z %.2f..%.2f" % (lo[0], hi[0], lo[1], hi[1], lo[2], hi[2]))
        if fw == "Z":
            print("   => Unity-space: x %.2f..%.2f  y %.2f..%.2f  z %.2f..%.2f" % (lo[0], hi[0], lo[2], hi[2], lo[1], hi[1]))
        else:
            print("   => raw FBX: z(unity) %.2f..%.2f, y %.2f..%.2f" % (-hi[1], -lo[1], lo[2], hi[2]))
