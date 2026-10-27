#!/usr/bin/env python3
"""trackride.py PID SECS [BT] - the running ORIGINAL's track rides (the karts, the water ride, the bumper rides), read-only,
one block on the first reading and on every change (Q581; docs/exe/park.md, "The karts' and the water ride's track").

For each record of the table [0x00877b60] (64 of 0xd0) with a BumperType (BT alone if given): its handle, the object
that holds it (+0x28) with that object's mIsTrackRideValid (+0x2c), duration, state, cars, seated, centre (+0x74,+0x78),
the unload point (+0x90,+0x94), the orientation (+0xb0), the race's first buoy (+0xa4); every buoy of its list (+0xa8,
the pool [0x00877b78] of 0x1c: flags, index, x, z, next, object) and every section (+0xbc, the pool [0x00877b80] of
0x28: type +4, x +0x14, z +0x18, its collision objects +0x1c, each flags/x/z/r1/r2 from [0x00877b70] of 0x20); and its
live cars (the pool [0x00877b68] of 0xac: flags, position, velocity, buoy +0x7c, timer +0x88, bonus +0x68,
patience +0x8c, heading +0x54, +0x58 (the karts' heading to turn to; a boat's spin), the turn +0x5c, mesh +4, clip +0x10).
Then the build tool's mode [0x0081ae2c] and anchor ([0x0081ede4], [0x0081ede8]), mGameTick and the track tick."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]); only = int(sys.argv[3]) if len(sys.argv) > 3 else None
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<i'):
	f.seek(a); return struct.unpack(fmt, f.read(struct.calcsize(fmt)))[0]
def holder(h):
	for t in range(1, 1200):
		e = rd(0x007cfb90 + t * 0x14, '<I')
		if e and rd(e + 2, '<B') == 3 and rd(e + 0x28) == h:
			return t, rd(e + 0x2c)
	return None, None
def objs(p):
	out = []
	for _ in range(16):
		if not p: break
		fl, x, z, r1, r2, nxt = rd(p, '<I'), rd(p + 4), rd(p + 8), rd(p + 12), rd(p + 16), rd(p + 20, '<I')
		out.append(f"[{fl:#x} ({x},{z}) {r1}/{r2}]"); p = nxt
	return ' '.join(out)
def snap():
	w = rd(0x007cf83c, '<I'); lines = [f"mGameTick {rd(w + 0x1da70c)} track tick {rd(0x00877b5c)} tool mode {rd(0x0081ae2c):#x} anchor ({rd(0x0081ede4)},{rd(0x0081ede8)})"]
	tab = rd(0x00877b60, '<I'); bpool = rd(0x00877b78, '<I'); cpool = rd(0x00877b68, '<I')
	if not tab: return lines + ["no table"]
	for s in range(64):
		r = tab + s * 0xd0; bt = rd(r)
		if not bt or (only is not None and bt != only): continue
		thing, valid = holder(s | (bt << 8))
		u = lambda o: rd(r + o)
		start = u(0xa4); startix = rd(start + 4) if start else None
		lines.append(f"slot {s} BumperType {bt} thing {thing} +0x2c {valid} duration {u(4)} state {u(0x50)} broken {u(0x54)} cars {u(0x5c)} seated {u(0x60)} most {u(0x64)} arrival {u(0x68)} perf {u(0x1c)} thrust {u(0x20)} friction {u(0x24)} turn {u(0x28)} restitution {u(0x2c)} centre ({u(0x74)},{u(0x78)}) unload ({u(0x90)},{u(0x94)}) orient {u(0xb0)} buoys {u(0xac)} race start buoy {startix}")
		b = u(0xa8); k = 0
		while b and k < 300:
			lines.append(f"    buoy {rd(b + 4)} flags {rd(b, '<I'):#x} ({rd(b + 8)},{rd(b + 12)}) object {'yes' if rd(b + 0x18) else 'no'}")
			b = rd(b + 0x14, '<I'); k += 1
		sec = u(0xbc); k = 0
		while sec and k < 300:
			lines.append(f"    section {k} type {rd(sec + 4) & 0xffff} flags {rd(sec + 4) >> 16:#x} cells ({rd(sec + 0x14) // 3072},{rd(sec + 0x18) // 3072}) at ({rd(sec + 0x14)},{rd(sec + 0x18)}) objects {objs(rd(sec + 0x1c, '<I'))}")
			sec = rd(sec + 0x24, '<I'); k += 1
		if cpool:
			for i in range(256):
				c = cpool + i * 0xac
				if rd(c, '<I') & 1 and rd(c + 0x9c, '<I') == r:
					lines.append(f"    car {i} flags {rd(c, '<I'):#x} at ({rd(c + 0x34)},{rd(c + 0x38)}) v ({rd(c + 0x3c)},{rd(c + 0x40)}) buoy {rd(c + 0x7c)} timer {rd(c + 0x88)} bonus {rd(c + 0x68)} patience {rd(c + 0x8c)} heading {rd(c + 0x54)} +0x58 {rd(c + 0x58)} turn {rd(c + 0x5c)} mesh {rd(c + 4)} anim {rd(c + 0x10)}")
	return lines
t0 = time.time(); last = None
while True:
	now = time.time() - t0
	try: ls = snap()
	except Exception as e: ls = [f"unreadable ({e})"]
	key = ls[1:] + [ls[0].split(' tool ')[1] if ' tool ' in ls[0] else '']
	if key != last:
		print(f"{now:8.2f} s " + "\n".join(ls)); sys.stdout.flush(); last = key
	if now >= secs: break
	time.sleep(0.25)
