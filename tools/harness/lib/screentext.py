#!/usr/bin/env python3
"""lib/screentext.py (Q644): the buy and hire screens and every tab of each, with each text measured against its box. screentext.py <outdir> <bin> [WxH]. On the desktop (a photograph).
OPENTPW_TEXT_CENSUS=1: each text laid out logs 'UI text: "<text>" font F, W x H in a box BW x BH, x S[ OVER]'.
The stock park entered and paused, the window sized, the gadget's Buy (control 26) by a real click, the pointer on the
second row, a frame; each buy tab (0x1fa-0x1fd) a frame; b_allstaff, a frame; each hire tab (0x2490-0x2494) a frame."""
import os, sys
OUT = sys.argv[1]; BIN = sys.argv[2]; NAME = "screentext"; WANT = sys.argv[3] if len(sys.argv) > 3 else "2560x1366"
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
os.environ["OPENTPW_TEXT_CENSUS"] = "1"
exec(open(paths.LIB + "/parkrun.py").read())
from PIL import Image

def topix(x, y):
    w, h = Image.open(os.path.join(OUT, "0-park.png")).size; s = h / 1536
    return round(x * s + (w - 2048 * s) / 2, 1), round(y * s, 1)
def press(what, x, y, wait=1.0):
    cx, cy = topix(x, y); reply(f"click {cx} {cy}", 0.05, 0); time.sleep(wait); print(f"   click {what} at {cx},{cy}")
def hover(what, x, y):
    cx, cy = topix(x, y); move(cx, cy, what); time.sleep(0.8)
def control(c):
    r_ = place(c); reply(f"click {r_[0] + r_[2] / 2} {r_[1] + r_[3] / 2}", 0.05, 0); time.sleep(1.0); print(f"   click control {c}")
def away():
    x, y = topix(1150, 1400); move(x, y, "away")
CENSUS = re.compile(r'UI (text): "(.*)" font (\d+), (\d+) x (\d+) in a box (\d+) x (\d+), x ([\d.]+)( OVER)?')
def census(mark, tag):
    got = CENSUS.findall(logtext()[mark:]); over = [g for g in got if g[8]]
    held = sorted({(g[1], g[7]) for g in got if float(g[7]) < 1.199})
    with open(os.path.join(OUT, f"census-{tag}.txt"), "w") as f:
        for g in got: f.write("\t".join(g[:8]) + ("\tOVER" if g[8] else "") + "\n")
    print(f"  census {tag}: {len(got)} texts laid out, {len(over)} over their box {[g[1] for g in over][:12]}, {len(held)} drawn under x 1.2 {held[:12]}")
    return got, over

try:
    enter(); reply("pause", 0.5, 0)
    w_, h_ = WANT.split("x"); reply(f"size {w_} {h_}", 0.5, 0); time.sleep(2.0)
    print("window:", reply("size", 0.4, 0), "x11", size())
    shot("0-park", 0.3)
    mk = len(logtext())
    control("26"); hover("the buy list's second row", 1131, 482); away(); shot("1-buy", 0.4)
    buy = census(mk, "buy")
    mk = len(logtext())
    press("b_allstaff", 1917, 634, 1.5); away(); shot("2-hire", 0.5)
    hire = census(mk, "hire")
    for t in ("2490", "2493", "2492", "2494", "2491"):
        mk = len(logtext()); control(t); away(); shot(f"2-hire-{t}", 0.4); census(mk, f"hire-{t}")
    press("b_allthings", 1917, 634, 1.5); away()
    for t in ("1fb", "1fa", "1fd", "1fc"):
        mk = len(logtext()); control(t); away(); shot(f"1-buy-{t}", 0.4); census(mk, f"buy-{t}")
    press("the hire screen's exit", 1930, 859, 1.2)
finally:
    done()
