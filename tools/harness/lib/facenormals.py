#!/usr/bin/env python3
"""A static model's face normals as the original works them out again, and which of them the lighter reads (Q819).

`FUN_00473a40` (park-engine.md, "Where a placed object's model stands", the face normals) takes each face record
(word, a, b, c) of a mesh node flagged 0x10000, its corners through the vertex order, and writes
normalise( (c - b) x (a - b) ) into the normals table (mesh +0x64) at the entry the word's low 15 bits name, or
(0, -1, 0) when that is not a number. The model lighter (`Light_ModelVertexColours`, 0x00574660) reads only the
table's first run, entry i for vertex-order entry i (mesh +0x5e entries). So a face whose word names an entry below
the vertex-order length overwrites a normal the lighter uses; one at or past it does not reach the lighting.

    facenormals.py <wad> <member.md2>     one model: a line a mesh with its counts, then each face naming a lit entry
    facenormals.py --sweep <data dir>     every .md2 (wad members and loose files): a line a mesh with any face naming a
                                          lit entry, then the totals

A mesh line: vertex-order length, faces, faces naming a lit entry ("lit"), and of those how many the recompute turns
by more than 1 degree from the file's entry ("turned"). Importable: `recomputed( mesh, corners )`, `lit_faces( mesh )`.
"""
import math
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import md2surf  # noqa: E402
import wadcat  # noqa: E402

DOWN = (0.0, -1.0, 0.0)


def recomputed(corner_a, corner_b, corner_c):
	"""FUN_00473a40's normal of one face: (c - b) x (a - b), normalised; (0, -1, 0) for a degenerate face."""
	e1 = (corner_a[0] - corner_b[0], corner_a[1] - corner_b[1], corner_a[2] - corner_b[2])
	e2 = (corner_c[0] - corner_b[0], corner_c[1] - corner_b[1], corner_c[2] - corner_b[2])
	x = e1[2] * e2[1] - e1[1] * e2[2]
	y = e1[0] * e2[2] - e1[2] * e2[0]
	z = e1[1] * e2[0] - e1[0] * e2[1]
	length = math.sqrt(x * x + y * y + z * z)
	if length == 0.0:
		return DOWN
	return (x / length, y / length, z / length)


def entry(model, mesh, index):
	return struct.unpack_from("<fff", model.blob, mesh.normals + index * 12)


def lit_faces(model, mesh):
	"""Each face whose normal word names an entry the lighter reads: (face, entry, file normal, recomputed, degrees)."""
	out = []
	olen = len(mesh.order)
	if not mesh.normals:
		return out
	for f, (word, a, b, c) in enumerate(mesh.faces):
		k = word & 0x7fff
		if k >= olen:
			continue
		was = entry(model, mesh, k)
		now = recomputed(model.corner(mesh, a), model.corner(mesh, b), model.corner(mesh, c))
		dot = sum(p * q for p, q in zip(was, now))
		norm = math.sqrt(sum(p * p for p in was)) or 1.0
		out.append((f, k, was, now, math.degrees(math.acos(max(-1.0, min(1.0, dot / norm))))))
	return out


def show(model):
	for mesh in model.meshes:
		lit = lit_faces(model, mesh)
		print("%-20s order %4d faces %4d lit %4d turned %4d" % (
			mesh.name, len(mesh.order), mesh.face_count, len(lit), sum(1 for x in lit if x[4] > 1.0)))
		for f, k, was, now, deg in lit:
			print("   face %4d entry %4d file (%.3f %.3f %.3f) recomputed (%.3f %.3f %.3f) %.1f deg" % (
				f, k, was[0], was[1], was[2], now[0], now[1], now[2], deg))


def sweep(data):
	files = meshes = with_lit = faces = lit = turned = 0
	for root, _dirs, found in os.walk(data):
		for name in sorted(found):
			path = os.path.join(root, name)
			if name.lower().endswith(".wad"):
				try:
					members = wadcat.members(open(path, "rb").read())
				except Exception:
					continue
			elif name.lower().endswith(".md2"):
				members = [(name, open(path, "rb").read())]
			else:
				continue
			for member, blob in members:
				if not member.lower().endswith(".md2") or blob[:4] != b"\x46\x5d\xd1\x1c":
					continue
				files += 1
				try:
					model = md2surf.Model(blob)
				except Exception:
					continue
				if not model.static:
					continue
				for mesh in model.meshes:
					meshes += 1
					faces += mesh.face_count
					x = lit_faces(model, mesh)
					if not x:
						continue
					t = sum(1 for e in x if e[4] > 1.0)
					with_lit += 1
					lit += len(x)
					turned += t
					print("%-44s %-18s %-20s order %4d faces %4d lit %4d turned %4d" % (
						os.path.relpath(path, data), member.split("\\")[-1], mesh.name, len(mesh.order),
						mesh.face_count, len(x), t))
	print("md2 %d; meshes %d, faces %d; meshes with a face naming a lit entry %d, such faces %d, turned past 1 degree %d" % (
		files, meshes, faces, with_lit, lit, turned))


if __name__ == "__main__":
	if len(sys.argv) == 3 and sys.argv[1] == "--sweep":
		sweep(sys.argv[2])
	elif len(sys.argv) == 3:
		blob = dict(wadcat.members(open(sys.argv[1], "rb").read()))
		key = next(k for k in blob if k.split("\\")[-1].lower() == sys.argv[2].lower())
		show(md2surf.Model(blob[key]))
	else:
		print(__doc__)
		sys.exit(2)
