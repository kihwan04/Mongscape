"""Editable cartoon characters, skeletons and authored motions. Blender 4.5 LTS.

These motions are locally authored; they are NOT downloaded Mixamo animations.
Coordinates: Z up, character faces -Y, T-pose for humanoid retargeting.
"""
import bpy, math, json
from pathlib import Path
from mathutils import Vector, Quaternion
from mathutils.kdtree import KDTree

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'Unity/Assets/External/Staging/Blender/Characters'
OUT.mkdir(parents=True, exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.context.scene.render.fps = 30
parts = []

def mat(name, rgb, metallic=0):
    m = bpy.data.materials.new(name); m.diffuse_color = (*rgb, 1); m.use_nodes = True
    p = m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value = (*rgb, 1)
    p.inputs['Metallic'].default_value = metallic; p.inputs['Roughness'].default_value = .62
    return m

fur=mat('Warm chestnut fur',(.34,.145,.060)); skin=mat('Golden muzzle and palms',(.90,.57,.29))
orange=mat('Prison orange',(.91,.22,.025)); seam=mat('Orange seam',(.53,.085,.012))
navy=mat('Police navy',(.038,.078,.17)); shirt=mat('Police blue grey',(.37,.48,.62))
black=mat('Boots and glasses',(.016,.021,.033)); white=mat('Ivory teeth',(.96,.92,.78))
gold=mat('Badge brass',(.94,.58,.06),.45); mouth=mat('Mouth interior',(.12,.012,.009))
tongue=mat('Tongue',(.73,.12,.13)); eye=mat('Eye white',(.98,.96,.87)); iris=mat('Chocolate pupils',(.032,.019,.009))

def weight(obj, bone):
    obj.vertex_groups.new(name=bone).add(list(range(len(obj.data.vertices))),1,'REPLACE')
    parts.append(obj); return obj

def ball(name, pos, size, material, bone, smooth=True):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=20, ring_count=12, radius=1, location=pos)
    ob=bpy.context.object; ob.name=name; ob.scale=size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    ob.data.materials.append(material)
    for p in ob.data.polygons: p.use_smooth=smooth
    return weight(ob,bone)

def box(name,pos,size,material,bone,bevel=.12):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos)
    ob=bpy.context.object; ob.name=name; ob.scale=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    mod=ob.modifiers.new('Soft cartoon edges','BEVEL'); mod.width=min(size)*bevel; mod.segments=3
    bpy.ops.object.modifier_apply(modifier=mod.name); ob.data.materials.append(material)
    return weight(ob,bone)

def limb(name,a,b,width,depth,material,bone):
    a,b=Vector(a),Vector(b)
    ob=ball(name,(a+b)/2,(width,depth,(b-a).length*.64),material,bone)
    ob.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler(); return ob

def skeleton():
    bpy.ops.object.armature_add(); rig=bpy.context.object; rig.name='Rig'
    bpy.ops.object.mode_set(mode='EDIT'); bones=rig.data.edit_bones; bones.remove(bones[0])
    specs={
        'Hips':((0,0,.78),(0,0,.98),None),
        'Spine':((0,0,.98),(0,0,1.15),'Hips'),
        'Chest':((0,0,1.15),(0,0,1.36),'Spine'),
        'Neck':((0,0,1.36),(0,0,1.49),'Chest'),
        'Head':((0,0,1.49),(0,0,2.08),'Neck')}
    for s,label in [(-1,'Right'),(1,'Left')]:
        specs.update({
            label+'Shoulder':((s*.07,0,1.32),(s*.32,0,1.32),'Chest'),
            label+'UpperArm':((s*.32,0,1.32),(s*.65,0,1.32),label+'Shoulder'),
            label+'LowerArm':((s*.65,0,1.32),(s*.94,0,1.32),label+'UpperArm'),
            label+'Hand':((s*.94,0,1.32),(s*1.12,0,1.32),label+'LowerArm'),
            label+'UpperLeg':((s*.18,0,.78),(s*.18,0,.43),'Hips'),
            label+'LowerLeg':((s*.18,0,.43),(s*.18,0,.13),label+'UpperLeg'),
            label+'Foot':((s*.18,0,.13),(s*.18,-.20,.10),label+'LowerLeg'),
            label+'Toes':((s*.18,-.20,.10),(s*.18,-.31,.10),label+'Foot')})
    for i in range(5):
        specs['Tail'+str(i)]=((0,.20+i*.12,.86-i*.035),(0,.32+i*.12,.825-i*.035),'Hips' if i==0 else 'Tail'+str(i-1))
    for name,(head,tail,parent) in specs.items():
        b=bones.new(name); b.head=head; b.tail=tail
        if parent: b.parent=bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT'); rig.show_in_front=True
    return rig

def model(guard):
    cloth=shirt if guard else fur
    ball('Rounded torso',(0,0,1.06),(.43 if guard else .28,.245 if guard else .225,.37),cloth,'Spine')
    ball('Rounded hips',(0,0,.78),(.31 if guard else .265,.24 if guard else .215,.19),navy if guard else fur,'Hips')
    if guard:
        box('Black utility belt',(0,-.02,.86),(.68,.48,.10),black,'Hips')
        box('Brass belt clasp',(0,-.272,.86),(.12,.035,.085),gold,'Hips')
        box('Jacket front seam',(0,-.252,1.1),(.018,.022,.42),navy,'Spine')
    else:
        ball('Neck connection',(0,0,1.41),(.19,.20,.23),fur,'Neck')
        ball('Golden belly',(0,-.20,1.055),(.205,.085,.285),skin,'Spine')
        # Readable prisoner identity without covering the expressive chest silhouette.
        box('Orange prisoner waistband',(0,0,.84),(.53,.44,.095),orange,'Hips',.20)
        box('Prisoner back patch',(0,.232,1.11),(.27,.035,.16),white,'Spine',.10)
        bpy.ops.object.text_add(location=(.115,.253,1.065),rotation=(math.pi/2,0,math.pi))
        text=bpy.context.object;text.data.body='0723';text.data.size=.10;text.data.extrude=.002
        text.data.materials.append(black);bpy.ops.object.convert(target='MESH');weight(bpy.context.object,'Spine')
    for s,label in [(-1,'Right'),(1,'Left')]:
        ball('Shoulder sleeve',(s*.34,0,1.29),(.19 if guard else .14,.20 if guard else .15,.18 if guard else .15),cloth,label+'UpperArm')
        limb('Upper sleeve',(s*.34,0,1.32),(s*.59,0,1.32),.145 if guard else .125,.155 if guard else .13,cloth,label+'UpperArm')
        limb('Forearm',(s*.65,0,1.32),(s*.93,0,1.32),.11,.12,skin if guard else fur,label+'LowerArm')
        ball('Elbow',(s*.65,0,1.32),(.12,.12,.12),skin if guard else fur,label+'LowerArm')
        ball('Knuckle palm',(s*1.015,-.015,1.32),(.12,.085,.10),skin,label+'Hand')
        for f in range(4): ball('Curled finger',(s*1.055,-.065,1.265+f*.035),(.075,.046,.028),skin,label+'Hand')
        ball('Thumb',(s*.98,-.095,1.375),(.055,.055,.065),skin,label+'Hand')
        if not guard:
            box('Prison wrist cuff',(s*.915,0,1.32),(.065,.215,.215),orange,label+'Hand',.18)
            for f in range(4):ball('Finger nail',(s*1.095,-.091,1.265+f*.035),(.025,.018,.014),white,label+'Hand')
        limb('Upper leg',(s*.18,0,.77),(s*.18,0,.43),.16 if guard else .13,.18 if guard else .145,navy if guard else fur,label+'UpperLeg')
        limb('Lower leg',(s*.18,0,.43),(s*.18,0,.16),.125,.145,navy if guard else fur,label+'LowerLeg')
        ball('Knee',(s*.18,-.005,.43),(.135,.15,.13),navy if guard else fur,label+'LowerLeg')
        if guard:
            box('Oversized boot',(s*.18,-.09,.13),(.30,.45,.25),black,label+'Foot',.28)
            box('Rubber sole',(s*.18,-.095,.035),(.31,.46,.065),black,label+'Foot',.22)
        else:
            ball('Broad bare foot',(s*.18,-.095,.105),(.17,.255,.105),skin,label+'Foot')
            for toe in range(3):
                ball('Toe',(s*.18+(toe-1)*.075,-.29,.095),(.046,.078,.060),skin,label+'Toes')
                ball('Toe nail',(s*.18+(toe-1)*.075,-.335,.128),(.025,.026,.012),white,label+'Toes')
        if guard:
            box('Pocket flap',(s*.21,-.23,1.18),(.20,.045,.09),navy,'Chest')
            box('Epaulette',(s*.29,0,1.40),(.20,.17,.034),gold,label+'Shoulder')
    # Large expressive face with recessed mouth, teeth, eye highlights and ears.
    ball('Large head',(0,0,1.77),(.36 if guard else .415,.29,.40),skin if guard else fur,'Head')
    if not guard:
        for s in (-1,1): ball('Eye face patch',(s*.14,-.232,1.83),(.19,.088,.23),skin,'Head')
        ball('Face bridge',(0,-.25,1.73),(.23,.085,.23),skin,'Head')
        for i in range(3):
            ob=ball('Hair tuft',((i-1)*.065,.015,2.115+i*.018),(.075,.075,.11),fur,'Head'); ob.rotation_euler.y=-.3
    ball('Rounded muzzle',(0,-.282,1.61),(.27 if guard else .305,.15,.15 if guard else .165),skin,'Head')
    ball('Open mouth',(0,-.41,1.573),(.195 if guard else .225,.048,.080 if guard else .096),mouth,'Head')
    if guard:
        box('Upper teeth',(0,-.445,1.627),(.26,.025,.038),white,'Head',.10)
    else:
        for tooth in range(4):
            box('Individual upper tooth',((tooth-1.5)*.053,-.447,1.632),(.048,.025,.035),white,'Head',.18)
        for side in (-1,1):
            ball('Cartoon canine',(side*.16,-.446,1.607),(.027,.016,.044),white,'Head')
    ball('Tongue',(0,-.446,1.538 if not guard else 1.558),(.095,.012,.026),tongue,'Head')
    ball('Nose',(0,-.436,1.71),(.075,.055,.055),skin if guard else fur,'Head')
    for s in (-1,1):
        ball('Ear',(s*(.355 if guard else .42),0,1.77),(.135,.085,.18),skin if guard else fur,'Head')
        ball('Inner ear',(s*(.366 if guard else .444),-.072,1.77),(.084,.026,.112),skin,'Head')
        if guard:
            box('Sunglass lens',(s*.145,-.29,1.855),(.25,.065,.16),black,'Head',.12)
            ob=ball('Moustache',(s*.088,-.423,1.675),(.10,.043,.037),fur,'Head'); ob.rotation_euler.y=s*.17
        else:
            ball('Eye white',(s*.145,-.303,1.842),(.112,.064,.142),eye,'Head')
            ball('Pupil',(s*.145,-.360,1.835),(.045,.014,.065),iris,'Head')
            ball('Eye glint',(s*.145-.014,-.375,1.86),(.016,.006,.022),white,'Head')
            ob=ball('Eyebrow',(s*.14,-.293,2.005),(.12,.040,.035),fur,'Head'); ob.rotation_euler.y=-s*.18
    if guard:
        box('Glasses bridge',(0,-.318,1.858),(.09,.06,.035),black,'Head')
        ball('Cap crown',(0,0,2.055),(.39,.31,.125),navy,'Head')
        box('Cap band',(0,-.025,1.992),(.66,.50,.055),black,'Head')
        box('Cap peak',(0,-.33,1.985),(.55,.27,.045),navy,'Head',.20)
        ball('Cap badge',(0,-.296,2.066),(.075,.015,.073),gold,'Head',False)
        box('Police tie',(0,-.272,1.22),(.085,.035,.22),navy,'Chest')
        ball('Shield badge',(.24,-.283,1.275),(.065,.016,.079),gold,'Chest',False)
        box('Portable radio',(-.33,-.12,.91),(.11,.13,.19),black,'Hips')
        box('Radio antenna',(-.33,-.1,1.03),(.018,.018,.14),black,'Hips')
    else:
        for i in range(5):
            limb('Curved tail',(0,.22+i*.12,.86-i*.035),(0,.34+i*.12,.825-i*.035),.045,.045,fur,'Tail'+str(i))

def fuse_surface(material):
    """Union the body surfaces and transfer blended skin weights across the new joints."""
    objects=[o for o in parts if o.data.materials[0]==material]
    samples=[]
    for ob in objects:
        for vertex in ob.data.vertices:
            samples.append((ob.matrix_world @ vertex.co,[(ob.vertex_groups[g.group].name,g.weight) for g in vertex.groups]))
    tree=KDTree(len(samples))
    for i,(position,_) in enumerate(samples): tree.insert(position,i)
    tree.balance()
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects: ob.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    for ob in objects: parts.remove(ob)
    bpy.ops.object.join(); mesh=bpy.context.object
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    mesh.data.remesh_voxel_size=.022
    bpy.ops.object.voxel_remesh()
    smoothing=mesh.modifiers.new('Continuous rounded silhouette','SMOOTH'); smoothing.factor=.50; smoothing.iterations=3
    bpy.ops.object.modifier_apply(modifier=smoothing.name)
    decimate=mesh.modifiers.new('Gameplay topology budget','DECIMATE'); decimate.ratio=.50
    bpy.ops.object.modifier_apply(modifier=decimate.name)
    mesh.vertex_groups.clear(); groups={}
    for vertex in mesh.data.vertices:
        weights={}
        for _,index,distance in tree.find_n(vertex.co,4):
            influence=1/max(distance,.005)**2
            for bone,w in samples[index][1]: weights[bone]=weights.get(bone,0)+w*influence
        total=sum(weights.values())
        for bone,w in weights.items():
            if bone not in groups: groups[bone]=mesh.vertex_groups.new(name=bone)
            groups[bone].add([vertex.index],w/total,'REPLACE')
    for polygon in mesh.data.polygons: polygon.use_smooth=True
    mesh.name='Continuous_'+material.name; parts.append(mesh)

def pose(rig,bone,axis,angle):
    b=rig.pose.bones[bone]; basis=rig.data.bones[bone].matrix_local.to_quaternion()
    b.rotation_mode='QUATERNION'; b.rotation_quaternion=basis.inverted() @ Quaternion(axis,math.radians(angle)) @ basis

def action(rig,name):
    a=bpy.data.actions.new(name); rig.animation_data_create(); rig.animation_data.action=a
    running=any(x in name for x in ['Run','SprintStart','HighKnee','LaneStep'])
    mining='MinePickaxe' in name; crouch='Crouch' in name; catch='Caught' in name or 'Catch' in name; cart='CartRide' in name; cell='CellIdle' in name
    frames=21 if running else 37
    for frame in range(1,frames+1):
        t=(frame-1)/(frames-1); wave=math.sin(t*2*math.pi)
        for b in rig.pose.bones:
            b.rotation_mode='QUATERNION'; b.rotation_quaternion=Quaternion(); b.location=(0,0,0)
        for s,label in [(-1,'Right'),(1,'Left')]:
            base=Quaternion((0,1,0),math.radians(s*90))
            # Positive-X arm is Left: lowering the T-pose rotates +90 degrees about Y.
            swing=Quaternion((1,0,0),math.radians((wave*s*34 if running else 0)+(-48+wave*2 if cell else -60+wave*2 if cart else -48+wave*26 if mining else -42 if catch else 0)))
            basis=rig.data.bones[label+'UpperArm'].matrix_local.to_quaternion()
            rig.pose.bones[label+'UpperArm'].rotation_quaternion=basis.inverted() @ swing @ base @ basis
            pose(rig,label+'LowerArm',(0,0,1),-s*(15 if cell else 25 if cart else 55 if mining else 65 if running else 22))
            pose(rig,label+'UpperLeg',(1,0,0),-42 if cart else wave*s*32 if running else -45 if crouch else -18 if 'Jump' in name else 0)
            pose(rig,label+'LowerLeg',(1,0,0),70 if cart else max(0,-wave*s)*55 if running else 75 if crouch else 30 if 'Jump' in name else 4)
            pose(rig,label+'Foot',(1,0,0),-max(0,-wave*s)*15 if running else 0)
        pose(rig,'Spine',(1,0,0),14+wave*1.5 if cart else 10 if running else 18+wave*8 if mining else 30 if crouch else 0)
        pose(rig,'Head',(0,0,1),wave*9 if cell else -65*math.sin(t*math.pi/2) if 'LookCart' in name else 0)
        pose(rig,'Neck',(1,0,0),12 if 'Idea' in name else -8 if mining else 0)
        rig.pose.bones['Hips'].location.y=(-.12 if cart else .022*math.cos(t*4*math.pi) if running else -.15 if crouch else 0)
        if 'Stumble' in name: pose(rig,'Spine',(0,1,0),18*math.sin(t*math.pi))
        for i in range(5): pose(rig,'Tail'+str(i),(0,0,1),wave*10)
        for b in rig.pose.bones:
            b.keyframe_insert('rotation_quaternion',frame=frame); b.keyframe_insert('location',frame=frame)
    bpy.context.scene.frame_start=1; bpy.context.scene.frame_end=frames; bpy.context.scene.frame_set(1)
    return a

def export(path,objects,animated=False):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects: o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'ARMATURE','MESH'},
        axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=animated,
        bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,
        use_armature_deform_only=True,mesh_smooth_type='FACE')

collections=[]; manifest=[]
for guard in (False,True):
    parts.clear(); rig=skeleton(); model(guard)
    if not guard:
        fuse_surface(fur); fuse_surface(skin)
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts: o.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]; bpy.ops.object.join(); mesh=bpy.context.object
    mesh.name='Police_SkinnedMesh' if guard else 'Monkey_SkinnedMesh'
    # Apply geometry transforms before attaching the armature: FBX bind matrices remain unit scale.
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    mod=mesh.modifiers.new('Humanoid skin','ARMATURE'); mod.object=rig; mesh.parent=rig
    if not guard:
        mesh.shape_key_add(name='Basis')
        blink=mesh.shape_key_add(name='Blink'); jaw=mesh.shape_key_add(name='JawOpen')
        eye_vertices=set(); mouth_vertices=set()
        for polygon in mesh.data.polygons:
            material=mesh.data.materials[polygon.material_index]
            for index in polygon.vertices:
                v=mesh.data.vertices[index].co
                if material in (eye,iris,white) and v.z>1.73 and v.y<-.295: eye_vertices.add(index)
                if material in (skin,mouth,tongue,white) and 1.43<v.z<1.665 and v.y<-.30: mouth_vertices.add(index)
        for index in eye_vertices: blink.data[index].co.z=1.842+(blink.data[index].co.z-1.842)*.06
        for index in mouth_vertices:
            v=jaw.data[index].co; v.z-=max(0,1.665-v.z)*.30; v.x*=1.07
    ident='Police' if guard else 'Monkey'; export(OUT/(ident+'.fbx'),[rig,mesh])
    if guard:
        duplicate=mesh.copy(); duplicate.data=mesh.data.copy(); bpy.context.collection.objects.link(duplicate)
        duplicate.parent=None; duplicate.matrix_world=mesh.matrix_world.copy()
        for modifier in list(duplicate.modifiers): duplicate.modifiers.remove(modifier)
        duplicate.vertex_groups.clear()
        export(OUT/'Police_TPose_For_Mixamo.fbx',[duplicate]); bpy.data.objects.remove(duplicate,do_unlink=True)
        names=['AN_Guard_Notice','AN_Guard_ChaseRun','AN_Guard_Catch']
    else:
        names=['AN_Monkey_'+n for n in ['Run','SprintStart','HighKnee','LaneStep_L','LaneStep_R','MinePickaxe_Loop','StopWork','LookCart','IdeaReact','Jump','CrouchSlide','Stumble','Caught','CartRide','CellIdle']]
    for name in names:
        a=action(rig,name); export(OUT/(name+'.fbx'),[rig],True)
        manifest.append({'clip':name,'source':'Locally authored Blender action','frames':bpy.context.scene.frame_end})
    rig.animation_data.action=None
    for b in rig.pose.bones: b.rotation_quaternion=Quaternion(); b.location=(0,0,0)
    collection=bpy.data.collections.new(ident); bpy.context.scene.collection.children.link(collection)
    for o in (rig,mesh):
        for c in list(o.users_collection): c.objects.unlink(o)
        collection.objects.link(o)
    collections.append(collection)

# A neutral preview scene is retained in the .blend source for visual inspection.
collections[0].objects.get('Rig').location.x=-1.5
for o in collections[1].objects:
    if o.type=='ARMATURE': o.location.x=1.5
bpy.ops.mesh.primitive_plane_add(size=200); bpy.context.object.data.materials.append(mat('Preview ground',(.065,.078,.105)))
world=bpy.context.scene.world; world.use_nodes=True; world.node_tree.nodes['Background'].inputs[0].default_value=(.15,.18,.24,1)
for pos,power,size in [((-3,-4,6),800,5),((4,-1,4),600,4),((0,3,5),1000,3)]:
    bpy.ops.object.light_add(type='AREA',location=pos); o=bpy.context.object; o.data.energy=power; o.data.shape='DISK'; o.data.size=size
    o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(3,-8,3.4)); cam=bpy.context.object
cam.rotation_euler=(Vector((0,0,1.1))-cam.location).to_track_quat('-Z','Y').to_euler(); cam.data.type='ORTHO'; cam.data.ortho_scale=5.8
bpy.context.scene.camera=cam; bpy.context.scene.render.engine='CYCLES'; bpy.context.scene.cycles.samples=24
bpy.context.scene.render.resolution_x=1440; bpy.context.scene.render.resolution_y=900; bpy.context.scene.render.resolution_percentage=100
source=ROOT/'References/Blender/MuhanokCharacters.blend'; source.parent.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(source))
preview=ROOT/'Builds/QA/VisualReview/ArtV10/BlenderCharacters.png'; preview.parent.mkdir(parents=True,exist_ok=True)
bpy.context.scene.render.filepath=str(preview); bpy.ops.render.render(write_still=True)
(OUT/'AuthoredMotions.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print('BLENDER_CHARACTERS_PASS: 2 detailed skinned characters, 18 authored animations including cart grip and cell idle, unrigged Mixamo T-pose')
