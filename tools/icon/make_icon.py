"""Draws OpenHOI3's own icon: a globe with a red front-line arrow across it, on a dark rounded square - the project's
own art, nothing taken from an install. Writes icon.png (1024) and icon.ico (16 to 256) into the folder given, with two
previews; the repository's icon.png and icon.ico are this script's, run as `python tools/icon/make_icon.py <folder>`."""
import math
import sys
from PIL import Image, ImageDraw, ImageFilter

out = sys.argv[1]
S = 2048  # drawn at twice the size and scaled down, for smooth edges
img = Image.new("RGBA", (S, S), (0, 0, 0, 0))

# The tile: a rounded square, steel blue darkening downwards, with a dark rim.
tile = Image.new("RGBA", (S, S), (0, 0, 0, 0))
grad = Image.new("RGBA", (S, S))
gd = ImageDraw.Draw(grad)
for y in range(S):
    t = y / S
    r = int(0x2e + (0x16 - 0x2e) * t)
    g = int(0x3d + (0x1f - 0x3d) * t)
    b = int(0x50 + (0x2a - 0x50) * t)
    gd.line([(0, y), (S, y)], fill=(r, g, b, 255))
mask = Image.new("L", (S, S), 0)
ImageDraw.Draw(mask).rounded_rectangle([40, 40, S - 40, S - 40], radius=360, fill=255)
tile.paste(grad, (0, 0), mask)
ImageDraw.Draw(tile).rounded_rectangle([40, 40, S - 40, S - 40], radius=360, outline=(10, 14, 20, 255), width=36)
img.alpha_composite(tile)

# The globe: a disc with meridians and parallels in parchment, clipped to it.
cx, cy, R = S // 2, S // 2 + 30, 700
globe = Image.new("RGBA", (S, S), (0, 0, 0, 0))
gl = ImageDraw.Draw(globe)
gl.ellipse([cx - R, cy - R, cx + R, cy + R], fill=(0x47, 0x5f, 0x74, 255))
lines = Image.new("RGBA", (S, S), (0, 0, 0, 0))
ld = ImageDraw.Draw(lines)
ink = (0xd8, 0xc4, 0x8c, 255)
for k in (-2, -1, 1, 2):  # meridians as ellipses of narrowing width
    w = abs(k) / 3 * R
    ld.ellipse([cx - (R - w), cy - R, cx + (R - w), cy + R], outline=ink, width=22)
ld.line([(cx, cy - R), (cx, cy + R)], fill=ink, width=22)
for k in (-2, -1, 0, 1, 2):  # parallels
    y = cy + k * R / 3
    ld.line([(cx - R, y), (cx + R, y)], fill=ink, width=22)
disc = Image.new("L", (S, S), 0)
ImageDraw.Draw(disc).ellipse([cx - R, cy - R, cx + R, cy + R], fill=255)
globe.alpha_composite(Image.composite(lines, Image.new("RGBA", (S, S), (0, 0, 0, 0)), disc))
ImageDraw.Draw(globe).ellipse([cx - R, cy - R, cx + R, cy + R], outline=ink, width=44)
img.alpha_composite(globe)

# The arrow: a thick red sweep from the lower left up to the right, with a dark edge and a shadow.
def arc_points(n=80):
    pts = []
    for i in range(n + 1):
        t = i / n
        x = 330 + t * (1500 - 330)
        y = 1560 - math.sin(t * math.pi * 0.62) * 900 - t * 140
        pts.append((x, y))
    return pts

pts = arc_points()
(hx, hy), (px, py) = pts[-1], pts[-6]
ang = math.atan2(hy - py, hx - px)
def head(scale):
    L, W = 330 * scale, 250 * scale
    tip = (hx + math.cos(ang) * L * 0.75, hy + math.sin(ang) * L * 0.75)
    left = (hx + math.cos(ang + math.pi / 2) * W, hy + math.sin(ang + math.pi / 2) * W)
    right = (hx + math.cos(ang - math.pi / 2) * W, hy + math.sin(ang - math.pi / 2) * W)
    return [tip, left, right]

def band(points, width, shift=0.0, dx=0.0, dy=0.0):
    """The curve as one polygon of the width given, its edges offset along each point's normal, moved by shift across."""
    left, right = [], []
    for i, (x, y) in enumerate(points):
        a = points[max(i - 1, 0)]
        b = points[min(i + 1, len(points) - 1)]
        tx, ty = b[0] - a[0], b[1] - a[1]
        n = math.hypot(tx, ty) or 1.0
        nx, ny = -ty / n, tx / n
        cxp, cyp = x + nx * shift + dx, y + ny * shift + dy
        left.append((cxp + nx * width / 2, cyp + ny * width / 2))
        right.append((cxp - nx * width / 2, cyp - ny * width / 2))
    return left + right[::-1]

shadow = Image.new("RGBA", (S, S), (0, 0, 0, 0))
sd = ImageDraw.Draw(shadow)
sd.polygon(band(pts, 210, dx=26, dy=34), fill=(0, 0, 0, 150))
sd.polygon([(x + 26, y + 34) for x, y in head(1.12)], fill=(0, 0, 0, 150))
img.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(24)))

arrow = ImageDraw.Draw(img)
arrow.polygon(band(pts, 210), fill=(0x3a, 0x08, 0x08, 255))
arrow.polygon(head(1.12), fill=(0x3a, 0x08, 0x08, 255))
arrow.polygon(band(pts, 160), fill=(0xc0, 0x26, 0x20, 255))
arrow.polygon(head(1.0), fill=(0xc0, 0x26, 0x20, 255))
arrow.polygon(band(pts[: len(pts) - 6], 46, shift=-28), fill=(0xe2, 0x4a, 0x3a, 255))

final = img.resize((1024, 1024), Image.LANCZOS)
final.save(out + "\\icon.png")
final.save(out + "\\icon.ico", sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
final.resize((64, 64), Image.LANCZOS).save(out + "\\icon_preview64.png")
final.resize((16, 16), Image.LANCZOS).resize((128, 128), Image.NEAREST).save(out + "\\icon_preview16.png")
print("written")
