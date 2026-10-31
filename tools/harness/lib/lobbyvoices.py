#!/usr/bin/env python3
"""lobbyvoices.py <outdir> <bin> [secs] (Q770): which voices the lobby sounds with nobody playing, and whether given samples are in its mix.
Desktop run, the mix to a file by SDL's disk driver. Polls the console's `voices` and `sound` about every 0.6 s for SECS (default 60) into
voices.txt (the time, the mix file's size, the reply), takes two shots (lobby_00, lobby_01), then prints how many polls name one of SAMPLES
(the census check) and scores each sample's first 1.5 s against the whole mix (the capture check), beside the jungle themes the census
names as a control (one that played whole scores high; a low one is a theme cut by the window, not a blind measure).
SAMPLES is the env var `LOBBY_SAMPLES`, `<sdt under the game's data>:<index>,<index>...`; the default `global/sound/sSfxHD.sdt:0,1,2,3` is
global lobby effect 3's ew_space_1 to 4. The original's side is `original/voices.py` and `original/capmatch.py` (TOOLING.md, "The original
under Proton"). Run it with a Python that has numpy and Xlib (`review-codex/venv` here)."""
import os, sys, re
OUT = sys.argv[1]; BIN = sys.argv[2]; SECS = float(sys.argv[3]) if len(sys.argv) > 3 else 60.0
NAME = "lobbyvoices"; AUDIO_FILE = os.path.join(os.path.abspath(OUT), "mix.f32")
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
os.makedirs(OUT, exist_ok=True)
exec(open(paths.LIB + "/parkrun.py").read())

def ask1(cmd, pattern, to=2.0):
    drain(); send(cmd); end = time.monotonic() + to
    while time.monotonic() < end:
        try: l = lines.get(timeout=0.1)
        except queue.Empty: continue
        if pattern in l: return l
    return None
try:
    wait_for("Loaded the lobby", 150); attach(); time.sleep(3.0)
    polls = 0; t0 = time.monotonic(); n = 0; shots = 0
    with open(os.path.join(OUT, "voices.txt"), "w") as f:
        while time.monotonic() - t0 < SECS:
            t = time.monotonic() - t0
            v = ask1("voices", "voices ")
            s = ask1("sound", "mode=") if n % 5 == 0 else None
            nbytes = os.path.getsize(AUDIO_FILE) if os.path.exists(AUDIO_FILE) else 0
            f.write(f"{t:.1f} bytes {nbytes} {v.strip() if v else 'NO-REPLY'}\n")
            if s: f.write(f"{t:.1f} bytes {nbytes} SOUND {s.strip()[s.find('mode='):]}\n")
            f.flush(); polls += 1
            if shots < 2 and t > shots * (SECS / 2) + 6:
                shot(f"lobby_{shots:02d}", 0.0); shots += 1
            n += 1; time.sleep(0.6)
    print(f"{polls} polls")
finally:
    done()

import numpy as np
sys.path.insert(0, paths.LIB); import mixmatch
spec = os.environ.get("LOBBY_SAMPLES", "global/sound/sSfxHD.sdt:0,1,2,3"); sdt, idx = spec.rsplit(":", 1)
sdt = os.path.join(paths.game(), "data", sdt); temps = [mixmatch.template(sdt, int(i)) for i in idx.split(",")]
rows = [l for l in open(os.path.join(OUT, "voices.txt")) if " voices " in l]
names = {n for n, _ in temps}
hit = sum(1 for l in rows if any(n in l for n in names))
print(f"voices with SAMPLES' names {sorted(names)}: {hit} of {len(rows)} polls")
mix = mixmatch.window(AUDIO_FILE, 0, os.path.getsize(AUDIO_FILE))
themes = sorted({m.group(1) for l in rows for m in re.finditer(r"(\S+\.mp2) Music", l)})
lm = os.path.join(paths.game(), "data", "levels/jungle/Sound/LobbyMusicHD.sdt"); ents = [n for n, _ in mixmatch.entries(lm)]
for th in (t for t in themes if t in ents):
    sc, g, at, rms = mixmatch.match(mix, mixmatch.template(lm, ents.index(th))[1], 6.0)
    print(f"  control {th} (a jungle theme the census names; one that played whole must score high) score {sc:.3f} gain {g:.4f}")
