#!/usr/bin/env python3
"""headresidual.py PAUSES.jsonl run.log... [--map FILE] [--max-gap F] - how far this game's drawn turn of each ride head is from the original's stored matrix (Q947, Q1009).

pauses.jsonl is `original/headpause.py`'s (each head's `pictures` matrix at record+4); run.log is a `headseries.py` run of this game whose `rides`
lines carry each head's `drawnturn [...]` (`LobbyModel.TryGetDrawnNodeTurn`). Per pause and head (record r is Head(r+1); the census spells the
first `Head1` and the rest `Head02`..): the angle of the rotation taking ours' turn onto the original's, and its axis in the node's frame
(an axis with y near nought is a tilt of the node's up). Ours is the nearest frame of the same role AND clip entry (the pause's `clip`), within --max-gap
(default 4.0) frames; several run.logs pool their samples. --map FILE is headpair.py's / ape-records.json's {"record": "HeadNN"} (the Crazy Ape's lookup
table has gaps); without it record r is Head(r+1). --interp turns ours at the pause's frame, the rotation slerped between its two samples either side
(only where both are within --max-gap and of the same role and clip entry), so the frame gap is not in the residual. Ends with the pair count and the largest
residual. Needs no display."""
import sys, json, math
sys.path.insert(0, __import__("os").path.dirname(__import__("os").path.abspath(__file__)))
import headframe as H


def quat(r):
    """The unit quaternion (w, x, y, z) of a row-vector rotation matrix r (rows are the images of the axes)."""
    t = r[0][0] + r[1][1] + r[2][2]
    if t > 0:
        k = math.sqrt(t + 1) * 2
        q = [k / 4, (r[1][2] - r[2][1]) / k, (r[2][0] - r[0][2]) / k, (r[0][1] - r[1][0]) / k]
    elif r[0][0] >= r[1][1] and r[0][0] >= r[2][2]:
        k = math.sqrt(1 + r[0][0] - r[1][1] - r[2][2]) * 2
        q = [(r[1][2] - r[2][1]) / k, k / 4, (r[0][1] + r[1][0]) / k, (r[2][0] + r[0][2]) / k]
    elif r[1][1] >= r[2][2]:
        k = math.sqrt(1 + r[1][1] - r[0][0] - r[2][2]) * 2
        q = [(r[2][0] - r[0][2]) / k, (r[0][1] + r[1][0]) / k, k / 4, (r[1][2] + r[2][1]) / k]
    else:
        k = math.sqrt(1 + r[2][2] - r[0][0] - r[1][1]) * 2
        q = [(r[0][1] - r[1][0]) / k, (r[2][0] + r[0][2]) / k, (r[1][2] + r[2][1]) / k, k / 4]
    n = math.sqrt(sum(a * a for a in q))
    return [a / n for a in q]


def unquat(q):
    w, x, y, z = q
    return [[1 - 2 * (y * y + z * z), 2 * (x * y + w * z), 2 * (x * z - w * y)],
            [2 * (x * y - w * z), 1 - 2 * (x * x + z * z), 2 * (y * z + w * x)],
            [2 * (x * z + w * y), 2 * (y * z - w * x), 1 - 2 * (x * x + y * y)]]


def slerp(a, b, t):
    qa, qb = quat(a), quat(b)
    d = sum(x * y for x, y in zip(qa, qb))
    if d < 0:
        qb, d = [-x for x in qb], -d
    if d > 0.9995:
        q = [x + t * (y - x) for x, y in zip(qa, qb)]
    else:
        th = math.acos(d)
        q = [(math.sin((1 - t) * th) * x + math.sin(t * th) * y) / math.sin(th) for x, y in zip(qa, qb)]
    n = math.sqrt(sum(x * x for x in q))
    return unquat([x / n for x in q])


def main():
    args = sys.argv[1:]
    interp = "--interp" in args
    mp = json.load(open(args[args.index("--map") + 1])) if "--map" in args else None
    gap = float(args[args.index("--max-gap") + 1]) if "--max-gap" in args else 4.0
    files = [a for i, a in enumerate(args) if not a.startswith("--") and (i == 0 or args[i - 1] not in ("--map", "--max-gap"))]
    series = [x for log in files[1:] for x in H.samples(log)]
    angles = []
    for line in open(files[0]):
        p = json.loads(line)["a"]
        if not p or not p.get("view"):
            continue
        for r, info in p["pictures"]:
            names = [mp[str(r)]] if mp is not None and str(r) in mp else ([] if mp is not None else [f"Head{r + 1}"])
            keys = [k for n in names for k in (n, f"Head{int(n[4:]):02d}", f"Head{int(n[4:])}")]
            held = [x for x in series if x["role"] == int(p["role"]) and x["entry"] == int(p["clip"]) and abs(x["frame"] - p["frame"]) <= gap
                    and any(k in x["heads"] and x["heads"][k]["turn"] for k in keys)]
            if not held:
                continue
            near = min(held, key=lambda x: abs(x["frame"] - p["frame"]))
            key = next(k for k in keys if k in near["heads"] and near["heads"][k]["turn"])
            ra, rb = H.rot(near["heads"][key]["turn"]), H.rot(info["matrix"])
            if interp:
                before = max((x for x in held if x["frame"] <= p["frame"]), key=lambda x: x["frame"], default=None)
                after = min((x for x in held if x["frame"] >= p["frame"]), key=lambda x: x["frame"], default=None)
                if not before or not after:
                    continue
                kb = next(k for k in keys if k in before["heads"] and before["heads"][k]["turn"])
                ka = next(k for k in keys if k in after["heads"] and after["heads"][k]["turn"])
                span = after["frame"] - before["frame"]
                ra = slerp(H.rot(before["heads"][kb]["turn"]), H.rot(after["heads"][ka]["turn"]), 0.0 if span == 0 else (p["frame"] - before["frame"]) / span)
            m = [[sum(ra[k][i] * rb[k][j] for k in range(3)) for j in range(3)] for i in range(3)]
            ang = math.degrees(math.acos(max(-1.0, min(1.0, (m[0][0] + m[1][1] + m[2][2] - 1) / 2))))
            ax = [m[2][1] - m[1][2], m[0][2] - m[2][0], m[1][0] - m[0][1]]
            n = math.sqrt(sum(a * a for a in ax)) or 1
            angles.append(ang)
            print(f"role {int(p['role'])} clip {int(p['clip'])} f{p['frame']:6.1f} record {r:2d} {key}: residual {ang:6.1f} deg about node-frame axis ({ax[0] / n:+.2f},{ax[1] / n:+.2f},{ax[2] / n:+.2f}) (ours at frame {near['frame']:.1f}{', interpolated' if interp else ''})")
    if angles:
        print(f"{len(angles)} pairs: residual mean {sum(angles) / len(angles):.2f}, max {max(angles):.2f} deg")
    else:
        print("0 pairs")


if __name__ == "__main__":
    main()
