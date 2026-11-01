#!/usr/bin/env python3
"""groundheight.py PID SECS X,Z=+D|-D|H ... - the ORIGINAL's memory, WRITTEN: the park ground's corner heights (the heightfield
[[0x007a0854] + 0x6c]: heights at +0x28, (width + 1) x (height + 1) floats, vertex X,Z at index Z * (width + 1) + X). X,Z=+D adds D
to the height the corner had when the script first read it, X,Z=-D subtracts it, X,Z=H (no sign) sets it; the new height is held
(written again whenever it reads otherwise) for SECS, a line a write with mGameTick, and the old height is put back when SECS is up
(or on a signal). HF=engine writes the terrain pass's heightfield [[0x00879d60]] instead. A POLL for loadfile.sh; Q914 raises a flat
cell's two off-diagonal corners to see which cut the cell's flag draws (spikecut.sh)."""
import os, signal, struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
f = open(f'/proc/{pid}/mem', 'r+b', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def wr(a, v):
    f.seek(a); f.write(struct.pack('<f', v))
jobs = []
for arg in sys.argv[3:]:
    pos, d = arg.split('='); x, z = map(int, pos.split(','))
    jobs.append((x, z, 'set' if d[0] not in '+-' else 'add', float(d)))
orig = {}; stop = False
def bye(*_):
    global stop; stop = True
signal.signal(signal.SIGTERM, bye); signal.signal(signal.SIGINT, bye)
def heights():
    model = rd(0x007a0854); engine = rd(0x00879d60)
    if not model or not engine or not rd(engine): return None
    hf = rd(engine) if os.environ.get('HF') == 'engine' else rd(model + 0x6c)
    return hf, rd(hf + 0x28), rd(hf + 0x18) + 1
t0 = time.time()
while time.time() - t0 < secs and not stop:
    try:
        h = heights()
        if not h: time.sleep(0.2); continue
        hf, hp, row = h; tick = rd(rd(0x0080239c) + 0x1da70c)
        for x, z, how, d in jobs:
            a = hp + 4 * (z * row + x); was = rd(a, 'f')
            if (x, z) not in orig: orig[(x, z)] = (a, was); print(f'{time.time() - t0:7.2f} s tick {tick}: heightfield {hf:#x} heights {hp:#x}; vertex ({x},{z}) reads {was}', flush=True)
            want = struct.unpack('<f', struct.pack('<f', d if how == 'set' else orig[(x, z)][1] + d))[0]   # as the game's float holds it
            if was != want: wr(a, want); print(f'{time.time() - t0:7.2f} s tick {tick}: vertex ({x},{z}) {was} -> {want}', flush=True)
    except (OSError, struct.error):
        pass
    time.sleep(0.2)
try:
    for (x, z), (a, was) in orig.items():
        wr(a, was); print(f'put back vertex ({x},{z}) -> {was}', flush=True)
except (OSError, struct.error):
    pass
