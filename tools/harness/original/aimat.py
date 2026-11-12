#!/usr/bin/env python3
"""aimat.py PID CX CY H - the pixel (of the original's 1024 x 768 frame) of cell (CX, CY)'s middle at ground height H, as original/camera.py --aim but with H.
Whole pixels, cut toward nought; projpoint.py takes any points and gives two decimals and w."""
import struct, sys
pid = int(sys.argv[1]); f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<I'):
    f.seek(a); v = struct.unpack(fmt, f.read(struct.calcsize(fmt))); return v[0] if len(v) == 1 else v
M = rd(rd(0x0087b088), '<16f'); cx, cy, h = int(sys.argv[2]), int(sys.argv[3]), float(sys.argv[4])
x, y, z = cx * 10 + 5, h, cy * 10 + 5
c = [M[0]*x + M[4]*y + M[8]*z + M[12], M[1]*x + M[5]*y + M[9]*z + M[13], M[3]*x + M[7]*y + M[11]*z + M[15]]
print('%d %d' % ((c[0]/c[2] + 1) * 512, (c[1]/c[2] + 1) * 384))
