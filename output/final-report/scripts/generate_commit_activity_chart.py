from __future__ import annotations

import subprocess
from collections import Counter
from datetime import date, timedelta
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


ROOT = Path(__file__).resolve().parents[1]
REPOSITORY = ROOT.parent / "AISD-SS26-Group-7"
OUTPUT = ROOT / "figures/github-commit-activity.png"


def font(name: str, size: int) -> ImageFont.FreeTypeFont:
    path = Path("C:/Windows/Fonts") / name
    return ImageFont.truetype(str(path), size=size)


def load_commit_dates() -> list[date]:
    result = subprocess.run(
        [
            "git",
            "-C",
            str(REPOSITORY),
            "log",
            "main",
            "--format=%ad",
            "--date=short",
        ],
        check=True,
        capture_output=True,
        text=True,
    )
    return [date.fromisoformat(value) for value in result.stdout.splitlines() if value]


def dashed_line(
    draw: ImageDraw.ImageDraw,
    start: tuple[int, int],
    end_x: int,
    color: str,
    dash: int = 10,
    gap: int = 8,
) -> None:
    x, y = start
    while x < end_x:
        draw.line((x, y, min(x + dash, end_x), y), fill=color, width=2)
        x += dash + gap


def main() -> None:
    commit_dates = load_commit_dates()
    if not commit_dates:
        raise RuntimeError("No commits found on main")

    first_date = min(commit_dates)
    last_date = max(commit_dates)
    first_week = first_date - timedelta(days=first_date.weekday())
    last_week = last_date - timedelta(days=last_date.weekday())

    counts = Counter(
        commit_date - timedelta(days=commit_date.weekday())
        for commit_date in commit_dates
    )
    weeks = []
    current = first_week
    while current <= last_week:
        weeks.append(current)
        current += timedelta(days=7)
    values = [counts[week] for week in weeks]

    width, height = 1800, 920
    image = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(image)

    title_font = font("seguisb.ttf", 46)
    subtitle_font = font("segoeui.ttf", 26)
    axis_font = font("segoeui.ttf", 22)
    value_font = font("seguisb.ttf", 21)

    draw.rounded_rectangle(
        (12, 12, width - 12, height - 12),
        radius=14,
        outline="#d0d7de",
        width=2,
        fill="white",
    )
    draw.text(
        (62, 48),
        "Weekly commit activity - April to July 2026",
        font=title_font,
        fill="#24292f",
    )
    draw.text(
        (64, 112),
        "Commits per week on the main branch",
        font=subtitle_font,
        fill="#57606a",
    )

    plot_left, plot_top = 120, 190
    plot_right, plot_bottom = width - 60, height - 120
    plot_width = plot_right - plot_left
    plot_height = plot_bottom - plot_top

    maximum = max(values)
    y_max = max(5, ((maximum + 4) // 5) * 5)
    for tick in range(0, y_max + 1, 5):
        y = plot_bottom - round((tick / y_max) * plot_height)
        dashed_line(draw, (plot_left, y), plot_right, "#d8dee4")
        label = str(tick)
        label_box = draw.textbbox((0, 0), label, font=axis_font)
        draw.text(
            (plot_left - 24 - (label_box[2] - label_box[0]), y - 14),
            label,
            font=axis_font,
            fill="#57606a",
        )

    draw.line((plot_left, plot_bottom, plot_right, plot_bottom), fill="#8c959f", width=2)
    slot_width = plot_width / len(weeks)
    bar_width = min(74, slot_width * 0.62)

    for index, (week, value) in enumerate(zip(weeks, values, strict=True)):
        center_x = plot_left + slot_width * (index + 0.5)
        bar_height = (value / y_max) * plot_height
        left = round(center_x - bar_width / 2)
        right = round(center_x + bar_width / 2)
        top = round(plot_bottom - bar_height)
        if value:
            draw.rounded_rectangle(
                (left, top, right, plot_bottom),
                radius=5,
                fill="#2da44e",
            )
            value_text = str(value)
            value_box = draw.textbbox((0, 0), value_text, font=value_font)
            draw.text(
                (center_x - (value_box[2] - value_box[0]) / 2, top - 32),
                value_text,
                font=value_font,
                fill="#24292f",
            )

        date_label = f"{week.strftime('%b')} {week.day}"
        label_box = draw.textbbox((0, 0), date_label, font=axis_font)
        draw.text(
            (center_x - (label_box[2] - label_box[0]) / 2, plot_bottom + 24),
            date_label,
            font=axis_font,
            fill="#57606a",
        )

    draw.text(
        (plot_left, height - 58),
        f"Source period: {first_date.strftime('%d %B %Y')} to {last_date.strftime('%d %B %Y')}",
        font=axis_font,
        fill="#57606a",
    )

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    image.save(OUTPUT, format="PNG", optimize=True)
    print(f"Created {OUTPUT.relative_to(ROOT)}")
    print("Weekly counts:")
    for week, value in zip(weeks, values, strict=True):
        print(f"  {week.isoformat()}: {value}")


if __name__ == "__main__":
    main()
