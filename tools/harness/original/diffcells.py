"""diffcells.py A.png B.png - where two frames of the original's map differ (channel sum > 30), inside the field
150..880 x 20..560, as cell boxes: frame x = 455 + (cx - 39) x 6.4, frame y = 365 + (29 - cy) x 6.4 (the stock park's map
at zoom 1 on (48,26), 800 x 600 in a 1024 x 768 frame; calibrated on the handyman's patrol (39,21)-(56,28) in a-s01).
Prints the changed pixel count and the cells holding more than 4 changed pixels, grouped into boxes."""
import sys
from PIL import Image
a = Image.open(sys.argv[1]).convert('RGB').load(); b = Image.open(sys.argv[2]).convert('RGB').load()
cells = {}
n = 0
for y in range(20, 560):
	for x in range(150, 880):
		if sum(abs(a[x, y][i] - b[x, y][i]) for i in range(3)) > 30:
			n += 1
			c = (int((x - 455) / 6.4 + 39), int(29 - (y - 365) / 6.4 + 1) - 1 if (y - 365) % 6.4 else int(29 - (y - 365) / 6.4))
			cx = int((x - 455) // 6.4) + 39; cy = 28 - int((y - 365) // 6.4)
			cells[(cx, cy)] = cells.get((cx, cy), 0) + 1
hot = {c for c, k in cells.items() if k > 4}
# group 4-connected
boxes = []
seen = set()
for c in sorted(hot):
	if c in seen: continue
	st = [c]; seen.add(c); grp = []
	while st:
		p = st.pop(); grp.append(p)
		for d in ((1,0),(-1,0),(0,1),(0,-1)):
			q = (p[0]+d[0], p[1]+d[1])
			if q in hot and q not in seen: seen.add(q); st.append(q)
	xs = [p[0] for p in grp]; ys = [p[1] for p in grp]
	boxes.append((min(xs), min(ys), max(xs), max(ys), len(grp)))
print(f'{sys.argv[1]} -> {sys.argv[2]}: {n} px changed; ' + ', '.join(f'({x0},{y0})-({x1},{y1}) {k}c' for x0, y0, x1, y1, k in boxes))
