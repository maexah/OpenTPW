#!/usr/bin/env python3
"""leafpoll.py PID ITEM SECS [NODE] - a placed ride's first channel (role, clip entry, frame, length) and one named node's
local matrix (+0x10, 3 rows of 4 floats) in the running ORIGINAL, read-only, every ~10 ms for SECS seconds: one JSON line per poll
to stdout (wall time, role, clip, frame, length, the node's 9 rotation elements, its three row lengths). ITEM is the thing's
id at slot record +0xf8 (headpause.py's way into a ride's channel: slot list 0x007a4614, [m+8] instance, [inst+4] model header,
[m+0x10] the first channel: words 1, 2, 7, 8 = role, clip, length, frame); NODE is a node name (default jm_leaf, Mumbo's), found by walking
the header's tree from +0x78 (child +0xc, sibling +8, name pointer +0x54) as modelnodes.py does. The node's matrix is a 64-byte read, so one poll's
matrix is whole; the frame is read after it, so a pose between the two reads shows as a frame a step later. Q1004."""
import json, struct, sys, time
pid, item, secs = int(sys.argv[1]), int(sys.argv[2]), float(sys.argv[3]); want = (sys.argv[4] if len(sys.argv) > 4 else 'jm_leaf').lower()
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<I'):
    f.seek(a); return struct.unpack(fmt, f.read(struct.calcsize(fmt)))[0]
def name(n):
    p = rd(n + 0x54)
    if not p: return ''
    f.seek(p); return f.read(40).split(b'\0')[0].decode('ascii', 'replace')
def find(n):
    while n:
        if name(n).lower() == want: return n
        r = find(rd(n + 0xc))
        if r: return r
        n = rd(n + 8)
    return 0
def slot():
    for s in range(rd(0x007a4084) + rd(0x007a3ebc)):
        m = rd(0x007a4614 + 4 * s)
        if m and rd(m + 0xf8) == item: return m
node = 0; t0 = time.time()
while time.time() - t0 < secs:
    try:
        m = slot()
        if not m: time.sleep(0.05); continue
        if not node: node = find(rd(rd(rd(m + 8) + 4) + 0x78)); print('node', hex(node), flush=True)
        f.seek(node + 0x10); x = struct.unpack('<12f', f.read(48))
        ch = rd(m + 0x10)
        if not ch: time.sleep(0.01); continue
        f.seek(ch); w = struct.unpack('<16I', f.read(64))
        rows = [x[0:3], x[4:7], x[8:11]]
        print(json.dumps({'t': round(time.time(), 3), 'role': w[1], 'clip': w[2], 'length': struct.unpack('<f', struct.pack('<I', w[7]))[0],
              'frame': round(struct.unpack('<f', struct.pack('<I', w[8]))[0], 3), 'rows': [[round(v, 5) for v in r] for r in rows],
              'lens': [round(sum(v * v for v in r) ** 0.5, 5) for r in rows]}), flush=True)
    except (OSError, struct.error): node = 0
    time.sleep(0.008)
