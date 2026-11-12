#!/usr/bin/env python3
"""projpoint.py PID X,Y,Z ... : world points (Y the height) put through the ORIGINAL's draw matrix [0x0087b088] to pixels of
its 1024 x 768 frame, as camera.py's corners are, with each point's w; first the eye (the camera matrix 0x0087aff0's last
row) and the stored point looked at (0x007908f0). Read-only. Q1106: at 595,355 zoom 70 rotation 180 over ground 2.6265,
(595, 2.6265, 355) went to (512.00, 384.00): the frame's middle is the point lifted by the ground, not the stored point
(whose height reads 0)."""
import struct, sys
if len(sys.argv) < 3:
    sys.exit(__doc__)
pid = int(sys.argv[1]); f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<I'):
    f.seek(a); v = struct.unpack(fmt, f.read(struct.calcsize(fmt))); return v[0] if len(v) == 1 else v
C = rd(0x0087aff0, '<16f'); M = rd(rd(0x0087b088), '<16f')
print('eye (%.3f, %.3f, %.3f); point looked at (%.3f, %.3f, %.3f)' % (C[12], C[13], C[14], *rd(0x007908f0, '<3f')))
for s in sys.argv[2:]:
    x, y, z = map(float, s.split(','))
    c = [M[0] * x + M[4] * y + M[8] * z + M[12], M[1] * x + M[5] * y + M[9] * z + M[13], M[3] * x + M[7] * y + M[11] * z + M[15]]
    print('point (%.4f, %.4f, %.4f) -> pixel (%.2f, %.2f) w %.4f' % (x, y, z, (c[0] / c[2] + 1) * 512, (c[1] / c[2] + 1) * 384, c[2]))
