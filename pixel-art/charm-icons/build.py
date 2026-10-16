# The charm icons (U33): one 32x32 picture per charm for the catalogue's cards, drawn pixel by pixel.
# One palette for all twenty, light from the top left, a dark outline outside every shape.
# Run: python build.py  ->  Assets/Vig/Resources/VigIcons/<id>.png (1x) and sheet.png (to look at).
import math, os, sys
sys.path.insert(0, os.path.expanduser("~/.claude/skills/pixel-art-studio/scripts"))
from pixelstudio import Sprite
from PIL import Image, ImageDraw

K = "#16101c"
G0, G1, G2, G3 = "#5a3a12", "#a8701c", "#e8b030", "#ffe27a"
R0, R1, R2, R3 = "#4a0f1e", "#a01f34", "#e8384f", "#ff8a8a"
N0, N1, N2, N3 = "#173d24", "#2f8a46", "#5fd36a", "#b6f58a"
B0, B1, B2, B3 = "#1d2552", "#3550b0", "#5aa0f0", "#a8f0ff"
W0, W1, W2, W3 = "#5a6478", "#94a4b8", "#d8e0e8", "#ffffff"
P1, P2 = "#7a2a8a", "#c050d0"
S1, S2 = "#c8805a", "#f0b48a"
PAL = [K, G0, G1, G2, G3, R0, R1, R2, R3, N0, N1, N2, N3, B0, B1, B2, B3, W0, W1, W2, W3, P1, P2, S1, S2]

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, "..", "..", "Assets", "Vig", "Resources", "VigIcons")


def new():
    return Sprite(32, 32, palette=PAL)


# ---------------------------------------------------------------- shared pieces

def card(s, x0, y0, x1, y1, face=W2, edge=W1):
    s.rect(x0, y0, x1, y1, face)
    s.line(x1, y0 + 1, x1, y1, edge)
    s.line(x0 + 1, y1, x1, y1, edge)
    s.line(x0 + 1, y0 + 1, x1 - 2, y0 + 1, W3)


def heart(s, cx, cy, c=R2, hi=R3):
    s.circle(cx - 2, cy, 2, c, fill=True)
    s.circle(cx + 2, cy, 2, c, fill=True)
    s.polygon([(cx - 4, cy + 1), (cx + 4, cy + 1), (cx, cy + 5)], c)
    s.px(cx - 3, cy - 1, hi)


def spade(s, cx, cy, c=K):
    s.polygon([(cx, cy - 4), (cx - 4, cy + 1), (cx + 4, cy + 1)], c)
    s.circle(cx - 2, cy + 1, 2, c, fill=True)
    s.circle(cx + 2, cy + 1, 2, c, fill=True)
    s.line(cx, cy + 2, cx, cy + 5, c)
    s.line(cx - 1, cy + 5, cx + 1, cy + 5, c)


PIPS = {1: [(1, 1)], 2: [(0, 0), (2, 2)], 3: [(0, 0), (1, 1), (2, 2)], 4: [(0, 0), (2, 0), (0, 2), (2, 2)],
        5: [(0, 0), (2, 0), (1, 1), (0, 2), (2, 2)], 6: [(0, 0), (0, 1), (0, 2), (2, 0), (2, 1), (2, 2)]}


def die(s, x0, y0, n, size=11, face=W2, pip=K):
    x1, y1 = x0 + size - 1, y0 + size - 1
    s.rect(x0, y0, x1, y1, face)
    s.line(x1, y0 + 1, x1, y1, W1)
    s.line(x0 + 1, y1, x1, y1, W1)
    s.line(x0 + 1, y0 + 1, x1 - 1, y0 + 1, W3)
    step = (size - 3) // 3 + 1
    for (i, j) in PIPS[n]:
        px, py = x0 + 2 + i * step, y0 + 2 + j * step
        s.rect(px, py, px + 1, py + 1, pip)


def bolt(s, pts, c=G3, shade=G2):
    s.polygon(pts, c)


def finish(s):
    s.outline(K, where="outside")
    return s


# ---------------------------------------------------------------- the icons

def luckyfuse():
    s = new()
    s.rect(11, 8, 20, 23, B3)                     # the glass
    s.rect(18, 8, 20, 23, B2)
    s.line(12, 9, 12, 22, W3)
    for y in range(9, 23):                         # the filament, a zigzag
        x = 15 + (1 if (y // 2) % 2 else -1)
        s.px(x, y, G2)
    for (dx, dy) in [(-2, 0), (2, 0), (0, -2), (0, 2)]:   # a clover in the glass
        s.circle(15 + dx, 15 + dy, 1, N2, fill=True)
    s.px(15, 15, N1)
    s.rect(10, 3, 21, 8, G2)                       # brass caps
    s.rect(10, 23, 21, 28, G2)
    s.line(10, 3, 21, 3, G3); s.line(10, 23, 21, 23, G3)
    s.line(21, 4, 21, 8, G1); s.line(21, 24, 21, 28, G1)
    s.line(10, 8, 21, 8, G1); s.line(10, 28, 21, 28, G1)
    return finish(s)


def overclock():
    s = new()
    s.circle(14, 15, 11, G1, fill=True)
    s.circle(14, 15, 9, W2, fill=True)
    s.circle(13, 14, 7, W3, fill=True)
    s.circle(14, 15, 9, W2, fill=False)
    for (x, y) in [(14, 7), (22, 15), (14, 23), (6, 15)]:
        s.px(x, y, K)
    s.line(14, 15, 14, 9, K); s.line(14, 15, 19, 15, K)
    s.px(14, 15, R2)
    s.polygon([(21, 13), (28, 13), (24, 19), (29, 19), (19, 30), (22, 22), (17, 22)], G3)
    s.line(24, 19, 21, 25, G2)
    return finish(s)


def leadapron():
    s = new()
    s.line(11, 8, 13, 3, W0); s.line(13, 3, 18, 3, W0); s.line(18, 3, 20, 8, W0)
    s.polygon([(10, 8), (21, 8), (22, 12), (25, 14), (25, 28), (6, 28), (6, 14), (9, 12)], W1)
    s.polygon([(19, 8), (21, 8), (22, 12), (25, 14), (25, 28), (21, 28)], W0)
    s.line(10, 9, 10, 12, W2); s.line(7, 15, 7, 26, W2)
    heart(s, 15, 17)
    s.line(9, 25, 21, 25, W0)
    return finish(s)


def magnet():
    s = new()
    s.circle(15, 13, 10, R2, fill=True)
    s.circle(15, 13, 4, None, fill=True)
    s.rect(5, 13, 25, 31, None)
    s.rect(5, 13, 10, 22, R2); s.rect(20, 13, 25, 22, R2)
    s.rect(11, 13, 19, 18, None)
    s.rect(5, 22, 10, 27, W2); s.rect(20, 22, 25, 27, W2)
    s.line(5, 22, 10, 22, W3); s.line(20, 22, 25, 22, W3)
    s.line(9, 4, 13, 3, R3); s.line(6, 7, 7, 6, R3)
    s.line(24, 9, 25, 21, R1); s.line(10, 14, 10, 21, R1)
    s.circle(15, 26, 3, B2, fill=True); s.px(14, 25, B3)    # a marker, pulled up
    s.px(15, 21, B3); s.px(12, 22, B3); s.px(18, 22, B3)
    return finish(s)


def insulated():
    s = new()
    for i, x in enumerate([9, 12, 15, 18]):
        top = [7, 5, 5, 7][i]
        s.rect(x, top + 1, x + 2, 15, G2)
        s.line(x, top + 1, x + 2, top + 1, G3)
        s.px(x + 2, top + 2, G1)
    s.ellipse(8, 11, 22, 25, G2)
    s.ellipse(3, 13, 10, 19, G2)
    s.line(4, 14, 7, 13, G3)
    s.line(21, 14, 21, 23, G1)
    for x in [11, 14, 17]:                          # the fingers part
        s.line(x + 1, 8, x + 1, 13, G1)
    s.rect(9, 24, 21, 29, G1)
    s.line(9, 26, 21, 26, R2)
    s.line(25, 6, 28, 3, B3); s.line(26, 10, 30, 9, B3)   # the spark it shrugs off
    return finish(s)


def flame(s, cx, base, h, w):
    """A teardrop flame, round at the base and leaning as it licks up; three layers, hottest inside."""
    for c, ks, kh in [(R2, 1.0, 1.0), (G2, 0.62, 0.72), (G3, 0.32, 0.42)]:
        hh = h * kh
        for y in range(int(base - hh), base + 1):
            t = (base - y) / hh
            if t <= 0.3:
                hw = w * ks * math.sqrt(max(0.0, 1 - ((0.3 - t) / 0.3) ** 2))
            else:
                hw = w * ks * (max(0.0, 1 - t) / 0.7) ** 1.1
            lean = math.sin(t * 4.2) * 1.6 * t
            x0, x1 = cx + lean - hw, cx + lean + hw
            for x in range(int(math.floor(x0 + 0.5)), int(math.floor(x1 + 0.5)) + 1):
                s.px(x, y, c)


def hothand():
    s = new()
    flame(s, 16, 18, 16, 9)
    for x in [9, 12, 15, 18]:                      # the hand under the fire
        s.rect(x, 16, x + 2, 22, S2)
    s.ellipse(8, 19, 22, 30, S2)
    s.ellipse(3, 21, 10, 26, S2)
    s.line(21, 21, 21, 28, S1); s.line(9, 29, 20, 29, S1)
    return finish(s)


def digit(s, d, x, y, c):
    glyphs = {"4": ["1.1", "1.1", "111", "..1", "..1"], "2": ["111", "..1", "111", "1..", "111"],
              "x": ["...", "1.1", ".1.", "1.1", "..."], "1": [".1.", "11.", ".1.", ".1.", "111"],
              "+": ["...", ".1.", "111", ".1.", "..."], "7": ["111", "..1", ".1.", ".1.", ".1."]}
    for j, row in enumerate(glyphs[d]):
        for i, ch in enumerate(row):
            if ch == "1":
                s.px(x + i, y + j, c)


def redrover():
    s = new()
    card(s, 5, 3, 21, 26)
    heart(s, 13, 12)
    digit(s, "7", 7, 5, R2)
    s.circle(23, 23, 6, G2, fill=True)
    s.circle(22, 22, 4, G3, fill=False)
    digit(s, "4", 20, 21, K); digit(s, "x", 24, 21, K)
    return finish(s)


def counter():
    s = new()
    card(s, 3, 3, 17, 23, face=B1, edge=B0)
    for y in range(6, 21, 3):
        s.line(6, y, 14, y, B2)
    card(s, 11, 7, 27, 29)
    spade(s, 19, 17)
    digit(s, "1", 13, 9, K)
    s.circle(7, 26, 4, G2, fill=True)
    digit(s, "+", 5, 24, K)
    return finish(s)


def snake():
    s = new()
    s.ellipse(4, 17, 26, 29, N1)
    s.ellipse(9, 20, 21, 26, None)
    s.ellipse(4, 17, 26, 22, N2, only=N1)
    s.rect(18, 9, 23, 19, N1)
    s.line(18, 10, 18, 18, N2)
    s.ellipse(17, 4, 27, 12, N2)
    s.px(21, 7, W3); s.px(24, 7, W3); s.px(22, 7, K)
    s.line(27, 9, 30, 9, R2); s.px(31, 8, R2); s.px(31, 10, R2)
    s.line(5, 24, 8, 27, N0); s.line(24, 25, 21, 28, N0)
    die(s, 2, 2, 1, 8, pip=R2)                     # snake eyes
    die(s, 10, 6, 1, 8, pip=R2)
    return finish(s)


def interest():
    s = new()
    for i in range(4):
        y = 22 - i * 4
        s.ellipse(3, y, 17, y + 6, G1)
        s.ellipse(3, y, 17, y + 4, G2)
        s.line(6, y + 1, 12, y + 1, G3)
    s.rect(21, 13, 25, 28, N1)
    s.polygon([(16, 14), (23, 4), (30, 14)], N2)
    s.line(21, 13, 21, 28, N2)
    return finish(s)


def comp():
    s = new()
    s.polygon([(5, 6), (26, 6), (16, 18), (15, 18)], B3)
    s.polygon([(7, 8), (24, 8), (15, 17)], B2)
    s.line(5, 6, 26, 6, W3)
    s.rect(15, 18, 16, 25, W2)
    s.ellipse(9, 25, 22, 29, W2)
    s.line(10, 27, 21, 27, W1)
    s.line(9, 3, 14, 10, G1)                       # the pick and its olive
    s.circle(13, 10, 2, N1, fill=True); s.px(12, 9, N3)
    return finish(s)


def seven():
    s = new()
    s.polygon([(5, 4), (27, 4), (27, 10), (17, 29), (10, 29), (19, 10), (5, 10)], R2)
    s.polygon([(22, 10), (27, 10), (17, 29), (14, 29)], R1)
    s.line(6, 5, 25, 5, R3)
    s.px(3, 16, G3); s.px(2, 16, G2); s.px(3, 15, G2)
    s.px(26, 22, G3); s.px(27, 22, G2); s.px(26, 23, G2)
    return finish(s)


def foot():
    s = new()
    s.circle(15, 4, 3, G2, fill=False)
    s.rect(12, 7, 18, 10, G1); s.line(12, 7, 18, 7, G3)
    s.ellipse(10, 9, 20, 25, W2)                    # the leg
    s.ellipse(9, 19, 24, 30, W2)                    # the paw, wider, a little forward
    s.ellipse(10, 9, 16, 22, W3, only=W2)
    s.line(19, 12, 19, 22, W1); s.line(23, 23, 23, 27, W1)
    for x in [14, 18]:                              # the toes
        s.line(x, 27, x, 30, W1)
    s.noise(11, 11, 21, 26, W1, density=0.06, seed=3, only=W2)
    s.px(13, 30, R3); s.px(16, 30, R3); s.px(20, 30, R3)
    return finish(s)


def chute():
    s = new()
    for y in range(3, 15):
        for x in range(3, 29):
            dx, dy = (x - 15.5) / 12.5, (y - 15) / 12
            if dx * dx + dy * dy <= 1:
                band = int((x - 3) / 26 * 5)
                s.px(x, y, R2 if band % 2 == 0 else W3)
    for cx in [6, 11, 16, 21, 26]:                  # scalloped hem
        s.px(cx, 15, R1 if cx % 2 == 0 else W1)
    s.line(4, 15, 14, 25, W1); s.line(27, 15, 18, 25, W1); s.line(15, 15, 15, 24, W1)
    s.rect(13, 24, 18, 29, G1); s.line(13, 24, 18, 24, G2)
    s.line(5, 6, 9, 4, R3)
    return finish(s)


def thumb():
    s = new()
    cx, cy = 15.5, 15.5
    for y in range(32):
        for x in range(32):
            r = math.hypot(x - cx, y - cy)
            if r <= 13.5:
                if r > 11.5:
                    s.px(x, y, G1 if y > cy else G2)
                elif r > 6.5:
                    a = (math.atan2(y - cy, x - cx) + math.pi) / (2 * math.pi) * 37
                    k = int(a)
                    s.px(x, y, N1 if k == 0 else (R2 if k % 2 else K))
                elif r > 3.5:
                    s.px(x, y, G0)
                else:
                    s.px(x, y, G2 if r > 1.5 else G3)
    s.circle(15, 6, 1, W3, fill=True)              # the ball
    return finish(s)


def centerpin():
    s = new()
    s.rect(3, 2, 28, 30, B0)
    for r in range(6):
        y = 4 + r * 3
        for i in range(r + 1):
            x = 15 - 2 * r + 4 * i
            s.rect(x, y, x + 1, y + 1, W2)
    for i, x in enumerate(range(4, 28, 4)):         # the slots, the center lit
        s.rect(x, 25, x + 3, 29, G2 if i == 3 else B1)
        s.line(x, 25, x, 29, K)
    s.circle(16, 22, 1, W3, fill=True)
    s.px(16, 20, B3); s.px(16, 18, B2)
    return finish(s)


def shooter():
    s = new()
    flame(s, 16, 16, 15, 11)
    die(s, 3, 14, 2, 12)
    die(s, 16, 17, 1, 12)
    return finish(s)


def vision():
    s = new()
    card(s, 2, 5, 15, 26)
    heart(s, 8, 13)
    card(s, 16, 5, 29, 26)
    heart(s, 22, 13)
    s.rect(13, 27, 18, 28, G2); s.rect(13, 30, 18, 31, G2)   # =
    s.px(8, 21, R2); s.px(22, 21, R2)
    return finish(s)


def company():
    s = new()
    for (x, y, body) in [(6, 15, B1), (16, 12, P1), (26, 15, N1)]:
        s.ellipse(x - 5, y + 5, x + 5, y + 18, body)
        s.circle(x, y, 4, S2, fill=True)
        s.px(x - 2, y - 2, W3)
        s.px(x - 1, y + 1, K); s.px(x + 1, y + 1, K)
        s.line(x - 1, y + 2, x + 1, y + 2, R1)
    s.px(16, 4, G3); s.px(15, 3, G2); s.px(17, 3, G2); s.px(16, 2, G3)
    return finish(s)


def tailwind():
    s = new()
    s.ellipse(12, 4, 19, 23, W2)
    s.ellipse(12, 4, 16, 23, W3, only=W2)
    s.polygon([(12, 8), (19, 8), (15, 2), (16, 2)], R2)
    s.circle(15, 12, 2, B2, fill=True); s.px(14, 11, B3)
    s.polygon([(12, 16), (8, 24), (12, 22)], R1)
    s.polygon([(19, 16), (23, 24), (19, 22)], R1)
    flame(s, 16, 31, 9, 3)
    for (y, x0, x1) in [(8, 2, 8), (14, 3, 9), (20, 1, 7), (11, 24, 29), (18, 25, 30)]:
        s.line(x0, y, x1, y, B3)
    return finish(s)


ICONS = {
    "luckyfuse": luckyfuse, "overclock": overclock, "leadapron": leadapron, "magnet": magnet, "insulated": insulated,
    "hothand": hothand, "redrover": redrover, "counter": counter, "snake": snake, "interest": interest,
    "comp": comp, "seven": seven, "foot": foot, "chute": chute, "thumb": thumb,
    "centerpin": centerpin, "shooter": shooter, "vision": vision, "company": company, "tailwind": tailwind,
}

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    scale, cols = 6, 5
    sheet = Image.new("RGBA", (cols * (32 * scale + 12), ((len(ICONS) + cols - 1) // cols) * (32 * scale + 34)), (30, 26, 36, 255))
    d = ImageDraw.Draw(sheet)
    for i, (name, fn) in enumerate(ICONS.items()):
        s = fn()
        path = os.path.join(OUT, name + ".png")
        s.save_png(path)
        im = Image.open(path).convert("RGBA").resize((32 * scale, 32 * scale), Image.NEAREST)
        x, y = (i % cols) * (32 * scale + 12) + 6, (i // cols) * (32 * scale + 34) + 4
        sheet.alpha_composite(im, (x, y))
        d.text((x, y + 32 * scale + 6), name, fill=(220, 220, 230, 255))
    sheet.save(os.path.join(ROOT, "sheet.png"))
    print(f"{len(ICONS)} icons -> {os.path.normpath(OUT)}")
