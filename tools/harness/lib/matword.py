#!/usr/bin/env python3
"""matword.py <data dir> [--list SUBSTR] - the material record's last word (+0x0C of the 16-byte record, FileFormats
models.md "Materials") over every static .md2, wad members and loose files: the census of its values, how many uses
carry bit 0x1 (DrawList_AddMaterialFaces 0x00580530 gives such a material's vertices the channel mask, white, instead
of their lit colours: drawn unlit, docs/exe/park-engine.md "Land outside the park is drawn at half light"), and a line
for each mesh with such a material. --list SUBSTR prints every material of the models whose path holds SUBSTR.

  matword.py <game>/data
  matword.py <game>/data --list jungle/terrain/base
"""
import collections, os, struct, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import md2surf  # noqa: E402
wadcat = md2surf.wadcat

data = sys.argv[1]; LIST = sys.argv[sys.argv.index('--list') + 1].lower() if '--list' in sys.argv else None
values = collections.Counter(); files = uses = lit_off = 0; meshes_off = []
for root, _dirs, found in os.walk(data):
	for name in sorted(found):
		path = os.path.join(root, name)
		if name.lower().endswith('.wad'):
			try: members = wadcat.members(open(path, 'rb').read())
			except Exception: continue
		elif name.lower().endswith('.md2'): members = [(name, open(path, 'rb').read())]
		else: continue
		for member, blob in members:
			if not member.lower().endswith('.md2') or blob[:4] != b'\x46\x5d\xd1\x1c': continue
			try: model = md2surf.Model(blob)
			except Exception: continue
			if not model.static: continue
			files += 1
			where = os.path.relpath(path, data) + ('' if member == name else ':' + member.split('\\')[-1])
			for i in range(model.mesh_count):
				at = model.mesh_table + i * 0xa0
				mcount = struct.unpack_from('<H', blob, at + 0x5a)[0]; mats = struct.unpack_from('<I', blob, at + 0x6c)[0]
				mesh = model.name(i); off = []
				for k in range(mcount):
					frame, first, count, lo, hi, word = struct.unpack_from('<IHHHHI', blob, mats + 16 * k)
					fw = struct.unpack_from('<I', blob, frame)[0] if frame else None
					values[word] += 1; uses += 1
					if word & 1: lit_off += 1; off.append(k)
					if LIST and LIST in where.lower().replace('\\', '/'):
						print(f'{where} {mesh} [{k}] word 0x{word:x} frame word {"-" if fw is None else hex(fw)}')
				if off: meshes_off.append(f'{where} {mesh}: materials {off} of {mcount}')
if not LIST:
	for l in meshes_off: print(l)
print(f'static md2 {files}; material uses {uses}; +0x0C values {dict(values.most_common(12))}; with bit 0x1: {lit_off} uses in {len(meshes_off)} meshes')
