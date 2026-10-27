#!/usr/bin/env python3
"""entcell.py PID - the running ORIGINAL's first entertainer (a thing of model 6): its world point (cell x 10 + 5) and
its state (+0x19c), as "X,Z STATE", for setcam.py. Read-only (Q668)."""
import struct, sys
f = open(f'/proc/{int(sys.argv[1])}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
for i in range(1, 2000):
    p = rd(0x007cfb90 + i * 0x14)
    if p and rd(p + 2, 'B') == 6:
        print(f"{rd(p + 5, 'B') * 10 + 5},{rd(p + 7, 'B') * 10 + 5} {rd(p + 0x19c, 'i'):#x}"); break
