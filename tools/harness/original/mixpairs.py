#!/usr/bin/env python3
"""mixpairs.py: every (far end, volume of 127) the original's mixer log holds on a 3D channel, beside the variation
volumes of that range in the global and jungle category files that round-to-even( v x 0.75 ) makes it from.
mixpairs.py [<mixer.py log>]   (default <work>/q265/orig/a-load.log)
The log is mixer.py's output (its `map lo,hi,k volume v` lines; the 2D ones are left out). The category files are read
from the game's data, fixed in the code, through lib/sfxmap.py."""
import re, sys, os, collections
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths; import sfxmap
H = paths.WORK
D = os.path.join(paths.game(), "data")
log = sys.argv[1] if len(sys.argv) > 1 else H + '/q265/orig/a-load.log'
pairs = collections.Counter()
for l in open(log):
    m = re.search(r'map ([\d.]+),([\d.]+),([\d.]+) volume ([\d.]+)', l)
    if m and '2D' not in l: pairs[(float(m.group(2)), round(float(m.group(4)) * 127))] += 1
have = collections.defaultdict(set)
for c in sfxmap.categories(D):
    if '/global/' not in c.lower() and '/jungle/' not in c.lower(): continue
    for e in sfxmap.Category(c).effects:
        for v in e.variations:
            lo, hi = sorted(v.volume)
            for x in range(lo, hi + 1): have[v.range].add((x, round(x * 0.75), os.path.basename(c), e.id))
bad = 0
for (far, v), n in sorted(pairs.items()):
    src = sorted(set(x for x, r, _, _ in have.get(far, ()) if r == v))
    who = sorted(set((c, e) for x, r, c, e in have.get(far, ()) if r == v))[:4]
    bad += not src
    print(f'far {far:6.1f} volume {v:3d} of 127  lines {n:5d}  variation volumes {src}  e.g. {who}')
print('pairs', len(pairs), 'with no variation', bad)
