"""Mine stair chunk (route chunk, entry z=0 floor y=0, exit z=24 floor y=+4.5).

Run: blender --background --factory-startup --python make_mine_stair.py [-- <preview_dir>]
Deterministic (fixed seed). Outputs MineStair.blend, MineStair.fbx and an optional preview PNG.
Materials: Rock, Timber, Iron, LanternGlass (4). One merged mesh.
"""
import os, sys, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
import stair_common as sc
from stair_common import height as H

paths = sc.repo_paths()
args = sc.script_args()
sc.reset_scene()

ROCK, TIMBER, IRON, GLOW = 0, 1, 2, 3
mats = [
    sc.make_material("MineStair_Rock", (0.36, 0.32, 0.28), 0.85),
    sc.make_material("MineStair_Timber", (0.46, 0.25, 0.095), 0.75),
    sc.make_material("MineStair_Iron", (0.20, 0.24, 0.27), 0.45, metallic=0.5),
    sc.make_material("MineStair_LanternGlass", (1.0, 0.61, 0.19), 0.35, emission=(1.0, 0.39, 0.07), strength=4.5),
]
b = sc.Builder(mats, seed=1337)

STEPS, DEPTH = 30, 0.8
FLOOR_BOTTOM = -0.7
WALK = 3.3           # half walkable width (6.6 m)
FLOOR_HALF = 4.9     # matches the 9.8 m continuous floor of neighbouring chunks
ROOF_BOT, ROOF_TOP = 5.3, 5.9

# --- steps: cut-rock blocks with timber boards, nosings sit on the smoothstep ----------
for i in range(STEPS):
    z0 = i * DEPTH
    zm = z0 + DEPTH / 2
    top = H(zm)                      # tread height on the curve (nosing z0 is within +-0.07 m)
    b.box((0, (FLOOR_BOTTOM + top - 0.1) / 2, zm), (2 * WALK, top - 0.1 - FLOOR_BOTTOM, DEPTH), ROCK)
    for k in range(2):               # two boards per tread
        bz = z0 + DEPTH * (0.25 + 0.5 * k)
        b.box((0, top - 0.05, bz + (-0.02 if k == 0 else 0.0)), (2 * WALK - 0.1, 0.1, DEPTH / 2 - 0.03), TIMBER)
    b.box((0, top - 0.1, z0 + 0.03), (2 * WALK, 0.08, 0.06), IRON)  # iron nosing strap

# --- side shelves (rock floor to the wall, follows the curve) --------------------------
for s in (-1, 1):
    x0, x1 = sorted((s * WALK, s * FLOOR_HALF))
    b.ribbon(ROCK, 0, 24, 24, lambda z, x0=x0, x1=x1: [(x0, FLOOR_BOTTOM), (x1, FLOOR_BOTTOM), (x1, H(z) - 0.05), (x0, H(z) - 0.05)])

# --- rock walls and roof ---------------------------------------------------------------
for s in (-1, 1):
    x0, x1 = sorted((s * 5.0, s * 5.8))
    b.ribbon(ROCK, 0, 24, 24, lambda z, x0=x0, x1=x1: [(x0, FLOOR_BOTTOM), (x1, FLOOR_BOTTOM), (x1, H(z) + ROOF_TOP), (x0, H(z) + ROOF_TOP)])
b.ribbon(ROCK, 0, 24, 24, lambda z: [(-5.8, H(z) + ROOF_BOT), (5.8, H(z) + ROOF_BOT), (5.8, H(z) + ROOF_TOP), (-5.8, H(z) + ROOF_TOP)])
# rough wall lumps
for s in (-1, 1):
    z = 0.6
    while z < 24.0:
        for row, y in enumerate((0.5, 2.0, 3.5, 4.9)):
            rr = b.r(0.75, 1.15)
            b.blob((s * b.r(4.85, 5.25), H(z) + y + b.r(-0.2, 0.2), z + b.r(-0.3, 0.3)), (rr * 0.8, rr * 0.95, rr * 1.1),
                   ROCK, subdiv=0, jitter=0.3, yaw=b.r(0, 6.28))
        z += 1.5
# rubble piles on the shelves
for s in (-1, 1):
    for z in (1.5, 4.8, 8.2, 11.5, 15.0, 18.3, 21.6):
        zz = z + b.r(-0.4, 0.4)
        b.blob((s * b.r(3.7, 4.4), H(zz) + 0.12, zz), (b.r(0.3, 0.5), b.r(0.2, 0.3), b.r(0.3, 0.5)), ROCK, subdiv=1, jitter=0.3, yaw=b.r(0, 6.28))
# stalactites
for k in range(14):
    z = b.r(1, 23)
    x = b.r(-4.5, 4.5)
    if abs(x) < 3.8 and k % 2:  # keep the runner envelope (>=4.3 m) clear
        x = math.copysign(b.r(4.0, 4.7), x)
    b.cone((x, H(z) + ROOF_BOT + 0.05, z), b.r(0.18, 0.32), (x, H(z) + ROOF_BOT - b.r(0.5, 0.9), z), ROCK, sides=5)

# --- timber supports (gangmok) every 6 m, same cadence as flat mine chunks -------------
lantern_pos = []
for z in (2.0, 8.0, 14.0, 20.0):
    h = H(z)
    for s in (-1, 1):
        b.box((s * 3.85, h + 2.35 - 0.15, z), (0.37, 4.7 + 0.3, 0.42), TIMBER)
        b.box((s * 3.85, h + 0.1, z), (0.5, 0.4, 0.5), IRON)
        b.box((s * 3.85, h + 3.25, z), (0.42, 0.16, 0.46), IRON)
        b.box((s * 3.85, h + 4.46, z), (0.43, 0.2, 0.47), IRON)
        b.beam((s * 3.85, h + 3.3, z), (s * 2.45, h + 4.75, z), 0.24, TIMBER)
    b.box((0, h + 4.72, z), (8.1, 0.36, 0.44), TIMBER)
    b.box((0, h + 5.05, z), (9.6, 0.3, 0.3), TIMBER)   # cap beam into the roof
    # hanging lanterns (geometry only)
    for x in (-2.3, 2.3):
        ly = h + 3.55
        b.box((x, h + 4.5, z), (0.03, 0.4, 0.03), IRON)      # chain
        b.cone((x, ly + 0.28, z), 0.22, (x, ly + 0.5, z), IRON, sides=4)  # cap
        b.box((x, ly + 0.05, z), (0.28, 0.4, 0.28), GLOW)    # glass
        b.box((x, ly - 0.17, z), (0.32, 0.05, 0.32), IRON)   # base
        lantern_pos.append((x, ly, z))

# --- timber scaffold handrails --------------------------------------------------------
for s in (-1, 1):
    x = s * 3.4
    for dy, w in ((1.0, 0.1), (0.5, 0.07)):
        b.ribbon(TIMBER, 0, 24, 24, lambda z, x=x, dy=dy, w=w: [(x - w / 2, H(z) + dy - w / 2), (x + w / 2, H(z) + dy - w / 2),
                                                               (x + w / 2, H(z) + dy + w / 2), (x - w / 2, H(z) + dy + w / 2)])
    z = 0.0
    while z <= 24.01:
        b.box((x, H(z) + 0.5, z), (0.13, 1.2, 0.13), TIMBER)
        z += 3.0

obj = b.build_object("MineStair", clamp=(5.8, 0.0, 24.0))
sc.stats(obj)
_ri = [H(i * DEPTH + DEPTH / 2 + DEPTH) - H(i * DEPTH + DEPTH / 2) for i in range(STEPS - 1)]
print("  riser min %.3f max %.3f m (rise %.1f m)" % (min(_ri), max(_ri), sc.RISE))
print("  steps=%d depth=%.2f m, max tread-to-curve error %.3f m" % (
    STEPS, DEPTH, max(abs(H(i * DEPTH + DEPTH / 2) - H(i * DEPTH)) for i in range(STEPS))))

blend_path = os.path.join(paths["blend_dir"], "MineStair.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)
print("SAVED", blend_path)
sc.export_fbx(obj, os.path.join(paths["fbx_dir"], "MineStair.fbx"))

if args:
    os.makedirs(args[0], exist_ok=True)
    sc.render_preview(os.path.join(args[0], "MineStair_preview.png"),
                      cam_loc_unity=(1.8, 3.2, -4.5), target_unity=(0, 2.5, 14),
                      lights_unity=[((x, y + 0.2, z), 400) for x, y, z in lantern_pos] + [((0, 3.0, -3), 500)],
                      sun=False, lens=22)
