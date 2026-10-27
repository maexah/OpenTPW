#!/usr/bin/env python3
"""staffpool.py PID SECS - the running ORIGINAL's staff pool (the hire screen's candidates), read-only, a block a change
with mGameTick (a POLL for loadfile.sh).

The pool object is [0x007cf83c]; its 32 records are 0x14 bytes from its start (FUN_00507bd0): +0 the kind (0 handyman,
1 mechanic, 2 entertainer, 3 guard, 4 researcher), +4 the name's row in the kind's names table (FUN_005074a0), +8 the
grade (byte; FUN_005077f0 pays by kind and grade), +10 in use (byte). Printed: each slot in use as
slot:kind/name row/grade, and the hire screen's tab [0x007ca30c] and screen pointer [0x007ca308] beside them.
The hire list is filled in slot order (FUN_0049b5b0), so the first of a kind printed is the one handed over first.
Resolve a name row with strdump on Language/English/<KIND>_NAMES.str (docs/TOOLING.md)."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
KINDS = 'HMEGR'
last = None; t0 = time.time()
while time.time() - t0 < secs:
    try:
        pool = rd(0x007cf83c); slots = []
        if pool:
            for s in range(32):
                r = pool + s * 0x14
                if rd(r + 10, 'B'):
                    slots.append(f'{s}:{KINDS[rd(r, "I")] if rd(r, "I") < 5 else rd(r, "I")}/{rd(r + 4, "I")}/{rd(r + 8, "B")}')
        now = (tuple(slots), rd(0x007ca30c), rd(0x007ca308) != 0)
        tick = rd(rd(0x0080239c) + 0x1da70c)
    except (OSError, struct.error): time.sleep(0.05); continue
    if now != last:
        print(f'{time.time() - t0:7.2f} s tick {tick}: hiretab {now[1]} hire screen {"up" if now[2] else "none"} pool {" ".join(now[0])}', flush=True)
        last = now
    time.sleep(0.05)
