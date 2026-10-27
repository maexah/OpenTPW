"""The advisor's response table (0x00768fb8, stride 0x20, 9999 ends it) read from testme.exe on disk:
id, sample, lip, gesture, slot|bank<<16, prop A (+0x14), prop B (+0x18), group. `responses.py` prints the spread of
the prop pairs; `responses.py ID...` prints those rows."""
import struct, sys, collections
import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
EXE = paths.exe()
d = open(EXE, 'rb').read()
pe = struct.unpack_from('<I', d, 0x3c)[0]
nsec = struct.unpack_from('<H', d, pe + 6)[0]
opt = struct.unpack_from('<H', d, pe + 20)[0]
base = struct.unpack_from('<I', d, pe + 24 + 28)[0]
secs = [struct.unpack_from('<8sIIII', d, pe + 24 + opt + 40 * i) for i in range(nsec)]
def off(va):
    for name, vsize, vaddr, rsize, raw in secs:
        if vaddr <= va - base < vaddr + max(vsize, rsize):
            return raw + (va - base - vaddr)
    raise SystemExit('no section holds %#x' % va)
def rows():
    o = off(0x00768fb8)
    while True:
        r = struct.unpack_from('<8i', d, o)
        if r[0] == 9999:
            return
        yield r
        o += 0x20
if __name__ == '__main__':
    R = list(rows())
    if len(sys.argv) > 1:
        want = set(int(a) for a in sys.argv[1:])
        for r in R:
            if r[0] in want:
                print('id %d sample %d lip %d gesture %d slot %d bank %d props %d %d group %d' % (r[0], r[1], r[2], r[3], r[4] & 0xffff, r[4] >> 16, r[5], r[6], r[7]))
    else:
        print(len(R), 'rows')
        c = collections.Counter((r[4] & 0xffff, r[5], r[6]) for r in R)
        for k, n in sorted(c.items()):
            ids = [r[0] for r in R if (r[4] & 0xffff, r[5], r[6]) == k]
            print('slot %d props %3d %3d: %3d rows, ids %s' % (*k, n, ' '.join(map(str, ids[:14])) + (' ...' if n > 14 else '')))
