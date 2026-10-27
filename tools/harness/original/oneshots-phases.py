#!/usr/bin/env python3
"""oneshots-phases.py LOG NEAR AWAY BACK END - oneshots.py's voices by phase (the line marks run.sh prints) and by place:
how many came, how many were played, and life - (length + 250). A torn read (the walk caught the list while the sound
thread changed it: voices already flagged 0x10 'come', or a voice longer than 30 ms gone within 30 ms) is left out and counted."""
import re, sys, collections
L = open(sys.argv[1]).read().splitlines(); a, b, c, d = map(int, sys.argv[2:6])
ph = {'near': (a, b), 'away': (b, c), 'back': (c, d)}
come = {}; n = collections.Counter(); ex = collections.defaultdict(list)
for i, l in enumerate(L, 1):
    m = re.match(r'\+(\d+) come (0x[0-9a-f]+) effect (\d+) \(0x(\w+)\) at \((-?\d+),(-?\d+),(-?\d+)\).* length (\d+)', l)
    if m: come[m.group(2)] = (i, m.group(5), m.group(7), int(m.group(8)), ' flags 0x' in l and int(l.split(' flags 0x')[1].split()[0], 16) & 0x10)
    m = re.match(r'\+(\d+) gone (0x[0-9a-f]+) effect (\d+) after (\d+) ms .*played: (\w+)', l)
    if m and m.group(2) in come:
        ci, x, z, ln, ended = come.pop(m.group(2)); p = [k for k, (s, e) in ph.items() if s <= ci < e]
        if not p: continue
        if ended or (ln > 30 and int(m.group(4)) < 30): n[(p[0], 'torn read', '-')] += 1; ex[(p[0], 'torn read', '-')].append(0); continue
        who = 'toilets' if x == '555' else 'spray' if (x, z) == ('525', '315') else '208 by the Spray' if (x, z) == ('525', '300') else 'others'
        k = (p[0], who, m.group(5)); n[k] += 1; ex[k].append(int(m.group(4)) - ln - 250)
for k in sorted(n, key=lambda k: (list(ph).index(k[0]), k[1], k[2])):
    print(k[0], k[1], 'played', k[2], n[k], 'voices; life-(length+250)', min(ex[k]), '..', max(ex[k]), 'ms')
