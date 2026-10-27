#!/usr/bin/env python3
"""poke32.py PID - Q618's instrument: make the ORIGINAL ask its ground tiles as on a machine of its day. Writes
dwTotalPhys [0x008bd520] = 0x10000000 (256 MB), so FUN_00572110's signed test (0x0057215f) keeps the ask; and, if the
texture memory [0x008c1624] reads negative, 0x04000000 (64 MB) there too, for its second test. Prints before and after."""
import struct, sys
pid = int(sys.argv[1]); f = open(f'/proc/{pid}/mem', 'r+b', buffering=0)
def rd(a, fmt='i'): f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize(fmt)))[0]
def wr(a, v): f.seek(a); f.write(struct.pack('<I', v))
fmt = rd(0x008bd554); bpp = rd(0x00701548 + 4 * fmt)
print('before: dwTotalPhys %d, texture memory %d, surface format %d, bytes a texel %d, [0x0079d86c] 0x%x' % (
    rd(0x008bd520), rd(0x008c1624), fmt, bpp, rd(0x0079d86c, 'I')))
wr(0x008bd520, 0x10000000)
if rd(0x008c1624) < 0: wr(0x008c1624, 0x04000000)
print('after:  dwTotalPhys %d, texture memory %d' % (rd(0x008bd520), rd(0x008c1624)))
