#!/usr/bin/env python3
"""inside.py PID SECS [LEVEL [BELOW]] - the original's memory: the guests inside the park as the park analyser counts them
(FUN_004c9130: things of type 1 on a cell whose type FUN_004fa990 passes: 0, 1, 3, 9 or 10) and those of them whose
toilet need, cut to a whole number's low byte, is over LEVEL (0x004c9290; 60, FeaturesNoToilets.NeedingMoreThan,
unless given), with the share in whole per cent that the advisor's row 82 sets against 25, mParkClosed and
mGameTick; a line a change of the two counts or the door. Read-only. A POLL for loadfile.sh.
With BELOW, also the guests inside whose happiness (the float at +0x19c), cut the same way, is under BELOW, as
FUN_004c9750 counts them for the advisor's row 14 (asked with 30), with their share in whole per cent and the crowd
(FUN_004c81e0: half the guests inside, at most 100); a line a change of that count too (Q415).

A thing is [0x007cfb90 + id * 0x14]; its cell is bytes +5 (x) and +7 (y); a cell's record is
[0x008023a0] + (y * 128 + x) * 68 (the packed id less one, LEA at 0x004fa9b6) and its type the dword at +8; the need is the float at +0x1ac."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]); level = int(sys.argv[3]) if len(sys.argv) > 3 else 60
below = int(sys.argv[4]) if len(sys.argv) > 4 else None
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
last = None; t0 = time.time()
while time.time() - t0 < secs:
    try:
        world = rd(0x0080239c); tick = rd(world + 0x1da70c); closed = rd(world + 0x1da710); cells = rd(0x008023a0)
        inside = []; needing = []; out = 0; unhappy = 0
        for thing in range(1, 400):
            base = rd(0x007cfb90 + thing * 0x14)
            if not base or rd(base + 2, 'B') != 1: continue
            x = rd(base + 5, 'B'); y = rd(base + 7, 'B')
            if rd(cells + (y * 128 + x) * 68 + 8) not in (0, 1, 3, 9, 10): out += 1; continue
            inside.append(thing)
            if (int(rd(base + 0x1ac, 'f')) & 0xff) > level: needing.append(thing)
            if below is not None and (int(rd(base + 0x19c, 'f')) & 0xff) < below: unhappy += 1
    except (OSError, struct.error, ValueError, OverflowError): time.sleep(0.05); continue
    now = (closed, len(inside), tuple(needing), unhappy)
    if now != last:
        share = len(needing) * 100 // len(inside) if inside else -1
        print(f"{time.time() - t0:7.2f} s tick {tick}: inside {len(inside)} outside {out} needing {len(needing)} {needing} share {share} closed {closed}" + (f" unhappy {unhappy} share {unhappy * 100 // len(inside) if inside else -1} crowd {min(100, len(inside) // 2)}" if below is not None else ""), flush=True); last = now
    time.sleep(0.1)
