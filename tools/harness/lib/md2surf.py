#!/usr/bin/env python3
"""The faces of a model a point can stand on: what the original's height lookup reads.

`FUN_004511a0` answers the height under a point from a model's triangles (`FUN_00451390`): only a face whose first
word carries 0x8000 is tried, of the meshes whose node-lookup record's flags carry 0x2 (`FUN_00451290`), each
vertex moved by its node's translation chain (`FUN_00450f20`). This prints, for a model:

  the header's flags (0x30) and float (0xa0), each lookup record's flags and id with its node's name,
  and for every mesh with a 0x8000 face: how many, their height range in the model (y, the chain added),
  and whether the mesh's lookup record carries 0x2 and 0x4.

    md2surf.py <file.wad> <member.md2>         one model
    md2surf.py <file.wad> <member.md2> X Z     the height under a point of the model (the highest, as the engine)
    md2surf.py --uvs <data dir>                every static model's material runs whose UVs leave [0, 1], then the totals
    md2surf.py --sweep <data dir>              every .md2 of every .wad; only static models (version 0xDD with a mesh table)
                                               are read past the count: a line for each with a 0x2 lookup record or marked
                                               faces on a mesh without one, then the totals. A .wad that will not open is
                                               passed over and not named. (wadcat.py is imported from the folder above lib.)

Importable: `Model( bytes )`, `.meshes` (each with `.uvs`), `.lookups`, `.height( x, z )`, `uv_runs( model )`.
"""
import os
import struct
import sys

sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import wadcat  # noqa: E402

NONE = -100.0


class Mesh:
	pass


class Model:
	def __init__(self, blob):
		self.blob = blob
		u16 = lambda at: struct.unpack_from("<H", blob, at)[0]
		u32 = lambda at: struct.unpack_from("<I", blob, at)[0]
		self.version = u32(4)
		self.flags = u32(0x30)
		self.lift = struct.unpack_from("<f", blob, 0xa0)[0]
		self.nodes, self.mesh_count = u16(0x42), u16(0x44)
		self.lookup_first, self.lookup_count = u16(0x46), u16(0x48)
		self.mesh_table, self.node_table, self.lookup_table = u32(0x70), u32(0x74), u32(0x7c)
		self.static = self.version == 0xDD and self.mesh_table != 0
		self.meshes, self.lookups = [], []

		if not self.static:
			return

		for r in range(self.lookup_count):
			flags, ident = struct.unpack_from("<II", blob, self.lookup_table + r * 20)
			self.lookups.append((self.lookup_first + r, flags, ident))

		for i in range(self.mesh_count):
			self.meshes.append(self._mesh(i))

	def record(self, node):
		if node < self.mesh_count:
			return self.mesh_table + node * 0xa0
		return self.node_table + (node - self.mesh_count) * 0x58

	def name(self, node):
		at = struct.unpack_from("<I", self.blob, self.record(node) + 0x54)[0]
		end = self.blob.find(b"\0", at)
		return self.blob[at:end].decode("latin-1") if 0 < at < len(self.blob) else "?"

	def _record_index(self, pointer):
		if pointer == 0:
			return None
		if self.mesh_table <= pointer < self.mesh_table + self.mesh_count * 0xa0:
			return (pointer - self.mesh_table) // 0xa0
		return self.mesh_count + (pointer - self.node_table) // 0x58

	def chain(self, node):
		"""The translations FUN_00450f20 adds to a node's vertices: its own and each parent's, the root's left out."""
		x = y = z = 0.0
		at = self.record(node)
		parent = struct.unpack_from("<I", self.blob, at + 4)[0]

		if parent == 0:
			return (0.0, 0.0, 0.0)

		while True:
			tx, ty, tz = struct.unpack_from("<fff", self.blob, at + 0x40)
			x, y, z = x + tx, y + ty, z + tz
			at = self.record(self._record_index(parent))
			parent = struct.unpack_from("<I", self.blob, at + 4)[0]
			if parent == 0:
				return (x, y, z)

	def _mesh(self, i):
		blob, at = self.blob, self.mesh_table + i * 0xa0
		m = Mesh()
		m.index, m.name = i, self.name(i)
		vcount, mcount, fcount, olen = struct.unpack_from("<HHHH", blob, at + 0x58)
		vdata, normals, _uv, mats, faces = struct.unpack_from("<IIIII", blob, at + 0x60)
		order = struct.unpack_from("<I", blob, at + 0x94)[0]
		m.face_count = fcount
		m.chain = self.chain(i)
		raw = []
		for v in range(vcount):
			block = vdata + (v >> 2) * 0x30
			raw.append((struct.unpack_from("<f", blob, block + (v & 3) * 4)[0] + m.chain[0],
				struct.unpack_from("<f", blob, block + 0x10 + (v & 3) * 4)[0] + m.chain[1],
				struct.unpack_from("<f", blob, block + 0x20 + (v & 3) * 4)[0] + m.chain[2]))
		m.order = struct.unpack_from("<%dH" % olen, blob, order) if olen else ()
		m.raw = raw
		# One (u, v) per vertex-order entry, packed four u's then four v's (ModelFile.cs): a face's corner index reads it.
		m.uvs = []
		for e in range(olen):
			block = _uv + (e >> 2) * 0x20
			m.uvs.append(struct.unpack_from("<f", blob, block + (e & 3) * 4)[0] + 0.0)
			m.uvs[-1] = (m.uvs[-1], struct.unpack_from("<f", blob, block + 0x10 + (e & 3) * 4)[0])
		m.materials = [struct.unpack_from("<HH", blob, mats + k * 16 + 4) for k in range(mcount)]
		m.faces = []
		for f in range(fcount):
			word, a, b, c = struct.unpack_from("<HHHH", blob, faces + f * 8)
			m.faces.append((word, a, b, c))
		m.normals = normals
		m.surface = [f for f in m.faces if f[0] & 0x8000]
		record = i - self.lookup_first
		m.lookup = self.lookups[record] if 0 <= record < len(self.lookups) else None
		return m

	def corner(self, m, index):
		return m.raw[m.order[index]]

	def face_height(self, m, face, x, z):
		"""FUN_00451390 for one face at angle 0: inside all three edges, then the plane through corner A by the face's normal."""
		word, a, b, c = face
		p = [self.corner(m, a), self.corner(m, b), self.corner(m, c)]
		for k in (3, 2, 1):
			v, w = p[k % 3], p[k - 1]
			if (-v[0] + w[0]) * z + (v[2] - w[2]) * x - ((-v[0] + w[0]) * w[2] + (v[2] - w[2]) * w[0]) < 0.0:
				return None
		nx, ny, nz = struct.unpack_from("<fff", self.blob, m.normals + (word & 0x7fff) * 12)
		if ny == 0.0:
			return None
		return -((nz * z + nx * x - (nz * p[0][2] + ny * p[0][1] + nx * p[0][0])) / ny)

	def height(self, x, z):
		"""The surface meshes' height under a model point: FUN_00451290's walk of the lookup records flagged 0x2."""
		best = NONE
		for m in self.meshes:
			if m.lookup is None or not m.lookup[1] & 2:
				continue
			here = NONE
			for face in m.surface:
				h = self.face_height(m, face, x, z)
				if h is not None and h > here:
					here = h
					if m.lookup[1] & 4:
						break
			if here > best:
				if m.lookup[1] & 4:
					return here
				best = here
		return best


def show(model, point=None):
	print("header flags 0x%x  +0xa0 %.4f  meshes %d of %d nodes  lookups %d from node %d" % (
		model.flags, model.lift, model.mesh_count, model.nodes, model.lookup_count, model.lookup_first))
	for node, flags, ident in model.lookups:
		print("  lookup node %2d %-16s flags 0x%08x id %d" % (node, model.name(node), flags, ident))
	for m in model.meshes:
		if not m.surface:
			continue
		ys = [model.corner(m, i)[1] for f in m.surface for i in f[1:]]
		print("  mesh %2d %-16s surface faces %3d of %3d  y %.4f..%.4f  mesh top %.4f  lookup %s" % (
			m.index, m.name, len(m.surface), m.face_count, min(ys), max(ys), max(v[1] for v in m.raw),
			"none" if m.lookup is None else "0x%x" % m.lookup[1]))
	if point:
		print("height under (%.3f, %.3f): %.4f" % (point[0], point[1], model.height(*point)))


def sweep(data):
	files = flagged = faces = marked = 0
	walk2 = walk4 = split = 0
	bad_materials = 0
	for root, _dirs, names in os.walk(data):
		for name in sorted(names):
			if not name.lower().endswith(".wad"):
				continue
			try:
				found = wadcat.members(open(os.path.join(root, name), "rb").read())
			except Exception:
				continue
			for member, blob in found:
				if not member.lower().endswith(".md2") or blob[:4] != b"\x46\x5d\xd1\x1c":
					continue
				files += 1
				try:
					model = Model(blob)
				except Exception as e:
					print("unreadable", name, member, e)
					continue
				if not model.static:
					continue
				has = sum(len(m.surface) for m in model.meshes)
				faces += sum(m.face_count for m in model.meshes)
				marked += has
				two = [l for l in model.lookups if l[1] & 2]
				walk2 += len(two)
				walk4 += len([l for l in model.lookups if l[1] & 4])
				for m in model.meshes:
					at, ok = 0, True
					for first, count in m.materials:
						ok = ok and first == at
						at += count
					if not ok or at != m.face_count:
						bad_materials += 1
				unflagged = [m.name for m in model.meshes if m.surface and not (m.lookup and m.lookup[1] & 2)]
				if has:
					flagged += 1
				if two or unflagged:
					print("%-40s %-16s 0x30=0x%x a0=%.2f  0x2 on %s  surface elsewhere on %d meshes" % (
						os.path.relpath(os.path.join(root, name), data), member.split("\\")[-1], model.flags, model.lift,
						",".join(model.name(l[0]) for l in two) or "-", len(unflagged)))
					if unflagged and two:
						split += 1
	print("md2 members %d; with a 0x8000 face %d; faces %d, marked %d; lookup records 0x2: %d, 0x4: %d; "
		"models with both a 0x2 mesh and marked faces elsewhere %d; meshes whose materials do not tile the faces %d" % (
		files, flagged, faces, marked, walk2, walk4, split, bad_materials))


def uv_runs(model, slack=0.01):
	"""Each mesh's material runs whose face corners' UVs leave [0, 1] by more than `slack`: (mesh, run, lo, hi)."""
	out = []
	for m in model.meshes:
		for run, (first, count) in enumerate(m.materials):
			us, vs = [], []
			for word, a, b, c in m.faces[first:first + count]:
				for k in (a, b, c):
					if k < len(m.uvs):
						us.append(m.uvs[k][0])
						vs.append(m.uvs[k][1])
			if not us:
				continue
			lo, hi = (min(us), min(vs)), (max(us), max(vs))
			if min(lo) < -slack or max(hi) > 1 + slack:
				out.append((m, run, lo, hi))
	return out


def uv_sweep(data):
	"""Every static model's material runs whose UVs leave [0, 1]: a line each, then the totals."""
	models = leaving = runs = total_runs = 0
	for root, _dirs, names in os.walk(data):
		for name in sorted(names):
			if not name.lower().endswith(".wad"):
				continue
			try:
				found = wadcat.members(open(os.path.join(root, name), "rb").read())
			except Exception:
				continue
			for member, blob in found:
				if not member.lower().endswith(".md2") or blob[:4] != b"\x46\x5d\xd1\x1c":
					continue
				try:
					model = Model(blob)
				except Exception as e:
					print("unreadable", name, member, e)
					continue
				if not model.static:
					continue
				models += 1
				total_runs += sum(len(m.materials) for m in model.meshes)
				found_runs = uv_runs(model)
				if found_runs:
					leaving += 1
					runs += len(found_runs)
				for m, run, lo, hi in found_runs:
					print("%-40s %-18s %-20s run %2d  u %7.3f..%7.3f  v %7.3f..%7.3f" % (
						os.path.relpath(os.path.join(root, name), data), member.split("\\")[-1], m.name, run,
						lo[0], hi[0], lo[1], hi[1]))
	print("static models %d; with a run whose UVs leave [0,1] %d; such runs %d of %d" % (models, leaving, runs, total_runs))


if __name__ == "__main__":
	if sys.argv[1] == "--sweep":
		sweep(sys.argv[2])
	elif sys.argv[1] == "--uvs":
		uv_sweep(sys.argv[2])
	else:
		members = dict((n.split("\\")[-1].lower(), b) for n, b in wadcat.members(open(sys.argv[1], "rb").read()))
		show(Model(members[sys.argv[2].lower()]), tuple(map(float, sys.argv[3:5])) if len(sys.argv) > 4 else None)
