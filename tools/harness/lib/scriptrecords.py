#!/usr/bin/env python3
"""scriptrecords.py <park file>...: the script module (RSSE) of a park file, one line a record (Q926).

    scriptrecords.py <file>...      prints `h HANDLE thing T pc PC child C parent P sound S dir D` for each script record, a count first:
                                    the fields of the console's `scripts` and original/origscripts.py (which prints
                                    `h pc thing child parent sound`: pc before thing, and no dir) in the order handle, thing, pc (the link
                                    words are +0x0c child, +0x10 parent, +0x14 sound child, ParkScriptStates)
    scriptrecords.py -s <file>...   the speed word (Q897): `h HANDLE thing T speed W rate R child C parent P dir D` a record,
                                    then `object T item I mOperatingSpeed S script H word W` a placed object, its script's
                                    word beside the thing's speed (the object record's offsets 20, 192 and 1036, ParkWorld.cs)
    records(path)                   the same, as a list of dicts (handle, thing, pc, child, parent, sound, dir, speed, rate:
                                    speed and rate are the record's +0xc0 and +0xe4 words, signed; park.md, "The clock,
                                    the speed word, and WAIT")
    objects(path)                   the placed objects, as a list of dicts (thing, item, speed, script)

Read a file a game wrote beside the one it was loaded from to see which records a sale, a purchase or a close left in it."""
import os, struct, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pk import Park
def records(path):
    p = Park(path); b = p.b; g = b.index(b'RYLFRSSE') + 4; hl = p.u32(g + 4)
    at = g + 8 + hl + 20; n, size = p.u32(at), p.u32(at + 4); at += 8; out = []
    for _ in range(n):
        d = struct.unpack_from('<61I', b, at); speed, rate = struct.unpack_from('<hh', b, at + 0xc0)[0], struct.unpack_from('<h', b, at + 0xe4)[0]
        at += size
        for _name in range(6):                       # body, stack, vars, strings, limbo, bounce: each a length and its bytes
            at += 4 + p.u32(at)
        at += 4 + 32 * p.u32(at)                     # the walk
        at += 4 + p.u32(at)                          # the heads
        ln = p.u32(at); name = bytes(b[at + 4:at + 4 + ln]).rstrip(b'\0').decode('latin1'); at += 4 + ln
        assert b[at:at + 4] == b'OBJ ', b[at:at + 4]
        at += 12 + p.u32(at + 4) * p.u32(at + 8)
        out.append(dict(handle=d[2], thing=d[0x2b] & 0xffff, pc=d[15], child=d[3], parent=d[4], sound=d[5], dir=name,
                        speed=speed, rate=rate))
    return out
def objects(path):
    p = Park(path)
    return [dict(thing=t, item=p.u16(at + 20), script=p.u32(at + 192), speed=struct.unpack_from('<i', p.b, at + 1036)[0])
            for t, (at, model) in p.things.items() if model == 3]
if __name__ == '__main__':
    args = sys.argv[1:]; speeds = args[:1] == ['-s']; args = args[1:] if speeds else args
    for path in args:
        rs = records(path); print(f'== {path}: {len(rs)} script records')
        for r in rs:
            if speeds:
                print(f"h {r['handle']} thing {r['thing']} speed {r['speed']} rate {r['rate']} child {r['child']} parent {r['parent']} dir {r['dir']}")
            else:
                print(f"h {r['handle']} thing {r['thing']} pc {r['pc']} child {r['child']} parent {r['parent']} sound {r['sound']} dir {r['dir']}")
        if speeds:
            word = {r['handle']: r['speed'] for r in rs}
            for o in objects(path):
                print(f"object {o['thing']} item {o['item']} mOperatingSpeed {o['speed']} script {o['script']} word {word.get(o['script'], '-')}")
