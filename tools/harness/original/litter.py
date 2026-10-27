#!/usr/bin/env python3
"""litter.py PID SECS - the running ORIGINAL's litter share as FUN_004ccdf0 takes it for the advisor's rows 8 and 9
(Q417), read-only, a line a change with mGameTick (a POLL for loadfile.sh): every map cell ([0x0080239c] + 0x2d8 +
(y x 128 + x) x 0x44), counted where its mType (+0x8) is 1, 3 or 9 or its mLitter (+0x2c) is not nought, littered
where mLitter is not nought; share = littered x 100 // counted. Each line also lists the littered cells (x,y:kind)."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, n): f.seek(a); return f.read(n)
last = None; t0 = time.time()
while time.time() - t0 < secs:
    try:
        world = struct.unpack('<I', rd(0x0080239c, 4))[0]
        tick = struct.unpack('<I', rd(world + 0x1da70c, 4))[0]
        cells = rd(world + 0x2d8, 16384 * 0x44)
    except (OSError, struct.error, ValueError): time.sleep(0.2); continue
    if len(cells) < 16384 * 0x44: time.sleep(0.2); continue
    walked = 0; lit = []
    for i in range(16384):
        t, = struct.unpack_from('<i', cells, i * 0x44 + 0x8); k, = struct.unpack_from('<i', cells, i * 0x44 + 0x2c)
        if k: lit.append('%d,%d:%d' % (i % 128, i // 128, k))
        if k or t in (1, 3, 9): walked += 1
    now = (walked, tuple(lit))
    if now != last:
        print(f'{time.time() - t0:7.2f} s tick {tick}: litter littered={len(lit)} walked={walked} share={len(lit) * 100 // walked if walked else 0} cells={" ".join(lit[:40])}', flush=True); last = now
    time.sleep(0.5)
