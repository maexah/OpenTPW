#!/usr/bin/env python3
"""flipdiag.py <dir> [--mask X0,Y0,X1,Y1 ...] [--levels N] - the frames flipdiag.sh took (A1 A2 B1 B2 C1 C2): how many pixels
differ by more than N levels (8) in any channel between the pairs, and where. The controls are A1/A2, B1/B2 and C1/C2 (nothing
changed between them: 0 pixels when the clock and camera are held), the flip is A1 against B1, and the put-back is C1
against A1 (0 when the poke wrote nothing else). --mask leaves a box out (default 880,590,950,650: the pointer loadfile.sh
leaves at 900,600, whose picture changes between frames by itself). Writes diff_A1_B1.png (the difference times 30) and
prints the changed pixels' bounding box and each connected patch (a dilation of 6 pixels joins a patch's speckle)."""
import argparse, os
import numpy as np
from PIL import Image
ap = argparse.ArgumentParser(); ap.add_argument("dir"); ap.add_argument("--mask", action="append"); ap.add_argument("--levels", type=int, default=8)
a = ap.parse_args()
masks = [tuple(int(v) for v in m.split(",")) for m in (a.mask or ["880,590,950,650"])]
L = lambda n: np.asarray(Image.open(os.path.join(a.dir, n + ".png")).convert("RGB")).astype(int)
f = {n: L(n) for n in ("A1", "A2", "B1", "B2", "C1", "C2")}
def diff(x, y):
	d = np.abs(f[x] - f[y]).max(axis=2) > a.levels
	for x0, y0, x1, y1 in masks: d[y0:y1, x0:x1] = False
	return d
for x, y in (("A1", "A2"), ("B1", "B2"), ("C1", "C2"), ("A1", "B1"), ("C1", "A1"), ("C2", "A2")):
	d = diff(x, y); ys, xs = np.nonzero(d)
	print(f"{x} against {y}: {d.sum()} pixels differ by more than {a.levels}" + (f", in x {xs.min()}..{xs.max()}, y {ys.min()}..{ys.max()}" if len(ys) else ""))
d = diff("A1", "B1")
Image.fromarray(np.clip(np.abs(f["A1"] - f["B1"]).max(axis=2) * 30, 0, 255).astype("uint8")).save(os.path.join(a.dir, "diff_A1_B1.png"))
# patches: flood-fill the dilated mask
h, w = d.shape; dil = d.copy()
for dx in range(-6, 7):
	for dy in range(-6, 7): dil |= np.roll(np.roll(d, dx, 1), dy, 0)
lab = np.zeros((h, w), int); n = 0
for y0, x0 in zip(*np.nonzero(dil)):
	if lab[y0, x0]: continue
	n += 1; st = [(y0, x0)]; lab[y0, x0] = n
	while st:
		y, x = st.pop()
		for yy, xx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
			if 0 <= yy < h and 0 <= xx < w and dil[yy, xx] and not lab[yy, xx]: lab[yy, xx] = n; st.append((yy, xx))
print(f"patches: {n}")
for i in range(1, n + 1):
	ys, xs = np.nonzero((lab == i) & d); print(f"  patch {i}: {len(ys)} pixels, x {xs.min()}..{xs.max()}, y {ys.min()}..{ys.max()}")
