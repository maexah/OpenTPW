#!/usr/bin/env python3
"""
Read a Theme Park World .wad (DWFB) without the game.

A faithful port of OpenTPW's WadArchive.ReadArchive and Refpack.Decompress, so that a .sam
inside an item's archive can be read from a shell. Written because the item catalogue's
per-item description - the file that OVERRIDES the folder's defaults - lives inside the .wad,
and reading only the folder's .sam gives a different park (ItemDescriptionFile says so about
CostOfGoods, which is 30 in the category file and 50 in the Jungle Spray's own).

    wadcat.py <file.wad> list
    wadcat.py <file.wad> cat <name>
    wadcat.py <file.wad> grep <regex>        # over every text member

Header, from WadArchive.ReadArchive: 4 magic 'DWFB', 4 version, 64 padding, 4 file count,
4 file-list offset, 4 file-list length, 4 unused = 88 bytes, then one 40-byte record each:
4 unused, 4 name offset, 4 name length, 4 data offset, 4 data length, 4 compression
(4 = refpack), 4 decompressed size, 12 null. Little-endian throughout - refpack itself is
big-endian, which is the one exception and is handled below.
"""

import re
import sys
import struct


def refpack(data):
	"""Port of Refpack.Decompress plus its five command classes."""
	if len(data) < 5 or data[0] != 0x10 or data[1] != 0xFB:
		raise ValueError("not refpack: header %r" % (data[:2],))

	# 2 signature bytes then a 3-byte big-endian decompressed size, which is skipped exactly
	# as the C# does - the length is implied by the stop command instead.
	pos = 5
	out = bytearray()

	while pos < len(data):
		b0 = data[pos]

		if b0 < 0x80:					# TwoByteCommand
			b1 = data[pos + 1]
			length = 2
			plain = b0 & 0x03
			ref_len = ((b0 & 0x1C) >> 2) + 3
			ref_dist = ((b0 & 0x60) << 3) + b1 + 1
		elif b0 < 0xC0:					# ThreeByteCommand
			b1, b2 = data[pos + 1], data[pos + 2]
			length = 3
			plain = (b1 & 0xC0) >> 6
			ref_len = (b0 & 0x3F) + 4
			ref_dist = ((b1 & 0x3F) << 8) + b2 + 1
		elif b0 < 0xE0:					# FourByteCommand
			b1, b2, b3 = data[pos + 1], data[pos + 2], data[pos + 3]
			length = 4
			plain = b0 & 0x03
			ref_len = ((b0 & 0x0C) << 6) + b3 + 5
			ref_dist = ((b0 & 0x10) << 12) + (b1 << 8) + b2 + 1
		elif ((b0 & 0x1F) + 1) << 2 <= 0x70:		# OneByteCommand
			length = 1
			plain = ((b0 & 0x1F) + 1) << 2
			ref_len = ref_dist = 0
		else:						# StopCommand
			plain = b0 & 0x03
			out += data[pos + 1:pos + 1 + plain]
			break

		out += data[pos + length:pos + length + plain]

		# The reference is read from the OUTPUT buffer, and may overlap what it is writing -
		# the source index is taken before the copy and walks forward with it, which is what
		# makes a run-length repeat work.
		if ref_len:
			start = len(out) - ref_dist
			for i in range(ref_len):
				at = start + i
				if at < 0 or at >= len(out):
					break
				out.append(out[at])

		pos += length + plain

	return bytes(out)


def members(blob):
	"""Every member of the archive as (full name, bytes)."""
	if blob[:4] != b"DWFB":
		raise ValueError("magic is %r, not DWFB" % blob[:4])

	count, _list_offset, _list_length, _unused = struct.unpack_from("<IIII", blob, 72)

	at = 88
	folder = ""
	found = []

	for _ in range(count):
		(_u, name_offset, name_length, data_offset,
			data_length, compression, _decompressed) = struct.unpack_from("<IIIIIII", blob, at)

		raw = blob[name_offset:name_offset + name_length - 1].decode("latin-1")

		# A name carrying a directory sets the directory for every member after it, which is
		# how the format stores a tree - see WadArchive's own note.
		if "\\" in raw:
			folder = raw[:raw.rindex("\\")]
			name = raw[raw.rindex("\\") + 1:]
		else:
			name = raw

		data = blob[data_offset:data_offset + data_length]

		if compression == 4:
			try:
				data = refpack(data)
			except ValueError:
				pass

		found.append(((folder + "\\" + name) if folder else name, data))
		at += 40

	return found


def main():
	if len(sys.argv) < 3:
		print(__doc__)
		return 2

	blob = open(sys.argv[1], "rb").read()
	mode = sys.argv[2]
	items = members(blob)

	if mode == "list":
		for name, data in items:
			print("%8d  %s" % (len(data), name))
		return 0

	if mode == "cat":
		wanted = sys.argv[3].lower()
		for name, data in items:
			if name.lower().endswith(wanted):
				sys.stdout.write(data.decode("latin-1"))
				return 0
		print("no member ending %r" % wanted, file=sys.stderr)
		return 1

	if mode == "grep":
		pattern = re.compile(sys.argv[3], re.IGNORECASE)
		for name, data in items:
			try:
				text = data.decode("ascii")
			except UnicodeDecodeError:
				continue
			for line in text.splitlines():
				if pattern.search(line):
					print("%s: %s" % (name, line.strip()))
		return 0

	print("unknown mode %r" % mode, file=sys.stderr)
	return 2


if __name__ == "__main__":
	sys.exit(main())
