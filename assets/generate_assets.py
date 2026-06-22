#!/usr/bin/env python3
"""
Generates the visual assets for the Navisworks IFC Exporter:

  installer/assets/appicon.ico                                  (setup + uninstall icon)
  installer/assets/wizard_large.bmp           (164x314)         (installer left banner)
  installer/assets/wizard_small.bmp           (55x55)           (installer top-right)
  deploy/.../Contents/Resources/NavisworksIfcExporter.png       (PackageContents icon)
  deploy/.../Contents/Resources/appicon.ico                     (shipped icon)

Theme: an isometric cube (the BIM model) with an orange "export" arrow leaving
it, on a navy->teal gradient. Run:  python3 assets/generate_assets.py
"""
import math
import os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
INSTALLER_ASSETS = os.path.join(ROOT, "installer", "assets")
RESOURCES = os.path.join(ROOT, "deploy", "NavisworksIfcExporter.bundle", "Contents", "Resources")

FONT_BOLD = "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf"

NAVY       = (22, 35, 64)
TEAL       = (16, 120, 148)
CUBE_TOP   = (126, 203, 232)
CUBE_LEFT  = (52, 120, 168)
CUBE_RIGHT = (32, 82, 122)
EDGE       = (255, 255, 255)
ORANGE     = (245, 146, 40)
WHITE      = (255, 255, 255)

SS = 4  # supersampling factor


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def gradient(w, h, c1, c2):
    img = Image.new("RGB", (w, h), c1)
    d = ImageDraw.Draw(img)
    for y in range(h):
        d.line([(0, y), (w, y)], fill=lerp(c1, c2, y / max(1, h - 1)))
    return img


def hexagon(cx, cy, r):
    """Vertices of the iso-cube silhouette, keyed by angle (degrees, y-down)."""
    pts = {}
    for ang in (90, 30, 330, 270, 210, 150):
        a = math.radians(ang)
        pts[ang] = (cx + r * math.cos(a), cy - r * math.sin(a))
    return pts


def draw_cube(d, cx, cy, r, ew):
    p = hexagon(cx, cy, r)
    c = (cx, cy)
    d.polygon([p[150], p[210], p[270], c], fill=CUBE_LEFT)    # left face
    d.polygon([p[30], p[330], p[270], c], fill=CUBE_RIGHT)    # right face
    d.polygon([p[150], p[90], p[30], c], fill=CUBE_TOP)       # top face
    outline = [p[90], p[30], p[330], p[270], p[210], p[150], p[90]]
    d.line(outline, fill=EDGE, width=ew, joint="curve")
    d.line([p[90], c], fill=EDGE, width=ew)
    d.line([p[210], c], fill=EDGE, width=ew)
    d.line([p[330], c], fill=EDGE, width=ew)


def draw_arrow(d, start, end, width, head, color):
    d.line([start, end], fill=color, width=width)
    ang = math.atan2(end[1] - start[1], end[0] - start[0])
    left = (end[0] - head * math.cos(ang - math.radians(28)),
            end[1] - head * math.sin(ang - math.radians(28)))
    right = (end[0] - head * math.cos(ang + math.radians(28)),
             end[1] - head * math.sin(ang + math.radians(28)))
    d.polygon([end, left, right], fill=color)


def render_icon(size, rounded=True, bg=True):
    s = size * SS
    base = Image.new("RGBA", (s, s), (0, 0, 0, 0))

    if bg:
        grad = gradient(s, s, NAVY, TEAL).convert("RGBA")
        mask = Image.new("L", (s, s), 0)
        md = ImageDraw.Draw(mask)
        if rounded:
            md.rounded_rectangle([0, 0, s - 1, s - 1], radius=int(s * 0.22), fill=255)
        else:
            md.rectangle([0, 0, s - 1, s - 1], fill=255)
        base.paste(grad, (0, 0), mask)

    d = ImageDraw.Draw(base)
    cx, cy, r = s * 0.46, s * 0.55, s * 0.30
    draw_cube(d, cx, cy, r, max(2, int(s * 0.012)))

    # Export arrow leaving the cube toward the upper-right.
    draw_arrow(d, (s * 0.55, s * 0.40), (s * 0.86, s * 0.16),
               width=max(3, int(s * 0.05)), head=int(s * 0.14), color=ORANGE)

    return base.resize((size, size), Image.LANCZOS)


def build_ico(path):
    sizes = [16, 32, 48, 64, 128, 256]
    imgs = [render_icon(n) for n in sizes]
    imgs[-1].save(path, format="ICO", sizes=[(n, n) for n in sizes])


def build_png(path, size=128):
    render_icon(size).save(path, format="PNG")


def text_centered(d, cx, y, text, font, fill):
    bbox = d.textbbox((0, 0), text, font=font)
    w = bbox[2] - bbox[0]
    d.text((cx - w / 2, y), text, font=font, fill=fill)
    return bbox[3] - bbox[1]


def build_wizard_large(path):
    w, h = 164, 314
    s = SS
    img = gradient(w * s, h * s, NAVY, TEAL)
    d = ImageDraw.Draw(img)
    draw_cube(d, w * s * 0.5, h * s * 0.30, w * s * 0.30, max(2, int(w * s * 0.012)))
    draw_arrow(d, (w * s * 0.60, h * s * 0.20), (w * s * 0.92, h * s * 0.10),
               width=int(w * s * 0.045), head=int(w * s * 0.11), color=ORANGE)
    f_big = ImageFont.truetype(FONT_BOLD, int(w * s * 0.26))
    f_mid = ImageFont.truetype(FONT_BOLD, int(w * s * 0.115))
    f_small = ImageFont.truetype(FONT_BOLD, int(w * s * 0.075))
    cx = w * s * 0.5
    text_centered(d, cx, h * s * 0.52, "IFC", f_big, WHITE)
    text_centered(d, cx, h * s * 0.70, "EXPORTER", f_mid, ORANGE)
    text_centered(d, cx, h * s * 0.90, "by CHAN", f_small, (210, 225, 235))
    img.resize((w, h), Image.LANCZOS).save(path, format="BMP")


def build_wizard_small(path):
    img = render_icon(55, rounded=False, bg=True).convert("RGB")
    img.save(path, format="BMP")


def main():
    os.makedirs(INSTALLER_ASSETS, exist_ok=True)
    os.makedirs(RESOURCES, exist_ok=True)
    build_ico(os.path.join(INSTALLER_ASSETS, "appicon.ico"))
    build_wizard_large(os.path.join(INSTALLER_ASSETS, "wizard_large.bmp"))
    build_wizard_small(os.path.join(INSTALLER_ASSETS, "wizard_small.bmp"))
    build_png(os.path.join(RESOURCES, "NavisworksIfcExporter.png"), 128)
    build_ico(os.path.join(RESOURCES, "appicon.ico"))
    print("Assets generated.")


if __name__ == "__main__":
    main()
