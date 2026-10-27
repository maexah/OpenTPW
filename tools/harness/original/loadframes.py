#!/usr/bin/env python3
"""loadframes.py PID SECS X,Z ZOOM ROT OUTPREFIX [EVERY] - frames of the running ORIGINAL at a camera of your own from
the moment a Load Park click's file is read in (Q26c). A POLL for loadfile.sh (it starts half a second before the click):

  POLL="python3 -u tick.py %PID% 90 & python3 -u loadframes.py %PID% 90 540,-20 130 0 $D/o 1.5; wait"

It waits for mGameTick to FALL (the file read in, tick.py's mark), sets the camera at once with setcam.py (follow off;
the numbers are setcam's and bothcam.py's: point x,z in world units, zoom 70..130, rotation in degrees), (again every half second until it stays: the load puts its
file's own camera back over its first frames), then takes a
frame of the off-screen display (:77, or DISPLAY_SHOT) every EVERY seconds (1.5) until SECS from its start, each
OUTPREFIX-t<mGameTick>.png, and prints one line a frame. RESET="9,12" writes the camera again that many seconds
after the fall: loadfile.sh's align sweep (6 s after the click) pans it. JUMP=1 takes a rise of more than 50 ticks as the
load too (fsload.sh: its fresh park's clock stands below the file's). X,Z "-" writes no camera at all (the file's own
kept; ZOOM and ROT are then ignored): frames named by the tick alone (Q743). Read-only but for the camera words setcam writes."""
import os, struct, subprocess, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]); point, zoom, rot, prefix = sys.argv[3], sys.argv[4], sys.argv[5], sys.argv[6]
every = float(sys.argv[7]) if len(sys.argv) > 7 else 1.5
here = os.path.dirname(os.path.abspath(__file__)); disp = os.environ.get("DISPLAY_SHOT", ":77")
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def tick():
    f.seek(0x0080239c); w = struct.unpack('<I', f.read(4))[0]; f.seek(w + 0x1da70c); return struct.unpack('<I', f.read(4))[0]
jump = os.environ.get("JUMP") == "1"   # a load whose file's tick is ABOVE the running park's (fsload.sh's fresh park)
t0 = time.time(); last = None
while time.time() - t0 < secs:
    try: now = tick()
    except (OSError, struct.error, ValueError): time.sleep(0.05); continue
    if last is not None and (now < last or (jump and now > last + 50)): break
    last = now; time.sleep(0.05)
else:
    print("no load seen", flush=True); sys.exit(1)
# The load sets its file's camera over a few frames after the fall, so the write is made again until it stays (ten tries).
nocam = point == "-"   # X,Z given as "-": no camera written, the file's own kept (Q743)
for attempt in range(0 if nocam else 10):
    r = subprocess.run([sys.executable, os.path.join(here, "setcam.py"), str(pid), point, zoom, rot, "off"], capture_output=True, text=True)
    if r.returncode == 0: break
    time.sleep(0.5)
if nocam: print(f"{time.time() - t0:7.2f} s tick {tick()} load seen; camera left as the file's", flush=True)
else: print(f"{time.time() - t0:7.2f} s tick {tick()} load seen; setcam exit {r.returncode}: {r.stdout.strip().splitlines()[-1] if r.stdout.strip() else r.stderr.strip()[-200:]}", flush=True)
# loadfile.sh's align sweep, 6 s after the click, drags the pointer to the edges and pans the camera: RESET="9,12" (seconds
# after the fall) writes it again then.
fell = time.time(); resets = [] if nocam else [float(x) for x in os.environ.get("RESET", "").split(",") if x]
while time.time() - t0 < secs:
    while resets and time.time() - fell >= resets[0]:
        resets.pop(0)
        r = subprocess.run([sys.executable, os.path.join(here, "setcam.py"), str(pid), point, zoom, rot, "off"], capture_output=True, text=True)
        print(f"{time.time() - t0:7.2f} s tick {tick()} camera written again: exit {r.returncode}", flush=True)
    t = tick(); out = f"{prefix}-t{t}.png"
    subprocess.run(["import", "-window", "root", "-display", disp, out], capture_output=True)
    print(f"{time.time() - t0:7.2f} s tick {t} frame {os.path.basename(out)}", flush=True)
    time.sleep(every)
