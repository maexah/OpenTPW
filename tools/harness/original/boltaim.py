#!/usr/bin/env python3
"""boltaim.py PID SECS [DX DZ [STEP]] - HOLDS the ORIGINAL's live lightning bolts at the ground point its park camera looks at, so a
bolt that would fall anywhere on the 128 x 128 map falls in the frame. It WRITES the original's memory (as thingpoke.py
does): stop the original afterwards, and never save a park under it.

  boltaim.py %PID% 90 0 0        every live bolt record's ground point set to the camera's point of interest
                                 ([0x007908f0], [0x007908f8]: x and z of the point it looks at, in world units), its top
                                 keeping the lean the original rolled; the i-th live record is shifted DX * i, DZ * i

A record is the bolt renderer's: [0x008bcbac] + 0x3008 + 0x30 i, the ground point (x, y, z) first, the sky point second
(FUN_00580320, docs/exe/weather.md, "The bolt"); y is up. Its life left, colour and shape hash are not touched, so the
envelope under test is the original's own. Prints a line a record aimed, with the wall clock. Beside boltpoll.py and
thingpoke.py (`7:0x38:B=1`, bolts on a clear day) as a POLL for loadfile.sh. Q1044."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
dx = float(sys.argv[3]) if len(sys.argv) > 3 else 0.0; dz = float(sys.argv[4]) if len(sys.argv) > 4 else 0.0
step = float(sys.argv[5]) if len(sys.argv) > 5 else 0.002
f = open(f'/proc/{pid}/mem', 'r+b', buffering=0)
def rd(a, fmt='<I'):
    f.seek(a); return struct.unpack(fmt, f.read(struct.calcsize(fmt)))[0]
def wr(a, fmt, *v):
    f.seek(a); f.write(struct.pack(fmt, *v))
t0 = time.time(); aimed = 0; seen = {}
while time.time() - t0 < secs:
    try:
        obj = rd(0x008bcbac)
        if not obj: time.sleep(0.05); continue
        n = rd(obj + 0x3308)
        cx = rd(0x007908f0, '<f'); cz = rd(0x007908f8, '<f')
        for i in range(min(n, 16)):
            rec = obj + 0x3008 + 0x30 * i
            gx, gy, gz, tx, ty, tz = struct.unpack('<6f', (f.seek(rec), f.read(24))[1])
            ax, az = cx + dx * i, cz + dz * i
            if abs(gx - ax) > 0.01 or abs(gz - az) > 0.01:
                key = (round(gx - tx, 1), round(gz - tz, 1))
                lx, lz = tx - gx, tz - gz
                wr(rec, '<6f', ax, gy, az, ax + lx, ty, az + lz)
                aimed += 1
                print(f'wall={time.time():.3f} record {i}: ground ({gx:.1f},{gy:.1f},{gz:.1f}) top ({tx:.1f},{ty:.1f},{tz:.1f}) -> ground ({ax:.1f},{gy:.1f},{az:.1f}) lean ({lx:.1f},{lz:.1f})', flush=True)
    except (OSError, struct.error): time.sleep(0.05); continue
    time.sleep(step)
print(f'aimed {aimed}', flush=True)
