#!/usr/bin/env python3
"""lobbystrike.py PID SECS [MASK] - the ORIGINAL's lobby, WRITTEN: finds the island lobby camera object (vtable 0x00702ec0 or
0x00702cb0, the wander box centre 500 / extent 400 as attractcam.py), lists the islands (index, address, centre +0x3c, lightning
mask +0x5c), takes the camera's nearest island ([camera+0xc], the one whose position the strike test 0x005e0f58..0x005e0f60
strikes at) and HOLDS its lightning mask at MASK (default 63, Hallow's: a strike on 1 frame in 64) for SECS, writing again whenever it
reads otherwise, then puts back what it held. A line a write, with the wall clock. Stop the original afterwards; never save under it.
How a storm is had on Lost Kingdom's island, which never strikes by itself (its LIGHTNING is 0). Q1037."""
import re, struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]); mask = int(sys.argv[3], 0) if len(sys.argv) > 3 else 63
f = open(f'/proc/{pid}/mem', 'r+b', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def rdf(a, n): f.seek(a); return struct.unpack('<%df' % n, f.read(4 * n))
def wr(a, v): f.seek(a); f.write(struct.pack('<I', v))
def find():
    hits = []
    for line in open(f'/proc/{pid}/maps'):
        m = re.match(r'([0-9a-f]+)-([0-9a-f]+) rw', line)
        if not m: continue
        lo, hi = int(m[1], 16), int(m[2], 16)
        if hi - lo > 256 << 20 or lo >= 0x80000000: continue
        try: f.seek(lo); buf = f.read(hi - lo)
        except OSError: continue
        for vt in (b'\xc0\x2e\x70\x00', b'\xb0\x2c\x70\x00'):
            i = buf.find(vt)
            while i >= 0:
                a = lo + i; i = buf.find(vt, i + 4)
                if a & 3: continue
                try:
                    lst = rd(a + 8); n = rd(lst + 0xc); bx = rdf(a + 0x16 * 4, 6)
                except (OSError, struct.error): continue
                if 0 < n < 10 and abs(bx[0] - 500) < 1 and abs(bx[3] - 400) < 1: hits.append(a)
    return hits
t0 = time.time(); obj = None
while obj is None and time.time() - t0 < 30:
    h = find()
    if h: obj = h[0]
    else: time.sleep(0.2)
if obj is None: print('no camera object'); sys.exit(1)
isl = []; node = rd(rd(obj + 8) + 4)
while node:
    ip = rd(node); isl.append(ip); node = rd(node + 4)
for k, ip in enumerate(isl):
    c = rdf(ip + 0x3c, 3); print(f'island {k} 0x{ip:08x} centre {c[0]:.1f} {c[1]:.1f} {c[2]:.1f} lightning {rd(ip + 0x5c)}', flush=True)
cur = rd(obj + 0xc); idx = isl.index(cur) if cur in isl else -1
print(f'camera 0x{obj:08x} nearest island {idx} 0x{cur:08x} state(+0x14) {rd(obj + 0x14)}', flush=True)
a = cur + 0x5c; was = rd(a); t1 = time.time(); n = 0
print(f'wall {t1:.3f} mask was {was} -> {mask}', flush=True)
while time.time() - t1 < secs:
    now = rd(a)
    if now != mask:
        wr(a, mask); n += 1
        if n <= 3: print(f'wall {time.time():.3f} read {now}, written {mask}; readback {rd(a)}', flush=True)
    time.sleep(0.02)
wr(a, was); print(f'wall {time.time():.3f} put back {was}; writes {n}; readback {rd(a)}', flush=True)
