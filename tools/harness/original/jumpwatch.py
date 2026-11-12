#!/usr/bin/env python3
"""jumpwatch.py PID SECS [--happy H] [--hold [--delay D] [--near X,Y,R]] - the ORIGINAL's guests' spot animations, polled every 40 ms (Q1340).

A guest is a thing of type 1 ([0x007cfb90 + id * 0x14], +2); its state the int at +0x220, the state a spot animation
returns to +0x224, the animation it asked for the int at +0x10 (FUN_004217f0 on +0xc writes [ecx+4]), the tick it started
+0x208, its happiness the float at +0x19c, its cell bytes +5 and +7 (ride-operation.md, "Spot animations"). A line each
guest entering state 8: `SPOT thing N anim A from S tick T cell (x,y) happiness H`, and `JUMP ...` where A is 5 and S is 6
(the deciding turn's happy jump, 0x004fecea); `OVER thing N tick T back to S` as it leaves state 8.

--happy H WRITES every guest's happiness float as H once a second (an instrument, as this game's `happy H` console
command is; stop the original afterwards and never save under it). --hold, on the first JUMP, WRITES the game clock's
paused flag 0x007859a0 = 1, D seconds after it is seen (0), (memhold.py's: every person held in one pose while the frame goes on drawing), prints
`HELD thing N cell (x,y)` (--near: only a guest within R cells of (X,Y), so one on camera) and exits, leaving it held: aim and grab, then put it back with
`memhold.py PID 0 0x007859a0:I=0`. Without --hold it runs SECS and prints a count of spot animations by (anim, from)."""
import struct, sys, time, collections
pid = int(sys.argv[1]); secs = float(sys.argv[2]); args = sys.argv[3:]
happy = float(args[args.index('--happy') + 1]) if '--happy' in args else None
hold = '--hold' in args
delay = float(args[args.index('--delay') + 1]) if '--delay' in args else 0.0
near = [int(v) for v in args[args.index('--near') + 1].split(',')] if '--near' in args else None
f = open(f'/proc/{pid}/mem', 'r+b' if happy is not None or hold else 'rb', buffering=0)
def rd(a, fmt='I'):
	f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def guests():
	g = {}
	for i in range(1, 2000):
		p = rd(0x007cfb90 + i * 0x14)
		if not p: continue
		try:
			f.seek(p); b = f.read(0x228)
		except OSError: continue
		if b[2] != 1: continue
		g[i] = (p, struct.unpack_from('<i', b, 0x220)[0], struct.unpack_from('<i', b, 0x224)[0], struct.unpack_from('<i', b, 0x10)[0],
			struct.unpack_from('<I', b, 0x208)[0], struct.unpack_from('<f', b, 0x19c)[0], (b[5], b[7]))
	return g
def tick(): return rd(rd(0x0080239c) + 0x1da70c)
t0 = time.time(); last_write = 0; was = {}; counts = collections.Counter()
print(f"jumpwatch pid {pid} {secs} s happy {happy} hold {hold} tick {tick()} wall {t0:.3f}", flush=True)
while time.time() - t0 < secs:
	g = guests(); tk = tick()
	if happy is not None and time.time() - last_write > 1.0:
		for p, *_ in g.values():
			f.seek(p + 0x19c); f.write(struct.pack('<f', happy))
		last_write = time.time()
	for i, (p, st, back, anim, stamp, h, cell) in g.items():
		before = was.get(i)
		if st == 8 and (before is None or before != 8):
			kind = "JUMP" if anim == 5 and back == 6 else "SPOT"
			counts[(anim, back)] += 1
			print(f"{time.time() - t0:7.2f}s {kind} thing {i} anim {anim} from {back} tick {tk} stamp {stamp} cell {cell} happiness {h:.1f}", flush=True)
			if kind == "JUMP" and hold and (near is None or max(abs(cell[0] - near[0]), abs(cell[1] - near[1])) <= near[2]):
				time.sleep(delay)
				f.seek(0x007859a0); f.write(struct.pack('<I', 1))
				print(f"HELD thing {i} cell {cell} tick {tick()} state {rd(p + 0x220, 'i')} anim {rd(p + 0x10, 'i')}", flush=True); sys.exit(0)
		elif before == 8 and st != 8:
			print(f"{time.time() - t0:7.2f}s OVER thing {i} tick {tk} back to {st}", flush=True)
	was = {i: v[1] for i, v in g.items()}
	time.sleep(0.04)
print("spot animations by (anim, from):", dict(counts), flush=True)
