#!/usr/bin/env python3
"""shadowdiff.py ON.png OFF.png CX CY [HALF] [--scale S] [--out crop.png] [--near X,Y] - a person's shadow measured from
a frame pair of either game, one with shadows and one without (everything else equal): the pixels within HALF of (CX, CY)
darker in ON by more than 6 on the sum of channels (with --near, only the 8-connected group of them nearest the frame
point X,Y: one person's shadow), their box relative to (CX, CY) (times S, to put a frame of another
height on this one's), the median per-channel ratio ON/OFF over them (the darkening), and a crop, 8x, the shadow's
pixels outlined. Q480. Run it with review-codex/venv/bin/python (numpy, PIL)."""
import sys
import numpy as np
from PIL import Image
a = sys.argv[1:]; scale = 1.0; out = None
if "--scale" in a: i = a.index("--scale"); scale = float(a[i + 1]); del a[i:i + 2]
near = None
if "--near" in a: i = a.index("--near"); near = tuple(float(v) for v in a[i + 1].split(",")); del a[i:i + 2]
if "--out" in a: i = a.index("--out"); out = a[i + 1]; del a[i:i + 2]
on = np.asarray(Image.open(a[0]).convert("RGB")).astype(float)
off = np.asarray(Image.open(a[1]).convert("RGB")).astype(float)
cx, cy = float(a[2]), float(a[3]); half = int(a[4]) if len(a) > 4 else 60
x0, y0 = int(cx - half), int(cy - half)
won, woff = on[y0:y0 + 2 * half, x0:x0 + 2 * half], off[y0:y0 + 2 * half, x0:x0 + 2 * half]
darker = (woff.sum(2) - won.sum(2)) > 6
lighter = (won.sum(2) - woff.sum(2)) > 6
if near is not None and darker.any():
    from collections import deque
    pts = np.argwhere(darker); k = np.argmin((pts[:, 1] + x0 - near[0]) ** 2 + (pts[:, 0] + y0 - near[1]) ** 2)
    keep = np.zeros_like(darker); q = deque([tuple(pts[k])]); keep[pts[k][0], pts[k][1]] = True
    while q:
        y, x = q.popleft()
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                yy, xx = y + dy, x + dx
                if 0 <= yy < darker.shape[0] and 0 <= xx < darker.shape[1] and darker[yy, xx] and not keep[yy, xx]:
                    keep[yy, xx] = True; q.append((yy, xx))
    darker = keep
ys, xs = np.nonzero(darker)
print(f"darker {darker.sum()} px, lighter {lighter.sum()} px; whole frame differing {(np.abs(on - off).sum(2) > 6).sum()}")
if len(xs):
    print(f"box rel ({cx},{cy}) x {(xs.min() + x0 - cx) * scale:.1f}..{(xs.max() + x0 - cx) * scale:.1f} "
          f"y {(ys.min() + y0 - cy) * scale:.1f}..{(ys.max() + y0 - cy) * scale:.1f} (x{scale})")
    # The interior: darker pixels whose 8 neighbours are all darker too.
    inner = darker.copy()
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            inner &= np.roll(np.roll(darker, dy, 0), dx, 1)
    sel = inner if inner.sum() > 20 else darker
    with np.errstate(divide="ignore", invalid="ignore"):
        r = won[sel] / np.maximum(woff[sel], 1)
    ok = woff[sel].min(1) > 40
    print(f"ratio on/off over {ok.sum()} interior px (off channels > 40): median R {np.median(r[ok, 0]):.3f} G {np.median(r[ok, 1]):.3f} "
          f"B {np.median(r[ok, 2]):.3f}; all {np.median(r[ok]):.3f}, quartiles {np.percentile(r[ok], 25):.3f}..{np.percentile(r[ok], 75):.3f}")
if out:
    crop = Image.fromarray(won.astype(np.uint8)).resize((16 * half, 16 * half), Image.NEAREST)
    m = np.zeros((2 * half, 2 * half, 3), np.uint8); m[darker] = (255, 0, 255)
    edge = darker & ~np.roll(darker, 1, 0) | darker & ~np.roll(darker, 1, 1)
    arr = np.asarray(crop).copy(); big = np.kron(edge, np.ones((8, 8), bool))
    arr[big] = (255, 0, 255); Image.fromarray(arr).save(out)
