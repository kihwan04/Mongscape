"""Author the broken-rail hazard sign for the cart ravine; run with blender -b --python this_file.

Same conventions as build_blender_modules.py: metres, Z up, the readable face toward Blender +Y
(Unity -Z, toward the approaching cart), exported as FBX into the Blender staging models folder.
The safe-side arrow is not in the model; the Unity builder adds it below the board per side.
"""
import bpy, math
from pathlib import Path

root=Path(__file__).resolve().parents[2]
destination=root/'Unity/Assets/External/Staging/Blender/Models'
destination.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)

def material(name,color,metal=0):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=.65
    return m
# Unity remaps slots by name: *Yellow -> emissive hazard yellow, *Black -> charcoal.
yellow=material('HazardYellow',(1,.74,.04)); black=material('HazardBlack',(.02,.02,.026))
steel=material('HazardPostSteel',(.20,.23,.26),.5)
authored=[]

def box(name,pos,size,mat,bevel=0,rotation=(0,0,0)):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos); ob=bpy.context.object; ob.name=name
    ob.scale=size; bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=ob.modifiers.new('Edge bevel','BEVEL'); mod.width=min(size)*bevel; mod.segments=1
        bpy.ops.object.modifier_apply(modifier=mod.name)
    ob.rotation_euler=rotation; ob.data.materials.append(mat); authored.append(ob); return ob

def export(name):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in authored: ob.select_set(True)
    bpy.context.view_layer.objects.active=authored[0]
    bpy.ops.export_scene.fbx(filepath=str(destination/(name+'.fbx')),use_selection=True,
        object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
        bake_anim=False,add_leaf_bones=False,path_mode='AUTO')
    tris=sum(len(p.vertices)-2 for ob in authored for p in ob.data.polygons)
    print('BLENDER_MODULE_EXPORTED:',name,'objects',len(authored),'tris',tris)

# Post and foot, with yellow/black hazard bands where the arrow board is fixed.
box('SignFoot',(0,-.07,.06),(.42,.42,.12),steel,.2)
box('SignPost',(0,-.07,1.27),(.12,.12,2.46),steel,.15)
for i in range(6):
    box('PostBand',(0,-.07,.18+i*.13),(.145,.145,.13),yellow if i%2==0 else black)
# Diamond board: black border, yellow face and a bold geometric '!' standing proud of it.
centre=1.86; diamond=(0,math.pi/4,0)
box('DiamondBorder',(0,0,centre),(1.05,.05,1.05),black,.06,diamond)
box('DiamondFace',(0,.032,centre),(.86,.02,.86),yellow,0,diamond)
box('ExclamationBar',(0,.052,centre+.09),(.15,.03,.48),black)
box('ExclamationDot',(0,.052,centre-.30),(.16,.03,.15),black)
# A back plate so the board reads as solid from the side the cart passes.
box('DiamondBack',(0,-.032,centre),(.86,.02,.86),yellow,0,diamond)
export('TrackHazardSign')

blend=root/'References/Blender/TrackHazardSign.blend'; blend.parent.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(blend))
print('BLENDER_AUTHORING_PASS:',blend)
