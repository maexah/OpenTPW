#!/usr/bin/env python3
"""sellload.py <outdir> <bin> <park file> <thing> [camera]: a park file loaded, one thing sold, the park saved, the saved file loaded (Q926).

Copies <park file> into the private jungle folder, Loads it (the Load list's row 1), reads the census, `sell <thing>`, reads it again,
`savepark`, Loads the saved file (the list's row 2, the newer name), reads it a third time. Each read is `scripts` (the count, the lines
of <thing> and of every thing-0 script, a SPAWNSOUND child's), the first ride line of `bumpers`, the Engine voices of `voices`, the log's
Car sounds lines and the `unimplemented` lines of the save and load gaps; a frame follows each (`camera` is the console's, default
"435 250 60 180", a Hot Pot's site). The saved file is copied to <outdir>/sellload-saved.TPWS for a script-record reader (`q241i/rsse.py`). Run it with the
venv's python (docs/TOOLING.md); `<bin>` is the build's folder. Prints "READ <tag> ..." lines; confirm.txt and run.log are in <outdir>."""
import os, sys, shutil, re as _re
OUT = sys.argv[1]; BIN = sys.argv[2]; SRC = os.path.expanduser(sys.argv[3]); THING = int(sys.argv[4]); NAME = "sellh"
CAMERA = sys.argv[5] if len(sys.argv) > 5 else "435 250 60 180"
L = os.path.dirname(os.path.abspath(__file__)) + "/"
sys.path.insert(0, L)
exec(open(L + "parkrun.py").read())
SAVED = own_file(os.path.join(JUNGLE, "sells.TPWS"))
def reads(tag):
    sc = reply("scripts", 3, 0, 600)
    open(os.path.join(OUT, f"scripts-{tag}.txt"), "w").write("\n".join(sc))
    print(f"READ {tag} scripts: {sc[0]}")
    for l in sc:
        if f"thing {THING} " in l or "thing 0 " in l: print(f"READ {tag}   {l.strip()[:200]}")
    time.sleep(2.0)
    print(f"READ {tag} bumpers: {bumper_line()[:240] or '(no ride line)'}")
    v = [l for l in reply("voices", 1.5, 0, 400) if "Engine" in l]
    print(f"READ {tag} Engine voices: {len(v)}")
    print(f"READ {tag} Car sounds lines in the log (all so far): {len(_re.findall(r'Car sounds: ride', logtext()))}")
    print(f"READ {tag} gaps: {gaps(r'SAVE_PARK|LOADED_|TRACK_CAP')}")
exec(open(L + "parkscene.py").read())
try:
    enter(); reply("pause", 0.3, 0)
    shutil.copy(SRC, WROTE); os.chmod(WROTE, 0o644); os.utime(WROTE, None)
    print("load:", load(1))
    print("companions started, the log so far:", _re.findall(r"(\d+) companions started from the save", logtext()))
    reply("resume", 0.3, 0)
    reads("1-loaded")
    reply(f"camera {CAMERA}", 0.6, 0); time.sleep(1.0); shot("1-loaded", 0.5)
    print("SELL:", reply(f"sell {THING}", 1.5, 3)); time.sleep(3.0)
    reads("2-sold")
    shot("2-sold", 0.5)
    reply("pause", 0.3, 0)
    mark = len(logtext()); reply("savepark sells", 2.5, 3)
    shutil.copy(SAVED, os.path.join(OUT, "sellload-saved.TPWS"))
    print("gaps after save:", gaps(r"SAVE_PARK|LOADED_|TRACK_CAP"))
    print("load again:", load(2))
    print("companions started, the log so far:", _re.findall(r"(\d+) companions started from the save", logtext()))
    reply("resume", 0.3, 0)
    reads("3-reloaded")
    reply(f"camera {CAMERA}", 0.6, 0); time.sleep(1.0); shot("3-reloaded", 0.5)
finally:
    done()
