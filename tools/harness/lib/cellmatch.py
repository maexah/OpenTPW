#!/usr/bin/env python3
"""Which procedural ground tile each game draws on a cell (Q488).

    cellmatch.py AIM SHEET X0,X1,Y0,Y1 FRAME...

AIM is the original's `camera.py PID --aim X,Y` output for six or more named cells, one 'cell X,Y: PX PY' line each
(at one camera; q488/orig/run.sh writes it). From them a homography from the ground (cell units) to the original's
1024 x 768 frame. SHEET is lndsheet.py's file. Each cell of the box X0..X1, Y0..Y1 that stands inside the frame is cut
out of each FRAME (a frame of 1280 x 720 is OpenTPW's, taken at the same camera with lib/bothcam.py, and mapped by
(640,360) + (o - (512,384)) x 0.9375) and matched, by the correlation of R - G, against the sheet's tile of the cell
and of its eight neighbours; prints the tally of the best offset (dx, dy), and the mean colour of the cells matched. The
original at Q479's camera A: (0, 0) on 84 of 122 cells; a game drawing each cell's own tile reads the same.
ONLY=FILE matches only the cells FILE names, a cell a line ('X Y ...', the rest ignored), inside the box (Q489: the
path cells, against lndsheet.py --paths's sheet). PAIR=FRAME0 instead correlates each cell's cut from each FRAME with
the same cell's from FRAME0 (the luminance of the N x N samples; either game's frame, mapped as above) and prints the
mean over the cells: two games drawing a cell alike read near 1 (Q489)."""
import sys, re, numpy as np
from PIL import Image
aim, sheetpath, box = sys.argv[1], sys.argv[2], tuple(map(int, sys.argv[3].split(',')))
pts = [(tuple(map(int, m.group(1, 2))), tuple(map(int, m.group(3, 4))))
       for m in re.finditer(r'cell (\d+),(\d+): (-?\d+) (-?\d+)', open(aim).read())]
A = []
for (x, y), (u, v) in pts:
    X, Y = x + 0.5, y + 0.5
    A.append([X, Y, 1, 0, 0, 0, -u * X, -u * Y, -u]); A.append([0, 0, 0, X, Y, 1, -v * X, -v * Y, -v])
H = np.linalg.svd(np.array(A, float))[2][-1].reshape(3, 3)
def px(X, Y):
    p = H @ np.array([X, Y, 1.0]); return p[0] / p[2], p[1] / p[2]
res = max(np.hypot(*(np.array(px(x + 0.5, y + 0.5)) - (u, v))) for (x, y), (u, v) in pts)
print('homography from %d cells, worst residual %.1f px' % (len(pts), res))
raw = np.fromfile(sheetpath, np.uint8); n = int(round((raw.size / (85 * 96 * 3)) ** 0.5))
sheet = raw.reshape(85, 96, n, n, 3).astype(float)
def load(path):
    im = Image.open(path).convert('RGB')
    if im.size == (1280, 720):
        x0 = 640 - 512 * 0.9375; y0 = 360 - 384 * 0.9375
        im = im.crop((int(x0), int(y0), int(x0 + 960), int(y0 + 720))).resize((1024, 768))
    return np.asarray(im, float)
def cut(img, x, y):
    out = np.zeros((n, n, 3))
    for k in range(n):
        for j in range(n):
            u, v = px(x + (j + 0.5) / n, y + (k + 0.5) / n)
            if not (0 <= u < 1023 and 0 <= v < 767): return None
            out[k, j] = img[int(v), int(u)]
    return out
x0, x1, y0, y1 = box
import os
only = {tuple(map(int, l.split()[:2])) for l in open(os.environ['ONLY']) if l.strip()} if os.environ.get('ONLY') else None
pair = load(os.environ['PAIR']) if os.environ.get('PAIR') else None
for name in sys.argv[4:]:
    img = load(name); tally = {}; colours = []; rs = []
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if only is not None and (x, y) not in only: continue
            c = cut(img, x, y)
            if c is None: continue
            colours.append(c.reshape(-1, 3).mean(0))
            if pair is not None:
                p = cut(pair, x, y); a, b = c @ (0.299, 0.587, 0.114), p @ (0.299, 0.587, 0.114)
                if a.std() > 0 and b.std() > 0: rs.append(np.corrcoef(a.ravel(), b.ravel())[0, 1])
                continue
            s = c[..., 0] - c[..., 1]
            if s.std() < 3: continue
            best = max(((np.corrcoef(s.ravel(), (sheet[y + dy, x + dx][..., 0] - sheet[y + dy, x + dx][..., 1]).ravel())[0, 1], dx, dy)
                        for dy in (-1, 0, 1) for dx in (-1, 0, 1) if 0 <= y + dy < 85 and 0 <= x + dx < 96), key=lambda t: t[0])
            tally[(best[1], best[2])] = tally.get((best[1], best[2]), 0) + 1
    if pair is not None:
        print(name, 'against', os.environ['PAIR'], ': mean luminance correlation %.3f over %d cells, mean RGB %s' % (np.mean(rs), len(rs), np.round(np.mean(colours, 0), 1)))
        continue
    print(name, 'best tile offset (dx, dy) by cells:', dict(sorted(tally.items(), key=lambda kv: -kv[1])),
          'mean RGB of the %d cells cut: %s' % (len(colours), np.round(np.mean(colours, 0), 1) if colours else '-'))
