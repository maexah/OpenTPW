#!/usr/bin/env python3
"""diffpic.py <a.png> <b.png> <out.png> [x0 y0 x1 y1]: a | b | b dimmed with the pixels differing by more than 40 in red."""
import sys
from PIL import Image
a = Image.open(sys.argv[1]).convert('RGB'); b = Image.open(sys.argv[2]).convert('RGB')
if len(sys.argv) > 4:
    box = tuple(map(int, sys.argv[4:8])); a = a.crop(box); b = b.crop(box)
d = b.point(lambda v: v // 3); pa = a.load(); pb = b.load(); pd = d.load(); n = 0
for y in range(a.size[1]):
    for x in range(a.size[0]):
        if max(abs(pa[x, y][i] - pb[x, y][i]) for i in range(3)) > 40: pd[x, y] = (255, 0, 0); n += 1
out = Image.new('RGB', (a.size[0] * 3 + 20, a.size[1]), (255, 255, 255))
out.paste(a, (0, 0)); out.paste(b, (a.size[0] + 10, 0)); out.paste(d, (2 * a.size[0] + 20, 0)); out.save(sys.argv[3]); print(n, 'differ')
