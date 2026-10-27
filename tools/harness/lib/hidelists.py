"""Each clip's hide list of an advisor model (the clip's block at +0x98: count +0x1a, offset +0x38, node
indices), with the model's lookup id beside each node: hidelists.py <advisor.wad> <stem, e.g. Advisor>."""
import os, struct, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
import wadcat, md2surf
wad, stem = sys.argv[1], sys.argv[2].lower()
M = dict((n.split('\\')[-1].lower(), b) for n, b in wadcat.members(open(wad, 'rb').read()))
model = md2surf.Model(M[stem + '.md2'])
def ident(node):
    return model.name(node)
n = 1
while stem + 'm%d.md2' % n in M:
    d = M[stem + 'm%d.md2' % n]
    b = struct.unpack_from('<I', d, 0x98)[0]
    count = struct.unpack_from('<H', d, b + 0x1a)[0]
    at = struct.unpack_from('<I', d, b + 0x38)[0]
    nodes = struct.unpack_from('<%dH' % count, d, at) if count else ()
    print('m%-2d hides %2d: %s' % (n, count, ' '.join('%d(%s)' % (x, ident(x)) for x in nodes)))
    n += 1
