#!/usr/bin/env python3
"""voices.py PID [SECS] - the ORIGINAL's live sound voices, by the service's own list (the container at
[[0x00802bcc]+4]+0x20, its head at +0x24, a voice's next at +0x34; FUN_006b62b0). A line a voice: its effect's id and
flags word, the voice's flags (+0x30: 0x80 a range of its own, 0x800 out of range and silenced, 0x1000 a rectangle
it follows the listener in, 0x8 no range), its place (+0x10), its range (+0x40) and its rectangle ([+0x4c]+8);
at the line's end its handle (+0x2c) and its class by vtable (Q560: the type a park file's voice list gives it). The
listener line ends with the crowd's handle word [0x00803aa4].
The listener is printed first (the camera, and the midpoint the park passes when [0x00790ab0] & 0x16 is clear).
One block without SECS; with it, read every 0.25 s, a block each time a voice comes, goes or changes its effect, its
flags or its place (the listener, the distance, the range and the rectangle are printed, not compared). A POLL for loadfile.sh. Read-only."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]) if len(sys.argv) > 2 else 0
mem = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    mem.seek(a); return struct.unpack('<' + fmt, mem.read(struct.calcsize('<' + fmt)))
CLASS = {0x70a498: 'music class (1)', 0x70a440: 'class 0x10 (2)', 0x70a3e8: 'one-shot (3)', 0x70a390: 'any chain (4)',
    0x70a338: 'plain (5)', 0x70a8d0: 'plain (5)', 0x70a2e0: 'held chain (6)', 0x70a878: 'class 0x100 (10)'}
def block():
    out = []
    mask = rd(0x00790ab0)[0]; cam = rd(0x007909c0, '3f'); aim = rd(0x00790ab8, '3f')
    lis = cam if mask & 0x16 else tuple((c + a) / 2 for c, a in zip(cam, aim))
    out.append('listener (%.0f,%.0f,%.0f) camera (%.0f,%.0f,%.0f) mask 0x%x crowd 0x%08x' % (*lis, *cam, mask, rd(0x00803aa4)[0]))
    cont = rd(rd(rd(0x00802bcc)[0] + 4)[0] + 0x20)[0]; v = rd(cont + 0x24)[0]
    for _ in range(400):
        if not v: break
        rec, = rd(v + 4); flags, nxt = rd(v + 0x30, '2I'); x, y, z = rd(v + 0x10, '3f'); rng, = rd(v + 0x40, 'f'); ext, = rd(v + 0x4c)
        eid, fl = (rd(rec)[0], rd(rec + 0x10, 'H')[0]) if rec else (-1, 0)
        d = ((x - lis[0]) ** 2 + (z - lis[2]) ** 2) ** 0.5
        line = 'effect %3d (0x%04x) voice flags 0x%05x at (%.0f,%.0f,%.0f) %.0f from the listener, range %.1f' % (eid, fl, flags, x, y, z, d, rng)
        if ext and flags & 0x1000: line += ' rectangle %d,%d to %d,%d' % rd(ext + 8, '4i')
        vt, = rd(v); line += ' handle 0x%08x %s' % (rd(v + 0x2c)[0], CLASS.get(vt, 'vtable 0x%x' % vt))
        out.append(line); v = nxt
    return out
last = None; end = time.time() + secs
for _ in range(100000):
    try: b = block()
    except (OSError, struct.error): b = ['(unreadable)']
    key = [l.split(' from the listener')[0] if 'effect' in l else '' for l in b]
    if key != last: print('--- %.2f' % time.time()); print('\n'.join(b), flush=True); last = key
    if time.time() >= end: break
    time.sleep(0.25)
