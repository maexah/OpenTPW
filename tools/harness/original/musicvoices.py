#!/usr/bin/env python3
"""musicvoices.py PID SECS [EFFECTS] [POLL_MS] - the ORIGINAL's voices of effect flags 0x4|0x2, read-only (Q561).

Those are the music's class (with 0x400: FUN_006be610, vtable 0x0070a498 - music 2, kids 91, the bus's global ambient
6) and the plain class (FUN_006be090, 0x0070a338 - the gulls, the sea, the rides' 0x0006 loops): one voice that plays
one sample after another. Walks the service's voice list as voices.py does (the container at [[0x00802bcc]+4]+0x20,
head +0x24, next +0x34) every POLL_MS (default 20) and prints a line when one comes, goes, or changes any of: its
variation (+0x8, as v<k> among the effect's from 1), its sample record (+0xc), its channel (+0x24), its own flags
(+0x30: 0x40 parked, 0x800 out of range), its controllers ([+0x4c]: four keys, four values; slot 0 is the zone
parameter, 0x006be5e0) or its place (+0x10, whole units):

    +<ms> come|chg <voice> effect <id> (0x<flags>) v<k> sample <rec> ch <channel> flags 0x<f> keys k0,k1,k2,k3 vals v0,v1,v2,v3 at (x,y,z)
    +<ms> gone <voice> effect <id>

EFFECTS (comma list, default every one) keeps only those effect ids. At the end, per effect, the order of the
variations each voice went through. The wall clock is this script's, not the game's.
"""
import struct, sys, time, collections

pid = int(sys.argv[1]); secs = float(sys.argv[2])
only = set(int(x) for x in sys.argv[3].split(',')) if len(sys.argv) > 3 and sys.argv[3] else None
poll = (float(sys.argv[4]) if len(sys.argv) > 4 else 20) / 1000
mem = open(f'/proc/{pid}/mem', 'rb', buffering=0)

def rd(a, fmt='I'):
	mem.seek(a); return struct.unpack('<' + fmt, mem.read(struct.calcsize('<' + fmt)))

def vk(var, base): return 'v%d' % ((var - base) // 0x2a + 1) if var and base and (var - base) % 0x2a == 0 else 'v?'

def voices():
	out = {}
	cont = rd(rd(rd(0x00802bcc)[0] + 4)[0] + 0x20)[0]; v = rd(cont + 0x24)[0]
	for _ in range(400):
		if not v: break
		rec, = rd(v + 4); flags, nxt = rd(v + 0x30, '2I')
		eid, fl = (rd(rec)[0], rd(rec + 0x10, 'H')[0]) if rec else (-1, 0)
		if fl & 0x6 == 0x6 and (only is None or eid in only):
			ctl, = rd(v + 0x4c); base, = rd(rec + 8)
			kv = rd(ctl, '8B') if ctl else (0,) * 8
			at = tuple(int(c) for c in rd(v + 0x10, '3f'))
			out[v] = (eid, fl, vk(rd(v + 8)[0], base), rd(v + 0xc)[0], rd(v + 0x24)[0], flags, kv, at)
		v = nxt
	return out

start = time.time(); seen = {}; order = collections.defaultdict(list)
def ms(): return '+%7d' % ((time.time() - start) * 1000)
while time.time() - start < secs:
	try:
		now = voices()
	except (OSError, struct.error):
		time.sleep(poll); continue
	for v, st in now.items():
		if seen.get(v) != st:
			eid, fl, k, s, ch, f, kv, at = st
			print('%s %s %#x effect %d (0x%04x) %s sample %#x ch %#x flags 0x%x keys %d,%d,%d,%d vals %d,%d,%d,%d at (%d,%d,%d)' % (
				ms(), 'come' if v not in seen else 'chg ', v, eid, fl, k, s, ch, f, *kv, *at), flush=True)
			if not order[v] or order[v][-1] != k: order[v].append(k)
	for v, st in seen.items():
		if v not in now: print('%s gone %#x effect %d' % (ms(), v, st[0]), flush=True)
	seen = now
	time.sleep(poll)
for v, ks in order.items():
	print('voice %#x: %s' % (v, ' '.join(ks)))
