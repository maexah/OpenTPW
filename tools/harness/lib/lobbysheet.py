#!/usr/bin/env python3
"""lobbysheet.py FILM.mkv FILM.t0 WALL FROM TO STEP OUT.png [--cols N] [--width W] [--all DIR] (Q749 fix): frames of a 30 fps film (lobbyfilm.py's
pick.mkv / quit.mkv) at WALL + FROM .. WALL + TO seconds every STEP seconds, as one contact sheet labelled with the seconds from WALL. WALL is a
wall clock (the click's, from pick-click.t, or the lobby's first answer from quit-eye.txt's header); the film's start clock FILM.t0 is good to
about 0.2 s. --all DIR also writes every frame of the span at the film's rate to DIR/f<n>.png."""
import os, shutil, subprocess, sys, tempfile
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from contactsheet import sheet
a = sys.argv[1:]; opts = {}
for o in ("--cols", "--width", "--all"):
    if o in a: i = a.index(o); opts[o] = a[i + 1]; del a[i:i + 2]
film, t0f, wall, lo, hi, step, out = a[0], a[1], float(a[2]), float(a[3]), float(a[4]), float(a[5]), a[6]
t0 = float(open(t0f).read()); items = []; tmp = tempfile.mkdtemp(prefix="lobbysheet-", dir=os.path.expanduser("~/.cache/dn-tmp"))   # never /tmp, which is RAM
n = int(round((hi - lo) / step)) + 1
for k in range(n):
    s = lo + k * step; ft = wall + s - t0; f = os.path.join(tmp, f"s{k:03d}.png")
    subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-ss", f"{max(ft, 0):.3f}", "-i", film, "-frames:v", "1", f], check=True)
    if os.path.exists(f): items.append((f, f"{s:+.2f} s"))
sheet(out, items, int(opts.get("--cols", 6)), int(opts.get("--width", 300))); shutil.rmtree(tmp)
if "--all" in opts:
    os.makedirs(opts["--all"], exist_ok=True)
    subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-ss", f"{max(wall + lo - t0, 0):.3f}", "-t", f"{hi - lo:.3f}", "-i", film,
                    os.path.join(opts["--all"], "f%03d.png")], check=True)
print(out, len(items), "frames")
