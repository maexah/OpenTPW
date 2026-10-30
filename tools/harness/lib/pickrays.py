#!/usr/bin/env python3
"""pickrays.py <out> <bin> <orig dir> [tag]: the original's own pick rays handed to this build's picker (Q824).

<orig dir> holds what a run of the original wrote for each camera i (1, 2, ...): `<tag>-run.out`, whose lines
`== camera i <setcam args> eye X H Z` give the eye read from memory in full ([0x00790aa0], [0x00790aa4], [0x00790aa8]),
and `<tag>-scan<i>.log`, `original/hoverscan.py PID 99 any pick exact grid ...`'s lines (the pointer's pixel, the cell
[0x007b05cc] and P [0x007a1c70] to three places, `met 0` where the object march met nothing, so P is the land's).
For each such pixel it sends `pickray` (the eye through P, in this game's axes: x, z, height) and compares the `exact`
point and the `cell` of the answer with the original's. Where the two pickers hold one land model the answer is P: the
hit of the line through P is P. Prints one line a pixel and the counts within 0.01 and in the same cell. `tag` defaults to a.
SHOTS="2:640,240;1:1000,360" (camera: the original's 1024 x 768 pixel) also puts this game on that camera by the original's numbers
(`bothcam.py`), moves the pointer to the mapped pixel, and keeps a frame `cam<i>-<x>-<y>.png` with the `pick` line and the pointer's."""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
OUT = sys.argv[1]; BIN = sys.argv[2]; NAME = "pickrays"; ORIG = os.path.expanduser(sys.argv[3]).rstrip("/") + "/"
TAG = sys.argv[4] if len(sys.argv) > 4 else "a"
exec(open(os.path.join(paths.LIB, "parkrun.py")).read())
exec(open(os.path.join(paths.LIB, "parkscene.py")).read())
import bothcam
eyes = {}
for l_ in open(ORIG + TAG + "-run.out"):
    m_ = re.match(r"== camera (\d+) .* eye (-?[\d.]+) (-?[\d.]+) (-?[\d.]+)", l_)
    if m_: eyes[int(m_.group(1))] = (float(m_.group(2)), float(m_.group(4)), float(m_.group(3)))
rays = []
for cam, eye in eyes.items():
    for l_ in open(ORIG + f"{TAG}-scan{cam}.log"):
        m_ = re.match(r"\((\d+),(\d+)\) .* cell \((\d+), (\d+)\) .* P \((-?[\d.]+),(-?[\d.]+),(-?[\d.]+)\) met 0", l_)
        if m_: rays.append((cam, eye, (int(m_.group(1)), int(m_.group(2))), (int(m_.group(3)), int(m_.group(4))),
                            (float(m_.group(5)), float(m_.group(7)), float(m_.group(6)))))
try:
    enter()
    while tick() < 200: time.sleep(1.0)
    same = cells = 0; worst = 0.0
    for cam, eye, px, ocell, p in rays:
        g = [l for l in reply(f"pickray {eye[0]} {eye[1]} {eye[2]} {p[0]} {p[1]} {p[2]}", 0.3, 0) if "pick:" in l]
        w = re.search(r"exact \((-?[\d.]+),(-?[\d.]+),(-?[\d.]+)\)", g[0]) if g else None
        c = re.search(r"cell \((\d+),(\d+)\)", g[0]) if g else None
        ours = tuple(float(v) for v in w.groups()) if w else None
        gap = max(abs(a - b) for a, b in zip(ours, p)) if ours else 99.0
        worst = max(worst, gap); same += gap <= 0.01
        ok_cell = c is not None and (int(c.group(1)), int(c.group(2))) == ocell; cells += ok_cell
        print(f"  cam {cam} {px} orig P {p} ours {ours} max diff {gap:.4f} {'SAME' if gap <= 0.01 else 'DIFF'} cell {ocell} {'same' if ok_cell else 'DIFFERENT'}")
    print(f"  within 0.01: {same}/{len(rays)}; worst {worst:.4f}; cell same {cells}/{len(rays)}")
    cams = {int(m_.group(1)): (float(m_.group(2)), float(m_.group(3)), float(m_.group(4)))
            for m_ in (re.match(r"== camera (\d+) (\S+),(\S+) (\S+) ", l_) for l_ in open(ORIG + TAG + "-run.out")) if m_}
    for item in filter(None, os.environ.get("SHOTS", "").split(";")):
        cam, pix = item.split(":"); ox, oy = (int(v) for v in pix.split(","))
        x_, z_, rot = cams[int(cam)]
        bothcam.ours(reply, x_, z_, 110, rot); reply("step 2", 0.5, 0); time.sleep(1.0)
        q = (round(640 + (ox - 512) * 720 / 768), round(360 + (oy - 384) * 720 / 768))
        move(*q, f"cam{cam}-{ox}-{oy}"); time.sleep(1.0)
        print(f"  shot cam {cam} original pixel ({ox},{oy}) this game's {q}: {pick()}; {pointer()}"); shot(f"cam{cam}-{ox}-{oy}", 0.3)
finally:
    done()
