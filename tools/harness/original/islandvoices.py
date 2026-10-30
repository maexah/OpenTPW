#!/usr/bin/env python3
"""islandvoices.py PID OBJ [SECS] - each lobby island's voice handles (+0x1c music, +0x20 bed) and category index +0x18, a line a
change, with the camera's nearest island [3]. OBJ is the camera object attractcam.py printed. Read-only."""
import struct, sys, time
pid, obj = int(sys.argv[1]), int(sys.argv[2], 16); secs = float(sys.argv[3]) if len(sys.argv) > 3 else 30
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a): f.seek(a); return struct.unpack('<I', f.read(4))[0]
isl = []; node = rd(rd(obj + 8) + 4)
while node: isl.append(rd(node)); node = rd(node + 4)
t0 = time.time(); last = None
while time.time() - t0 < secs:
    row = tuple((rd(i + 0x18), rd(i + 0x1c), rd(i + 0x20)) for i in isl) + (isl.index(rd(obj + 0xc)) if rd(obj + 0xc) in isl else -1,)
    if row != last:
        last = row; print(f'{time.time()-t0:7.2f} ' + '  '.join(f'isl{k} cat{c} music {m:#x} bed {b:#x}' for k, (c, m, b) in enumerate(row[:-1])) + f'  nearest {row[-1]}', flush=True)
    time.sleep(0.01)
