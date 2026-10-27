#!/usr/bin/env python3
"""thingpoke.py PID SECS KIND THING:OFFSET:FMT=VALUE ... - HOLDS words of one or more things of the running ORIGINAL
at given values for SECS, writing each again every 50 ms where it reads otherwise. It WRITES the original's memory
(as scriptvar.py and breakq.py do): stop the original afterwards, and never save a park under it.

  thingpoke.py %PID% 150 15 7:0x38:B=1
        holds the weather thing's mLightning (thing 7, type 15) at 1, so a bolt falls every few seconds and its
        thunder follows (docs/exe/weather.md, the field map): how a storm is had on a clear day

KIND is the type byte every THING named must hold at +2 (15 for the weather, FUN_0050afe0's byte): a thing of another
type is refused and nothing is written, which is the guard against a wrong id. FMT is a struct letter (B, H, I, i, f).
A line each time a word is written, with what it read before, and a count at the end. A thing is
[0x007cfb90 + id * 0x14]; mGameTick is [[0x0080239c] + 0x1da70c]. A POLL for loadfile.sh, beside a reader
(thingwords.py reads the same words; mixfreq.py the mixer)."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]); kind = int(sys.argv[3], 0); want = []
for a in sys.argv[4:]:
    spec, val = a.split('='); p = spec.split(':'); fmt = p[2] if len(p) > 2 and p[2] else 'H'
    want.append((int(p[0]), int(p[1], 0), fmt, float(val) if fmt == 'f' else int(val, 0)))
f = open(f'/proc/{pid}/mem', 'r+b', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
t0 = time.time(); writes = 0
while time.time() - t0 < secs:
    try:
        for thing, off, fmt, val in want:
            base = rd(0x007cfb90 + thing * 0x14)
            if not base: continue
            if rd(base + 2, 'B') != kind: sys.exit(f'thing {thing} is of type {rd(base + 2, "B")}, not {kind}: nothing written')
            was = rd(base + off, fmt)
            if was != val:
                f.seek(base + off); f.write(struct.pack('<' + fmt, val)); writes += 1
                print(f'{time.time() - t0:7.2f} s tick {rd(rd(0x0080239c) + 0x1da70c)}: thing {thing} +{off:#x} was {was}, written {val}, reads {rd(base + off, fmt)}', flush=True)
    except (OSError, struct.error): pass
    time.sleep(0.05)
print(f'{writes} writes in {secs:.0f} s')
