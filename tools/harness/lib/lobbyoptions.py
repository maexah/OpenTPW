"""lobbyoptions.py WORKDIR CASE FOLDER [gpu=LINE] [vc=N] [clickN] [tick] [box=tick|cross] [ENV=VAL ...]: one start of the build on a private game
folder (made on first use under WORKDIR/game-FOLDER, kept between cases that name the same FOLDER, so a saved Config.tcf carries
to the next start), the lobby, the Options screen opened from the lobby menu, screenshots (lobby, options, click1..N), optionally
N clicks on the Video Card row and the tick, then the log's Graphics card / GPU / Options lines. box=tick|cross answers the RESTART GAME box the tick raises after a card change (Q985): cross ticks again and shows the box again, tick waits for the game to end. gpu=LINE sets opentpw.cfg's gpu
line ("-" removes it); vc=N sets Config.tcf's video card field (byte 16; byte 12 is graphics quality). Output in WORKDIR/runs/CASE.
BIN names the build's folder (default this repo's Debug build). Run with the harness Python (Xlib), on the desktop display.
Q983: TOOLING.md, the table row `lib/lobbyoptions.py`."""
import os, sys, re, shutil, struct
LIB = os.path.dirname(os.path.abspath(__file__)); sys.path.insert(0, LIB)
REPO = os.path.dirname(os.path.dirname(os.path.dirname(LIB)))
os.environ["OPENTPW_NO_MESSAGE_BOX"] = "1"
import gamefolder
HERE = os.path.abspath(os.path.expanduser(sys.argv[1])); CASE, FOLDER = sys.argv[2], sys.argv[3]
GPU = VC = None; CLICKS = 0; TICK = False; BOX = None
for kv in sys.argv[4:]:
    if kv.startswith("gpu="): GPU = kv[4:]
    elif kv.startswith("vc="): VC = int(kv[3:])
    elif kv.startswith("click"): CLICKS = int(kv[5:] or 1)
    elif kv == "tick": TICK = True
    elif kv.startswith("box="): BOX = kv[4:]; TICK = True
    else: k, v = kv.split("=", 1); os.environ[k] = v
OUT = os.path.join(HERE, "runs", CASE); os.makedirs(OUT, exist_ok=True)
BIN = os.environ.get("BIN", os.path.join(REPO, "source/OpenTPW/bin/Debug/net10.0"))
PRIVATE = os.path.join(HERE, "game-" + FOLDER)
if not os.path.isdir(PRIVATE): gamefolder.make(PRIVATE, {})
cfg = os.path.join(PRIVATE, "save", "opentpw.cfg")
if GPU is not None:
    text = open(cfg).read() if os.path.exists(cfg) else ""
    text = "".join(l + "\n" for l in text.splitlines() if not l.startswith("gpu"))
    if GPU != "-": text += "gpu " + GPU + "\n"
    open(cfg, "w").write(text)
tcf = os.path.join(PRIVATE, "save", "Config.tcf")
if VC is not None:
    b = bytearray(open(tcf, "rb").read()); b[16:20] = struct.pack("<I", VC); open(tcf, "wb").write(b)
def vcbyte():
    return struct.unpack("<I", open(tcf, "rb").read()[16:20])[0] if os.path.exists(tcf) else None
os.environ["OPENTPW_LOG_DIR"] = OUT
exec(open(os.path.join(LIB, "gamedrive.py")).read())
try:
    print(f"CASE {CASE}: Config.tcf video card dword at the start: {vcbyte()}")
    wait_for("GPU:", 60)
    wait_for("Loaded the lobby", 150); attach(); time.sleep(3.0)
    shot("lobby", 0.5)
    key("Escape", "the lobby menu"); time.sleep(1.0)
    l = ask("control b", "control"); m = re.search(r"at \((-?\d+),(-?\d+)\) size \((\d+),(\d+)\)", l)
    x, y, w_, h = [int(v) for v in m.groups()]
    move(x + w_ / 2, y + h / 2, "Options"); button(1, "Options"); time.sleep(1.5)
    shot("options", 0.5)
    l = ask("control 1d4d3", "control"); print("  ", l)
    m = re.search(r"at \((-?\d+),(-?\d+)\) size \((\d+),(\d+)\)", l)
    x, y, w_, h = [int(v) for v in m.groups()]
    for i in range(CLICKS):
        move(x + w_ / 2, y + h / 2, "the Video Card arrow"); button(1, "arrow"); time.sleep(1.0)
        shot(f"click{i+1}", 0.5)
    if TICK:
        l = ask("control ffffffff", "control"); m = re.search(r"at \((-?\d+),(-?\d+)\) size \((\d+),(\d+)\)", l)
        tx, ty, tw, th = [int(v) for v in m.groups()]
        move(tx + tw / 2, ty + th / 2, "the tick"); button(1, "tick"); time.sleep(1.5)
    if BOX:
        # RESTART GAME (Q985): the box after a card change. Its cross (9873c8) leaves the Options screen open, so "cross" ticks again
        # and photographs the second box; its tick (9873c9) quits the lobby, so "tick" waits for the game to end and prints the exit code.
        def box_button(cid, what):
            l = ask("control " + cid, "control"); print("  ", l)
            m = re.search(r"at \((-?\d+),(-?\d+)\) size \((\d+),(\d+)\)", l)
            bx, by, bw, bh = [int(v) for v in m.groups()]
            move(bx + bw / 2, by + bh / 2, what); button(1, what); time.sleep(1.5)
        time.sleep(0.5); shot("restart", 0.5)
        if BOX == "cross":
            box_button("9873c8", "the box's cross"); shot("after-cross", 0.5)
            box_button("ffffffff", "the Options tick again"); shot("again", 0.5)
            box_button("9873c8", "the box's cross again"); shot("after-cross2", 0.5)
        else:
            box_button("9873c9", "the box's tick")
            for _ in range(40):
                if game.poll() is not None: break
                time.sleep(0.5)
            print(f"  exit code after the box's tick: {game.poll()}")
    print(f"  Config.tcf video card dword at the end: {vcbyte()}")
finally:
    finish()
for l in open(os.path.join(OUT, "run.log"), errors="replace"):
    if re.search(r"Graphics card|GPU:|opentpw.cfg|Options:|video card|Saves: wrote|Message box|quitting|Front end", l): print("LOG>", l.rstrip())
