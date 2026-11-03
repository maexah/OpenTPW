#!/usr/bin/env python3
"""loadtime_ours.py <outdir> <bin dir> : Load Park and Restart Park from inside a park, timed in THIS game (Q921). Run with the harness's python:
  OFF=1 ROW=2 LOADS=3 RESTARTS=2 python3 -u loadtime_ours.py ~/.cache/opentpw-harness/q921/run1 source/OpenTPW/bin/Release/net10.0
The display (:78 off-screen, lavapipe; OFF=0 for the desktop's own, FILMSIZE its size) is filmed by ffmpeg with wall-clock frame stamps from before the
menu opens; every click's press and release wall times go to OUT/press.txt (`k press release`, `rk` for a restart); analyse with lib/loadtime.py
(CROP 512:288:0:0 for the off-screen display's 1280 x 720 window). The Load list's rows: ROW (default 2: the player's autosave is first, the file
copied in, FILE (default the game's levels/jungle/Easymode.TPWI, the file the original loads), second). The game's own `[load] jungle: N ms in total`
line is in OUT/run.log. Restart Park reads the player's restart.INTS (the stock park's, in the private game folder)."""
import os, re, shutil, subprocess as sp, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import paths
OUT = os.path.expanduser(sys.argv[1]); os.makedirs(OUT, exist_ok=True); BIN = os.path.expanduser(sys.argv[2]); NAME = "LOADTIME"; OFFSCREEN = os.environ.get("OFF", "1") == "1"
exec(open(os.path.join(paths.LIB, "parkrun.py")).read())
SRC = os.path.expanduser(os.environ.get("FILE") or os.path.join(paths.game(), "data/levels/jungle/Easymode.TPWI"))
ROW = int(os.environ.get("ROW", "2"))
film = None
def film_start(path):
    global film
    if _OFF: src, geom, crop = _DISP, "1600x900", "512:288:0:0"      # the private display whole; the window is its top left corner
    else:                                                               # the desktop: the window's own rectangle
        at = win.translate_coords(root, 0, 0); g = win.get_geometry()
        src, geom = f"{os.environ.get('DISPLAY', ':0')}+{-at.x},{-at.y}", f"{g.width}x{g.height}"
        crop = f"640:{(640 * g.height // g.width) // 2 * 2}:0:0"
    print(f"  film: {src} {geom}; loadtime.py's CROP for it: {crop}")
    film = sp.Popen(["ffmpeg", "-loglevel", "error", "-y", "-use_wallclock_as_timestamps", "1", "-f", "x11grab", "-framerate", "30",
        "-video_size", geom, "-i", src, "-vf", "scale=640:-2", "-fps_mode", "passthrough", "-copyts", "-c:v", "libx264", "-preset", "ultrafast", "-crf", "16", "-pix_fmt", "yuv420p", path],
        stdin=sp.DEVNULL, stderr=open(os.path.join(OUT, "ffmpeg.log"), "w"))
def film_stop():
    if film: film.send_signal(2); film.wait(20)
try:
    enter()
    ask("pause", "pause")
    first = re.findall(r"\[load\] jungle: (\d+) ms in total", logtext()); print("  first entry from the lobby: LoadTimer", first)
    dest = os.path.join(JUNGLE, "LOADTIME.TPWS"); own_file(dest); shutil.copy(SRC, dest); print("  copied", SRC, os.path.getsize(dest), "bytes")
    ask("resume", "resume"); time.sleep(3.0)
    film_start(os.path.join(OUT, "film.mkv")); time.sleep(1.5)
    for k in range(1, int(os.environ.get("LOADS", "3")) + 1):
        reply("menu"); time.sleep(0.6)
        x, y, w_, h = place("1"); move(x + w_ / 2, y + h / 2, "the menu's Load row"); button(1, "Load"); time.sleep(0.8)
        lp = re.findall(r"Load Park: \d+ saved parks.*", logtext()); print("  log: " + (lp[-1].strip() if lp else "(none)"))
        rh = int(re.search(r"rows of (\d+) units", lp[-1]).group(1)) if lp else 39
        shot(f"{k}-list", 0.6)
        x, y, w_, h = place("23bce0"); px, py = x + w_ * 0.25, y + h * ((229 - 203) + (ROW - 0.5) * rh) / (772 - 203)
        move(px, py, f"row {ROW}"); drain()
        tp = _time.time()
        xtest.fake_input(d, X.ButtonPress, 1); d.sync(); time.sleep(0.06)
        xtest.fake_input(d, X.ButtonRelease, 1); d.sync(); tr = _time.time()
        open(os.path.join(OUT, "press.txt"), "a").write(f"{k} {tp:.3f} {tr:.3f}\n"); print(f"  [XTEST load {k}: button 1 at wall {tp:.3f}, released {tr:.3f}, row {ROW}]")
        wait_for("Loaded jungle", 180); tl = _time.time(); print(f"  load {k}: 'Loaded jungle' seen at +{tl - tp:.2f} s (the script's own reader, 0.2 s grain)")
        time.sleep(5.0); shot(f"{k}-loaded", 0.1)
    for k in range(1, int(os.environ.get("RESTARTS", "2")) + 1):
        reply("menu"); time.sleep(0.6)
        x, y, w_, h = place("3"); move(x + w_ / 2, y + h / 2, "the menu's Restart Park row"); button(1, "Restart Park"); time.sleep(1.0)
        shot(f"r{k}-question", 0.3)
        x, y, w_, h = place("9873c9"); move(x + w_ / 2, y + h / 2, "the question's tick"); drain()
        tp = _time.time()
        xtest.fake_input(d, X.ButtonPress, 1); d.sync(); time.sleep(0.06)
        xtest.fake_input(d, X.ButtonRelease, 1); d.sync(); tr = _time.time()
        open(os.path.join(OUT, "press.txt"), "a").write(f"r{k} {tp:.3f} {tr:.3f}\n"); print(f"  [XTEST restart {k}: tick pressed at wall {tp:.3f}, released {tr:.3f}]")
        wait_for("Loaded jungle", 180); tl = _time.time(); print(f"  restart {k}: 'Loaded jungle' seen at +{tl - tp:.2f} s (the script's own reader, 0.2 s grain)")
        time.sleep(5.0); shot(f"r{k}-loaded", 0.1)
    film_stop(); film = None
    txt = logtext()
    for l in re.findall(r"\[load\] jungle: .*", txt)[-24:]: print("  " + l.strip())
    print("  log: " + " | ".join(re.findall(r"jungle: reading the park from .*", txt)[-1:]))
finally:
    if film: film_stop()
    done()
