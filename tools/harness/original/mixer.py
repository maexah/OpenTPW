#!/usr/bin/env python3
"""mixer.py PID [SECS] - the ORIGINAL's QMixer channels, read in QMixer.dll's own memory: the session (a 0x160
block, word 0x5432 at +0 and +0x15c, found by a scan of the writable maps), its listener (+0x58) and, for each
open channel (the array at +0xc, count at +8; open = byte +0x2c4), its flags (+0x200: 0x1000 the linear
distance arm), place (+0x1b4), mapping min/max/scale (+0x1e4), volume (+0x1f0), the mixer's distance (+0x24c),
cone gain (+0x258), distance gain (+0x25c), final gain (+0x260) and the 2D mark (+0x2c8). `law` is the gain
FUN_1800a980 gives that distance, to hold against the read; ' dirty' is byte +0x2c7 set. One block without SECS;
with it, read every 0.25 s, a block each time the session line (the channel count, the listener) changes or a channel
opens, closes or changes its flags, place or mapping: a change of volume, distance or gains alone prints nothing.
A POLL for loadfile.sh. Read-only. The game's voices by its own list are voices.py's."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]) if len(sys.argv) > 2 else 0
mem = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    mem.seek(a); return struct.unpack('<' + fmt, mem.read(struct.calcsize('<' + fmt)))
def session():
    for line in open(f'/proc/{pid}/maps'):
        p = line.split()
        if len(p) < 2 or not p[1].startswith('rw'): continue
        lo, hi = (int(v, 16) for v in p[0].split('-'))
        if hi > 0x80000000 or hi - lo > 0x4000000: continue
        try: mem.seek(lo); buf = mem.read(hi - lo)
        except OSError: continue
        for o in range(0, len(buf) - 0x160, 4):
            if buf[o:o + 2] == b'\x32\x54' and buf[o + 0x15c:o + 0x15e] == b'\x32\x54':
                n, arr = struct.unpack_from('<iI', buf, o + 8)
                if 0 < n <= 256 and arr > 0x10000: return lo + o
    return 0
def law(d, lo, hi, k, flags):
    if flags & 0x1000:
        if d > hi: return 0.0
        if d <= lo or k == 0: return 1.0
        return ((hi - d) / (hi - lo)) ** k
    if d > hi and not flags & 0x800: return 0.0
    d = min(d, hi)
    return lo / ((d - lo) * k + lo) if d > lo and k != 0 else 1.0
S = session()
def block():
    if not S: return ['no session found']
    n, arr = rd(S + 8, 'iI'); out = ['session 0x%x channels %d listener (%.0f,%.0f,%.0f)' % (S, n, *rd(S + 0x58, '3f'))]
    for i in range(n):
        c, = rd(arr + 4 * i)
        if not c or not rd(c + 0x2c4, 'B')[0]: continue
        flags, = rd(c + 0x200); x, y, z = rd(c + 0x1b4, '3f'); lo, hi, k = rd(c + 0x1e4, '3f'); vol, = rd(c + 0x1f0, 'f')
        d, = rd(c + 0x24c, 'f'); cone, dg, fin = rd(c + 0x258, '3f'); two, = rd(c + 0x2c8, 'B'); dirty, = rd(c + 0x2c7, 'B')
        out.append('channel %2d flags 0x%04x at (%.0f,%.0f,%.0f) map %.1f,%.1f,%.2f volume %.4f distance %.2f cone %.3f gain %.4f law %.4f final %.4f%s%s'
                   % (i, flags, x, y, z, lo, hi, k, vol, d, cone, dg, law(d, lo, hi, k, flags), fin, ' 2D' if two else '', ' dirty' if dirty else ''))
    return out
last = None; end = time.time() + secs
for _ in range(100000):
    try: b = block()
    except (OSError, struct.error): b = ['(unreadable)']
    key = [l.split(' volume')[0] for l in b]
    if key != last: print('--- %.2f' % time.time()); print('\n'.join(b), flush=True); last = key
    if time.time() >= end: break
    time.sleep(0.25)
