#!/usr/bin/env python3
"""spriteheights.py OUT BIN SAMPLES... - the sprite pass's ground sampler, the original's against this game's (Q839).

SAMPLES are files of lines `spr kind K flags F x X z Z off O st0 S` that `original/spritesampler.sh` writes: a hardware breakpoint
(brk.sh) at 0x005420f3, the instruction after FUN_00542010's call to FUN_004527f0 (0x005420ee) for a sprite whose +0xc4 lacks
0x1000000, so st0 is the sampler the original asked at the sprite's +0x88, +0x90. This game, in one park (the console's
`ground X Z`, run through parkrun.py, BIN the build's folder): at every distinct (x, z) `stands` (ScapeHeight), `camera`
(CameraHeight) and `bilinear`. Prints how many samples each sampler matches to TOL, split into the flagged 0x800 cells and the
others, and every sample in a flagged cell with the three values and the near corner's plane (the rule before Q823: the cell's
nearest vertex plus the two edge slopes). Writes ours.json beside the log.

  ~/.cache/tpw-harnesses/review-codex/venv/bin/python -u spriteheights.py $OUT $BIN samples.txt ...

SHOT="X,Z,ZOOM,ROT" (the original's camera words, setcam.py's) also takes a frame of this game at that camera, paused, `ours-rest.png`: the ground a
person would stand on, to put beside the original's frame of a person held there (original/spritehold.py --stay).
"""
import sys, os, json, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
OUT = sys.argv[1]; BIN = sys.argv[2]; FILES = [os.path.expanduser(a) for a in sys.argv[3:]]; NAME = "Q839"
exec(open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "parkrun.py")).read())

TOL = float(os.environ.get("TOL", "0.0015"))
samples = []
for f in FILES:
	for l in open(f):
		m = re.match(r"spr kind (\S+) flags (\S+) x (\S+) z (\S+) off (\S+) st0 (\S+)", l)
		if m: samples.append((int(m.group(1)), int(m.group(2), 16), float(m.group(3)), float(m.group(4)), float(m.group(5)), float(m.group(6))))
print(f"{len(samples)} samples from {len(FILES)} files")

def ground(x, z):
	g = " ".join(reply(f"ground {x} {z}", 0, 0))
	m = re.search(r"flags 0x([0-9a-f]+): stands (-?[\d.]+) camera (-?[\d.]+) bilinear (-?[\d.]+)", g)
	return int(m.group(1), 16), float(m.group(2)), float(m.group(3)), float(m.group(4))

res = []
try:
	enter(); reply("pause", 0.3, 0)
	cache = {}; verts = {}
	for kind, flags, x, z, off, st0 in samples:
		k = (round(x, 3), round(z, 3))
		if k not in cache: cache[k] = ground(x, z)
		fl, stands, camera, bil = cache[k]
		if fl & 0x800:
			cx, cz = math.floor(x / 10), math.floor(z / 10)
			for i in (0, 1):
				for j in (0, 1):
					if (10 * (cx + i), 10 * (cz + j)) not in verts: verts[(10 * (cx + i), 10 * (cz + j))] = ground(10 * (cx + i), 10 * (cz + j))[1]
			fx, fz = x / 10 - cx, z / 10 - cz; i, j = round(fx), round(fz)
			h = lambda a, b: verts[(10 * (cx + a), 10 * (cz + b))]
			plane = h(i, j) + abs(fx - i) * (h(1 - i, j) - h(i, j)) + abs(fz - j) * (h(i, 1 - j) - h(i, j))
		else: plane = stands
		res.append(dict(kind=kind, flags=flags, x=x, z=z, off=off, st0=st0, cell=[math.floor(x / 10), math.floor(z / 10)], cflags=fl, stands=stands, camera=camera, bilinear=bil, plane=plane))
	json.dump(res, open(os.path.join(OUT, "ours.json"), "w"))
	for name, pick in (("flagged 0x800 cells", lambda r: r["cflags"] & 0x800), ("other cells", lambda r: not r["cflags"] & 0x800)):
		rs = [r for r in res if pick(r)]
		if not rs: print(f"{name}: no samples"); continue
		c = lambda key: sum(abs(r["st0"] - r[key]) <= TOL for r in rs)
		w = lambda key: max(abs(r["st0"] - r[key]) for r in rs)
		print(f"{name}: {len(rs)} samples ({len({tuple(r['cell']) for r in rs})} cells); original's st0 = stands at {c('stands')} (worst {w('stands'):.4f}), camera at {c('camera')} ({w('camera'):.4f}), bilinear at {c('bilinear')} ({w('bilinear'):.4f}), the near corner's plane at {c('plane')} ({w('plane'):.4f})")
	if os.environ.get("SHOT"):
		import bothcam
		cx, cz, zoom, rot = (float(v) for v in os.environ["SHOT"].split(","))
		cam = bothcam.console(cx, cz, zoom, rot); reply(cam, 0.4, 0); move(5, 5, "away"); shot("ours-rest", 0.4)
	for r in res:
		if r["cflags"] & 0x800: print("  flagged:", r["kind"], r["cell"], f"x {r['x']:.3f} z {r['z']:.3f} off {r['off']:.3f} st0 {r['st0']:.4f} stands {r['stands']:.4f} camera {r['camera']:.4f} bilinear {r['bilinear']:.4f} plane {r['plane']:.4f}")
finally:
	done()
