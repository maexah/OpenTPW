#!/usr/bin/env python3
"""framediff.py A.png B.png [--levels N] [--out DIFF.png] [--hud] : how many pixels of two frames of the same size differ by
more than N levels (8) in any channel, the HUD left out, and the box they fill. `--out` writes the difference times 30 as a
picture (a cell's patch shows where a change landed). The HUD mask is the 1280 x 720 frame's money counter (x under 430, y under 130),
gadget (x under 240, y over 380), flag and key counters (x over 1090, y under 140) and message bar (y over 665, x 420 to 860),
scaled to the frame's height; `--hud` keeps it in. Two frames of different launches differ everywhere (the ground's tiles are
made at random, the park moves), so compare two frames of ONE session: the same camera, one thing changed (Q821's `cut old` /
`cut new`), and take a pair with nothing changed beside them as the control."""
import argparse
import numpy as np
from PIL import Image
ap = argparse.ArgumentParser(); ap.add_argument("a"); ap.add_argument("b"); ap.add_argument("--levels", type=int, default=8)
ap.add_argument("--out"); ap.add_argument("--hud", action="store_true"); a = ap.parse_args()
A = np.asarray(Image.open(a.a).convert("RGB")).astype(int); B = np.asarray(Image.open(a.b).convert("RGB")).astype(int)
if A.shape != B.shape: raise SystemExit(f"sizes differ: {A.shape} {B.shape}")
h, w = A.shape[:2]; s = h / 720
keep = np.ones((h, w), bool)
if not a.hud:
	keep[:int(130 * s), :int(430 * s)] = False; keep[int(380 * s):, :int(240 * s)] = False
	keep[:int(140 * s), int(1090 * s * w / (1280 * s)):] = False; keep[int(665 * s):, int(420 * s):int(860 * s)] = False
d = np.abs(A - B).max(axis=2) * keep
ys, xs = np.nonzero(d > a.levels)
print(f"{a.a} {a.b}: {len(ys)} pixels differ by more than {a.levels} levels" + (f", in x {xs.min()}..{xs.max()}, y {ys.min()}..{ys.max()}" if len(ys) else ""))
if a.out: Image.fromarray(np.clip(d * 30, 0, 255).astype("uint8")).save(a.out)
