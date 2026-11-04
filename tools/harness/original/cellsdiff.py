#!/usr/bin/env python3
"""cellsdiff.py A.txt [B.txt] - the flag words of `groundcell.py READ=0,0:95,84` dumps (`cell (X,Y) base flags 0xWWWW ...`), counted and compared.

One file: the number of cells, those with 0x800 and with 0x200. Two files: the same for B, then the cells whose word differs
(`(x,y) 0xA -> 0xB`, up to LIMIT, default 40), how many differ in the whole word and in the 0x800 bit, and how many cells
agree on the 0x800 bit. The base flags are the word the game keeps; `drawn flags` are the terrain pass's copy and are ignored here.
Q885 (the footprint stamp in play): a purchase's cells lose 0x800 and gain 0x200, a sale puts the map back."""
import os, re, sys

CELL = re.compile(r'cell \((\d+),(\d+)\) base flags 0x([0-9a-fA-F]+)')

def load(path):
	cells = {}
	with open(path) as f:
		for line in f:
			m = CELL.search(line)
			if m: cells[(int(m.group(1)), int(m.group(2)))] = int(m.group(3), 16)
	return cells

def summary(name, c):
	print(f'{name}: {len(c)} cells, {sum(1 for w in c.values() if w & 0x800)} with 0x800, {sum(1 for w in c.values() if w & 0x200)} with 0x200')

def main():
	a = load(sys.argv[1]); summary(sys.argv[1], a)
	if len(sys.argv) < 3: return
	b = load(sys.argv[2]); summary(sys.argv[2], b)
	limit = int(os.environ.get('LIMIT', '40'))
	keys = sorted(set(a) | set(b), key=lambda k: (k[1], k[0]))
	diff = [k for k in keys if a.get(k) != b.get(k)]
	for k in diff[:limit]:
		print(f'  ({k[0]},{k[1]}) 0x{a.get(k, 0):04x} -> 0x{b.get(k, 0):04x}')
	if len(diff) > limit: print(f'  ... {len(diff) - limit} more')
	same800 = sum(1 for k in keys if (a.get(k, 0) & 0x800) == (b.get(k, 0) & 0x800))
	print(f'whole word differs at {len(diff)} cells; 0x800 bit agrees at {same800} of {len(keys)} cells')

main()
