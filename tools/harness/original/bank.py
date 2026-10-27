#!/usr/bin/env python3
"""bank.py PID - the original's money in memory, on one line: the bank balance (FUN_005195d0's thing +0xc), the analyser's
staff costs this month (FUN_00519510's thing +0x1f7f0), the hand's worker (mStaffHQ, world +0x1da718, its +0x60: the id is
the low word), the current mode [0x007b05d8], the staff window's kept member [0x007c2650] and mGameTick. World is
[0x0080239c]; things by id from [0x007cfb90 + id*20]. Read-only; Q432 (a dismissal's wage, read either side of the tick)."""
import struct, sys
pid = int(sys.argv[1]); f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
thing = lambda off: rd(0x007cfb90 + rd(w + off, 'H') * 20)
w = rd(0x0080239c)
print(f"bank {rd(thing(0x1da726) + 0xc, 'i')} staffcost {rd(thing(0x1da720) + 0x1f7f0, 'i')} "
      f"hand {rd(thing(0x1da718) + 0x60) & 0xffff} (word {rd(thing(0x1da718) + 0x60):#x}) mode {rd(0x007b05d8):#x} kept {rd(0x007c2650):#x} tick {rd(0x00877d34)}")
