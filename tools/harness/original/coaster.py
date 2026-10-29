#!/usr/bin/env python3
"""coaster.py PID [SECS] [full] - the original's memory: every coaster on the list [0x00790fe0] (next +0x18), each with
its model instance (+0x10), handle (+0x14), the highest track point Global 0 reads (+0x110, written by
Coaster_StoreTrackStats 0x0043df90 from FUN_0043c450), the top speed (+0x114), the track's samples (+0x9c, 0x38 bytes
each, count +0xa4, spacing +0xac; a sample's x, y, z at +4..+0xc) with the largest y over samples 1..n-2 (the analysis'
own walk) and over all; then the pylon chain (head +0x54, next +0x64): each node's cell record (+0x100: its +8 cell
index, its +0x1c..+0x24 place, its +0x18 pylon type with heights +0xbc/+0xc0 and stack step +0xc4), stack level (+4),
loft (+0xe8), its place (+0x4c..+0x54) and its knots (+0x90 -> count, rail 1, rail 2; 0x58 bytes, place at +0x40).
docs/exe/ride-operation.md, "A coaster's highest track point" (Q593). With SECS, a block whenever the summary changes
(stamped with mGameTick); 'full' also prints every sample. 'pylons' adds to each node its pylon model (+0x70, the index into
0x007a4610), the flags it was made with (+0xe4), its root's height ([[[model+8]+4]+0x78]+0x44), its base node ([model+8]+0x2c)
and that node's flag word (0x80000010 hidden), the pylon type's flag word (cell +0x18, +0), and the cell's stack (+0x30
count, +0x34 nodes: id:flags each): the stack Coaster_StandCellPylons (0x00432df0) walks (park-engine.md, "A coaster's
pylons", Q816). Read-only."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]) if len(sys.argv) > 2 and sys.argv[2][0].isdigit() else 0; full = 'full' in sys.argv[2:]; pylons = 'pylons' in sys.argv[2:]
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def fl(a): return rd(a, 'f')
def v3(a): return (fl(a), fl(a + 4), fl(a + 8))
def p3(v): return '(%.3f, %.3f, %.3f)' % v
def tick():
    try: return rd(rd(0x0080239c) + 0x1da70c)
    except (OSError, struct.error, ValueError): return -1
def pylon(node, cell, pt):
    idx = rd(node + 0x70); model = rd(0x007a4610 + 4 * idx) if idx < 4096 else 0
    inst = rd(model + 8) if model else 0; base = rd(inst + 0x2c) if inst else 0
    rooty = fl(rd(rd(inst + 4) + 0x78) + 0x44) if inst else 0.0
    count = rd(cell + 0x30) if cell else 0
    stack = ' '.join(f"{rd(rd(cell + 0x34 + 4 * i))}:{rd(rd(cell + 0x34 + 4 * i) + 8):#x}" for i in range(min(count, 8)) if rd(cell + 0x34 + 4 * i))
    return (f" | model {idx} made {rd(model + 0xe4) if model else 0:#x} root y {rooty:.3f} base {base:#x} flags "
            f"{rd(base) if base else 0:#x} type flags {rd(pt) if pt else 0:#x} cell stack {count}: {stack}")
def snapshot():
    out, ctl, guard = [], rd(0x00790fe0), 0
    while ctl and guard < 16:
        n, smp = rd(ctl + 0xa4), rd(ctl + 0x9c)
        ys = [fl(smp + 0x38 * i + 8) for i in range(n)] if smp and 0 < n < 20000 else []
        inner = max([0.0] + ys[1:-1]) if len(ys) > 2 else 0.0
        out.append(f"coaster {ctl:#x} flags {rd(ctl):#x} type {rd(ctl + 4):#x} instance {rd(ctl + 0x10)} handle {rd(ctl + 0x14)} "
                   f"+0x3c {rd(ctl + 0x3c):#x} highest(+0x110) {fl(ctl + 0x110):.4f} topspeed(+0x114) {fl(ctl + 0x114):.4f} "
                   f"factor(type+8) {fl(rd(ctl + 4) + 8):.4f} samples {n} spacing {fl(ctl + 0xac):.3f} max y 1..n-2 {inner:.4f} all {max(ys) if ys else 0:.4f} "
                   f"at {ys.index(max(ys)) if ys else -1}")
        node, k = rd(ctl + 0x54), 0
        while node and k < 400:
            cell = rd(node + 0x100); pt = rd(cell + 0x18) if cell else 0
            kn = rd(node + 0x90); kc = rd(kn) if kn else 0
            r1 = rd(kn + 4) if kn else 0
            knots = [p3(v3(r1 + 0x58 * i + 0x40)) + (f" in {p3(v3(r1 + 0x58 * i + 0x34))} out {p3(v3(r1 + 0x58 * i + 0x4c))} "
                      f"dir {p3(v3(r1 + 0x58 * i + 0x28))} tag {rd(r1 + 0x58 * i + 4)} t {fl(r1 + 0x58 * i + 8):.4f}" if full else '')
                     for i in range(min(kc, 64))] if r1 else []
            out.append(f"  node {k} {node:#x} id {rd(node)} flags {rd(node + 8):#x} stack {rd(node + 4)} cell {rd(cell + 8) if cell else -1} "
                       f"cellplace {p3(v3(cell + 0x1c)) if cell else '-'} H {fl(pt + 0xbc) if pt else 0:.3f}..{fl(pt + 0xc0) if pt else 0:.3f} "
                       f"step {fl(pt + 0xc4) if pt else 0:.3f} loft {fl(node + 0xe8):.4f} place {p3(v3(node + 0x4c))} "
                       f"yaw {fl(node + 0xe0):.4f} bank {fl(node + 0xd8):.4f} incl {fl(node + 0xdc):.4f} +0xfc {rd(node + 0xfc)} "
                       f"knots {kc}: {' '.join(knots)}" + (pylon(node, cell, pt) if pylons else ''))
            node, k = rd(node + 0x64), k + 1
        if full:
            for i in range(n):
                a = smp + 0x38 * i
                out.append(f"  sample {i} flags {rd(a):#x} {p3(v3(a + 4))}")
        ctl, guard = rd(ctl + 0x18), guard + 1
    return '\n'.join(out) if out else 'no coaster'
t0 = time.time(); last = None
while True:
    try: s = snapshot()
    except (OSError, struct.error, ValueError) as e: s = f'unreadable ({e.__class__.__name__})'
    summary = s.split('\n')[0] if s else s
    if s != last and (last is None or summary != last.split('\n')[0] or secs == 0):
        print(f"{time.time() - t0:7.2f} s tick {tick()}\n{s}", flush=True)
    last = s
    if time.time() - t0 >= secs: break
    time.sleep(0.5)
