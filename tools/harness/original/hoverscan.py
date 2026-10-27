#!/usr/bin/env python3
"""hoverscan.py PID CATS [person|noperson|any] [click] [pick] [grid X0,X1,DX,Y0,Y1,DY] - the ORIGINAL's hover, found by
stepping the pointer (Q474).

Jumps the pointer (xdotool mousemove on $DISPLAY, default :77) over a grid of 1024 x 768 window points, row by row, and
after each jump reads the hover's category [0x007c24f4], its cell [0x007b05cc] and the person the sprite pick took
[0x007b05ac] (park-engine.md, "The person under the pointer decides first"). It stops at the first point whose category
is one of CATS (comma-separated, e.g. 4 or 7,8) and whose person is there (person), absent (noperson) or either (any,
the default). With `click` it presses there at once (0.2 s held: a few seconds of reads let a walker go) and reads what
the click keeps: the person [0x007c2650] (FUN_0048c920) and the object [0x007c2654] (FUN_0048c930), each as a thing id.
Each line: the point, the category, the cell (x,y), the person as id and kind. Read-only apart from the pointer and the
one click; exits 1 when no point matched.
With `pick` (Q494) each line also gives the picker's point P [0x007a1c70] (x, height, z) and the model record the
object march met [0x007a1cd4] as its handle in the table at 0x007a4610 (0: the ground answered; park-engine.md, "The
picker's object march").
Default grid: 220..1000 by 40 across, 120..680 by 40 down (off the gadget and the money)."""
import os, struct, subprocess, sys, time
pid = int(sys.argv[1]); cats = {int(c) for c in sys.argv[2].split(',')}
want = next((a for a in sys.argv[3:] if a in ('person', 'noperson', 'any')), 'any'); click = 'click' in sys.argv[3:]
pick = 'pick' in sys.argv[3:]
grid = (220, 1000, 40, 120, 680, 40)
if 'grid' in sys.argv: grid = tuple(int(v) for v in sys.argv[sys.argv.index('grid') + 1].split(','))
disp = os.environ.get('DISPLAY', ':77')
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def ids():
    out = {}
    for tid in range(1, 4096):
        p = rd(0x007cfb90 + tid * 0x14)
        if p: out[p] = tid
    return out
def handle(rec):
    if not rec: return 0
    for h in range(1, 4096):
        if rd(0x007a4610 + h * 4) == rec: return h
    return f'0x{rec:x}'
def picked():
    if not pick: return ''
    px, py, pz = rd(0x007a1c70, 'f'), rd(0x007a1c74, 'f'), rd(0x007a1c78, 'f')
    return f' P ({px:.1f},{py:.1f},{pz:.1f}) met {handle(rd(0x007a1cd4))}'
def xdo(*a): subprocess.run(['xdotool', *a], env=dict(os.environ, DISPLAY=disp), check=False)
def state(by):
    cat = rd(0x007c24f4); cell = rd(0x007b05cc, 'H'); p = rd(0x007b05ac)
    kind = rd(p + 2, 'B') if p else 0
    return cat, ((cell - 1) % 128, (cell - 1) // 128) if cell else None, by.get(p, p), kind
by = ids(); hit = None
x0, x1, dx, y0, y1, dy = grid
for y in range(y0, y1 + 1, dy):
    for x in range(x0, x1 + 1, dx):
        xdo('mousemove', str(x), str(y)); time.sleep(0.35)
        cat, cell, person, kind = state(by)
        print(f'({x},{y}) cat {cat} cell {cell} person {person} kind {kind}{picked()}', flush=True)
        if cat in cats and (want == 'any' or (want == 'person') == (person != 0)):
            hit = (x, y); break
    if hit: break
if not hit:
    print('no point matched'); sys.exit(1)
print(f'== matched ({hit[0]},{hit[1]})', flush=True)
if click:
    xdo('mousedown', '1'); time.sleep(0.2); xdo('mouseup', '1'); time.sleep(1.0)
    by = ids()
    print(f'== clicked: person kept {by.get(rd(0x007c2650), rd(0x007c2650))} object kept {by.get(rd(0x007c2654), rd(0x007c2654))}'
          f' cat now {rd(0x007c24f4)}', flush=True)
