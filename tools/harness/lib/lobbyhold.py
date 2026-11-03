#!/usr/bin/env python3
"""lobbyhold.py OUT BIN - ours on the desktop (Q920): the lobby's attract flight held by the console's `lobbyput` at an eye and look point (HOLD="ex ey ez lx ly lz", y-up, default desk3's C1 start), a real click on the player's slot by XTEST, the film (30 a second, ffmpeg x11grab) and `lobbyeye` rows from a poller thread for 4 s: OUT/pick.mkv, pick.t0, pick-click.t, pick-eye.txt (rows `pick <wall> eye (..) look (..) placed= orbit= time=`), shot c1-after-pick. Needs OPENTPW_GAME_PATH and DOTNET_ROOT; run with review-codex/venv/bin/python -u; BIN is the build's folder. The original's side is original/lobbypick.py; the two are compared by lib/pickcmp.py
"""
import os, sys, math, threading, subprocess
OUT = os.path.abspath(os.path.expanduser(sys.argv[1])); BIN = sys.argv[2]
NAME = "Q920"; AUDIO_FILE = os.path.join(OUT, "mix.f32")
os.makedirs(OUT, exist_ok=True)
sys.path.insert(0, "/home/alex/repos/OpenTPW/tools/harness/lib"); import paths
exec(open(paths.LIB + "/parkrun.py").read())

console = threading.Lock()
def ask1(cmd, pattern, to=1.5):
    with console:
        drain(); send(cmd); end = time.monotonic() + to
        while time.monotonic() < end:
            try: l = lines.get(timeout=0.02)
            except queue.Empty: continue
            if pattern in l: return l
    return None
def ctl(c):
    l = ask1("control " + c, "[dbg] control", 3.0); m = re.search(r"middle \((-?\d+),(-?\d+)\)", l or "")
    return (int(m.group(1)), int(m.group(2))) if m else None
rows = []
class Poller(threading.Thread):
    def __init__(self, tag, extra=()): super().__init__(daemon=True); self.tag = tag; self.on = True; self.extra = list(extra)
    def run(self):
        while self.on:
            l = ask1("lobbyeye", "[dbg]", 1.0)
            if l and "eye (" in l and self.on: rows.append((self.tag, time.time(), "eye", l[l.find("eye ("):].strip()))
            for c, pat in self.extra:
                l = ask1(c, pat, 1.0)
                if l and self.on: rows.append((self.tag, time.time(), c, l[l.find(pat.replace("[dbg] ", "")):].strip()))
            time.sleep(0.005)
    def stop(self): self.on = False; self.join(5)
def write(name, header=None):
    with open(os.path.join(OUT, name), "w") as f:
        if header: f.write(header + "\n")
        for tag, t, c, r in rows: f.write(f"{tag} {t:.4f} {c} {r}\n")
    rows.clear()
def click(what, held=0.06):
    xtest.fake_input(d, X.ButtonPress, 1); d.sync(); time.sleep(held); xtest.fake_input(d, X.ButtonRelease, 1); d.sync()
    print(f"  [XTEST button 1 held {held} s: {what}] at {time.time():.3f}")
_film = [None]
def film_start(name):
    at = win.translate_coords(root, 0, 0); w_, h_ = size()
    open(os.path.join(OUT, name + ".t0"), "w").write(f"{time.time():.4f}\n")
    _film[0] = subprocess.Popen(["ffmpeg", "-loglevel", "error", "-y", "-f", "x11grab", "-draw_mouse", "0", "-framerate", "30",
        "-video_size", f"{w_}x{h_}", "-i", f"{os.environ['DISPLAY']}+{-at.x},{-at.y}", "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18",
        "-pix_fmt", "yuv420p", os.path.join(OUT, name + ".mkv")], stdin=subprocess.DEVNULL)
    time.sleep(0.5)
def film_stop():
    p = _film[0]
    if p: p.send_signal(2); p.wait(20); _film[0] = None
EYE = re.compile(r"eye \(([-\d.]+), ([-\d.]+), ([-\d.]+)\) look \(([-\d.]+), ([-\d.]+), ([-\d.]+)\)")
def pitch(r):
    m = EYE.search(r)
    if not m: return None
    ex, ey, ez, lx, ly, lz = map(float, m.groups())
    return math.degrees(math.atan2(ey - ly, math.hypot(ex - lx, ez - lz)))
def last(tag, cmd):
    for t_, ti, c, r in reversed(rows):
        if t_ == tag and c == cmd: return ti, r
    return None
def snp(prefix, poller):
    """Escape (the lobby's menu), Select New Player, the box's tick: real clicks. Returns the wall clocks of the two clicks."""
    key("Escape", "the lobby menu"); time.sleep(0.8)
    p = ctl("c"); move(p[0], p[1], "Select New Player"); time.sleep(0.2); click("Select New Player"); time.sleep(1.0)
    p = ctl("9873c9"); move(p[0], p[1], "the box's tick"); time.sleep(0.2)
    poller.tag = prefix + "-tick"; tt = time.time(); click("the box's tick"); return tt
try:
    t_end = time.monotonic() + 90
    while True:
        try: attach(); break
        except Exception as e:
            if time.monotonic() > t_end: raise
            time.sleep(0.3)
    print("window", size())
    wait_for("Loaded the lobby", 150); time.sleep(4.0)
    slot = ctl("7a14"); print("slot", slot)
    move(slot[0], slot[1], "the player's slot")

    # ---------------- Q920: the flight held at a chosen state, then a real click on the slot
    HOLD = os.environ.get("HOLD", "479.466 86.613 363.595 575.835 15.470 392.691")
    send("lobbyput " + HOLD); time.sleep(1.0)
    film_start("pick"); P = Poller("flight", [("sound", "[dbg] mode=")]); P.start(); time.sleep(1.5)
    tc = time.time(); P.tag = "pick"; open(os.path.join(OUT, "pick-click.t"), "w").write(f"{tc:.4f}\n")
    click("the slot (pick out of the held flight)")
    time.sleep(4.0); P.stop(); film_stop(); write("pick-eye.txt", f"click-at {tc:.4f} hold {HOLD}")
    shot("c1-after-pick", 0.0)
finally:
    film_stop()
    if rows: write("rows-left.txt")
    done()
