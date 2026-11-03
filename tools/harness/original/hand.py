#!/usr/bin/env python3
"""hand.py PID - the ORIGINAL's hand: [0x007b05d8] (the current mode: the setter FUN_0046c350 stores it in DAT_007b05d8) -> mode object; a buy shell (vtable 0x6fe9e0 with +0xc == 1: FUN_0046c5a0's ctor sets +8 = tool, u16 +0x10 = item) prints tool and item; any other mode prints its vtable only (FUN_0046c580's shells set no item, the idle mode is 8 bytes). Read-only."""
import struct, sys
pid = int(sys.argv[1]); f = open(f"/proc/{pid}/mem", "rb", 0)
def rd(a, n): f.seek(a); return f.read(n)
holder = struct.unpack("<I", rd(0x007b05d8, 4))[0]
if not holder: print("hand: no mode"); sys.exit()
mode = holder
vt = struct.unpack("<I", rd(mode, 4))[0]
if vt == 0x6fe9e0 and struct.unpack("<I", rd(mode + 0xc, 4))[0] == 1:
	tool = struct.unpack("<I", rd(mode + 8, 4))[0]; item = struct.unpack("<H", rd(mode + 0x10, 2))[0]
	print(f"hand: mode {mode:#x} vtable {vt:#x} tool {tool} item {item}")
else: print(f"hand: mode {mode:#x} vtable {vt:#x}")
