# The shared kit for The Vig's Blender-built models in the Unity project (DECISIONS U13). Every model is a
# re-runnable script under art/<family>/build_*.py that execs this file first, run inside Blender
# (5.2, through the Blender MCP):
#     exec(open(r'<repo>/art/people/build_collector.py').read())
#
# The look is U2's clean stylized neon, not the TS game's pixel pass: smooth lofts with enough sides to
# read round at full resolution, bevelled boxes, smooth shading with weighted normals so the flat faces of
# a bevelled box stay flat. No textures: Unity gives every part a URP Lit material by its role.
#
# Coordinates: every shape is written in UNITY's frame (meters, y up, the figure facing +z, its right
# hand at +x), so the numbers here are the numbers People.cs uses. `build` turns them into Blender's frame
# and `export_fbx` turns them back, so Unity imports exactly what was written (checked: a part's bounds
# in Unity match the script's). Each part's origin is the joint it hangs from (a forearm at the elbow, a
# head at the neck), so People.cs places a part at its joint with no offset.
#
# A part is split by ROLE into meshes named `<part>` (the role 'base', the part's own color) and
# `<part>__<role>` (e.g. 'dark' for eyes, 'accent' for a tie, 'eye' for the Collector's glowing eyes);
# People.cs gives each role its material.
import bpy, bmesh, math, os
from mathutils import Matrix, Vector

# Unity (x, y, z) -> Blender (-x, -z, y); the FBX export (-Z forward, Y up, baked) and Unity's importer
# (which mirrors x) turn it back
TO_B = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))


def _new():
    return bmesh.new()


def _ring_pts(sides, ra, rb, ca=0.0, cb=0.0, spin=None, power=2.0):
    """Points round an ellipse (or a superellipse, power > 2 squares it off) in the (a, b) plane."""
    spin = math.pi / sides if spin is None else spin
    out = []
    for k in range(sides):
        t = spin + 2 * math.pi * k / sides
        s, c = math.sin(t), math.cos(t)
        if power != 2.0:
            s = math.copysign(abs(s) ** (2 / power), s)
            c = math.copysign(abs(c) ** (2 / power), c)
        out.append((ca + ra * s, cb + rb * c))
    return out


def loft(rings, sides=16, cap0='flat', cap1='flat', axis='y', spin=None, power=2.0, dome=None):
    """A smooth shell through rings along an axis. axis 'y': a ring is (y, rx, rz[, cx, cz[, tilt]]), the
    ring lying in x/z (`tilt` lifts its front, +z, and drops its back: a hairline). axis 'z': (z, rx, ry[,
    cx, cy]). Caps: 'flat' (a fan to a center point), 'dome' (a rounded end, `dome` deep, default the
    ring's smaller radius), or None (open; avoid: Unity draws back faces culled)."""
    bm = _new()
    rows = []
    for r in rings:
        h, ra, rb = r[0], r[1], r[2]
        ca = r[3] if len(r) > 3 else 0.0
        cb = r[4] if len(r) > 4 else 0.0
        tilt = r[5] if len(r) > 5 else 0.0
        row = []
        for a, b in _ring_pts(sides, ra, rb, ca, cb, spin, power):
            if axis == 'y':
                row.append(bm.verts.new((a, h + tilt * (b - cb), b)))
            else:
                row.append(bm.verts.new((a, b, h)))
        rows.append((row, r))
    for j in range(len(rows) - 1):
        a, b = rows[j][0], rows[j + 1][0]
        for k in range(sides):
            k2 = (k + 1) % sides
            bm.faces.new((a[k], a[k2], b[k2], b[k]))

    def cap(row, r, sign, kind):
        h, ra, rb = r[0], r[1], r[2]
        ca = r[3] if len(r) > 3 else 0.0
        cb = r[4] if len(r) > 4 else 0.0
        if kind is None:
            return
        if kind == 'flat':
            c = sum((v.co for v in row), Vector()) / len(row)
            cv = bm.verts.new(c)
            for k in range(sides):
                bm.faces.new((row[k], row[(k + 1) % sides], cv))
            return
        depth = dome if dome is not None else min(ra, rb)
        steps = 4
        prev = row
        base = [v.co.copy() for v in row]
        if axis == 'y':
            ctr = Vector((ca, sum(p.y for p in base) / len(base), cb))
            ax = Vector((0, sign, 0))
        else:
            ctr = Vector((ca, cb, h))
            ax = Vector((0, 0, sign))
        for s in range(1, steps):
            t = s / steps * math.pi / 2
            k = math.cos(t)
            cur = [bm.verts.new(ctr + (p - ctr) * k + ax * depth * math.sin(t)) for p in base]
            for i in range(sides):
                i2 = (i + 1) % sides
                bm.faces.new((prev[i], prev[i2], cur[i2], cur[i]))
            prev = cur
        tip = bm.verts.new(ctr + ax * depth)
        for i in range(sides):
            bm.faces.new((prev[i], prev[(i + 1) % sides], tip))

    up = rings[-1][0] >= rings[0][0]
    cap(rows[0][0], rows[0][1], -1 if up else 1, cap0)
    cap(rows[-1][0], rows[-1][1], 1 if up else -1, cap1)
    return bm


def lathe(profile, sides=24, cap0='flat', cap1='flat'):
    """A profile [(r, y)...] turned round the y axis."""
    return loft([(y, r, r) for r, y in profile], sides, cap0, cap1)


def capsule(length, r0, r1=None, sides=14, down=True, at=(0, 0, 0), rz0=None, rz1=None):
    """A limb: from the joint (at) down -y (or up) `length`, radius r0 tapering to r1, rounded both ends
    (the rounded top is what lets a rigid part turn at its joint without a gap showing)."""
    r1 = r0 if r1 is None else r1
    rz0 = r0 if rz0 is None else rz0
    rz1 = r1 if rz1 is None else rz1
    s = -1 if down else 1
    x, y, z = at
    bm = loft([(y, r0, rz0, x, z), (y + s * length * 0.5, (r0 + r1) / 2, (rz0 + rz1) / 2, x, z), (y + s * length, r1, rz1, x, z)],
              sides, 'dome', 'dome')
    return bm


def ellipsoid(c, rx, ry, rz, seg=20, rings=12):
    bm = _new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0)
    bmesh.ops.scale(bm, vec=(rx, ry, rz), verts=bm.verts)
    bmesh.ops.translate(bm, vec=c, verts=bm.verts)
    return bm


def rbox(c, sx, sy, sz, r=0.01, seg=3):
    """A box with rounded edges, centered at c."""
    bm = _new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(sx, sy, sz), verts=bm.verts)
    r = min(r, sx / 2 - 1e-4, sy / 2 - 1e-4, sz / 2 - 1e-4)
    if r > 1e-4:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=r, segments=seg, profile=0.5, affect='EDGES', clamp_overlap=True)
    bmesh.ops.translate(bm, vec=c, verts=bm.verts)
    return bm


def prism(profile, x0, x1, r=0.0, seg=2):
    """A side profile [(z, y)...] (convex, going round) pushed along x from x0 to x1, edges rounded r."""
    bm = _new()
    a = [bm.verts.new((x0, y, z)) for z, y in profile]
    b = [bm.verts.new((x1, y, z)) for z, y in profile]
    bm.faces.new(a)
    bm.faces.new(b)
    n = len(profile)
    for k in range(n):
        k2 = (k + 1) % n
        bm.faces.new((a[k], a[k2], b[k2], b[k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if r > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=r, segments=seg, profile=0.5, affect='EDGES', clamp_overlap=True)
    return bm


def xform(bm, rot=None, at=None, scale=None):
    """Turn a piece (rot: (rx, ry, rz) degrees, Unity's order: z, then x, then y), scale it, move it."""
    if scale is not None:
        bmesh.ops.scale(bm, vec=scale, verts=bm.verts)
    if rot is not None:
        rx, ry, rz = (math.radians(v) for v in rot)
        # Quaternion.Euler(x, y, z)'s matrix is Ry·Rx·Rz with the usual matrices (Unity's left-handedness
        # is in how it draws them, not in the numbers), so a part turned here turns as it would in Unity
        m = Matrix.Rotation(ry, 4, 'Y') @ Matrix.Rotation(rx, 4, 'X') @ Matrix.Rotation(rz, 4, 'Z')
        bmesh.ops.transform(bm, matrix=m, verts=bm.verts)
    if at is not None:
        bmesh.ops.translate(bm, vec=at, verts=bm.verts)
    return bm


def mirror_x(bm):
    """A copy of a piece mirrored left to right."""
    m = bm.copy()
    bmesh.ops.scale(m, vec=(-1, 1, 1), verts=m.verts)
    bmesh.ops.reverse_faces(m, faces=m.faces)
    return m


class Part:
    """A model part: pieces by role, all in the joint's frame (Unity coordinates)."""

    def __init__(self, name):
        self.name = name
        self.pieces = {}

    def add(self, bm, role='base'):
        self.pieces.setdefault(role, []).append(bm)
        return bm

    def both(self, bm, role='base'):
        """A piece and its mirror (a left and a right eye)."""
        self.add(bm, role)
        self.add(mirror_x(bm), role)

    def build(self, collection):
        objs = []
        for role, bms in self.pieces.items():
            name = self.name if role == 'base' else f'{self.name}__{role}'
            me = bpy.data.meshes.new(name)
            acc = bmesh.new()
            for bm in bms:
                tmp = bpy.data.meshes.new('_tmp')
                bm.to_mesh(tmp)
                acc.from_mesh(tmp)
                bpy.data.meshes.remove(tmp)
            bmesh.ops.remove_doubles(acc, verts=acc.verts, dist=1e-6)
            bmesh.ops.recalc_face_normals(acc, faces=acc.faces)
            bmesh.ops.transform(acc, matrix=TO_B, verts=acc.verts)
            # the x mirror inside TO_B turned every face inside out: turn them back
            bmesh.ops.reverse_faces(acc, faces=acc.faces)
            acc.to_mesh(me)
            acc.free()
            for poly in me.polygons:
                poly.use_smooth = True
            ob = bpy.data.objects.new(name, me)
            collection.objects.link(ob)
            wn = ob.modifiers.new('weighted', 'WEIGHTED_NORMAL')
            wn.mode = 'FACE_AREA'
            wn.weight = 50
            wn.keep_sharp = False
            objs.append(ob)
        return objs

    def tris(self):
        n = 0
        for bms in self.pieces.values():
            for bm in bms:
                n += sum(len(f.verts) - 2 for f in bm.faces)
        return n


def fresh_collection(name):
    """An empty collection for one family (other families' objects in the file are left alone)."""
    col = bpy.data.collections.get(name)
    if col:
        for ob in list(col.objects):
            me = ob.data
            bpy.data.objects.remove(ob, do_unlink=True)
            if me and me.users == 0:
                bpy.data.meshes.remove(me)
    else:
        col = bpy.data.collections.new(name)
        bpy.context.scene.collection.children.link(col)
    # hidden objects can't be selected, and the exporter takes the selection
    col.hide_viewport = False
    col.hide_select = False
    lc = bpy.context.view_layer.layer_collection.children.get(name)
    if lc:
        lc.exclude = False
        lc.hide_viewport = False
    return col


def export_fbx(parts, family, out):
    """Build the parts into the family's collection and write it to `out` (.fbx)."""
    col = fresh_collection(family)
    objs = []
    for p in parts:
        objs += p.build(col)
    os.makedirs(os.path.dirname(out), exist_ok=True)
    for ob in bpy.data.objects:
        if ob.name in bpy.context.view_layer.objects:
            ob.select_set(False)
    for ob in objs:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.fbx(
        filepath=out, use_selection=True, object_types={'MESH'}, use_mesh_modifiers=True,
        mesh_smooth_type='FACE', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', add_leaf_bones=False,
        use_tspace=False, bake_anim=False, path_mode='STRIP', embed_textures=False,
    )
    total = 0
    for p in parts:
        t = p.tris()
        total += t
        print(f'{p.name:16s} {t:6d} tris')
    print('total', total, '->', out, os.path.getsize(out), 'bytes')
    return objs
