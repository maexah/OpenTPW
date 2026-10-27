#!/usr/bin/env python3
"""modelboxes.py PID [FIRST LAST] - every thing's model record's cell rectangle in the running ORIGINAL, read-only:
the box FUN_00466b70 answers (docs/exe/ride-operation.md), and its middle, where a scream is sounded (FUN_00556af0).

A thing is [0x007cfb90 + id * 0x14]; its model handle is the dword at +0x20; a model record is
[0x007a4610 + handle * 4]; the rectangle is +0xc0 (least cell: x the high word, z the low) and +0xc4 (greatest),
the .hmp at +0xcc (nought: no box at all), the base height [[[rec + 8] + 4] + 0x78] + 0x44. Things 1 to 200 unless said.
Read the OBJECTS' lines: a person's +0x20 is no model handle, and where it happens to name a record its line is noise."""
import struct, sys
pid = int(sys.argv[1]); first, last = (int(sys.argv[2]), int(sys.argv[3])) if len(sys.argv) > 3 else (1, 200)
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
for thing in range(first, last + 1):
    try:
        base = rd(0x007cfb90 + thing * 0x14)
        if not base: continue
        handle = rd(base + 0x20)
        if not 0 < handle < 4096: continue
        rec = rd(0x007a4610 + handle * 4)
        if not rec: continue
        lo, hi, hmp = rd(rec + 0xc0), rd(rec + 0xc4), rd(rec + 0xcc)
        x0, z0, x1, z1 = lo >> 16, lo & 0xffff, hi >> 16, hi & 0xffff
        if not (x0 <= x1 < 128 and z0 <= z1 < 128): continue
        try: height = rd(rd(rd(rd(rec + 8) + 4) + 0x78) + 0x44, 'f')
        except (OSError, struct.error): height = float('nan')
        print(f'thing {thing:3d} model {handle:3d} covers ({x0},{z0})..({x1},{z1}) hmp {"yes" if hmp else "NONE"} '
              f'middle ({(x0 + x1 + 1) * 5:g}, {(z0 + z1 + 1) * 5:g}) height {height:g}')
    except (OSError, struct.error): continue
