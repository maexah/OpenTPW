"""rsys.py <park file>... : the model module (RSYS) walked slot by slot, beside the objects' MeshInstanceID and the
cells' mMeshInstance (Q241i). Channel counts are not in the file: found by search (the walk must end on the tag)."""
import sys, struct, collections
import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths; sys.setrecursionlimit(100000)
from pk import Park
def walk(p):
    b = p.b; g = b.index(b'SYSG', p.messend) + 4; n = p.u32(g); end = g + 4 + n; assert b[end:end+4] == b'SYSR', b[end:end+4]
    present, free, cursor = p.u32(g+4), p.u32(g+8), p.u32(g+12); total = present + free
    recs = []
    def rec(at, slot):
        if slot == total: return [] if at == end else None
        if b[at] == 0:
            r = rec(at+1, slot+1); return None if r is None else [None] + r
        if b[at] != 1: return None
        nodes, looks = p.u16(at+0x2b), p.u16(at+0x2d)
        base = at + 0x37 + looks*8 + nodes*4
        for ch in (1, 3, 2, 0, 4, 5, 6):
            ok = all(p.u32(base + 44*i + 4) <= 13 for i in range(ch)) and base + 44*ch <= end
            if not ok: continue
            r = rec(base + 44*ch, slot+1)
            if r is not None: return [(at, ch, base)] + r
        return None
    return g, (present, free, cursor), rec(g+16, 0)
if __name__ == '__main__':
    for path in [a for a in sys.argv[1:] if a != "-v"]:
        p = Park(path); g, hdr, recs = walk(p); b = p.b
        print('==', path.split('/')[-1], 'present/free/cursor', hdr, 'slots', len(recs))
        objs = {p.u32(at+54): (tid, p.u16(at+20)) for tid, (at, m) in p.things.items() if m == 3}
        cells = {}
        for i, c in enumerate(p.cell):
            if b[c] & 1:
                mi = p.u32(c+1+3)
                if mi: cells[mi] = (i % 128, i // 128, p.u32(c+1+24), tuple(struct.unpack_from('<3I', b, c+1+12)))
        byitem = collections.Counter()
        for slot, r in enumerate(recs):
            if r is None: continue
            at, ch, base = r; item = p.u32(at+1); byitem[item] += 1
            x, y, f0, f4, e4, d4 = struct.unpack_from('<6I', b, at+5); flags = p.u32(at+0x1d); w = p.u16(at+0x21)
            prog = p.f32(at+0x23); ang = p.u32(at+0x27)
            who = f'object thing {objs[slot+1][0]} item {objs[slot+1][1]}' if slot+1 in objs else f'cell {cells[slot+1]}' if slot+1 in cells else ''
            if '-v' in sys.argv or who:
                print(f' h{slot+1:4} item {item:5} at ({x},{y}) f0 {f0:#x} f4 {f4:#x} e4 {e4:#x} d4 {d4} ang {ang} fl {flags:#x} w {w} prog {prog} nodes {p.u16(at+0x2b)} looks {p.u16(at+0x2d)} ch {ch} | {who}')
        print('  by item', dict(sorted(byitem.items())))
        print('  objects with no slot', [v for k, v in objs.items() if k == 0 or k > len(recs) or recs[k-1] is None][:20], '; cells naming', len(cells), 'missing', sum(1 for k in cells if k > len(recs) or recs[k-1] is None))
