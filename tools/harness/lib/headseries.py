#!/usr/bin/env python3
"""headseries.py <outdir> <bin> [ITEM [NAME]] [--photo ROLE:FRAME,...] [--load FILE] [--save-role R] - a ride's riders' heads in THIS game across its clips (Q848).
The stock park, paused, ITEM bought at (41,22) (default 1109, Mumbo; NAME is the word its `rides` line carries, default Mumbo), its queue
joined to the path at (43,21), five guests sent to it; once `heads 5/N` (five riders, N the ride's capacity) and running (or after 120 s), the park is paused and saved (<outdir>/Q848.TPWS,
which the original loads with original/loadfile.sh) and then, still paused:
    series (default)   `step STEP` and `rides` COUNT times (env Q848_STEP, Q848_COUNT; 3 and 450): series.json holds, a sample each, the ride's first
                       channel (role, clip, frame, length) and each head's name, kind, drawn place (x, depth, height), rest place and node (the census's leading number)
    --save-role R      (Q917) a bought ride's park: polls until its first channel is in role R at frames 5 to 30, then pauses (about 0.8 s, some 23 frames, later) and
                       saves; the role is read again after the pause and the run stops if the channel has left R. Not with --load (nothing is saved then). The riders
                       are on in an OnAMesh role in the file the original loads (the original draws its own riders' heads, so it does not hold the same ones: DOES-NOT)
    --load FILE        (Q916) start from a park file the original loaded, not a bought ride: nothing is bought or saved; the file is loaded from the Load Park list
    --photo T,...      on the desktop (not off-screen): a shot of the window at each role:frame (within 1.6 frames) named role<R>-frame<F>.png
Each head is held on a script node (the census's leading number: Mumbo's are 1-5, the Crazy Ape's sparse; the original's record numbers are the lookup table's, Q916); original/headpause.py reads the original's the same way and
lib/headcompare.py sets the two against each other. review-codex/venv/bin/python -u; BIN the build's folder."""
import os, sys, json
OUT = sys.argv[1]; BIN = sys.argv[2]; NAME = "Q848"
LOAD = os.path.abspath(sys.argv[sys.argv.index("--load") + 1]) if "--load" in sys.argv else None
SAVE_ROLE = sys.argv[sys.argv.index("--save-role") + 1] if "--save-role" in sys.argv else None
if LOAD and SAVE_ROLE: print("  note: --save-role is ignored with --load (nothing is saved)")
pos = [a for a in sys.argv[3:] if not a.startswith("--") and ":" not in a and os.path.abspath(a) != LOAD and a != SAVE_ROLE]
ITEM = pos[0] if pos else "1109"; WORD = pos[1] if len(pos) > 1 else "Mumbo"
PHOTO = [tuple(float(x) for x in t.split(":")) for t in sys.argv[sys.argv.index("--photo") + 1].split(",")] if "--photo" in sys.argv else None
OFFSCREEN = PHOTO is None
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
exec(open(paths.LIB + "/parkrun.py").read())
STEP = int(os.environ.get("Q848_STEP", "3")); COUNT = int(os.environ.get("Q848_COUNT", "450"))
def ride():
    return [g for g in reply("rides", 0.7, 0) if WORD in g]
def chan(line):
    m = re.search(r"channels \[0:role (\d+) entry (\d+) frame ([\d.]+)/([\d.]+)", line)
    return m.groups() if m else None
def heads(line):
    return [(h[2], h[10], [float(v) for v in h[11:14]], [float(v) for v in h[4:7]], int(h[0])) for h in re.findall(
        r"(\d+):(\d+) '(Head\w+)' (\w+) \(([-\d.]+),([-\d.]+),([-\d.]+)\) model \(([-\d.]+),([-\d.]+),([-\d.]+)\) drawn (\w+) \(([-\d.]+),([-\d.]+),([-\d.]+)\)", line)]
try:
    enter(); reply("pause", 0.5)
    if LOAD:   # the series starts from a park file (the one the original loaded) instead of a bought ride: Q916
        shutil.copy(LOAD, WROTE)
        read, _, _ = load(2 if OFFSCREEN else 1)   # off-screen the list runs oldest first, the autosave then this file; the row is the file's place in the list: off-screen the private folder held an autosave, row 2; the desktop's held none on 2026-11-04, row 1; the assert below stops a wrong row
        assert read.endswith(NAME + ".TPWS"), f"loaded {read}, not the file asked for"
        reply("camera 430 237 90 0", 0.5)
    else:
        got = " ".join(reply(f"buy {ITEM} 41 22 0", 2.5, 9)); print("  buy:", got[:200])
        m_ = re.search(r"built as thing (\d+)", got); tid = int(m_.group(1)) if m_ else -1
        ask(f"tool queue {tid}", "tool:"); [ask(f"worldclick {c}", "world click") for c in ("42 21", "43 21")]; ask("tool off", "tool:")
        reply("resume", 0.3)
        for i in range(5):
            l = " ".join(reply("admit 43 21", 1.0, 0)); m_ = re.search(r"admit: guest (\d+)", l)
            if m_: reply(f"send {m_.group(1)} {tid}", 1.0, 0)
        reply("camera 430 237 90 0", 0.5)
        ok = False
        for i in range(120):
            time.sleep(1.0); l = ride()
            if l and re.search(r"heads 5/\d+", l[0]) and "running True" in l[0]: ok = True; print("  aboard after", i + 1, "s"); break
        print("  aboard:", ok)
        for i in range(400 if SAVE_ROLE else 0):   # Q917: running on until the first channel is in role SAVE_ROLE, then pause and save
            l = ride(); c = chan(l[0]) if l else None
            if c and c[0] == SAVE_ROLE and 5 <= float(c[2]) <= 30: print("  role", SAVE_ROLE, "frame", c[2], "after", i, "polls"); break
            time.sleep(0.3)
        else:
            if SAVE_ROLE: print("  never reached role", SAVE_ROLE); sys.exit(1)
        reply("pause", 0.5); send("step 2"); time.sleep(1.0)
        if SAVE_ROLE:   # the pause lands some 23 frames after the poll (Q917): say the channel the file is saved in
            l = ride(); c = chan(l[0]) if l else None
            print("  saved in role", c[0] if c else None, "entry", c[1] if c else None, "frame", c[2] if c else None)
            if not c or c[0] != SAVE_ROLE: print("  left role", SAVE_ROLE, "before the save"); sys.exit(1)
        p, _ = save(); print("  saved:", WROTE, "things", len(p.things))
    if PHOTO is None:
        series = []
        for i in range(COUNT):
            l = ride()
            if l: series.append({"i": i, "channel": chan(l[0]), "heads": heads(l[0])})
            send(f"step {STEP}"); time.sleep(0.25)
        json.dump(series, open(OUT + "/series.json", "w")); print("  series", len(series))
    else:
        for role, frame in PHOTO:
            for i in range(700):
                l = ride(); c = chan(l[0]) if l else None
                if c and int(c[0]) == int(role) and abs(float(c[2]) - frame) <= 1.6:
                    print(f"  at role {c[0]} frame {c[2]}:", l[0][l[0].find('heads'):][:700]); shot(f"role{int(role)}-frame{int(frame)}", 0.8); break
                send("step 3"); time.sleep(0.25)
            else: print("  never reached", role, frame)
finally:
    try: reply("resume", 0.3)
    except Exception: pass
    done()
