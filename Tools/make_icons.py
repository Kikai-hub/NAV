"""Placeholder inventory/building icons for NAV: 128x128 PNG, transparent background.

Style: flat-shaded shapes with a top-light gradient, dark outline, small highlights and a
soft drop shadow, drawn at 4x and downsampled for anti-aliasing.

Usage (from the project root):
    uv run --no-project --with pillow python Tools/make_icons.py Assets/NAV/Art/Icons
"""
import math
import os
import sys

from PIL import Image, ImageDraw, ImageFilter

S = 512                   # working size
OUT = 128                 # final size
OUTLINE = (30, 20, 12, 255)
OW = 13                   # outline width at working size


# ---------------------------------------------------------------- colour helpers

def shade(color, factor):
    r, g, b = color[:3]
    return (min(255, int(r * factor)), min(255, int(g * factor)), min(255, int(b * factor)), 255)


WOOD = (150, 96, 50)
WOOD_DARK = (104, 64, 32)
WOOD_LIGHT = (205, 152, 92)
STONE = (146, 146, 142)
STONE_DARK = (98, 98, 96)
FLINT = (74, 82, 104)
BONE = (238, 230, 206)
LEATHER = (120, 70, 40)


# ---------------------------------------------------------------- drawing primitives

class Icon:
    """A layer stack: shapes are drawn onto `art`; shadow + compose happen in finish()."""

    def __init__(self):
        self.art = Image.new("RGBA", (S, S), (0, 0, 0, 0))

    def _paint(self, mask, color, light=1.22, dark=0.72, angle_down=True):
        """Fill `mask` with a vertical gradient of `color` (lighter on top)."""
        box = mask.getbbox()
        if box is None:
            return
        top, bottom = box[1], box[3]
        grad = Image.new("RGBA", (S, S))
        gd = ImageDraw.Draw(grad)
        for y in range(top, bottom + 1):
            t = (y - top) / max(1, bottom - top)
            f = light + (dark - light) * t if angle_down else dark + (light - dark) * t
            gd.line([(0, y), (S, y)], fill=shade(color, f))
        self.art.paste(grad, (0, 0), mask)

    def _outline(self, draw_fn):
        layer = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        draw_fn(ImageDraw.Draw(layer))
        self.art.alpha_composite(layer)

    def poly(self, pts, color, outline=True, **kw):
        mask = Image.new("L", (S, S), 0)
        ImageDraw.Draw(mask).polygon(pts, fill=255)
        self._paint(mask, color, **kw)
        if outline:
            self._outline(lambda d: d.line(pts + [pts[0]], fill=OUTLINE, width=OW, joint="curve"))

    def ellipse(self, box, color, outline=True, **kw):
        mask = Image.new("L", (S, S), 0)
        ImageDraw.Draw(mask).ellipse(box, fill=255)
        self._paint(mask, color, **kw)
        if outline:
            self._outline(lambda d: d.ellipse(box, outline=OUTLINE, width=OW))

    def rrect(self, box, color, radius=24, outline=True, **kw):
        mask = Image.new("L", (S, S), 0)
        ImageDraw.Draw(mask).rounded_rectangle(box, radius=radius, fill=255)
        self._paint(mask, color, **kw)
        if outline:
            self._outline(lambda d: d.rounded_rectangle(box, radius=radius, outline=OUTLINE, width=OW))

    def bar(self, cx, cy, length, thick, angle, color, outline=True):
        self.poly(rotated_rect(cx, cy, length, thick, angle), color, outline)

    def line(self, pts, color, width):
        self._outline(lambda d: d.line(pts, fill=color, width=width, joint="curve"))

    def highlight(self, pts, alpha=110):
        self._outline(lambda d: d.line(pts, fill=(255, 255, 255, alpha), width=12, joint="curve"))

    def finish(self):
        # Soft drop shadow from the art's own silhouette, offset down-right.
        alpha = self.art.getchannel("A")
        blurred = alpha.filter(ImageFilter.GaussianBlur(10)).point(lambda v: int(v * 0.45))
        # Plain shift (ImageChops.offset wraps around, which put right-edge shadow on the left).
        shadow_mask = Image.new("L", (S, S), 0)
        shadow_mask.paste(blurred, (10, 14))
        result = Image.new("RGBA", (S, S), (0, 0, 0, 0))
        result.paste((0, 0, 0, 255), (0, 0), shadow_mask)
        result.alpha_composite(self.art)
        return result.resize((OUT, OUT), Image.LANCZOS)


def rotated_rect(cx, cy, length, thick, angle_deg):
    a = math.radians(angle_deg)
    dx, dy = math.cos(a), math.sin(a)
    px, py = -dy, dx
    hl, ht = length / 2, thick / 2
    return [
        (cx - dx * hl - px * ht, cy - dy * hl - py * ht),
        (cx + dx * hl - px * ht, cy + dy * hl - py * ht),
        (cx + dx * hl + px * ht, cy + dy * hl + py * ht),
        (cx - dx * hl + px * ht, cy - dy * hl + py * ht),
    ]


def along(x0, y0, x1, y1, t):
    return x0 + (x1 - x0) * t, y0 + (y1 - y0) * t


# ---------------------------------------------------------------- resources

def wood(i):
    for (x0, y0) in ((96, 250), (150, 160)):
        i.rrect((x0, y0, x0 + 300, y0 + 104), WOOD, radius=52)
        i.line([(x0 + 40, y0 + 34), (x0 + 210, y0 + 34)], WOOD_DARK + (255,), 8)
        i.line([(x0 + 70, y0 + 68), (x0 + 240, y0 + 68)], WOOD_DARK + (255,), 8)
        i.ellipse((x0 + 250, y0, x0 + 354, y0 + 104), WOOD_LIGHT, light=1.1, dark=0.9)
        i.ellipse((x0 + 278, y0 + 28, x0 + 326, y0 + 76), WOOD, outline=False)
        i.ellipse((x0 + 294, y0 + 44, x0 + 310, y0 + 60), WOOD_DARK, outline=False)
        i.highlight([(x0 + 50, y0 + 14), (x0 + 200, y0 + 14)], 90)


def rock(i):
    i.poly([(96, 350), (130, 210), (240, 128), (372, 150), (432, 280), (376, 396), (190, 412)], STONE)
    i.poly([(150, 220), (240, 150), (330, 170), (262, 250)], shade(STONE, 1.25), outline=False)
    i.poly([(262, 250), (330, 170), (412, 282), (360, 380)], STONE_DARK, outline=False)
    i.line([(262, 250), (190, 400)], OUTLINE[:3] + (120,), 7)
    i.highlight([(170, 214), (236, 164)])


def flint(i):
    pts = [(130, 392), (180, 196), (300, 92), (414, 214), (352, 410)]
    i.poly(pts, FLINT)
    i.poly([(196, 200), (296, 116), (360, 214), (262, 268)], shade(FLINT, 1.45), outline=False)
    i.poly([(262, 268), (360, 214), (390, 300), (300, 340)], shade(FLINT, 0.8), outline=False)
    i.line([(196, 200), (262, 268), (300, 340), (300, 404)], (40, 44, 60, 160), 6)
    i.highlight([(214, 196), (288, 132)], 140)


def resin(i):
    amber = (226, 150, 36)
    i.poly([(256, 86), (196, 230), (316, 230)], amber, outline=False)
    i.ellipse((150, 200, 362, 412), amber, light=1.25, dark=0.75)
    i.poly([(256, 86), (172, 262), (340, 262)], amber, outline=False, light=1.3, dark=1.1)
    i.line([(256, 86), (178, 250)], OUTLINE, OW)
    i.line([(256, 86), (334, 250)], OUTLINE, OW)
    i.ellipse((196, 250, 252, 320), (255, 236, 170), outline=False, light=1.0, dark=0.95)
    i.highlight([(206, 230), (232, 176)], 150)


# ---------------------------------------------------------------- tools & weapons
# Handles run from bottom-left (grip) to top-right (head), head perpendicular at the top.

HANDLE_A = (120, 420)
HANDLE_B = (360, 140)


def handle(i, a=HANDLE_A, b=HANDLE_B, thick=40):
    cx, cy = (a[0] + b[0]) / 2, (a[1] + b[1]) / 2
    length = math.dist(a, b)
    angle = math.degrees(math.atan2(b[1] - a[1], b[0] - a[0]))
    i.bar(cx, cy, length, thick, angle, WOOD)
    # leather wrap near the grip
    gx, gy = along(*a, *b, 0.14)
    i.bar(gx, gy, 70, thick + 8, angle, LEATHER)
    return angle


def stone_axe(i):
    angle = handle(i)
    # Blade: wedge on one side of the handle top, edge curved outward.
    hx, hy = along(*HANDLE_A, *HANDLE_B, 0.86)
    perp = math.radians(angle - 90)
    px, py = math.cos(perp), math.sin(perp)
    ax, ay = math.cos(math.radians(angle)), math.sin(math.radians(angle))
    base1 = (hx - ax * 38, hy - ay * 38)
    base2 = (hx + ax * 38, hy + ay * 38)
    edge1 = (base1[0] + px * 150 - ax * 40, base1[1] + py * 150 - ay * 40)
    edge2 = (base2[0] + px * 150 + ax * 40, base2[1] + py * 150 + ay * 40)
    mid = (hx + px * 175, hy + py * 175)
    i.poly([base1, edge1, mid, edge2, base2], STONE)
    i.line([edge1, mid, edge2], (230, 230, 225, 200), 9)
    # binding
    i.bar(hx, hy, 70, 54, angle + 90, LEATHER)


def stone_pickaxe(i):
    angle = handle(i, thick=36)
    hx, hy = along(*HANDLE_A, *HANDLE_B, 0.9)
    perp = angle - 90
    # Curved two-sided head: arc made of a thick polyline.
    pts = []
    for k in range(-6, 7):
        t = k / 6
        r = 170
        bend = 38 * (1 - t * t)
        x = hx + math.cos(math.radians(perp)) * r * t + math.cos(math.radians(angle)) * bend
        y = hy + math.sin(math.radians(perp)) * r * t + math.sin(math.radians(angle)) * bend
        pts.append((x, y))
    i.line(pts, OUTLINE, 66)
    i.line(pts, STONE + (255,), 42)
    i.line(pts[1:6], shade(STONE, 1.3)[:3] + (255,), 12)
    i.bar(hx, hy, 60, 60, angle, LEATHER)


def wood_hammer(i):
    angle = handle(i, thick=38)
    hx, hy = along(*HANDLE_A, *HANDLE_B, 0.88)
    i.bar(hx, hy, 220, 116, angle + 90, WOOD_LIGHT)
    for t in (-0.32, 0.32):
        bx = hx + math.cos(math.radians(angle + 90)) * 220 * t
        by = hy + math.sin(math.radians(angle + 90)) * 220 * t
        i.bar(bx, by, 26, 120, angle + 90, WOOD_DARK)
    i.highlight([rotated_rect(hx, hy, 200, 100, angle + 90)[0], rotated_rect(hx, hy, 200, 100, angle + 90)[1]], 80)


def stone_sword(i):
    tip = (420, 92)
    guard_c = (170, 342)
    angle = math.degrees(math.atan2(tip[1] - guard_c[1], tip[0] - guard_c[0]))
    ax, ay = math.cos(math.radians(angle)), math.sin(math.radians(angle))
    px, py = -ay, ax
    w = 30
    blade = [
        (guard_c[0] + px * w, guard_c[1] + py * w),
        (tip[0] - ax * 60 + px * w * 0.8, tip[1] - ay * 60 + py * w * 0.8),
        tip,
        (tip[0] - ax * 60 - px * w * 0.8, tip[1] - ay * 60 - py * w * 0.8),
        (guard_c[0] - px * w, guard_c[1] - py * w),
    ]
    i.poly(blade, FLINT)
    i.line([guard_c, (tip[0] - ax * 30, tip[1] - ay * 30)], shade(FLINT, 1.5)[:3] + (255,), 8)
    i.bar(guard_c[0], guard_c[1], 150, 34, angle + 90, WOOD_DARK)
    grip = (guard_c[0] - ax * 70, guard_c[1] - ay * 70)
    i.bar(grip[0], grip[1], 110, 36, angle, LEATHER)
    pommel = (guard_c[0] - ax * 132, guard_c[1] - ay * 132)
    i.ellipse((pommel[0] - 26, pommel[1] - 26, pommel[0] + 26, pommel[1] + 26), STONE)


# ---------------------------------------------------------------- food

def meat(i, body, inner, grill):
    i.bar(372, 148, 150, 40, -45, BONE)
    i.ellipse((384, 74, 452, 142), BONE)
    i.ellipse((410, 106, 478, 174), BONE)
    i.ellipse((76, 168, 364, 432), body, light=1.2, dark=0.75)
    i.ellipse((146, 236, 292, 368), inner, outline=False, light=1.15, dark=0.9)
    if grill:
        for k in range(3):
            x = 140 + k * 64
            i.line([(x, 222), (x + 78, 400)], (66, 30, 12, 255), 18)
    i.highlight([(130, 236), (190, 196)], 120)


def raw_meat(i):
    meat(i, (198, 58, 56), (240, 150, 150), False)


def cooked_meat(i):
    meat(i, (150, 82, 34), (192, 122, 64), True)


# ---------------------------------------------------------------- building pieces

def foundation(i):
    i.poly([(64, 226), (256, 140), (448, 226), (256, 312)], WOOD_LIGHT, light=1.1, dark=0.95)
    i.poly([(64, 226), (256, 312), (256, 380), (64, 294)], WOOD, light=1.0, dark=0.8)
    i.poly([(256, 312), (448, 226), (448, 294), (256, 380)], WOOD_DARK, light=1.0, dark=0.85)
    for k in range(1, 4):
        a = along(64, 226, 256, 140, k / 4)
        b = along(256, 312, 448, 226, k / 4)
        i.line([a, b], WOOD_DARK + (200,), 7)


def wall(i):
    i.rrect((96, 104, 416, 412), WOOD, radius=10)
    for k in range(1, 4):
        y = 104 + 308 * k / 4
        i.line([(108, y), (404, y)], WOOD_DARK + (255,), 10)
    for x in (132, 380):
        i.rrect((x - 18, 92, x + 18, 424), WOOD_DARK, radius=8)
    i.highlight([(150, 124), (360, 124)], 80)


def door(i):
    i.rrect((150, 84, 362, 432), WOOD, radius=18)
    for k in (1, 2):
        x = 150 + 212 * k / 3
        i.line([(x, 96), (x, 420)], WOOD_DARK + (255,), 9)
    for y in (150, 360):
        i.rrect((160, y - 16, 352, y + 16), WOOD_DARK, radius=6)
    i.ellipse((310, 244, 342, 276), (214, 182, 96))


def roof(i):
    i.poly([(52, 320), (256, 100), (460, 320)], (124, 66, 40))
    for k in range(1, 4):
        y = 100 + 220 * k / 4
        half = 204 * k / 4
        i.line([(256 - half + 10, y), (256 + half - 10, y)], (82, 42, 22, 255), 10)
    i.rrect((150, 320, 362, 420), WOOD, radius=8)
    i.highlight([(150, 230), (240, 130)], 90)


def palisade(i):
    for k in range(5):
        x = 82 + k * 72
        color = WOOD if k % 2 == 0 else WOOD_LIGHT
        i.poly([(x, 424), (x, 176), (x + 32, 100), (x + 64, 176), (x + 64, 424)], color)
    i.bar(256, 330, 380, 30, 0, WOOD_DARK)


def workbench(i):
    i.rrect((64, 186, 448, 248), WOOD_LIGHT, radius=12)
    for x in (96, 382):
        i.rrect((x, 238, x + 36, 424), WOOD, radius=6)
    i.rrect((114, 320, 400, 350), WOOD_DARK, radius=6)
    i.rrect((292, 120, 404, 190), STONE, radius=12)
    i.bar(170, 160, 120, 26, -20, WOOD_DARK)
    i.highlight([(90, 200), (420, 200)], 90)


def campfire(i):
    for k in range(7):
        a = math.pi * (0.95 + k * 1.1 / 6)
        cx, cy = 256 + math.cos(a) * 168, 384 - math.sin(a) * 40
        i.ellipse((cx - 40, cy - 28, cx + 40, cy + 28), STONE)
    i.bar(256, 362, 300, 50, 18, WOOD)
    i.bar(256, 362, 300, 50, -18, WOOD_DARK)
    i.poly([(256, 84), (346, 250), (314, 344), (198, 344), (166, 250)], (242, 108, 28), light=1.15, dark=0.9)
    i.poly([(256, 184), (302, 280), (288, 334), (224, 334), (210, 280)], (255, 214, 90), outline=False)
    i.highlight([(230, 170), (252, 118)], 120)


ICONS = {
    "Wood": wood, "Rock": rock, "Flint": flint, "Resin": resin,
    "StoneAxe": stone_axe, "StonePickaxe": stone_pickaxe, "WoodHammer": wood_hammer,
    "StoneSword": stone_sword, "RawMeat": raw_meat, "CookedMeat": cooked_meat,
    "WoodFoundation": foundation, "WoodWall": wall, "WoodDoor": door, "WoodRoof": roof,
    "WoodPalisade": palisade, "WoodWorkbench": workbench, "Campfire": campfire,
}


def main():
    out_dir = sys.argv[1]
    os.makedirs(out_dir, exist_ok=True)
    for name, draw in ICONS.items():
        icon = Icon()
        draw(icon)
        icon.finish().save(os.path.join(out_dir, f"Icon_{name}.png"))
        print("wrote", name)


if __name__ == "__main__":
    main()
