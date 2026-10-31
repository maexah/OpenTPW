"""parkoptions.py OUT BIN [box=tick|cross] [vc=N]: the park's Options screen and the RESTART GAME box a changed video card raises (Q985).
Enters the stock jungle park on a private game folder (OUT + "-game", made from paths.PRIVATE_GAME; vc=N sets Config.tcf's video card
field, byte 16), opens the Options screen from the park menu by real clicks (Escape, the menu's row 5, `control 1d4d3` the Video Card
arrow, `control ffffffff` the tick), shots park-options, park-click1, park-restart and park-after. box=cross answers the box's cross
(`9873c8`): the screen stays open and a second tick asks again (park-again). box=tick (the default) answers its tick (`9873c9`): the park
is left through the autosave and the lobby is built again; the run waits for "Loaded the lobby" a second time and lists the autosave.
Run with review-codex/venv/bin/python -u on the desktop; parkrun.py's names are in scope, so take none of them (README.md, "Writing a run")."""
import os, sys, struct
LIB = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, LIB)
import paths, gamefolder
OUT = os.path.abspath(os.path.expanduser(sys.argv[1])); BIN = os.path.abspath(os.path.expanduser(sys.argv[2])); NAME = "Q985"
BOXMODE = "tick"; VC = None
for _o in sys.argv[3:]:
    if _o.startswith("box="): BOXMODE = _o[4:]
    elif _o.startswith("vc="): VC = int(_o[3:])
GAME_FOLDER = OUT + "-game"
os.makedirs(OUT, exist_ok=True)
gamefolder.make(GAME_FOLDER, {})
_tcf = os.path.join(GAME_FOLDER, "save", "Config.tcf")
if VC is not None:
    _b = bytearray(open(_tcf, "rb").read()); _b[16:20] = struct.pack("<I", VC); open(_tcf, "wb").write(_b)
def vcbyte(): return struct.unpack("<I", open(_tcf, "rb").read()[16:20])[0]
exec(open(os.path.join(paths.LIB, "parkrun.py")).read())
def click(cid, what):
    x_, y_, w_, h_ = place(cid); move(x_ + w_ / 2, y_ + h_ / 2, what); button(1, what); time.sleep(1.2)
def windows(): return " | ".join(l_.strip() for l_ in reply("windows", 0.8, 0))
try:
    print(f"Config.tcf video card dword at the start: {vcbyte()}")
    enter(); time.sleep(1.5)
    key("Escape", "the park menu"); time.sleep(0.8)
    click("5", "the menu's Options row"); shot("park-options", 0.5)
    click("1d4d3", "the Video Card arrow"); shot("park-click1", 0.5)
    own_file(os.path.join(JUNGLE, "autosave.TPWS"))
    click("ffffffff", "the Options tick"); time.sleep(0.8); shot("park-restart", 0.5)
    print("  windows after the tick:", windows())
    print(f"  Config.tcf video card dword after the tick: {vcbyte()}")
    if BOXMODE == "cross":
        click("9873c8", "the box's cross"); shot("park-after", 0.5)
        print("  windows after the cross:", windows())
        click("ffffffff", "the Options tick again"); shot("park-again", 0.5)
        print("  windows after the second tick:", windows())
        click("9873c8", "the box's cross again")
    else:
        click("9873c9", "the box's tick")
        for _ in range(60):
            if len(re.findall(r"Loaded the lobby", logtext())) >= 2: break
            time.sleep(1)
        time.sleep(2); shot("park-after", 0.5)
        print("  windows after the box's tick:", windows())
        print("  the player's jungle folder:", sorted(os.listdir(JUNGLE)))
finally:
    for _l in logtext().splitlines():
        if re.search(r"Options:|Message box|Leaving the park|Loaded the lobby|Exit To Lobby|reload|Graphics card", _l): print("LOG>", _l.rstrip())
    done()
