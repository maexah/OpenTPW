#!/usr/bin/env python3
"""heads.py PID SECS ITEM - in the ORIGINAL's memory, every model of catalogue item ITEM: its lookup state (shared
flags, the count attached, each lookup record holding 0x2 and its handle) and, for each such handle, the sprite the
table at 0x007b49f0 holds on that slot (kind +0xac, bank +0xb0, state +0x18, word +0x08, flags +0xc4, shown +0x114,
place +0x88..+0x90, scale +0xa4, frame +0xb8). Polls every 0.1 s for SECS and prints each change of the lookup state
(a head's place and frame change every drawing and are printed, not compared).

A poller for loadfile.sh:  POLL='python3 tools/harness/original/heads.py %PID% 90 1104'"""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]); item = int(sys.argv[3])
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<I'):
    f.seek(a); return struct.unpack(fmt, f.read(struct.calcsize(fmt)))[0]
def spr(s):
    t, n = rd(0x007b49f0), rd(0x007b49f4)
    if not t or s < 1 or s >= n: return "past the table", ""
    a = rd(t + 4 * s)
    if not a: return "EMPTY", ""
    return (f"kind {rd(a + 0xac)} bank {rd(a + 0xb0)} state {rd(a + 0x18)} word {rd(a + 8)} flags {rd(a + 0xc4):x} shown {rd(a + 0x114)} slot {rd(a + 4)}",
            f"place ({rd(a + 0x88, '<f'):.1f},{rd(a + 0x8c, '<f'):.1f},{rd(a + 0x90, '<f'):.1f}) scale {rd(a + 0xa4, '<f'):.3f} frame {rd(a + 0xb8)}")
def models():
    out = {}; drawn = {}
    for slot in range(rd(0x007a4084) + rd(0x007a3ebc)):
        m = rd(0x007a4614 + 4 * slot)
        if not m or rd(m + 0xf8) != item: continue
        inst = rd(m + 8); mesh = rd(inst + 4); looks = rd(mesh + 0x48, '<H'); l = rd(inst + 0x28)
        shared, recs, count = rd(l), rd(l + 4), rd(l + 8)
        pairs = tuple((r, rd(recs + 20 * r), rd(recs + 20 * r + 12, '<i')) for r in range(looks))
        held = tuple((r, fl, h, spr(h)[0]) for r, fl, h in pairs if fl & 2)
        out[slot + 1] = (shared, count, held); drawn[slot + 1] = {h: spr(h)[1] for r, fl, h in pairs if fl & 2}
    return out, drawn
def show(t, s, drawn):
    w = rd(0x007cf83c)
    for h, (shared, count, held) in sorted(s.items()):
        print(f"{t:7.2f}s tick {rd(w + 0x1da70c)} handle {h} shared {shared} attached {count}")
        for r, fl, hd, sp in held: print(f"      record {r} flags {fl:#x} handle {hd}: {sp} {drawn[h].get(hd, '')}")
    if not s: print(f"{t:7.2f}s tick {rd(w + 0x1da70c)} no model of item {item}")
    sys.stdout.flush()
t0 = time.time(); last = None
while True:
    try: s, drawn = models()
    except Exception as e: s = None
    if s is not None and s != last: show(time.time() - t0, s, drawn); last = s
    if time.time() - t0 >= secs: break
    time.sleep(0.1)
