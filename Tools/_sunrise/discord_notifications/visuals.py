"""Компактные таблицы и значки GitHub без внешних серверов изображений."""

import base64
import io
import os
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ASSETS = Path(__file__).with_name("assets")


def icon_image(name: str, color: str, size: int = 32) -> Image.Image:
    path = ASSETS / name
    if path.parent != ASSETS or path.suffix != ".png":
        raise ValueError("Значок должен быть PNG из папки assets")
    with Image.open(path) as source:
        mask = source.getchannel("A").resize(
            (size, size), Image.Resampling.LANCZOS
        )
    image = Image.new("RGBA", (size, size), color)
    image.putalpha(mask)
    return image


def encode_image(image: Image.Image) -> str:
    stream = io.BytesIO()
    image.save(stream, format="PNG")
    return base64.b64encode(stream.getvalue()).decode()


def check_image(checks: list[dict], config: dict) -> str:
    options = config["check_card"]
    font_path = options["font"] or (
        str(Path(os.environ.get("WINDIR", "C:/Windows")) / "Fonts/arial.ttf")
        if os.name == "nt"
        else "DejaVuSans.ttf"
    )
    font = ImageFont.truetype(font_path, options["font_size"])
    width = options["width"]
    row_height = options["font_size"] + 12
    measure = ImageDraw.Draw(Image.new("RGBA", (1, 1)))
    rows = []
    for check in checks:
        status = check.get("status")
        if status == "completed":
            status = check.get("conclusion")
        style = "neutral"
        if status == "success":
            style = "success"
        elif status in {
            "failure",
            "error",
            "timed_out",
            "action_required",
            "stale",
        }:
            style = "failure"
        elif status in {
            "queued",
            "pending",
            "in_progress",
            "waiting",
            "requested",
        }:
            style = "pending"
        name = " ".join(str(check.get("name") or "Unnamed check").split())
        line = ""
        first = True
        for character in name:
            if (
                line
                and measure.textlength(line + character, font=font)
                > width - 64
            ):
                rows.append((style if first else None, line))
                first, line = False, ""
            line += character
        rows.append((style if first else None, line))
    height = len(rows) * row_height + 16
    if width * height > 20_000_000:
        raise ValueError("Список проверок слишком велик для одной таблицы")
    image = Image.new("RGBA", (width, height), options["background"])
    draw = ImageDraw.Draw(image)
    for index, (style, line) in enumerate(rows):
        top = 8 + index * row_height
        if style:
            image.alpha_composite(
                icon_image(
                    options[style + "_icon"],
                    options[style + "_color"],
                    24,
                ),
                (8, top),
            )
        draw.text((44, top), line, font=font, fill=options["text_color"])
    return encode_image(image)
