#!/usr/bin/env python3
"""floormats.py PID NAME... - every model material in the ORIGINAL's memory whose texture is a named cache entry: the
source material record (rec+4 = material object M, M+8 = texture slots, slot[0] = the cache's texture), its flag word
(0x800 = drawn opaque, exe/render-states.md), counted by texture."""
import sys, struct, collections
pid = int(sys.argv[1]); want = {n.lower() for n in sys.argv[2:]}
m = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def u(a): m.seek(a); return struct.unpack('<I', m.read(4))[0]
def cstr(a): m.seek(a); return m.read(64).split(b'\0')[0].decode('latin-1')
cache = u(0x0087a268); tex = {}
for i in range(u(cache + 0x10000)):
    nm = cstr(u(cache + 16 * i + 4)).lower().rsplit('.', 1)[0]
    if nm in want: tex[u(cache + 16 * i + 8)] = nm
regions = []
for line in open(f'/proc/{pid}/maps'):
    a, b = (int(x, 16) for x in line.split()[0].split('-'))
    if 'rw' in line.split()[1] and a < 0x7f000000:
        try: m.seek(a); d = m.read(b - a); regions.append((a, struct.unpack(f'<{len(d) // 4}I', d[:len(d) // 4 * 4])))
        except OSError: pass
slots = {a + 4 * i: tex[v] for a, arr in regions for i, v in enumerate(arr) if v in tex}
mats = {a + 4 * i - 8: slots[v] for a, arr in regions for i, v in enumerate(arr) if v in slots}
recs = {a + 4 * i - 4: mats[v] for a, arr in regions for i, v in enumerate(arr) if v in mats}
c = collections.Counter()
for r, nm in sorted(recs.items()):
    fl = u(r); c[(nm, bool(fl & 0x800))] += 1
    print(f'{r:#x} {nm} flags {fl:#06x}')
print('by texture (with 0x800 / without):')
for nm in sorted({k[0] for k in c}): print(f'  {nm}: {c[(nm, True)]} / {c[(nm, False)]}')
print(f'total {sum(v for k, v in c.items() if k[1])} / {sum(v for k, v in c.items() if not k[1])}')
