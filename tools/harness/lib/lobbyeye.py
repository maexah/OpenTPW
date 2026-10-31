#!/usr/bin/env python3
"""lobbyeye.py <outdir> <bin-folder> [pre-seconds] (Q749): ours' lobby eye after a park is quit  (off-screen, lavapipe: numbers are the evidence)
A player chosen by a real click, `park jungle`, 15 s in the park, `lobby`, then `lobbyeye` polled (a pure getter) until 14 s after the first
lobby answer; before the park it is polled for PRE seconds too (the lobby's own eye the last flight ends at). Rows: outdir/eye.txt, in the
original's y-up axes, to set beside `original/rendereye.py` (`docs/TOOLING.md`)."""
import os, sys
OUT = sys.argv[1]; BIN = sys.argv[2]; PRE = float(sys.argv[3]) if len(sys.argv) > 3 else 8.0
NAME = "LOBBYEYE"; OFFSCREEN = True; OFFSCREEN_DISPLAY = ":78"
os.makedirs(OUT, exist_ok=True)
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
exec(open(paths.LIB + "/parkrun.py").read())

def ask1(cmd, pattern, to=1.5):
    drain(); send(cmd); end = time.monotonic() + to
    while time.monotonic() < end:
        try: l = lines.get(timeout=0.05)
        except queue.Empty: continue
        if pattern in l: return l
    return None
rows = []
def poll(tag, secs, stop_when_lobby=False):
    t0 = time.monotonic()
    while time.monotonic() - t0 < secs:
        l = ask1("lobbyeye", "[dbg]", 1.0)
        if l and "eye (" in l:
            rows.append((tag, time.monotonic(), l[l.find("eye ("):].strip()))
        time.sleep(0.02)
try:
    wait_for("Loaded the lobby", 150); attach(); time.sleep(3.0)
    l = ask("control 7a14", "control"); m = re.search(r"middle \((-?\d+),(-?\d+)\)", l)
    move(int(m.group(1)), int(m.group(2)), "the player's slot"); button(1, "the slot")
    poll("pre", PRE)
    shot("lobby-before-park", 0.0)
    send("park jungle"); wait_for("Loaded jungle", 180); time.sleep(15.0)
    shot("park", 0.0)
    mark = time.monotonic(); send("lobby")
    # poll until the lobby answers, then 14 s on
    first = None
    while first is None and time.monotonic() - mark < 90:
        l = ask1("lobbyeye", "[dbg]", 1.0)
        if l and "eye (" in l:
            first = time.monotonic(); rows.append(("post", first, l[l.find("eye ("):].strip()))
        time.sleep(0.02)
    print("lobby answered", None if first is None else f"{first - mark:.2f} s after `lobby`")
    poll("post", 14.0)
    shot("lobby-after", 0.0)
    with open(os.path.join(OUT, "eye.txt"), "w") as f:
        f.write(f"quit-at {mark:.4f}\n")
        for tag, t, r in rows: f.write(f"{tag} {t:.4f} {r}\n")
    print(f"{len(rows)} rows")
finally:
    done()
