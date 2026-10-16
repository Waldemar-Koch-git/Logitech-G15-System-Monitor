"""Rebuild the original G15 icon assets with Python 3 and Pillow (build-only)."""
from pathlib import Path

from PIL import Image, ImageDraw


def render(size):
    scale = 4
    image = Image.new("RGBA", (size * scale, size * scale))
    draw = ImageDraw.Draw(image)
    factor = size * scale / 256

    def box(coords):
        return tuple(round(value * factor) for value in coords)

    def rounded(coords, radius, color):
        draw.rounded_rectangle(box(coords), round(radius * factor), fill=color)

    # A bright LCD trace and four keys evoke the G15 without using a vendor logo.
    rounded((8, 24, 248, 224), 32, "#84919F")
    rounded((16, 32, 240, 216), 26, "#202A36")
    rounded((32, 48, 224, 174), 12, "#080F18")
    points = [(44, 119), (77, 119), (98, 83), (125, 143), (150, 102), (170, 119), (212, 119)]
    width = max(1, round(12 * factor))
    draw.line([box(point) for point in points], fill="#FF9D32", width=width, joint="curve")
    radius = width / 2
    for point in points:
        x, y = box(point)
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill="#FF9D32")
    for x in (56, 96, 136, 176):
        rounded((x, 188, x + 24, 198), 4, "#FF9D32")
    return image.resize((size, size), Image.Resampling.LANCZOS)


if __name__ == "__main__":
    output = Path(__file__).resolve().parents[1] / "assets"
    output.mkdir(exist_ok=True)
    sizes = (16, 20, 24, 32, 40, 48, 64, 128, 256)
    frames = [render(size) for size in sizes]
    frames[-1].save(output / "G15SystemMonitor.png")
    frames[-1].save(output / "G15SystemMonitor.ico", sizes=[(s, s) for s in sizes], append_images=frames[:-1])
    print(f"Created PNG and ICO ({len(sizes)} sizes) in {output}")
