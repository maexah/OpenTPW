#!/usr/bin/env python3
"""effectstep.py <outdir> <bin> <park file> <effect> [ticks]: where an effect's emitters stand, tick by tick, after a load (Q928).

Copies <park file> (a Full Simulation save) in as the private Full Simulation player's newest, Loads it paused (Load row 1), then `step 1` and the console's `particles`
rows of <effect> each tick for [ticks] (60): <outdir>/steps.txt holds one line a row, `step N slot S life L particles P raw (x,y,z)`, the emitter's place as it holds it, 64 to a
unit. The log's `started effect <effect>` lines (the raw place over 1024 they were started at) are in <outdir>/run.log and <outdir>/started.txt. The same file given to a binary with
a start rule and to one without shows the rule (the control is a worktree of `main` built in its own folder); the original's side is `docs/TOOLING.md`, "Where an effect is started,
in both games" (`brk.sh` on `Particles_Spawn`, `RAW=1 emitters.py`). Run it with the venv's python; `<bin>` is the build's folder."""
import os, sys, shutil, re as _re
OUT = sys.argv[1]; BIN = sys.argv[2]; SRC = os.path.expanduser(sys.argv[3]); EFFECT = int(sys.argv[4]); TICKS = int(sys.argv[5]) if len(sys.argv) > 5 else 60; NAME = "effectstep"
L = os.path.dirname(os.path.abspath(__file__)) + "/"
sys.path.insert(0, L)
exec(open(L + "parkrun.py").read())
exec(open(L + "fsplayer.py").read())
PROFILE = os.environ.get("OPENTPW_FULLSIM_PLAYER", os.path.expanduser("~/repos/game/saves-fullsim/save/users/1FullSimCheated")) + "/gms.dat"
try:
    fs_profile(PROFILE)
    enter(); reply("pause", 0.3, 0)
    shutil.copy(SRC, WROTE); os.chmod(WROTE, 0o644); os.utime(WROTE, None)
    print("load:", load(1))
    steps = []
    for t in range(TICKS):
        reply("step 1", 0.25, 0)
        for row in reply("particles", 1.2, 0, 400):
            m = _re.search(rf'slot (\d+) effect {EFFECT} .*? life (-?\d+) particles (\d+).*raw \((-?\d+),(-?\d+),(-?\d+)\)', row)
            if m:
                steps.append(f"step {t} slot {m.group(1)} life {m.group(2)} particles {m.group(3)} raw ({m.group(4)},{m.group(5)},{m.group(6)})")
    open(os.path.join(OUT, "steps.txt"), "w").write("\n".join(steps) + "\n")
    started = _re.findall(rf'[^\n]*started effect {EFFECT} [^\n]*', logtext())
    open(os.path.join(OUT, "started.txt"), "w").write("\n".join(started) + "\n")
    print(f"STEPS {len(steps)} rows of effect {EFFECT}; {len(started)} starts logged")
    for place in sorted(set(s.split("raw ")[1] for s in steps)): print("  raw", place)
finally:
    done()
    fs_off()
