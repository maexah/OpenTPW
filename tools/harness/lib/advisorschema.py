"""advisorschema.py [OFFSET-HEX ... | all] - the advisor's .sam schema walked from testme.exe on disk (0x0072e120,
60 bytes a descriptor): the Advisor.sam key behind each offset of the balance object at 0x00fb3540, which is ECX in
every score function of the metadata table (`MOV EAX,[ECX + 0x5e4]` is TutorialUIPanel.Score).

    advisorschema.py 5dc 5e0 5e4     the keys at those offsets
    advisorschema.py all             every key with its offset, kind and descriptor

Four bytes a field from +0x18; the one array (MessageGroups, ten entries of three fields) takes 124 bytes, four more
than its fields: that four is ANCHORED, not read - on RidesBreakdownImminent.Score at +0x248 and HelpMessage.Score
and .ContextHelpScore at +0x328 and +0x32c (docs/exe/advisor-park.md) - so an offset inside the array is not to be
trusted from here. Importable: walk() answers (offset, kind, key, descriptor)."""
import sys, struct
import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
import responses
exe = open(paths.exe(),'rb').read()
def walk():
    va = 0x0072e120; off = 0x18; out = []; pending = []; arr = None
    while True:
        o = responses.off(va); kind = struct.unpack_from('<I', exe, o)[0]
        name = exe[o+4:o+36].split(b'\0')[0].decode('latin1')
        cnt = struct.unpack_from('<I', exe, o+0x34)[0]
        if kind > 7: break
        if kind in (4,5,6,7):
            pending.append((off, kind, name, va)); off += 4
        elif kind == 1:
            out += [(o_, k, name + '.' + n, v) for o_, k, n, v in pending]; pending = []
        elif kind == 3:
            out += [(o_, k, name + '[].' + n, v) for o_, k, n, v in pending]; pending = []
            out.append((None, 3, name + ' x%d' % cnt, va)); off += 4 * 3 * (cnt - 1) + 4
        va += 60
    return out
if __name__ == '__main__':
    rows = walk()
    want = [int(a,16) for a in sys.argv[1:] if a != 'all']
    for o, k, n, v in rows:
        if 'all' in sys.argv or o in want or (not sys.argv[1:] and o is not None and o < 0x30):
            print('+0x%x' % o if o is not None else '  -  ', k, n, '0x%08x' % v)
