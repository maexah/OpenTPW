#!/usr/bin/env python3
"""origscripts.py PID SECS [EVERY] - every script of the running ORIGINAL, read-only (a POLL for loadfile.sh and fsload.sh).

A script is a node of the list at [0x008791b0] (docs/exe/saves.md, "A script's record is its struct"): the handle at +8, the
program counter at +0x3c, the thing at +0xac (low word; 0 for a companion), and the links to other scripts: +0x0c its child
(SPAWNCHILD), +0x10 its parent, +0x14 its sound child (SPAWNSOUND). mGameTick is [[0x0080239c] + 0x1da70c].
Every EVERY seconds (default 5) until SECS have gone, one block: `tick T scripts N (C with no thing)`, then one line a script,
`h HANDLE pc PC thing THING child C parent P sound S`. Compare the handles with this game's `scripts` console command."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]); every = float(sys.argv[3]) if len(sys.argv) > 3 else 5.0
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def snap():
    out = []; s = rd(0x008791b0); k = 0
    while s and k < 4000:
        k += 1
        out.append((rd(s + 8), rd(s + 0x3c, 'i'), rd(s + 0xac) & 0xffff, rd(s + 0x0c), rd(s + 0x10), rd(s + 0x14)))
        s = rd(s)
    return out
start = time.time()
while time.time() - start < secs:
    try:
        tick = rd(rd(0x0080239c) + 0x1da70c); nodes = snap()
        print(f"tick {tick} scripts {len(nodes)} ({sum(1 for n in nodes if n[2] == 0)} with no thing)", flush=True)
        for h, pc, thing, child, parent, sound in sorted(nodes):
            print(f"h {h} pc {pc} thing {thing} child {child} parent {parent} sound {sound}", flush=True)
    except Exception as why:
        print("unreadable:", why, flush=True)
    time.sleep(every)
