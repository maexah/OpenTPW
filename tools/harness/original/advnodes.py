#!/usr/bin/env python3
"""advnodes.py PID SECS [SLOT] - the ORIGINAL's advisor model as drawn, read-only, a line a change (a POLL for
loadfile.sh and fsload.sh). Q597.

Slot SLOT's model (0x0079039c + 0xb0 x SLOT, default 1, the park's own advisor): its data (model +8), the renderable
(data +0x1c), whose two matrices ModelRenderable_Draw (0x0057a750) hands its screen-space arm: A at renderable +0x10
(the transform the vertices go through, then ClipToScreen_Fpu's divide by w) and B at +0x50; the node tree from
[[renderable +8] +0x78] (node +0 flags, +0x10 local 4x4 row-vector matrix, +8 next, +0xc first child; 0x0057a530).
Prints A and B once a change; each node, depth first, as its order, flags, basis lengths (rows 0-2), translation and
column 3, and the w its chain gives (column 3 of the product of every local matrix to the root and A): a line per node
whose basis lengths or column 3 changed by more than 0.005. Also the running clip's flags (0x00f79660) and the mode tick.
docs/exe/ui.md, "How he reaches the screen"."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]); slot = int(sys.argv[3]) if len(sys.argv) > 3 else 1
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def mat(a):
    f.seek(a); v = struct.unpack('<16f', f.read(64)); return [list(v[r * 4:r * 4 + 4]) for r in range(4)]
def mul(a, b):
    return [[sum(a[r][k] * b[k][c] for k in range(4)) for c in range(4)] for r in range(4)]
def lens(m):
    return tuple(round(sum(x * x for x in m[r][:3]) ** 0.5, 4) for r in range(3))
def fmt(m):
    return ' | '.join(' '.join(f'{x:.4g}' for x in row) for row in m)
def walk(node, parent, out, depth=0):
    n = 0
    while node and n < 64:
        local = mat(node + 0x10); chain = mul(local, parent)
        out.append((depth, node, rd(node), local, chain))
        if not rd(node) & 0x20 and rd(node + 0xc):
            walk(rd(node + 0xc), chain, out, depth + 1)
        node = rd(node + 8); n += 1
t0 = time.time(); lastA = lastB = None; last = {}
while time.time() - t0 < secs:
    try:
        tick = rd(rd(0x0080239c) + 0x1da70c)
        model = rd(0x0079039c + 0xb0 * slot)
        if not model:
            time.sleep(0.05); continue
        data = rd(model + 8); rend = rd(data + 0x1c)
        A = mat(rend + 0x10); B = mat(rend + 0x50)
        nodes = []; walk(rd(rd(rend + 8) + 0x78), A, nodes)
        clip = hex(rd(0x00f79660))
    except (OSError, struct.error) as e:
        print(f'{time.time() - t0:7.2f} gone ({e})', flush=True); break
    s = f'{time.time() - t0:7.2f} tick {tick} clip {clip}'
    if A != lastA or B != lastB:
        print(f'{s} model {model:#x} data {data:#x} renderable {rend:#x} drawflags {rd(rd(rend + 8) + 0x30):#x}\n'
              f'    A {fmt(A)}\n    B {fmt(B)}', flush=True)
        lastA, lastB = A, B
    for i, (depth, node, flags, local, chain) in enumerate(nodes):
        key = (lens(local), tuple(round(local[r][3], 4) for r in range(4)))
        old = last.get(i)
        if old is None or any(abs(a - b) > 0.005 for a, b in zip(old[0] + old[1], key[0] + key[1])):
            print(f'{s} node {i:2d} d{depth} {node:#x} flags {flags:#x} lens {key[0]} t ({local[3][0]:.3f} {local[3][1]:.3f} '
                  f'{local[3][2]:.3f}) col3 {key[1]} chainlens {lens(chain)} chainw {tuple(round(chain[r][3], 4) for r in range(4))}',
                  flush=True)
            last[i] = key
    time.sleep(0.03)
