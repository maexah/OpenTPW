#!/usr/bin/env python3
"""The interface's faces the original's face test drops: a UI mesh's faces turned away on the screen (Q608).

A UI control's mesh is a renderable like any model (`FUN_00476f10` -> `Renderable_SetMatrix`, mode 1), so its faces go
through `DrawList_AddMaterialFaces` and `Face_KeptByWinding` (`0x00581320`; render-states.md, "The software back-face
test"). Its matrix (`FUN_00476c50`) is affine with a positive x scale (2 / 2048 over the fit) and a positive y scale
(2 / 1536 over the fit), and `Renderable_SetMatrix` (`0x0057a260`) multiplies it by diag(1, -1, 1, 1), so at the test the
file's y is turned over and a face is kept exactly when its corners a, b, c (the face record's +2, +4, +6, through the
vertex order) turn clockwise in the file's (x, y), y up, after the node chain: (x1 - x0)(y2 - y0) - (x2 - x0)(y1 - y0) < 0
(the test's det > 0 of the clip x, y, w with y negated). OpenTPW's `UiMesh` draws both sides.

    uiwinding.py <ui.wad>                 a line a mesh with any face turned away, then the totals
    uiwinding.py <ui.wad> <member.md2>    one model: each mesh's kept, dropped and edge-on counts, and the dropped faces

Importable: `winding( blob )` -> [(mesh name, kept, dropped, edge-on, [dropped face indices])]; `placed( blob )` ->
[(mesh name, [(face index, three (x, y) points)])] and `turned( points )` (-1 kept, 1 dropped, 0 edge-on).
"""
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import wadcat  # noqa: E402


def _matmul(a, b):
	return [[sum(a[i][k] * b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]


def placed(blob):
	"""Each mesh: (name, [(face index, (x, y) a, b, c)]), its points through the node chain, the file's (x, y)."""
	u16 = lambda at: struct.unpack_from("<H", blob, at)[0]
	u32 = lambda at: struct.unpack_from("<I", blob, at)[0]
	if blob[:4] != b"\x46\x5d\xd1\x1c" or u32(4) != 0xDD or u32(0x70) == 0:
		return []
	nodes, mesh_count = u16(0x42), u16(0x44)
	mesh_table, node_table = u32(0x70), u32(0x74)

	def record(n):
		return mesh_table + n * 0xa0 if n < mesh_count else node_table + (n - mesh_count) * 0x58

	def index_of(p):
		if p == 0:
			return None
		if mesh_table <= p < mesh_table + mesh_count * 0xa0:
			return (p - mesh_table) // 0xa0
		return mesh_count + (p - node_table) // 0x58

	def name(n):
		at = u32(record(n) + 0x54)
		end = blob.find(b"\0", at)
		return blob[at:end].decode("latin-1") if 0 < at < len(blob) else "?"

	def local(n):
		m = struct.unpack_from("<16f", blob, record(n) + 0x10)
		return [list(m[r * 4:r * 4 + 4]) for r in range(4)]

	def world(n, guard=64):
		# Row vectors, as ModelFile: world = local * parent's world.
		parent = index_of(u32(record(n) + 4))
		if parent is None or guard == 0 or parent == n or parent >= nodes + mesh_count:
			return local(n)
		return _matmul(local(n), world(parent, guard - 1))

	out = []
	for i in range(mesh_count):
		at = record(i)
		vcount, _mcount, fcount, olen = struct.unpack_from("<HHHH", blob, at + 0x58)
		vdata, _normals, _uv, _mats, faces = struct.unpack_from("<IIIII", blob, at + 0x60)
		order = struct.unpack_from("<%dH" % olen, blob, u32(at + 0x94)) if olen else ()
		w = world(i)
		points = []
		for v in range(vcount):
			block = vdata + (v >> 2) * 0x30
			x, y, z = (struct.unpack_from("<f", blob, block + k * 0x10 + (v & 3) * 4)[0] for k in range(3))
			points.append((x * w[0][0] + y * w[1][0] + z * w[2][0] + w[3][0], x * w[0][1] + y * w[1][1] + z * w[2][1] + w[3][1]))
		tris = []
		for f in range(fcount):
			_word, a, b, c = struct.unpack_from("<HHHH", blob, faces + f * 8)
			try:
				tris.append((f, tuple(points[order[k]] for k in (a, b, c))))
			except IndexError:
				continue
		out.append((name(i), tris))
	return out


def turned(tri):
	"""-1 kept by the original's test, 1 dropped, 0 edge-on: the sign of the file's (x, y) winding (module doc)."""
	(x0, y0), (x1, y1), (x2, y2) = tri
	det = (x1 - x0) * (y2 - y0) - (x2 - x0) * (y1 - y0)
	return (det > 0) - (det < 0)


def winding(blob):
	out = []
	for mesh, tris in placed(blob):
		signs = [(f, turned(t)) for f, t in tris]
		out.append((mesh, sum(1 for _f, s in signs if s < 0), sum(1 for _f, s in signs if s > 0),
			sum(1 for _f, s in signs if s == 0), [f for f, s in signs if s > 0]))
	return out


def main():
	members = wadcat.members(open(sys.argv[1], "rb").read())
	if len(sys.argv) == 3:
		blob = next(b for m, b in members if m.lower().endswith(sys.argv[2].lower()))
		for mesh, kept, dropped, edge, gone in winding(blob):
			print("%-24s kept %4d dropped %4d edge-on %3d  %s" % (mesh, kept, dropped, edge, ",".join(map(str, gone))))
		return
	models = meshes = faces = drop = edges = touched = touched_meshes = 0
	for member, blob in members:
		if not member.lower().endswith(".md2"):
			continue
		rows = winding(blob)
		if not rows:
			continue
		models += 1
		hit = False
		for mesh, kept, dropped, edge, _gone in rows:
			meshes += 1
			faces += kept + dropped + edge
			drop += dropped
			edges += edge
			if dropped:
				touched_meshes += 1
				hit = True
				print("%-28s %-24s kept %4d dropped %4d edge-on %3d" % (member.split("\\")[-1], mesh, kept, dropped, edge))
		touched += hit
	print("models %d, meshes %d, faces %d: %d turned away (in %d meshes of %d models), %d edge-on" % (
		models, meshes, faces, drop, touched_meshes, touched, edges))


if __name__ == "__main__":
	main()
