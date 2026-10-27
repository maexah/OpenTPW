#!/usr/bin/env python3
"""stockscene.py - a scene file made from the stock Lost Kingdom park: entered, paused on the first tick at or past
TICK, each console command given in turn (`sell 13`, `buy 1101 41 22 0`), the objects listed and the park saved.
  stockscene.py <outdir> <bin> <dest.TPWS> <TICK> [command ...]
Off-screen (a census run). The pause's tick and each command's last reply are printed; the saved file is copied to
<dest.TPWS>. Stamps and flags are then written into copies with parkfile.thing_set. review-codex/venv/bin/python3."""
import os, sys, shutil
OUT = sys.argv[1]; BIN = sys.argv[2]; DEST = sys.argv[3]; AT = int(sys.argv[4]); CMDS = sys.argv[5:]
NAME = "scene-" + os.path.splitext(os.path.basename(DEST))[0]; OFFSCREEN = True
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
exec(open(paths.LIB + "/parkrun.py").read()); exec(open(paths.LIB + "/parkscene.py").read())
try:
    enter()
    while tick() < AT: time.sleep(0.1)
    reply("pause", 0.4, 0); S = tick(); print("  paused on", S)
    for c_ in CMDS:
        got_ = reply(c_, 2.5, 0); print("  ", c_, "->", got_[-1:])
    for o_ in reply("objects", 1.5, 0):
        if " type 0 " in o_ or " type 1 " in o_ or " type 2 " in o_: print("  ", o_.strip()[:170])
    park_, _l = save(); shutil.copyfile(os.path.join(OUT, NAME + ".TPWS"), DEST)
    predict(f"the pause is on a tick from {AT} to {AT + 7}")
    said(AT <= S <= AT + 7, f"paused on {S}")
finally:
    done()
