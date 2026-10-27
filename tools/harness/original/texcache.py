#!/usr/bin/env python3
"""texcache.py PID SECS [NAME=+MASK|-MASK|=VALUE ...] - the ORIGINAL's texture cache, read and optionally WRITTEN.

The cache is [0x0087a268] (FUN_00575870): up to 4096 entries of 16 bytes {folder ptr, name ptr, texture ptr, refs},
the count at +0x10000. A texture is FUN_005755f0's array of 0x60-byte records (one per frame of an animated one; the
count at -4), each record's flags at +8: 0x01000000, 0x10000000 for a .wct, 0x100 cut out (ref 240) / 0x200 graded
(ref 16) / 0x300 additive, 0x10000/0x20000 from a material's 0x10/0x20 (exe/render-states.md, exe/textures.md). The
state word is built from those flags per draw (FUN_00581db0 -> FUN_00581790), so a write shows on the next frame.

With no NAME it lists every entry (folder, name, refs, records, each record's flags); LIST=substr narrows the list.
@ADDR=... writes the one record at ADDR (a page record or any other found outside the cache, e.g. by brk.sh on
0x00590c40) the same way. NAME=... (matched case-insensitively against the entry's name, '*' a prefix wildcard, e.g. 'm_grass*') writes every
record of every match: +MASK ORs, -MASK clears, =VALUE sets the 0x300 bits to VALUE (0, 0x100, 0x200, 0x300). Held
(written again whenever it reads otherwise) for SECS, then RESTORED to what it was unless KEEP=1; a line a write with
mGameTick. AFTER=FILE waits until FILE exists before the first write; DONE=FILE is touched once every write is in
place (a frame can be grabbed from then on)."""
import os, struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
f = open(f'/proc/{pid}/mem', 'r+b', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def cstr(a):
    if not a: return ''
    f.seek(a); return f.read(128).split(b'\0')[0].decode('latin-1')
def tick():
    try: return rd(rd(0x0080239c) + 0x1da70c)
    except (OSError, struct.error): return -1
def entries():
    cache = rd(0x0087a268); n = rd(cache + 0x10000); out = []
    for i in range(min(n, 4096)):
        d, nm, tex, refs = struct.unpack('<4I', (f.seek(cache + 16 * i), f.read(16))[1])
        recs = rd(tex - 4) if tex else 0
        out.append((i, cstr(d), cstr(nm), tex, refs, [tex + 0x60 * k for k in range(min(recs, 64))]))
    return cache, n, out
def match(pat, name):
    pat, name = pat.lower(), name.lower()
    return name.startswith(pat[:-1]) if pat.endswith('*') else name == pat
jobs = []
for arg in sys.argv[3:]:
    name, op = arg.split('=', 1)
    jobs.append((name, op[0] if op[0] in '+-' else '=', int(op[1:] if op[0] in '+-' else op, 0)))
cache, n, ents = entries()
print(f'tick {tick()}: cache {cache:#x}, {n} entries', flush=True)
flt = os.environ.get('LIST', '').lower()
for i, d, nm, tex, refs, recs in ents:
    if jobs and not any(match(j[0], nm) for j in jobs): continue
    if flt and flt not in (d + '\\' + nm).lower(): continue
    print(f'  [{i}] {d}\\{nm} refs {refs} texture {tex:#x} records {len(recs)} flags ' + ' '.join(f'{rd(r + 8):#010x}' for r in recs), flush=True)
if not jobs: sys.exit(0)
targets = {}
for name, kind, val in jobs:
    if name.startswith('@'):
        r = int(name[1:], 0); was = rd(r + 8)
        want = was | val if kind == '+' else was & ~val & 0xffffffff if kind == '-' else (was & ~0x300) | (val & 0x300)
        targets[r + 8] = (was, want, name)
for i, d, nm, tex, refs, recs in ents:
    for name, kind, val in jobs:
        if match(name, nm):
            for r in recs:
                was = rd(r + 8)
                want = was | val if kind == '+' else was & ~val & 0xffffffff if kind == '-' else (was & ~0x300) | (val & 0x300)
                targets[r + 8] = (was, want, f'{d}\\{nm}')
if os.environ.get('AFTER'):
    while not os.path.exists(os.environ['AFTER']): time.sleep(0.1)
t0 = time.time(); done = False
try:
    while time.time() - t0 < secs:
        for a, (was, want, label) in targets.items():
            if rd(a) != want:
                cur = rd(a); f.seek(a); f.write(struct.pack('<I', want))
                print(f'{time.time() - t0:7.2f} s tick {tick()}: {label} record {a - 8:#x} flags {cur:#010x} -> {want:#010x}', flush=True)
        if not done and os.environ.get('DONE'):
            open(os.environ['DONE'], 'w').close(); done = True
        time.sleep(0.02)
finally:
    if os.environ.get('KEEP') != '1':
        for a, (was, want, label) in targets.items():
            f.seek(a); f.write(struct.pack('<I', was))
        print(f'{time.time() - t0:7.2f} s tick {tick()}: restored {len(targets)} record(s)', flush=True)
