#!/usr/bin/env python3
"""boughtload.py <outdir> <bin> [camera [steps [sweeps a step]]]: a Hot Pot bought and run in the stock jungle park, saved, and the saved file loaded (Q929).

Enters the private jungle folder's park, `parkscene.hot_pot()` (item 1140 on (41,23), its queue joined by clicks, five guests sent), waits until the
pot is in a go (`bumpers` Running with somebody seated) and a few seconds more, reads the census, `pause`s, `savepark`s and Loads the saved file
(the Load list's row 1, the newest), reads it again, then steps the paused park `steps` times (default 30) of `sweeps a step` (default 48; about 15 sweeps are a game tick), printing "STEP" lines (the 3-stepped frame is taken at the last step). A pot not in a go in 120 s stops the run as "VOID" (exit status 3), nothing saved. Each read is `scripts` (the count and every line of the pot and of every thing-0 script),
the first ride line of `bumpers`, the Engine lines of `voices`, the log's Car sounds lines and the save and load gaps; a frame follows each (`camera` is the
console's, default "435 250 60 180", the pot's site). The saved file is copied to <outdir>/boughtload-saved.TPWS for `lib/scriptrecords.py`, and is
left in the private folder until done() so the original can be shown it (docs/TOOLING.md, "A bought Hot Pot saved and loaded, in both games"). Run it with the venv's python;
`<bin>` is the build's folder. Prints "READ <tag> ..." lines; confirm.txt and run.log are in <outdir>."""
import os, sys, shutil, re as _re
OUT = sys.argv[1]; BIN = sys.argv[2]; NAME = "boughtl"
CAMERA = sys.argv[3] if len(sys.argv) > 3 else "435 250 60 180"
STEPS = int(sys.argv[4]) if len(sys.argv) > 4 else 30; EVERY = int(sys.argv[5]) if len(sys.argv) > 5 else 48
L = os.path.dirname(os.path.abspath(__file__)) + "/"
sys.path.insert(0, L)
exec(open(L + "parkrun.py").read())
exec(open(L + "parkscene.py").read())
def reads(tag, pot):
    sc = reply("scripts", 3, 0, 600)
    open(os.path.join(OUT, f"scripts-{tag}.txt"), "w").write("\n".join(sc))
    print(f"READ {tag} scripts: {sc[0]}")
    for l in sc:
        if f"thing {pot} " in l or "thing 0 " in l: print(f"READ {tag}   {l.strip()[:200]}")
    time.sleep(2.0)
    print(f"READ {tag} bumpers: {bumper_line()[:240] or '(no ride line)'}")
    v = [l for l in reply("voices", 1.5, 0) if "Engine" in l]
    print(f"READ {tag} Engine voices: {len(v)}")
    for l in v: print(f"READ {tag}   {l.strip()[:200]}")
    print(f"READ {tag} Car sounds lines in the log (all so far): {len(_re.findall(r'Car sounds: ride', logtext()))}")
    print(f"READ {tag} gaps: {gaps(r'SAVE_PARK|LOADED_|TRACK_CAP')}")
try:
    enter(); reply("pause", 0.3, 0)
    reads("0-entered", 43)   # 43 is the thing id the bought pot gets in the stock park, read before it is bought
    reply("resume", 0.3, 0)
    pot, sent = hot_pot(); print("pot", pot, "guests", sent)
    go = in_a_go(120)
    if not _re.search(r"state Running .*seated [1-9]", go):
        print("VOID: the pot was not in a go (state Running, somebody seated) in 120 s; nothing saved or loaded"); reply("pause", 0.3, 0); sys.exit(3)
    time.sleep(6.0)
    reads("1-bought", pot)
    reply(f"camera {CAMERA}", 0.6, 0); time.sleep(1.0); shot("1-bought", 0.5)
    reply("pause", 0.3, 0)
    saved, said = save(); print("SAVED", said[-1:])
    print("gaps after save:", gaps(r"SAVE_PARK|LOADED_|TRACK_CAP"))
    shutil.copy(WROTE, os.path.join(OUT, "boughtload-saved.TPWS"))
    got = load(1); print("load:", got)
    print("companions started, the log so far:", _re.findall(r"(\d+) companions started from the save", logtext()))
    reads("2-loaded", pot)
    reply(f"camera {CAMERA}", 0.6, 0); time.sleep(1.0); shot("2-loaded", 0.5)
    # The loaded park is paused at the file's tick: step it, so the game's own clock (not the wall clock's) says when the engine starts,
    # when the saved round ends and when the next begins (the original's mixfreq.py stamps its channels with the same mGameTick).
    for i in range(STEPS):
        sc = reply("scripts", 1.0, 0, 600)
        w = [_re.search(r" at (\d+) ", l).group(1) for l in sc if f"thing {pot} " in l]
        b = bumper_line(); v = len([l for l in reply("voices", 0.6, 0) if "Engine" in l])
        print(f"STEP +{i * EVERY:4d} sweeps tick {tick()} word {w} state {field(b, 'state')} seated {field(b, 'seated')} Engine voices {v}")
        if i == STEPS - 1: shot("3-stepped", 0.5)
        reply(f"step {EVERY}", 0.5, 0)
finally:
    done()
