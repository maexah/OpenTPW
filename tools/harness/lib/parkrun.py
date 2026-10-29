"""parkrun.py - what every save-and-load confirm run shares, so a run's own script is only its scene and its checks.

exec() it after setting OUT (the run's folder), BIN (the build's bin folder) and NAME (the park file's name, "Q255").
Optional before the exec: GAME_FOLDER = "<path>" (a private game folder of the script's own, used on the desktop or off it: lib/gamefolder.py
makes one whose data differs in the files named), AUDIO_FILE = "<path>" (the mix to a file; lib/mixmatch.py measures a sample in it), OFFSCREEN = True, SLOT_CONTROL = "7a14" (player slot 1).

It starts the game in a PRIVATE game folder (links to the real one, a save/ of its own) through q119/lib.py, so every
name of that library is here too (send, ask, collect, drain, tail, wait_for, attach, move, button, key, shot, grab,
predict, finish). NEVER name a variable of your own `before`, `lines`, `log`, `game`, `d`, `win` or `root`.
The library's other names are here as well and are as unsafe to reuse: `size`, `count`, `tail`, `env`, `GAME`, `print`
(which also appends each line to OUT/confirm.txt), and the modules `re`, `time`, `os`, `sys`, `queue` (a `def queue` breaks every click: Q580), `threading`, `subprocess`.
The private folder is q241f/game itself (with OFFSCREEN, lib/game-off, made from it on first use). The exec stops on an
assert if its jungle folder already holds NAME.TPWS. OUT receives confirm.txt, run.log and the shots.

    said(ok, what)            one prediction's result, counted in `score`
    PRIVATE, JUNGLE, WROTE    the private game folder, its save/users/1ref/jungle, and NAME.TPWS there
    REAL0, score              the real save/'s sum at the start, and [as predicted, said]; done() reads these: do not reuse the names
    logtext()                 the whole run.log so far
    reply(cmd, secs, show)    a console command's [dbg] lines
    place(control)            a control's (x, y, w, h) in window pixels
    tick()                    mGameTick, from `sweeps`
    gaps(pattern)             the `unimplemented` lines matching a pattern
    enter(theme="jungle")     the lobby, a real click on the player's slot, `park <theme>`
    save()                    `savepark NAME`; answers (pk.Park of the file, the save's last log lines); copies it to OUT
    load(row=1)               the menu's Load row and the list's row by real clicks, then `pause`;
                              answers (the path read, the clock's first tick, where the log stood at the click)
    done()                    in `finally`: prints SCORE, quits (kills a game that stays), removes the file, stops the private display,
                              lists the private jungle folder, prints the real save/'s sum beside the one taken at the start
    original_view(x, y)       the `camera` command whose SAVED view shows world point (x, y) mid-frame in the ORIGINAL
                              (cell x 10; send it, `step 2`, then take the census again and save)

OFFSCREEN = True runs on a private Xvfb (:78, or OFFSCREEN_DISPLAY) drawn by lavapipe, beside a run on the desktop, in a second private
game folder. It keeps time and takes real clicks, and lavapipe draws the textures as the desktop's GPU does, only
slower; a change to what is drawn is still photographed on the desktop (AGENTS.md rule 6). The run says so once at its
start; shot() does not repeat it."""
import os, shutil, subprocess as _sub, sys, time as _time
import paths   # the calling script put tools/harness/lib on sys.path (README.md, "Writing a run")
import pk

OUT = os.path.expanduser(OUT)
os.environ.setdefault("OPENTPW_LOG_DIR", OUT)   # the game's console.log pair goes in the run's folder, never in the game folder (Q647)
_OFF = bool(globals().get("OFFSCREEN", False))
_DISP = globals().get("OFFSCREEN_DISPLAY", ":78")   # lane B of a two-lane run takes ":79" (handoff/README.md, "Two lanes")
_TEMPLATE = paths.PRIVATE_GAME
assert os.path.isdir(_TEMPLATE), f"no private game folder at {_TEMPLATE}: make it once with privategame.py (README.md)"
PRIVATE = paths.PRIVATE_GAME_OFF if _OFF else _TEMPLATE
_OWN = globals().get("GAME_FOLDER")
if _OWN: PRIVATE = os.path.expanduser(_OWN)
if _OFF and not _OWN and not os.path.isdir(PRIVATE):
    os.makedirs(PRIVATE)
    for entry in os.listdir(_TEMPLATE):
        src = os.path.join(_TEMPLATE, entry)
        if entry == "save": shutil.copytree(src, os.path.join(PRIVATE, entry))
        else: os.symlink(os.readlink(src), os.path.join(PRIVATE, entry))
JUNGLE = os.path.join(PRIVATE, "save/users/1ref/jungle"); WROTE = os.path.join(JUNGLE, NAME + ".TPWS")
assert not os.path.exists(WROTE), f"remove the last run's file first: {WROTE}"
_REAL = f"find '{paths.game()}/save' -type f -exec md5sum {{}} \\; | sort | md5sum"
REAL0 = _sub.run(_REAL, shell=True, capture_output=True, text=True).stdout.strip()

_xvfb = None
if _OFF:
    _xvfb = _sub.Popen([paths.xvfb(), _DISP, "-screen", "0", "1600x900x24", "-nolisten", "tcp", "-noreset"],
                       stdout=_sub.DEVNULL, stderr=_sub.DEVNULL)
    _time.sleep(2)
    os.environ.update(DISPLAY=_DISP, SDL_VIDEODRIVER="x11", VK_DRIVER_FILES="/usr/share/vulkan/icd.d/lvp_icd.json",
                      VK_ICD_FILENAMES="/usr/share/vulkan/icd.d/lvp_icd.json")
    os.environ.pop("WAYLAND_DISPLAY", None)

_LIB = open(os.path.join(paths.LIB, "gamedrive.py")).read()
if globals().get("AUDIO_FILE"):   # the game's own mix written to a file (f32le stereo 22050), nothing to the speakers
    assert 'SDL_AUDIODRIVER="dummy"' in _LIB; AUDIO_FILE = os.path.abspath(os.path.expanduser(AUDIO_FILE))
    _LIB = _LIB.replace('SDL_AUDIODRIVER="dummy"', f'SDL_AUDIODRIVER="disk", SDL_DISKAUDIOFILE="{AUDIO_FILE}"')
exec(_LIB)
if _OFF:
    print(f"OFF-SCREEN on {_DISP} (lavapipe): photograph a change to what is drawn on the desktop, not here")

score = [0, 0]
def said(ok, what):
    score[0] += bool(ok); score[1] += 1; print(f"  RESULT {'as predicted' if ok else 'NOT as predicted'}: {what}")
def logtext(): log.flush(); return open(os.path.join(OUT, "run.log"), errors="replace").read()
def reply(cmd, secs=1.2, show=3):
    drain(); send(cmd); got = [tail(l) for l in collect(secs) if "[dbg]" in l]
    for g in got[:show]: print(f"  {cmd:<14} -> {g[:300]}")
    return got
def place(cid):
    got = reply(f"control {cid}")
    m = re.search(r"at \((-?\d+),(-?\d+)\) size \((\d+),(\d+)\)", got[0]); return [int(v) for v in m.groups()]
def tick():
    # patient (Q141): off-screen, a big park's load (Alexah's Full Simulation jungle, 13 s) outlasts one half-second census
    for _ in range(60):
        drain(); send("sweeps"); got = [tail(l) for l in collect(0.5) if "sweeps:" in l and "mGameTick" in l]
        if got: return int(re.search(r"mGameTick (\d+)", got[0]).group(1))
    raise SystemExit("tick(): no sweeps reply in 30 s")
def gaps(pattern): return [g for g in reply("unimplemented", 1.5, 0) if re.search(pattern, g)]

def enter(theme="jungle"):
    wait_for("Loaded the lobby", 150); attach(); time.sleep(3.0)
    l = ask("control " + globals().get("SLOT_CONTROL", "7a14"), "control"); m = re.search(r"middle \((-?\d+),(-?\d+)\)", l)
    move(int(m.group(1)), int(m.group(2)), "the player's slot"); button(1, "the slot"); time.sleep(1.0)
    send(f"park {theme}"); wait_for(f"Loaded {theme}", 180); time.sleep(2.0)

def save():
    reply(f"savepark {NAME}", 2.5)
    said_ = [l.strip() for l in re.findall(r"Save: .*", logtext())[-6:]]
    for l in said_: print("  log:", l)
    shutil.copy(WROTE, os.path.join(OUT, NAME + ".TPWS"))
    return pk.Park(WROTE), said_

def load(row=1):
    reply("menu"); time.sleep(0.6)
    x, y, w_, h = place("1"); move(x + w_ / 2, y + h / 2, "the menu's Load row"); button(1, "Load"); time.sleep(0.8)
    lp = re.findall(r"Load Park: \d+ saved parks.*", logtext()); print("  log: " + (lp[-1].strip() if lp else "(none)"))
    rh = int(re.search(r"rows of (\d+) units", lp[-1]).group(1)) if lp else 39
    x, y, w_, h = place("23bce0"); px, py = x + w_ * 0.25, y + h * ((229 - 203) + (row - 0.5) * rh) / (772 - 203)
    move(px, py, f"row {row}"); mark = len(logtext())
    drain(); xtest.fake_input(d, X.ButtonPress, 1); d.sync(); time.sleep(0.06)
    xtest.fake_input(d, X.ButtonRelease, 1); d.sync(); print(f"  [XTEST button 1: row {row}]")
    wait_for("Loaded jungle", 180); time.sleep(0.3); ask("pause", "pause")
    txt = logtext(); read = re.findall(r"jungle: reading the park from (.*)", txt)[-1].strip().replace("\\", "/")
    start = int(re.findall(r"clock starts at mGameTick (\d+)", txt)[-1])
    print(f"  log: reading the park from {read}; clock starts at mGameTick {start}; now {tick()}")
    return read, start, mark

def original_view(x, y):
    """The `camera` command whose saved view shows world point (x, y) mid-frame in the original after its Load Park
    list, whose view is 65 units on from the file's camera (Q359; the eye at the same numbers is the same, Q435, and
    the picture is narrower, Q260): y is moved 65 on, at zoom 110 and yaw 180. The 65 holds for
    that zoom and yaw alone."""
    return f"camera {x} {y + 65} 110 180"

def done():
    print(f"SCORE {score[0]} of {score[1]}")
    finish()
    if os.path.exists(WROTE): os.remove(WROTE)
    if _xvfb: _xvfb.terminate()
    print("private jungle folder:", sorted(os.listdir(JUNGLE)))
    print("real save/:", _sub.run(_REAL, shell=True, capture_output=True, text=True).stdout.strip(), "(started", REAL0, ")")
