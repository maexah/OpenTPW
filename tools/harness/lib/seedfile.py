#!/usr/bin/env python3
"""seedfile.py <in> <out> <old> <new> - writes <out>, a copy of park file <in> whose mRandomSeed is <new> (hex): the one
place in the inflated BILZ block (at 0x60d) holding the 32-bit <old> (hex; the stock Lost Kingdom's and q340/late30's is
3f416bba) is rewritten and the block's two sizes put back, as original/research.py does. Stops on an assert where <old>
is not found exactly once. What the original then draws is not the file seed's first draw (park.md, "Which vehicle
comes"): try seeds until the run shows the vehicle wanted. <in> is only read: give a different <out>."""
import struct, sys, zlib
data = open(sys.argv[1], 'rb').read(); old, new = int(sys.argv[3], 16), int(sys.argv[4], 16)
assert data[0x60d:0x611] == b'BILZ'
size, block = struct.unpack_from('<II', data, 0x611)
payload = bytearray(zlib.decompress(data[0x629:])); assert len(payload) == size
needle = struct.pack('<I', old); at = payload.find(needle)
assert at >= 0 and payload.find(needle, at + 1) < 0, f"{old:#x} not found exactly once"
struct.pack_into('<I', payload, at, new)
packed = zlib.compress(bytes(payload), 9); head = bytearray(data[:0x629])
struct.pack_into('<II', head, 0x611, len(payload), 0x629 - 0x60d + len(packed))
open(sys.argv[2], 'wb').write(bytes(head) + packed); print(f"mRandomSeed at {at:#x}: {old:#010x} -> {new:#010x}; written {sys.argv[2]}")
