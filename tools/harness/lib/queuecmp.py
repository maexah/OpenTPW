#!/usr/bin/env python3
"""queuecmp.py OURS.out ORIGDIR a=b ... : compare the cell lines of ours' '== N-name' blocks with the original's queuecells.py files (ORIGDIR/NAME.txt).
Pairs: ours-block-prefix=orig-file-stem, e.g. 1-edit=t1-edit. Prints each differing cell line, the tools' modes/anchors/pending counts/click counters,
and the click counter's step from the previous pair (a refused click moves neither game's counter; Q1038)."""
import re, sys
ours = open(sys.argv[1], errors='replace').read()
blocks = {}
cur = None
for l in ours.splitlines():
    m = re.match(r"== (\S+): balance (-?\d+)\s+\[dbg\] tool: mode (\d+)(?:, anchored at \((\d+),(\d+)\)|, not anchored)?, (\d+) pending, (\d+) clicks", l)
    if m: cur = m.group(1); blocks[cur] = dict(tool=(int(m.group(3)), (int(m.group(4)), int(m.group(5))) if m.group(4) else None, int(m.group(6)), int(m.group(7))), bal=int(m.group(2)), cells={}); continue
    m = re.match(r"\s+\((\d+),(\d+)\) type", l)
    if m and cur: blocks[cur]['cells'][(int(m.group(1)), int(m.group(2)))] = l.strip()
orig = sys.argv[2]
prev = None
for pair in sys.argv[3:]:
    a, b = pair.split('=')
    t = open(f"{orig}/{b}.txt", errors='replace').read().splitlines()
    m = re.match(r".*mode (\d+) anchor \((-?\d+),(-?\d+)\) clicks (\d+) pending (\d+) .* balance (-?\d+)", t[1] if t[0].startswith('==') else t[0])
    otool = (int(m.group(1)), (int(m.group(2)), int(m.group(3))) if int(m.group(2)) >= 0 else None, int(m.group(5)), int(m.group(4))); obal = int(m.group(6))
    oc = {}
    for l in t:
        mm = re.match(r"\s+\((\d+),(\d+)\) type", l)
        if mm: oc[(int(mm.group(1)), int(mm.group(2)))] = l.strip()
    ob = blocks[a]; diffs = [(c, ob['cells'].get(c), oc[c]) for c in oc if ob['cells'].get(c) != oc[c]]
    print(f"{a} vs {b}: cells differing {len(diffs)} of {len(oc)}; tool ours (mode, anchor, pending, clicks) {ob['tool']} original {otool}; balance ours {ob['bal']} original {obal}")
    step = (ob['tool'][3] - otool[3])
    if prev is not None and step != prev:
        print(f"    click counter: the gap between the games moved from {prev} to {step}")
    prev = step
    for c, x, y in diffs: print(f"    ours     {x}\n    original {y}")
