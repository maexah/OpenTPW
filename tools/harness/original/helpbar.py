#!/usr/bin/env python3
"""helpbar.py PID [SECS] - the running ORIGINAL's help bar, read-only (Q473; docs/exe/hud.md, "The help bar holds one
posted row"). The label [0x007ca0d8] shown (flags +0x48 bit 0), the help sink [0x007ca0c0]'s +4 (switched on), +6 (a
control's row up) and +0xc (built) and its control +0x14 (the control the pointer came onto last; the park's view is
[0x007cb2ac]), the hover's category [0x007c24f4], the cell [0x007b05cc] and the entry-price screen [0x007ca210].
With no SECS one line; with SECS polls /proc/PID/mem as fast as it can (about 110,000 reads a second) and prints a
line, in ms, on each change of the category, the cell, the shown flag or the control - fast enough to catch the
hover's own steps: each hover zeroes the category for a moment (0x00486de5), so a 0 then the old category again is
one hover with nothing changed (q473/orig/d-run.out: one every 130 to 270 ms). The text itself is not read: crop the
frame (1024 x 768: 560x50+232+712).
The buy screen's poster FUN_004abc80 (Q495; hud.md, "The buy screen's help rows"): `buy` prints its item [0x007cc1e8] (the
list's last 0x401, nought none), the item last posted [0x007cc1e0] and the state [0x0075513c] (-1 the pointer off the list,
0 a mystery ride the tickets buy, 1 one they do not, 2 too dear, 3 to buy, 4 a land row), and the buy screen [0x007cc1f8];
the poll prints a line on a change of those too.
The online visit's poster (Q496; hud.md, "The online visit's help rows"): `cam` prints the game type [0x00fb3b7c] (1 online),
that interface's tree [0x007cc8bc] and full-screen control [0x007cc8c0] (nought: never built, as in every offline park), its
state [0x007cc8e4] and thing [0x007cc8e8], the current mode's vtable [[0x007b05d8]] (0x6fead0 the camcorder's pick, 0x6fea10
idle) and gui_CameraFlags [0x00790ab0] (& 0x16 first person); the poll prints a line on a change of the state too.
First person's layers (Q497; hud.md, "The help bar in first person"): `fp` prints the hover's on-the-view flag [0x007c24fc]
(Park_MouseMessageProc runs the hover on its timer only while it is set), the hover's thing [0x007c24f8], layer 1
[0x007cb2b0] (first person's, the viewfinder's parent), the library's pointed control [0x00faa5e0] (the one that had the
last 0x10001) and the cursor id applied [0x0077c484] (2 c_pick, 3 c_path, 0x15 c_crosshair, 1 c_busy); the poll prints a
line on a change of any of them too."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]) if len(sys.argv) > 2 else None
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
lab = rd(0x007ca0d8)
def mode():
    m = rd(0x007b05d8); return rd(m) if m else 0
if secs is None:
    print(f"bar shown {rd(lab + 0x48) & 1} sink on {rd(0x007ca0c4, 'h')} up {rd(0x007ca0c6, 'h')} built {rd(0x007ca0cc, 'h')} "
          f"ctl 0x{rd(0x007ca0d4):x} view 0x{rd(0x007cb2ac):x} cat {rd(0x007c24f4, 'i')} cell {rd(0x007b05cc, 'h')} "
          f"price {rd(0x007ca210)} buy item {rd(0x007cc1e8, 'h')} posted {rd(0x007cc1e0, 'h')} state {rd(0x0075513c, 'i')} "
          f"screen 0x{rd(0x007cc1f8):x} cam type {rd(0x00fb3b7c)} tree 0x{rd(0x007cc8bc):x} ctl 0x{rd(0x007cc8c0):x} "
          f"state {rd(0x007cc8e4, 'i')} thing 0x{rd(0x007cc8e8):x} mode 0x{mode():x} flags 0x{rd(0x00790ab0):x} "
          f"fp on {rd(0x007c24fc)} thing 0x{rd(0x007c24f8):x} layer1 0x{rd(0x007cb2b0):x} pointed 0x{rd(0x00faa5e0):x} "
          f"cursor 0x{rd(0x0077c484, 'h') & 0xffff:x}")
    sys.exit(0)
last = None; t0 = time.time(); n = 0
while time.time() - t0 < secs:
    now = (rd(0x007c24f4, 'i'), rd(0x007b05cc, 'h'), rd(lab + 0x48) & 1, hex(rd(0x007ca0d4)),
           rd(0x007cc1e8, 'h'), rd(0x007cc1e0, 'h'), rd(0x0075513c, 'i'), rd(0x007cc8e4, 'i'),
           rd(0x007c24fc), hex(rd(0x007c24f8)), hex(rd(0x00faa5e0)), hex(rd(0x0077c484, 'h') & 0xffff), hex(rd(0x00790ab0)))
    n += 1
    if now != last:
        print(f'{(time.time() - t0) * 1000:8.1f} ms  cat {now[0]} cell {now[1]} shown {now[2]} ctl {now[3]} '
              f'buy item {now[4]} posted {now[5]} state {now[6]} cam state {now[7]} on {now[8]} thing {now[9]} '
              f'pointed {now[10]} cursor {now[11]} flags {now[12]}', flush=True); last = now
print(f'{n} reads', flush=True)
