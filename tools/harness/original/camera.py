#!/usr/bin/env python3
"""camera.py PID [CELLS] - the ORIGINAL's park camera at this moment: the saved words (point looked at 0x007908f0..f8, zoom
0x007909ec, rotation 0x00790a38 and its copy 0x007909e0), the camera's matrix 0x0087aff0 (its last row the eye), the
draw matrix [0x0087b088] and DAT_008bcbcc; then each named cell's four corners at heights 1.5 and 21.5 (ground taken
as nought) put through the draw matrix to pixels of a 1024 x 768 frame, and the lift's height in pixels at each corner.
CELLS: 'x,y;x,y' (default the four of row 26, 51..54).
camera.py PID --aim X,Y prints only 'px py', the pixel of cell (X,Y)'s middle on the ground (1.5): where to click it."""
import struct, sys, math
pid = int(sys.argv[1]); f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
AIM = len(sys.argv) > 2 and sys.argv[2] == '--aim'
def rd(a, fmt='<I'):
    f.seek(a); v = struct.unpack(fmt, f.read(struct.calcsize(fmt))); return v[0] if len(v) == 1 else v
poi = rd(0x007908f0, '<3f'); zoom = rd(0x007909ec, '<f'); rot = rd(0x00790a38, '<f'); rot2 = rd(0x007909e0, '<f')
if not AIM: print('poi (%.3f, %.3f, %.3f) zoom %.3f rotation %.5f (copy %.5f) = %.2f deg' % (*poi, zoom, rot, rot2, math.degrees(rot)))
C = rd(0x0087aff0, '<16f')
m = rd(0x0087b088); M = rd(m, '<16f')
if not AIM:
    print('camera matrix 0x0087aff0:'); [print('   ', ' '.join('%10.4f' % C[r * 4 + c] for c in range(4))) for r in range(4)]
    print('draw matrix at %#x:' % m); [print('   ', ' '.join('%10.5f' % M[r * 4 + c] for c in range(4))) for r in range(4)]
    print('DAT_008bcbcc %.4f' % rd(0x008bcbcc, '<f'))
def px(x, y, z):
    c = [M[0] * x + M[4] * y + M[8] * z + M[12], M[1] * x + M[5] * y + M[9] * z + M[13], M[3] * x + M[7] * y + M[11] * z + M[15]]
    return ((c[0] / c[2] + 1) * 512, (c[1] / c[2] + 1) * 384)
if AIM:
    cx, cy = map(int, sys.argv[3].split(',')); print('%d %d' % px(cx * 10 + 5, 1.5, cy * 10 + 5)); sys.exit()
cells = [tuple(map(int, s.split(','))) for s in (sys.argv[2] if len(sys.argv) > 2 else '51,26;52,26;53,26;54,26').split(';')]
for cx, cy in cells:
    out = []
    for dx, dz in ((0, 0), (10, 0), (10, 10), (0, 10)):
        x, z = cx * 10 + dx, cy * 10 + dz
        b = px(x, 1.5, z); t = px(x, 21.5, z)
        out.append('(%d,%d) ground %.0f,%.0f top %.0f,%.0f lift %.1f px' % (x, z, *b, *t, math.hypot(t[0] - b[0], t[1] - b[1])))
    print('cell (%d,%d): ' % (cx, cy) + ' | '.join(out))
