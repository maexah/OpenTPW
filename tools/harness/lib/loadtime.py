#!/usr/bin/env python3
"""loadtime.py FILM PRESS RELEASE CROP END : how long a filmed Load Park (or Restart Park) took, from a film of the display whose frames
carry the WALL CLOCK (ffmpeg -use_wallclock_as_timestamps 1 -fps_mode passthrough -copyts, as loadtime.sh and loadtime_ours.py film) (Q921).
PRESS and RELEASE are the click's unix times (the driver stamps them), CROP is w:h:x:y of the film's own pixels (the game's picture),
END the unix time to read to (the next click's less half a second). Each frame (cropped, 64x36 gray) is classified by its nearest reference:
LIST = the last frame before the press, PARK = the frame PARKREF seconds after the press (env; default the last frame of the slice,
which is wrong where the slice runs into the next menu: give PARKREF=4); anything else is OTHER (a loading screen, a black frame).
Prints each change of class with its time after the press, the first frame of the loaded park (five in a row) after the press and after the
release, and the count of OTHER frames before it. A frame is good to about 33 ms (30 fps); the stamps are the film's, not the game's."""
import subprocess as sp, sys, numpy as np
film, press, release, crop, end = sys.argv[1], float(sys.argv[2]), float(sys.argv[3]), sys.argv[4], float(sys.argv[5])
hold = release - press
start = press - 2.0
W, H = 64, 36
pts = [float(x) for x in sp.check_output(["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries", "frame=pts_time", "-of", "csv=p=0", film]).decode().replace(",", " ").split()]
raw = sp.check_output(["ffmpeg", "-v", "error", "-i", film, "-vf", f"crop={crop},scale={W}:{H},format=gray", "-f", "rawvideo", "-"])
fr = np.frombuffer(raw, np.uint8).reshape(-1, H * W).astype(np.float32)
n = min(len(pts), len(fr)); pts = np.array(pts[:n]); fr = fr[:n]
keep = (pts >= start) & (pts <= end); pts = pts[keep]; fr = fr[keep]; n = len(pts)
t = pts - press
print(f"{n} frames, stamps {pts[0]-press:+.2f} .. {pts[-1]-press:+.2f} s about the press; median gap {np.median(np.diff(pts))*1000:.0f} ms, worst {np.diff(pts).max()*1000:.0f} ms")
before = np.where(t < -0.05)[0]; assert len(before), "no frame before the press"
import os
ref = float(os.environ["PARKREF"]) if os.environ.get("PARKREF") else None   # seconds after the press whose frame is the loaded park (else the last frame)
pi = int(np.argmin(np.abs(t - ref))) if ref is not None else n - 1
L = fr[before[-1]]; P = fr[pi]
dL = np.abs(fr - L).mean(1); dP = np.abs(fr - P).mean(1)
print(f"list ref frame at {t[before[-1]]:+.2f}, park ref at {t[pi]:+.2f}; list-park difference {np.abs(L-P).mean():.1f}")
cls = []
for i in range(n):
    c = "LIST" if dL[i] < 2.0 else ("PARK" if dP[i] < 0.6 * np.abs(L - P).mean() and dP[i] < dL[i] else "OTHER")
    cls.append(c)
last = None
for i in range(n):
    if t[i] < -0.3: continue
    if cls[i] != last:
        print(f"{t[i]:+7.3f} s  {cls[i]:5s}  (to list {dL[i]:5.1f}, to park {dP[i]:5.1f})"); last = cls[i]
# the first frame after the press that is not the list, and the first of the PARK run that stays PARK
post = [i for i in range(n) if t[i] >= 0]
first_change = next((i for i in post if cls[i] != "LIST"), None)
first_park = next((i for i in post if all(cls[j] == "PARK" for j in range(i, min(i + 5, n)))), None)
print(f"first frame not the list: {t[first_change]:+.3f} s" if first_change is not None else "never left the list")
print(f"first frame of the loaded park (and the 5 after it): {t[first_park]:+.3f} s after the press, {t[first_park]-hold:+.3f} s after the release" if first_park is not None else "no park frame")
other = [i for i in post if cls[i] == "OTHER" and (first_park is None or i < first_park)]
print(f"OTHER frames before the park: {len(other)}" + (f", from {t[other[0]]:+.3f} to {t[other[-1]]:+.3f} s" if other else ""))
# frozen runs: consecutive frames identical to within 0.3 (the whole picture stood still)
run = 0; worst = 0.0; start = None
for i in range(1, n):
    if t[i] < 0: continue
    if np.abs(fr[i] - fr[i-1]).mean() < 0.3:
        if run == 0: start = i - 1
        run += 1
        worst = max(worst, t[i] - t[start])
    else: run = 0
print(f"longest stretch after the press with the picture unchanged: {worst:.3f} s")
