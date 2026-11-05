#!/usr/bin/env python3
"""headpicmatch.py PAUSES.jsonl RUN.log... [--map FILE] [--max-gap F] - this game's census `picture N` (ParkGuestSprites.HeadOnRide, ours' camera)
against the original's sprite picture (+0xb8) per head per pause (Q1003, Q1008). A head pairs when ours holds it in a sample of the same role AND
clip entry whose frame is within --max-gap (default 4.0) of the pause's. A record is Head(r+1) (the census spells the first `Head1`, the rest
`Head02`...), or --map FILE's {"record": "HeadNN"} (headpair.py's: the Crazy Ape's lookup table has gaps). Several RUN.logs pool their samples (a ride
holds other heads each time). Prints each pair and the count; exit 0. review-codex/venv/bin/python -u."""
import sys, json, re
args = sys.argv[1:]
mp = json.load(open(args[args.index("--map") + 1])) if "--map" in args else None
gap = float(args[args.index("--max-gap") + 1]) if "--max-gap" in args else 4.0
files = [a for i, a in enumerate(args) if not a.startswith("--") and (i == 0 or args[i - 1] not in ("--map", "--max-gap"))]
series = []
for log in files[1:]:
    for line in open(log, errors="replace"):
        c = re.search(r"channels \[0:role (\d+) entry (\d+) frame ([\d.]+)/", line)
        if not c or " picture " not in line: continue
        heads = {}
        for part in line.split(", "):
            m = re.search(r"'(Head\w+)' .* picture (\d+)", part)
            if m: heads[m.group(1)] = int(m.group(2))
        series.append((int(c.group(1)), int(c.group(2)), float(c.group(3)), heads, log))
same = total = edge = 0
for line in open(files[0]):
    p = json.loads(line)["a"]
    if not p or not p.get("view"): continue
    for r, info in p["pictures"]:
        keys = [mp[str(r)]] if mp is not None and str(r) in mp else ([] if mp is not None else [f"Head{r + 1}", f"Head{r + 1:02d}"])
        held = [s for s in series if s[0] == int(p["role"]) and s[1] == int(p["clip"]) and any(k in s[3] for k in keys) and abs(s[2] - p["frame"]) <= gap]
        if not held: continue
        before = max((s for s in held if s[2] <= p["frame"]), key=lambda s: s[2], default=None)
        after = min((s for s in held if s[2] >= p["frame"]), key=lambda s: s[2], default=None)
        pic = lambda s: next(s[3][k] for k in keys if k in s[3])
        near = min(held, key=lambda s: abs(s[2] - p["frame"]))
        if before and after and pic(before) != pic(after):
            edge += 1
            print(f"role {int(p['role'])} clip {int(p['clip'])} f{p['frame']:6.1f} record {r:2d} {keys[0]}: original {info['picture']:2d} ours {pic(before)} at {before[2]:.1f} and {pic(after)} at {after[2]:.1f} EDGE ({near[4].split('/')[-2]})")
            continue
        ours = pic(near)
        total += 1; same += ours == info["picture"]
        print(f"role {int(p['role'])} clip {int(p['clip'])} f{p['frame']:6.1f} record {r:2d} {keys[0]}: original {info['picture']:2d} ours {ours:2d} (frame {near[2]:.1f}, {near[4].split('/')[-2]}) {'same' if ours == info['picture'] else 'DIFFERENT'}")
print(f"{same} of {total} pairs: ours' census picture equals the original's sprite picture ({edge} more straddle a picture's edge between two of ours' samples)")
