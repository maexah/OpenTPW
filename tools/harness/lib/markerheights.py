#!/usr/bin/env python3
"""markerheights.py OUT BIN ORIGDIR - the build markers' corner heights, the original's against this game's samplers (Q838).

ORIGDIR holds `g-*.txt`, each a run of original/markercorners.py (the original paused by memhold.py 0x007859a0:I=1, a carried
thing's squares, `--glide GX,GZ` holding the glide words so the corners are drawn at an offset). Each corner there is
y = wave + FUN_004527f0( x, z ) + the squares' record height (1.5, `squares.py`), so the sampler the original asked is y - wave - 1.5.
This game, in one park (the console's `ground X Y`, run through parkrun.py, BIN the build's folder): at every distinct point,
`stands` (ScapeHeight), `camera` (CameraHeight) and `bilinear`, and at the four corners of every cell the load flags 0x800 the
vertex heights. Prints, for the points in a flagged cell and for the others, how many each sampler matches to 0.0015 (the wave
table's one step, 0.0015, is the reading's own grain) and how many the near corner's plane matches (FUN_004527f0's unflagged rule, ours ScapeHeight before Q823; the markers' own
glide before Q823 was bilinear).
Writes ours.json (points) and corners.json (vertex heights) beside the log.

  <a python with Xlib> -u markerheights.py $OUT $BIN $ORIGDIR   (docs/TOOLING.md, "A harness run needs a python with Xlib and PIL")
"""
import sys, os, json, glob, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
OUT = sys.argv[1]; BIN = sys.argv[2]; ORIGDIR = os.path.expanduser(sys.argv[3]); NAME = "Q838"
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "parkrun.py")).read())

LIFT = 1.5     # the squares' record height (squares.py), added to the sampler in FUN_0053ddd0
TOL = 0.0015
pts = {}
for f in sorted(glob.glob(os.path.join(ORIGDIR, "g-*.txt"))):
	for l in open(f):
		m = re.match(r"mesh 0 x (\S+) z (\S+) y (\S+) wave (\S+)", l)
		if m:
			x, z, y, w = map(float, m.groups())
			pts[(round(x, 4), round(z, 4))] = y - w - LIFT
print(f"{len(pts)} distinct corner points from {len(glob.glob(os.path.join(ORIGDIR, 'g-*.txt')))} reads")

def ground(x, z):
	g = " ".join(reply(f"ground {x} {z}", 0, 0))
	m = re.search(r"flags 0x([0-9a-f]+): stands (-?[\d.]+) camera (-?[\d.]+) bilinear (-?[\d.]+)", g)
	return int(m.group(1), 16), float(m.group(2)), float(m.group(3)), float(m.group(4))

try:
	enter(); reply("pause", 0.3, 0)
	ours = {p: ground(*p) for p in pts}
	flagged = sorted({(math.floor(x / 10), math.floor(z / 10)) for (x, z), (fl, *_ ) in ours.items() if fl & 0x800})
	verts = {(10 * (cx + i), 10 * (cz + j)): ground(10 * (cx + i), 10 * (cz + j))[1] for cx, cz in flagged for i in (0, 1) for j in (0, 1)}
	json.dump([[x, z, s, *ours[(x, z)]] for (x, z), s in pts.items()], open(os.path.join(OUT, "ours.json"), "w"))
	json.dump([[x, z, h] for (x, z), h in verts.items()], open(os.path.join(OUT, "corners.json"), "w"))
	tally = {True: [0, 0, 0, 0, 0, 0.0, 0.0], False: [0, 0, 0, 0, 0, 0.0, 0.0]}   # n, stands, camera, bilinear, old, worst old, worst bilinear
	for (x, z), s in pts.items():
		fl, stands, camera, bil = ours[(x, z)]
		isf = bool(fl & 0x800); t = tally[isf]; t[0] += 1
		t[1] += abs(stands - s) <= TOL; t[2] += abs(camera - s) <= TOL; t[3] += abs(bil - s) <= TOL
		t[6] = max(t[6], abs(bil - s))
		if isf:
			cx, cz = math.floor(x / 10), math.floor(z / 10); fx, fz = x / 10 - cx, z / 10 - cz
			h = lambda i, j: verts[(10 * (cx + i), 10 * (cz + j))]
			old = h(0, 0) + fx * (h(1, 0) - h(0, 0)) + fz * (h(0, 1) - h(0, 0))   # the floor corner's plane (truncated index)
			t[4] += abs(old - s) <= TOL; t[5] = max(t[5], abs(old - s))
	for isf in (True, False):
		n, a, b, c, d, wo, wb = tally[isf]
		print(f"{'flagged 0x800 cells' if isf else 'other cells'}: {n} points; the original's sampler = stands at {a}, camera at {b}, bilinear at {c} (worst {wb:.4f})"
			+ (f", the near corner's plane at {d} (worst {wo:.4f})" if isf else ""))
	print("flagged cells met:", flagged)
finally:
	done()
