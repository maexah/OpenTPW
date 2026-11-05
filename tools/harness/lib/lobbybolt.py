#!/usr/bin/env python3
"""lobbybolt.py <outdir> <bin> (Q1037): ours' lobby bolt on the DESK. review-codex/venv/bin/python -u. Island 0, attract off, settle, freeze, pause, a no-strike
sweep, `strike`, a frame and the census every 1/20 s of the bolt's age (stepsweep). Frames in <outdir> (nostrike-NN, strike-NN); result.json."""
import os, sys, re, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import paths
OUT = sys.argv[1]; BIN = sys.argv[2]; NAME = "q1037-ours"
exec(open(paths.LIB + "/parkrun.py").read())
exec(open(paths.LIB + "/framesweep.py").read())
N = 20
try:
    wait_for("Loaded the lobby", 150); attach(); time.sleep(3.0)
    reply("attract off", 0.3, 1); reply("island 0", 0.3, 1); reply("settle", 0.3, 0); reply("freeze", 0.3, 0); time.sleep(2.0)
    pre = ask("near", "near flyers"); print("  before pausing:", pre)
    reply("pause", 0.3, 1); time.sleep(0.8)
    pre = ask("near", "near flyers")
    if "axisDist=-" not in pre:
        print("  THROWN AWAY: a natural strike came before the pause (", pre, ")"); raise SystemExit(3)
    base = grab(); base.save(os.path.join(OUT, "base.png"))
    quiet = stepsweep("near", "near flyers", N, "nostrike")
    base2 = grab(); base2.save(os.path.join(OUT, "base2.png"))
    s = stepsweep("near", "near flyers", N, "strike", "strike")
    json.dump({"size": base.size, "census": [(a, g) for a, g, _ in s], "quiet": [(a, g) for a, g, _ in quiet]}, open(os.path.join(OUT, "result.json"), "w"))
finally:
    done()
