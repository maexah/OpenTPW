#!/usr/bin/env python3
"""upgradesrun.py <outdir> <bin> <game> ia|fs <park file|-> <thing> [step...]: a ride window's Buy Upgrades list (Q512) driven
off-screen in a cloud lane, through consoledrive.py (no python-xlib). `ia` plays with nobody (Instant Action); `fs` makes a Full
Simulation player 'Tester' by real clicks and typing (TOOLING.md, the Q1154 recipe) and puts <park file> in its jungle folder as
`Mine.TPWS`, so `park jungle` loads it. The park is entered paused; `money` is asked until the park answers it (at most 180 s), then `upgrades <thing>` is read. Steps, in order:
  cmd:<console command>  sent as it is, its reply printed
  thing:<id>             the thing the next steps are about
  open                   `openthing <thing>`, then the console's `click` on the ride window's upgrades button (`control 3e35`)
  row:<n>                a real pointer move and click (xdotool) on slot <n> of the list (`control 1f4d`; the row height from the
                         window's `Upgrades list:` log line), so the list's own pointer selection is used
Ends with `hand` and `unimplemented`, and the game is stopped even when a step fails. Everything is in <outdir>/stdout.txt; the steps and replies on stdout. <bin> is the build
folder. A park file to load in a state no screen reaches is made with parkfile.thing_set (a ride's `mState` 200 '<i', its
`mUpgradeLevel` 1098 '<B')."""
import os, re, shutil, subprocess, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import consoledrive

out, binp, game, mode, park, thing = sys.argv[1:7]; steps = sys.argv[7:]
d = consoledrive.Drive(out, binp, game, lobby_secs=30)

def say(command, until=".", secs=6):
    got = d.ask(command, until, secs); print(f">>> {command}", flush=True)
    for line in got: print("   ", line, flush=True)
    return got

def xdo(*args):
    own = d._own("xdotool " + " ".join(args))
    if own is None: return ""
    return subprocess.run(["xdotool", *args], env=dict(os.environ, **own), capture_output=True, text=True).stdout

def window():
    return (xdo("search", "--onlyvisible", "--name", ".").split() or [None])[0]

def placed(control):
    for line in say(f"control {control}", r"control"):
        m = re.search(r"at \((\d+),(\d+)\) size \((\d+),(\d+)\) middle \((\d+),(\d+)\)", line)
        if m: return [int(v) for v in m.groups()]
    return None

def run():
    global thing
    if mode == "fs":
        say("click 1007 56", "click"); time.sleep(2)     # the lobby's first slot, 7a14
        say("click 622 250", "click"); time.sleep(2)     # Full Simulation, 70e
        w = window(); xdo("windowfocus", "--sync", w); xdo("type", "--delay", "150", "Tester"); time.sleep(0.5)
        xdo("key", "--delay", "200", "Return"); time.sleep(3)
        jungle = os.path.join(out, "game", "save", "users", "1Tester", "jungle"); os.makedirs(jungle, exist_ok=True)
        shutil.copy(park, os.path.join(jungle, "Mine.TPWS"))

    say("park jungle", "park", 2); say("pause", "pause", 2)
    # The park answers `money` once it is loaded (45 s alone; longer on a busy machine), at most 180 s.
    end = time.time() + 180
    while time.time() < end and not any("money balance" in line for line in say("money", "money", 5)):
        time.sleep(5)
    say(f"upgrades {thing}", "upgrades")

    for step in steps:
        if step.startswith("cmd:"):
            say(step[4:], ".", 4)
        elif step.startswith("thing:"):
            thing = step[6:]
        elif step == "open":
            say(f"openthing {thing}", "openthing"); time.sleep(2)
            button = placed("3e35")
            if button: say(f"click {button[4]} {button[5]}", "click"); time.sleep(2)
        elif step.startswith("row:"):
            listed = placed("1f4d")
            logged = re.findall(r"Upgrades list: .*rows of (\d+) units", open(os.path.join(out, "stdout.txt")).read())
            if not listed or not logged: print("    no list to click", flush=True); continue
            x, y, wide, high = listed[:4]; row = int(logged[-1]); n = int(step[4:])
            # The window's list rectangle is 440 units high (ParkUpgradesWindow); its rows start at its top.
            px, py = x + wide // 3, y + int((n * row + row / 2) * high / 440.0)
            w = window(); xdo("mousemove", "--window", w, str(px), str(py)); time.sleep(0.7); xdo("click", "1"); time.sleep(2)
            print(f">>> real click on row {n} at ({px},{py})", flush=True)
            for line in d.drain(): print("   ", line, flush=True)
    say("hand", "hand"); say("unimplemented", r"unimplemented", 4)
    for line in d.collect(2): print("   ", line, flush=True)

try:
    run()
finally:
    d.quit()
