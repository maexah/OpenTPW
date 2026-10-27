#!/usr/bin/env python3
"""gaugesplit.py - where the happiness gauge's boundary stands in a frame of either game (Q244).

    gaugesplit.py <frame.png> <x0> <y0> <x1> <y1> [<meter.png>]

The gauge is meter.wct's two halves cropped at one boundary (docs/exe/hud.md, "The happiness gauge's two quads"): the lit
left half below it, the dark right half above it, each row of the box showing the texture's row at the same height. So
each row of the box (frame pixels, x1/y1 exclusive) is scored against both halves' rows at that height, stretched to the
box's width, over the texels opaque in both; the split k (rows lit from the bottom) with the least total difference is
the boundary. Prints the box, the split as a share of the height (0 to 100, comparable with the meter's percentage), the
fit's mean difference either side, and the rows' verdicts from the top (L lit, D dark, - no opaque texel). meter.png is
lib/wctpng.py's decode of ui.wad's textures\\meter.wct (default: lib/meter.png beside it). A misplaced box shows as a poor fit."""
import sys, os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
from PIL import Image

def split(frame, box, meter=os.path.join(paths.LIB, "meter.png")):
    x0, y0, x1, y1 = box
    f = Image.open(frame).convert("RGB"); t = Image.open(meter).convert("RGBA")
    W, H = x1 - x0, y1 - y0; tw, th = t.size; half = tw // 2
    fp = f.load(); tp = t.load(); dl, dd = [], []
    for r in range(H):
        ty = min(th - 1, int((r + 0.5) * th / H)); sl = sd = n = 0
        for c in range(W):
            tx = min(half - 1, int((c + 0.5) * half / W))
            a, b = tp[tx, ty], tp[half + tx, ty]
            if a[3] < 200 or b[3] < 200: continue
            p = fp[x0 + c, y0 + r]
            sl += sum((p[i] - a[i]) ** 2 for i in range(3)); sd += sum((p[i] - b[i]) ** 2 for i in range(3)); n += 1
        dl.append(sl / n if n else None); dd.append(sd / n if n else None)
    best = None
    for k in range(H + 1):   # the bottom k rows lit
        cost = sum((dl[r] if r >= H - k else dd[r]) for r in range(H) if dl[r] is not None)
        if best is None or cost < best[0]: best = (cost, k)
    k = best[1]
    verdict = "".join("-" if dl[r] is None else ("L" if dl[r] < dd[r] else "D") for r in range(H))
    lit = [dl[r] for r in range(H - k, H) if dl[r] is not None]; dark = [dd[r] for r in range(H - k) if dd[r] is not None]
    return dict(box=box, rows=H, lit_rows=k, share=100.0 * k / H if H else 0,
                fit_lit=(sum(lit) / len(lit)) ** 0.5 if lit else None, fit_dark=(sum(dark) / len(dark)) ** 0.5 if dark else None,
                verdict=verdict)

if __name__ == "__main__":
    a = sys.argv; s = split(a[1], tuple(int(v) for v in a[2:6]), *a[6:7])
    fl = "%.1f" % s["fit_lit"] if s["fit_lit"] is not None else "-"; fd = "%.1f" % s["fit_dark"] if s["fit_dark"] is not None else "-"
    print("%s box %s: lit %d of %d rows from the bottom, the boundary %.1f%% up; fit (rms RGB distance) lit %s dark %s"
          % (os.path.basename(a[1]), s["box"], s["lit_rows"], s["rows"], s["share"], fl, fd))
    print("  rows from the top:", s["verdict"])
