#!/usr/bin/env python3
"""assetdiff.py <before> <after> [depth]: what a scene left behind, by name (Q832).

Each file holds the console's `assets list` replies (lines `asset <Type> <path or shape>`, with or without the
`[dbg] ` prefix a game log carries). For each asset type it prints how many of `after` are not in `before` (new), how
many of `before` are gone, and how many stay, with the new ones grouped by the first `depth` folders of their path (a file directly inside fewer folders groups under its folders, not by its name; the twelve largest groups print, then one line for the rest)
(default 3; an asset with no path groups under its shape, "128x128", or its shader). Counts are by line, so a path
asked for twice counts twice: the duplicates the console's `assets` summary warns of show here as a count.
`census( text )` returns the Counter of one reply's `(type, what)`; `diff( before, after )` returns
`{type: (new, gone, kept)}` of Counters. Reads files only; touches no game."""
import re, sys
from collections import Counter

LINE = re.compile(r"(?:\[dbg\]\s+)?asset (\S+) (.*)$")

def census(text):
    found = Counter()
    for line in text.splitlines():
        m = LINE.search(line)
        if m: found[(m.group(1), m.group(2).strip())] += 1
    return found

def diff(before, after):
    out = {}
    for kind in sorted({k for k, _ in before} | {k for k, _ in after}):
        b = Counter({w: n for (k, w), n in before.items() if k == kind})
        a = Counter({w: n for (k, w), n in after.items() if k == kind})
        out[kind] = (a - b, b - a, a & b)
    return out

def group(what, depth):
    if what.startswith("shader "): return "shader " + what.rsplit("/", 1)[-1]
    parts = what.strip("/").split("/")
    return "/".join(parts[:depth] if len(parts) > depth else parts[:-1] or parts)

def main(argv):
    if len(argv) < 3: raise SystemExit(__doc__)
    depth = int(argv[3]) if len(argv) > 3 else 3
    before, after = census(open(argv[1], errors="replace").read()), census(open(argv[2], errors="replace").read())
    for kind, (new, gone, kept) in diff(before, after).items():
        print(f"{kind}: new {sum(new.values())}, gone {sum(gone.values())}, kept {sum(kept.values())}")
        grouped = Counter()
        for what, n in new.items(): grouped[group(what, depth)] += n
        shown = grouped.most_common(12)
        for name, n in shown: print(f"    new {n:5d}  {name}")
        rest = sum(grouped.values()) - sum(n for _, n in shown)
        if rest: print(f"    new {rest:5d}  (others, {len(grouped) - len(shown)} groups)")

if __name__ == "__main__":
    main(sys.argv)
