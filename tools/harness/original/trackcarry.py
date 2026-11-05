#!/usr/bin/env python3
"""trackcarry.py PID - the original's coaster track tool, read-only, one line: the cell under the pointer (the build
preview [0x008186b8], [0x008186b4] and the picker's packed cell [0x007b05cc]; cellat.py's [0x007c24f4] is left out), the tool's mode bits [0x0079c658] (0x100 a
node is being carried), the carried node [0x0079c6a4], the picked node [0x0079c6dc], the hover [0x007a1cd4] (the model
under the pointer: a pylon's pixel reads a fixed value while the camera holds), [0x0079c640] (set with the carried node at the press; a click alone left it 0) and the
coaster being edited [0x00790fec]. A press carries a pylon only after a click on it since the last move, and each frame
of the carry runs Coaster_MoveNodeToCell on the cell under the pointer (docs/exe/park-engine.md, "A pylon carried onto a
cell that holds one", Q1001): move the pointer in several steps with the button held, wait 1.5 to 2.5 seconds, read this and
`coaster.py PID 1 pylons` before the release. Replaces nothing: cellfind.py moves the pointer, this does not."""
import struct, sys
f = open(f'/proc/{int(sys.argv[1])}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
	f.seek(a); return struct.unpack('<' + fmt, f.read(4))[0]
print('preview %d %d picker %d | mode %#x carried %#x picked %#x hover %#x sel %#x coaster %#x' % (
	rd(0x008186b8, 'i'), rd(0x008186b4, 'i'), rd(0x007b05cc, 'i'), rd(0x0079c658), rd(0x0079c6a4), rd(0x0079c6dc), rd(0x007a1cd4), rd(0x0079c640), rd(0x00790fec)))
