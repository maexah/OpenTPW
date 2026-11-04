#!/usr/bin/env python3
"""faceturn.py <data dir> [-v] [--order zyx|xyz] [--edge point|corner1|offset] - FUN_0044a640's turn at rest against each face-anchored node's
own rest turn, every .md2 (Q1003; docs/exe/park.md, "Where a head on a morphing face stands").

For each lookup record with flag 0x40000: the kept turn k (three floats at the anchor less its word at +2), the face's frame F (rows n x e, n,
e: e the face's corner 0 toward the point the two lerps reach, normalised; n the file's normal entry and, beside it, FUN_00473a40's recompute),
R = Rz(k2) Ry(k1) Rx(k0) . F with the 4,096 table's sines, against the node's +0x10 rows, in the parent mesh's space (the engine works in the
world; the parents' matrices are turns, so the angle is the same). Prints a line per record with -v (else only those over 1 degree and the
jungle's Mumbo and Crazy Ape), then the counts. Also checks the layout: the word is zero exactly where 0x40000 is absent, and the turns are one
16-byte table ending at the first anchor, in the anchors' order, the fourth float zero. --order xyz and --edge corner1 / offset are the
controls (the wrong rules score far worse). Needs no display; ~10 s."""
import os, sys, struct, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import md2surf, wadcat, facenormals

TAU = 2 * math.pi
K = 4096 / TAU


def tab(a):
    return math.sin((round(a * K) & 4095) / 4096 * TAU)


def mul(a, b):
    return [[sum(a[i][k] * b[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


def kept(k, order="zyx"):
    turns = {"z": lambda c, s: [[c, s, 0], [-s, c, 0], [0, 0, 1]], "y": lambda c, s: [[c, 0, -s], [0, 1, 0], [s, 0, c]],
             "x": lambda c, s: [[1, 0, 0], [0, c, s], [0, -s, c]]}
    L = [[1, 0, 0], [0, 1, 0], [0, 0, 1]]
    for axis in order:
        a = k["xyz".index(axis)]
        L = mul(L, turns[axis](tab(a + math.pi / 2), tab(a)))
    return L


def norm(v):
    n = math.sqrt(sum(x * x for x in v))
    return [x / n for x in v] if n else [float("nan")] * 3


def cross(a, b):
    return [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]]


def angle(a, b):
    a, b = [norm(r) for r in a], [norm(r) for r in b]
    t = (sum(a[k][i] * b[k][i] for k in range(3) for i in range(3)) - 1) / 2
    return math.degrees(math.acos(max(-1.0, min(1.0, t))))


def models(data):
    for root, _, files in os.walk(data):
        for f in sorted(files):
            p = os.path.join(root, f)
            items = []
            if f.lower().endswith(".wad"):
                try:
                    items = wadcat.members(open(p, "rb").read())
                except Exception:
                    continue
            elif f.lower().endswith(".md2"):
                items = [(f, open(p, "rb").read())]
            for name, blob in items:
                if not name.lower().endswith(".md2") or blob[:4] != b"\x46\x5d\xd1\x1c":
                    continue
                try:
                    m = md2surf.Model(blob)
                except Exception:
                    continue
                if m.static and m.lookup_table:
                    yield os.path.relpath(p, data), name, m


def main():
    data = sys.argv[1]
    order = sys.argv[sys.argv.index("--order") + 1] if "--order" in sys.argv else "zyx"
    edge = sys.argv[sys.argv.index("--edge") + 1] if "--edge" in sys.argv else "point"
    rows, anchors, agree, entries, pad0, tables, with_turns = [], 0, 0, 0, 0, 0, 0
    for path, name, m in models(data):
        blob = m.blob
        ancs = []
        for r in range(m.lookup_count):
            flags, _, _, anc = struct.unpack_from("<IIII", blob, m.lookup_table + 20 * r)
            if anc:
                ancs.append((anc, flags, m.lookup_first + r))
        turns = []
        for anc, flags, node in ancs:
            anchors += 1
            face, back = struct.unpack_from("<HH", blob, anc)
            agree += (back != 0) == bool(flags & 0x40000)
            if back:
                turns.append(anc - back)
                entries += 1
                pad0 += struct.unpack_from("<f", blob, anc - back + 12)[0] == 0.0
            if not flags & 0x40000:
                continue
            rec = m.record(node)
            parent = m._record_index(struct.unpack_from("<I", blob, rec + 4)[0])
            if parent is None or parent >= m.mesh_count:
                rows.append((path, name, m.name(node), flags, None, None))
                continue
            mesh = m.meshes[parent]
            u, v, off = struct.unpack_from("<3f", blob, anc + 4)
            k = struct.unpack_from("<3f", blob, anc - back)
            word, a, b, c = mesh.faces[face]
            raw = lambda i: [mesh.raw[mesh.order[i]][j] - mesh.chain[j] for j in range(3)]
            c0, c1, c2 = raw(a), raw(b), raw(c)
            pt = [c1[j] * u + c0[j] * (1 - u) for j in range(3)]
            pt = [c2[j] * v + pt[j] * (1 - v) for j in range(3)]
            nf = list(facenormals.entry(m, mesh, word & 0x7fff))
            nr = list(facenormals.recomputed(c0, c1, c2))
            own = [list(struct.unpack_from("<3f", blob, rec + 0x10 + 16 * i)) for i in range(3)]
            res = []
            for n in (nf, nr):
                to = {"point": pt, "corner1": c1, "offset": [pt[j] + off * norm(n)[j] for j in range(3)]}[edge]
                e = norm([to[j] - c0[j] for j in range(3)])
                if math.isnan(e[0]):
                    e = [n[2], n[0], n[1]]
                res.append(angle(mul(kept(k, order), [cross(n, e), n, e]), own))
            rows.append((path, name, m.name(node), flags, res[0], res[1]))
        if turns:
            with_turns += 1
            first = min(a for a, _, _ in ancs)
            in_order = [a - struct.unpack_from("<H", blob, a + 2)[0] for a, _, _ in sorted(ancs) if struct.unpack_from("<H", blob, a + 2)[0]]
            ts = sorted(turns)
            tables += ts[-1] + 16 == first and all(q - p == 16 for p, q in zip(ts, ts[1:])) and in_order == ts
    ok = [r for r in rows if r[4] is not None]
    for r in rows:
        if "-v" in sys.argv or r[4] is None or r[4] > 1.0 or ("jungle" in r[0] and r[1].lower() in ("mumbo.md2", "monkey.md2")):
            print(r[0], r[1], r[2], "%#x" % r[3], "parent not a mesh" if r[4] is None else "file normal %.3f recomputed %.3f deg" % (r[4], r[5]))
    print(f"order {order}, edge {edge}: records with 0x40000 {len(rows)}, measured {len(ok)}; within 0.1 deg {sum(r[4] <= 0.1 for r in ok)}, "
          f"within 1 deg {sum(r[4] <= 1.0 for r in ok)}, worst {max(r[4] for r in ok):.3f} (recomputed normal: within 1 deg {sum(r[5] <= 1.0 for r in ok)})")
    print(f"layout: anchors {anchors}, word zero exactly where 0x40000 is absent {agree}; turn entries {entries}, fourth float zero {pad0}; "
          f"models with turns {with_turns}, one 16-byte table ending at the first anchor in the anchors' order {tables}")


if __name__ == "__main__":
    main()
