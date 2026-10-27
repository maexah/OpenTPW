#!/usr/bin/env python3
"""Faces drawn twice: a static model's faces whose three corners stand on the same three points (Q586).

The original culls every model face before it reaches a batch (Face_KeptByWinding, 0x00581320; render-states.md, "The
software back-face test"), so of a pair wound opposite ways only the one turned to the eye is drawn, whichever comes first
in the file, and lit its own way. Most twins are a sheet authored two-sided, the two faces turned opposite ways. A group is
consecutive when it is two records one straight after the other (n, n + 1).

    twinfaces.py <wad> <member.md2>     one model: each twin group, its faces in file order, material and normals
    twinfaces.py --sweep <data dir>     every .md2 (wad members and loose files): a line a mesh with twins, then the totals

A face is (corner a, b, c) through the vertex order; its normal is the table entry its first word names (mesh +0x64),
which agrees with cross(b - a, c - a) on every twin counted. Importable: `twins( model, mesh )`.
"""
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import md2surf  # noqa: E402
import wadcat  # noqa: E402


def _sub(a, b): return (a[0] - b[0], a[1] - b[1], a[2] - b[2])
def _cross(a, b): return (a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0])
def _dot(a, b): return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]


def material_of(mesh, face):
	for k, (first, count) in enumerate(mesh.materials):
		if first <= face < first + count:
			return k
	return -1


def twins(model, mesh):
	"""Groups of two or more face indices of the mesh on the same three points, each group in file order."""
	groups = {}
	for f, (_word, a, b, c) in enumerate(mesh.faces):
		try:
			key = frozenset(tuple(round(v, 4) for v in mesh.raw[mesh.order[i]]) for i in (a, b, c))
		except IndexError:
			continue
		if len(key) == 3:
			groups.setdefault(key, []).append(f)
	return [g for g in groups.values() if len(g) > 1]


def facing(model, mesh, f):
	_w, a, b, c = mesh.faces[f]
	pa, pb, pc = (mesh.raw[mesh.order[i]] for i in (a, b, c))
	return _cross(_sub(pb, pa), _sub(pc, pa))


def show(model, names):
	for mesh in model.meshes:
		for g in twins(model, mesh):
			first = facing(model, mesh, g[0])
			print("%-12s faces %s (normals %s)  material %s  %s" % (
				mesh.name, ",".join(str(f) for f in g), ",".join("0x%x" % (mesh.faces[f][0] & 0x7fff) for f in g),
				",".join(str(material_of(mesh, f)) for f in g),
				"opposite" if any(_dot(first, facing(model, mesh, f)) < 0 for f in g[1:]) else "same way"))


def sweep(data):
	files = meshes = groups = opposite = consecutive = 0
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
					g = twins(model, mesh)
					if not g:
						continue
					meshes += 1
					groups += len(g)
					opp = sum(1 for x in g if any(_dot(facing(model, mesh, x[0]), facing(model, mesh, f)) < 0 for f in x[1:]))
					opposite += opp
					consecutive += sum(1 for x in g if len(x) == 2 and x[1] == x[0] + 1)
					print("%-48s %-18s %-14s twins %4d opposite %4d" % (
						os.path.relpath(path, data), member.split("\\")[-1], mesh.name, len(g), opp))
	print("md2 files %d; meshes with twins %d; twin groups %d, %d of them turned opposite ways, %d two records one straight after the other" % (
		files, meshes, groups, opposite, consecutive))


if __name__ == "__main__":
	if len(sys.argv) == 3 and sys.argv[1] == "--sweep":
		sweep(sys.argv[2])
	elif len(sys.argv) == 3:
		blob = dict(wadcat.members(open(sys.argv[1], "rb").read()))
		key = next(k for k in blob if k.lower().endswith(sys.argv[2].lower()))
		show(md2surf.Model(blob[key]), None)
	else:
		print(__doc__)
		sys.exit(2)
