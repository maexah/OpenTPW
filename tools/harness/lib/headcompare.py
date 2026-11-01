#!/usr/bin/env python3
"""headcompare.py LABEL OURS_SERIES.json PAUSES.jsonl... [--turn] - the original's paused heads (original/headpause.py) against this game's
(lib/headseries.py's series.json) at the same (role, clip) and channel frame, ours linear between its two nearest samples; record r is
the head numbered r + 1 (Q848) unless --map names ours' head for each record (Q916: the Crazy Ape's table is sparse; the pairing is by rest
place). A head ours dropped between its two samples is left out, not allowed to shift the others.

The original turns Mumbo's whole ring about a vertical axis as the ride runs, and this game has since Q915. The comparison keeps to what a
turn cannot change, so it holds for a build that does not turn the ring too: each head's height, and the horizontal distance between each
two heads held in the same pause. The line at the end gives the maxima and means. --turn adds, for each pause holding three or more
heads, the rigid turn (2-D rotation and shift) that best takes this game's heads onto the original's: its angle (near nought when the
ring turns as the original's) and each head's residual after it."""
import itertools, json, math, sys
MAP = json.load(open(sys.argv[sys.argv.index("--map") + 1])) if "--map" in sys.argv else None   # {"record": "HeadNN"}: which of ours' heads is the original's record
args = [a for a in sys.argv[1:] if a != "--turn" and a != "--map" and not (MAP and a == sys.argv[sys.argv.index("--map") + 1])]; TURN = "--turn" in sys.argv[1:]
label = args[0]; series = json.load(open(args[1])); pauses = [json.loads(l) for f in args[2:] for l in open(f)]
samples = [(int(s["channel"][0]), int(s["channel"][1]), float(s["channel"][2]), s["heads"]) for s in series if s["channel"]]
def ours_at(role, clip, frame):
    pts = sorted([s for s in samples if s[0] == role and s[1] == clip], key=lambda s: s[2])
    for a, b in zip(pts, pts[1:]):
        if a[2] <= frame <= b[2] and b[2] > a[2]:
            t = (frame - a[2]) / (b[2] - a[2]); out = {}
            for ha in a[3]:
                for hb in b[3]:
                    if hb[0] == ha[0]: out[ha[0]] = [ha[2][i] + t * (hb[2][i] - ha[2][i]) for i in range(3)]
            return out
    return None
hs = []; ds = []; skipped = 0
for p in pauses:
    a = p["a"]
    if not a["heads"]: continue
    ours = ours_at(a["role"], a["clip"], a["frame"])
    if not ours: skipped += 1; continue
    held = []
    for r, h, place in a["heads"]:
        if not place: continue
        name = MAP.get(str(r)) if MAP else next((n for n in ours if int("".join(c for c in n if c.isdigit())) == r + 1), None)
        if name not in ours: print(f"{label} role {a['role']} frame {a['frame']:7.2f} record {r}: ours holds no such head at both samples, left out"); continue
        v = ours[name]; dh = v[2] - place[1]; hs.append(abs(dh)); held.append((r, name, v, place))
        print(f"{label} role {a['role']} frame {a['frame']:7.2f} record {r} {name}: height ours {v[2]:.3f} original {place[1]:.3f} diff {dh:+.3f}")
    for (r1, n1, v1, o1), (r2, n2, v2, o2) in itertools.combinations(held, 2):
        d_ours = math.hypot(v1[0] - v2[0], v1[1] - v2[1]); d_orig = math.hypot(o1[0] - o2[0], o1[2] - o2[2]); ds.append(abs(d_ours - d_orig))
        print(f"{label} role {a['role']} frame {a['frame']:7.2f} heads {r1}-{r2}: horizontal distance ours {d_ours:.3f} original {d_orig:.3f} diff {d_ours - d_orig:+.3f}")
    if TURN and len(held) >= 3:
        P = [(v[0], v[1], o[0], o[2]) for _, _, v, o in held]; n = len(P)
        cx = sum(q[0] for q in P) / n; cy = sum(q[1] for q in P) / n; dx = sum(q[2] for q in P) / n; dy = sum(q[3] for q in P) / n
        s = sum((q[0] - cx) * (q[3] - dy) - (q[1] - cy) * (q[2] - dx) for q in P); c = sum((q[0] - cx) * (q[2] - dx) + (q[1] - cy) * (q[3] - dy) for q in P)
        th = math.atan2(s, c); co, si = math.cos(th), math.sin(th)
        res = [math.hypot(dx + co * (q[0] - cx) - si * (q[1] - cy) - q[2], dy + si * (q[0] - cx) + co * (q[1] - cy) - q[3]) for q in P]
        print(f"{label} role {a['role']} frame {a['frame']:7.2f} {n} heads: turn {math.degrees(th):7.2f} deg, residuals " + " ".join(f"{r:.3f}" for r in res))
print(f"{label} heights {len(hs)}: max {max(hs, default=0):.3f} mean {sum(hs) / max(1, len(hs)):.3f}; distances {len(ds)}: max {max(ds, default=0):.3f} "
      f"mean {sum(ds) / max(1, len(ds)):.3f}; pauses skipped for lack of ours samples {skipped}; pauses agreeing {sum(1 for p in pauses if p['agree'])}/{len(pauses)}")
