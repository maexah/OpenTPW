#!/usr/bin/env python3
"""miserable.py PID SECS - WRITES the running ORIGINAL's memory: every guest (a thing of type 1: [0x007cfb90 + id*0x14] -> base, +2 the type byte) has its
happiness (float at +0x19c) written 0 every 0.5 s for SECS, so they leave by the deciding turn's miserable arm (leavers.py, Q668). Stop the original afterwards."""
import struct, sys, time
pid, secs = int(sys.argv[1]), float(sys.argv[2])
f = open(f'/proc/{pid}/mem', 'r+b', buffering=0)
def rd(a, fmt='<I'):
    f.seek(a); v = struct.unpack(fmt, f.read(struct.calcsize(fmt))); return v[0] if len(v) == 1 else v
t0 = time.time(); seen = set(); writes = 0
while time.time() - t0 < secs:
    for tid in range(1, 3000):
        try:
            base = rd(0x007cfb90 + tid * 0x14)
            if base and 0x1000000 < base < 0x7fffffff and rd(base + 2, '<B') == 1:
                f.seek(base + 0x19c); f.write(struct.pack('<f', 0.0)); writes += 1; seen.add(tid)
        except Exception: pass
    time.sleep(0.5)
print(f'{writes} writes over {len(seen)} guests', flush=True)
