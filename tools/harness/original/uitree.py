#!/usr/bin/env python3
"""uitree.py PID GLOBAL - a UI tree of the running ORIGINAL walked from the control a global points at (the hire
screen's [0x007ca308], checked; the open park screen's [0x007c24c8], the ride window's tree, checked by Q693; any other
global unchecked), read-only, one line a control,
indented by depth: its address, its rectangle (+0xc, four shorts, 2048 x 1536 layout), the rectangle its skin is handed
(+0x30, flag 0x10 at 0x0065cd55; FUN_00485e30 insets it), its text (+0x98) and skin (+0x9c) pointers and the skin's
vtable. A meter's skin (vtable 0x006feeb8, FUN_00477ff0) adds its kept rectangle (+0x18), its value of 100 (+0x120)
and its matrix's two scales. Children are +0x5c, siblings +0x54 (bar.py). Made for Q636 (docs/exe/hud.md, "The skill bar
`0x2482`"). GLOBAL may be ADDR+OFF, the root's pointer at [ADDR] + OFF: the lobby's root is 0x00f82884+4 (Q752, the version
label, rect (1984 - w, 1472 - h, 1984, 1472))."""
import struct, sys
pid = int(sys.argv[1])
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))
# GLOBAL may be ADDR+OFF: the pointer at [ADDR] plus OFF holds the root (the lobby's root is 0x00f82884+4, Q752).
base, _, off = sys.argv[2].partition('+')
glob = int(base, 16)
if off: glob = rd(glob)[0] + int(off, 16); print(f'[{int(base, 16):#010x}] + {off} = {glob:#010x}')
def kids(c):
    out = []; k = rd(c + 0x5c)[0]; n = 0
    while k and n < 80: out.append(k); k = rd(k + 0x54)[0]; n += 1
    return out
root = rd(glob)[0]
print(f'[{glob:#010x}] = {root:#010x}')
def walk(c, d):
    text, skin = rd(c + 0x98)[0], rd(c + 0x9c)[0]
    vt = rd(skin)[0] if skin else 0
    print('  ' * d + f'{c:08x} rect {rd(c + 0xc, "4h")} skinrect {rd(c + 0x30, "4h")} text {text:08x} skin {skin:08x} vt {vt:08x}')
    if vt == 0x006feeb8:
        m = rd(skin + 0xdc, '16f')
        print('  ' * d + f'  meter kept {rd(skin + 0x18, "4h")} value {rd(skin + 0x120, "f")[0]:.2f} scale x {m[0]:.6g} y {m[5]:.6g}')
    for k in kids(c): walk(k, d + 1)
if root: walk(root, 0)
