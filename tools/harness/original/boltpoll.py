#!/usr/bin/env python3
"""boltpoll.py PID SECS [STEP] - the ORIGINAL's lightning bolt records, read-only, a line a change (a POLL for loadfile.sh).

  boltpoll.py %PID% 120
        t=<s since start> n=<live bolts> life=<each live record's life left, ms> dt=<the frame's ms> col=<the diffuse
        colour word the bolt draw packed last> hash=<the shape hash>

The bolt renderer is [0x008bcbac] (FUN_0057ff60's `this`, called each frame from 0x00576dab): its record count at
+0x3308 (a draw past 16 is dropped, 0x0058032c), record i at +0x3008 + 0x30 i with the life left in ms at record
+0x24 (+0x302c, the first), the frame's elapsed ms at +0x9d9c, the shape hash at +0xa198 and the grey it packed for
the bolt it last drew at +0xa19c (FUN_0057ee30, 0x0057ef4f..0x0057ef61). docs/exe/weather.md, "The bolt".
A line is printed when a record's life, the count or the colour changes; STEP (default 0.002 s) is the poll. Bolts
come on a clear day with `thingpoke.py PID SECS 15 7:0x38:B=1`. Q1044."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]); step = float(sys.argv[3]) if len(sys.argv) > 3 else 0.002
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<I'):
    f.seek(a); return struct.unpack(fmt, f.read(struct.calcsize(fmt)))[0]
last = None; t0 = time.time()
while time.time() - t0 < secs:
    try:
        obj = rd(0x008bcbac)
        if not obj: time.sleep(0.05); continue
        n = rd(obj + 0x3308)
        life = [rd(obj + 0x302c + 0x30 * i, '<i') for i in range(min(n, 16))]
        now = (n, tuple(life), rd(obj + 0x9d9c, '<i'), rd(obj + 0xa19c), rd(obj + 0xa198))
    except (OSError, struct.error): time.sleep(0.05); continue
    if n and now[:2] != (last or (None, None))[:2] or (n and now[3] != last[3]):
        print(f't={time.time() - t0:8.3f} wall={time.time():.3f} n={n} life={list(now[1])} dt={now[2]} col=0x{now[3]:08x} hash=0x{now[4]:08x}', flush=True)
    elif last is not None and last[0] and not n:
        print(f't={time.time() - t0:8.3f} wall={time.time():.3f} n=0 (last life {list(last[1])})', flush=True)
    last = now
    time.sleep(step)
