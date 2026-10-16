# The crowd's kit (DECISIONS U13): one set of rigid parts that dresses every person on floor 1 (the
# players, the dealers, the bartender, the railbirds, the pianist), plus the seats the room doesn't have
# (a slot stool, a card player's chair, a piano bench) and what they hold (a glass, a cigarette). Run
# inside Blender:
#     exec(open(r'C:\Users\herna\Documents\the-vig-unity\art\people\build_crowd.py').read())
# It writes Assets/Vig/Art/People/Resources/People/Crowd.fbx; nothing in the .fbx is edited by hand.
#
# The joints and measures are the TS game's (../the-vig/src/render3d/npcs.ts: LEG, ARM, SHOULDER), so
# its poses carry over: a 1.72 m figure, the hips 0.90 up standing, shoulders at (±0.24, 0.53) above the
# hip joint, the neck at 0.58. Each part's origin is its joint, in the figure's frame (facing +z):
#   pelvis, skirt, torso_*, shirt_*, neckline   the torso joint (the hips' center)
#   head, hair_*, hat                           the neck, 0.58 above it
#   arm_upper                                   a shoulder; hangs down -y 0.30
#   arm_fore                                    the elbow; 0.27 to the wrist
#   hand                                        the wrist (palm in, thumb forward when hanging)
#   thigh                                       a hip (±0.09); 0.44 to the knee
#   shin                                        the knee; 0.40 to the ankle
#   shoe                                        the ankle; its sole 0.07 below
#   chair, stool, bench                         the floor under the seat's center (seat tops 0.55, 0.69, 0.54)
#   glass, cig                                  the bottom of the glass, the middle of the cigarette
# Roles (People.cs colors them per person): base (the part's own color), shade (a darker shade of it),
# dark (eyes, mouth, soles, a bow tie), accent (a tie, a hat band, the drink), skin (a neckline's V),
# metal (a seat's brass), ember (a cigarette's glowing tip).
import math, os

REPO = r'C:\Users\herna\Documents\the-vig-unity'
exec(open(os.path.join(REPO, 'art', 'blender_kit.py')).read())
OUT = os.path.join(REPO, 'Assets', 'Vig', 'Art', 'People', 'Resources', 'People', 'Crowd.fbx')

parts = []


def part(name):
    p = Part(name)
    parts.append(p)
    return p


# ------------------------------------------------------------------ the torso joint
p = part('pelvis')
p.add(loft([(-0.07, 0.13, 0.085), (-0.03, 0.16, 0.1), (0.06, 0.165, 0.102), (0.16, 0.158, 0.098)], 20, 'dome', 'flat', dome=0.04))

p = part('skirt')
p.add(loft([(0.16, 0.152, 0.097), (0.06, 0.188, 0.124), (-0.07, 0.222, 0.158), (-0.15, 0.236, 0.172)], 24, 'flat', 'flat'))
p.add(loft([(-0.15, 0.236, 0.172), (-0.165, 0.228, 0.165)], 24, 'flat', 'flat'), 'shade')

# a suit coat: square shoulders, a waist, the hem over the hips; lapels and two buttons in its shade
p = part('torso_coat')
p.add(loft([(0.05, 0.176, 0.112), (0.2, 0.16, 0.1), (0.37, 0.19, 0.115), (0.48, 0.232, 0.112), (0.55, 0.222, 0.1),
            (0.59, 0.17, 0.08), (0.61, 0.075, 0.06)], 24, 'dome', 'flat', power=2.3, dome=0.03))
for s in (-1, 1):
    p.add(xform(rbox((0, 0, 0), 0.05, 0.25, 0.014, 0.006), rot=(-8, 0, s * 13), at=(s * 0.058, 0.47, 0.104)), 'shade')
for k in range(2):
    p.add(ellipsoid((0.0, 0.25 + k * 0.07, 0.106), 0.009, 0.009, 0.006, 10, 6), 'shade')

# a dress's bodice: narrower shoulders, a smaller waist, a sash at the waist
p = part('torso_dress')
p.add(loft([(0.08, 0.152, 0.1), (0.21, 0.13, 0.088), (0.35, 0.163, 0.11), (0.47, 0.205, 0.1), (0.545, 0.19, 0.085),
            (0.585, 0.13, 0.065), (0.6, 0.062, 0.05)], 24, 'flat', 'flat', power=2.2))
p.add(loft([(0.17, 0.142, 0.096), (0.225, 0.134, 0.091)], 24, 'flat', 'flat'), 'shade')

# what shows at the front of a coat: a shirt and tie; a shirt and bow tie (the staff); a dress's neckline
p = part('shirt_tie')
p.add(prism([(0.104, 0.575), (0.112, 0.56), (0.118, 0.36), (0.112, 0.35), (0.106, 0.36)], -0.034, 0.034, 0.004))
p.add(prism([(0.121, 0.548), (0.124, 0.53), (0.126, 0.33), (0.121, 0.31), (0.118, 0.33)], -0.013, 0.013, 0.004), 'accent')
p.add(ellipsoid((0, 0.553, 0.118), 0.017, 0.014, 0.01, 12, 8), 'accent')

p = part('shirt_bow')
p.add(prism([(0.104, 0.575), (0.112, 0.56), (0.118, 0.38), (0.112, 0.37), (0.106, 0.38)], -0.036, 0.036, 0.004))
for s in (-1, 1):
    p.add(xform(ellipsoid((0, 0, 0), 0.022, 0.014, 0.008, 12, 8), rot=(0, 0, s * 12), at=(s * 0.022, 0.55, 0.12)), 'dark')
p.add(ellipsoid((0, 0.55, 0.124), 0.009, 0.01, 0.008, 10, 6), 'dark')

p = part('neckline')
p.add(prism([(0.094, 0.595), (0.103, 0.58), (0.108, 0.48), (0.1, 0.47), (0.095, 0.48)], -0.045, 0.045, 0.004))

# ------------------------------------------------------------------ the neck joint
p = part('head')
p.add(loft([(-0.07, 0.047, 0.05), (0.06, 0.046, 0.048)], 16, 'flat', 'flat'))
p.add(loft([(0.012, 0.05, 0.055, 0, 0.03), (0.05, 0.088, 0.09, 0, 0.012), (0.1, 0.108, 0.108), (0.16, 0.116, 0.114),
            (0.215, 0.108, 0.108)], 24, 'dome', 'dome', dome=0.05))
p.add(prism([(0.104, 0.158), (0.104, 0.11), (0.128, 0.112), (0.113, 0.15)], -0.014, 0.014, 0.006))
for s in (-1, 1):
    p.add(ellipsoid((s * 0.113, 0.14, -0.004), 0.014, 0.026, 0.018, 12, 8))
    p.add(ellipsoid((s * 0.044, 0.152, 0.101), 0.014, 0.011, 0.008, 12, 8), 'dark')
p.add(xform(capsule(0.04, 0.0045, 0.0045, 8), rot=(0, 0, 90), at=(0.02, 0.083, 0.1)), 'dark')


def shell(p, back=-0.004):
    """The hair's cap: lower at the back than at the front (a hairline over the forehead), and brows."""
    p.add(loft([(0.15, 0.125, 0.124, 0, back, 0.5), (0.2, 0.123, 0.122, 0, back, 0.25), (0.245, 0.1, 0.1, 0, back)], 24, 'flat', 'dome', dome=0.05))
    for s in (-1, 1):
        p.add(xform(capsule(0.042, 0.0065, 0.0065, 8), rot=(0, 0, s * 84), at=(s * 0.024, 0.178, 0.106)))


p = part('hair_short')
shell(p)

p = part('hair_long')
shell(p)
p.add(loft([(0.21, 0.118, 0.1, 0, -0.03), (0.12, 0.128, 0.105, 0, -0.035), (0.0, 0.122, 0.09, 0, -0.045), (-0.07, 0.1, 0.06, 0, -0.06)],
           20, 'flat', 'dome', dome=0.03))

p = part('hair_bun')
shell(p)
p.add(ellipsoid((0, 0.225, -0.12), 0.052, 0.05, 0.045, 16, 10))

p = part('hair_quiff')
shell(p)
p.add(xform(loft([(-0.06, 0.07, 0.04), (0.02, 0.075, 0.05), (0.08, 0.06, 0.035)], 16, 'dome', 'dome', axis='z', dome=0.03),
            rot=(-12, 0, 0), at=(0, 0.262, 0.04)))

# a fedora: a brim, a pinched crown, a band (the hair under it is short, so no hair part)
p = part('hat')
p.add(loft([(0.2, 0.178, 0.192, 0, -0.01), (0.212, 0.182, 0.196, 0, -0.01), (0.222, 0.172, 0.186, 0, -0.01)], 32, 'flat', 'flat'))
p.add(loft([(0.215, 0.122, 0.124), (0.26, 0.12, 0.122), (0.31, 0.108, 0.112), (0.335, 0.06, 0.08)], 28, 'flat', 'dome', dome=0.012))
p.add(loft([(0.222, 0.124, 0.126), (0.255, 0.123, 0.125)], 28, 'flat', 'flat'), 'accent')
for s in (-1, 1):
    p.add(xform(capsule(0.042, 0.0065, 0.0065, 8), rot=(0, 0, s * 84), at=(s * 0.024, 0.178, 0.106)), 'shade')

# ------------------------------------------------------------------ arms and legs
p = part('arm_upper')
p.add(capsule(0.3, 0.058, 0.05, 16))

p = part('arm_fore')
p.add(capsule(0.235, 0.049, 0.041, 16))
p.add(loft([(-0.22, 0.044, 0.044), (-0.262, 0.043, 0.043)], 16, 'flat', 'dome', dome=0.012), 'shade')

# a hand hanging from the wrist: palm toward the body (-x for a left hand: People.cs mirrors the right's
# scale), fingers together, the thumb forward
p = part('hand')
p.add(rbox((0, -0.05, 0.002), 0.032, 0.075, 0.07, 0.014))
p.add(rbox((0, -0.098, 0.0), 0.026, 0.045, 0.064, 0.012))
p.add(xform(capsule(0.055, 0.011, 0.009, 10), rot=(25, 0, 0), at=(0.002, -0.025, 0.035)))

p = part('thigh')
p.add(capsule(0.44, 0.072, 0.058, 16, rz0=0.078, rz1=0.062))

p = part('shin')
p.add(capsule(0.4, 0.056, 0.044, 16, rz0=0.06, rz1=0.046))

p = part('shoe')
p.add(loft([(-0.06, 0.045, 0.04, 0, -0.03), (0.05, 0.05, 0.036, 0, -0.04), (0.14, 0.04, 0.026, 0, -0.048), (0.17, 0.02, 0.016, 0, -0.054)],
           18, 'dome', 'dome', axis='z', dome=0.018))
p.add(rbox((0, -0.064, 0.05), 0.092, 0.014, 0.25, 0.006), 'dark')

# ------------------------------------------------------------------ seats and things held
# a card player's chair: a dark wood seat (top at 0.55), a curved back, brass legs
p = part('chair')
p.add(rbox((0, 0.525, 0), 0.44, 0.05, 0.42, 0.018))
p.add(xform(rbox((0, 0, 0), 0.4, 0.3, 0.04, 0.016), rot=(-8, 0, 0), at=(0, 0.83, -0.2)))
for sx in (-1, 1):
    p.add(xform(capsule(0.34, 0.012, 0.012, 8, down=False), rot=(-8, 0, 0), at=(sx * 0.17, 0.53, -0.19)), 'metal')
    for sz in (-1, 1):
        p.add(capsule(0.5, 0.016, 0.012, 10, at=(sx * 0.18, 0.5, sz * 0.17)), 'metal')

# a slot stool: a round velvet seat (top at 0.69) on a brass column and foot ring
p = part('stool')
p.add(lathe([(0.0, 0.64), (0.19, 0.64), (0.205, 0.665), (0.19, 0.69), (0.0, 0.692)], 32, None, None))
p.add(lathe([(0.2, 0.0), (0.2, 0.025), (0.05, 0.05), (0.035, 0.62), (0.07, 0.645), (0.0, 0.645)], 28, 'flat', None), 'metal')
p.add(loft([(0.3, 0.16, 0.16), (0.315, 0.16, 0.16)], 28, 'flat', 'flat'), 'metal')

# a piano bench: a padded top (0.54) on four black legs
p = part('bench')
p.add(rbox((0, 0.51, 0), 0.9, 0.06, 0.36, 0.02))
p.add(rbox((0, 0.46, 0), 0.86, 0.06, 0.32, 0.01), 'shade')
for sx in (-1, 1):
    for sz in (-1, 1):
        p.add(capsule(0.45, 0.022, 0.018, 10, at=(sx * 0.4, 0.45, sz * 0.13)), 'shade')

# a tumbler with a drink in it
p = part('glass')
p.add(lathe([(0.0, 0.0), (0.033, 0.0), (0.036, 0.012), (0.04, 0.1), (0.036, 0.1), (0.032, 0.014), (0.0, 0.014)], 20, None, None))
p.add(lathe([(0.0, 0.014), (0.032, 0.014), (0.035, 0.06), (0.0, 0.06)], 20, None, None), 'accent')

# a cigarette, its tip glowing
p = part('cig')
p.add(loft([(-0.035, 0.0045, 0.0045), (0.03, 0.0045, 0.0045)], 10, 'flat', 'flat', axis='z'))
p.add(loft([(0.03, 0.0047, 0.0047), (0.038, 0.0047, 0.0047)], 10, 'flat', 'dome', axis='z', dome=0.002), 'ember')

export_fbx(parts, 'crowd', OUT)
