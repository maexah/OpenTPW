#!/usr/bin/env python3
"""tick.py PID SECS - the original's memory: mGameTick (the world [0x0080239c] + 0x1da70c) every 50 ms, a line each time it
changes, stamped from the start; a tick that FALLS is marked "FALL", which is the moment a Load Park click's file is read
in. Read-only. A POLL for loadfile.sh beside another reader that prints only on its own changes (inside.py, thingwords.py):
POLL="python3 -u tick.py %PID% 25 & python3 -u inside.py %PID% 25; wait" (Q241j, q241j/orig/b-load.log)."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a):
    f.seek(a); return struct.unpack('<I', f.read(4))[0]
t0 = time.time(); last = None
while time.time() - t0 < secs:
    try: tick = rd(rd(0x0080239c) + 0x1da70c)
    except (OSError, struct.error, ValueError): time.sleep(0.05); continue
    if tick != last:
        print(f"{time.time() - t0:7.2f} s tick {tick}" + (" FALL" if last is not None and tick < last else ""), flush=True); last = tick
    time.sleep(0.05)
