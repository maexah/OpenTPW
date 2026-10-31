#!/usr/bin/env python3
"""lobbyeyecmp.py quit|pick OURS_EYE.txt ORIG_EYE.txt [ORIG_CLICK.t] (Q749): ours' island camera against the original's, the eye AND the look
point AND the view's pitch (degrees below the horizon, from the two), by time since the island camera's first pass.
quit: ours' `post` rows of lobbyfilm.py's quit-eye.txt (or lobbyeye.py's eye.txt) against original/rendereye.py's rows of the lobby built after
      Exit To Lobby (rows after ORIG_CLICK.t + 0.4 s); the first pass is each side's first row off the seed (eye 1 unit or more from x = 500).
pick: lobbyfilm.py's pick-eye.txt against the first object's rows of the original's file (the slot click from the attract flight); the first
      pass is the first row whose look point moved 5 units from the row before (the flight's look moves under 1 per row), ours' the first row
      tagged `pick` whose orbit is over 0.
Prints both sides every 0.25 s to 1 s then each second, their distances, how long ours' pitch stays over 45 degrees, the mean eye distance over
0.5 to 6 s with no shift, and the shift of OURS ahead (ours at t+s against the original's at t) that minimises it."""
import re, math, sys
V = r'\(([^,]+), ([^,]+), ([^)]+)\)'
def pitch(e, k): return math.degrees(math.atan2(e[1] - k[1], math.hypot(e[0] - k[0], e[2] - k[2])))
def rows_ours(path, tag):
    out = []
    for l in open(path).read().splitlines()[1:]:
        p = l.split(None, 2)
        if p[0] != tag: continue
        m = re.search(r'eye ' + V + r' look ' + V + r'.*orbit=(\S+)', p[2])
        out.append((float(p[1]), tuple(map(float, m.groups()[:3])), tuple(map(float, m.groups()[3:6])), float(m.group(7))))
    return out
def rows_orig(path):
    out = []
    for l in open(path):
        m = re.match(r'(\S+)\s+\S+ obj (\S+) eye ' + V + r' look ' + V + r'.*delta (\S+)', l)
        if not m: continue
        e = tuple(float(m[i]) for i in (3, 4, 5)); k = tuple(float(m[i]) for i in (6, 7, 8))
        if abs(e[0]) > 1e6: continue
        out.append((float(m[1]), e, k, m[2], float(m[9])))
    return out
mode, ours_path, orig_path = sys.argv[1:4]
orig = rows_orig(orig_path)
if mode == 'quit':
    ours = rows_ours(ours_path, 'post')
    click = float(open(sys.argv[4]).read()); orig = [r for r in orig if r[0] > click + 0.4]
    i0 = next(i for i, r in enumerate(ours) if abs(r[1][0] - 500) > 1); j0 = next(i for i, r in enumerate(orig) if abs(r[1][0] - 500) > 1)
else:
    ours = rows_ours(ours_path, 'pick'); first_obj = orig[0][3]; orig = [r for r in orig if r[3] == first_obj]
    i0 = next(i for i, r in enumerate(ours) if r[3] > 0)
    j0 = next(i for i in range(1, len(orig)) if math.dist(orig[i][2], orig[i - 1][2]) > 5)
ours = [(r[0] - ours[i0][0], r[1], r[2]) for r in ours[max(i0 - 1, 0):]]
orig = [(r[0] - orig[j0][0], r[1], r[2]) for r in orig[max(j0 - 1, 0):]]
def at(rows, t): return min(rows, key=lambda r: abs(r[0] - t))
d = math.dist
print(f'{mode}: t = 0 is each side\'s first island pass; eye / look / pitch deg; distances eye, look')
print(' t      ours eye                    ours look                  pit   |  original eye               original look              pit   | deye  dlook')
for t in (-0.05, 0, .25, .5, .75, 1, 2, 3, 4, 5, 6):
    o = at(ours, t); g = at(orig, t)
    print(f'{t:5.2f} ({o[1][0]:7.2f},{o[1][1]:6.2f},{o[1][2]:7.2f}) ({o[2][0]:7.2f},{o[2][1]:6.2f},{o[2][2]:7.2f}) {pitch(o[1], o[2]):5.1f} | '
          f'({g[1][0]:7.2f},{g[1][1]:6.2f},{g[1][2]:7.2f}) ({g[2][0]:7.2f},{g[2][1]:6.2f},{g[2][2]:7.2f}) {pitch(g[1], g[2]):5.1f} | {d(o[1], g[1]):5.2f} {d(o[2], g[2]):5.2f}')
over = [r[0] for r in ours if r[0] >= 0 and pitch(r[1], r[2]) > 45]
print(f'ours over 45 degrees after the first pass: {"none" if not over else f"{len(over)} rows, until {max(over):.2f} s"}; '
      f'the original: {sum(1 for r in orig if r[0] >= 0 and pitch(r[1], r[2]) > 45)} rows')
ts = [x / 10 for x in range(5, 60)]
def mean(f, s): return sum(f(t, s) for t in ts) / len(ts)
print('no shift: mean eye distance %.2f, mean look distance %.2f' % (mean(lambda t, s: d(at(ours, t + s)[1], at(orig, t)[1]), 0), mean(lambda t, s: d(at(ours, t + s)[2], at(orig, t)[2]), 0)))
best = min((mean(lambda t, s: d(at(ours, t + s)[1], at(orig, t)[1]), s), s) for s in [x / 20 for x in range(-30, 31)])
print('best shift of ours ahead of the original: %.2f s, mean eye distance %.2f' % (best[1], best[0]))
