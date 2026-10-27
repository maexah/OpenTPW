#!/usr/bin/env python3
"""flagangle.py PID SECS STEP - flag 0's angle in the running original, read every STEP s for SECS: the corner [0][0] of the
first flags face (dynfaces.py's read), 0.5 off the pole (411.25, 22, 38.75), atan2(dz, dx) in degrees. One stamped line a read."""
import math, struct, sys, time
pid, secs, step = int(sys.argv[1]), float(sys.argv[2]), float(sys.argv[3])
LIST, SIZE, N = 0x00820b00, 0xa0, 0x806
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
t0 = time.time()
while time.time() - t0 < secs:
    f.seek(LIST); raw = f.read(SIZE * N)
    for i in range(N):
        r = raw[i * SIZE:(i + 1) * SIZE]
        if struct.unpack_from('<i', r, 0x90)[0] == 3:
            x, y, z = struct.unpack_from('<3f', r, 12)
            dx, dz = x - 411.25, z - 38.75
            print("%.3f face %d angle %.3f off %.4f" % (time.time(), i, (math.degrees(math.atan2(dz, dx)) + 360) % 360, math.hypot(dx, dz)), flush=True)
            break
    else:
        print("%.3f no flags face" % time.time(), flush=True)
    time.sleep(step)
