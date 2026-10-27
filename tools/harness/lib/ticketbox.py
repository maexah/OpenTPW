"""The big ticket's box in a frame: the largest connected run of its near-white border, in units of the frame's height,
measured from the advisor's anchor (0.8 across and 0.8 down the 4:3 interface, which this game pins to the window's
right edge). ticketbox.py <png> [interface-left-px] (Q599)"""
import sys
import numpy as np
from PIL import Image
from collections import deque
p = sys.argv[1]; a = np.asarray(Image.open(p).convert('RGB')).astype(int); h, w, _ = a.shape
left = float(sys.argv[2]) if len(sys.argv) > 2 else w - h * 4 / 3
m = (a.min(axis=2) > 225) & ((a.max(axis=2) - a.min(axis=2)) < 30)
pad = np.zeros((h + 4, w + 4), bool)
for dy in range(-2, 3):
    for dx in range(-2, 3): pad[2 + dy:2 + dy + h, 2 + dx:2 + dx + w] |= m
grown = pad[2:2 + h, 2:2 + w]; seen = np.zeros_like(grown); best = []
for y0, x0 in zip(*np.nonzero(grown)):
    if seen[y0, x0]: continue
    q = deque([(y0, x0)]); seen[y0, x0] = True; comp = []
    while q:
        y, x = q.popleft(); comp.append((y, x))
        for ny, nx in ((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)):
            if 0 <= ny < h and 0 <= nx < w and grown[ny, nx] and not seen[ny, nx]: seen[ny, nx] = True; q.append((ny, nx))
    if len(comp) > len(best): best = comp
c = np.array(best); keep = m[c[:, 0], c[:, 1]]; ys, xs = c[keep, 0], c[keep, 1]
ax, ay = left + 0.8 * h * 4 / 3, 0.8 * h
print('%s %dx%d border px %d box x %d..%d y %d..%d  width %.3f H  height %.3f H  centre from anchor (%.3f, %.3f) H'
      % (p, w, h, len(xs), xs.min(), xs.max(), ys.min(), ys.max(), (xs.max() - xs.min()) / h, (ys.max() - ys.min()) / h,
         ((xs.max() + xs.min()) / 2 - ax) / h, ((ys.max() + ys.min()) / 2 - ay) / h))
