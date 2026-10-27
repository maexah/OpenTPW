#!/usr/bin/env python3
"""watermean.py IMG [X0,Y0,X1,Y1] - the mean colour and count of the water's pixels in a frame (or a box of it): those
whose green and blue both exceed red by 40 and whose blue is over 0.6 of green (the river's cyan, not grass or rock)."""
import sys
from PIL import Image
im = Image.open(sys.argv[1]).convert('RGB')
if len(sys.argv) > 2: im = im.crop(tuple(int(v) for v in sys.argv[2].split(',')))
px = [p for p in im.getdata() if p[1] > p[0] + 40 and p[2] > p[0] + 40 and p[2] > 0.6 * p[1]]
n = len(px); m = [sum(p[i] for p in px) / max(n, 1) for i in range(3)]
print(f"{sys.argv[1]}: {n} water pixels, mean ({m[0]:.1f}, {m[1]:.1f}, {m[2]:.1f}) lum {0.299*m[0]+0.587*m[1]+0.114*m[2]:.1f}")
