#!/usr/bin/env python3
"""chains.py PID SECS [POLL_MS] - the ORIGINAL's chain voices and their children, read-only (Q558).

A chain is a voice of effect flags 0x4 without 0x2: 0x404 the held chain (0x006bdd30, the screams, the level ambient's
177-180, the staff's 188) and 0x4 alone (Voice_MakeAnyChain 0x006bdc60: the rides' 89 and 100). It plays nothing itself;
its children hang from [voice+0x4c]+0x18, each child's next from [child+0x4c]+0x18 (0x006bdb50). Walks the service's
voice list as voices.py does (the container at [[0x00802bcc]+4]+0x20, head +0x24, next +0x34) every POLL_MS (default
20), and prints a line each time a chain comes or goes, and each time a child is made:

    +<ms since start> chain <voice> effect <id> (0x<flags>) at (x,y,z) flags 0x<voice flags> key <ctl +0> param <ctl +4>
          own v<k> (the chain's own +0x8, which its next child draws from)
    +<ms> child <addr> of <voice> effect <id>: time <+0x1c> (+<time - previous child's time> ms) sample <rec +0xc>
          variation <+0x8> (v<k>, its place among the effect's from 1) channel <+0x24> flags 0x<+0x30>
    +<ms> gone <voice> effect <id>

At the end, per effect: children made, distinct sample records, the variations' counts and how often a child took the
variation of the child before (each by its v<k>; Q559), and the spread of the gaps between children's times
(a child's time is its start plus its wait, 0x006c3a80; the next child is made on the first service pass past it, so a
gap is the next child's wait plus up to one pass). The wall clock is this script's, not the game's.
"""
import struct, sys, time, collections

pid = int(sys.argv[1]); secs = float(sys.argv[2]); poll = (float(sys.argv[3]) if len(sys.argv) > 3 else 20) / 1000
mem = open(f'/proc/{pid}/mem', 'rb', buffering=0)

def rd(a, fmt='I'):
	mem.seek(a); return struct.unpack('<' + fmt, mem.read(struct.calcsize('<' + fmt)))

def chains():
	out = {}
	cont = rd(rd(rd(0x00802bcc)[0] + 4)[0] + 0x20)[0]; v = rd(cont + 0x24)[0]
	for _ in range(400):
		if not v: break
		rec, = rd(v + 4); flags, nxt = rd(v + 0x30, '2I')
		eid, fl = (rd(rec)[0], rd(rec + 0x10, 'H')[0]) if rec else (-1, 0)
		if fl & 0x4 and not fl & 0x2:
			kids = []; c = rd(rd(v + 0x4c)[0] + 0x18)[0]
			for _ in range(64):
				if not c: break
				blk, = rd(c + 0x4c)
				kids.append((c, rd(c + 0x1c)[0], rd(c + 0xc)[0], rd(c + 0x8)[0], rd(c + 0x24)[0], rd(c + 0x30)[0]))
				c = rd(blk + 0x18)[0]
			ctl, = rd(v + 0x4c); base, = rd(rec + 8)
			out[v] = (eid, fl, flags, rd(v + 0x10, '3f'), kids, base, (rd(ctl, 'B')[0], rd(ctl + 4, 'B')[0]), rd(v + 8)[0])
		v = nxt
	return out

start = time.time(); seen = {}; last_time = {}; made = collections.Counter(); samples = collections.defaultdict(set)
gaps = collections.defaultdict(list); kids_seen = {}; last_var = {}
variations = collections.defaultdict(collections.Counter); repeats = collections.Counter()
def vk(var, base): return 'v%d' % ((var - base) // 0x2a + 1) if var and base and (var - base) % 0x2a == 0 else 'v?'
def ms(): return '+%7d' % ((time.time() - start) * 1000)
while time.time() - start < secs:
	try:
		now = chains()
	except (OSError, struct.error):
		time.sleep(poll); continue
	for v, (eid, fl, flags, at, kids, base, (key, param), own) in now.items():
		if v not in seen:
			print('%s chain %#x effect %d (0x%04x) at (%.0f,%.0f,%.0f) flags 0x%x key %d param %d own %s' % (
				ms(), v, eid, fl, *at, flags, key, param, vk(own, base)), flush=True)
		for (c, t, s, var, ch, cf) in kids:
			if (v, c, t) in kids_seen: continue
			kids_seen[(v, c, t)] = True
			gap = t - last_time[v] if v in last_time else None
			if gap is not None: gaps[eid].append(gap)
			last_time[v] = t; made[eid] += 1; samples[eid].add(s); k = vk(var, base); variations[eid][k] += 1
			if v in last_var and last_var[v] == k: repeats[eid] += 1
			last_var[v] = k
			print('%s child %#x of %#x effect %d: time %d (%s) sample %#x variation %#x (%s) channel %#x flags 0x%x' % (
				ms(), c, v, eid, t, '+%d ms' % gap if gap is not None else 'first', s, var, k, ch, cf), flush=True)
	for v, (eid, fl, flags, at, kids, *_) in seen.items():
		if v not in now:
			print('%s gone %#x effect %d' % (ms(), v, eid), flush=True)
			last_time.pop(v, None); last_var.pop(v, None)
	seen = now
	time.sleep(poll)
for eid in sorted(made):
	g = sorted(gaps[eid])
	print('effect %d: %d children, %d distinct samples, gaps %s; variations %s, a repeat of the one before %d' % (
		eid, made[eid], len(samples[eid]), '%d to %d, median %d over %d' % (g[0], g[-1], g[len(g) // 2], len(g)) if g else 'none',
		' '.join('%s:%d' % kv for kv in sorted(variations[eid].items())), repeats[eid]))
