#!/usr/bin/env python3
"""groundcell.py PID SECS X,Y[:X2,Y2]=+MASK|-MASK|t@X,Y ... - the ORIGINAL's memory, WRITTEN: the park ground's heightfield
cell words (the base.MD2 heightfield [[0x007a0854] + 0x6c], cells at +0x2c, 4 bytes each: flags low 16, texture index
high 16; width +0x18, height +0x1c; FUN_0046df50 writes them, exe/textures.md and park.md). +MASK ORs the mask into
each cell's flag word of the box, -MASK clears it; held (written again whenever it reads otherwise) for SECS, a line a
write with mGameTick; t@X,Y copies cell X,Y's texture index instead (a positive control: a path cell's index shows at
once if the ground reads its cells again). A POLL for loadfile.sh. 0x101 is what FUN_0046df50 sets on a cell whose type FUN_00535dd0 answers
8 for (2, 4, 7, 9, 10, 21, 30: a built thing's footprint among them), Q359.
HF=engine writes the heightfield the terrain pass FUN_0056f670 draws from, [[0x00879d60]] (the engine's terrain object's
first word), instead of the base model's; the first line prints both pointers and the render word [0x008bcbc8] either
way. AFTER=FILE waits until FILE exists before the first write (a frame taken unpoked, then the same camera poked: Q360).
READ=X,Y[:X2,Y2] prints those cells' words in both heightfields once, before any write."""
import os, struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
f = open(f'/proc/{pid}/mem', 'r+b', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
jobs = []
for arg in sys.argv[3:]:
    box, m = arg.split('='); a, b = (box.split(':') + [box])[:2]
    x0, y0 = map(int, a.split(',')); x1, y1 = map(int, b.split(','))
    src = tuple(map(int, m[2:].split(','))) if m.startswith('t@') else None
    jobs.append((min(x0, x1), min(y0, y1), max(x0, x1), max(y0, y1), m[0] == '+', src or int(m[1:], 0)))
t0 = time.time(); said = False
while time.time() - t0 < secs:
    try:
        model = rd(0x007a0854); engine = rd(0x00879d60)
        if not model or not engine or not rd(engine): time.sleep(0.2); continue
        base = rd(model + 0x6c); drawn = rd(engine)
        hf = drawn if os.environ.get('HF') == 'engine' else base
        cells = rd(hf + 0x2c); w = rd(hf + 0x18); h = rd(hf + 0x1c)
        tick = rd(rd(0x0080239c) + 0x1da70c)
        if not said:
            print(f'{time.time() - t0:7.2f} s tick {tick}: base model heightfield {base:#x} cells {rd(base + 0x2c):#x}; terrain pass heightfield {drawn:#x} cells {rd(drawn + 0x2c):#x}; render word [0x008bcbc8] {rd(0x008bcbc8):#010x}', flush=True)
            print(f'{time.time() - t0:7.2f} s writing heightfield {hf:#x} cells {cells:#x} {w} x {h}', flush=True)
            if os.environ.get('READ'):
                a, b = (os.environ['READ'].split(':') + [os.environ['READ']])[:2]
                (rx0, ry0), (rx1, ry1) = map(int, a.split(',')), map(int, b.split(','))
                for y in range(min(ry0, ry1), max(ry0, ry1) + 1):
                    for x in range(min(rx0, rx1), max(rx0, rx1) + 1):
                        wb = rd(rd(base + 0x2c) + 4 * (y * rd(base + 0x18) + x)); wd = rd(rd(drawn + 0x2c) + 4 * (y * rd(drawn + 0x18) + x))
                        print(f'  cell ({x},{y}) base flags {wb & 0xffff:#06x} texture {wb >> 16}; drawn flags {wd & 0xffff:#06x} texture {wd >> 16}', flush=True)
            said = True
            if os.environ.get('AFTER'):
                while not os.path.exists(os.environ['AFTER']) and time.time() - t0 < secs: time.sleep(0.1)
                time.sleep(0.5); print(f'{time.time() - t0:7.2f} s {os.environ["AFTER"]} is there: writing now', flush=True)
        for x0, y0, x1, y1, on, mask in jobs:
            for y in range(y0, y1 + 1):
                for x in range(x0, x1 + 1):
                    if isinstance(mask, tuple):
                        a = cells + 4 * (y * w + x) + 2; was = rd(a, 'H'); want = rd(cells + 4 * (mask[1] * w + mask[0]) + 2, 'H')
                        if was != want:
                            f.seek(a); f.write(struct.pack('<H', want)); print(f'{time.time() - t0:7.2f} s tick {tick}: cell ({x},{y}) texture {was} -> {want}', flush=True)
                        continue
                    a = cells + 4 * (y * w + x); was = rd(a, 'H'); want = (was | mask) if on else (was & ~mask & 0xffff)
                    if was != want:
                        f.seek(a); f.write(struct.pack('<H', want))
                        print(f'{time.time() - t0:7.2f} s tick {tick}: cell ({x},{y}) flags {was:#06x} -> {want:#06x} texture {rd(a + 2, "H")}', flush=True)
    except (OSError, struct.error):
        pass
    time.sleep(0.2)
