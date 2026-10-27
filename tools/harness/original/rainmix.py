#!/usr/bin/env python3
"""rainmix.py PID SECS - the ORIGINAL's rain beside its own mixer channels, one stamped log (Q301). Every 20 ms: the
weather thing 7's quality (+0xc), forecast (+0x28), drops (+0x30), target (+0x34), lightning (+0x38) and its own rain
handle (+0x54, mRainSoundHandle); the global rain handle ([0x00803aac], FUN_0051e830's); every voice of class
0x0070a498 whose first controller key is 8 (the rain's two: keys, values, the current variation's volume bytes, key1
`+0x16` and mask `+0x18`, the voice's flags); and every open 2D channel of QMixer's session under 40 of 127 (its far
end, volume as a whole number of 127 and frequency `+0x1f4`). A line each time any of it changes, stamped with the
wall clock and mGameTick. Read-only. A POLL for loadfile.sh; hold the forecast with thingpoke.py beside it
(`15 7:0x28:i=1` makes the next weather change the heaviest rain). crowdmix.py is the same for the crowd's voice."""
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
                    if 0x10000 < c < 0x80000000 and rd(c, 'B') == 8: out.append(a + i)
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
        out.append('ch%d far %.0f vol %d freq %d' % (i, hi, n127, rd(c + 0x1f4)))
    return out
end = time.time() + secs; last = None; vs = []; scan = 0
while time.time() < end:
    try:
        world = rd(0x007cf83c); tick = rd(world + 0x1da70c)
        w = rd(0x007cfb90 + 7 * 0x14)
        if rd(w + 2, 'B') != 15: sys.exit('thing 7 is not the weather')
        q, fc, drops, target = rd(w + 0xc, 'i'), rd(w + 0x28, 'i'), rd(w + 0x30, 'i'), rd(w + 0x34, 'i')
        own = rd(w + 0x54); glob = rd(0x00803aac)
        if (own or glob) and time.time() > scan: vs = voices(); scan = time.time() + 1
        if not own and not glob: vs = []
        vt = []
        for v in vs:
            if rd(v) != 0x0070a498: continue
            c = rd(v + 0x4c)
            if not 0x10000 < c < 0x80000000 or rd(c, 'B') != 8: continue
            keys = [rd(c + i, 'B') for i in range(4)]; vals = [rd(c + 4 + i, 'B') for i in range(4)]; var = rd(v + 8)
            vt.append('voice %#x keys %s values %s var %#x vol %s key1 %s mask1 %s flags %#x' % (v, keys, vals, var,
                (rd(var + 0xc, 'B'), rd(var + 0xd, 'B')) if var else None, rd(var + 0x16, 'B') if var else None,
                rd(var + 0x18, 'H') if var else None, rd(v + 0x30)))
        now = (q, fc, drops, target, rd(w + 0x38, 'B'), own, glob, tuple(vt), tuple(channels()))
        if now != last:
            print('%.3f tick %d quality %d forecast %d drops %d target %d lightning %d own %#x global %#x | %s | %s' % (
                (time.time(), tick) + now[:7] + (' ; '.join(vt), ' , '.join(now[8]))), flush=True)
            last = now
    except SystemExit: raise
    except Exception as e:
        print('%.3f read failed %s' % (time.time(), e), flush=True)
    time.sleep(0.02)
