from __future__ import annotations

from math import cos, radians, sin
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "figures" / "ai-usage-score-pie.png"

SCALE = 3
SIZE = 1600
COLORS = (
    (0, 101, 189, 255),
    (0, 153, 145, 255),
    (112, 78, 170, 255),
    (220, 105, 30, 255),
)
VALUES = (30, 40, 20, 10)


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    windows_font = Path("C:/Windows/Fonts") / ("segoeuib.ttf" if bold else "segoeui.ttf")
    fallback = Path("C:/Windows/Fonts") / ("arialbd.ttf" if bold else "arial.ttf")
    selected = windows_font if windows_font.exists() else fallback
    return ImageFont.truetype(str(selected), size * SCALE)


def main() -> None:
    canvas_size = SIZE * SCALE
    image = Image.new("RGBA", (canvas_size, canvas_size), (255, 255, 255, 0))
    draw = ImageDraw.Draw(image)

    margin = 95 * SCALE
    box = (margin, margin, canvas_size - margin, canvas_size - margin)
    start_angle = -90.0
    segment_midpoints: list[float] = []

    for value, color in zip(VALUES, COLORS, strict=True):
        end_angle = start_angle + value * 3.6
        draw.pieslice(box, start=start_angle, end=end_angle, fill=color, outline="white", width=8 * SCALE)
        segment_midpoints.append((start_angle + end_angle) / 2)
        start_angle = end_angle

    center = canvas_size / 2
    inner_radius = 350 * SCALE
    draw.ellipse(
        (
            center - inner_radius,
            center - inner_radius,
            center + inner_radius,
            center + inner_radius,
        ),
        fill=(255, 255, 255, 255),
    )

    value_font = font(94, bold=True)
    label_radius = 535 * SCALE
    for value, angle in zip(VALUES, segment_midpoints, strict=True):
        x = center + label_radius * cos(radians(angle))
        y = center + label_radius * sin(radians(angle))
        draw.text((x, y), str(value), fill="white", font=value_font, anchor="mm")

    draw.text((center, center - 42 * SCALE), "100", fill=(18, 31, 48, 255), font=font(142, bold=True), anchor="mm")
    draw.text((center, center + 110 * SCALE), "points", fill=(82, 96, 112, 255), font=font(60), anchor="mm")

    image = image.resize((SIZE, SIZE), Image.Resampling.LANCZOS)
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    image.save(OUTPUT, optimize=True)
    print(f"Created {OUTPUT}")


if __name__ == "__main__":
    main()
