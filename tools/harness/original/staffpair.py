#!/usr/bin/env python3
"""staffpair.py PID SECS - wait until two of the stock park's staff (things 25 to 30) are drawn with their pick circles'
middles within 40 x 30 px of each other on the 1024 x 768 frame, both well inside it; print the pair and their `where`
lines and exit 0, or exit 1 after SECS. Read-only (spritepick.py where)."""
import re, subprocess, sys, time, os, itertools
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
pid, secs = sys.argv[1], float(sys.argv[2])
SP = os.path.join(paths.ORIGINAL_TOOLS, "spritepick.py")
t0 = time.time()
while time.time() - t0 < secs:
    at = {}
    for t in range(25, 31):
        o = subprocess.run(["python3", SP, pid, "where", str(t)], capture_output=True, text=True).stdout.strip()
        m = re.search(r"pixel (-?\d+) (-?\d+) r2 ([\d.]+) w ([\d.]+)", o)
        if m: at[t] = (int(m.group(1)), int(m.group(2)), float(m.group(3)), float(m.group(4)), o)
    for a, b in itertools.combinations(sorted(at), 2):
        (xa, ya, *_), (xb, yb, *_) = at[a], at[b]
        if abs(xa - xb) < 40 and abs(ya - yb) < 30 and all(120 < v[0] < 900 and 120 < v[1] < 650 for v in (at[a], at[b])):
            print(f"pair {a} {b} after {time.time() - t0:.1f} s"); print(at[a][4]); print(at[b][4]); sys.exit(0)
    time.sleep(0.2)
print("pair none"); sys.exit(1)
