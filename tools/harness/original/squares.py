#!/usr/bin/env python3
"""squares.py PID SECS [N] - the marker squares of the running ORIGINAL (BlueprintMesh's face list), read-only.

The build tools, a stranded person and the staff locator's patrol edges each append a square a frame to the face list
at 0x00820b00 (0xa0 bytes a record, count [0x00871ee0]; FUN_0053c8d0), and the marker draw FUN_0053c3f0 empties it
every frame (docs/exe/park-engine.md, "Placement feedback"). So the count read is mostly nought: this samples for SECS
and prints the fullest list seen, a record a line - its cell (x / 10, z / 10), its height y, its texture (+0x9c: 0 blue,
1 red, 8 m_link, 11 m_end), its face (+0x0c: 0 the flat square, 1/4/0x10/0x40 a wall on the N/E/S/W edge rising 10 from y,
FUN_0053df30), its orientation byte (+0x10) and '20 wide' where +0x18 is set (the track tool's squares, a two by two;
0x0053df39) - then the cells by texture and how many times each was laid. Textures 15 and up are not
markers: the polygon faces FUN_0053c9c0 adds to the same list (water and the like), hundreds a frame, after the
squares. With N it reads N records whatever the count, the last frame's leftovers.

  squares.py %PID% 5         the fullest list in five seconds
  squares.py %PID% 0 48      the first 48 records as they lie
"""
import collections, struct, sys, time

pid = int(sys.argv[1]); secs = float(sys.argv[2]); fixed = int(sys.argv[3]) if len(sys.argv) > 3 else None
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, n):
    f.seek(a); return f.read(n)
LIST, COUNT, SIZE = 0x00820b00, 0x00871ee0, 0xa0

best, seen, t0 = b'', 0, time.time()
if fixed is not None:
    best = rd(LIST, SIZE * fixed)
else:
    while True:
        n = struct.unpack('<I', rd(COUNT, 4))[0]
        if 0 < n <= 0x806 and n > len(best) // SIZE:
            data = rd(LIST, SIZE * n)
            if struct.unpack('<I', rd(COUNT, 4))[0] == n:   # the list did not move under the read
                best = data
        seen += 1
        if time.time() - t0 >= secs:
            break
        time.sleep(0.002)

n = len(best) // SIZE
print(f'squares {n} (samples {seen})')
cells = collections.defaultdict(collections.Counter)
for i in range(n):
    r = best[i * SIZE:(i + 1) * SIZE]
    x, y, z = struct.unpack_from('<3f', r, 0)
    tex = r[0x9c]
    wide = ' 20 wide' if struct.unpack_from('<I', r, 0x18)[0] else ''
    print(f'  {i}: cell ({x / 10:.0f},{z / 10:.0f}) y {y:.2f} tex {tex} face {r[0x0c]:#x} turn {r[0x10]}{wide}')
    cells[tex][(round(x / 10), round(z / 10))] += 1
for tex, got in sorted(cells.items()):
    twice = sorted(c for c, k in got.items() if k > 1)
    print(f'texture {tex}: {sum(got.values())} squares on {len(got)} cells'
          + (f', x, y min..max ({min(c[0] for c in got)}..{max(c[0] for c in got)}, {min(c[1] for c in got)}..{max(c[1] for c in got)})')
          + (f', laid more than once {twice}' if twice else ''))
