#!/usr/bin/env python3
"""particledump.py PID EFFECT [raw] - the ORIGINAL: EVERY particle of each live emitter of EFFECT, oldest last (the chain's
order): its place less the emitter's in park units, its velocity (+0xc, three ints), colour (+0x18), size (+0x28) and
frame (+0x2c), then the spread of each of the offsets, the velocities, the size and the frame over the emitter's
particles, and the alpha values and the number of colours. `raw` adds the emitter's own 0x140 bytes and each
particle's 52 bytes as hex, for a field this does not name. At most 600 particles an emitter. spritepass.py prints
only an emitter's first six; this is the whole cloud."""
import struct, sys
pid = int(sys.argv[1]); want = int(sys.argv[2]); raw = len(sys.argv) > 3; f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<I'):
    f.seek(a); v = struct.unpack(fmt, f.read(struct.calcsize(fmt))); return v[0] if len(v) == 1 else v
pool = rd(0x0080cef8); s = rd(0x00816d1c, '<h')
for _ in range(120):
    if s < 0 or s >= 120: break
    e = 0x0080cefc + 0x140 * s
    if rd(e + 0xc, '<h') == want:
        ex, ey, ez = rd(e + 0x14, '<3i'); rows = []
        print('slot %d effect %d at (%.2f,%.2f,%.2f) own velocity %s particles %d' % (s, want, ex / 64, ey / 64, ez / 64, rd(e + 0x38, '<3i'), rd(e + 8, '<H')))
        if raw: f.seek(e); print('   emitter', f.read(0x140).hex())
        p = rd(e + 6, '<h'); n = 0
        while p >= 0 and n < 600:
            a = pool + p * 0x34; x, y, z = rd(a, '<3i'); vx, vy, vz = rd(a + 0xc, '<3i')
            row = ((x - ex) / 64, (y - ey) / 64, (z - ez) / 64, vx, vy, vz, rd(a + 0x28, '<h'), rd(a + 0x2c, '<h'), rd(a + 0x18))
            rows.append(row); extra = ''
            if raw: f.seek(a); extra = '  ' + f.read(0x34).hex()
            print('   %3d particle %3d off (%7.2f,%7.2f,%7.2f) vel (%5d,%5d,%5d) size %5d frame %2d colour %#010x%s' % (n, p, *row, extra))
            p = rd(a + 0x30, '<h'); n += 1
        for i, name in enumerate(('dx', 'dy', 'dz', 'vx', 'vy', 'vz', 'size', 'frame')):
            c = sorted(r[i] for r in rows)
            if c: print('   %-5s least %8.2f quarter %8.2f middle %8.2f three-quarter %8.2f most %8.2f' % (name, c[0], c[len(c) // 4], c[len(c) // 2], c[3 * len(c) // 4], c[-1]))
        print('   alphas', sorted(set(r[8] >> 24 for r in rows)), 'colours', len(set(r[8] for r in rows)))
    s = rd(e + 0xd0, '<h')
