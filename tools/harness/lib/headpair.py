#!/usr/bin/env python3
"""headpair.py PAUSES.jsonl SERIES.json... > map.json - pairs the original's head records with this game's heads by REST place (Q916), for a ride whose
lookup table is sparse (the Crazy Ape's): ours' rest table (name -> x, depth, height, from every series given) against the original's heads read at its
loop's end (original/headpause.py's pauses at frame 112 and on, where a head is at or next to rest). A pair needs a horizontal distance under 0.25; a
record no head of ours rests at is said on stderr (ours has not held that head). The JSON on stdout is headcompare.py's --map."""
import json, math, sys
rest = {}
for f in sys.argv[2:]:
    for r in json.load(open(f)):
        for h in r["heads"]: rest[h[0]] = tuple(h[3])
pairs = {}
for l in open(sys.argv[1]):
    a = json.loads(l)["a"]
    if a["frame"] < 112: continue
    for rec, hd, pl in a["heads"]:
        n, v = min(rest.items(), key=lambda kv: math.hypot(kv[1][0] - pl[0], kv[1][1] - pl[2]))
        d = math.hypot(v[0] - pl[0], v[1] - pl[2])
        if d < 0.25: pairs[str(rec)] = n
        else: print(f"record {rec}: no head of ours rests within 0.25 (nearest {n} at {d:.2f}; ours has not held that head)", file=sys.stderr)
print(json.dumps(pairs))
