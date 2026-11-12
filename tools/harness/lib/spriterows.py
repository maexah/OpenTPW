#!/usr/bin/env python3
"""spriterows.py REST.png X0,Y0,X1,Y1 FRAME.png... [--gap N] : the pixels of each FRAME that differ from REST by more than 24
(the three channels' differences summed) inside the box, split into bands of rows: a run of at least N (4) unchanged rows
ends a band. One line a frame: the count in the box and in the whole frame, then each band's rows, columns and count, top
first. A sprite drawn above another (a balloon over the person holding it) is two bands, so the rows between them, and a
sprite's lowest row against a projected point, are read off the line (Q1106). Take REST and FRAME in ONE session at one
camera, beside a second rest frame as the control (0 changed in the box)."""
import sys
import numpy as np
from PIL import Image

args = sys.argv[1:]
gap = 4
if "--gap" in args:
	i = args.index("--gap")
	gap = int(args[i + 1])
	del args[i:i + 2]
if len(args) < 3:
	sys.exit(__doc__)


def load(path):
	return np.asarray(Image.open(path).convert("RGB")).astype(int)


rest = load(args[0])
x0, y0, x1, y1 = (int(v) for v in args[1].split(","))
for path in args[2:]:
	changed = np.abs(load(path) - rest).sum(axis=2) > 24
	box = np.zeros_like(changed)
	box[y0:y1, x0:x1] = changed[y0:y1, x0:x1]
	bands = []
	for row in np.nonzero(box.any(axis=1))[0]:
		if bands and row - bands[-1][1] <= gap:
			bands[-1][1] = row
		else:
			bands.append([row, row])
	said = []
	for top, bottom in bands:
		ys, xs = np.nonzero(box[top:bottom + 1])
		said.append(f"rows {top}..{bottom} cols {xs.min()}..{xs.max()} n {len(xs)}")
	print(path.split("/")[-1], f"changed in box {int(box.sum())} (whole frame {int(changed.sum())}):",
		" | ".join(said) if said else "none")
