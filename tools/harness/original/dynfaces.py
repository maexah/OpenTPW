#!/usr/bin/env python3
"""dynfaces.py PID MESH [out.json] [N] - the polygon faces of one dynamic-faces mesh of the running ORIGINAL, read-only.

FUN_0053c9c0 appends a face a record to the list at 0x00820b00 (0xa0 bytes; cap 0x806) every park frame, and the draw
FUN_0053c3f0 sets the count [0x00871ee0] back to nought at its end, so the count reads nought; the records stay where
they lie until the next frame writes them again. This reads the first N records (default 0x806) in ONE read, keeps
those whose mesh id (+0x90) is MESH - 1 WaterMesh (the sea, 134 a frame), 3 FlagsMesh (the gates' flags, 384) - and
prints their count, their texture words (+0x9c: 15-17 gby_sur1-3, 18/19 gte_lgo1) and their box. With out.json it
writes every kept record: index, word, the four corners (+0x00, x y z each), the four UVs (+0x30, u v of a 12-byte
step) and the four normals (+0x60), in the original's world axes (y up). docs/exe/park-engine.md, "The park's sea"
and "The gates' flags". A frame may land inside the read: compare two reads, or hold the mesh's phase first.

  dynfaces.py %PID% 3 flags.json      the flags as the last frame laid them
"""
import collections, json, struct, sys

pid, mesh = int(sys.argv[1]), int(sys.argv[2])
out = sys.argv[3] if len(sys.argv) > 3 and sys.argv[3] != '-' else None
n = int(sys.argv[4], 0) if len(sys.argv) > 4 else 0x806
LIST, SIZE = 0x00820b00, 0xa0
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
f.seek(LIST); raw = f.read(SIZE * n)
recs = []
for i in range(n):
	r = raw[i * SIZE:(i + 1) * SIZE]
	if struct.unpack_from('<i', r, 0x90)[0] != mesh: continue
	v = [list(struct.unpack_from('<3f', r, 12 * k)) for k in range(4)]
	uv = [list(struct.unpack_from('<2f', r, 0x30 + 12 * k)) for k in range(4)]
	nm = [list(struct.unpack_from('<3f', r, 0x60 + 12 * k)) for k in range(4)]
	recs.append({'index': i, 'word': r[0x9c], 'v': v, 'uv': uv, 'n': nm})
print(f'mesh {mesh}: {len(recs)} faces, indices {recs[0]["index"]}..{recs[-1]["index"]}' if recs else f'mesh {mesh}: no faces')
if recs:
	print('words', dict(sorted(collections.Counter(r['word'] for r in recs).items())))
	pts = [p for r in recs for p in r['v']]
	print('box x %.2f..%.2f y %.2f..%.2f z %.2f..%.2f' % tuple(g(p[k] for p in pts) for k in range(3) for g in (min, max)))
if out:
	json.dump(recs, open(out, 'w'))
	print('written', out)
