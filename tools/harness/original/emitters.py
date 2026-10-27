#!/usr/bin/env python3
"""emitters.py PID SECS - in the ORIGINAL's memory, the particle system's live emitters (the image at 0x0080ced0 a park
file's particles module holds): the used chain from its head (0x00816d1c), each emitter's slot, count (+0xa), handle,
effect (+0xc), place in whole units (+0x14, +0x18, +0x1c over 64), its own velocity (+0x38, +0x3c, +0x40: a type 2's aim),
life (+0x20), particles alive (+0x8), its four rates (+0x68, signed bytes) and its most (+0x64) as the density left them,
and in each block's head the live density ([0x0080cef0], the header's +0x20 a loaded park file sets; Q703). Polls every
0.05 s for SECS; prints a block when the set of emitters (slot, count, effect, place) changes, headed by the next
count ([0x0080cee8]) and the free chain's head ([0x00816d20]), and once a second besides the particle counts and
the own velocity of the emitters of effects 37 and 73 only.

A poller for loadfile.sh:  POLL='python3 tools/harness/original/emitters.py %PID% 40'
The layout: FileFormats saves.md, "The particles module"."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<I'):
    f.seek(a); return struct.unpack(fmt, f.read(struct.calcsize(fmt)))[0]
def used():
    out = []; s = rd(0x00816d1c, '<h')
    for _ in range(120):
        if s < 0 or s >= 120: break
        e = 0x0080cefc + 0x140 * s
        out.append((s, rd(e + 0xa, '<H'), rd(e + 0xc, '<h'), (rd(e + 0x14, '<i') >> 6, rd(e + 0x18, '<i') >> 6, rd(e + 0x1c, '<i') >> 6),
                    rd(e + 0x20, '<i'), rd(e + 8, '<H'), (rd(e + 0x38, '<i'), rd(e + 0x3c, '<i'), rd(e + 0x40, '<i')),
                    struct.unpack('<4b', bytes(rd(e + 0x68 + i, '<B') for i in range(4))), rd(e + 0x64, '<h')))
        s = rd(e + 0xd0, '<h')
    return out
def tick():
    w = rd(0x007cf83c); return rd(w + 0x1da70c) if w else -1
t0 = time.time(); last = None; said = 0
while True:
    t = time.time() - t0
    try: u = used(); k = tick()
    except Exception: u = None
    if u is not None:
        key = [x[:4] for x in u]
        if key != last:
            print(f"{t:7.2f}s tick {k}: {len(u)} emitters in use, next count {rd(0x0080cee8, '<H')}, free head {rd(0x00816d20, '<h')}, density {rd(0x0080cef0, '<i')}")
            for s, c, eff, at, life, n, aim, rates, most in u: print(f"      slot {s:3} count {c:5} handle {c << 16 | s:#x} effect {eff:3} at {at} aim {aim} life {life} particles {n} rates {rates} most {most}")
            last = key; sys.stdout.flush()
        elif t - said >= 1.0:
            print(f"{t:7.2f}s tick {k}: particles " + " ".join(f"{s}:{n}" for s, c, eff, at, life, n, aim, *_ in u) + " aims " + " ".join(f"{s}:{aim[0]},{aim[1]},{aim[2]}" for s, c, eff, at, life, n, aim, *_ in u if eff in (37, 73))); said = t; sys.stdout.flush()
    if t >= secs: break
    time.sleep(0.05)
