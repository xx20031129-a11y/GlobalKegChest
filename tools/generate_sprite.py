"""Render the code-defined pixel vector icon into the game's six-frame atlas.

Each rectangle is written to both SVG source and PNG so the asset can be
maintained as a tiny vector icon, without copying or shipping game textures.
Requires Pillow only when regenerating the checked-in asset.
"""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
asset = root / "src" / "GlobalKegChest" / "assets"
asset.mkdir(parents=True, exist_ok=True)
image = Image.new("RGBA", (96, 32))
draw = ImageDraw.Draw(image)
rectangles = []

def rect(frame, x, y, w, h, color):
    x += frame * 16
    draw.rectangle((x, y, x + w - 1, y + h - 1), fill=color)
    rectangles.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" fill="{color}"/>')

for frame in range(6):
    opened = max(0, frame - 1)
    rect(frame, 2, 15, 12, 15, "#32292d")
    rect(frame, 1, 17, 14, 11, "#3f2c2b")
    rect(frame, 2, 16, 12, 12, "#a1693f")
    rect(frame, 3, 18, 10, 9, "#885033")
    rect(frame, 3, 19, 10, 1, "#bd8350")
    rect(frame, 3, 23, 10, 1, "#674331")
    rect(frame, 2, 27, 12, 2, "#d3a45f")
    rect(frame, 3, 17, 1, 10, "#e6be75")
    rect(frame, 12, 17, 1, 10, "#b78b50")
    rect(frame, 5, 20, 6, 6, "#3a3439")
    rect(frame, 6, 20, 4, 6, "#d7b284")
    rect(frame, 5, 21, 6, 4, "#d7b284")
    rect(frame, 6, 21, 4, 1, "#765348")
    rect(frame, 6, 24, 4, 1, "#765348")
    rect(frame, 7, 22, 2, 2, "#7f436e")
    # The dark well covers the closed lid underneath the animated full frame.
    rect(frame, 2, 14, 12, 4, "#332d35")
    rect(frame, 3, 15, 10, 2, "#1e242c")
    lid_y = 11 - opened * 2
    rect(frame, 2, lid_y, 12, 5, "#3f2c2b")
    rect(frame, 3, lid_y - 1, 10, 1, "#3f2c2b")
    rect(frame, 3, lid_y, 10, 3, "#b57a46")
    rect(frame, 3, lid_y, 10, 1, "#e6b56c")
    rect(frame, 2, lid_y + 3, 12, 1, "#d3a45f")
    rect(frame, 7, lid_y + 2, 2, 2, "#396d71")
    rect(frame, 7, lid_y + 2, 1, 1, "#8bd2bf")

image.save(asset / "chest.png", optimize=True)
(root / "tools" / "chest.svg").write_text(
    '<svg xmlns="http://www.w3.org/2000/svg" width="96" height="32" viewBox="0 0 96 32" shape-rendering="crispEdges">'
    + "".join(rectangles) + "</svg>", encoding="utf-8")
preview = root / "artifacts" / "sprite-preview.png"
preview.parent.mkdir(exist_ok=True)
image.resize((768, 256), Image.Resampling.NEAREST).save(preview)
print(f"Rendered {asset / 'chest.png'} (96x32 RGBA, 6 frames)")
