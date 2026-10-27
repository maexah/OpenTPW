#!/usr/bin/env python3
"""campoi.py - poll the running ORIGINAL's park camera point of interest, its scroll terms and the map's edges (Q625).

  campoi.py PID [SECONDS] [INTERVAL]      SECONDS default 5, INTERVAL default 0.03; SECONDS 0 prints one line

First line: the landscape [[0x007a0854]+0x6c]: its counts +0x18/+0x1c, its sizes +0x10/+0x14 and the edges they make
(the camera update 0x0042b1c0 flags the point past x < 0, z < 0, x > count*size, z > count*size). Then one line a
poll: seconds since start, the point 0x007908f0/0x007908f8, the scroll terms 0x00790a88/0x00790a90 (units a ms), the
zoom 0x007909ec, the asked rotation 0x00790a38 in degrees, gui_CameraFlags 0x00790ab0 and the key word 0x00790aac;
then (Q626) the drawn rotation 0x007909e0 and the copy first person saves on entry and puts back on leaving: the point
0x00790ad0/0x00790ad8 and the asked rotation 0x00790a9c. docs/exe/park-engine.md, "The point of interest's edge" and
"Entering and leaving first person". Read-only."""
import math, struct, sys, time

def main():
    pid = int(sys.argv[1]); secs = float(sys.argv[2]) if len(sys.argv) > 2 else 5
    step = float(sys.argv[3]) if len(sys.argv) > 3 else 0.03
    f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
    def rd(a, fmt='I'):
        f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
    land = rd(rd(0x007a0854) + 0x6c)
    cx, cz, sx, sz = rd(land + 0x18), rd(land + 0x1c), rd(land + 0x10, 'f'), rd(land + 0x14, 'f')
    print(f"landscape counts {cx} {cz} sizes {sx:g} {sz:g} edges x 0..{cx * sx:g} z 0..{cz * sz:g}", flush=True)
    t0 = time.time()
    while True:
        t = time.time() - t0
        x, z = rd(0x007908f0, 'f'), rd(0x007908f8, 'f')
        vx, vz = rd(0x00790a88, 'f'), rd(0x00790a90, 'f')
        print(f"{t:7.3f} poi {x:9.3f} {z:9.3f} v {vx:+.5f} {vz:+.5f} zoom {rd(0x007909ec, 'f'):6.2f} "
              f"rot {math.degrees(rd(0x00790a38, 'f')):7.2f} flags {rd(0x00790ab0):#x} keys {rd(0x00790aac):#x} "
              f"drawn {math.degrees(rd(0x007909e0, 'f')):7.2f} saved {rd(0x00790ad0, 'f'):9.3f} {rd(0x00790ad8, 'f'):9.3f} "
              f"{math.degrees(rd(0x00790a9c, 'f')):7.2f}", flush=True)
        if t >= secs: break
        time.sleep(step)

main()
