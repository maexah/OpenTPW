#!/usr/bin/env python3
"""pointsat.py PID HEX... - every 4-aligned dword of the ORIGINAL's writable memory pointing at any of the given addresses (OFFS=N: or up to N
bytes before them), printed "A -> T -off". Run it again on what it prints to walk a pointer chain up: texture record -> material slot -> material object -> source material record (Q361)."""
import sys, struct, os
pid = int(sys.argv[1]); targets = [int(a, 16) for a in sys.argv[2:]]; offs = int(os.environ.get('OFFS', '0'), 0)
m = open(f'/proc/{pid}/mem', 'rb', buffering=0); regions = []
for line in open(f'/proc/{pid}/maps'):
    a, b = (int(x, 16) for x in line.split()[0].split('-'))
    if 'rw' in line.split()[1] and a < 0x7f000000:
        try: m.seek(a); regions.append((a, m.read(b - a)))
        except OSError: pass
want = {}
for t in targets:
    for o in range(0, offs + 1, 4): want[t - o] = (t, o)
for a, data in regions:
    n = len(data) // 4
    arr = struct.unpack(f'<{n}I', data[:n * 4])
    for i, v in enumerate(arr):
        if v in want: print(f'{a + 4 * i:#x} -> {want[v][0]:#x} -{want[v][1]}')
