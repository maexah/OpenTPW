#!/usr/bin/env python3
"""particleheights.py PID SECS EFFECT [STEP] - the ORIGINAL: over SECS, read every STEP seconds (default 0.05), every particle
(at most 200) of each emitter of EFFECT: the highest height seen over the emitter's own, the widest step aside, and
the least and most particles, a line a slot at the end."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]); want = int(sys.argv[3]); f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<I'):
    f.seek(a); v = struct.unpack(fmt, f.read(struct.calcsize(fmt))); return v[0] if len(v) == 1 else v
top = {}; side = {}; counts = {}; t0 = time.time()
while time.time() - t0 < secs:
    pool = rd(0x0080cef8); s = rd(0x00816d1c, '<h')
    for _ in range(120):
        if s < 0 or s >= 120: break
        e = 0x0080cefc + 0x140 * s
        if rd(e + 0xc, '<h') == want:
            ex, ey, ez = rd(e + 0x14, '<3i'); p = rd(e + 6, '<h'); n = 0
            while p >= 0 and n < 200:
                a = pool + p * 0x34; x, y, z = rd(a, '<3i')
                top[s] = max(top.get(s, 0), (y - ey) / 64); side[s] = max(side.get(s, 0), abs(x - ex) / 64, abs(z - ez) / 64)
                p = rd(a + 0x30, '<h'); n += 1
            counts.setdefault(s, set()).add(rd(e + 8, '<H'))
        s = rd(e + 0xd0, '<h')
    time.sleep(float(sys.argv[4]) if len(sys.argv) > 4 else 0.05)
for s in top: print(f"slot {s}: highest {top[s]:.1f} over the emitter, widest {side[s]:.1f} aside, particles {min(counts[s])}..{max(counts[s])}")
