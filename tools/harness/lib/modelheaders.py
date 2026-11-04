#!/usr/bin/env python3
"""modelheaders.py <wad glob under data/levels> [name prefix ...] - the header word (+0x30) of each .md2 in the matching WADs (Q891).

`modelheaders.py '*/hoarding.wad'` lists every model with its header; `modelheaders.py '*/rides/gokarts.wad' gk_trck` only the names
starting so. Bit 0x8 is the procedural base: without it the footprint stamp has no table for the model (`park-engine.md`, "The
track kit's, the hoardings' and the coaster's cells stamp nothing"); Lost Kingdom's hoardings and kit pieces read 0x1, the queue's
pieces 0x9, `StdPylon` 0xd. Needs OPENTPW_GAME_PATH; run it with `python3 -I`."""
import glob, os, struct, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import paths, wadcat

def main(argv):
	if not argv:
		print(__doc__); return 2
	prefixes = tuple(p.lower() for p in argv[1:])
	for wad in sorted(glob.glob(os.path.join(paths.game(), "data/levels", argv[0]))):
		for name, data in wadcat.members(open(wad, "rb").read()):
			short = name.lower().rsplit("\\", 1)[-1]
			if short.endswith(".md2") and len(data) >= 0x34 and (not prefixes or short.startswith(prefixes)):
				print("%-8s %-14s %-20s header 0x%x" % (wad.split("levels/")[1].split("/")[0], os.path.basename(wad), short, struct.unpack_from("<I", data, 0x30)[0]))
	return 0

if __name__ == "__main__":
	sys.exit(main(sys.argv[1:]))
