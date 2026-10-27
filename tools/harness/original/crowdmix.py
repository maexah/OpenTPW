#!/usr/bin/env python3
"""crowdmix.py PID SECS - the ORIGINAL's crowd voice beside its own mixer channel, one stamped log (Q269). Every
20 ms: the level ([0x00803ab0]), the handle ([0x00803aa4]), the voice of class 0x0070a498 whose first controller
key is 7 (its keys, values, the current variation's key1 `+0x16`, mask `+0x18`, volume bytes, its weight as memory holds it `+0x1e`, the variation's
address and the voice's sample record `+0xc`), and every open 2D channel of QMixer's session that is not the music's (volume as a whole number of 127,
with its far end). A line each time any of it changes, stamped with the wall clock and mGameTick. Read-only.
The crowd's channel is the 2D one with a far end of 95 that is there while the handle is held (Q268).
A POLL for loadfile.sh. crowd.py (q236) and mixer.py are the two it is made of."""
import struct, sys, time, re
pid = int(sys.argv[1]); secs = float(sys.argv[2])
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); v = struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt))); return v[0] if len(v) == 1 else v
def maps():
    for line in open(f'/proc/{pid}/maps'):
        m = re.match(r'([0-9a-f]+)-([0-9a-f]+) rw', line)
        if not m: continue
        a, b = int(m.group(1), 16), int(m.group(2), 16)
        if b > 0x80000000 or b - a > 64 << 20: continue
        try: f.seek(a); yield a, f.read(b - a)
        except Exception: continue
def session():
    for lo, buf in maps():
        for o in range(0, len(buf) - 0x160, 4):
            if buf[o:o + 2] == b'\x32\x54' and buf[o + 0x15c:o + 0x15e] == b'\x32\x54':
                n, arr = struct.unpack_from('<iI', buf, o + 8)
                if 0 < n <= 256 and arr > 0x10000: return lo + o
    return 0
def voices():
    out = []
    for a, buf in maps():
        i = buf.find(b'\x98\xa4\x70\x00')
        while i >= 0:
            if i % 4 == 0:
                try:
                    c = rd(a + i + 0x4c)
                    if 0x10000 < c < 0x80000000 and rd(c, 'B') == 7: out.append(a + i)
                except Exception: pass
            i = buf.find(b'\x98\xa4\x70\x00', i + 1)
    return out
S = session()
def channels():
    out = []
    if not S: return ['no session']
    n, arr = rd(S + 8, 'iI')
    for i in range(n):
        c = rd(arr + 4 * i)
        if not c or not rd(c + 0x2c4, 'B') or not rd(c + 0x2c8, 'B'): continue
        flags = rd(c + 0x200); hi = rd(c + 0x1e8, 'f'); vol = rd(c + 0x1f0, 'f'); n127 = round(vol * 127)
        if not flags & 0x1000 or n127 >= 40: continue
        out.append('ch%d far %.0f vol %d' % (i, hi, n127))
    return out
end = time.time() + secs; last = None; vs = []; scan = 0
while time.time() < end:
    try:
        world = rd(0x007cf83c); tick = rd(world + 0x1da70c)
        level = rd(0x00803ab0, 'i'); handle = rd(0x00803aa4)
        if handle and (not vs or time.time() > scan): vs = voices(); scan = time.time() + 2
        if not handle: vs = []
        vt = []
        for v in vs:
            if rd(v) != 0x0070a498: continue
            c = rd(v + 0x4c); keys = [rd(c + i, 'B') for i in range(4)]; vals = [rd(c + 4 + i, 'B') for i in range(4)]
            var = rd(v + 8)
            vt.append('voice %#x keys %s values %s var %#x key1 %s mask1 %s vol %s flags %#x weight %s sample %#x' % (v, keys, vals, var,
                rd(var + 0x16, 'B') if var else None, rd(var + 0x18, 'H') if var else None,
                (rd(var + 0xc, 'B'), rd(var + 0xd, 'B')) if var else None, rd(v + 0x30), rd(var + 0x1e) if var else None, rd(v + 0xc)))
        now = (level, handle, tuple(vt), tuple(channels()))
        if now != last:
            print('%.3f tick %d level %d handle %#x | %s | %s' % (time.time(), tick, level, handle, ' ; '.join(vt), ' , '.join(now[3])), flush=True)
            last = now
    except Exception as e:
        print('%.3f read failed %s' % (time.time(), e), flush=True)
    time.sleep(0.02)
