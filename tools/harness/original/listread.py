#!/usr/bin/env python3
"""listread.py PID [expire] - the ORIGINAL's hire screen list, read-only (Q936).

Finds the list control under the screen the global points at ([0x007ca308], the hire screen): a control whose
+0x144 (row tree), +0x150 (count), +0x154 (top row), +0x14c (selected index), +0x172 (visible rows) and +0x48
(flags) read plausibly. Prints those and every row's id in list order, walked as FUN_00663ba2 / FUN_00663a93 do
(the row tree's nodes are 16 bytes: +0 left, +4 id, +8 right, +0xc parent, addressed as node*4 from [list+0x144]),
and, from the staff pool [0x007cf83c] (32 records of 0x14: +0 kind, +4 name row, +8 grade, +0xa in use, +0xc mark,
+0x10 lifetime), the record of the SELECTED row's id.

  listread.py PID                 print
  listread.py PID expire          print, then set the selected row's candidate's lifetime to nought (the next sweep drops it)
"""
import struct, sys
pid = int(sys.argv[1]); mode = sys.argv[2] if len(sys.argv) > 2 else ''
glob = 0x007ca308
f = open(f'/proc/{pid}/mem', 'r+b' if mode == 'expire' else 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def kids(c):
    out = []; k = rd(c + 0x5c); n = 0
    while k and n < 80: out.append(k); k = rd(k + 0x54); n += 1
    return out
def find(c, acc):
    try:
        base = rd(c + 0x144); cnt = rd(c + 0x150, 'i'); vis = rd(c + 0x172, 'h'); top = rd(c + 0x154, 'i'); sel = rd(c + 0x14c, 'i')
        if base > 0x10000 and 0 <= cnt <= 64 and 3 <= vis <= 30 and 0 <= top <= 64 and -1 <= sel <= 64:
            acc.append(c)
    except Exception:
        pass
    for k in kids(c): find(k, acc)
root = rd(glob)
if not root:
    print('no hire screen open'); sys.exit(1)
found = []; find(root, found)
if not found:
    print('no list control found'); sys.exit(1)
L = found[0]
base = rd(L + 0x144); cnt = rd(L + 0x150, 'i'); top = rd(L + 0x154, 'i'); sel = rd(L + 0x14c, 'i'); vis = rd(L + 0x172, 'h'); flags = rd(L + 0x48)
def I(off): return rd(base + off, 'i')
def nxt(p):
    if I(8 + p * 4) >= 0: return I(8 + p * 4)
    if I(p * 4) < 0:
        if I(0xc + p * 4) >= 0:
            q = I(0xc + p * 4); n = 0
            while I(q * 4) < 0 and I(0xc + q * 4) >= 0 and n < 100: q = I(0xc + q * 4); n += 1
            return I(q * 4)
        return -2
    return I(p * 4)
p = rd(L + 0x160, 'i'); order = []
for i in range(cnt):
    if p < 0: break
    order.append(I(4 + p * 4)); p = nxt(p)
pool = rd(0x007cf83c)
def rec(slot):
    r = pool + slot * 0x14
    return dict(slot=slot, kind=rd(r), name=rd(r + 4), grade=rd(r + 8, 'B'), inuse=rd(r + 10, 'B'), mark=rd(r + 12), life=rd(r + 16))
tick = rd(rd(0x0080239c) + 0x1da70c) if rd(0x0080239c) else -1
print(f'list {L:#x} flags {flags:#x} (0x100 {"set" if flags & 0x100 else "clear"}) count {cnt} top {top} visible {vis} selected {sel} tick {tick}')
print('rows (index:slot):', ' '.join(f'{i}:{s}' for i, s in enumerate(order)))
if 0 <= sel < len(order):
    s = order[sel]; print(f'selected index {sel} = slot {s}: {rec(s)}  (slot-in-window {sel - top})')
    if mode == 'expire':
        f.seek(pool + s * 0x14 + 0x10); f.write(struct.pack('<I', 0)); print('lifetime set to 0')
else:
    print('nothing selected')
