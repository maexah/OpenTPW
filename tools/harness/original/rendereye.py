#!/usr/bin/env python3
"""rendereye.py PID [SECS] [POLL_MS] - Q749: the ORIGINAL's lobby render camera, a line each time it changes: the wall clock,
the object's address (the global [0x00f83778]; FrontEnd_Init builds a new one on every entry to the lobby, 0x005d5ab3..0x005d5ace),
its eye +8/+0xc/+0x10, its look point +0x14/+0x18/+0x1c, its angle +4, its fade word +0x60 and the delta [0x00f82884]+8.
The island camera copies the eye and the look point from here in its constructor (0x005dfd3c..0x005dfd99) and writes its eased
eye and look point back every pass (FUN_005e1210, 0x005e1308..0x005e133b), so the first row after the pointer changes whose
values are finite (the new object's first row or two are uninitialised memory, eye around 1e24) is the seed. Read-only. The first line is the wall clock of the start (time.time(), against record.sh's NAME.t0)."""
import struct, sys, time

SCENE, LENS = 0x00f82884, 0x00f83778
pid = int(sys.argv[1]); secs = float(sys.argv[2]) if len(sys.argv) > 2 else 60
poll = (float(sys.argv[3]) if len(sys.argv) > 3 else 1) / 1000
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)


def rd(a, fmt='I'):
	f.seek(a)
	return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]


t0 = time.time()
print(f'start wall {t0:.6f}', flush=True)
last = None
while time.time() - t0 < secs:
	try:
		obj = rd(LENS)
		if obj < 0x10000 or obj >= 0x80000000:
			row = (obj,)
		else:
			f.seek(obj)
			raw = f.read(0x64)
			eye = struct.unpack_from('<3f', raw, 8)
			look = struct.unpack_from('<3f', raw, 0x14)
			angle = struct.unpack_from('<f', raw, 4)[0]
			fade = struct.unpack_from('<I', raw, 0x60)[0]
			delta = rd(rd(SCENE) + 8, 'f')
			row = (obj, eye, look, angle, fade, delta)
	except (OSError, struct.error):
		row = ('unreadable',)
	if row != last:
		now = time.time()
		if len(row) == 1:
			print(f'{now:.4f} {now - t0:8.3f}s render camera {row[0]}', flush=True)
		else:
			obj, eye, look, angle, fade, delta = row
			print(f'{now:.4f} {now - t0:8.3f}s obj 0x{obj:08x} eye ({eye[0]:.3f}, {eye[1]:.3f}, {eye[2]:.3f}) '
				f'look ({look[0]:.3f}, {look[1]:.3f}, {look[2]:.3f}) angle {angle:.2f} fade 0x{fade:x} delta {delta:.4f}', flush=True)
		last = row
	time.sleep(poll)
