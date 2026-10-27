#!/usr/bin/env python3
"""research.py <in> <out> <item> [0] - writes <out>, a copy of park file <in> with catalogue item <item> marked researched
(or, with a last argument 0, not researched): byte +0x10 of its 32-byte record set to 1 (or 0), in the table of the inflated BILZ block (at 0x60d) that begins with the ten
item ids of IDS (the jungle files'; no theme is checked), and the block's two sizes rewritten. Prints the record.
Stops on an assert where the block, the table or the item (among the table's first 150 records) is not found exactly
once. <in> is only read: give a different <out>, never the reference file."""
import sys, struct, zlib
IDS = [1100,1140,1150,1108,1180,1101,1109,100,1102,1113]; item = int(sys.argv[3])
data = open(sys.argv[1], 'rb').read()
assert data[0x60d:0x611] == b'BILZ'
size, block = struct.unpack_from('<II', data, 0x611)
payload = bytearray(zlib.decompress(data[0x629:])); assert len(payload) == size
hits = [p for p in range(0, len(payload) - 320) if all(struct.unpack_from('<H', payload, p + 32 * i)[0] == v for i, v in enumerate(IDS))]
assert len(hits) == 1, hits
recs = [hits[0] + 32 * k for k in range(150) if struct.unpack_from('<H', payload, hits[0] + 32 * k)[0] == item]
assert len(recs) == 1, recs
rec = recs[0]; print(f"record {(rec-hits[0])//32} at {rec:#x}: {payload[rec:rec+32].hex()}; +0x10 was {payload[rec+0x10]}")
payload[rec + 0x10] = int(sys.argv[4]) if len(sys.argv) > 4 else 1
packed = zlib.compress(bytes(payload), 9); head = bytearray(data[:0x629])
struct.pack_into('<II', head, 0x611, len(payload), 0x629 - 0x60d + len(packed))
open(sys.argv[2], 'wb').write(bytes(head) + packed); print("written", sys.argv[2])
