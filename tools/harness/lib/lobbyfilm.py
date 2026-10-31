#!/usr/bin/env python3
"""lobbyfilm.py <outdir> <bin-folder> (Q749 fix): ours' lobby eye and look point, polled and filmed at 30 frames a second, for the two ways
the island camera first runs - a player chosen out of the attract flight by a real click on the slot (`pick-*`), and a return by the
park menu's Exit To Lobby (`quit-*`). Off-screen on :79, lavapipe draws the textures (set OFFSCREEN_DISPLAY / GAME_FOLDER as lane B does).
One launch: the lobby flies 4 s unfilmed, the film starts, the flight is polled 3 s, the slot is clicked (the wall clock of the press in
pick-click.t), 4 s more are polled and filmed. Then `park jungle`, 15 s in, Escape, the menu's Exit To Lobby row `6`, filmed from the click
until 8 s after the lobby answers, polled the same way. Rows: pick-eye.txt and quit-eye.txt, `tag wall eye (x, y, z) look (x, y, z) ...` in the
original's y-up axes as `lobbyeye`/`original/rendereye.py` print them; films pick.mkv and quit.mkv with their start wall clocks in
pick.t0 and quit.t0 (good to about 0.2 s); firstpass.txt holds the log's line for the lobby's first pass after each build.
`lobbyeyecmp.py` and `lobbysheet.py` read the folder. Run with review-codex/venv/bin/python -u; parkrun.py's names are in scope."""
import os, sys, subprocess, threading
OUT = os.path.abspath(os.path.expanduser(sys.argv[1])); BIN = sys.argv[2]
NAME = "LOBBYFILM"; OFFSCREEN = True; OFFSCREEN_DISPLAY = os.environ.get("OPENTPW_OFFSCREEN_DISPLAY", ":79")
if os.environ.get("OPENTPW_GAME_FOLDER"): GAME_FOLDER = os.environ["OPENTPW_GAME_FOLDER"]   # lane B: ~/.cache/tpw-harnesses/lib/game-off-B
os.makedirs(OUT, exist_ok=True)
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
exec(open(paths.LIB + "/parkrun.py").read())

def ask1(cmd, pattern, to=1.5):
    drain(); send(cmd); end = time.monotonic() + to
    while time.monotonic() < end:
        try: l = lines.get(timeout=0.05)
        except queue.Empty: continue
        if pattern in l: return l
    return None
rows = []
class Poller(threading.Thread):
    """Asks `lobbyeye` as fast as the console answers, on a thread of its own: a real click's button() holds the main thread for over a second,
    and the first pass after a click is exactly what it must not miss. `tag` is read at each answer; the main thread owns the console otherwise."""
    def __init__(self, tag): super().__init__(daemon=True); self.tag = tag; self.on = True
    def run(self):
        while self.on:
            tag = self.tag; l = ask1("lobbyeye", "[dbg]", 1.0)
            if l and "eye (" in l and self.on: rows.append((tag() if callable(tag) else tag, time.time(), l[l.find("eye ("):].strip()))
            time.sleep(0.01)
    def stop(self): self.on = False; self.join(5)
def poll(tag, secs):
    p = Poller(tag); p.start(); time.sleep(secs); p.stop()
def write(name, header=None):
    with open(os.path.join(OUT, name), "w") as f:
        if header: f.write(header + "\n")
        for tag, t, r in rows: f.write(f"{tag} {t:.4f} {r}\n")
    rows.clear()
_film = [None]
def click(what, held=0.06):
    """A real left click through XTEST that leaves the console's lines alone: gamedrive's button() drains the queue and collects from it for
    1.5 s, which would eat the poller's answers for exactly the seconds this script is here to read."""
    xtest.fake_input(d, X.ButtonPress, 1); d.sync(); time.sleep(held); xtest.fake_input(d, X.ButtonRelease, 1); d.sync()
    print(f"  [XTEST button 1 held {held} s: {what}]")
def film_start(name):
    at = win.translate_coords(root, 0, 0); w_, h_ = size()
    env = dict(os.environ, DISPLAY=OFFSCREEN_DISPLAY)
    open(os.path.join(OUT, name + ".t0"), "w").write(f"{time.time():.4f}\n")
    _film[0] = subprocess.Popen(["ffmpeg", "-loglevel", "error", "-y", "-f", "x11grab", "-draw_mouse", "0", "-framerate", "30",
        "-video_size", f"{w_}x{h_}", "-i", f"{OFFSCREEN_DISPLAY}+{-at.x},{-at.y}", "-c:v", "libx264", "-preset", "ultrafast", "-crf", "18",
        "-pix_fmt", "yuv420p", os.path.join(OUT, name + ".mkv")], env=env, stdin=subprocess.DEVNULL)
    time.sleep(0.5)
def film_stop():
    p = _film[0]
    if p: p.send_signal(2); p.wait(20); _film[0] = None
def firstpass_lines(): return [l.strip() for l in logtext().splitlines() if "the first pass after a build" in l]
try:
    wait_for("Loaded the lobby", 150); attach(); time.sleep(4.0)
    l = ask("control 7a14", "control"); m = re.search(r"middle \((-?\d+),(-?\d+)\)", l)
    film_start("pick"); pk_ = Poller("flight"); pk_.start(); time.sleep(3.0)
    move(int(m.group(1)), int(m.group(2)), "the player's slot")
    time.sleep(1.0)
    tc = time.time(); pk_.tag = "pick"; open(os.path.join(OUT, "pick-click.t"), "w").write(f"{tc:.4f}\n"); click("the slot")
    time.sleep(4.0); pk_.stop(); film_stop(); write("pick-eye.txt", f"click-at {tc:.4f}")
    send("park jungle"); wait_for("Loaded jungle", 180); time.sleep(15.0)
    own_file(os.path.join(JUNGLE, "autosave.TPWS"))
    key("Escape", "the park menu"); time.sleep(0.8)
    x_, y_, w_, h_ = place("6"); move(x_ + w_ / 2, y_ + h_ / 2, "the menu's Exit To Lobby row")
    film_start("quit"); n0 = len(re.findall(r"Loaded the lobby", logtext())); answered = None; t_start = time.monotonic()   # counted BEFORE the click: button() holds the thread for over a second and the lobby is back in one
    pq = Poller(lambda: "post" if answered else "park"); pq.start()
    tq = time.time(); open(os.path.join(OUT, "quit-click.t"), "w").write(f"{tq:.4f}\n"); click("Exit To Lobby")
    while time.monotonic() - t_start < 120:
        if answered is None and len(re.findall(r"Loaded the lobby", logtext())) > n0: answered = time.time()
        if answered and time.time() - answered > 8.0: break
        time.sleep(0.2)
    pq.stop()
    film_stop(); write("quit-eye.txt", f"click-at {tq:.4f} lobby-answered {answered}")
    shot("lobby-after", 0.0)
    open(os.path.join(OUT, "firstpass.txt"), "w").write("\n".join(firstpass_lines()) + "\n")
    print("first-pass log lines:", *firstpass_lines(), sep="\n  ")
finally:
    film_stop()
    done()
