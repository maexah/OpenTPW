#!/usr/bin/env python3
"""scriptrecords.py <park file>...: the script module (RSSE) of a park file, one line a record (Q926).

    scriptrecords.py <file>...      prints `h HANDLE thing T pc PC child C parent P sound S dir D` for each script record, a count first:
                                    the fields of the console's `scripts` and original/origscripts.py (which prints
                                    `h pc thing child parent sound`: pc before thing, and no dir) in the order handle, thing, pc (the link
                                    words are +0x0c child, +0x10 parent, +0x14 sound child, ParkScriptStates)
    records(path)                   the same, as a list of dicts (handle, thing, pc, child, parent, sound, dir)

Read a file a game wrote beside the one it was loaded from to see which records a sale, a purchase or a close left in it."""
import os, struct, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pk import Park
def records(path):
    p = Park(path); b = p.b; g = b.index(b'RYLFRSSE') + 4; hl = p.u32(g + 4)
    at = g + 8 + hl + 20; n, size = p.u32(at), p.u32(at + 4); at += 8; out = []
    for _ in range(n):
        d = struct.unpack_from('<61I', b, at); at += size
        for _name in range(6):                       # body, stack, vars, strings, limbo, bounce: each a length and its bytes
            at += 4 + p.u32(at)
        at += 4 + 32 * p.u32(at)                     # the walk
        at += 4 + p.u32(at)                          # the heads
        ln = p.u32(at); name = bytes(b[at + 4:at + 4 + ln]).rstrip(b'\0').decode('latin1'); at += 4 + ln
        assert b[at:at + 4] == b'OBJ ', b[at:at + 4]
        at += 12 + p.u32(at + 4) * p.u32(at + 8)
        out.append(dict(handle=d[2], thing=d[0x2b] & 0xffff, pc=d[15], child=d[3], parent=d[4], sound=d[5], dir=name))
    return out
if __name__ == '__main__':
    for path in sys.argv[1:]:
        rs = records(path); print(f'== {path}: {len(rs)} script records')
        for r in rs:
            print(f"h {r['handle']} thing {r['thing']} pc {r['pc']} child {r['child']} parent {r['parent']} sound {r['sound']} dir {r['dir']}")
