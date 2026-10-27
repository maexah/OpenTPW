#!/usr/bin/env python3
"""mapview.py PID [SECS [STEP]] - the ORIGINAL's park map screen object (the static at 0x00f86db8, docs/exe/hud.md,
"The park map"): its centre SX +0x54 and SY +0x58 (cells), zoom Z +0x5c, the heightfield's size +0x24/+0x28, the
picture's size +0x48/+0x4c, the pointer +0x10/+0x14 (layout units), the right-drag flag +0x20084, the layer bits +0x18,
the radios +0x1c/+0x20; then the camera's look-at 0x007908f0/f8 and the block the map opens on, 0x00790ab8/c0.
With SECS, a line every STEP seconds (default 0.1) for SECS seconds."""
import struct, sys, time
pid = int(sys.argv[1]); f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt):
    f.seek(a); return struct.unpack(fmt, f.read(struct.calcsize(fmt)))[0]
M = 0x00f86db8
def line():
    return ('map SX %.3f SY %.3f Z %.3f size %dx%d pic %dx%d ptr (%d,%d) drag %d layers %#x radios %d %d | '
            'look-at (%.2f, %.2f) open-from (%.2f, %.2f)' % (
        rd(M + 0x54, '<f'), rd(M + 0x58, '<f'), rd(M + 0x5c, '<f'), rd(M + 0x24, '<i'), rd(M + 0x28, '<i'),
        rd(M + 0x48, '<i'), rd(M + 0x4c, '<i'), rd(M + 0x10, '<i'), rd(M + 0x14, '<i'), rd(M + 0x20084, '<i'),
        rd(M + 0x18, '<I'), rd(M + 0x1c, '<i'), rd(M + 0x20, '<i'),
        rd(0x007908f0, '<f'), rd(0x007908f8, '<f'), rd(0x00790ab8, '<f'), rd(0x00790ac0, '<f')))
if len(sys.argv) > 2:
    secs = float(sys.argv[2]); step = float(sys.argv[3]) if len(sys.argv) > 3 else 0.1; t0 = time.time()
    while time.time() - t0 < secs:
        print('%.2f %s' % (time.time() - t0, line()), flush=True); time.sleep(step)
else:
    print(line())
