#!/usr/bin/env python3
"""headseries.py <outdir> <bin> [ITEM [NAME]] [--photo ROLE:FRAME,...] - a ride's riders' heads in THIS game across its clips (Q848).
The stock park, paused, ITEM bought at (41,22) (default 1109, Mumbo; NAME is the word its `rides` line carries, default Mumbo), its queue
joined to the path at (43,21), five guests sent to it; once `heads 5/5` and running, the park is paused and saved (<outdir>/Q848.TPWS,
which the original loads with original/loadfile.sh) and then, still paused:
    series (default)   `step STEP` and `rides` COUNT times (env Q848_STEP, Q848_COUNT; 3 and 450): series.json holds, a sample each, the ride's first
                       channel (role, clip, frame, length) and each head's name, kind and drawn place (x, depth, height) and rest place
    --photo T,...      on the desktop (not off-screen): a shot of the window at each role:frame (within 1.6 frames) named role<R>-frame<F>.png
Each head is a record of the lookup table, head N of the node ids from 1; original/headpause.py reads the original's the same way and
lib/headcompare.py sets the two against each other. review-codex/venv/bin/python -u; BIN the build's folder."""
import os, sys, json
OUT = sys.argv[1]; BIN = sys.argv[2]; NAME = "Q848"
pos = [a for a in sys.argv[3:] if not a.startswith("--") and ":" not in a]
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
    return [(h[2], h[10], [float(v) for v in h[11:14]], [float(v) for v in h[4:7]]) for h in re.findall(
        r"(\d+):(\d+) '(Head\w+)' (\w+) \(([-\d.]+),([-\d.]+),([-\d.]+)\) model \(([-\d.]+),([-\d.]+),([-\d.]+)\) drawn (\w+) \(([-\d.]+),([-\d.]+),([-\d.]+)\)", line)]
try:
    enter(); reply("pause", 0.5)
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
        if l and "heads 5/5" in l[0] and "running True" in l[0]: ok = True; print("  aboard after", i + 1, "s"); break
    print("  aboard:", ok)
    reply("pause", 0.5); send("step 2"); time.sleep(1.0)
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
