#!/usr/bin/env python3
"""pathscene.py - a scene file of the stock Lost Kingdom park with more developed cells: entered, paused on the first
tick at or past TICK, `path X Y` given for the first N cells of the W x H block at (X0,Y0), row by row, each with a
0.15 s wait (stockscene.py's 2.5 s a command is twelve minutes for 289), then saved and copied to <dest.TPWS>.
  pathscene.py <outdir> <bin> <dest.TPWS> <TICK> <N> [X0 Y0 W [H]] [-- command ...]     (default 60 15 19 20)
Each command after `--` is given once the cells are laid, with stockscene.py's 2.5 s wait (`fire 28`, the guard).
The stock park holds 126 developed cells (parkfile.py -d), so the file holds 126 + N; the block at (60,15) is bare
grass inside the park 19 x 20 (parkfile.bare_blocks finds 20 x 20, but its column 79 holds (79,31) and (79,32),
cells of mType 0 that are a track's: `path` refuses them). A `path` reply can come after its window: the script's
tally may read a cell or two short where the file holds them all (Q411), so count the file with parkfile.py -d.
N may be 0: the commands alone (Q413: `760 0 -- "fire 28"`, the guard dismissed, paused on 765).
Off-screen. review-codex/venv/bin/python3. Made for Q411 (row 10) and Q412 (row 13, with `-- fire 28`)."""
import os, sys, shutil
OUT = sys.argv[1]; BIN = sys.argv[2]; DEST = sys.argv[3]; AT = int(sys.argv[4]); N = int(sys.argv[5])
ARGS = sys.argv[6:sys.argv.index("--")] if "--" in sys.argv else sys.argv[6:]
CMDS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
X0, Y0, W = (int(a) for a in ARGS[:3]) if len(ARGS) >= 3 else (60, 15, 19)
HT = int(ARGS[3]) if len(ARGS) >= 4 else (W if len(ARGS) >= 3 else 20)
NAME = "scene-" + os.path.splitext(os.path.basename(DEST))[0]; OFFSCREEN = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
exec(open(paths.LIB + "/parkrun.py").read()); exec(open(paths.LIB + "/parkscene.py").read())
CELLS = [(X0 + i, Y0 + j) for j in range(HT) for i in range(W)][:N]
assert len(CELLS) == N, f"a {W} x {HT} block holds {W * HT}, not {N}"
try:
    enter()
    while tick() < AT: time.sleep(0.1)
    reply("pause", 0.4, 0); S = tick(); print("  paused on", S)
    laid = 0; late = []
    for x_, y_ in CELLS:
        g_ = reply(f"path {x_} {y_}", 0.15, 0)
        if any("laid at" in l for l in g_): laid += 1
        else: late.append((x_, y_, g_[-1:] if g_ else None))
    time.sleep(1.0); print("  laid", laid, "no reply in the window", len(late), late[:5])
    for c_ in CMDS:
        got_ = reply(c_, 2.5, 0); print("  ", c_, "->", got_[-1:])
    reply("money", 1.0, 2)
    park_, _l = save(); shutil.copyfile(os.path.join(OUT, NAME + ".TPWS"), DEST)
    predict(f"the pause is on a tick from {AT} to {AT + 7}")
    said(AT <= S <= AT + 7, f"paused on {S}")
finally:
    done()
