"""maprects.py SHOT... - the blue outline (B > R + 60 and B > G + 60, inside the map's field 150..880 x 20..560) of each
frame of the original's map: its bounding box, width and height in frame pixels, and its mean colour."""
import sys
from PIL import Image
for f in sys.argv[1:]:
	im = Image.open(f).convert('RGB'); px = im.load()
	pts = [(x, y) for y in range(20, 560) for x in range(150, 880)
	       if px[x, y][2] > px[x, y][0] + 60 and px[x, y][2] > px[x, y][1] + 60]
	if not pts: print(f, 'none'); continue
	xs = [p[0] for p in pts]; ys = [p[1] for p in pts]
	c = [sum(px[p][i] for p in pts) // len(pts) for i in range(3)]
	print(f'{f}: {len(pts)} px, box ({min(xs)},{min(ys)})-({max(xs)},{max(ys)}) {max(xs)-min(xs)+1} x {max(ys)-min(ys)+1}, mean rgb {tuple(c)}')
