#!/usr/bin/env python3
"""modeltypes.py PID [WAIT] - the ORIGINAL's memory, read-only, WAIT seconds on (default 0): how many animation channels
each model type and each made model holds, against the rule lib/modelchannels.py reads park files by (Q814;
docs/exe/saves.md, "How many channels a saved model has").

The types are 0x007ae6a8[1..[0x007a439c]] (FUN_004629d0 appends each): its id +0xf8 (the item id a saved record names),
its channel count u16 +0xe (FUN_004629d0's seventh argument), its file name [[type+8]+4]+0x18. The made models are the
slot list 0x007a4614, [0x007a4084] present + [0x007a3ebc] free: [inst] its type, +0xe its channel count (copied from the
type by FUN_004557c0), +0xe4 the flags it was made with. One line a type, then the models by id, made flags and count,
then two totals: the types whose count differs from modelchannels.counts() (an id it does not name is 1), and the models
whose count differs from their type's."""
import collections, os, struct, sys, time
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib"))
pid = int(sys.argv[1]); time.sleep(float(sys.argv[2]) if len(sys.argv) > 2 else 0)
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, n): f.seek(a); return f.read(n)
def u32(a): return struct.unpack('<I', rd(a, 4))[0]
def u16(a): return struct.unpack('<H', rd(a, 2))[0]
def cstr(a): return rd(a, 40).split(b'\0')[0].decode('ascii', 'replace')
def name(t):
	try: return cstr(u32(u32(t + 8) + 4) + 0x18)
	except Exception: return '?'
import modelchannels
rule = modelchannels.counts()
types = {}; off = 0
for i in range(1, u32(0x007a439c) + 1):
	t = u32(0x007ae6a8 + 4 * i)
	if not t: continue
	tid, ch = u32(t + 0xf8), u16(t + 0xe); types[t] = (tid, ch)
	want = rule.get(tid, 1); off += ch != want
	print(f'type {i:3} id {tid:5} channels {ch} rule {want}{"" if ch == want else "  DIFFERS"} {name(t)}')
models = collections.Counter(); moff = 0; slots = u32(0x007a4084) + u32(0x007a3ebc)
for s in range(slots):
	m = u32(0x007a4614 + 4 * s)
	if not m: continue
	t = u32(m); tid, tch = types.get(t, (-1, -1)); ch = u16(m + 0xe)
	models[(tid, u32(m + 0xe4), ch)] += 1; moff += ch != tch
for (tid, made, ch), n in sorted(models.items()): print(f'models id {tid:5} made {made:#x} channels {ch}: {n}')
print(f'{len(types)} types, {off} differ from the rule; {sum(models.values())} models of {slots} slots, {moff} differ from their type')
