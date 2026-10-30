#!/usr/bin/env python3
"""attractcam.py PID [SECS] [POLL_MS] - the ORIGINAL's lobby attract camera (FUN_005e0470's no-player path), a line a pass:
t, eye [0x1e..0x20], target [0x21..0x23], look point [0x2d..0x2f], look speed [0x2b], nearest island [3] (its index in the
list and centre +0x3c..+0x44), the delta [0x00f82884]+8, and the render camera's +0x50..+0x58 (copied from the island's
+0x64..+0x6c each pass). Read-only. Object: vtable 0x00702ec0 (derived) or 0x00702cb0 (base)."""
import re, struct, sys, time
SCENE, LENS = 0x00f82884, 0x00f83778
pid = int(sys.argv[1]); secs = float(sys.argv[2]) if len(sys.argv) > 2 else 60
poll = (float(sys.argv[3]) if len(sys.argv) > 3 else 1) / 1000
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def rdf(a, n): f.seek(a); return struct.unpack('<%df' % n, f.read(4 * n))
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
t0 = time.time(); print(f'start wall {t0:.6f}', flush=True)
obj = None
while obj is None and time.time() - t0 < secs:
    h = find()
    if h: obj = h[0]; print(f'object 0x{obj:08x} candidates {[hex(x) for x in h]}', flush=True)
    else: time.sleep(0.2)
if obj is None: print('none found'); sys.exit(1)
isl = []; node = rd(rd(obj + 8) + 4)
while node:
    ip = rd(node); isl.append((ip, rdf(ip + 0x3c, 3), rdf(ip + 0x64, 3))); node = rd(node + 4)
for k, (ip, c, sky) in enumerate(isl): print(f'island {k} 0x{ip:08x} centre {c[0]:.1f} {c[1]:.1f} {c[2]:.1f} sky {sky[0]:.3f} {sky[1]:.3f} {sky[2]:.3f}', flush=True)
idx = {ip: k for k, (ip, _, _) in enumerate(isl)}
last = None
while time.time() - t0 < secs:
    try:
        v = rdf(obj + 0x1e * 4, 6); lk = rdf(obj + 0x2d * 4, 3); sp = rd(obj + 0x2b * 4, 'f'); cur = rd(obj + 0xc)
        d = rd(rd(SCENE) + 8, 'f'); sky = rdf(rd(LENS) + 0x50, 3)
    except (OSError, struct.error): break
    key = v[:3] + lk
    if key != last:
        last = key
        print(f'{time.time()-t0:8.4f} eye {v[0]:.3f} {v[1]:.3f} {v[2]:.3f} tgt {v[3]:.1f} {v[4]:.1f} {v[5]:.1f} look {lk[0]:.3f} {lk[1]:.3f} {lk[2]:.3f} spd {sp:.4f} isl {idx.get(cur, -1)} d {d:.4f} sky {sky[0]:.3f} {sky[1]:.3f} {sky[2]:.3f}', flush=True)
    time.sleep(poll)
