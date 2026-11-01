#!/usr/bin/env python3
"""matchgone.py POLL.log [KIND=0] - for each sprite of KIND that puffpoll.py saw gone, the formula's raw words (int(x*1024)>>4, int(z*1024)>>4 of its last place as float32)
and the effect-19 emitter that appeared within one tick; prints each and a count of those within 1 raw word."""
import re, struct, sys
kind = sys.argv[2] if len(sys.argv) > 2 else '0'
gone = []; new = []
for l in open(sys.argv[1]):
    m = re.search(rf'tick (\d+): sprite slot (\d+) kind {kind} gone; last place \(([-\d.]+), ([-\d.]+), ([-\d.]+)\)', l)
    if m: gone.append((int(m.group(1)), int(m.group(2)), float(m.group(3)), float(m.group(5))))
    m = re.search(r'tick (\d+): NEW effect 19 slot (\d+) count (\d+) raw \((\d+), (\d+), (\d+)\)', l)
    if m: new.append((int(m.group(1)), int(m.group(3)), int(m.group(4)), int(m.group(5)), int(m.group(6))))
f32 = lambda x: struct.unpack('<f', struct.pack('<f', x))[0]
hit = 0
for t, slot, x, z in gone:
    rx = int(f32(x) * 1024) >> 4; rz = int(f32(z) * 1024) >> 4
    m = [n for n in new if abs(n[0] - t) <= 1 and abs(n[2] - rx) <= 1 and abs(n[4] - rz) <= 1]
    print('gone tick', t, 'slot', slot, 'place', x, z, '-> formula raw', (rx, rz), 'emitter', m[0][2:] if m else None); hit += bool(m)
print(hit, 'of', len(gone), 'gone sprites of kind', kind, 'have an effect-19 emitter within 1 raw word of the formula')
