#!/usr/bin/env python3
"""doorscene.py <outdir> <bin> [item x y angle qx1 qy1 qx2 qy2 [camera ...]]: a ride bought in the stock jungle park, opened and closed by its own
ride window's door (Q1104).

Default: Aztec Mayhem (1104, the jungle TV Sim) bought at (44,25) turned 180, so it covers (41..44, 22..25) and `buy` lays its one queue node
at (43,26); the queue tool then anchors at (43,27) and lays to (43,28), a cell of the path along y=28, which joins it. Each step:
  1 the buy (a bought ride is closed: `Object N: Closing...`, `back of queue is not connected`);
  2 the door clicked with the queue not joined (the door is not let open: ParkRideOperation.MayOpen);
  3 the queue joined (the remeasure opens the ride: `back of queue is connected`, `Object N: opened`) and the clock run until the ride's
    script has a child;
  4 the door clicked (closes it), the clock run until the script's line shows child 0;
  5 the door clicked again (opens it), the clock run until a child again;
then `savepark`, kept as <outdir>/doorscene-open.TPWS for the original (docs/TOOLING.md, "A bought ride's door in both games").
Each read is `scripts` (the count and the thing's lines), `scriptvar <thing> VAR_RIDECLOSED`, the thing's `objects` line, the save and load gaps
and `particles` (to <outdir>/particles-<tag>.txt; the lines whose x is in 400..459 printed); three frames a camera at 3, 4 and 5 with the clock
running and the HUD hidden. The door is the real click on b_door (0x3e38) of `openthing <thing>`, the window's okay after. A ride's own script is
the thing's line with parent 0. Run it with the venv's python; <bin> is the build's folder. Prints READ, DOOR and WAIT lines; confirm.txt and run.log
are in <outdir>."""
import os, sys, shutil, re as _re
OUT = sys.argv[1]; BIN = sys.argv[2]; NAME = "doorscene"
A = [int(v) for v in sys.argv[3:11]] if len(sys.argv) > 10 else [1104, 44, 25, 180, 43, 27, 43, 28]
ITEM, SX, SY, ANGLE, QX1, QY1, QX2, QY2 = A
CAMS = [(chr(ord("a") + i), c) for i, c in enumerate(sys.argv[11:] or ["440 240 75 0", "430 250 75 180"])]
L = os.path.dirname(os.path.abspath(__file__)) + "/"
sys.path.insert(0, L)
exec(open(L + "parkrun.py").read())
exec(open(L + "parkscene.py").read())
THING = [0]
def away():
    xtest.fake_input(d, X.MotionNotify, x=2550, y=1430); d.sync(); time.sleep(0.3)
def ride_line(sc): return next((l for l in sc if f"thing {THING[0]} " in l and " parent 0 " in l), "")
def read(tag):
    k = THING[0]; sc = reply("scripts", 2, 0, 600)
    open(os.path.join(OUT, f"scripts-{tag}.txt"), "w").write("\n".join(sc))
    print(f"READ {tag} {sc[0].strip()}")
    for l in sc:
        if f"thing {k} " in l: print(f"READ {tag}   {l.strip()[:200]}")
    print(f"READ {tag} {' '.join(reply(f'scriptvar {k} VAR_RIDECLOSED', 0.6, 0)[-1:]).strip()}")
    print(f"READ {tag} " + " ".join(l.strip() for l in reply("objects", 1.5, 0, 300) if f"thing  {k} " in l)[:220])
    print(f"READ {tag} gaps: {gaps(r'SAVE_PARK|LOADED_|OPEN_GUARD')}")
    pa = reply("particles", 1.5, 0, 400)
    open(os.path.join(OUT, f"particles-{tag}.txt"), "w").write("\n".join(pa))
    near = [l.strip() for l in pa if _re.search(r" at \(4[0-5]\d,", l)]
    print(f"READ {tag} particles: emitters at x 400..459: {len(near)}")
    for l in near[:8]: print(f"READ {tag}   {l[:160]}")
def until(pred, secs, what):
    t0 = time.time(); line = ""
    while time.time() - t0 < secs:
        line = ride_line(reply("scripts", 1, 0, 600))
        if pred(line): print(f"WAIT {what}: met after {time.time() - t0:.0f} s: {line.strip()[:200]}"); return True
        time.sleep(2)
    print(f"WAIT {what}: NOT met in {secs} s: {line.strip()[:200]}"); return False
def door(what):
    k = THING[0]; reply(f"openthing {k}", 0.8, 0); time.sleep(1.0)
    mark = len(logtext())
    x, y, w_, h_ = place("3e38"); move(x + w_ / 2, y + h_ / 2, f"the door: {what}"); button(1, what); time.sleep(0.8)
    shot(f"window-{what}", 0.3)
    x, y, w_, h_ = place("ffffffff"); move(x + w_ / 2, y + h_ / 2, "the window's okay"); button(1, "okay"); time.sleep(0.6)
    print(f"DOOR {what} logged: {_re.findall(rf'Object {k}:[^\n]*', logtext()[mark:])}")
def shots(tag):
    key("F2", "hide HUD"); away(); reply("resume", 0.3, 0)
    for c, cam in CAMS:
        reply(f"camera {cam}", 0.6, 0); time.sleep(1.2)
        for i in range(3): shot(f"{tag}-{c}{i}", 0.5); time.sleep(0.7)
    reply("pause", 0.3, 0); key("F2", "show HUD")
try:
    enter(); reply("pause", 0.3, 0)
    got = " ".join(reply(f"buy {ITEM} {SX} {SY} {ANGLE}", 1.5, 9)); print("BUY:", got[:200])
    THING[0] = int(_re.search(r"built as thing (\d+)", got).group(1))
    print("LOG after buy:", _re.findall(rf"Object {THING[0]}:[^\n]*", logtext()))
    read("1-bought")
    door("unjoined")
    read("2-unjoined-door")
    for c in (f"tool queue {THING[0]}", f"worldclick {QX1} {QY1}", f"worldclick {QX2} {QY2}", "tool off"): print("Q", c, "->", reply(c, 0.8, 0)[-1:])
    reply("resume", 0.3, 0)
    until(lambda l: _re.search(r" child [1-9]", l), 30, "child after the join")
    reply("pause", 0.3, 0); time.sleep(0.5)
    print("LOG after join:", _re.findall(rf"Object {THING[0]}:[^\n]*", logtext()))
    read("3-joined"); shots("3-joined")
    door("close")
    read("4-closed-at-click")
    reply("resume", 0.3, 0)
    until(lambda l: _re.search(r" child 0 ", l), 60, "no child after the close")
    reply("pause", 0.3, 0); time.sleep(0.5)
    read("5-closed"); shots("5-closed")
    door("open")
    read("6-opened-at-click")
    reply("resume", 0.3, 0)
    until(lambda l: _re.search(r" child [1-9]", l), 30, "child after the reopen")
    time.sleep(2); reply("pause", 0.3, 0); time.sleep(0.5)
    read("7-reopened"); shots("7-reopened")
    saved, said = save(); print("SAVED", said[-1:])
    shutil.copy(WROTE, os.path.join(OUT, "doorscene-open.TPWS"))
    print("gaps after save:", gaps(r"SAVE_PARK|LOADED_"))
finally:
    done()
