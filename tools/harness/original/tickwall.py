#!/usr/bin/env python3
"""tickwall.py PID SECS - the original's mGameTick (the world [0x0080239c] + 0x1da70c) every 20 ms, a line each time it changes,
stamped with the WALL CLOCK (unix seconds), a fall marked FALL: tick.py's reading on a clock a film or a driver's own stamps
share (Q921). The park clock stands still while a Load Park runs (the load is one call, no frame is shown until it returns),
so the gap between the last tick before a click and the FALL after it is the load as the game's own clock sees it. Read-only."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a):
    f.seek(a); return struct.unpack('<I', f.read(4))[0]
t0 = time.time(); last = None
while time.time() - t0 < secs:
    try: tick = rd(rd(0x0080239c) + 0x1da70c)
    except (OSError, struct.error, ValueError): time.sleep(0.02); continue
    if tick != last:
        print(f"{time.time():.3f} tick {tick}" + (" FALL" if last is not None and tick < last else ""), flush=True); last = tick
    time.sleep(0.02)
