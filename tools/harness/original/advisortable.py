#!/usr/bin/env python3
"""advisortable.py PID [meta_full.json] - the ORIGINAL's memory, read-only: the advisor's metadata table as it stands
in the running game (0x0076e300, 351 rows of 0x38; the image holds zeros, a static initializer fills it): the rows
whose +0x00 is nought (the POLLED rows, FUN_0059c680) as ranges with their count, each polled row's position in a
round, and, with the emulated table's json, every dword where the live table differs from it. A POLL for loadfile.sh."""
import json, struct, sys
pid = int(sys.argv[1]); f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
f.seek(0x0076e300); raw = f.read(351 * 0x38); row = lambda i: struct.unpack_from('<14I', raw, i * 0x38)
pol = [i for i in range(351) if row(i)[0] == 0]
out = []; s = p = pol[0]
for i in pol[1:] + [None]:
    if i is None or i != p + 1: out.append(str(s) if s == p else f'{s}-{p}'); s = i
    p = i
print(len(pol), 'polled rows:', ', '.join(out))
print('ids not their row:', [(i, row(i)[1]) for i in range(351) if row(i)[1] != i])
print('positions:', {i: pol.index(i) for i in (0, 185, 281, 282) if i in pol})
if len(sys.argv) > 2:
    m = json.load(open(sys.argv[2])); diff = []
    for i in range(351):
        for k in range(14):
            a = 0x76e300 + i * 0x38 + k * 4
            if m.get(hex(a), 0) != row(i)[k]: diff.append((i, hex(k * 4), m.get(hex(a), 0), row(i)[k]))
    print(len(diff), 'dwords differ from the emulated table (row, offset, emulated, live):', diff[:40])
