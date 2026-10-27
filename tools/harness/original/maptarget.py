#!/usr/bin/env python3
"""maptarget.py GRIDFILE what - a frame point (1024 x 768, the original at 800 x 600) for a hover target, from a mapgrid.py
'records' dump: 'staff' the shown s record; 'guest' the shown v record farthest from every other shown record (prints
its thing id too); 'thing:N' the shown record of thing N; 'cell:X,Y' the centre of cell (X,Y) from the dump's centre."""
import re, sys
recs = []
for l in open(sys.argv[1]):
	m = re.search(r'rec (\d+): sheet (\w) .* shown (\d+) at \(([\d.]+),([\d.]+)\) screen \(([\d.]+),([\d.]+)\)(?: thing (\d+))?', l)
	if m: recs.append((int(m[1]), m[2], int(m[3]), float(m[4]), float(m[5]), float(m[6]), float(m[7]), int(m[8] or 0)))
shown = [r for r in recs if r[2]]
fr = lambda sx, sy: (round(sx * 1.28), round((600 - sy) * 1.28))
w = sys.argv[2]
if w == 'staff': r = [r for r in shown if r[1] == 's'][0]
elif w == 'guest':
	vs = [r for r in shown if r[1] == 'v']
	r = max(vs, key=lambda a: min(((a[5]-b[5])**2 + (a[6]-b[6])**2) ** .5 for b in shown if b is not a))
elif w.startswith('thing:'): r = [r for r in shown if r[7] == int(w[6:])][0]
elif w.startswith('cell:'):
	cx, cy = map(float, w[5:].split(','))
	a = shown[0]; ox, oy = a[5] - 5 * a[3], a[6] - 5 * a[4]   # zoom 1: 5 game pixels a cell
	x, y = fr(ox + 5 * (cx + .5), oy + 5 * (cy + .5)); print(x, y); sys.exit()
x, y = fr(r[5], r[6]); print(x, y, r[7])
