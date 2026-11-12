"""Shared helpers for the procedural stair chunk generators (Blender 5.x, headless).

All geometry is authored in Unity-style coordinates (x right, y up, z forward, metres)
and converted to Blender coordinates (X=ux, Y=uz, Z=uy) when the mesh is created.
FBX export uses axis_forward='Z', axis_up='Y' so Blender +Y -> FBX/Unity +Z and Blender +Z -> +Y.
"""
import math, os, random, sys
import bpy, bmesh
from mathutils import Vector

LENGTH = 24.0
RISE = 3.0


def height(z):
    """Game route height curve (RouteSurface.LocalHeight, type 7): smoothstep 0..3 over 24 m."""
    t = max(0.0, min(1.0, z / LENGTH))
    return RISE * t * t * (3 - 2 * t)


def repo_paths():
    here = os.path.dirname(os.path.abspath(__file__))
    pr = os.path.abspath(os.path.join(here, "..", "..", ".."))
    return {
        "blend_dir": os.path.join(pr, "References", "Blender"),
        "fbx_dir": os.path.join(pr, "Unity", "Assets", "External", "Staging", "Blender"),
    }


def script_args():
    argv = sys.argv
    return argv[argv.index("--") + 1:] if "--" in argv else []


def reset_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    for coll in (bpy.data.meshes, bpy.data.materials, bpy.data.objects):
        for item in list(coll):
            coll.remove(item)


def make_material(name, color, roughness, metallic=0.0, emission=None, strength=0.0):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Base Color"].default_value = (color[0], color[1], color[2], 1.0)
    bsdf.inputs["Roughness"].default_value = roughness
    bsdf.inputs["Metallic"].default_value = metallic
    if emission is not None:
        bsdf.inputs["Emission Color"].default_value = (emission[0], emission[1], emission[2], 1.0)
        bsdf.inputs["Emission Strength"].default_value = strength
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    mat.diffuse_color = (color[0], color[1], color[2], 1.0)
    mat.roughness = roughness
    mat.metallic = metallic
    return mat


class Builder:
    def __init__(self, materials, seed):
        self.materials = materials  # list of bpy materials; face material = index
        self.verts = []
        self.faces = []
        self.fmat = []
        self.rng = random.Random(seed)

    def r(self, a=0.0, b=1.0):
        return a + (b - a) * self.rng.random()

    def _add(self, pts, faces, mat):
        base = len(self.verts)
        self.verts.extend(pts)
        for f in faces:
            self.faces.append(tuple(base + i for i in f))
            self.fmat.append(mat)

    # primitives -------------------------------------------------------------
    def box(self, c, size, mat, yaw=0.0):
        sx, sy, sz = size[0] / 2, size[1] / 2, size[2] / 2
        cs, sn = math.cos(yaw), math.sin(yaw)
        pts = []
        for dx in (-1, 1):
            for dy in (-1, 1):
                for dz in (-1, 1):
                    x, z = dx * sx, dz * sz
                    pts.append((c[0] + x * cs + z * sn, c[1] + dy * sy, c[2] - x * sn + z * cs))
        faces = [(0, 1, 3, 2), (4, 6, 7, 5), (0, 4, 5, 1), (2, 3, 7, 6), (0, 2, 6, 4), (1, 5, 7, 3)]
        self._add(pts, faces, mat)

    def beam(self, p0, p1, w, mat, wy=None):
        a, b = Vector(p0), Vector(p1)
        d = (b - a)
        d.normalize()
        ref = Vector((0, 1, 0)) if abs(d.y) < 0.9 else Vector((1, 0, 0))
        u = d.cross(ref)
        u.normalize()
        v = d.cross(u)
        v.normalize()
        hu, hv = w / 2, (wy if wy else w) / 2
        pts = []
        for p in (a, b):
            for su, sv in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                q = p + u * (su * hu) + v * (sv * hv)
                pts.append((q.x, q.y, q.z))
        faces = [(0, 1, 2, 3), (4, 7, 6, 5), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
        self._add(pts, faces, mat)

    def ribbon(self, mat, z0, z1, n, profile):
        """Closed prism swept along z. profile(z) -> list of (x, y), convex, consistent order."""
        pts, faces = [], []
        m = None
        for k in range(n + 1):
            z = z0 + (z1 - z0) * k / n
            prof = profile(z)
            m = len(prof)
            pts.extend((x, y, z) for x, y in prof)
        for k in range(n):
            for i in range(m):
                j = (i + 1) % m
                faces.append((k * m + i, k * m + j, (k + 1) * m + j, (k + 1) * m + i))
        faces.append(tuple(range(m)))
        faces.append(tuple(n * m + i for i in range(m)))
        self._add(pts, faces, mat)

    def blob(self, c, radii, mat, subdiv=0, jitter=0.25, yaw=0.0):
        """Noisy icosahedron (rock / bush lump). 20 tris at subdiv 0, 80 at subdiv 1."""
        t = (1 + 5 ** 0.5) / 2
        v = [(-1, t, 0), (1, t, 0), (-1, -t, 0), (1, -t, 0), (0, -1, t), (0, 1, t), (0, -1, -t), (0, 1, -t),
             (t, 0, -1), (t, 0, 1), (-t, 0, -1), (-t, 0, 1)]
        f = [(0, 11, 5), (0, 5, 1), (0, 1, 7), (0, 7, 10), (0, 10, 11), (1, 5, 9), (5, 11, 4), (11, 10, 2),
             (10, 7, 6), (7, 1, 8), (3, 9, 4), (3, 4, 2), (3, 2, 6), (3, 6, 8), (3, 8, 9), (4, 9, 5),
             (2, 4, 11), (6, 2, 10), (8, 6, 7), (9, 8, 1)]
        v = [tuple(x / math.sqrt(sum(a * a for a in p)) for x in p) for p in v]
        for _ in range(subdiv):
            cache, nf = {}, []

            def mid(a, b):
                key = (min(a, b), max(a, b))
                if key not in cache:
                    p = tuple((v[a][i] + v[b][i]) / 2 for i in range(3))
                    l = math.sqrt(sum(x * x for x in p))
                    v.append(tuple(x / l for x in p))
                    cache[key] = len(v) - 1
                return cache[key]
            for a, b, cc in f:
                ab, bc, ca = mid(a, b), mid(b, cc), mid(cc, a)
                nf += [(a, ab, ca), (b, bc, ab), (cc, ca, bc), (ab, bc, ca)]
            f = nf
        cs, sn = math.cos(yaw), math.sin(yaw)
        pts = []
        for p in v:
            k = 1 + (self.rng.random() * 2 - 1) * jitter
            x, y, z = p[0] * radii[0] * k, p[1] * radii[1] * k, p[2] * radii[2] * k
            pts.append((c[0] + x * cs + z * sn, c[1] + y, c[2] - x * sn + z * cs))
        self._add(pts, [tuple(x) for x in f], mat)

    def cone(self, base_c, radius, tip, mat, sides=5):
        pts = [(base_c[0] + radius * math.cos(2 * math.pi * i / sides), base_c[1],
                base_c[2] + radius * math.sin(2 * math.pi * i / sides)) for i in range(sides)]
        pts.append(tip)
        faces = [(i, (i + 1) % sides, sides) for i in range(sides)]
        faces.append(tuple(range(sides - 1, -1, -1)))
        self._add(pts, faces, mat)

    # output -----------------------------------------------------------------
    def build_object(self, name, clamp=None):
        """clamp=(xmax, zmin, zmax) keeps lumps inside the chunk footprint."""
        if clamp:
            xm, z0, z1 = clamp
            self.verts = [(max(-xm, min(xm, x)), y, max(z0, min(z1, z))) for x, y, z in self.verts]
        me = bpy.data.meshes.new(name)
        bverts = [(x, z, y) for x, y, z in self.verts]  # Unity (x,y,z) -> Blender (X=x, Y=z, Z=y)
        me.from_pydata(bverts, [], self.faces)
        for m in self.materials:
            me.materials.append(m)
        me.update()
        for poly, mi in zip(me.polygons, self.fmat):
            poly.material_index = mi
        bm = bmesh.new()
        bm.from_mesh(me)
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bmesh.ops.triangulate(bm, faces=bm.faces[:], quad_method='BEAUTY')
        bm.to_mesh(me)
        bm.free()
        for p in me.polygons:
            p.use_smooth = False
        obj = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(obj)
        return obj


def stats(obj):
    me = obj.data
    used = sorted({p.material_index for p in me.polygons})
    bb = [obj.matrix_world @ Vector(c) for c in obj.bound_box]
    lo = [min(p[i] for p in bb) for i in range(3)]
    hi = [max(p[i] for p in bb) for i in range(3)]
    print("STATS %s: tris=%d verts=%d materials=%d meshes=1" % (obj.name, len(me.polygons), len(me.vertices), len(used)))
    print("  blender bbox x %.2f..%.2f  y %.2f..%.2f  z %.2f..%.2f" % (lo[0], hi[0], lo[1], hi[1], lo[2], hi[2]))
    print("  unity   bbox x %.2f..%.2f  y %.2f..%.2f  z %.2f..%.2f" % (lo[0], hi[0], lo[2], hi[2], lo[1], hi[1]))


def export_fbx(obj, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', global_scale=1.0,
        bake_space_transform=True, axis_forward='Z', axis_up='Y',
        mesh_smooth_type='FACE', add_leaf_bones=False, use_mesh_modifiers=True,
        path_mode='AUTO')
    print("EXPORTED", path)


def render_preview(path, cam_loc_unity, target_unity, lights_unity, sun=True, res=(1280, 720), lens=24):
    """Render a quick preview. Coordinates given in Unity space."""
    def b(p):
        return Vector((p[0], p[2], p[1]))
    scn = bpy.context.scene
    world = bpy.data.worlds.new("W")
    world.use_nodes = True
    nt = world.node_tree
    nt.nodes.clear()
    bg = nt.nodes.new("ShaderNodeBackground")
    wo = nt.nodes.new("ShaderNodeOutputWorld")
    bg.inputs["Color"].default_value = (0.35, 0.42, 0.55, 1)
    bg.inputs["Strength"].default_value = 1.0
    nt.links.new(bg.outputs["Background"], wo.inputs["Surface"])
    scn.world = world
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    cam.data.lens = lens
    cam.data.clip_end = 200
    scn.collection.objects.link(cam)
    cam.location = b(cam_loc_unity)
    cam.rotation_euler = (b(target_unity) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    scn.camera = cam
    if sun:
        s = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))
        s.data.energy = 3.0
        s.rotation_euler = (math.radians(50), math.radians(10), math.radians(30))
        scn.collection.objects.link(s)
    for i, (p, e) in enumerate(lights_unity):
        l = bpy.data.objects.new("L%d" % i, bpy.data.lights.new("L%d" % i, 'POINT'))
        l.data.energy = e
        l.data.shadow_soft_size = 0.3
        l.location = b(p)
        scn.collection.objects.link(l)
    scn.render.resolution_x, scn.render.resolution_y = res
    scn.render.filepath = path
    scn.render.image_settings.file_format = 'PNG'
    try:
        scn.render.engine = 'BLENDER_EEVEE'
    except TypeError:
        scn.render.engine = 'BLENDER_EEVEE_NEXT'
    try:
        bpy.ops.render.render(write_still=True)
    except Exception as ex:
        print("Eevee failed (%s); falling back to Workbench" % ex)
        scn.render.engine = 'BLENDER_WORKBENCH'
        scn.display.shading.light = 'STUDIO'
        scn.display.shading.color_type = 'MATERIAL'
        bpy.ops.render.render(write_still=True)
    print("PREVIEW", path)
