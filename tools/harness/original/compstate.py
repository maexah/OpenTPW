#!/usr/bin/env python3
"""compstate.py PID - the ORIGINAL's procedural ground compositor, read once (Q483; docs/exe/park-engine.md,
"The procedural ground"): [0x008bd550] (bit 4 ready), the terrain object [0x00879d60]'s box (+0x234 x0, +0x238 y0,
+0x23c x1, +0x240 y1), its cache (+0x230) with the tile size (+4), the tiles (+0x10010) and the lnd object (+0)'s
tile size, set, ramp bytes and map size, [0x0079d86c] (the detail's size bits) and [0x008c162c] (the cache's count)."""
import struct, sys
pid = int(sys.argv[1]); f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize(fmt)))[0]
def rb(a, n):
    f.seek(a); return f.read(n)
t = rd(0x00879d60)
print('[0x008bd550] 0x%x (bit 4 %s)  [0x0079d86c] 0x%x  [0x008c162c] %d' % (rd(0x008bd550), 'set' if rd(0x008bd550) & 4 else 'clear', rd(0x0079d86c), rd(0x008c162c)))
print('terrain 0x%x: box x0 %d y0 %d x1 %d y1 %d; cache 0x%x' % (t, rd(t + 0x234, 'i'), rd(t + 0x238, 'i'), rd(t + 0x23c, 'i'), rd(t + 0x240, 'i'), rd(t + 0x230)))
c = rd(t + 0x230)
if c:
    l = rd(c)
    print('cache: tile size %d, tiles %d; lnd 0x%x: size %d set %d ramps %s map %dx%dx%d cells %d x %d' % (
        rd(c + 4), rd(c + 0x10010), l, rb(l, 1)[0], rb(l, 2)[1], list(rb(l + 2, 10)), rd(l + 0x14), rd(l + 0x18), rd(l + 0x1c), rd(l + 0xc), rd(l + 0x10)))
    used = sum(1 for i in range(0x4000) if rd(c + 8 + 4 * i))
    print('cache slots holding a tile (of the 128 x 128): %d' % used)
