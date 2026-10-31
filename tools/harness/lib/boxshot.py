"""boxshot.py - start OpenTPW.dll with a game folder that fails, on a private Xvfb (:79), photograph the failed start's message box
(Q978) and answer it with Return, or run with the box off / no display and read the log.

    boxshot.py <outdir> <bin dir> <game folder|-> [ENV=VAL ...]

<outdir> gets shot.png (the whole display, box up), stdout.txt and logs/console.log. DISPLAY=none starts with no display
(the box cannot show: "SDL said 'No message system available'"); OPENTPW_NO_MESSAGE_BOX=1 keeps the box off. There is no window
manager on the Xvfb, so the box has no title bar. NULLSTD=1 puts stdin, stdout and stderr on /dev/null (the nearest to a Windows start from
Explorer, which has no console; closed descriptors make .NET abort on Linux, so they are not used), so stdout.txt is not written and the log is all there is. The box is found as the smallest viewable top-level window. The game's HOME is
<outdir>/home, never the player's. Run with review-codex/venv/bin/python -u (python-xlib, PIL); the display is :79 (lane B's)."""
import os, sys, time, subprocess as sp
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import paths
from Xlib import X, display, XK
from Xlib.ext import xtest
out, bindir, game = sys.argv[1:4]; label = os.path.basename(out.rstrip("/"))
env = dict(os.environ); env["SDL_AUDIODRIVER"] = "dummy"; env["DOTNET_ROOT"] = paths.dotnet_env()["DOTNET_ROOT"]
env["HOME"] = os.path.abspath(out) + "/home"; os.makedirs(env["HOME"], exist_ok=True)
env["XDG_DATA_HOME"] = env["HOME"] + "/xdg"; env.pop("OPENTPW_GAME_PATH", None)
os.makedirs(out + "/logs", exist_ok=True); logs = os.path.abspath(out) + "/logs"
env["OPENTPW_LOG_DIR"] = logs
nodisp = nullstd = False
for kv in sys.argv[4:]:
    k, v = kv.split("=", 1)
    if k == "DISPLAY" and v == "none": nodisp = True
    elif k == "NULLSTD": nullstd = v == "1"
    else: env[k] = v
out = os.path.abspath(out)
xv = None
if nodisp: env.pop("DISPLAY", None)
else:
    env["DISPLAY"] = ":79"
    xv = sp.Popen([paths.xvfb(), ":79", "-screen", "0", "1024x768x24", "-nolisten", "tcp"], stdout=sp.DEVNULL, stderr=sp.DEVNULL)
    time.sleep(1.5)
t0 = time.time()
cmd = [paths.dotnet(), f"{bindir}/OpenTPW.dll"] + (["--game", game] if game != "-" else [])
if nullstd: p = sp.Popen(cmd, env=env, stdin=sp.DEVNULL, stdout=sp.DEVNULL, stderr=sp.DEVNULL)  # a start with nothing to print to (Q979)
else: p = sp.Popen(cmd, env=env, stdout=open(out + "/stdout.txt", "w"), stderr=sp.STDOUT)
try:
    if nodisp or env.get("OPENTPW_NO_MESSAGE_BOX"):
        rc = p.wait(30); print(f"{label}: exit {rc} after {time.time()-t0:.1f}s, no window expected")
    else:
        d = display.Display(":79"); root = d.screen().root
        win = None
        for _ in range(60):
            time.sleep(0.5)
            if p.poll() is not None: break
            v = [c for c in root.query_tree().children if c.get_attributes().map_state == X.IsViewable]
            if v:
                time.sleep(0.5); v = [c for c in root.query_tree().children if c.get_attributes().map_state == X.IsViewable]
                win = min(v, key=lambda c: c.get_geometry().width * c.get_geometry().height); break
        print(f"{label}: viewable top-level window {'found' if win else 'NOT found'} after {time.time()-t0:.1f}s; alive={p.poll() is None}")
        time.sleep(1.0)
        g = root.get_geometry(); img = root.get_image(0, 0, g.width, g.height, X.ZPixmap, 0xffffffff)
        raw = img.data; mean = sum(raw[::4]) / (len(raw) / 4)
        from PIL import Image
        Image.frombuffer("RGBX", (g.width, g.height), raw, "raw", "BGRX", 0, 1).convert("RGB").save(out + "/shot.png")
        print(f"{label}: grabbed {g.width}x{g.height}, mean blue-channel brightness {mean:.2f}")
        if win:
            win.set_input_focus(X.RevertToParent, X.CurrentTime); d.sync()
            kc = d.keysym_to_keycode(XK.XK_Return)
            xtest.fake_input(d, X.KeyPress, kc); d.sync(); time.sleep(0.1); xtest.fake_input(d, X.KeyRelease, kc); d.sync()
        rc = p.wait(15); print(f"{label}: exit {rc} after {time.time()-t0:.1f}s")
finally:
    if p.poll() is None: p.kill()
    if xv: xv.terminate()
