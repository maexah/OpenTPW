#!/usr/bin/env python3
"""oneshots.py PID SECS [POLL_MS] - the ORIGINAL's one-shot voices (effect flags without 0x4, looped under 0x8), read-only (Q565).

A one-shot (0x006bd7b0, vtable 0x0070a3e8) holds the sample its set-up drew ([voice+0xc], the sample record, its length in
ms at +8) and a drop time at +0x1c: the wall clock at its set-up plus 250 plus that length (0x006bbf60). Each service
pass drops a voice flagged 0x10 (its channel ended by itself, 0x006bbe40) or, without voice flag 0x2, one whose drop time
has passed (0x006bcb60); a voice in range is ticked (0x006bd800), which starts a channel when neither 0x1 nor 0x10 is set.
Out of range it is flagged 0x800 and its channel given up (0x006bca70). Walks the service's voice list as voices.py does
(the container at [[0x00802bcc]+4]+0x20, head +0x24, next +0x34) every POLL_MS (default 10), and prints:

    +<ms> come <voice> effect <id> (0x<flags>) at (x,y,z) flags 0x<+0x30> channel <+0x24> sample <rec> length <ms>
          drop <+0x1c> (made = drop - 250 - length, in the game's clock)
    +<ms> flags <voice> effect <id> 0x<old> -> 0x<new> channel <+0x24>        (each change of the flags or the channel)
    +<ms> gone <voice> effect <id> after <ms> (flags 0x<last>, channels seen <n>, played: yes/no)

At the end per effect: how many came, how many ever had a channel, and the spread of each kind's life (seen to gone)
against its length + 250. The game's clock is not read: `offset` is the smallest (first seen - made) over all voices,
so `drop + offset` is when this script should see the voice go if the drop word decides it.
"""
import struct, sys, time, collections

pid = int(sys.argv[1]); secs = float(sys.argv[2]); poll = (float(sys.argv[3]) if len(sys.argv) > 3 else 10) / 1000
mem = open(f'/proc/{pid}/mem', 'rb', buffering=0)

def rd(a, fmt='I'):
	mem.seek(a); return struct.unpack('<' + fmt, mem.read(struct.calcsize('<' + fmt)))

def walk():
	cont = rd(rd(rd(0x00802bcc)[0] + 4)[0] + 0x20)[0]; v = rd(cont + 0x24)[0]; out = {}
	for _ in range(400):
		if not v: break
		rec, = rd(v + 4); flags, nxt = rd(v + 0x30, '2I')
		if rec:
			eid, = rd(rec); efl, = rd(rec + 0x10, 'H')
			if not efl & 0x4:
				smp, = rd(v + 0xc); length = rd(smp + 8)[0] if smp else -1
				out[v] = dict(eid=eid, efl=efl, flags=flags, chan=rd(v + 0x24)[0], smp=smp, length=length,
					drop=rd(v + 0x1c)[0], at=rd(v + 0x10, '3f'))
		v = nxt
	return out

t0 = time.time(); ms = lambda: int((time.time() - t0) * 1000)
live = {}; done = []; offset = None
while time.time() - t0 < secs:
	try: now = walk()
	except (OSError, struct.error): time.sleep(poll); continue
	t = ms()
	for v, r in now.items():
		if v not in live or live[v]['drop'] != r['drop'] or live[v]['eid'] != r['eid']:
			if v in live:
				print(f'+{t} gone {v:#x} effect {live[v]["eid"]} (slot reused)'); done.append(live.pop(v))
			r.update(seen=t, chans={r['chan']} - {0}, played=bool(r['flags'] & 1))
			made = r['drop'] - 250 - r['length']; off = t - made
			if not r['efl'] & 0x8: offset = off if offset is None else min(offset, off)   # a looped one is never dropped by time
			x, y, z = r['at']
			print(f'+{t} come {v:#x} effect {r["eid"]} (0x{r["efl"]:04x}) at ({x:.0f},{y:.0f},{z:.0f}) flags 0x{r["flags"]:05x} '
				f'channel {r["chan"]:#x} sample {r["smp"]:#x} length {r["length"]} drop {r["drop"]}', flush=True)
			live[v] = r
		else:
			o = live[v]
			if (o['flags'], o['chan']) != (r['flags'], r['chan']):
				print(f'+{t} flags {v:#x} effect {r["eid"]} 0x{o["flags"]:05x} -> 0x{r["flags"]:05x} channel {r["chan"]:#x}', flush=True)
				o['flags'], o['chan'] = r['flags'], r['chan']
				if r['chan']: o['chans'].add(r['chan'])
				if r['flags'] & 1: o['played'] = True
	for v in [v for v in live if v not in now]:
		o = live.pop(v); o['gone'] = t; done.append(o)
		print(f'+{t} gone {v:#x} effect {o["eid"]} after {t - o["seen"]} ms (flags 0x{o["flags"]:05x}, channels seen {len(o["chans"])}, '
			f'played: {"yes" if o["played"] else "no"}; length {o["length"]}, drop + offset = +{o["drop"] + (offset or 0)}'
			f' vs gone +{t})', flush=True)
	time.sleep(poll)

print(f'== end after {ms()} ms; offset {offset}; still live {len(live)}')
by = collections.defaultdict(list)
for o in done:
	if 'gone' in o: by[(o['eid'], o['efl'], o['played'])].append(o)
for (eid, efl, played), os_ in sorted(by.items()):
	lives = [o['gone'] - o['seen'] for o in os_]; over = [o['gone'] - o['seen'] - o['length'] - 250 for o in os_]
	late = [o['gone'] - (o['drop'] + offset) for o in os_]
	print(f'effect {eid} (0x{efl:04x}) played {"yes" if played else "no"}: {len(os_)} voices, life {min(lives)}..{max(lives)} ms, '
		f'life - (length + 250) {min(over)}..{max(over)} ms, gone - (drop + offset) {min(late)}..{max(late)} ms')
