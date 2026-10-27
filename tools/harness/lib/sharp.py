#!/usr/bin/env python3
"""lib/sharp.py IMG NAME=X0,Y0,X1,Y1 ... - each patch's sharpness: the mean absolute 4-neighbour Laplacian of luminance
(0.299 R + 0.587 G + 0.114 B), and its mean colour. Q617."""
import sys
import numpy as np
from PIL import Image
im = np.asarray(Image.open(sys.argv[1]).convert("RGB")).astype(float)
lum = im @ np.array([0.299, 0.587, 0.114])
out = {}
for arg in sys.argv[2:]:
    name, box = arg.split("="); x0, y0, x1, y1 = map(int, box.split(","))
    p = lum[y0:y1, x0:x1]
    lap = 4 * p[1:-1, 1:-1] - p[:-2, 1:-1] - p[2:, 1:-1] - p[1:-1, :-2] - p[1:-1, 2:]
    out[name] = np.abs(lap).mean()
    c = im[y0:y1, x0:x1].reshape(-1, 3).mean(0)
    print(f"{name} ({x0},{y0})-({x1},{y1}): sharpness {out[name]:.2f} mean ({c[0]:.1f}, {c[1]:.1f}, {c[2]:.1f})")
names = list(out)
if len(names) >= 2: print(f"ratio {names[0]}/{names[1]} = {out[names[0]] / out[names[1]]:.2f}")
