#!/usr/bin/env python3
"""ringsweep.py [<list of park files>] - the park analyser's sixteen int rings (+62598 into thing 5, 589 bytes apiece:
mCurrentEntry, mNumEntries, mWrappedAround, mTemp, 144 months) across every park file on hand (Q387; saves.md,
"The park analyser"). With no list it finds them: every .TPWS/.TPWI under the harness folders, ~/Games and
the original's install (paths.ORIGINAL). Prints, per ring, how many files hold it on the cash-in ring's slot, how many have a month so far that
is not 0, and the commonest first months; then the balance law, each closed month of the 1st ring = the one before
plus that month's cash in (6th) less its all costs (3rd), over the files whose 1st and 6th share a slot; and the files
that break either. Then the visitors law (Q389): the 11th ring's months never fall, its newest is at most mLifetimeVisitors
(+73480), and its month so far is 0; with the files that break it. A reader for one file is parkfile.money_rings."""
import collections, os, subprocess, sys, struct
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import parkfile
def rings(p):
    at = p.at(p.get('analyser')) + 62598; out = []
    for k in range(16):
        a = at + 589 * k; cur, num = struct.unpack_from('<ii', p.b, a); wrap = p.b[a + 8]; temp = struct.unpack_from('<i', p.b, a + 9)[0]
        months = struct.unpack_from('<144i', p.b, a + 13)
        out.append(dict(current=cur, total=temp, months=[months[(cur - i) % num] for i in range(num if wrap else cur + 1)][::-1] if num else []))
    return out
if len(sys.argv) > 1: paths = [l for l in open(sys.argv[1]).read().split('\n') if l]
else:
    roots = [paths.WORK, paths.ORIGINAL] + ([os.environ['OPENTPW_FULLSIM_PLAYER']] if 'OPENTPW_FULLSIM_PLAYER' in os.environ else [])
    paths = subprocess.run(['find'] + roots + ['(', '-iname', '*.TPWS', '-o', '-iname', '*.TPWI', ')'], capture_output=True, text=True).stdout.split('\n')
    paths = [p for p in paths if p]
slot = collections.Counter(); temp = collections.Counter(); first = [collections.Counter() for _ in range(16)]
law = collections.Counter(); bad = []; vlaw = collections.Counter(); since = collections.Counter()
for path in paths:
    try: r = rings(parkfile.Park(path))
    except Exception: law['unread'] += 1; bad.append(('unread', path)); continue
    law['files'] += 1
    v = r[10]; life = parkfile.analyser(parkfile.Park(path))['mLifetimeVisitors']
    vbad = [w for w in ('falls' if any(b < a for a, b in zip(v['months'], v['months'][1:])) else None,
        'over lifetime' if v['months'] and v['months'][-1] > life else None, 'month so far' if v['total'] else None) if w]
    vlaw['visitors law fails' if vbad else 'visitors law holds'] += 1
    if vbad: bad.append(('visitors', path, vbad, v['months'][-3:], life))
    elif v['months']: since[life - v['months'][-1]] += 1
    for k in range(16):
        slot[k] += r[k]['current'] == r[5]['current']; temp[k] += r[k]['total'] != 0
        if r[k]['months']: first[k][r[k]['months'][0]] += 1
    if r[0]['current'] != r[5]['current']: bad.append(('slot', path, r[0]['current'], r[5]['current'])); continue
    b, c, o = r[0]['months'], r[5]['months'], r[2]['months']
    miss = [(k, b[k], b[k - 1] + c[k] - o[k]) for k in range(1, len(b)) if b[k] != b[k - 1] + c[k] - o[k]]
    law['law fails' if miss else 'law holds'] += 1
    if miss: bad.append(('law', path, miss[:3]))
print(dict(law))
for k in range(16): print(f'ring {k + 1:2}: on the 6th\'s slot {slot[k]}, a month so far {temp[k]}, first months {first[k].most_common(3)}')
print(dict(vlaw), 'lifetime less the newest month, commonest:', since.most_common(5))
for x in bad: print(x)
