#!/usr/bin/env python3
"""memhold.py PID SECS ADDR[:fmt]=VALUE ... - the ORIGINAL's memory, WRITTEN: holds each static word of the image at a
value for SECS (written again whenever it reads otherwise, a line a write), then puts back what each held before and
says so. fmt as tpwmem.py's: I (u32, default) i H h B f; VALUE in the same kind (0x.. allowed for the integers).

  memhold.py %PID% 20 0x008bd490:f=10000 0x008bd494:f=20000   the fog's start and end pushed past everything (Q479)

ADDR is hex, with or without 0x. Only a static word of the image: a thing's words are thingpoke.py's, one byte with a
tick switch is bytehold.py's. SECS 0 writes once, prints, and restores nothing (`restore` by hand after)."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
def pair(t):
	a, v = t.split('='); a, _, fmt = a.partition(':'); fmt = fmt or 'I'
	return int(a, 16), fmt, (float(v) if fmt in 'fd' else int(v, 0))
held = [pair(t) for t in sys.argv[3:]]
f = open(f'/proc/{pid}/mem', 'r+b', buffering=0)
def rd(a, fmt):
	f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def wr(a, fmt, v):
	f.seek(a); f.write(struct.pack('<' + fmt, v))
was = {a: rd(a, fmt) for a, fmt, _ in held}
for a, fmt, v in held: print(f'{a:#010x}:{fmt} was {was[a]} -> {v}', flush=True)
t0 = time.time()
while True:
	for a, fmt, v in held:
		now = rd(a, fmt)
		if now != v and not (fmt in 'fd' and abs(now - v) < 1e-6 * max(1, abs(v))):
			wr(a, fmt, v)
			if time.time() - t0 > 0.05: print(f'{time.time() - t0:7.2f} s: {a:#010x} read {now}, written {v} again', flush=True)
	if time.time() - t0 >= secs: break
	time.sleep(0.01)
if secs > 0:
	for a, fmt, _ in held: wr(a, fmt, was[a]); print(f'{a:#010x}:{fmt} restored to {was[a]}', flush=True)
