"""Makes OpenHOI3's icon from the public-domain photograph of a line of battleships beside this script: the leading ship,
bow on, in a sunset colouring - an orange sky and copper sea, the ships dark against them - cut square with rounded corners.
Writes icon.png (512) and icon.ico (16 to 256) into the folder given; the repository's two are this script's, made by
`python tools/icon/make_icon.py .` from the repository's root. See readme.md beside it for the photograph's source."""
import os
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

HERE = os.path.dirname(os.path.abspath(__file__))
PHOTO = os.path.join(HERE, "battleships.webp")
CROP = (40, 40, 530, 530)      # the leading ship, in the 800 x 641 photograph
HORIZON = 0.585                # where the sea meets the sky, as a share of the photograph's height


def smoothstep(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def lerp(a, b, t):
    return a + (b - a) * (t[..., None] if np.ndim(t) == 2 else t)


def hue(r, g, b):
    """A colour divided by its brightness: what a pixel's own brightness is multiplied by to take that colour."""
    c = np.array([r, g, b], dtype=np.float32)
    return c / (0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2])


def sunset(photo):
    """The photograph coloured as at sunset, its own light and shade kept: each of sky, sea and ships given a hue."""
    width, height = photo.size
    L = np.asarray(photo).astype(np.float32) / 255.0
    horizon = HORIZON * height
    y = np.arange(height, dtype=np.float32)[:, None].repeat(width, axis=1)
    sky = smoothstep(horizon + 30, horizon - 30, y)
    # Ships and smoke: darker than what is around them, or dark outright - spread and smoothed so a hull keeps one colour.
    blurred = np.asarray(photo.filter(ImageFilter.GaussianBlur(28))).astype(np.float32) / 255.0
    dark = np.maximum(smoothstep(0.04, 0.16, blurred - L), smoothstep(0.50, 0.28, L))
    mask = Image.fromarray((np.clip(dark, 0, 1) * 255).astype(np.uint8)).filter(ImageFilter.MaxFilter(5)).filter(ImageFilter.GaussianBlur(3))
    dark = np.asarray(mask).astype(np.float32) / 255.0

    ones = np.ones((height, width, 3), dtype=np.float32)
    sky_hue = lerp(hue(1.0, 0.42, 0.22) * ones, hue(1.0, 0.70, 0.38) * ones, np.clip(y / horizon, 0, 1) ** 1.5)
    sea_hue = hue(0.95, 0.52, 0.34) * ones
    ship_hue = hue(0.50, 0.30, 0.32) * ones
    light = lerp((0.22 + L * 0.80)[..., None] * ones, (L * 0.55)[..., None] * ones, dark ** 1.5)
    tint = lerp(lerp(sea_hue, sky_hue, sky), ship_hue, dark)
    tint = 1.0 + (tint - 1.0) * 0.85
    return Image.fromarray((np.clip(light * tint, 0, 1) * 255).astype(np.uint8), "RGB")


def rounded(img):
    radius = img.size[0] * 40 // 256
    mask = Image.new("L", img.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, img.size[0] - 1, img.size[1] - 1], radius=radius, fill=255)
    result = Image.new("RGBA", img.size, (0, 0, 0, 0))
    result.paste(img, (0, 0), mask)
    return result


def main(out):
    square = sunset(Image.open(PHOTO).convert("L")).crop(CROP)
    rounded(square.resize((512, 512), Image.LANCZOS)).save(os.path.join(out, "icon.png"))
    # Each size of the .ico made from the crop itself, not from a smaller copy.
    sizes = [16, 24, 32, 48, 64, 128, 256]
    largest = rounded(square.resize((256, 256), Image.LANCZOS))
    largest.save(os.path.join(out, "icon.ico"), sizes=[(s, s) for s in sizes])


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else ".")
