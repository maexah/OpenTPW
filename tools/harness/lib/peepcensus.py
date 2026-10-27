#!/usr/bin/env python3
"""peepcensus.py - this game's guests, from the console's `peeps` reply, counted as original/leavers.py counts the
original's (Q668). Import it (`sys.path.insert(0, LIB); import peepcensus`) or run it on a log: `peepcensus.py run.log`
prints a census for each `peeps` reply in the log.

    parse(lines)            {thing id: (state name, happiness, exit level, cell)} from a `peeps` reply's lines ([dbg] or not)
    watchers(guests, staff) each entertainer of a `staff` reply, his cell and activity, and the guests deciding round him
    census(guests, loaded)  the leavers.py census's words: guests, leaving (HeadingForExit .. AtTheBusStop), deciding,
                            loaded-left, the mean happiness of those not leaving, byte0 (happiness byte nought), under25,
                            exit<0, exit>0
    leavers(text, loaded)   from a log: each "Person N: leaving, <why>, ..." line as (id, why, loaded or arrived)
"""
import re, sys, collections

LEAVING = {"HeadingForExit", "PickingACellOutside", "WalkingOutside", "AtTheBusStop"}
LINE = re.compile(r"thing\s+(\d+) kind \d+ state (\w+) .*? exit\s+(-?\d+) happy\s+(-?[\d.]+).*? at \((-?[\d.]+),(-?[\d.]+)\)")
STAFF = re.compile(r"thing\s+(\d+) model 6 .*?\" (\w+) .*? cell \((-?\d+),(-?\d+)\)")
LEAVE = re.compile(r"Person (\d+): leaving, ([a-z ]+), exit level (-?\d+), happiness (-?\d+) to (-?\d+)")

def parse(lines):
    out = {}
    for s in lines:
        m = LINE.search(s)
        if m: out[int(m.group(1))] = (m.group(2), float(m.group(4)), int(m.group(3)), (int(float(m.group(5))), int(float(m.group(6)))))
    return out

def watchers(guests, staff_lines):
    """Each entertainer (model 6) in a `staff` reply, his cell, his activity and his watchers: the guests deciding on
    the nine cells round him (leavers.py's words)."""
    out = []
    for s in staff_lines:
        m = STAFF.search(s)
        if not m: continue
        c = (int(m.group(3)), int(m.group(4)))
        n = sum(1 for v in guests.values() if v[0] == "Deciding" and max(abs(v[3][0] - c[0]), abs(v[3][1] - c[1])) <= 1)
        out.append(f"entertainer {m.group(1)} at {c} {m.group(2)} watchers {n}")
    return " ".join(out)

def hbyte(h): return int(h) & 0xff

def census(guests, loaded=()):
    here = {i: v for i, v in guests.items() if v[0] not in LEAVING}
    hs = [v[1] for v in here.values()]
    mean = sum(hs) / len(hs) if hs else 0
    return (f"guests {len(guests)} leaving {len(guests) - len(here)} deciding {sum(1 for v in guests.values() if v[0] == 'Deciding')} "
            f"loaded-left {len(set(loaded) & set(guests))} mean-happiness {mean:.1f} byte0 {sum(1 for h in hs if hbyte(h) == 0)} "
            f"under25 {sum(1 for h in hs if h < 25)} exit<0 {sum(1 for v in here.values() if v[2] < 0)} "
            f"exit>0 {sum(1 for v in here.values() if v[2] > 0)}")

def leavers(text, loaded=()):
    return [(int(i), why, "loaded" if int(i) in loaded else "arrived") for i, why, *_ in LEAVE.findall(text)]

if __name__ == "__main__":
    text = open(sys.argv[1], errors="replace").read()
    for block in re.split(r"\[dbg\] peeps \d+\n", text)[1:]:
        print(census(parse(block.split("\n[dbg] peeps ")[0].splitlines())))
    print(collections.Counter((w, k) for _, w, k in leavers(text)))
