#!/usr/bin/env python3
"""mixfreq.py PID SECS - the ORIGINAL's QMixer channels with each one's FREQUENCY (the word at +0x1f4 of its record:
22050 for a kids' sample at pitch 0, 22050 x 2^((p + 1) / 96) for a pitch of 1 to 5; Q269). Every 20 ms, each open
channel that is not 2D: its index, place, far end, volume as a whole number of 127 and frequency. A line each time a
channel opens or its place, far end, volume or frequency changes, stamped with the wall clock and mGameTick, and at
the end a count of the lines by far end, volume and frequency. `2d` as a third argument reads the 2D channels
instead (the crowd's, the music's). Read-only. A POLL for loadfile.sh. mixer.py prints the gains and no frequency;
crowdmix.py the crowd's voice beside its 2D channel. A ride's screams are the channels with a far end of 95 at the
middle of its cells (Q266), a child a channel (Q297)."""
import struct, sys, time, collections
pid = int(sys.argv[1]); secs = float(sys.argv[2]); flat = len(sys.argv) > 3 and sys.argv[3] == '2d'
mem = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    mem.seek(a); v = struct.unpack('<' + fmt, mem.read(struct.calcsize('<' + fmt))); return v[0] if len(v) == 1 else v
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
S = session()
if not S: sys.exit('no session found')
last = {}; counts = collections.Counter(); end = time.time() + secs
while time.time() < end:
    try:
        world = rd(0x007cf83c); tick = rd(world + 0x1da70c)
        n, arr = rd(S + 8, 'iI'); seen = set()
        for i in range(n):
            c = rd(arr + 4 * i)
            if not c or not rd(c + 0x2c4, 'B') or bool(rd(c + 0x2c8, 'B')) != flat: continue
            x, y, z = rd(c + 0x1b4, '3f'); far = rd(c + 0x1e8, 'f'); vol = round(rd(c + 0x1f0, 'f') * 127); freq = rd(c + 0x1f4)
            now = (round(x), round(y), round(z), round(far), vol, freq); seen.add(i)
            if last.get(i) != now:
                print('%.3f tick %d ch%d at (%d,%d,%d) far %d vol %d freq %d' % (time.time(), tick, i, *now), flush=True)
                last[i] = now; counts[now[3:]] += 1
        for i in list(last):
            if i not in seen: del last[i]
    except (OSError, struct.error) as e:
        print('%.3f read failed %s' % (time.time(), e), flush=True)
    time.sleep(0.02)
print('--- lines by far end, volume, frequency')
for (far, vol, freq), k in sorted(counts.items()): print('far %d vol %d freq %d: %d' % (far, vol, freq, k))
