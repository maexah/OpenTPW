#!/usr/bin/env python3
"""flipstats.py <dir> [--mask X0,Y0,X1,Y1] - what changes inside flipdiag.sh's patches (Q837): per patch the mean change of luma
(Rec.601, B1 less A1) over its changed pixels, and the vertical shift of the content at the patch's centroid (the shift of a
40 x 30 window of A1 that best matches B1, mean absolute difference, -12..12 pixels; the window centre is the centroid of the
changed pixels, so the numbers are the picture's, not a cell's projected centre). Same patches and mask as flipdiag.py (dilation 6,
no wrap). Prints the all-patch luma change too. Stdlib + numpy + PIL only."""
import argparse, os
import numpy as np
from PIL import Image
ap = argparse.ArgumentParser(); ap.add_argument("dir"); ap.add_argument("--mask", action="append"); a = ap.parse_args()
masks = [tuple(int(v) for v in m.split(",")) for m in (a.mask or ["880,590,950,650"])]
L = lambda n: np.asarray(Image.open(os.path.join(a.dir, n + ".png")).convert("RGB")).astype(float)
A, B = L("A1"), L("B1")
d = np.abs(A - B).max(axis=2) > 8
for x0, y0, x1, y1 in masks: d[y0:y1, x0:x1] = False
Y = np.array([.299, .587, .114]); dl = (B - A) @ Y
print(f"all changed pixels: {d.sum()}, mean luma change {dl[d].mean():+.3f}")
h, w = d.shape; dil = np.zeros_like(d)
for dx in range(-6, 7):
	for dy in range(-6, 7):
		s = np.zeros_like(d); s[max(0, dy):h + min(0, dy), max(0, dx):w + min(0, dx)] = d[max(0, -dy):h + min(0, -dy), max(0, -dx):w + min(0, -dx)]; dil |= s
lab = np.zeros((h, w), int); n = 0
for y0, x0 in zip(*np.nonzero(dil)):
	if lab[y0, x0]: continue
	n += 1; st = [(y0, x0)]; lab[y0, x0] = n
	while st:
		y, x = st.pop()
		for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
			if 0 <= yy < h and 0 <= xx < w and dil[yy, xx] and not lab[yy, xx]: lab[yy, xx] = n; st.append((yy, xx))
for i in range(1, n + 1):
	m = (lab == i) & d; ys, xs = np.nonzero(m); cx, cy = int(xs.mean()), int(ys.mean())
	win = A[cy - 15:cy + 15, cx - 20:cx + 20]; r = []
	for s in range(-12, 13):
		v = B[cy - 15 + s:cy + 15 + s, cx - 20:cx + 20]
		if v.shape == win.shape: r.append((np.abs(v - win).mean(), s))
	r.sort()
	print(f"patch {i}: {m.sum()} pixels, centroid ({cx},{cy}), luma {dl[m].mean():+.3f}, best shift {r[0][1]:+d} (error {r[0][0]:.2f}; next {r[1][1]:+d} {r[1][0]:.2f}, {r[2][1]:+d} {r[2][0]:.2f})")
