#!/usr/bin/env python3
"""headframe.py LABEL pauses.jsonl run.log [--map FILE] [--samples N] - which of the 56 head pictures a ride head shows, by the
original's matrix and by this game's rest matrix (Q940).

pauses.jsonl is `original/headpause.py`'s since Q940 (each head's `pictures`: its stored matrix at record+4, the picture its sprite
shows at +0xb8, and the `view` matrix at *[0x790a98]); run.log is the log of a `headseries.py` run of this game (its `rides` census
lines carry each head's `matrix [...]`: the node's stored REST matrix, 12 numbers, rotation rows then translation). --map FILE is
headcompare's record -> head name ({"3": "Head03"}); without it record r is Head(r+1) (Mumbo's).

The picture is `FUN_0044b510`'s (docs/exe/ride-operation.md, "Which picture a head shows"): the view's place taken into the node's frame
(row vectors, the view first and then the node's inverse), normalised, then the first of 56 directions (7 rows tilted 30 degrees about
x from straight up, 8 columns turned -45 degrees about y; the sin table of 4096) with the largest dot product above nought, rows 0 and 6 column 0.
Prints, per pause and head: the sprite's picture, the picture this script works out from the original's own matrix (the check on the
reading: they must agree), the picture from this game's rest matrix on the same view, the angle between the two rotations, and a count over
uniformly spread camera directions of how often the two matrices pick different pictures."""
import json, math, re, sys, random

TAU = 2 * math.pi
K = 4096 / TAU


def tab(angle):
    """The engine's table: sin( i / 4096 * 2pi ) at the index the float angle rounds to (FUN_004708d0, 0x007b0968)."""
    return math.sin((round(angle * K) & 4095) / 4096 * TAU)


A_COL = -math.pi / 4          # 0x006fe2bc
A_ROW = math.pi / 6           # 0x006fe2c4


def candidates():
    """The 56 directions in the order the engine walks them: (x, y, z) per frame c + 8 r."""
    sc, cc = tab( A_COL ), tab( -A_COL )          # sin( -45 ), cos( -45 ) = tab( +45 )
    sr, cr = tab( A_ROW ), tab( 2 * math.pi / 3 )     # 0x006fe2c8 = 2pi/3, whose sine is cos( 30 )
    x, y, z = 0.0, 1.0, 0.0
    out = []
    for _ in range(7):
        for _ in range(8):
            out.append((x, y, z))
            x, z = cc * x + sc * z, cc * z - x * sc
        y, z = cr * y - sr * z, y * sr + cr * z
    return out


CAND = candidates()


def picture(d):
    """The frame the engine picks for a direction d = (x, y, z) in the node's frame."""
    n = math.sqrt(d[0] ** 2 + d[1] ** 2 + d[2] ** 2)
    if n == 0 or math.isnan(n):
        return 0
    x, y, z = d[0] / n, d[1] / n, d[2] / n
    best, pick = 0.0, (0, 0)
    for i, (cx, cy, cz) in enumerate(CAND):
        dot = z * cz + x * cx + y * cy
        if best < dot:
            best, pick = dot, divmod(i, 8)[::-1]
    r, c = pick[1], pick[0]
    if r in (0, 6):
        c = 0
    return c + 8 * r


def rot(m):
    return [m[0:3], m[4:7], m[8:11]] if len(m) == 16 else [m[0:3], m[3:6], m[6:9]]


def trans(m):
    return m[12:15] if len(m) == 16 else m[9:12]


def vecmat(v, r):
    return [sum(v[i] * r[i][j] for i in range(3)) for j in range(3)]


def inv_rot(r):
    """Inverse of an orthonormal-ish 3x3 by transpose scaled (the matrices carry no scale at rest; checked by the determinant column)."""
    return [[r[j][i] for j in range(3)] for i in range(3)]


def into_node(cam, m):
    """The direction from the node to the camera place `cam`, in the node's frame (row vectors)."""
    t = trans(m)
    return vecmat([cam[i] - t[i] for i in range(3)], inv_rot(rot(m)))


def angle_between(a, b):
    """The angle of the rotation taking rotation a onto rotation b."""
    ra, rb = rot(a), rot(b)
    m = [[sum(ra[k][i] * rb[k][j] for k in range(3)) for j in range(3)] for i in range(3)]
    c = max(-1.0, min(1.0, (m[0][0] + m[1][1] + m[2][2] - 1) / 2))
    return math.degrees(math.acos(c))


def census(log):
    """The rest matrices of this game's heads, by name, from a `rides` census in a log: {name: 12 floats}."""
    out = {}
    for m in re.finditer(r"'(Head\w+)' \w+ \([^)]*\) model [^\[]*?matrix \[([^\]]*)\]", open(log, errors="replace").read()):
        out.setdefault(m.group(1), [float(v) for v in m.group(2).split()])
    return out


def samples(log):
    """Every `rides` census line of a headseries run: [{role, frame, heads: {name: {rest, turn, at}}}], where `at` is the drawn place in the model's axes (y up)."""
    out = []
    pat = re.compile(r"'(Head\w+)' \w+ \([^)]*\) model [^\[]*?drawn \w+ \(([-\d.]+),([-\d.]+),([-\d.]+)\) matrix \[([^\]]*)\](?: drawnturn \[([^\]]*)\])?")
    for line in open(log, errors="replace"):
        c = re.search(r"channels \[0:role (\d+) entry \d+ frame ([\d.]+)/", line)
        if not c or "matrix [" not in line:
            continue
        heads = {}
        for m in pat.finditer(line):
            x, y, z = float(m.group(2)), float(m.group(3)), float(m.group(4))
            heads[m.group(1)] = {"rest": [float(v) for v in m.group(5).split()], "turn": [float(v) for v in m.group(6).split()] if m.group(6) else None, "at": [x, z, y]}
        out.append({"role": int(c.group(1)), "frame": float(c.group(2)), "heads": heads})
    return out


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    label, pauses, log = args[:3]
    mp = json.load(open(sys.argv[sys.argv.index("--map") + 1])) if "--map" in sys.argv else None
    n_dirs = int(sys.argv[sys.argv.index("--samples") + 1]) if "--samples" in sys.argv else 2000
    rest = census(log)
    series = samples(log)
    rng = random.Random(940)
    dirs = []
    while len(dirs) < n_dirs:
        v = [rng.gauss(0, 1) for _ in range(3)]
        n = math.sqrt(sum(a * a for a in v))
        dirs.append([a / n for a in v])
    rows = []
    for line in open(pauses):
        p = json.loads(line)
        a = p["a"]
        if not a or not a.get("view"):
            continue
        view = a["view"]
        cam = trans(view)
        for r, info in a["pictures"]:
            name = (mp or {}).get(str(r)) or (None if mp else f"Head{r + 1:d}")
            if name is None:
                continue
            ours = rest.get(name) or rest.get(f"Head{int(name[4:]):02d}") or rest.get(f"Head{int(name[4:])}")
            theirs = info["matrix"]
            if not ours or not theirs:
                continue
            seen_orig = picture(into_node(cam, theirs))
            seen_ours = picture(into_node(cam, ours))
            # This game at the same channel frame (nearest sample of the same role in any loop of the clip), its drawn turn at its drawn place.
            key = next((k for k in (name, f"Head{int(name[4:]):02d}", f"Head{int(name[4:])}") if any(k in x["heads"] for x in series)), name)   # the census spells Head1 and Head02
            near = min((x for x in series if x["role"] == int(a["role"]) and key in x["heads"]), key=lambda x: abs(x["frame"] - a["frame"]), default=None)
            seen_turn, turn_angle, gap = None, None, None
            if near and near["heads"][key]["turn"]:
                h = near["heads"][key]
                gap = round(abs(near["frame"] - a["frame"]), 1)
                m12 = h["turn"][0:3] + [0] + h["turn"][3:6] + [0] + h["turn"][6:9] + [0] + h["at"] + [1]
                seen_turn = picture(into_node(cam, m12))
                turn_angle = round(angle_between(m12, theirs), 1)
            # Over camera directions spread evenly: the node's frame by each matrix, the same world direction.
            differ = sum(1 for d in dirs if picture(vecmat(d, inv_rot(rot(theirs)))) != picture(vecmat(d, inv_rot(rot(ours)))))
            rows.append((a["role"], round(a["frame"], 1), r, name, info["picture"], seen_orig, seen_ours,
                         round(angle_between(ours, theirs), 1), round(100 * differ / n_dirs, 1), seen_turn, turn_angle, gap))
    print(f"{label}: role frame rec head | sprite orig-matrix ours-rest | angle(deg) | % of directions that differ")
    for row in rows:
        print("  role %d frame %6.1f rec %2d %-7s | sprite %s  orig-matrix %2d  ours-rest %2d | %6.1f deg | %5.1f%% | ours-turn %s, %s deg (frame gap %s)" % row)
    if rows:
        reading = sum(1 for r in rows if r[4] == r[5])
        same = sum(1 for r in rows if r[5] == r[6])
        print(f"{label}: {len(rows)} head samples; the reading of the original's own matrix matches its sprite in {reading}; "
              f"ours' rest matrix picks the original's picture in {same}; mean angle {sum(r[7] for r in rows) / len(rows):.1f}, "
              f"max {max(r[7] for r in rows):.1f}; mean {sum(r[8] for r in rows) / len(rows):.1f}% of directions differ")
        turned = [r for r in rows if r[9] is not None]
        if turned:
            print(f"{label}: with this game's drawn turn ({len(turned)} head samples): picks the original's picture in {sum(1 for r in turned if r[5] == r[9])}"
                  f" (rest: {sum(1 for r in turned if r[5] == r[6])}); mean angle to the original's {sum(r[10] for r in turned) / len(turned):.1f}, max {max(r[10] for r in turned):.1f}")


if __name__ == "__main__":
    main()
