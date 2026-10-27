#!/usr/bin/env python3
"""floormesh.py PID - every loaded model instance in the ORIGINAL's memory with its procedural base as the loader left
it: the model's .md2 header (found by its magic, version 0xDD, a mesh table), each instance M whose +4 is that header,
M+0xc the base vertices FUN_00469a80 counted, M+0x2c the floor mesh it picked (by index and name; 0 = none), and the
root mesh's material words (0x800 = drawn opaque). exe/textures.md, "Which materials the loader makes opaque"."""
import sys, struct, collections
pid = int(sys.argv[1])
m = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, n):
    m.seek(a); return m.read(n)
def u32(a): return struct.unpack('<I', rd(a, 4))[0]
def u16(a): return struct.unpack('<H', rd(a, 2))[0]
def cstr(a, n=40):
    try: return rd(a, n).split(b'\0')[0].decode('latin-1')
    except OSError: return '?'
regions = []
for line in open(f'/proc/{pid}/maps'):
    a, b = (int(x, 16) for x in line.split()[0].split('-'))
    if 'rw' in line.split()[1] and a < 0x7f000000:
        try: regions.append((a, rd(a, b - a)))
        except OSError: pass
MAGIC = struct.pack('<II', 0x1CD15D46, 0xDD)
headers = {}
for a, d in regions:
    i = d.find(MAGIC)
    while i >= 0:
        h = a + i
        if i % 4 == 0 and i + 0x80 <= len(d):
            meshes, count = struct.unpack_from('<I', d, i + 0x70)[0], struct.unpack_from('<H', d, i + 0x44)[0]
            if meshes and 0 < count < 400: headers[h] = (cstr(h + 0x18, 20), meshes, count, struct.unpack_from('<I', d, i + 0x78)[0])
        i = d.find(MAGIC, i + 4)
want = set(headers)
found = collections.defaultdict(list)
for a, d in regions:
    words = struct.unpack(f'<{len(d) // 4}I', d[:len(d) // 4 * 4])
    for k, v in enumerate(words):
        if v in want: found[v].append(a + 4 * k - 4)
def meshname(table, rec): return cstr(u32(rec + 0x54), 24) if rec else '-'
for h, (name, table, count, root) in sorted(headers.items(), key=lambda kv: kv[1][0].lower()):
    for M in found[h]:
        try:
            n, floor = u16(M + 0xc), u32(M + 0x2c)
        except OSError: continue
        if floor and not (table <= floor < table + 0xa0 * count and (floor - table) % 0xa0 == 0): continue
        if n > 4 * 400: continue
        idx = (floor - table) // 0xa0 if floor else '-'
        mats = []
        if table <= root < table + 0xa0 * count:
            for g in range(u16(root + 0x5a)):
                try: mats.append(f'{u16(u32(u32(root + 0x6c) + 16 * g)):#06x}')
                except OSError: mats.append('?')
        print(f'{name:20s} header {h:#x} instance {M:#x} base {n:3d} floor {idx} {meshname(table, floor)}'
              f'  root {meshname(table, root)} words {" ".join(mats)}')
