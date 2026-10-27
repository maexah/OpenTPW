#!/usr/bin/env python3
"""vehicles.py PID SECS - the running ORIGINAL's arrival vehicles and leavers, read-only, polled every 50 ms (a POLL for
loadfile.sh; Q627). A line a change of the vehicles' tuple, with mGameTick: the current vehicle word (+0x1da72a), the
three slots' things (+0x1da72c bus, +0x1da72e seaplane, +0x1da730 ferry), each made one's script VAR_TRIGGER (variable 0)
and VAR_STATUS (variable 1), the manager's still-to-drop (+0x2d0) and offloading byte (+0x2d4). A line each guest made
or gone (its last state +0x220 and cell). Every 60 s a census line: guests, leavers in states 0x12..0x15, made and gone
so far. At the end a summary: per vehicle, its visits to the arrivals' stop (status becomes 2) and to the leavers' stop
(status becomes 4), with the ticks. Compare with the count of this game's `the <vehicle>'s status A -> B on mGameTick N` log lines (q627/ours.py counts
those lines and asks the `vehicles` census only at the start and the end)."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
world = rd(0x007cf83c)
def tick(): return rd(world + 0x1da70c)
def script(thing):
    p = rd(0x007cfb90 + thing * 0x14)
    if not p: return None
    h = rd(p + 0x24); n = rd(0x008791b0); k = 0
    while n and k < 5000:
        k += 1
        if rd(n + 8) == h: return n
        n = rd(n)
    return None
NAMES = ('bus', 'seaplane', 'ferry')
def vehicles():
    cur = rd(world + 0x1da72a, 'H'); out = [cur]
    for i in range(3):
        t = rd(world + 0x1da72c + 2 * i, 'H'); s = script(t) if t else None; v = rd(s + 0x1c) if s else 0
        out.append((t, rd(v, 'i') if v else None, rd(v + 4, 'i') if v else None))
    out.append(rd(world + 0x2d0, 'i')); out.append(rd(world + 0x2d4, 'B'))
    return tuple(out)
def guests():
    g = {}
    for i in range(1, 1500):
        p = rd(0x007cfb90 + i * 0x14)
        if not p: continue
        try:
            if rd(p + 2, 'B') == 1: g[i] = (rd(p + 0x220, 'i'), (rd(p + 5, 'B'), rd(p + 7, 'B')))
        except (OSError, struct.error): pass
    return g
def show(v):
    return f"current {v[0]} " + " ".join(f"{NAMES[i]} {v[1 + i][0]}:t{v[1 + i][1]}/s{v[1 + i][2]}" for i in range(3)) + f" todrop {v[4]} off {v[5]}"
t0 = time.time(); last = None; lastg = guests(); made = gone = 0; census = t0
visits = {n: {2: [], 4: []} for n in NAMES}; start = tick()
print(f"start mGameTick {start}, {len(lastg)} guests", flush=True)
while time.time() - t0 < secs:
    try:
        v = vehicles(); g = guests(); tk = tick()
    except (OSError, struct.error):
        time.sleep(0.05); continue
    if v != last:
        print(f"{time.time() - t0:7.2f} s tick {tk}: {show(v)}", flush=True)
        for i, n in enumerate(NAMES):
            s = v[1 + i][2]; was = last[1 + i][2] if last else None
            if s in (2, 4) and s != was: visits[n][s].append(tk)
        last = v
    for i, (st, cell) in g.items():
        if i not in lastg: made += 1; print(f"tick {tk}: guest {i} MADE state {st:#x} on {cell}", flush=True)
    for i, (st, cell) in lastg.items():
        if i not in g: gone += 1; print(f"tick {tk}: guest {i} GONE from state {st:#x} on {cell}", flush=True)
    lastg = g
    if time.time() - census >= 60:
        census = time.time(); lv = sum(1 for s, _ in g.values() if 0x12 <= s <= 0x15)
        print(f"CENSUS {time.time() - t0:7.1f} s tick {tk}: guests {len(g)} leaving {lv} made {made} gone {gone}", flush=True)
    time.sleep(0.05)
end = tick()
print(f"end mGameTick {end} ({end - start} sweeps), guests {len(lastg)}, made {made}, gone {gone}")
for n in NAMES:
    print(f"VISITS {n}: arrivals' stop {len(visits[n][2])} {visits[n][2]}; leavers' stop {len(visits[n][4])} {visits[n][4]}")
