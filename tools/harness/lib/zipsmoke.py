#!/usr/bin/env python3
"""zipsmoke.py - does a weekly build's zip start the game? (Q751)

    python3 -u zipsmoke.py <OpenTPW-<tag>-linux-x64.zip> <outdir> [--game DIR]

Unzips the zip into <outdir>/unzipped (fresh), runs the repo's guard on it (tools/weekly/release.py guard), starts
the program INSIDE it (the apphost, `./OpenTPW`, so the bundled runtime is the one used, DOTNET_ROOT unset) on the
desktop's display with SDL_AUDIODRIVER=dummy, OPENTPW_DEBUG_CONSOLE=1 and OPENTPW_LOG_DIR=<outdir>, waits for
"Loaded the lobby", grabs one frame of the window (root-window grab by _NET_WM_PID, CLAUDE.local.md's recipe) to
<outdir>/lobby.png with its mean brightness (an all-black frame means it was not presenting), sends `quit`, then
prints the launch report's lines from console.log and runs logpii.py on it. The game folder's save/ is summed either
side. Exit 0 only when the guard, the lobby, a non-black frame, the zip's own tag in the Build line and a clean log
all hold. Linux zips only: the Windows and macOS zips cannot run here."""
import hashlib, os, re, shutil, subprocess, sys, threading, time, zipfile
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
from Xlib import X, display
from PIL import Image, ImageStat

a = sys.argv[1:]
if len(a) < 2:
    sys.exit(__doc__)
ZIP, OUT = os.path.abspath(a[0]), os.path.abspath(a[1])
GAME = a[a.index("--game") + 1] if "--game" in a else paths.game()
REPO = paths.REPO
HERE = os.path.dirname(os.path.abspath(__file__))
tag = re.match(r"OpenTPW-(.+)-linux-x64\.zip$", os.path.basename(ZIP))
if not tag:
    sys.exit("not a linux-x64 zip by the weekly name")
tag = tag.group(1)
ok = {}

def save_sum():
    h = hashlib.md5()
    for root, _, names in sorted(os.walk(os.path.join(GAME, "save"))):
        for n in sorted(names):
            p = os.path.join(root, n); h.update(p.encode()); h.update(open(p, "rb").read())
    return h.hexdigest()

os.makedirs(OUT, exist_ok=True)
UNZ = os.path.join(OUT, "unzipped")
shutil.rmtree(UNZ, ignore_errors=True)
g = subprocess.run([sys.executable, os.path.join(REPO, "tools/weekly/release.py"), "guard", ZIP], capture_output=True, text=True)
print(g.stdout.strip()); ok["guard"] = g.returncode == 0
with zipfile.ZipFile(ZIP) as z:
    z.extractall(UNZ)
    for info in z.infolist():   # extractall drops the modes; put the zip's back
        mode = (info.external_attr >> 16) & 0o777
        if mode: os.chmod(os.path.join(UNZ, info.filename), mode)
top = os.path.join(UNZ, f"OpenTPW-{tag}")
before = save_sum()
env = {k: v for k, v in os.environ.items() if k != "DOTNET_ROOT"}
env.update(SDL_AUDIODRIVER="dummy", OPENTPW_DEBUG_CONSOLE="1", OPENTPW_LOG_DIR=OUT)
logf = open(os.path.join(OUT, "run.log"), "w")
game = subprocess.Popen(["./OpenTPW", "--game", GAME], cwd=top, env=env, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                        stderr=subprocess.STDOUT, text=True)
lines = []
def pump():
    for line in game.stdout:
        lines.append(line); logf.write(line); logf.flush()
threading.Thread(target=pump, daemon=True).start()
deadline = time.time() + 90
while time.time() < deadline and not any("Loaded the lobby" in l for l in lines) and game.poll() is None:
    time.sleep(0.5)
ok["lobby"] = any("Loaded the lobby" in l for l in lines)

def find_by_pid(d, pid):
    atom = d.intern_atom("_NET_WM_PID"); st = [d.screen().root]
    while st:
        w = st.pop()
        try:
            p = w.get_full_property(atom, X.AnyPropertyType)
            if p and p.value[0] == pid and w.get_wm_name() == "Theme Park World": return w
            st.extend(w.query_tree().children)
        except Exception: pass

mean = 0.0
if ok["lobby"]:
    time.sleep(4)
    d = display.Display(); win = find_by_pid(d, game.pid); root = d.screen().root
    if win:
        geo = win.get_geometry(); at = win.translate_coords(root, 0, 0)
        raw = root.get_image(-at.x, -at.y, geo.width, geo.height, X.ZPixmap, 0xffffffff)
        im = Image.frombytes("RGB", (geo.width, geo.height), raw.data, "raw", "BGRX")
        im.save(os.path.join(OUT, "lobby.png")); mean = ImageStat.Stat(im.convert("L")).mean[0]
        print(f"frame {geo.width}x{geo.height} mean brightness {mean:.1f} -> {OUT}/lobby.png")
ok["frame"] = mean > 5
try:
    game.stdin.write("quit\n"); game.stdin.flush(); game.wait(15)
except Exception:
    game.kill()
log = os.path.join(OUT, "console.log")
text = open(log, errors="replace").read() if os.path.exists(log) else ""
for l in text.splitlines():
    if re.search(r"\] (Build|System|\.NET|GPU|Display|Game data|Game files):", l): print(" ", l)
ok["tag"] = f"Build: {tag}, commit " in text
p = subprocess.run([sys.executable, os.path.join(HERE, "logpii.py"), log], capture_output=True, text=True)
print(p.stdout.strip().splitlines()[-1] if p.stdout.strip() else p.stderr.strip()); ok["clean log"] = p.returncode == 0
after = save_sum(); ok["save/ untouched"] = before == after
print("save/ sum", before, "->", after)
print("RESULT", "PASS" if all(ok.values()) else "FAIL", ", ".join(f"{k} {'yes' if v else 'NO'}" for k, v in ok.items()))
sys.exit(0 if all(ok.values()) else 1)
