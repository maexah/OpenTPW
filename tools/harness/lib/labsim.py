#!/usr/bin/env python3
"""labsim.py - the original's research lab run forward from a park file, to predict when each project finishes.

    labsim.py <park file> [--ability 12] [--researchers 1] [--first 760] [--every 20] [--tech 0,0,80,85,85]

The rules are OpenTPW's docs/exe/park-engine.md, "The lab at work" (Q245): every hand-out (one researcher's
ResearchAbility, every 20 ticks) shares effort / the open kinds' efforts' sum x mWorkLoad x ability x 0.01 among the
kinds with an open project, stored as a float32; a project at or over its cost is finished, its kind's highest group
raised against ResearchTech[highest + 1], and the kind's next project picked (first open one after the current at or
under the group, else the first before it, else none). The lab (projects, efforts, current projects, groups,
mWorkLoad) is the file's, read with parkfile.lab. NOT modelled: the upgrade a finished ride appends (an item's
Upgrades[1..2].CostOfResearch, which needs the item files; Lost Kingdom's Easy layer makes every one nought), a
researcher who is resting or carried at a hand-out, researchers hired or fired, and ResearchTech[5..] (taken as 0).
It models 24-bit precision control (the compare on the stored float); the original compares the FPU sum before the store
(park-engine.md, "A project finished"), and the precision its sweep runs at is not read.

--researchers N hands out N times on each hand-out tick. --first is the tick of the first hand-out (760 in stock
Easymode, Q134). Prints one line per finished project: the hand-out, its tick, the kind, the item, a group raised, the
kind's next project; then whether every project is done.
"""
import argparse, os, sys
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from parkfile import Park, lab

f32 = np.float32


def run(path, ability=12, researchers=1, first=760, every=20, tech=(0, 0, 80, 85, 85), limit=100000, fpu=53):
    l = lab(Park(path))
    proj = [[item, cat, group, level, f32(cost), f32(done)] for item, cat, group, level, cost, done in l['projects']]
    kinds = [[effort, current, highest] for effort, output, current, highest in l['kinds']]
    work = l['mWorkLoad']
    table = list(tech) + [0] * (11 - len(tech))

    def open_(i):
        return i >= 0 and proj[i][5] < proj[i][4]

    def raise_group(k):
        raised = False
        while True:
            h = kinds[k][2]
            sel = [p for p in proj if p[1] == k and p[2] <= h]
            pct = (sum(1 for p in sel if p[5] >= p[4]) * 100) // len(sel) if sel else 0
            if pct < table[h + 1] or h >= 7:
                return raised
            kinds[k][2] = h + 1
            raised = True

    def pick(k):
        cur, before = kinds[k][1], -1
        for i, p in enumerate(proj):
            if p[1] == k and p[2] <= kinds[k][2] and p[5] < p[4]:
                if i < cur:
                    before = i if before < 0 else before
                elif i > cur:
                    kinds[k][1] = i
                    return
        if before >= 0:
            kinds[k][1] = before
        elif cur >= 0 and not open_(cur):
            kinds[k][1] = -1

    events, n = [], 0
    while n < limit and not all(p[5] >= p[4] for p in proj):
        tick = first + (n // researchers) * every
        n += 1
        total = sum(kinds[k][0] for k in range(5) if open_(kinds[k][1]))
        if total == 0:
            break
        for k in range(5):
            if kinds[k][0] and kinds[k][1] >= 0:
                if fpu == 53:
                    share = f32(float(kinds[k][0]) / float(total) * work * ability * float(f32(0.01)))
                else:
                    share = f32(np.longdouble(kinds[k][0]) / np.longdouble(total) * work * ability * np.longdouble(f32(0.01)))
                p = proj[kinds[k][1]]
                if p[5] < p[4]:
                    exact = float(p[5]) + float(share) if fpu == 53 else f32(p[5] + share)
                    p[5] = f32(exact)
                    if exact >= p[4]:
                        raised = raise_group(k)
                        pick(k)
                        events.append((n, tick, k, p[0], kinds[k][2] if raised else None, kinds[k][1]))
    return events, all(p[5] >= p[4] for p in proj), n


def main():
    a = argparse.ArgumentParser()
    a.add_argument('park')
    a.add_argument('--ability', type=int, default=12)
    a.add_argument('--researchers', type=int, default=1)
    a.add_argument('--first', type=int, default=760)
    a.add_argument('--every', type=int, default=20)
    a.add_argument('--tech', default='0,0,80,85,85')
    a.add_argument('--fpu', type=int, default=53, choices=(53, 24))
    o = a.parse_args()
    events, done, n = run(o.park, o.ability, o.researchers, o.first, o.every, tuple(int(x) for x in o.tech.split(',')),
                          fpu=o.fpu)
    for hand, tick, k, item, group, nxt in events:
        print('hand-out %5d tick %7d kind %d item %d%s next %d' % (hand, tick, k, item,
              ' group->%d' % group if group is not None else '', nxt))
    print('every project done: %s, after hand-out %d' % (done, n))


if __name__ == '__main__':
    main()
