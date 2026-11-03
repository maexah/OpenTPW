#!/usr/bin/env python3
"""hand.py PID - the ORIGINAL's hand: [0x007b05d8] (the current mode: the setter FUN_0046c350 stores it in DAT_007b05d8) -> mode object; a carry shell (FUN_0046c5a0, ctor sets +8 = tool, u16 +0x10 = item) prints tool and item. Read-only."""
import struct, sys
pid = int(sys.argv[1]); f = open(f"/proc/{pid}/mem", "rb", 0)
def rd(a, n): f.seek(a); return f.read(n)
holder = struct.unpack("<I", rd(0x007b05d8, 4))[0]
if not holder: print("hand: no holder"); sys.exit()
mode = struct.unpack("<I", rd(holder, 4))[0] if False else holder
vt = struct.unpack("<I", rd(mode, 4))[0]; tool = struct.unpack("<I", rd(mode + 8, 4))[0]; item = struct.unpack("<H", rd(mode + 0x10, 2))[0]
print(f"hand: holder {holder:#x} vtable {vt:#x} tool {tool} item {item}")
