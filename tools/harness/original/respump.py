#!/usr/bin/env python3
"""respump.py PID [SECS] [POLL_MS] - the ORIGINAL's research screen pumps, live, read-only (Q705).

The screen's tree is [0x007cc1d8] (nought while it is shut; ResearchScreen_Open 0x004aa480). A control's first child is
at +0x5c, its next sibling at +0x54, its id at +0x8c, its sixteen user slots from +0xa0 (FUN_0065e6d5). Each of the five
panels (0xd2d01 rides, 0xd2d07 upgrades, 0xd2d08 shops, 0xd2d09 sideshows, 0xd2d0a miscellaneous) keeps the pump in its
slots: 0 the position (16.16; the frame is its high word), 1 a clock stamp written on each UI pass that moves it, 2 the
speed ((rate + 20) x 0x10000 / 100), 3 the state (0 stopped at 0x30, 1 running 0x00-0x1f, 2 stopping 0x20-0x2f, 3
starting 0x30-0x3f; ResearchPump_Tick 0x004a9fc0, on message 0x1e once a pass). docs/exe/hud.md, "The pump".

One block without SECS: each panel's slots. With SECS: a poll every POLL_MS (default 2), a line each time a panel's
state changes, and at the end, per panel that moved, the passes counted (each new stamp is one pass), passes a second
over the wall clock, frames a second (the position's advance, its wraps counted), the stamps' step in their own units
(timeGetTime's milliseconds where it is that) and mGameTick at both ends. Prints 'shut' while the screen is.
"""
import struct, sys, time, collections

PANELS = [(0xd2d01, 'rides'), (0xd2d07, 'upgrades'), (0xd2d08, 'shops'), (0xd2d09, 'sideshows'), (0xd2d0a, 'misc')]
pid = int(sys.argv[1]); secs = float(sys.argv[2]) if len(sys.argv) > 2 else 0
poll = (float(sys.argv[3]) if len(sys.argv) > 3 else 2) / 1000
mem = open(f'/proc/{pid}/mem', 'rb', buffering=0)

def rd(a, fmt='I'):
	mem.seek(a); v = struct.unpack('<' + fmt, mem.read(struct.calcsize('<' + fmt))); return v[0] if len(v) == 1 else v

def tick():
	return rd(rd(0x007cf83c) + 0x1da70c)

def find(root):
	found = {}
	stack = [root]
	seen = 0
	while stack and seen < 2000:
		c = stack.pop(); seen += 1
		if not c: continue
		cid = rd(c + 0x8c)
		if cid in dict(PANELS): found[cid] = c
		stack.append(rd(c + 0x54) if c != root else 0)
		stack.append(rd(c + 0x5c))
	return found

def slots(c):
	return rd(c + 0xa0, '4i')

def main():
	root = rd(0x007cc1d8)
	if not root:
		print('shut'); return
	panels = find(root)
	print(f'tree 0x{root:08x} id 0x{rd(root + 0x8c):x} panels ' + ' '.join(f'{n} 0x{panels.get(i, 0):08x}' for i, n in PANELS))
	for i, n in PANELS:
		if i in panels:
			p, s, v, st = slots(panels[i])
			print(f'  {n:9s} pos 0x{p:08x} frame {p >> 16} stamp {s} speed {v} state {st}')
	if not secs: return
	t0 = time.time(); k0 = tick()
	last = {i: slots(panels[i]) for i, _ in PANELS if i in panels}
	passes = collections.Counter(); adv = collections.Counter(); steps = collections.defaultdict(list); first = {}
	while time.time() - t0 < secs:
		if rd(0x007cc1d8) != root:
			print(f'+{(time.time() - t0) * 1000:.0f} the screen shut'); break
		for i, n in PANELS:
			if i not in panels: continue
			now = slots(panels[i]); was = last[i]
			if now[3] != was[3]:
				print(f'+{(time.time() - t0) * 1000:.0f} {n} state {was[3]} -> {now[3]} frame {now[0] >> 16} speed {now[2]}')
			if now[1] != was[1]:
				passes[i] += 1; steps[i].append(now[1] - was[1]); first.setdefault(i, time.time())
				d = now[0] - was[0]
				if d < 0:
					# a wrap: state 1 back to 0 from past 0x1f, or state 3 into state 1 from past 0x3f
					d = now[0] + ((0x20 << 16) - was[0] if was[3] == 1 else (0x40 << 16) - was[0])
				adv[i] += d
			last[i] = now
		time.sleep(poll)
	wall = time.time() - t0; k1 = tick()
	print(f'over {wall:.2f} s of the wall clock, mGameTick {k0} to {k1}')
	for i, n in PANELS:
		if passes[i]:
			st = sorted(steps[i]); mid = st[len(st) // 2]
			print(f'  {n:9s} passes {passes[i]} = {passes[i] / wall:.2f} a second, frames {adv[i] / 65536:.1f} = {adv[i] / 65536 / wall:.2f} a second, '
				f'stamp step median {mid} min {st[0]} max {st[-1]} (sum {sum(st)})')

main()
