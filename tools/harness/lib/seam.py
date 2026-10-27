#!/usr/bin/env python3
"""seam.py <frame.png> x0 x1 y0 y1: a dark seam between water and its near bank, column by column (Q358). In each
column of the window the last water pixel (blue over red by 40 and green over red by 30) is found; the band is the
darkest luma in the 10 pixels below it, set against the darker of the water's (5 pixels above) and the bank's (pixels
10 to 18 below) medians. A column whose band is under half of that is a seam column. Prints the share of seam columns
among those with water, the median ratio, and the longest run of neighbouring seam columns (a band, where a rock's
outline touching the water is a few). Frames of two games lit differently compare by the ratio, not a level."""
import sys, statistics
from PIL import Image
im = Image.open(sys.argv[1]).convert('RGB'); px = im.load()
x0, x1, y0, y1 = (int(v) for v in sys.argv[2:6])
luma = lambda p: 0.299 * p[0] + 0.587 * p[1] + 0.114 * p[2]
water = lambda p: p[2] > p[0] + 40 and p[1] > p[0] + 30
ratios = []; flags = []
for x in range(x0, x1):
    ys = [y for y in range(y0, y1) if water(px[x, y])]
    if not ys: continue
    e = max(ys)
    if e + 18 >= im.size[1] or e - 5 < 0: continue
    band = min(luma(px[x, e + k]) for k in range(1, 11))
    w = statistics.median(luma(px[x, e - k]) for k in range(5))
    b = statistics.median(luma(px[x, e + k]) for k in range(10, 19))
    ratios.append(band / max(1.0, min(w, b))); flags.append(ratios[-1] < 0.5)
run = best = 0
for f in flags:
    run = run + 1 if f else 0; best = max(best, run)
seam = sum(1 for r in ratios if r < 0.5)
print(f"{sys.argv[1]}: {len(ratios)} columns with water, {seam} seam columns = {seam / max(1, len(ratios)):.3f}, median ratio {statistics.median(ratios) if ratios else 0:.3f}, longest run of seam columns {best}")
