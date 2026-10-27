#!/usr/bin/env python3
"""money.py PID SECS THING... - the all-items screen's columns of objects of the running ORIGINAL, read-only, a line a change
(a POLL for loadfile.sh; Q368).

  money.py %PID% 90 16 14 43     each thing's customers last30, last month's profit, total profit, satisfaction and its bar,
                                 with mGameTick and the seconds since the start

The object is [0x007cfb90 + thing * 0x14] (model byte +2 is 3). Each day ring is mTemp, mData[30], mCurrentEntry,
mNumEntries, mWrappedAround: takings +0x70, costs +0xf8, customers +0x1a8, satisfaction +0x340; mTotalTakings +0x180,
mTotalCosts +0x184 (docs/exe/hud.md, "The all-items rows: a number, then bars"). Last month is FUN_004e1c70 (takings
less costs, the last min(filled, 30) days unsigned), the total FUN_004e1c60, the satisfaction FUN_004e1e30 (50 plus the
mean truncated toward nought, 50 with no finished day), the bar ((v & 0xff) << 10) // 100."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]); things = [int(v) for v in sys.argv[3:]]
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def last(e, at):
    """A ring's last min(filled, 30) finished days, newest first, signed."""
    days = struct.unpack('<30i', (f.seek(e + at + 4), f.read(120))[1]); entry = rd(e + at + 0x7c, 'i')
    num = rd(e + at + 0x80, 'i'); wrap = rd(e + at + 0x84, 'B'); filled = num if wrap else entry + 1
    return [days[(entry - i) % num] for i in range(min(30, filled))] if num else []
def wrap32(v): return (v + 2**31) % 2**32 - 2**31
def bar(v): return ((v & 0xff) << 10) // 100
def snap(thing):
    e = rd(0x007cfb90 + thing * 0x14)
    if not e or rd(e + 2, 'B') != 3: return None
    u = lambda ds: sum(d & 0xffffffff for d in ds)
    sat = last(e, 0x340); s = 50 + (int(sum(sat) / len(sat)) if sat else 0)
    return (u(last(e, 0x1a8)), wrap32(u(last(e, 0x70)) - u(last(e, 0xf8))), wrap32(rd(e + 0x180, 'i') - rd(e + 0x184, 'i')),
            s, bar(s), len(sat))
t0 = time.time(); prev = None
while time.time() - t0 < secs:
    try: now = {t: snap(t) for t in things}; tick = rd(rd(0x0080239c) + 0x1da70c)
    except Exception: now = None; tick = -1
    if now != prev:
        cols = ' | '.join(f"{t}: " + (f"C {v[0]} last {v[1]} total {v[2]} sat {v[3]} bar {v[4]} days {v[5]}" if v else 'none')
                          for t, v in (now or {}).items())
        print(f"{time.time() - t0:7.2f} s tick {tick}: {cols or 'unreadable'}", flush=True); prev = now
    time.sleep(0.05)
