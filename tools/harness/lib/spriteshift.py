#!/usr/bin/env python3
"""Where one sprite's own pixels stand in this game's frame against the original's, in the original's pixels (Q1108).

    spriteshift.py ORIG_REST.png ORIG_FRAME.png OURS_REST.png OURS_FRAME.png --box x0,y0,x1,y1
                   [--anchor ORIG_Y,OURS_Y] [--range N] [--threshold T]

Each game's sprite is its frame's pixels that differ from its rest frame (the sum of the three channels' differences
above T, 24) inside BOX, given in the original's 1024 x 768 pixels. This game's frames are laid on the original's
pixels as `sceneshift.py` lays them (the middle 4:3 scaled to 1024 x 768), so both sprites are measured on one grid.
For each game: the sprite's top and lowest changed rows, its height, and the mean row of its changed pixels (a pixel's
row is its middle, row + 0.5), which a threshold's edge moves far less than it moves the lowest row. Then the
vertical (and across) shift of this game's sprite that best matches the original's, the two masks correlated within
N (8) each way and the best row refined to a tenth by a parabola through its neighbours: dy above nought, this game
draws the sprite lower.

--anchor gives the point the sprite hangs from in each game's own rows (the original's 768, this game's frame's own,
e.g. 720; continuous rows, the frame's top edge 0, as `projpoint.py` and the console's `circle` print them): each
row is then also said as a height above that game's anchor, and the two heights compared.
"""
import sys
import numpy as np
from PIL import Image
from sceneshift import mapped


def mask(rest, frame, box, threshold):
	a = np.asarray(rest).astype(int)
	b = np.asarray(frame).astype(int)
	d = np.abs(b - a).sum(axis=2) > threshold
	x0, y0, x1, y1 = box
	m = np.zeros_like(d)
	m[y0:y1, x0:x1] = d[y0:y1, x0:x1]
	return m


def rows_of(m):
	ys, _ = np.nonzero(m)
	if len(ys) == 0:
		return None
	return ys.min(), ys.max(), float(ys.mean()) + 0.5, len(ys)


def shift(a, b, box, reach):
	x0, y0, x1, y1 = box
	target = a[y0:y1, x0:x1].astype(float)
	target = (target - target.mean()) / (target.std() + 1e-9)
	scores = {}
	for dy in range(-reach, reach + 1):
		for dx in range(-reach, reach + 1):
			part = b[y0 + dy:y1 + dy, x0 + dx:x1 + dx].astype(float)
			part = (part - part.mean()) / (part.std() + 1e-9)
			scores[(dx, dy)] = float((target * part).mean())
	(dx, dy), score = max(scores.items(), key=lambda kv: kv[1])
	fine = float(dy)
	if (dx, dy - 1) in scores and (dx, dy + 1) in scores:
		lo, mid, hi = scores[(dx, dy - 1)], score, scores[(dx, dy + 1)]
		bend = lo - 2 * mid + hi
		if bend < 0:
			fine = dy + 0.5 * (lo - hi) / bend
	return dx, dy, fine, score


def main(argv):
	args = list(argv)
	opts = {"--box": None, "--anchor": None, "--range": "8", "--threshold": "24"}
	for k in list(opts):
		if k in args:
			i = args.index(k); opts[k] = args[i + 1]; del args[i:i + 2]
	if len(args) != 4 or opts["--box"] is None:
		print(__doc__); return 2
	box = tuple(int(v) for v in opts["--box"].split(","))
	reach, threshold = int(opts["--range"]), int(opts["--threshold"])
	orig_rest, orig_frame = (Image.open(p).convert("RGB") for p in args[:2])
	size = orig_rest.size
	ours_h = Image.open(args[3]).size[1]
	ours_rest, ours_frame = (mapped(p, size) for p in args[2:])
	scale = size[1] / ours_h
	a, b = mask(orig_rest, orig_frame, box, threshold), mask(ours_rest, ours_frame, box, threshold)
	ra, rb = rows_of(a), rows_of(b)
	if ra is None or rb is None:
		print("no changed pixels in the box:", "the original's" if ra is None else "this game's"); return 1
	anchors = None
	if opts["--anchor"]:
		oy, uy = (float(v) for v in opts["--anchor"].split(","))
		anchors = (oy, uy * scale)
	for name, r, anchor in (("original", ra, anchors and anchors[0]), ("this game", rb, anchors and anchors[1])):
		top, low, mean, n = r
		line = f"{name:9}: rows {top}..{low} ({low - top + 1} tall), mean row {mean:.2f}, {n} px"
		if anchor is not None:
			line += f"; anchor {anchor:.2f}: lowest row's bottom {anchor - (low + 1):.2f} above it, mean {anchor - mean:.2f} above it"
		print(line)
	dx, dy, fine, score = shift(a, b, box, reach)
	print(f"best shift of this game's sprite: dx {dx:+d} dy {dy:+d} (refined {fine:+.2f}), correlation {score:.3f}")
	if anchors is not None:
		gap = anchors[1] - anchors[0]
		print(f"anchors: this game's stands {gap:+.2f} below the original's; the sprite about its anchor: this game's "
			f"{fine - gap:+.2f} lower by the shift, {(rb[2] - anchors[1]) - (ra[2] - anchors[0]):+.2f} by the mean row, "
			f"{(rb[1] - anchors[1]) - (ra[1] - anchors[0]):+.2f} by the lowest row (on the original's pixels)")
	return 0


if __name__ == "__main__":
	sys.exit(main(sys.argv[1:]))
