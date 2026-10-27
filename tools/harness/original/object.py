#!/usr/bin/env python3
"""object.py PID SECS THING [VAR ...] - one object of the running ORIGINAL, read-only, a line a change (a POLL for loadfile.sh).

  object.py %PID% 90 43            mState, mCanLoad, the guest being loaded, the head of the queue, the member assigned,
                                   the State of repair and the remaining life, with mGameTick and the seconds since the start
  object.py %PID% 90 43 4 6 7 9    and those variables of its ride script (the .RSE's own order: 4 VAR_BREAKSTAT,
                                   6 VAR_RIDECLOSED, 7 VAR_BROKEN, 9 VAR_RUNNING of the common twelve)

The object is [0x007cfb90 + thing * 0x14] (model byte +2 is 3): mState +0x19c, mCanLoad +0x68, the guest being loaded
+0x6c, mFirstInQ +0x3c, the assigned member +0x5e, the State of repair +0x44, the remaining life +0x48
(docs/exe/ride-operation.md, "Object fields"). Its script is the node of [0x008791b0] naming the thing at +0xac with
the most variables (original/scriptvar.py). mGameTick is [[0x0080239c] + 0x1da70c]."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]); thing = int(sys.argv[3]); want = [int(v) for v in sys.argv[4:]]
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def variables():
    s = rd(0x008791b0); k = 0; best = None
    while s and k < 4000:
        k += 1
        if rd(s + 0xac) & 0xffff == thing:
            n = rd(s + 0x8c); v = rd(s + 0x1c)
            if v and 0 < n < 400 and (best is None or n > best[0]): best = (n, v)
        s = rd(s)
    if best is None: return None
    f.seek(best[1]); vs = struct.unpack(f'<{best[0]}i', f.read(4 * best[0])); return [vs[i] if i < best[0] else None for i in want]
def snap():
    e = rd(0x007cfb90 + thing * 0x14)
    if not e or rd(e + 2, 'B') != 3: return None
    return dict(state=rd(e + 0x19c, 'i'), canload=rd(e + 0x68, 'i'), loading=rd(e + 0x6c, 'H'), first=rd(e + 0x3c, 'H'), staff=rd(e + 0x5e, 'H'),
                repair=round(rd(e + 0x44, 'f'), 2), life=round(rd(e + 0x48, 'f'), 2), vars=variables())
t0 = time.time(); last = None
while time.time() - t0 < secs:
    try: now = snap(); tick = rd(rd(0x0080239c) + 0x1da70c)
    except Exception as x: now = None; tick = -1
    if now != last: print(f"{time.time() - t0:7.2f} s tick {tick}: {now if now else 'no object ' + str(thing)}", flush=True); last = now
    time.sleep(0.05)
