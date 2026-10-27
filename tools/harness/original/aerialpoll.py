#!/usr/bin/env python3
"""aerialpoll.py PID SECS - the aerial's controls in the running ORIGINAL as their window-animation streams play, read-only,
a line a change with the wall clock (a POLL for loadfile.sh; Q637).

The mast (control 0x2d) is [0x007cb2c4]; its children run from +0x5c through each one's +0x54 (FUN_0065d7d0,
FUN_0065d56b): the red top 0x2e first, then the tags. For each: the rectangle (four shorts at +0xc, the 2048 x 1536
layout), whether a stream plays (+0x135), its stream (+0xe8), its row (+0xec, a short) and the offset the stream has
added (+0xf8, four shorts) (FUN_0065e72d, FUN_0065e9a4; docs/exe/hud.md, "The window-animation engine")."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))
last = None; t0 = time.time()
while time.time() - t0 < secs:
    try:
        mast = rd(0x007cb2c4)[0]; kids = []; c = rd(mast + 0x5c)[0]
        while c and len(kids) < 12:
            playing = rd(c + 0x135, 'B')[0]
            kids.append((rd(c + 0xc, '4h'), playing, hex(rd(c + 0xe8)[0]) if playing else '-', rd(c + 0xec, 'h')[0] if playing else '-', rd(c + 0xf8, '4h')))
            c = rd(c + 0x54)[0]
        now = {'mast': rd(mast + 0xc, '4h'), 'children': kids}
    except (OSError, struct.error): time.sleep(0.01); continue
    if now != last: print(f'{time.time() - t0:7.3f} s wall {time.time():.3f}: {now}', flush=True); last = now
    time.sleep(0.005)
