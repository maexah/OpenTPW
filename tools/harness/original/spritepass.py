#!/usr/bin/env python3
"""spritepass.py PID - the ORIGINAL's sprite pass inputs: the matrix SpriteBatch_DrawParticles projects a world
particle by ([0x0087b088] -> 16 floats), DAT_008bcbcc, and every live emitter's draw words and its first six particles
(place / 64 in world units, size at +0x28, frame +0x2c, colour +0x18)."""
import struct, sys
pid = int(sys.argv[1]); f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<I'):
    f.seek(a); v = struct.unpack(fmt, f.read(struct.calcsize(fmt))); return v[0] if len(v) == 1 else v
m = rd(0x0087b088); M = rd(m, '<16f')
print('matrix at %#x' % m)
for r in range(4): print('   ', ' '.join('%11.5f' % M[c * 4 + r] for c in range(4)), '  (row %d of the product: out[%d] = this . (x, y, z, 1))' % (r, r))
print('DAT_008bcbcc', rd(0x008bcbcc, '<f'))
pool = rd(0x0080cef8); s = rd(0x00816d1c, '<h')
for _ in range(120):
    if s < 0 or s >= 120: break
    e = 0x0080cefc + 0x140 * s
    print('slot %d effect %d onscreen %d particles %d flags %#x scale %d set %d frames %d at (%d,%d,%d)' % (s, rd(e + 0xc, '<h'), rd(e + 5, '<B'), rd(e + 8, '<H'),
          rd(e + 0x70), rd(e + 0xa2, '<h'), rd(e + 0x94, '<h'), rd(e + 0x96, '<h'), rd(e + 0x14, '<i') >> 6, rd(e + 0x18, '<i') >> 6, rd(e + 0x1c, '<i') >> 6))
    p = rd(e + 6, '<h'); n = 0
    while p >= 0 and n < 6:
        a = pool + p * 0x34; x, y, z = rd(a, '<3i')
        cx = [M[0] * x / 64 + M[4] * y / 64 + M[8] * z / 64 + M[12], M[1] * x / 64 + M[5] * y / 64 + M[9] * z / 64 + M[13], M[2] * x / 64 + M[6] * y / 64 + M[10] * z / 64 + M[14], M[3] * x / 64 + M[7] * y / 64 + M[11] * z / 64 + M[15]]
        size = rd(a + 0x28, '<h')
        print('     particle %d at (%.2f,%.2f,%.2f) size %d frame %d colour %#010x  clip (%.3f,%.3f,%.3f,%.3f) half height %.5f -> %.2f px of 768' % (p, x / 64, y / 64, z / 64, size, rd(a + 0x2c, '<h'), rd(a + 0x18), *cx, size * 2 / 1024 * 0.5, (size / 1024) / cx[3] * 384 if cx[3] else 0))
        p = rd(a + 0x30, '<h'); n += 1
    s = rd(e + 0xd0, '<h')
