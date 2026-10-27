#!/usr/bin/env python3
"""mapptr.py PID - the original's map pointer (+0x10/+0x14, layout units), centre (+0x54/+0x58), zoom, cells, the word
+0x20064, and the cell the hover's own sum (0x005f1b13..0x005f1b8a) puts under the pointer, with that cell's code and id."""
import struct, sys
pid = int(sys.argv[1]); f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt): f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))
M = 0x00f86db8
px, py = rd(M + 0x10, 'ii'); W, H = rd(M + 0x24, 'ii'); cx, cy, z = rd(M + 0x54, 'fff')
SW, SH = rd(0x007a1968, 'I')[0], rd(0x007a1930, 'I')[0]
gx, gy = px * SW / 2048, (0x600 - py) * SH / 1536
ax = cx + (gx - SW * .5) / (480 / W * z); ay = cy + (gy - SH * .5) / (425 / H * z)
code, tid = rd(M + 0x60 + (int(ay) * 128 + int(ax)) * 8, 'IH')
print(f'pointer layout ({px},{py}) game ({gx:.1f},{gy:.1f}) screen {SW}x{SH} centre ({cx:.2f},{cy:.2f}) zoom {z:.2f} '
      f'-> cell ({ax:.2f},{ay:.2f}) code {code} id {tid}; word {rd(M + 0x20064, "H")[0]}')
