"""Author editable low-poly modules in Blender; run with blender -b --python this_file."""
import bpy, math, random
from pathlib import Path
from mathutils import Vector

root=Path(__file__).resolve().parents[2]
destination=root/'Unity/Assets/External/Staging/Blender/Models'
destination.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
random.seed(723)

def material(name,color,metal=0):
    m=bpy.data.materials.new(name); m.diffuse_color=(*color,1); m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal; p.inputs['Roughness'].default_value=.65
    return m
stone=[material('Stone_'+str(i),(.22+i*.024,.24+i*.022,.27+i*.024)) for i in range(6)]
fur=material('Fur',(.26,.105,.036)); skin=material('HandSkin',(.82,.45,.20))
tan=material('GiraffeTan',(.76,.48,.16)); patch=material('GiraffeSpots',(.28,.12,.035))
gray=material('ElephantGray',(.35,.38,.44)); ivory=material('Ivory',(.92,.86,.73))
black=material('Eyes',(.016,.019,.026)); gold=material('Ore',(.95,.59,.06),.5)
authored=[]

def box(name,pos,size,mat,bevel=.09,segments=1):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos); ob=bpy.context.object; ob.name=name
    ob.scale=size; bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=ob.modifiers.new('Rounded rock edges','BEVEL'); mod.width=min(size)*bevel; mod.segments=segments
        bpy.ops.object.modifier_apply(modifier=mod.name)
    ob.data.materials.append(mat); authored.append(ob); return ob

def sphere(name,pos,size,mat,subdiv=1,smooth=False):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv,radius=.5,location=pos)
    ob=bpy.context.object; ob.name=name; ob.scale=size; ob.data.materials.append(mat)
    if smooth:
        for polygon in ob.data.polygons: polygon.use_smooth=True
    authored.append(ob); return ob

def link(name,a,b,width,mat):
    a,b=Vector(a),Vector(b); ob=box(name,(a+b)/2,(width,width,(b-a).length),mat,.15)
    ob.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler(); return ob

def export(name):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in authored: ob.select_set(True)
    bpy.context.view_layer.objects.active=authored[0]
    bpy.ops.export_scene.fbx(filepath=str(destination/(name+'.fbx')),use_selection=True,
        object_types={'MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,
        bake_anim=False,add_leaf_bones=False,path_mode='AUTO')
    collection=bpy.data.collections.new(name); bpy.context.scene.collection.children.link(collection)
    for ob in authored:
        for c in list(ob.users_collection): c.objects.unlink(ob)
        collection.objects.link(ob)
    authored.clear()
    print('BLENDER_MODULE_EXPORTED:',name)

# Interlocking broad fractured slabs, with dark recessed joins and varied silhouettes.
box('Recessed rock bed',(.30,0,2.1),(.45,2.5,4.3),stone[0],.07)
for row in range(6):
    for column in range(4):
        z=.32+row*.72+random.uniform(-.12,.12)
        y=-.94+column*.63+random.uniform(-.10,.10)+(row%2)*.09
        ob=box('FracturedBasaltSlab',(-.08+random.uniform(-.15,.10),y,z),
            (random.uniform(.50,.82),random.uniform(.66,.89),random.uniform(.68,.89)),random.choice(stone),.22)
        # Different planes and chipped corners, rather than a regular brick silhouette.
        for vertex in ob.data.vertices:
            vertex.co.x+=random.uniform(-.055,.055)
            vertex.co.y+=random.uniform(-.045,.045)
            vertex.co.z+=random.uniform(-.045,.045)
        ob.rotation_euler=(random.uniform(-.14,.14),random.uniform(-.15,.15),random.uniform(-.18,.18))
export('CaveWall')
for i in range(9):
    angle=math.pi*i/8
    ob=box('ArchStone',(4.8*math.cos(angle),0,3.8+1.5*math.sin(angle)),(1.4,1.5,.85),random.choice(stone),.16)
    ob.rotation_euler.y=-angle*.34
export('CaveArch')
for i in range(11):
    ob=sphere('LooseRock',(random.uniform(-.75,.75),random.uniform(-.65,.65),random.uniform(.15,.40)),
        (random.uniform(.35,.8),random.uniform(.35,.8),random.uniform(.28,.6)),random.choice(stone),1)
for i in range(4): sphere('GoldOre',(random.uniform(-.5,.5),random.uniform(-.5,.5),.5),(.32,.28,.40),gold,1)
export('RockPile')

for side in (-1,1):
    box('Palm',(0,0,0),(.22,.22,.15),skin,.28,4)
    box('Wrist',(0,-.14,0),(.15,.15,.12),skin,.25,4)
    for finger in range(4):
        x=-.077+finger*.052
        sphere('Knuckle',(x,.11,.045),(.062,.13,.10),skin,3,True)
        sphere('FoldedFinger',(x,.06,-.055),(.060,.09,.09),skin,3,True)
    link('Thumb',(side*.11,-.04,.025),(side*.115,.035,.075),.068,skin)
    link('ThumbTip',(side*.115,.035,.075),(side*.055,.055,.085),.062,skin)
    export('FP_Hand_Left' if side<0 else 'FP_Hand_Right')

sphere('GiraffeBody',(0,0,1.35),(.8,1.3,.95),tan,2)
for x in (-.24,.24):
    for y in (-.42,.42): link('Leg',(x,y,.12),(x,y,1.35),.20,tan)
link('Neck',(0,-.42,1.55),(0,-.65,3.10),.36,tan)
sphere('Head',(0,-.75,3.13),(.52,.72,.42),tan,2)
sphere('Muzzle',(0,-1.06,3.01),(.48,.36,.28),ivory,1)
for side in (-1,1):
    sphere('Ear',(side*.35,-.62,3.19),(.27,.19,.14),tan)
    link('Ossicone',(side*.15,-.62,3.25),(side*.15,-.62,3.49),.07,patch)
    sphere('EyeWhite',(side*.235,-.91,3.18),(.13,.08,.14),ivory,2)
    sphere('Pupil',(side*.248,-.953,3.18),(.06,.04,.07),black,2)
    for i in range(8): sphere('BodySpot',(side*.37,random.uniform(-.45,.42),random.uniform(1.13,1.65)),(.025,.18,.18),patch)
for i in range(6): sphere('NeckSpot',(.18,-.46-i*.03,1.7+i*.22),(.04,.13,.17),patch)
export('ZooGiraffe')
sphere('ElephantBody',(0,0,1.10),(1.6,2.15,1.45),gray,2)
sphere('Head',(0,-.95,1.45),(1.30,1.13,1.12),gray,2)
for x in (-.50,.50):
    for y in (-.59,.59): box('Leg',(x,y,.50),(.43,.47,1.0),gray,.24)
for side in (-1,1):
    sphere('Ear',(side*.72,-.94,1.49),(.77,.23,1.14),gray,2)
    sphere('Eye',(side*.43,-1.45,1.60),(.16,.07,.17),ivory,2)
    sphere('Pupil',(side*.43,-1.492,1.60),(.075,.03,.095),black,2)
    link('Tusk',(side*.35,-1.35,1.21),(side*.38,-1.77,1.05),.15,ivory)
points=[(0,-1.48,1.33),(0,-1.72,1.03),(0,-1.76,.62),(0,-1.83,.31),(0,-2.03,.40)]
for a,b in zip(points,points[1:]): link('Trunk',a,b,.29,gray)
export('ZooElephant')

# An open cart with plank seams, metal bands, rivets and circular wheels.
cartwood=material('CartTimber',(.31,.14,.046)); cartiron=material('CartSteel',(.13,.17,.20),.6)
cartdark=material('CartWheel',(.023,.03,.042),.25); lampglass=material('CartLampGlass',(1,.63,.19))
box('CartChassis',(0,0,.28),(1.38,1.87,.18),cartiron,.14)
box('CartBed',(0,0,.48),(1.32,1.75,.15),cartwood,.12)
for side in (-1,1):
    for layer in range(3):
        box('SidePlank',(side*.69,0,.66+layer*.24),(.105,1.8,.21),cartwood,.16)
        box('EndPlank',(0,side*.88,.66+layer*.24),(1.40,.11,.21),cartwood,.16)
    box('SteelRim',(side*.70,0,1.25),(.15,1.92,.11),cartiron,.12)
    box('EndRim',(0,side*.89,1.25),(1.50,.16,.11),cartiron,.12)
    for y in (-.73,.73):
        box('IronBrace',(side*.754,y,.90),(.045,.11,.69),cartiron,.1)
        for z in (.63,1.14): sphere('Rivet',(side*.786,y,z),(.055,.065,.065),cartiron,2)
        bpy.ops.mesh.primitive_cylinder_add(vertices=20,radius=.235,depth=.13,location=(side*.79,y,.245),rotation=(0,math.pi/2,0))
        ob=bpy.context.object; ob.name='CircularWheel'; ob.data.materials.append(cartdark); authored.append(ob)
        bpy.ops.mesh.primitive_cylinder_add(vertices=16,radius=.09,depth=.145,location=(side*.79,y,.245),rotation=(0,math.pi/2,0))
        ob=bpy.context.object; ob.name='WheelHub'; ob.data.materials.append(cartiron); authored.append(ob)
for i in range(9): sphere('OreLoad',(random.uniform(-.5,.5),random.uniform(-.55,.55),.85+random.uniform(0,.22)),(.30,.35,.31),gold if i%3==0 else random.choice(stone),2)
box('HeadlampHousing',(0,-.985,.99),(.37,.16,.36),cartiron,.22)
sphere('RoundHeadlamp',(0,-1.08,.99),(.27,.07,.27),lampglass,2)
cart_parts=list(authored)
export('MineCart')
for part in cart_parts:
    if part.name.startswith('OreLoad'):continue
    copy=part.copy();copy.data=part.data.copy();bpy.context.collection.objects.link(copy);authored.append(copy)
export('MineCartEmpty')

blend=root/'References/Blender/MuhanokModules.blend'; blend.parent.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(blend))
print('BLENDER_AUTHORING_PASS:',blend)
