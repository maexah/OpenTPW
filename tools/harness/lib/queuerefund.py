#!/usr/bin/env python3
"""queuerefund.py <outdir> <bin> <A|N|F> - a queue cell's refund in THIS game, read from the balance at each step (Q1052; docs/exe/park-engine.md, "DEMOLISH refunds").
The venv's python -u, the desktop or off-screen (parkrun.py's rules), OPENTPW_GAME_PATH set; BIN the build's folder.
A: the stock Belly Bounce's queue edited as q1038/ours/ours.py A does (click (50,22), south (49,25), west (46,25)), Backspace x3: expect +111 each (37 a cell).
N: Mumbo (1109) bought at (41,22) with its queue joined at (43,21) as headseries.py does; a run laid and taken up (age 0, no customer: expect 75 a cell),
   then a guest sent aboard until `rings` counts a customer, then the same run laid and taken up (expect 37 a cell): `queue 42 20` and `delqueue 42 20`, LiftQueue's refund.
F: the park file the original is given: Mumbo just bought at (41,22), its queue node (42,21) left UNJOINED (a joined one takes guests and the original counts customers at once: 8 users, scrap
   value 50 per cent), saved paused as Q1052F.TPWS in OUT."""
import os, sys, re, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__))); import paths
OUT = sys.argv[1]; BIN = sys.argv[2]; CASE = sys.argv[3]; NAME = "Q1052" + CASE   # F saves NAME.TPWS for original/queuerefund.sh
exec(open(os.path.join(paths.LIB, "parkrun.py")).read())
def bal():
    g = reply("money", 0.4, 0)[0]; return int(re.search(r"balance (-?\d+)", g).group(1))
def say(label, b0):
    b1 = bal(); print(f"  {label}: balance {b0} -> {b1} (delta {b1 - b0:+d})"); return b1
def click(x, y):
    g = reply(f"worldclick {x} {y}", 0.7, 0); print("  worldclick", x, y, "->", " | ".join(g)); return g
def back():
    g = reply("backspace", 0.8, 0); print("  backspace ->", " | ".join(g)); return g
def customers(tid):
    g = " ".join(reply(f"rings {tid}", 0.5, 0)); m = re.search(r"customers (\d+)", g); return int(m.group(1)) if m else -1
try:
    enter(); reply("pause", 0.5, 1)
    if CASE == "F":
        got = " ".join(reply("buy 1109 41 22 0", 2.5, 9)); print("  buy:", got[:200])
        print("  money:", reply("money", 0.4, 0)[0]); print("  save:", save())
    elif CASE == "A":
        reply("camera 495 230 90 0", 0.5, 1)
        click(50, 22); b = bal(); print("  start balance", b); click(49, 25); b = say("south run (3 cells laid)", b); click(46, 25); b = say("west run (3 cells laid)", b)
        shot("1-laid")
        for i in range(1, 4):
            back(); b = say(f"backspace {i}", b); shot(f"2-backspace{i}")
    else:
        got = " ".join(reply("buy 1109 41 22 0", 2.5, 9)); print("  buy:", got[:200])
        tid = int(re.search(r"built as thing (\d+)", got).group(1))
        reply("camera 430 237 90 0", 0.5, 1)
        def lay_and_lift(label):
            b = bal(); g = reply(f"queue 42 20 {tid} 42 21", 0.6, 0); b = say(f"{label}: queue cell laid", b)
            print("   ", " | ".join(g)); g = reply("delqueue 42 20", 0.6, 0); print("   ", " | ".join(g)); return say(f"{label}: delqueue", b)
        lay_and_lift("NEW ride (age 0, no customer)")
        shot("1-fresh")
        reply(f"tool queue {tid}", 0.5, 0); click(42, 21); click(43, 21)
        reply("tool off", 0.5, 0)
        reply("resume", 0.3, 0)
        g = " ".join(reply("admit 43 21", 1.0, 0)); m = re.search(r"admit: guest (\d+)", g); reply(f"send {m.group(1)} {tid}", 1.0, 0)
        n = 0
        for i in range(120):
            time.sleep(1.0); n = customers(tid)
            if n >= 1: print("  customers", n, "after", i + 1, "s"); break
        reply("pause", 0.5, 1)
        print("  rings (after):", " ".join(reply(f"rings {tid}", 0.5, 0))[:200])
        lay_and_lift("SAME ride after a customer")
        shot("2-used")
    print("unimplemented lines:"); [print("  ", l) for l in reply("unimplemented", 1.5, 0) if "QUEUE" in l or "BACKSPACE" in l or "SCRAP" in l]
finally:
    done()
