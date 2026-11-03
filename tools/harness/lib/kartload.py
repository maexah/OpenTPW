#!/usr/bin/env python3
"""kartload.py <outdir> <bin> [camera [steps [sweeps a step]]]: a Dino Karts (or, with KIND=water in the environment, a Splish Splash) bought and run in the stock jungle park, saved, and the saved file loaded (Q939).

The kart's twin of boughtload.py (Q929). Enters the private jungle folder's park, buys Dino Karts (item 1150) on (32,25), lays its loop by Q763's four clicks
and lays a path from the street's spur to (32,23) and joins its queue there, sends guests, runs on, then reads the census,
`pause`s, `savepark`s and Loads the saved file (the Load list's row 1), reads it again and steps the paused park. Each read is `scripts` (the count, the ride's line
and every thing-0 script), the first ride line of `bumpers`, the Engine lines of `voices`, the log's Car sounds lines and the save and load gaps; a frame follows
each. The saved file is copied to <outdir>/kartload-saved.TPWS (for `lib/scriptrecords.py`) and left in the private folder until done(), so the original can be shown
it (docs/TOOLING.md, "A bought kart or water ride saved and loaded, in both games"). The cars are not in `bumpers`, so the run waits RUNSECS (default 40) seconds, not for a ride "in a go". Run it
with the venv's python; `<bin>` is the build's folder. Prints "READ <tag> ..." lines; confirm.txt and run.log are in <outdir>."""
import os, sys, shutil, re as _re
OUT = sys.argv[1]; BIN = sys.argv[2]; WATER = os.environ.get("KIND") == "water"; NAME = "waterl" if WATER else "kartl"
# item, site, camera and the loop's four corners (Q858's confirm.py): the water ride is bought on (20,20) from camera 185 200 75 0 and takes no queue here
ITEM, SITE, VIEW, CORNERS = ((1160, (20, 20), "185 200 75 0", ((22, 19), (22, 15), (16, 15), (16, 19))) if WATER
                             else (1150, (32, 25), "310 260 75 0", ((34, 25), (34, 29), (28, 29), (28, 25))))
CAMERA = sys.argv[3] if len(sys.argv) > 3 else VIEW
STEPS = int(sys.argv[4]) if len(sys.argv) > 4 else 30; EVERY = int(sys.argv[5]) if len(sys.argv) > 5 else 48
L = os.path.dirname(os.path.abspath(__file__)) + "/"
sys.path.insert(0, L)
exec(open(L + "parkrun.py").read())
exec(open(L + "parkscene.py").read())
def karts(guests=5):
    """The ride bought on its site, its loop by Q763's clicks and, for the karts, the queue joined to the path before the exit and guests sent. Returns (thing, guests sent)."""
    reply(f"camera {VIEW}", 0.5, 0)
    reply(f"carry {ITEM}", 1.0, 0); reply(f"hover {SITE[0]} {SITE[1]}", 0.6, 0)
    r = " ".join(reply(f"worldclick {SITE[0]} {SITE[1]}", 1.5, 0)); reply("rightclick", 0.8, 0); k = int(_re.search(r"built as thing (\d+)", r).group(1)); print("  bought thing", k)
    for x, y in CORNERS: print("  worldclick", x, y, "->", reply(f"worldclick {x} {y}", 1.5, 0)[-1:])
    if WATER: return k, []
    # The stock park's street runs along y=21 from x=39 to 49 with a spur down x=39 to y=24 (q939/probe1.out): (32,23) is bare ground, so lay the path
    # from the spur's (39,23) west to it, then join the queue to that.
    for x in range(38, 31, -1): print("  path", x, 23, "->", reply(f"path {x} 23", 0.5, 0)[-1:])
    for c in (f"tool queue {k}", "worldclick 31 23", "worldclick 32 23", "tool off"): print("  ", c, "->", reply(c, 0.6, 0)[-1:])
    sent = []
    for i in range(guests):
        g = _re.findall(r"guest (\d+)", " ".join(reply("admit 43 21", 0.8, 0)))
        if g: sent.append(int(g[-1])); reply(f"send {g[-1]} {k}", 0.4, 0)
    return k, sent
def reads(tag, ride):
    sc = reply("scripts", 3, 0, 600)
    open(os.path.join(OUT, f"scripts-{tag}.txt"), "w").write("\n".join(sc))
    print(f"READ {tag} scripts: {sc[0]}")
    for l in sc:
        if f"thing {ride} " in l or "thing 0 " in l: print(f"READ {tag}   {l.strip()[:200]}")
    time.sleep(2.0)
    print(f"READ {tag} bumpers: {bumper_line()[:240] or '(no ride line)'}")
    v = [l for l in reply("voices", 1.5, 0) if "Engine" in l]
    print(f"READ {tag} Engine voices: {len(v)}")
    for l in v: print(f"READ {tag}   {l.strip()[:200]}")
    print(f"READ {tag} Car sounds lines in the log (all so far): {len(_re.findall(r'Car sounds: ride', logtext()))}")
    print(f"READ {tag} gaps: {gaps(r'SAVE_PARK|LOADED_|TRACK_CAP')}")
try:
    enter(); reply("pause", 0.3, 0)
    reads("0-entered", 43)   # 43 is the thing id the bought ride gets in the stock park, read before it is bought
    reply("resume", 0.3, 0)
    ride, sent = karts(); print("ride", ride, "guests", sent)
    # The karts' cars are not in `bumpers` (0 lines with the ride standing and guests queued: q939/run2), so there is no "in a go" to wait for: run on
    # for RUNSECS (default 40) so the ride's script and its companion have started, then read.
    time.sleep(float(os.environ.get("RUNSECS", "40")))
    reads("1-bought", ride)
    reply(f"camera {CAMERA}", 0.6, 0); time.sleep(1.0); shot("1-bought", 0.5)
    reply("pause", 0.3, 0)
    saved, said = save(); print("SAVED", said[-1:])
    print("gaps after save:", gaps(r"SAVE_PARK|LOADED_|TRACK_CAP"))
    shutil.copy(WROTE, os.path.join(OUT, "kartload-saved.TPWS"))
    got = load(1); print("load:", got)
    print("companions started, the log so far:", _re.findall(r"(\d+) companions started from the save", logtext()))
    reads("2-loaded", ride)
    reply(f"camera {CAMERA}", 0.6, 0); time.sleep(1.0); shot("2-loaded", 0.5)
    for i in range(STEPS):
        sc = reply("scripts", 1.0, 0, 600)
        w = [_re.search(r" at (\d+) ", l).group(1) for l in sc if f"thing {ride} " in l]
        b = bumper_line(); v = len([l for l in reply("voices", 0.6, 0) if "Engine" in l])
        print(f"STEP +{i * EVERY:4d} sweeps tick {tick()} word {w} state {field(b, 'state')} seated {field(b, 'seated')} Engine voices {v}")
        if i == STEPS - 1: shot("3-stepped", 0.5)
        reply(f"step {EVERY}", 0.5, 0)
finally:
    done()
