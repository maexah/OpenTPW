#!/usr/bin/env python3
"""outside.py PID [X,Y] [N] - the running ORIGINAL's map cells outside the park nearest cell (X,Y) (default the camera's
point, 0x007908f0 over ten), read-only (Q439): a cell's mFlags is the word at +0xe of its 0x44-byte record ([0x0080239c]
+ 0x2d8 + (y x 128 + x) x 0x44), and bit 0x40 is outside the park, the bit FUN_00532a50 tests through FUN_00535db0.
Prints the N (default 5) nearest runs of three or more such cells along a row, nearest first, as 'x0 x1 y distance';
the patrol tool refuses a rectangle all of whose cells are outside (hud.md, "The patrol switch and the patrol tool")."""
import math, struct, sys
pid = int(sys.argv[1]); f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, n): f.seek(a); return f.read(n)
if len(sys.argv) > 2 and ',' in sys.argv[2]:
    cx, cy = map(float, sys.argv[2].split(','))
else:
    x, _, z = struct.unpack('<3f', rd(0x007908f0, 12)); cx, cy = x / 10, z / 10
n = int(sys.argv[-1]) if len(sys.argv) > 2 and ',' not in sys.argv[-1] else 5
world, = struct.unpack('<I', rd(0x0080239c, 4)); cells = rd(world + 0x2d8, 16384 * 0x44)
out = [[bool(struct.unpack_from('<H', cells, (y * 128 + x) * 0x44 + 0xe)[0] & 0x40) for x in range(128)] for y in range(128)]
runs = []
for y in range(128):
    x = 0
    while x < 128:
        if out[y][x]:
            x0 = x
            while x < 128 and out[y][x]: x += 1
            if x - x0 >= 3:
                nx = min(max(cx, x0), x - 1)
                runs.append((math.hypot(nx - cx, y - cy), x0, x - 1, y))
        else:
            x += 1
print(f'# camera cell ({cx:.2f}, {cy:.2f}); {sum(map(sum, out))} cells outside the park', file=sys.stderr)
for d, x0, x1, y in sorted(runs)[:n]: print(x0, x1, y, f'{d:.1f}')
