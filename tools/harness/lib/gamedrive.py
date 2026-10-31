"""Shared by q114b's runs: launch OpenTPW with the console, XTEST keys and buttons, root-window grabs.
exec() it after setting OUT and BIN."""
import os, queue, re, subprocess, sys, threading, time, hashlib
import paths   # the calling script put tools/harness/lib on sys.path (README.md, "Writing a run")
from Xlib import X, XK, display
from Xlib.ext import xtest
from PIL import Image, ImageStat

GAME = globals().get("PRIVATE") or paths.game()   # parkrun.py sets PRIVATE; alone, the real game folder
os.makedirs(OUT, exist_ok=True)

# Every line a run prints (its predictions, results and score) is kept in OUT/confirm.txt too, so a run's score can
# be read back later and not only from a terminal that has gone (the 2026-10-06 review's fix 7).
import builtins as _builtins
_kept = open(os.path.join(OUT, "confirm.txt"), "a")
_kept.write(time.strftime("== run of %Y-%m-%d %H:%M:%S\n"))
def print(*args, **kwargs):
    _builtins.print(*args, **kwargs)
    if kwargs.get("file") is None:
        _kept.write(kwargs.get("sep", " ").join(str(a) for a in args) + kwargs.get("end", "\n")); _kept.flush()

def save_sum():
    h = hashlib.md5()
    for root_, _, files in sorted(os.walk(os.path.join(GAME, "save"))):
        for f in sorted(files):
            p = os.path.join(root_, f); h.update(p.encode()); h.update(open(p, "rb").read())
    return h.hexdigest()

before = save_sum(); print("save/ before:", before)
print("binary md5:", hashlib.md5(open(os.path.join(BIN, "OpenTPW.dll"), "rb").read()).hexdigest())
env = dict(os.environ, DOTNET_ROOT=paths.dotnet_env()['DOTNET_ROOT'], OPENTPW_DEBUG_CONSOLE="1",
           OPENTPW_GAME_PATH=GAME, SDL_AUDIODRIVER="dummy", OPENTPW_NO_MESSAGE_BOX="1")
# The game runs in a systemd scope of its own under a memory cap (OPENTPW_GAME_MEMMAX, "8G" by default, "off" for none), so
# a runaway is killed alone and never the terminal's scope with every session in it (2026-10-30: 11.1 GB off-screen on a
# Full Simulation park, Q954). Without systemd-run the game runs uncapped.
_CAP = os.environ.get("OPENTPW_GAME_MEMMAX", "8G")
_CMD = [paths.dotnet(), "OpenTPW.dll", "--game", GAME]
if _CAP != "off" and __import__("shutil").which("systemd-run"):
    _CMD = ["systemd-run", "--user", "--scope", "--quiet", "--collect", "-p", f"MemoryMax={_CAP}", "-p", "MemorySwapMax=0", "--"] + _CMD
    print(f"game memory cap: {_CAP}")
game = subprocess.Popen(_CMD, cwd=BIN,
                        stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                        env=env, text=True, bufsize=1)
lines = queue.Queue(); log = open(os.path.join(OUT, "run.log"), "w")
def pump():
    for l in game.stdout:
        log.write(l); log.flush(); lines.put(l.rstrip())
threading.Thread(target=pump, daemon=True).start()
def send(c): game.stdin.write(c + "\n"); game.stdin.flush()
def wait_for(t, to):
    end = time.monotonic() + to
    while time.monotonic() < end:
        try: l = lines.get(timeout=0.5)
        except queue.Empty:
            if game.poll() is not None: raise SystemExit(f"game exited early ({game.returncode}" + (", killed: the memory cap?" if game.returncode in (-9, 137) else "") + ")")
            continue
        if t in l: return l
    raise SystemExit(f"timed out waiting for {t!r}")
def drain():
    while not lines.empty(): lines.get()
def collect(secs):
    got = []; end = time.monotonic() + secs
    while time.monotonic() < end:
        try: got.append(lines.get(timeout=0.2))
        except queue.Empty: pass
    return got
def tail(l): return l.strip()[l.find('[dbg]'):] if '[dbg]' in l else l.strip()[-220:]
def ask(c, m, to=12):
    drain(); send(c); l = wait_for(m, to); print(f"  {c:<12} -> {tail(l)}"); return l
def predict(what): print(f"PREDICT {what}")
def find_by_pid(d_, pid):
    atom = d_.intern_atom("_NET_WM_PID"); st = [d_.screen().root]
    while st:
        w = st.pop()
        try:
            p = w.get_full_property(atom, X.AnyPropertyType)
            if p and p.value[0] == pid and w.get_wm_name() == "Theme Park World": return w
            st.extend(w.query_tree().children)
        except Exception: pass

d = win = root = None
def attach():
    global d, win, root
    d = display.Display(); win = find_by_pid(d, game.pid); root = d.screen().root
    win.set_input_focus(X.RevertToParent, X.CurrentTime); d.sync()
def size():
    g = win.get_geometry(); return g.width, g.height
def grab():
    w, h = size(); at = win.translate_coords(root, 0, 0)
    raw = root.get_image(-at.x, -at.y, w, h, X.ZPixmap, 0xffffffff)
    return Image.frombytes("RGB", (w, h), raw.data, "raw", "BGRX")
def gadget_gold(im):
    """Pixels of the gadget body's blue in the bottom-left corner: the ruler for 'is the interface drawn'. The sky's
    blue is bright in green too and the ground has none, so it is checked against a frame with the gadget and one without."""
    w, h = im.size; crop = im.crop((0, int(h * 0.60), int(w * 0.20), h))
    return sum(1 for r, g, b in crop.getdata() if b > 140 and r < 70 and g < 90)
def shot(name, settle=1.0):
    time.sleep(settle)
    im = grab(); im.save(os.path.join(OUT, name + ".png"))
    print(f"  shot {name}: {im.size[0]}x{im.size[1]} mean {ImageStat.Stat(im.convert('L')).mean[0]:.1f} gadget-blue {gadget_gold(im)}")
    return im
def move(px, py, what):
    at = win.translate_coords(root, 0, 0)
    xtest.fake_input(d, X.MotionNotify, x=int(-at.x + px + 3), y=int(-at.y + py)); d.sync(); time.sleep(0.2)
    xtest.fake_input(d, X.MotionNotify, x=int(-at.x + px), y=int(-at.y + py)); d.sync(); time.sleep(0.6)
    print(f"  [XTEST pointer to window ({px:.0f},{py:.0f}), {what}]")
WATCH = ("world click", "Hand:", "Escape:", "Park gadget", "backspace", "delete:", "Camcorder", "camcorder", "Full-screen", "right click")
def _after(got):
    got = [l for l in got if any(w_ in l for w_ in WATCH)]
    print("  log: " + (" | ".join(tail(g) for g in got) if got else "(no line)"))
def key(name, what="", mods=()):
    """One real key through XTEST: modifiers down, key down, key up, modifiers up."""
    drain()
    codes = [d.keysym_to_keycode(XK.string_to_keysym(m)) for m in mods]
    code = d.keysym_to_keycode(XK.string_to_keysym(name))
    for c in codes: xtest.fake_input(d, X.KeyPress, c); d.sync(); time.sleep(0.05)
    xtest.fake_input(d, X.KeyPress, code); d.sync(); time.sleep(0.12)
    xtest.fake_input(d, X.KeyRelease, code); d.sync(); time.sleep(0.05)
    for c in reversed(codes): xtest.fake_input(d, X.KeyRelease, c); d.sync()
    print(f"  [XTEST key {'+'.join(list(mods) + [name])}{': ' + what if what else ''}]")
    _after(collect(1.2))
def key_down(name):
    xtest.fake_input(d, X.KeyPress, d.keysym_to_keycode(XK.string_to_keysym(name))); d.sync()
def key_up(name):
    xtest.fake_input(d, X.KeyRelease, d.keysym_to_keycode(XK.string_to_keysym(name))); d.sync()
def button(n, what, held=0.06):
    drain()
    xtest.fake_input(d, X.ButtonPress, n); d.sync(); time.sleep(held)
    xtest.fake_input(d, X.ButtonRelease, n); d.sync()
    print(f"  [XTEST button {n} held {held} s: {what}]")
    _after(collect(1.5))
def count(name):
    drain(); send("unimplemented"); got = collect(1.5)
    n = [l for l in got if name in l]
    print(f"  unimplemented -> {tail(n[0]) if n else name + ' absent'}")
    return n[0] if n else None
def memory_peak():
    """The game's peak resident memory (VmHWM) so far, in GB; None once it has gone."""
    try:
        for l in open(f"/proc/{game.pid}/status"):
            if l.startswith("VmHWM:"): return int(l.split()[1]) / 1048576
    except OSError: return None
def finish():
    peak = memory_peak()
    if peak is not None: print(f"game memory peak: {peak:.2f} GB")
    try: send("quit")
    except Exception: pass
    time.sleep(2)
    if game.poll() is None: game.kill()
    after = save_sum(); print("save/ after: ", after, "(same)" if after == before else "(CHANGED)")
