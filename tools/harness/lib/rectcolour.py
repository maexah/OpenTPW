#!/usr/bin/env python3
"""rectcolour.py <png> <x0> <y0> <x1> <y1> ... : what fills each layout rectangle of a frame of either game (the 2048 x
1536 layout fitted, 4:3 centred, as barshare.py), its edges' outer 4 per cent left out: the shares of yellow (the
interface's 255, 239, 0 lettering: r and g over 150, b under 110, r - b over 90), white (each channel over 190), teal
(b over r + 40 and g over r + 25: the `!cbut` frame's body), black (each under 30), and the rectangle's mean colour.
A line a rectangle: `yellow white teal black (r, g, b)`. Built for Q370's headings; it reads any framed control."""
import sys
from PIL import Image
im = Image.open(sys.argv[1]).convert("RGB"); w, h = im.size; s = h / 1536; ox = (w - 2048 * s) / 2
a = list(map(float, sys.argv[2:]))
for i in range(0, len(a), 4):
    x0, y0, x1, y1 = a[i:i + 4]; dx = (x1 - x0) * 0.04; dy = (y1 - y0) * 0.04
    px = [im.getpixel((x, y)) for y in range(int((y0 + dy) * s), int((y1 - dy) * s))
          for x in range(int(round((x0 + dx) * s + ox)), int(round((x1 - dx) * s + ox)))]
    n = len(px) or 1
    yellow = sum(1 for r, g, b in px if r > 150 and g > 150 and b < 110 and r - b > 90) / n
    white = sum(1 for p in px if min(p) > 190) / n
    teal = sum(1 for r, g, b in px if b > r + 40 and g > r + 25) / n
    black = sum(1 for p in px if max(p) < 30) / n
    mean = tuple(round(sum(p[c] for p in px) / n, 1) for c in range(3))
    print(round(yellow, 3), round(white, 3), round(teal, 3), round(black, 3), mean)
