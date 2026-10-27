#!/usr/bin/env python3
"""screens.py PID SECS - which park screen the running ORIGINAL has open and what its gadget's three category
buttons are left on, read-only, a line a change with the wall clock and mGameTick (a POLL for loadfile.sh).

  left     [0x007cb28c], [0x007cb2a0], [0x007cb290]: the screen each category button opens next (FUN_004a0940):
           1 buy / 2 hire; 3 park status / 4 staff / 5 items / 6 visitors; 7 finances / 8 loans / 9 staff costs /
           10 entry price. Seeded 1, 3, 10 by the gadget's builder; each screen's opener writes its own (FUN_004a0810).
  screen   [0x007c24c8], the park screen that is open (FUN_00485b40 destroys it), 0 for none
  buy      [0x007cc1f8], the buy screen; hire [0x007ca308], the hire screen (0 while not built)
  hiretab  [0x007ca30c], the hire screen's tab (FUN_0049bdd0's argument: 0 cleaners, 1 mechanics, 2 entertainers,
           3 guards, 4 scientists), kept after the screen is destroyed

The pointers are printed as up/0 with the pointer itself, so a screen destroyed and built again shows as a change."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
last = None; t0 = time.time()
while time.time() - t0 < secs:
    try:
        now = (rd(0x007cb28c), rd(0x007cb2a0), rd(0x007cb290), rd(0x007c24c8), rd(0x007cc1f8), rd(0x007ca308), rd(0x007ca30c, 'i'))
        world = rd(0x0080239c); tick = rd(world + 0x1da70c) if world else -1
    except (OSError, struct.error): time.sleep(0.05); continue
    if now != last:
        print(f'{time.time():.2f} {time.time() - t0:7.2f} s tick {tick}: left {now[0]},{now[1]},{now[2]} screen {now[3]:#x} buy {now[4]:#x} hire {now[5]:#x} hiretab {now[6]}', flush=True)
        last = now
    time.sleep(0.03)
