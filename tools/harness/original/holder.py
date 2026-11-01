#!/usr/bin/env python3
"""holder.py PID X Z SECS [KIND=6] - WRITES the running ORIGINAL's memory: the first sprite of KIND (6 = the mechanic) has its place (+0x88 x, +0x90 z) written X, Z over and over
for SECS (until the sprite is gone), so a dismissal's FUN_004fa470 reads that place (Q900). Prints how many writes and the last +0x8c it saw. Stop the original afterwards."""
import struct, sys, time
pid, X, Z, secs = int(sys.argv[1]), float(sys.argv[2]), float(sys.argv[3]), float(sys.argv[4]); kind = int(sys.argv[5]) if len(sys.argv) > 5 else 6
f = open(f'/proc/{pid}/mem', 'r+b', buffering=0)
def rd(a, fmt='<I'):
    f.seek(a); v = struct.unpack(fmt, f.read(struct.calcsize(fmt))); return v[0] if len(v) == 1 else v
def wr(a, fmt, *v): f.seek(a); f.write(struct.pack(fmt, *v))
table, n = rd(0x007b49f0), rd(0x007b49f4); slot = s = None
for i in range(1, n):
    a = rd(table + 4 * i)
    if a and rd(a + 0xac) == kind: slot, s = i, a; break
assert s, 'no sprite of that kind'
print(f'holding slot {slot} kind {kind} at ({X}, {Z})', flush=True)
t0 = time.time(); writes = 0; off = None
while time.time() - t0 < secs:
    try:
        if rd(table + 4 * slot) != s: print('the sprite is gone', flush=True); break
        wr(s + 0x88, '<f', X); wr(s + 0x90, '<f', Z); writes += 1; off = rd(s + 0x8c, '<f')
    except Exception as e: print('stopped:', e, flush=True); break
print(f'{writes} writes in {time.time() - t0:.1f}s; last +0x8c {off}', flush=True)
