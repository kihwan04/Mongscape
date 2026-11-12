"""Zoo stair chunk (route chunk, entry z=0 floor y=0, exit z=24 floor y=+4.5).

Run: blender --background --factory-startup --python make_zoo_stair.py [-- <preview_dir>]
Deterministic (fixed seed). Outputs ZooStair.blend, ZooStair.fbx and an optional preview PNG.
Materials: Stone, Wood, Foliage, Earth (4). One merged mesh.
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import stair_common as sc
from stair_common import height as H

paths = sc.repo_paths()
args = sc.script_args()
sc.reset_scene()

STONE, WOOD, FOLIAGE, EARTH = 0, 1, 2, 3
mats = [
    sc.make_material("ZooStair_Stone", (0.52, 0.48, 0.40), 0.8),
    sc.make_material("ZooStair_Wood", (0.40, 0.21, 0.09), 0.7),
    sc.make_material("ZooStair_Foliage", (0.17, 0.40, 0.16), 0.9),
    sc.make_material("ZooStair_Earth", (0.20, 0.33, 0.17), 0.95),
]
b = sc.Builder(mats, seed=2024)

STEPS, DEPTH = 30, 0.8
BOTTOM = -0.7
WALK = 3.3
OUT = 9.0  # embankment half width


def ground_y(x, z):
    """Embankment surface height at |x| (terrain beside the stairs)."""
    ax = abs(x)
    h = H(z)
    if ax <= 5.0:
        return h - 0.05
    hout = max(-0.5, h * 0.3 - 0.5)
    t = min(1.0, (ax - 5.0) / (OUT - 5.0))
    return (h - 0.05) * (1 - t) + hout * t


# --- stone steps with wooden nosing boards (nosings on the smoothstep) -----------------
for i in range(STEPS):
    z0 = i * DEPTH
    zm = z0 + DEPTH / 2
    top = H(zm)
    b.box((0, (BOTTOM + top) / 2, zm), (2 * WALK, top - BOTTOM, DEPTH), STONE)
    b.box((0, top + 0.01, z0 + 0.09), (2 * WALK - 0.1, 0.05, 0.18), WOOD)

# --- wooden side stringers (retaining boards) ------------------------------------------
for s in (-1, 1):
    x0, x1 = sorted((s * WALK, s * (WALK + 0.22)))
    b.ribbon(WOOD, 0, 24, 24, lambda z, x0=x0, x1=x1: [(x0, H(z) - 0.8), (x1, H(z) - 0.8), (x1, H(z) + 0.14), (x0, H(z) + 0.14)])

# --- grass embankments on both sides, rising with the stairs ---------------------------
for s in (-1, 1):
    def prof(z, s=s):
        xi, xm, xo = WALK + 0.22, 5.0, OUT
        pts = [(xi, BOTTOM), (xo, BOTTOM), (xo, ground_y(xo, z)), (xm, ground_y(xm, z)), (xi, ground_y(xi, z))]
        return [(s * x, y) for x, y in pts]
    b.ribbon(EARTH, 0, 24, 24, prof)

# --- wooden railings -------------------------------------------------------------------
for s in (-1, 1):
    x = s * (WALK + 0.05)
    for dy, w, wy in ((1.05, 0.12, 0.1), (0.62, 0.08, 0.08)):
        b.ribbon(WOOD, 0, 24, 24, lambda z, x=x, dy=dy, w=w, wy=wy: [
            (x - w / 2, H(z) + dy - wy / 2), (x + w / 2, H(z) + dy - wy / 2),
            (x + w / 2, H(z) + dy + wy / 2), (x - w / 2, H(z) + dy + wy / 2)])
    z = 0.0
    while z <= 24.01:
        zz = min(z, 24.0)
        b.box((x, H(zz) + 0.5, max(0.1, min(23.9, zz))), (0.16, 1.2, 0.16), WOOD)
        z += 2.4
# gate posts at both ends with a cap, zoo-entrance feel
for z in (0.3, 23.7):
    for s in (-1, 1):
        b.box((s * 3.75, H(z) + 1.2, z), (0.28, 2.4, 0.28), WOOD)
        b.box((s * 3.75, H(z) + 2.45, z), (0.4, 0.1, 0.4), WOOD)

# --- bushes and rock clusters at the sides --------------------------------------------
for s in (-1, 1):
    z = 1.2
    while z < 23.0:
        zc = z + b.r(-0.5, 0.5)
        for k in range(3):
            x = s * b.r(4.1, 7.2)
            zz = zc + b.r(-0.9, 0.9)
            r = b.r(0.45, 0.85)
            b.blob((x, ground_y(x, zz) + r * 0.55, zz), (r, r * 0.8, r), FOLIAGE, subdiv=1, jitter=0.18, yaw=b.r(0, 6.28))
        z += b.r(2.3, 3.4)
    for zr in (3.0, 9.5, 14.5, 20.5):
        for k in range(3):
            x = s * b.r(4.4, 6.6)
            zz = zr + b.r(-0.9, 0.9)
            r = b.r(0.3, 0.7)
            b.blob((x, ground_y(x, zz) + r * 0.35, zz), (r, r * 0.7, r * 1.1), STONE, subdiv=0, jitter=0.3, yaw=b.r(0, 6.28))

obj = b.build_object("ZooStair", clamp=(OUT, 0.0, 24.0))
sc.stats(obj)
_ri = [H(i * DEPTH + DEPTH / 2 + DEPTH) - H(i * DEPTH + DEPTH / 2) for i in range(STEPS - 1)]
print("  riser min %.3f max %.3f m (rise %.1f m)" % (min(_ri), max(_ri), sc.RISE))
print("  steps=%d depth=%.2f m, max tread-to-curve error %.3f m" % (
    STEPS, DEPTH, max(abs(H(i * DEPTH + DEPTH / 2) - H(i * DEPTH)) for i in range(STEPS))))

blend_path = os.path.join(paths["blend_dir"], "ZooStair.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)
print("SAVED", blend_path)
sc.export_fbx(obj, os.path.join(paths["fbx_dir"], "ZooStair.fbx"))

if args:
    os.makedirs(args[0], exist_ok=True)
    sc.render_preview(os.path.join(args[0], "ZooStair_preview.png"),
                      cam_loc_unity=(4.5, 3.4, -7), target_unity=(0, 2.0, 12),
                      lights_unity=[], sun=True, lens=24)
