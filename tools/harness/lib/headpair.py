#!/usr/bin/env python3
"""headpair.py PAUSES.jsonl SERIES.json... > map.json - pairs the original's head records with this game's heads by REST place (Q916), for a ride whose
lookup table has gaps (the Crazy Ape's): ours' rest table (name -> x, depth, height, from every series given) against the original's heads read at its
loop's end (original/headpause.py's pauses at frame 112 and on). A pair needs a distance under 0.25 over x, z and height together. The JSON on stdout is
headcompare.py's --map.

ASSUMPTIONS, said because the pairing is a guess from places and not the model's table (the record-to-node table is the lookup table of the ride's model,
whose header word +0x46 offsets a record's id to its node, docs/exe/park-engine.md; reading it would replace this guess): the heads are at or next to
rest from frame 112 of a 125-frame loop (the Ape's; another ride needs another --from), and no two of ours' heads rest within 0.25 of one another. A
record that comes out as two different heads in two pauses is dropped and said on stderr (not left to the last pause); a record no head of ours rests at
is said on stderr (ours has not held that head: the Ape's records 0, 2 and 10 are in the model as Head1, Head06 and Head09 and need a longer series of
ours, Q917)."""
import json, math, sys
args = [a for i, a in enumerate(sys.argv[1:], 1) if a != "--from" and sys.argv[i - 1] != "--from"]
FROM = float(sys.argv[sys.argv.index("--from") + 1]) if "--from" in sys.argv else 112.0
rest = {}
for f in args[1:]:
    for r in json.load(open(f)):
        for h in r["heads"]: rest[h[0]] = tuple(h[3])   # x, depth, height
pairs = {}; dropped = set()
for l in open(args[0]):
    a = json.loads(l)["a"]
    if a["frame"] < FROM: continue
    for rec, hd, pl in a["heads"]:   # pl: x, height, z
        n, v = min(rest.items(), key=lambda kv: math.dist((kv[1][0], kv[1][1], kv[1][2]), (pl[0], pl[2], pl[1])))
        d = math.dist(v, (pl[0], pl[2], pl[1]))
        if d >= 0.25: print(f"record {rec}: no head of ours rests within 0.25 (nearest {n} at {d:.2f}; ours has not held that head)", file=sys.stderr); continue
        if pairs.get(str(rec), n) != n: print(f"record {rec}: paired with {pairs[str(rec)]} and with {n} in two pauses, dropped", file=sys.stderr); dropped.add(str(rec))
        pairs.setdefault(str(rec), n)
for r in dropped: del pairs[r]
print(json.dumps(pairs))
