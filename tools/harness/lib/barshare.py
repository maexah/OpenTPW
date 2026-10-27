#!/usr/bin/env python3
"""barshare.py <png> <x0> <y0> <x1> <y1> ... : each bar's (coloured share, grey share, mean r - g of the coloured, last
coloured pixel, mean grey) along its middle row, for a frame of either game (the 2048 x 1536 layout fitted, 4:3 centred)."""
import sys
from PIL import Image
im = Image.open(sys.argv[1]).convert("RGB"); w, h = im.size; s = h / 1536; ox = (w - 2048 * s) / 2
a = list(map(float, sys.argv[2:]))
for i in range(0, len(a), 4):
    x0, y0, x1, y1 = a[i:i + 4]; ym = int((y0 + y1) / 2 * s)
    xs = range(int(round(x0 * s + ox)) + 1, int(round(x1 * s + ox)) - 1); px = [im.getpixel((x, ym)) for x in xs]
    col = [p for p in px if max(p) - min(p) > 30]; grey = [p for p in px if max(p) - min(p) <= 12]
    last = max((j for j, p in enumerate(px) if max(p) - min(p) > 30), default=-1)
    print(round(len(col) / len(px), 3), round(len(grey) / len(px), 3), round(sum(p[0] - p[1] for p in col) / len(col), 1) if col else 0,
          px[last] if last >= 0 else None, round(sum(sum(p) / 3 for p in grey) / len(grey), 1) if grey else None,
          "quarter", px[len(px) // 8] if px else None)
