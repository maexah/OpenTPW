#!/usr/bin/env python3
"""stampdiff.py A.txt B.txt - two `groundcell.py READ=0,0:95,84` dumps (the original's live ground map, before and after a purchase
or a sale): the cells that gained `0x200` (a stamp), the cells that lost it, the cells that lost `0x800`, and how many words are
equal. `cellsdiff.py` lists every changed word (the `0x100` band too); this lists the three sets a footprint is judged on. Q842."""
import re, sys
def load(p):
	d = {}
	for l in open(p):
		m = re.match(r"\s*cell \((\d+),(\d+)\) base flags 0x([0-9a-f]+)", l)
		if m: d[(int(m.group(1)), int(m.group(2)))] = int(m.group(3), 16)
	return d
a, b = load(sys.argv[1]), load(sys.argv[2])
new = sorted(c for c in b if b[c] & 0x200 and not a[c] & 0x200)
gone = sorted(c for c in b if a[c] & 0x200 and not b[c] & 0x200)
lost = sorted(c for c in b if a[c] & 0x800 and not b[c] & 0x800)
print("newly stamped", len(new), new); print("stamp gone", gone); print("lost 0x800", lost)
print("words equal", sum(1 for c in a if a[c] == b[c]), "of", len(a))
