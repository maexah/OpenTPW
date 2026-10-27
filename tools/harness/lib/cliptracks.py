"""Each track (and a scale track's keys) of an advisor clip: target node (name), flags. cliptracks.py <wad> <model stem> <clip suffix, m19, C, s>... (Q597): the model is <stem>.md2, each clip <stem><suffix>.md2"""
import os, struct, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
import wadcat, md2surf
wad, stem = sys.argv[1], sys.argv[2].lower()
M = dict((n.split('\\')[-1].lower(), b) for n, b in wadcat.members(open(wad, 'rb').read()))
model = md2surf.Model(M[stem + '.md2'])
for c in sys.argv[3:]:
    d = M[stem + c.lower() + '.md2']
    b = struct.unpack_from('<I', d, 0x98)[0]
    n = struct.unpack_from('<H', d, b + 0x12)[0]
    t = struct.unpack_from('<I', d, b + 0x2c)[0]
    print('clip', c, 'frames', struct.unpack_from('<ii', d, b+4), 'tracks', n)
    for i in range(n):
        o = t + 64*i
        fl = struct.unpack_from('<I', d, o+4)[0]; tg = struct.unpack_from('<H', d, o+0x14)[0]
        print('  %2d node %3d %-20s flags %#x  words %s' % (i, tg, model.name(tg), fl, ' '.join('%08x'%w for w in struct.unpack_from('<14I', d, o+8))))
        if fl & 0x80:   # the scale track: count at +0x12, 16-byte keys (frame, 0, x, y, z) at +0x20
            k = struct.unpack_from('<H', d, o + 0x12)[0]; at = struct.unpack_from('<I', d, o + 0x20)[0]
            print('      scale', [(struct.unpack_from('<H', d, at + 16 * j)[0],) + tuple(round(x, 4) for x in struct.unpack_from('<3f', d, at + 16 * j + 4)) for j in range(k)])
