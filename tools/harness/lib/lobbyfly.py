#!/usr/bin/env python3
"""lobbyfly.py <outdir> <bin> [secs] (Q950): the lobby's attract flight, ours   (desktop; the mix to a file by SDL's disk driver)
Phase 1: nobody playing, `aim` and `sound` polled for SECS (default 300): the camera's statistics and the sound's census,
         shots of the flight. Phase 2: a player chosen by a real click while the nearest island is not Lost Kingdom:
         the log, `sound` before and after, the mix's rms either side of the click, a shot.
Run it with a python that has Xlib, PIL and numpy. The camera numbers are the ones `original/attractcam.py` and lobbyattract/stats.py give the original
(`docs/exe/lobby.md`, "Attract"); the sound census reads the console's `sound` line. Its `said()` lines are the checks of Q950 and are edited per item."""
import os, sys, math, statistics as st
OUT = sys.argv[1]; BIN = sys.argv[2]; SECS = float(sys.argv[3]) if len(sys.argv) > 3 else 300.0
NAME = "LOBBYFLY"; AUDIO_FILE = os.path.join(OUT, "mix.f32")
os.makedirs(OUT, exist_ok=True)
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
exec(open(paths.LIB + "/parkrun.py").read())
import numpy as np

NUM = r"(-?\d+\.\d+)"
def ask1(cmd, pattern, to=2.0):
    drain(); send(cmd); end = time.monotonic() + to
    while time.monotonic() < end:
        try: l = lines.get(timeout=0.1)
        except queue.Empty: continue
        if pattern in l: return l
    return None
def vec(tag, l):
    m = re.search(tag + r"=\(" + ",".join([NUM] * 3) + r"\)", l); return tuple(float(v) for v in m.groups())
def rms_between(a, b):
    a -= a % 8; b -= b % 8
    x = np.fromfile(AUDIO_FILE, dtype="<f4", offset=a, count=max(b - a, 0) // 4).astype(np.float64)
    return float(np.sqrt((x * x).mean())) if len(x) else 0.0

try:
    wait_for("Loaded the lobby", 150); attach(); time.sleep(3.0)
    samples, sounds = [], []
    t0 = time.monotonic(); n = 0; shots = 0
    print(f"PHASE 1: nobody playing, {SECS:.0f} s of `aim` and `sound`")
    while time.monotonic() - t0 < SECS:
        t = time.monotonic() - t0
        l = ask1("aim", "aim wandering")
        if l:
            samples.append(dict(t=t, pos=vec("pos", l), look=vec("look", l), at=vec("aimAt", l), isl=int(re.search(r"island=(-?\d+)", l).group(1)),
                                name=re.search(r"name='([^']*)'", l).group(1)))
        if n % 5 == 0:
            s = ask1("sound", "mode=")
            if s: sounds.append((t, s.strip()[s.find("mode="):], os.path.getsize(AUDIO_FILE) if os.path.exists(AUDIO_FILE) else 0))
        if t > shots * (SECS / 6) + 8 and shots < 6:
            shot(f"flight_{shots:02d}", 0.0); shots += 1
        n += 1; time.sleep(0.12)
    with open(os.path.join(OUT, "cam.txt"), "w") as f:
        for s in samples: f.write(f"{s['t']:.3f} pos {s['pos']} look {s['look']} aimAt {s['at']} isl {s['isl']} {s['name']}\n")
    with open(os.path.join(OUT, "sound.txt"), "w") as f:
        for t, s, sz in sounds: f.write(f"{t:.2f} bytes {sz} {s}\n")

    # ---- the camera's statistics (the same quantities as lobbyattract/stats.py)
    nm = lambda v: math.sqrt(sum(x * x for x in v)); sub = lambda a, b: tuple(x - y for x, y in zip(a, b))
    S = samples[5:]
    height = [s["pos"][2] for s in S]; dist = [nm(sub(s["pos"], s["at"])) for s in S]
    changes = [b["t"] for a, b in zip(S, S[1:]) if a["isl"] != b["isl"]]
    speed = []; turn = []
    for i, a in enumerate(S):
        j = i + 1
        while j < len(S) and S[j]["t"] - a["t"] < 0.2: j += 1
        if j >= len(S): break
        b = S[j]; dt = b["t"] - a["t"]
        speed.append(nm(sub(b["pos"], a["pos"])) / dt)
        u = sub(a["look"], a["pos"]); v = sub(b["look"], b["pos"])
        c = sum(x * y for x, y in zip(u, v)) / (nm(u) * nm(v)); turn.append(math.degrees(math.acos(max(-1, min(1, c)))) / dt)
    q = lambda xs, p: sorted(xs)[min(len(xs) - 1, int(len(xs) * p))]
    T = S[-1]["t"] - S[0]["t"]
    print(f"CAMERA {T:.0f} s, {len(S)} samples, island changes {len(changes)} (every {T / max(1, len(changes)):.1f} s)")
    print(f"  height min {min(height):.1f} median {st.median(height):.1f} max {max(height):.1f}")
    print(f"  distance to nearest island's aim point median {st.median(dist):.1f} p10 {q(dist, .1):.1f} p90 {q(dist, .9):.1f}")
    print(f"  eye speed median {st.median(speed):.2f}")
    print(f"  view turn deg/s median {st.median(turn):.2f} p90 {q(turn, .9):.1f} max {max(turn):.1f}")
    said(min(height) >= 20.0 and max(height) <= 115.0 and 62 <= st.median(height) <= 85, "C1b height 20-115, median 62-85")
    said(80 <= st.median(dist) <= 100, "C2 distance median 80-100")
    said(4.5 <= st.median(turn) <= 7.5 and q(turn, .9) < 16 and max(turn) < 80, "C3 turn median 4.5-7.5, p90 < 16, max < 80")
    said(abs(st.median(speed) - 10.0) < 0.3 and 9 <= len(changes) <= 18, "C4b eye speed 10, 9-18 island changes")

    # ---- the sound's census
    fl = [s for _, s, _ in sounds]
    parks = {re.search(r"sounds=(\S+)", s).group(1) for s in fl}
    counts = [int(re.search(r"sounding=(\d+)", s).group(1)) for s in fl]
    print(f"SOUND {len(fl)} polls; sounds= {sorted(parks)}; sounding= {sorted(set(counts))}; placed= {sorted({re.search(r'placed=(\S+)', s).group(1) for s in fl})}")
    print(f"  nearest park (park=) over the polls: {sorted({re.search(r'park=(\S+)', s).group(1) for s in fl})}")
    said(len(parks) == 1 and "Jungle" in parks, "A1 one park sounds on every poll, the head island's Jungle")
    said(max(counts) <= 1 and all("placed=flat" in s for s in fl) and counts.count(0) <= 0.02 * len(counts), f"A2b placed=flat always, sounding never above 1, sounding=0 on <= 2 percent of polls ({counts.count(0)} of {len(counts)})")
    # the theme is never cut short by a change of nearest island: the log's takes are one theme's length (18 s) and its delay apart
    stamps = [int(h) % 12 * 3600 + (43200 if ap == "PM" else 0) + int(mi) * 60 + int(se)
              for h, mi, se, ap in re.findall(r"\[(\d+):(\d+):(\d+)\W+(AM|PM)\] Lobby audio: Jungle music takes", open(os.path.join(OUT, "console.log"), errors="replace").read())]
    gaps_ = [b - a for a, b in zip(stamps, stamps[1:])]
    print(f"  theme takes in the log: {len(stamps)}; seconds between them {gaps_}; island changes in the run {len(changes)}")
    said(len(gaps_) >= 10 and all(20.5 <= g <= 22.5 for g in gaps_), "A3b every gap between the theme's takes is 20.5-22.5 s (none cut short by the island changes)")
    # mix level over the flight
    b0, b1 = sounds[1][2], sounds[-1][2]
    x = np.fromfile(AUDIO_FILE, dtype="<f4", offset=b0 - b0 % 8, count=(b1 - b0) // 4).astype(np.float64)
    rms = math.sqrt((x * x).mean()); pk_ = float(np.abs(x).max())
    print(f"  the mix over the flight: rms {rms:.4f} = {20 * math.log10(rms):.1f} dBFS, peak {pk_:.4f} = {20 * math.log10(pk_):.1f} dBFS (four parks, 2026-09: -31.0 / -12.0)")
    said(-34 < 20 * math.log10(rms) < -28 and 20 * math.log10(pk_) <= -8, "A4b rms between -34 and -28 dBFS, peak <= -8")

    # ---- phase 2: a player chosen from the flight, nearest island not Lost Kingdom
    print("PHASE 2: choose a player while the nearest island is not Lost Kingdom")
    for _ in range(600):
        l = ask1("aim", "aim wandering")
        if l and int(re.search(r"island=(-?\d+)", l).group(1)) != 0: break
        time.sleep(0.2)
    near_name = re.search(r"name='([^']*)'", l).group(1); print(f"  nearest at the click: {near_name}")
    pre = ask1("sound", "mode="); print("  before:", pre.strip()[pre.find("mode="):])
    shot("before_choice", 0.0)
    mark = len(logtext()); m_pre = os.path.getsize(AUDIO_FILE)
    c = ask1("control 7a14", "control"); m = re.search(r"middle \((-?\d+),(-?\d+)\)", c)
    move(int(m.group(1)), int(m.group(2)), "the player's slot"); m_move = os.path.getsize(AUDIO_FILE)
    button(1, "the slot"); m_click = os.path.getsize(AUDIO_FILE)
    post = []
    for i in range(8):
        s = ask1("sound", "mode=")
        if s: post.append((os.path.getsize(AUDIO_FILE), s.strip()[s.find("mode="):]))
        time.sleep(0.3)
    for sz, s in post: print(f"  after +{sz - m_click} B:", s)
    shot("after_choice", 1.0)
    new = logtext()[mark:]
    lob = [l.strip() for l in new.splitlines() if "Lobby audio" in l]
    print("  log lines 'Lobby audio' since the click:", lob)
    sec = 22050 * 2 * 4   # bytes a second if the file is 22050 Hz stereo f32; only the RATIO below is relied on
    before_r = rms_between(m_click - 2 * sec, m_click - 0 * sec); after_r = rms_between(m_click + 1 * sec, m_click + 3 * sec)
    print(f"  mix rms 2 s before the click's release {before_r:.4f}, 2 s after (from +1 s) {after_r:.4f}, ratio {after_r / max(before_r, 1e-9):.2f}")
    said(not any("theme '-', bed '-'" in l for l in lob) and any("kept from the flight" in l for l in lob), "H1 no 'theme - bed -' line; 'kept from the flight'")
    th_pre = re.search(r"theme=(\S+)", pre).group(1); th_post = {re.search(r"theme=(\S+)", s).group(1) for _, s in post}
    cnt_post = {int(re.search(r"sounding=(\d+)", s).group(1)) for _, s in post}
    print(f"  theme before {th_pre}, after {sorted(th_post)}; sounding after {sorted(cnt_post)}")
    said(cnt_post <= {1} and (th_pre in th_post or th_pre == "-"), "H2 same theme voice around the click (or its sample ended), sounding=1")
    ratio = after_r / max(before_r, 1e-9)
    said(0.7 <= ratio <= 1.4, "H3 mix rms after the click 0.7-1.4 x before")
    print("  H4 (the island on show is Lost Kingdom): read after_choice.png")
finally:
    done()
