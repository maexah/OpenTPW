#!/usr/bin/env python3
"""bytehold.py PID SECS ADDR=VALUE [TICK ADDR=VALUE] - the ORIGINAL's memory, WRITTEN: holds one byte of the image at a
value (written again whenever it reads otherwise, a line a write with mGameTick); with TICK and a second pair, once
mGameTick has read at or under TICK and then reads over it, the second value is held instead. A POLL for loadfile.sh.

  bytehold.py %PID% 110 0x0078d90d=0 900 0x0078d90d=1    the Options screen's Tutorial switch (DAT_0078d90d) off
                                                         through a load and until tick 900, then on

Only a static byte of the image: a thing's words are thingpoke.py's. Stop the original afterwards (the switch is an
option the game may keep)."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
def pair(t): a, v = t.split('='); return int(a, 0), int(v, 0)
a1, v1 = pair(sys.argv[3]); late = len(sys.argv) > 5
if late: T = int(sys.argv[4]); a2, v2 = pair(sys.argv[5])
f = open(f'/proc/{pid}/mem', 'r+b', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def hold(a, v, tick):
    was = rd(a, 'B')
    if was != v: f.seek(a); f.write(bytes([v])); print(f'{time.time() - t0:7.2f} s tick {tick}: byte {a:#x} {was} -> {v}', flush=True)
t0 = time.time(); under = False; over = False
while time.time() - t0 < secs:
    try:
        tick = rd(rd(0x0080239c) + 0x1da70c)
        if late and not over:
            if tick <= T: under = True
            elif under: over = True; print(f'{time.time() - t0:7.2f} s tick {tick}: past {T}', flush=True)
        if over: hold(a2, v2, tick)
        else: hold(a1, v1, tick)
    except (OSError, struct.error): pass
    time.sleep(0.02)
