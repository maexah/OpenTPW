#!/usr/bin/env python3
"""headresidual.py PAUSES.jsonl run.log - how far this game's drawn turn of each Mumbo head is from the original's stored matrix (Q947).

pauses.jsonl is `original/headpause.py`'s (each head's `pictures` matrix at record+4); run.log is a `headseries.py` run of this game whose `rides`
lines carry each head's `drawnturn [...]` (`LobbyModel.TryGetDrawnNodeTurn`). Per pause and head (record r is Head(r+1); the census spells the
first `Head1` and the rest `Head02`..): the angle of the rotation taking ours' turn onto the original's, and its axis in the node's frame
(an axis with y near nought is a tilt of the node's up). Ours is the nearest frame of the same role. Needs no display."""
import sys, json, math
sys.path.insert(0, __import__("os").path.dirname(__import__("os").path.abspath(__file__)))
import headframe as H


def main():
    series = H.samples(sys.argv[2])
    for line in open(sys.argv[1]):
        p = json.loads(line)["a"]
        if not p or not p.get("view"):
            continue
        for r, info in p["pictures"]:
            name = f"Head{r + 1}"
            key = next((k for k in (name, f"Head{r + 1:02d}") if any(k in x["heads"] for x in series)), None)
            near = min((x for x in series if key and x["role"] == int(p["role"]) and key in x["heads"]), key=lambda x: abs(x["frame"] - p["frame"]), default=None)
            if not near or not near["heads"][key]["turn"]:
                continue
            ra, rb = H.rot(near["heads"][key]["turn"]), H.rot(info["matrix"])
            m = [[sum(ra[k][i] * rb[k][j] for k in range(3)) for j in range(3)] for i in range(3)]
            ang = math.degrees(math.acos(max(-1.0, min(1.0, (m[0][0] + m[1][1] + m[2][2] - 1) / 2))))
            ax = [m[2][1] - m[1][2], m[0][2] - m[2][0], m[1][0] - m[0][1]]
            n = math.sqrt(sum(a * a for a in ax)) or 1
            print(f"role {int(p['role'])} f{p['frame']:6.1f} {name}: residual {ang:6.1f} deg about node-frame axis ({ax[0] / n:+.2f},{ax[1] / n:+.2f},{ax[2] / n:+.2f})")


if __name__ == "__main__":
    main()
