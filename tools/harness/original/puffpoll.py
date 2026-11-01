#!/usr/bin/env python3
"""puffpoll.py PID SECS [EFFECT] - the ORIGINAL's effect-EFFECT (default 19) emitters as they appear, with the sprites near them.
Polls every ~30 ms: the used emitter chain (as emitters.py: head 0x00816d1c, emitter 0x0080cefc + 0x140*slot, next +0xd0, effect +0xc, raw place +0x14/+0x18/+0x1c = start >> 4)
and the sprite table (0x007b49f0, count 0x007b49f4; place +0x88/+0x8c/+0x90 floats, kind +0xac). A new emitter (slot, count) of the effect prints its raw place,
the place its formula implies back (raw/64), and every sprite whose last place was within 1.5 units in x and z, with that place and how long ago it was read;
a sprite slot that was there and is gone prints once with its last place."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]); eff = int(sys.argv[3]) if len(sys.argv) > 3 else 19
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<I'):
    f.seek(a); v = struct.unpack(fmt, f.read(struct.calcsize(fmt))); return v[0] if len(v) == 1 else v
def emitters():
    out = []; s = rd(0x00816d1c, '<h')
    for _ in range(120):
        if s < 0 or s >= 120: break
        e = 0x0080cefc + 0x140 * s
        out.append((s, rd(e + 0xa, '<H'), rd(e + 0xc, '<h'), rd(e + 0x14, '<3i'), rd(e + 0x20, '<i')))
        s = rd(e + 0xd0, '<h')
    return out
def tick():
    w = rd(0x007cf83c); return rd(w + 0x1da70c) if w else -1
last = {}; seen = set(); gone = set(); recheck = {}; t0 = time.time(); polls = 0
while time.time() - t0 < secs:
    t = time.time() - t0; polls += 1
    try:
        now = {}
        table, n = rd(0x007b49f0), rd(0x007b49f4)
        for i in range(1, n):
            s = rd(table + 4 * i)
            if not s: continue
            x, h, z = rd(s + 0x88, '<3f'); now[i] = (rd(s + 0xac), x, h, z, t)
        for i, v in last.items():
            if i not in now and (i, v[4]) not in gone:
                gone.add((i, v[4])); print(f'{t:8.2f}s tick {tick()}: sprite slot {i} kind {v[0]} gone; last place ({v[1]:.3f}, {v[2]:.3f}, {v[3]:.3f}) read {t - v[4]:.2f}s ago', flush=True)
        for i, v in now.items(): last[i] = v
        for s, c, e, raw, life in emitters():
            if e == eff and (s, c) not in seen:
                seen.add((s, c)); recheck[(s, c)] = 4
                print(f'{t:8.2f}s tick {tick()}: NEW effect {e} slot {s} count {c} raw {raw} = units ({raw[0]/64:.3f}, {raw[1]/64:.3f}, {raw[2]/64:.3f}) life {life}', flush=True)
                for i, v in last.items():
                    if abs(v[1] - raw[0] / 64) < 1.5 and abs(v[3] - raw[2] / 64) < 1.5:
                        print(f'            near sprite slot {i} kind {v[0]} place ({v[1]:.3f}, {v[2]:.3f}, {v[3]:.3f}) raw x,z would be ({int(v[1]*1024)>>4}, {int(v[3]*1024)>>4}) read {t - v[4]:.2f}s ago', flush=True)
        for (s, c), left in list(recheck.items()):
            e = 0x0080cefc + 0x140 * s
            if rd(e + 0xa, '<H') == c:
                raw = rd(e + 0x14, '<3i'); print(f'            slot {s} count {c} read again ({left} left): raw {raw}', flush=True)
            if left <= 1 or rd(e + 0xa, '<H') != c: del recheck[(s, c)]
            else: recheck[(s, c)] = left - 1
    except Exception as ex:
        print('read failed', ex, flush=True)
    time.sleep(0.03)
print(f'polls {polls}, emitters of effect {eff} seen {len(seen)}')
