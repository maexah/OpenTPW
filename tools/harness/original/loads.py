#!/usr/bin/env python3
"""loads.py PID SECS - the ORIGINAL's park analyser's ring of the last five arrival loads, read-only, a line a change
with mGameTick (a POLL for freshfs.sh / loadfile.sh): the slot (+0x216f0, -1 before the first load), the count
(+0x216f4, 5), wrapped (+0x216f8), the five loads (+0x216dc) and the loads called as the advisor's row 0x119 counts them
(AdvisorScore_GoldTicketExplanation 0x005a0780: five once wrapped, else the slot plus one), beside the lifetime visitors
(+0x21c08). docs/exe/hud.md, "The arrivals". Q708."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
last = None; t0 = time.time()
while time.time() - t0 < secs:
    try:
        world = rd(0x0080239c); tick = rd(world + 0x1da70c)
        a = rd(0x007cfb90 + rd(world + 0x1da720, 'H') * 0x14)
        slot = rd(a + 0x216f0, 'i'); count = rd(a + 0x216f4, 'i'); wrapped = rd(a + 0x216f8, 'B')
        loads = [rd(a + 0x216dc + 4 * i, 'i') for i in range(5)]; visitors = rd(a + 0x21c08, 'i')
    except (OSError, struct.error): time.sleep(0.05); continue
    now = (slot, wrapped, tuple(loads), visitors)
    if now != last:
        called = count if wrapped else slot + 1
        print(f'{time.time() - t0:7.2f} s tick {tick} loads: slot {slot} count {count} wrapped {wrapped} called {called} '
              f'loads {loads} visitors {visitors}', flush=True); last = now
    time.sleep(0.05)
