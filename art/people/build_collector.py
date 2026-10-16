# The Collector (DECISIONS U13): a tall gaunt figure in a long dark coat and a wide hat, seated in his
# high-backed chair across his round table, eyes burning the violet of his tier. Run inside Blender:
#     exec(open(r'C:\Users\herna\Documents\the-vig-unity\art\people\build_collector.py').read())
# It writes Assets/Vig/Art/People/Resources/People/Collector.fbx; nothing in the .fbx is edited by hand.
#
# Proportions after the TS game's coll_* parts (../the-vig/art/people/build_people.py): shoulders at
# (±0.36, 0.64) above the hips, the neck at 0.80, a long narrow head widening at the temples, long arms
# and long pale fingers. Unlike the TS one he has legs (Unity's camera sees under his table) and his arm
# is two parts, so People.cs can bend an elbow to rest a hand on the felt or reach for a lever.
#
# Joints (each part's origin), in his own frame (facing +z; People.cs turns him to face the player):
#   coll_pelvis, coll_torso   the hips' center (the torso turns there to lean)
#   coll_head, coll_hat       the neck, 0.80 above the hips
#   coll_upper                a shoulder (±0.36, 0.64); hangs down -y
#   coll_fore                 the elbow, 0.36 below the shoulder
#   coll_hand                 the wrist, 0.33 below the elbow
#   coll_thigh                a hip (±0.11, 0); hangs down -y (seated, People.cs swings it forward)
#   coll_shin                 the knee, 0.50 below the hip
#   coll_shoe                 the ankle, 0.50 below the knee
# Roles: base (his coat; the head's and hat's own dark), dark (sockets, cuffs, soles), accent (the violet
# waistcoat and hat band), eye (the glowing slits: neon in Unity), pale (his skin: hands).
import math, os

REPO = r'C:\Users\herna\Documents\the-vig-unity'
exec(open(os.path.join(REPO, 'art', 'blender_kit.py')).read())
OUT = os.path.join(REPO, 'Assets', 'Vig', 'Art', 'People', 'Resources', 'People', 'Collector.fbx')

parts = []


def part(name):
    p = Part(name)
    parts.append(p)
    return p


def band(y0, y1, r0, r1, thick, a0, a1, n=20, cz=0.0):
    """A thick open band round the y axis (a stand-up collar): from angle a0 to a1 (degrees, 0 = +z,
    the front), radius r0 at y0 flaring to r1 at y1."""
    bm = _new()
    rows = []
    for y, r in ((y0, r0), (y1, r1)):
        for rr in (r, r - thick):
            row = []
            for k in range(n + 1):
                a = math.radians(a0 + (a1 - a0) * k / n)
                row.append(bm.verts.new((math.sin(a) * rr, y, cz + math.cos(a) * rr)))
            rows.append(row)
    lo_o, lo_i, hi_o, hi_i = rows
    for k in range(n):
        bm.faces.new((lo_o[k], lo_o[k + 1], hi_o[k + 1], hi_o[k]))
        bm.faces.new((lo_i[k], hi_i[k], hi_i[k + 1], lo_i[k + 1]))
        bm.faces.new((hi_o[k], hi_o[k + 1], hi_i[k + 1], hi_i[k]))
        bm.faces.new((lo_o[k], lo_i[k], lo_i[k + 1], lo_o[k + 1]))
    for k in (0, n):
        bm.faces.new((lo_o[k], hi_o[k], hi_i[k], lo_i[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return bm


# ------------------------------------------------------------------ the body
p = part('coll_pelvis')
p.add(loft([(-0.09, 0.17, 0.12), (-0.02, 0.215, 0.14), (0.1, 0.21, 0.135), (0.16, 0.2, 0.13)], 20, 'dome', 'flat', dome=0.05))

# a long frock coat: a narrow waist, square high shoulders, closing to the neck
p = part('coll_torso')
p.add(loft([(0.0, 0.215, 0.14), (0.12, 0.2, 0.13), (0.26, 0.205, 0.125), (0.44, 0.27, 0.14), (0.58, 0.32, 0.14),
            (0.66, 0.31, 0.13), (0.72, 0.22, 0.11), (0.78, 0.085, 0.07)], 24, 'flat', 'flat', power=2.3))
# the coat's skirt over his lap (he sits: his thighs run forward along +z at the hips' height), split at
# the front
for s in (-1, 1):
    p.add(xform(rbox((0, 0, 0), 0.2, 0.035, 0.36, 0.015), rot=(0, 0, s * -10), at=(s * 0.11, 0.1, 0.16)))
# a stand-up collar round his throat, open at the front
p.add(band(0.66, 0.93, 0.115, 0.16, 0.018, 38, 322, 22, cz=-0.01))
# the waistcoat's V and its buttons
p.add(prism([(0.142, 0.64), (0.155, 0.26), (0.133, 0.22), (0.118, 0.26), (0.126, 0.64)], -0.07, 0.07, 0.006), 'accent')
for k in range(4):
    p.add(ellipsoid((0, 0.3 + k * 0.085, 0.152), 0.012, 0.012, 0.008, 12, 8), 'dark')
# lapels: two long dark folds either side of the waistcoat
for s in (-1, 1):
    p.add(xform(rbox((0, 0, 0), 0.07, 0.42, 0.02, 0.008), rot=(-6, 0, s * 9), at=(s * 0.1, 0.48, 0.135)), 'dark')

# the head: a neck, then a long skull widening at the temples, a long nose, deep sockets, the eyes
p = part('coll_head')
p.add(loft([(-0.08, 0.05, 0.05), (0.08, 0.048, 0.05)], 14, 'flat', 'flat'))
p.add(loft([(0.0, 0.055, 0.065, 0, -0.03), (0.05, 0.095, 0.105, 0, -0.012), (0.13, 0.108, 0.118), (0.21, 0.142, 0.135),
            (0.29, 0.138, 0.128), (0.34, 0.1, 0.1)], 24, 'dome', 'dome', dome=0.03))
p.add(prism([(0.115, 0.215), (0.122, 0.13), (0.168, 0.118), (0.13, 0.2)], -0.015, 0.015, 0.006))
p.add(ellipsoid((0, 0.065, 0.1), 0.045, 0.006, 0.01, 14, 6), 'dark')
for s in (-1, 1):
    p.add(ellipsoid((s * 0.068, 0.2, 0.112), 0.05, 0.03, 0.025, 18, 10), 'dark')
    p.add(xform(rbox((0, 0, 0), 0.07, 0.022, 0.012, 0.006), rot=(0, 0, s * 8), at=(s * 0.07, 0.2, 0.132)), 'eye')

# a wide-brimmed hat with a tall crown and a violet band
p = part('coll_hat')
p.add(lathe([(0.0, 0.312), (0.27, 0.318), (0.282, 0.328), (0.276, 0.338), (0.2, 0.336), (0.0, 0.34)], 40, None, None))
p.add(loft([(0.33, 0.145, 0.14), (0.52, 0.15, 0.145), (0.6, 0.152, 0.147), (0.625, 0.135, 0.13)], 36, 'flat', 'dome', dome=0.025))
p.add(loft([(0.338, 0.149, 0.144), (0.395, 0.152, 0.147)], 36, 'flat', 'flat'), 'accent')

# ------------------------------------------------------------------ the limbs
p = part('coll_upper')
p.add(capsule(0.46, 0.072, 0.058, 18))

p = part('coll_fore')
p.add(capsule(0.42, 0.058, 0.046, 18))
p.add(loft([(-0.37, 0.054, 0.054), (-0.415, 0.056, 0.056)], 18, 'flat', 'flat'), 'dark')

# a long hand, palm toward the body when hanging (-x for the right hand: People.cs mirrors nothing, the
# hand is symmetric), long fingers curling at the tips
p = part('coll_hand')
p.add(rbox((0, -0.06, 0), 0.03, 0.12, 0.09, 0.012), 'pale')
for k, (z, ln) in enumerate(((0.034, 0.1), (0.012, 0.13), (-0.011, 0.125), (-0.032, 0.095))):
    p.add(xform(capsule(ln, 0.009, 0.0075, 10), rot=(0, 0, 0), at=(0, -0.11, z)), 'pale')
    p.add(xform(capsule(0.035, 0.0075, 0.0065, 10), rot=(0, 0, -35), at=(0, -0.11 - ln, z)), 'pale')
p.add(xform(capsule(0.075, 0.011, 0.008, 10), rot=(-30, 0, -20), at=(0.006, -0.02, 0.04)), 'pale')

p = part('coll_thigh')
p.add(capsule(0.5, 0.088, 0.07, 18))

p = part('coll_shin')
p.add(capsule(0.5, 0.068, 0.052, 18))

p = part('coll_shoe')
# a long pointed shoe along +z under the ankle, on a dark sole
p.add(loft([(-0.07, 0.05, 0.045, 0, -0.035), (0.05, 0.055, 0.042, 0, -0.04), (0.16, 0.042, 0.03, 0, -0.05), (0.23, 0.012, 0.014, 0, -0.06)],
           18, 'dome', 'dome', axis='z', dome=0.02))
p.add(rbox((0, -0.08, 0.06), 0.1, 0.016, 0.27, 0.006), 'dark')

export_fbx(parts, 'collector', OUT)
