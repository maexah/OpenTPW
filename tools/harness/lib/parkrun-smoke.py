#!/usr/bin/env python3
"""parkrun-smoke.py <out> <bin> [off]: the shared scaffold's own check. Enters Lost Kingdom, saves under pause,
loads the file back from the Load Park list, and compares the three ticks. With `off`, on the private display."""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
OUT = sys.argv[1]; BIN = sys.argv[2]; NAME = "SMOKE"; OFFSCREEN = len(sys.argv) > 3 and sys.argv[3] == "off"
exec(open(os.path.join(paths.LIB, "parkrun.py")).read())
try:
    enter(); ask("pause", "pause"); t = tick()
    predict(f"the file's mGameTick is the paused clock's, {t}, and the load starts on it")
    park, said_ = save()
    ask("resume", "resum"); time.sleep(1.0)
    read, start, mark = load()
    said(park.get("tick") == t and start == t and read.endswith(f"users/1ref/jungle/{NAME}.TPWS"), f"file {park.get('tick')}, load {start}, read {read}")
    shot("after-the-load", 0.5); reply("stats", 0.8)
finally:
    done()
