#!/usr/bin/env python3
"""themescan.py CAPTURE [FROM_BYTE] (Q950): which park's lobby music is in the ORIGINAL's own mix. Every theme of the four parks'
LobbyMusicHD.sdt (twelve entries) is matched, by its first 6 s, against the capture an ORIG_ASOUND file device wrote (f32 stereo
48 kHz, unpaced; capmatch.py's note), in 25 s chunks from FROM_BYTE (default 20,000,000), and ranked by its best score. The original's
flight (2026-10-31): Lost Kingdom's three 0.29 to 0.48, the other nine 0.08 to 0.13. A 0.6 s template (capmatch.py --scan) is too short
to tell them apart. Needs numpy and ffmpeg."""
import os, subprocess, sys, numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths; import mixmatch
R = 48000; mixmatch.R = R
cap = sys.argv[1]; size = os.path.getsize(cap); off0 = int(sys.argv[2]) if len(sys.argv) > 2 else 20000000
game = paths.need("OPENTPW_GAME_PATH")
res = []
for theme in ("jungle", "fantasy", "hallow", "space"):
    sdt = os.path.join(game, "data", "levels", theme, "Sound", "LobbyMusicHD.sdt")
    for i, (name, data) in enumerate(mixmatch.entries(sdt)):
        tpl = np.frombuffer(subprocess.run(["ffmpeg", "-v", "error", "-f", "mp3", "-i", "-", "-ac", "1", "-ar", str(R), "-f", "f32le", "-"],
                                           input=data, capture_output=True).stdout, dtype="<f4").astype(np.float64)
        best = (0.0, 0); a = off0; step = R * 2 * 4 * 25
        while a < size:
            score, _, at, _ = mixmatch.match(mixmatch.window(cap, a, min(size, a + step + R * 2 * 4 * 7)), tpl, 6.0)
            if score > best[0]: best = (score, a + int(at * R * 8))
            a += step
        res.append((best[0], theme, name, best[1]))
for r in sorted(res, reverse=True): print("%.3f %-8s %-14s at byte %d" % r)
