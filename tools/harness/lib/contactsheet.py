#!/usr/bin/env python3
"""contactsheet.py OUT.png [--cols N] [--width W] IMG[=LABEL] ...: frames side by side, each scaled to W pixels wide
(default 420) with its label (default the file's name) printed under it, N to a row (default 3). For showing a set
of candidate photographs at once, so they can be compared and picked by label."""
import os, sys
from PIL import Image, ImageDraw, ImageFont

def sheet(out, items, cols=3, width=420):
    tiles = []
    for path, label in items:
        im = Image.open(path).convert("RGB"); h = round(im.height * width / im.width)
        tiles.append((im.resize((width, h), Image.LANCZOS), label))
    th = max(t.height for t, _ in tiles) + 26; rows = (len(tiles) + cols - 1) // cols
    canvas = Image.new("RGB", (cols * (width + 8) + 8, rows * (th + 8) + 8), (32, 32, 32))
    draw = ImageDraw.Draw(canvas)
    try: font = ImageFont.truetype("DejaVuSans-Bold.ttf", 16)
    except OSError: font = ImageFont.load_default()
    for n, (tile, label) in enumerate(tiles):
        x = 8 + (n % cols) * (width + 8); y = 8 + (n // cols) * (th + 8)
        canvas.paste(tile, (x, y)); draw.text((x + 4, y + tile.height + 4), label, fill=(255, 230, 80), font=font)
    canvas.save(out)

if __name__ == "__main__":
    args = sys.argv[1:]; cols, width = 3, 420
    out = args.pop(0)
    while args and args[0].startswith("--"):
        flag, value = args.pop(0), int(args.pop(0))
        if flag == "--cols": cols = value
        elif flag == "--width": width = value
        else: raise SystemExit(f"unknown option {flag}")
    items = [(a.split("=", 1)[0], a.split("=", 1)[1] if "=" in a else os.path.splitext(os.path.basename(a))[0]) for a in args]
    sheet(out, items, cols, width)
