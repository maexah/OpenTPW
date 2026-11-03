#!/usr/bin/env python3
"""closeload.py <outdir> <bin> <label> <park file> <thing> [camera]: a ride CLOSED after a load and saved (Q927).

Copies <park file> (a file that holds an OPEN ride with its companion child: a ride bought in a park, saved by this game) into the private jungle folder as a Full
Simulation player's, Loads it (the load starts the companion from the file), writes the ride script's `VAR_RIDECLOSED` to 1 (what the ride window's door writes through
`ParkRideOperation.Close`; a bought ride's door cannot open it until its queue is joined, so a scene writes the variable), runs until no script has a parent but 0, `savepark`s,
and Loads the saved file. Each read is `scripts` (the count and every line of <thing>), the gaps of the save and load; a frame follows the first load and the last (`camera` is
the console's, default "220 640 60 0", the site of `buy <id> 20 60`), and the saved file is copied to <outdir>/<label>-saved1.TPWS for `scriptrecords.py`. The same file given to a
binary that has the bug and one that has not shows it: the Q910 tip loads the closed ride's child again (`SAVE_PARK_SCRIPT_ENDED`, smoke on the closed Volcano), the fixed one does not.
Run it with the venv's python (docs/TOOLING.md); `<bin>` is the build's folder. Prints "R <tag> ..." lines; confirm.txt and run.log are in <outdir>."""
import os, sys, shutil, re as _re
OUT = sys.argv[1]; BIN = sys.argv[2]; LABEL = sys.argv[3]; SRC = os.path.expanduser(sys.argv[4]); THING = int(sys.argv[5]); NAME = "closeload"
CAMERA = sys.argv[6] if len(sys.argv) > 6 else "220 640 60 0"
L = os.path.dirname(os.path.abspath(__file__)) + "/"
sys.path.insert(0, L)
exec(open(L + "parkrun.py").read())
exec(open(L + "fsplayer.py").read())
def away():
    xtest.fake_input(d, X.MotionNotify, x=2550, y=1430); d.sync(); time.sleep(0.3)
def reads(tag):
    sc = reply("scripts", 2, 0, 600)
    open(os.path.join(OUT, f"scripts-{tag}.txt"), "w").write("\n".join(sc))
    mine = [l.strip() for l in sc if re.search(rf"thing {THING} ", l)]
    print(f"R {tag}: {sc[0].strip()}")
    for l in mine: print(f"R {tag}   {l[:230]}")
    print(f"R {tag} gaps: {gaps(r'SAVE_PARK|LOADED_|TRACK_CAP')}")
    return sc, mine
def wait_until(cond, secs, tag):
    t0 = time.time()
    while time.time() - t0 < secs:
        sc, mine = reads(f"{tag}-{int(time.time()-t0)}s")
        if cond(sc, mine): return sc, mine
        time.sleep(2.5)
    return None, None
def companions(): return _re.findall(r"[^\n]*companions started[^\n]*", logtext())[-1:]
def view(tag):
    reply(f"camera {CAMERA}", 0.6, 0); reply("resume", 0.3, 0); time.sleep(5); reply("pause", 0.3, 0)
    key("F2", "hide HUD"); away(); shot(f"{LABEL}-{tag}", 0.5); key("F2", "show HUD")
own_file(os.path.join(JUNGLE, "autosave.TPWS"))
try:
    fs_on()
    enter(); reply("pause", 0.3, 0)
    shutil.copy(SRC, WROTE); os.chmod(WROTE, 0o644); os.utime(WROTE, None)
    print("load 1:", load(1)); print("LOG", companions())
    reads("1-loaded"); view("1-loaded")
    reply("resume", 0.3, 0)
    print("poke close", reply(f"scriptvar {THING} VAR_RIDECLOSED 1", 0.5, 3))
    got = wait_until(lambda sc, m: any(re.search(rf"thing {THING} ", l) for l in m) and not any(re.search(r"parent [1-9]", l) for l in m), 90, "2-closed")
    print("CLOSED seen:", got[1]); reply("pause", 0.3, 0)
    reads("2-closed"); key("F2", "hide HUD"); away(); shot(f"{LABEL}-2-closed", 0.5); key("F2", "show HUD")
    saved, said = save(); print("SAVED1", said[-1:])
    print("gaps after save 1:", gaps(r"SAVE_PARK|LOADED_|TRACK_CAP"))
    shutil.copy(WROTE, os.path.join(OUT, f"{LABEL}-saved1.TPWS"))
    print("load 2:", load(1)); print("LOG", companions())
    reads("3-reloaded"); view("3-reloaded")
    print("gaps end:", gaps(r"SAVE_PARK|LOADED_|TRACK_CAP"))
finally:
    done()
    fs_off()
