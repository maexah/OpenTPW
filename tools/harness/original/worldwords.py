#!/usr/bin/env python3
"""worldwords.py PID SECS OFFSET[:FMT[:NAME]] ... - any words of the running ORIGINAL's world, read-only, a line a
change with mGameTick (a POLL for loadfile.sh). FMT is a struct letter: H (default), I, i, f, B, Q.

  worldwords.py %PID% 60 0x1da72a:H:current 0x1da72c:H:size1 0x1da72e:H:size2 0x1da730:H:size3
        the arrival vehicle that is current and the three slots' things

The world is [0x0080239c]; mGameTick is its +0x1da70c. A thing's words are thingwords.py's."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]); want = []
for a in sys.argv[3:]:
    p = a.split(':'); want.append((int(p[0], 0), p[1] if len(p) > 1 and p[1] else 'H', p[2] if len(p) > 2 else p[0]))
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
last = None; t0 = time.time()
while time.time() - t0 < secs:
    try:
        world = rd(0x0080239c); now = {}
        for off, fmt, name in want:
            v = rd(world + off, fmt); now[name] = round(v, 3) if isinstance(v, float) else v
        tick = rd(world + 0x1da70c)
    except (OSError, struct.error): time.sleep(0.05); continue
    if now != last: print(f'{time.time() - t0:7.2f} s tick {tick}: {now}', flush=True); last = now
    time.sleep(0.03)
