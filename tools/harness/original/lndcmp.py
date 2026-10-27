#!/usr/bin/env python3
"""lndcmp.py <base.lnd> <tilecap OUTDIR> - each tile tilecap.sh caught from the ORIGINAL against lib/lndtile.py's run of
the same function on the same cell and size (Q483): a line a tile (bytes differing, of all four and of 1..3), then the
count. A tile with a record texture (a path laid over by its alpha) is listed apart: lndtile.py makes it without.
Run with $OPENTPW_EMU_PYTHON (unicorn)."""
import sys, os, glob, re
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'lib'))
import numpy as np, lndtile
lnd, d = sys.argv[1], sys.argv[2]; tilers = {}; same = diff = over = 0
for p in sorted(glob.glob(os.path.join(d, 'tile-*.bin')), key=lambda s: int(re.search(r'tile-(\d+)-', s).group(1))):
    k, x, y, n, rt, w = re.search(r'tile-(\d+)-(-?\d+)-(-?\d+)-(\d+)-r([0-9a-f]+)-w([0-9a-f]+)\.bin', p).groups()
    x, y, n = int(x), int(y), int(n)
    t = tilers.setdefault(n, lndtile.Tiler(lnd, n))
    a = np.frombuffer(open(p, 'rb').read(), np.uint8).reshape(n, n, 4)
    e = np.frombuffer(t.tile(x, y), np.uint8).reshape(n, n, 4)
    d4 = int((a != e).sum()); d3 = int((a[:, :, 1:] != e[:, :, 1:]).sum())
    tag = 'path laid over' if int(rt, 16) else ''
    print('tile %s cell (%d,%d) N %d word %s: differing bytes %d (of 1..3: %d) %s' % (k, x, y, n, w, d4, d3, tag))
    if int(rt, 16): over += 1
    elif d4 == 0: same += 1
    else: diff += 1
print('== %d tiles identical in all four bytes, %d differing, %d with a path laid over (not compared)' % (same, diff, over))
