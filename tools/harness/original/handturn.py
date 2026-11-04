#!/usr/bin/env python3
"""handturn.py PID [DEGREES] - the ORIGINAL's carried rotation `DAT_0081d7a4` (park-engine.md: no key or click changes it; a
move or a Ctrl+click on a turned thing sets it to that thing's angle): with DEGREES, WRITES it (a dword: 0, 90, 180, 270), so
the item in the hand is put down turned by a real click; without, reads it. Prints the word, then the tool-4 preview's anchor
`[0x008186a4]`/`[0x008186a8]` and cell `[0x008186b8]`/`[0x008186b4]`. The one non-click step of a turned put-down: say so in the account."""
import struct, sys
pid = int(sys.argv[1]); f = open(f"/proc/{pid}/mem", "r+b", buffering=0)
def rd(a): f.seek(a); return struct.unpack("<i", f.read(4))[0]
if len(sys.argv) > 2:
	f.seek(0x0081d7a4); f.write(struct.pack("<i", int(sys.argv[2])))
print(f"rotation [0x0081d7a4] {rd(0x0081d7a4)}; preview anchor ({rd(0x008186a4)}, {rd(0x008186a8)}), cell ({rd(0x008186b8)}, {rd(0x008186b4)})")
