#!/usr/bin/env python3
"""spritedump.py PID [KIND=6] - the ORIGINAL's first sprite of KIND (6 the mechanic, 0 a guest): its words +0x00..+0xfc, hex and as a float where plausible (Q856). Read-only."""
import struct, sys
pid = int(sys.argv[1]); kind = int(sys.argv[2]) if len(sys.argv) > 2 else 6
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<I'):
    f.seek(a); v = struct.unpack(fmt, f.read(struct.calcsize(fmt))); return v[0] if len(v) == 1 else v
table, n = rd(0x007b49f0), rd(0x007b49f4)
for i in range(1, n):
    s = rd(table + 4 * i)
    if s and rd(s + 0xac) == kind:
        out = []
        for o in range(0, 0x100, 4):
            w = rd(s + o); fl = struct.unpack('<f', struct.pack('<I', w))[0]
            out.append('%02x:%08x%s' % (o, w, ('(%.3f)' % fl) if 1e-3 < abs(fl) < 1e5 else ''))
        print('slot', i, 'at', hex(s)); print(' '.join(out)); break
