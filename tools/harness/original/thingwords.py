#!/usr/bin/env python3
"""thingwords.py PID SECS THING:OFFSET[:FMT[:NAME]] ... - any words of any things of the running ORIGINAL, read-only,
a line a change with mGameTick (a POLL for loadfile.sh). FMT is a struct letter: H (default), I, i, f, B.

  thingwords.py %PID% 60 2:0xc:H:cursor 26:0x19c:I:state 26:0x218:H:job 26:0x214:I:left
        the mechanics' HQ's search cursor (thing 2 +0xc) beside mechanic 26's state, job and count

A thing is [0x007cfb90 + id * 0x14]; mGameTick is [[0x0080239c] + 0x1da70c]. Use it before writing a new poller for
a field or two; a structure with lists of its own (cars, emitters, heads) has its own poller."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]); want = []
for a in sys.argv[3:]:
    p = a.split(':'); want.append((int(p[0]), int(p[1], 0), p[2] if len(p) > 2 and p[2] else 'H', p[3] if len(p) > 3 else f'{p[0]}+{p[1]}'))
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
last = None; t0 = time.time()
while time.time() - t0 < secs:
    try:
        now = {}
        for thing, off, fmt, name in want:
            base = rd(0x007cfb90 + thing * 0x14); v = rd(base + off, fmt) if base else None
            now[name] = round(v, 3) if isinstance(v, float) else v
        tick = rd(rd(0x0080239c) + 0x1da70c)
    except (OSError, struct.error): time.sleep(0.05); continue
    if now != last: print(f'{time.time() - t0:7.2f} s tick {tick}: {now}', flush=True); last = now
    time.sleep(0.03)
