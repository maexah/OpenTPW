#!/usr/bin/env python3
"""spikecut.py <dir> [--levels N] [--mask X0,Y0,X1,Y1] - the frames spikecut.sh took (A1 A2 pristine; B1 B2 two corners raised; C1 C2 the
same with CELL's 0x2 set; D1 0x2 cleared; E1 the corners put back): the pixels that differ by more than N levels (8) between the pairs
that matter, with their box and patches, and the skyline of the raised shape in C against B: per column, the topmost row differing from A1
by over --sky levels (25), C's less B's (> 0 is lower on the screen). The controls are A1/A2 and B1/B2
(0 pixels), the put-backs D1 against B1 and E1 against A1 (0), the raise A1 against B1, the flag B1 against C1."""
import argparse, os
import numpy as np
from PIL import Image
ap = argparse.ArgumentParser(); ap.add_argument("dir"); ap.add_argument("--levels", type=int, default=8); ap.add_argument("--mask", action="append"); ap.add_argument("--sky", type=int, default=25)
a = ap.parse_args()
masks = [tuple(int(v) for v in m.split(",")) for m in (a.mask or ["880,590,950,650"])]
L = lambda n: np.asarray(Image.open(os.path.join(a.dir, n + ".png")).convert("RGB")).astype(int)
f = {n: L(n) for n in ("A1", "A2", "B1", "B2", "C1", "C2", "D1", "E1")}
def diff(x, y):
	d = np.abs(f[x] - f[y]).max(axis=2) > a.levels
	for x0, y0, x1, y1 in masks: d[y0:y1, x0:x1] = False
	return d
def box(d):
	ys, xs = np.nonzero(d); return f"{d.sum()} pixels" + (f", x {xs.min()}..{xs.max()}, y {ys.min()}..{ys.max()}" if len(ys) else "")
for x, y in (("A1", "A2"), ("B1", "B2"), ("C1", "C2"), ("A1", "B1"), ("B1", "C1"), ("D1", "B1"), ("E1", "A1")):
	print(f"{x} against {y}: {box(diff(x, y))} differ by more than {a.levels}")
d = diff("B1", "C1")
Image.fromarray(np.clip(np.abs(f["B1"] - f["C1"]).max(axis=2) * 30, 0, 255).astype("uint8")).save(os.path.join(a.dir, "diff_B1_C1.png"))
Image.fromarray(np.clip(np.abs(f["A1"] - f["B1"]).max(axis=2) * 30, 0, 255).astype("uint8")).save(os.path.join(a.dir, "diff_A1_B1.png"))
# the skyline of the raised shape: per column, the topmost row (below the HUD's top rows) where a frame differs from the pristine A1 by over
# SKY levels; C against B in the columns where both have one (> 0: C's skyline is lower on the screen). The window shift search of the
# first version read the whole structure's move, not the cell's centre, and is not kept.
def sky(F):
	m = np.abs(F - f["A1"]).max(axis=2) > a.sky
	for x0, y0, x1, y1 in masks: m[y0:y1, x0:x1] = False
	return [(int(np.nonzero(m[60:420, x])[0].min() + 60) if m[60:420, x].any() else None) for x in range(m.shape[1])]
sb, sc = sky(f["B1"]), sky(f["C1"])
cols = [x for x in range(0, len(sb), 10) if sb[x] is not None and sc[x] is not None and sb[x] != sc[x]]
print(f"skyline, C minus B (rows; > 0 is lower on the screen), every 10th column where they differ (levels over {a.sky} from A1):")
print("  " + ", ".join(f"x{x}: {sc[x] - sb[x]:+d}" for x in cols))
dv = [sc[x] - sb[x] for x in range(len(sb)) if sb[x] is not None and sc[x] is not None]
print(f"  every column: {sum(v > 0 for v in dv)} lower, {sum(v < 0 for v in dv)} higher, {sum(v == 0 for v in dv)} the same; largest {max(dv, key=abs):+d}")
