#!/usr/bin/env python3
"""screendiff.py <a.png> <b.png> [x0 y0 x1 y1]: pixels differing by more than 40 (grey) between two frames in a box,
by default the Staff Training Budgets screen's rectangle in a 1280 x 720 frame (Q358 P3), and their bounding box."""
import sys
from PIL import Image, ImageChops
box = tuple(int(v) for v in sys.argv[3:7]) if len(sys.argv) > 6 else (270, 20, 1020, 470)
a, b = (Image.open(p).convert('RGB').crop(box) for p in sys.argv[1:3])
d = ImageChops.difference(a, b).convert('L').point(lambda v: 255 if v > 40 else 0)
print(f"box {box}: {sum(1 for v in d.getdata() if v)} pixels differ by more than 40, within {d.getbbox()}")
