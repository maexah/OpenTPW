"""oddsaves.py OUT BIN [tcf=KIND] [gms=KIND] [desktop]: (OUT must be a new folder; a game folder kept there keeps its odd or replaced files and backups.) Config.tcf and gms.dat written over when this game did not read them whole (Q216).
Enters the stock jungle park on a private game folder (OUT + "-game", made from paths.PRIVATE_GAME) whose Config.tcf and the player's
gms.dat are made odd first: KIND is one of `newer` (version dword +1), `zero` (version 0), `tail` (two bytes more), `short` (cut to
12 bytes), or `whole` (left as they are, the control). The Options screen's tick writes Config.tcf; `savepark` writes gms.dat
(Player.Save); each is done twice. Prints each file's md5 at the start, after each write, and each backup's, with the
`Saves:` log lines. Shots `oddsaves-options1`, `2` and `oddsaves-after-tick1`, `2`. OFFSCREEN on :79 (lane B), or the desktop with `desktop` as an
option. Run with review-codex/venv/bin/python -u; parkrun.py's names are in scope, so take none of them (README.md, "Writing a run")."""
import os, sys, hashlib, glob
LIB = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, LIB)
import paths, gamefolder
OUT = os.path.abspath(os.path.expanduser(sys.argv[1])); BIN = os.path.abspath(os.path.expanduser(sys.argv[2])); NAME = "Q216"
TCF_KIND = GMS_KIND = "whole"
for _o in sys.argv[3:]:
    if _o.startswith("tcf="): TCF_KIND = _o[4:]
    elif _o.startswith("gms="): GMS_KIND = _o[4:]
    elif _o == "desktop": pass
OFFSCREEN = "desktop" not in sys.argv[3:]; OFFSCREEN_DISPLAY = ":79"
GAME_FOLDER = OUT + "-game"
os.makedirs(OUT, exist_ok=True)
gamefolder.make(GAME_FOLDER, {})
_tcf = os.path.join(GAME_FOLDER, "save", "Config.tcf"); _gms = os.path.join(GAME_FOLDER, "save", "users", "1ref", "gms.dat")
def _odd(path, kind):
    b = bytearray(open(path, "rb").read())
    if kind == "newer": b[0:4] = (int.from_bytes(b[0:4], "little") + 1).to_bytes(4, "little")
    elif kind == "zero": b[0:4] = bytes(4)
    elif kind == "tail": b += bytes([0xA5, 0x5A])
    elif kind == "short": b = b[:12]
    elif kind != "whole": raise SystemExit(f"unknown kind {kind}")
    open(path, "wb").write(b)
_odd(_tcf, TCF_KIND); _odd(_gms, GMS_KIND)
def md5(path): return hashlib.md5(open(path, "rb").read()).hexdigest()[:12] + f" ({os.path.getsize(path)} bytes)"
def backups(path): return sorted(os.path.basename(p) for p in glob.glob(path + ".opentpw-backup*"))
SEED = {"Config.tcf": md5(_tcf), "gms.dat": md5(_gms)}
exec(open(os.path.join(paths.LIB, "parkrun.py")).read())
def click(cid, what):
    x_, y_, w_, h_ = place(cid); move(x_ + w_ / 2, y_ + h_ / 2, what); button(1, what); time.sleep(1.2)
def report(stage):
    print(f"  [{stage}] Config.tcf {md5(_tcf)}  backups {backups(_tcf)}; gms.dat {md5(_gms)}  backups {backups(_gms)}")
    for _p in backups(_tcf):
        print(f"      {_p}: {md5(_tcf + _p[len('Config.tcf'):])}")
    for _p in backups(_gms):
        print(f"      {_p}: {md5(_gms + _p[len('gms.dat'):])}")
try:
    print(f"seeded Config.tcf={TCF_KIND} {SEED['Config.tcf']}; gms.dat={GMS_KIND} {SEED['gms.dat']}")
    enter(); time.sleep(1.5); report("in the park")
    for n in (1, 2):
        key("Escape", "the park menu"); time.sleep(0.8)
        click("5", "the menu's Options row"); shot(f"oddsaves-options{n}", 0.5)
        click("ffffffff", "the Options tick"); time.sleep(0.8); shot(f"oddsaves-after-tick{n}", 0.5)
        report(f"after Options tick {n}")
        save(); report(f"after savepark {n}")
finally:
    for _l in logtext().splitlines():
        if re.search(r"Saves: |Save trouble|Message box|NOT SAVED", _l): print("LOG>", _l.rstrip())
    done()
