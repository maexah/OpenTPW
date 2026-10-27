#!/usr/bin/env python3
"""scriptvar.py PID THING [INDEX [VALUE]] - a variable of a thing's ride script in the running ORIGINAL: read, or WRITE.

  scriptvar.py PID 43            every variable of thing 43's script (by index: the .RSE's own order, rselisting prints the names)
  scriptvar.py PID 43 4          variable 4
  scriptvar.py PID 43 4 1        sets variable 4 to 1 and reads it back: the one tool here that WRITES the original's memory.
                                 It is how a ride is broken on demand (VAR_BREAKSTAT; docs/TOOLING.md, "Recipes").

A script is the node of the list at [0x008791b0] whose +0xac low word is the thing; its variables are the dwords at
[node + 0x1c], +0x8c of them (docs/exe/saves.md, "A script's record is its struct"). The companion scripts of a thing
(its sound's, +0xac the same thing) are listed too: pick by the count when reading. A VALUE is written only to the
FIRST node of the thing in the list's order; every later node is only read. Where that first node has no variables,
or fewer than INDEX, nothing is written at all: only a line saying "written" proves a write. object.py reads the
node with the most variables instead, so read first (no VALUE) and check the first line printed is that one."""
import struct, sys
pid = int(sys.argv[1]); thing = int(sys.argv[2]); idx = int(sys.argv[3]) if len(sys.argv) > 3 else None
val = int(sys.argv[4]) if len(sys.argv) > 4 else None
f = open(f'/proc/{pid}/mem', 'r+b' if val is not None else 'rb', buffering=0)
def rd(a, fmt='<I'):
    f.seek(a); return struct.unpack(fmt, f.read(struct.calcsize(fmt)))[0]
s = rd(0x008791b0); k = 0; hit = 0
while s and k < 4000:
    k += 1
    if rd(s + 0xac) & 0xffff == thing:
        n = rd(s + 0x8c); v = rd(s + 0x1c); hit += 1
        if not v or not 0 < n < 400: print(f"script {rd(s + 8)} of thing {thing}: no variables"); s = rd(s); continue
        f.seek(v); vs = struct.unpack(f'<{n}i', f.read(4 * n))
        if idx is None: print(f"script {rd(s + 8)} of thing {thing} at word {rd(s + 0x3c)}: {n} variables {list(vs)}")
        elif idx < n:
            if val is not None and hit == 1:
                f.seek(v + 4 * idx); f.write(struct.pack('<i', val))
                print(f"script {rd(s + 8)} of thing {thing}: variable {idx} was {vs[idx]}, written {val}, reads {rd(v + 4 * idx, '<i')}")
            else: print(f"script {rd(s + 8)} of thing {thing}: variable {idx} = {vs[idx]}")
    s = rd(s)
if not hit: sys.exit(f"no script of thing {thing} among {k}")
