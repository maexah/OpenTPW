#!/usr/bin/env python3
"""lobbyentry.py <outdir> <bin> (Q630): ours entering Lost Kingdom from the lobby as a Full Simulation player, on the desktop.
A copy of Alexah's Full Simulation profile (keys 2) on the private player, the slot clicked, the island panel's price read
(`control 1e0ea`), `particles screen` and the frame `before`; then a real Return (Enter this park, on its release), and
for FOR seconds (env, default 2) frames f-NN every ~0.05 s for the first 2 s and every ~0.3 s after, each printed with its
time after the release and its mean, `particles screen` every 0.1 s over the first 2 s and `windows` at 0.5 s. Then the
park's load waited for, the profile put back, done(). The census lines, the "key puff" and "entering" log lines are in
confirm.txt. CANCEL=<s> (env) presses Escape that long after the release, asks `windows` 0.2 s later, and waits for no
load: the flight cancelled, the panel back (Q669). ORBIT=<radians> (env) sets the orbit's angle by the console's `orbit` just
before the Return, to start the turn where a measured original's stood (`original/lobbycam.py`); the "Lobby camera" log
lines (the leave's start, facing the gate, arrival, each with its seconds and passes) are printed (Q670). The original's same entry: original/lobbypanel.sh with FILM= and
POLL=emitters.py."""
import os, sys, hashlib
OUT = sys.argv[1]; BIN = sys.argv[2]; NAME = "lobbyentry"
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
exec(open(paths.LIB + "/parkrun.py").read())
exec(open(paths.LIB + "/fsplayer.py").read())
print("OpenTPW.dll md5 " + hashlib.md5(open(os.path.join(BIN, "OpenTPW.dll"), "rb").read()).hexdigest()[:8])
try:
    fs_profile(os.path.join(paths.need("OPENTPW_FULLSIM_PLAYER"), "gms.dat"))
    wait_for("Loaded the lobby", 150); attach(); time.sleep(3.0)
    l = ask("control 7a14", "control"); m = re.search(r"middle \((-?\d+),(-?\d+)\)", l)
    move(int(m.group(1)), int(m.group(2)), "the player's slot"); button(1, "the slot"); time.sleep(8.0)
    move(640, 600, "away from the price"); time.sleep(1.0)
    reply("control 1e0ea", 0.8, 1)
    reply("particles screen", 0.8, 9)
    if os.environ.get("ORBIT"): reply("orbit " + os.environ["ORBIT"], 0.5, 1)
    shot("before", 0.3)
    drain()
    code = d.keysym_to_keycode(XK.string_to_keysym("Return"))
    xtest.fake_input(d, X.KeyPress, code); d.sync(); time.sleep(0.10)
    xtest.fake_input(d, X.KeyRelease, code); d.sync(); t0 = time.time()
    print("  [XTEST key Return: Enter this park]")
    frames = []; asked = 0; win_asked = False
    cancel = float(os.environ["CANCEL"]) if os.environ.get("CANCEL") else None; cancelled = None; win2 = False
    while time.time() - t0 < float(os.environ.get("FOR", "2.0")):
        if time.time() - t0 >= asked * 0.1 and time.time() - t0 < 2.0: send("particles screen"); asked += 1
        if not win_asked and time.time() - t0 >= 0.5: send("windows"); win_asked = True
        if cancel is not None and cancelled is None and time.time() - t0 >= cancel:
            esc = d.keysym_to_keycode(XK.string_to_keysym("Escape"))
            xtest.fake_input(d, X.KeyPress, esc); d.sync(); time.sleep(0.05)
            xtest.fake_input(d, X.KeyRelease, esc); d.sync(); cancelled = time.time() - t0
        if cancelled is not None and not win2 and time.time() - t0 >= cancelled + 0.2: send("windows"); win2 = True
        frames.append((time.time() - t0, grab())); time.sleep(0.04 if time.time() - t0 < 2.0 else 0.3)
    got = collect(1.5)
    if cancelled is not None: print(f"  [XTEST key Escape at {cancelled:.2f} s: cancel the flight]")
    for g in got:
        if "[dbg]" in g or "Front end" in g: print("  log:", g.strip()[-200:])
    for i, (t, im) in enumerate(frames):
        im.save(os.path.join(OUT, f"f-{i:02d}.png"))
        print(f"  frame f-{i:02d} at {t:.2f} s mean {ImageStat.Stat(im.convert('L')).mean[0]:.1f}")
    for g in [l for l in logtext().splitlines() if "key puff" in l or "entering" in l or "Lobby camera" in l][-6:]: print("  log:", g.strip())
    if cancel is None and "Loaded jungle" not in logtext(): wait_for("Loaded jungle", 180)
    time.sleep(1.0)
finally:
    fs_off(); done()
