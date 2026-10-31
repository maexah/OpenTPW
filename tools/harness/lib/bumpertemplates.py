#!/usr/bin/env python3
"""bumpertemplates.py [testme.exe] - the track rides' 14 templates, read from the executable's data (0x764178, 0xd0 apart, BumperType -1 first).
Prints each one's BumperType, duration (+0x04), car radius (+0x08), w (+0x0c), first mesh (+0x14), mesh count (+0x18) and performance (+0x1c):
the words a ride's record is filled from (FUN_00545890) and a park file keeps (docs/exe/saves.md, "OpenTPW's writer, a kart's or water ride's record")."""
import os, struct, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import paths
BASE = 0x764178; SIZE = 0xd0; COUNT = 14
def templates(exe):
    d = open(exe, 'rb').read(); pe = struct.unpack_from('<I', d, 0x3c)[0]
    sections, image = struct.unpack_from('<H', d, pe + 6)[0], struct.unpack_from('<I', d, pe + 24 + 28)[0]
    first = pe + 24 + struct.unpack_from('<H', d, pe + 20)[0]
    def at(a):
        for i in range(sections):
            _, vs, va, rs, ro = struct.unpack_from('<8sIIII', d, first + 40 * i)
            if va <= a - image < va + max(vs, rs): return ro + a - image - va
    out = []
    for k in range(COUNT):
        w = struct.unpack_from('<8i', d, at(BASE + k * SIZE)); out.append(dict(type=w[0], duration=w[1], radius=w[2], w=w[3], mesh_base=w[5], mesh_count=w[6], performance=w[7]))
    return out
if __name__ == '__main__':
    for t in templates(sys.argv[1] if len(sys.argv) > 1 else paths.exe()): print(' '.join(f'{k} {v}' for k, v in t.items()))
