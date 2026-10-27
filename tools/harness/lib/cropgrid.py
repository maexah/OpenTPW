#!/usr/bin/env python3
"""cropgrid.py IMG CX CY HALFW HALFH OUT [--scale 8] [--step 10] [--mark Y,Y,...] [--markx X,X,...]

A frame's few pixels made countable (Q470): the box CX +- HALFW by CY +- HALFH of IMG, enlarged SCALE times without
smoothing, a red rule every STEP source pixels (labelled with the frame's own pixel coordinates along the top and left
edges), and a cyan line across at each --mark y and down at each --markx x (frame pixels): put the predicted top and
bottom of a sprite there and see whether the drawn one meets them. For a person's drawn height in either game's
frame (the original's 1024 x 768, this game's 1280 x 720). Prints the box it cut."""
import argparse
from PIL import Image, ImageDraw

a = argparse.ArgumentParser()
a.add_argument('img'); a.add_argument('cx', type=float); a.add_argument('cy', type=float)
a.add_argument('halfw', type=int); a.add_argument('halfh', type=int); a.add_argument('out')
a.add_argument('--scale', type=int, default=8); a.add_argument('--step', type=int, default=10)
a.add_argument('--mark', default=''); a.add_argument('--markx', default='')
o = a.parse_args()

im = Image.open(o.img).convert('RGB')
x0, y0 = int(round(o.cx)) - o.halfw, int(round(o.cy)) - o.halfh
x1, y1 = x0 + 2 * o.halfw, y0 + 2 * o.halfh
k = o.scale; pad = 40
c = im.crop((x0, y0, x1, y1)).resize(((x1 - x0) * k, (y1 - y0) * k), Image.NEAREST)
out = Image.new('RGB', (c.width + pad, c.height + pad), (0, 0, 0)); out.paste(c, (pad, pad))
d = ImageDraw.Draw(out)
for x in range(x0 - x0 % o.step + o.step, x1, o.step):
    X = pad + (x - x0) * k; d.line([(X, pad), (X, out.height)], fill=(255, 0, 0)); d.text((X + 2, 4), str(x), fill=(255, 255, 255))
for y in range(y0 - y0 % o.step + o.step, y1, o.step):
    Y = pad + (y - y0) * k; d.line([(pad, Y), (out.width, Y)], fill=(255, 0, 0)); d.text((2, Y + 2), str(y), fill=(255, 255, 255))
for y in (float(v) for v in o.mark.split(',') if v):
    Y = pad + (y - y0) * k; d.line([(pad, Y), (out.width, Y)], fill=(0, 255, 255), width=2)
for x in (float(v) for v in o.markx.split(',') if v):
    X = pad + (x - x0) * k; d.line([(X, pad), (X, out.height)], fill=(0, 255, 255), width=2)
out.save(o.out)
print(f'cropgrid: {o.img} box ({x0},{y0})-({x1},{y1}) x{k} -> {o.out}')
