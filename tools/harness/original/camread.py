#!/usr/bin/env python3
"""camread.py - poll the running ORIGINAL's park camera and interface layers, one line a change.

  camread.py PID [SECONDS] [INTERVAL]      SECONDS default 60, INTERVAL default 0.1

Each line: seconds since start, the state machine's step counter [0x00877d34] (not mGameTick; tick.py reads that), gui_CameraFlags [0x00790ab0], the ride view's model index
[0x00790a44], the look offsets [0x00790adc]/[0x00790ae0], the eye [0x007909c0/c4/c8], layer 0's and layer 1's
visibility bit (bit 0 of [0x007cb2ac]+0x48 and [0x007cb2b0]+0x48; '-' where the layer is nought), the music dip
(the word at [0x00803ad0], FUN_0051e710's; [0x00803ac8] is only the volumes' dirty flag), the End feature's thing id [0x007cf4f4] and the compositor word [0x007a1a8c]. A line is printed when any
field but the step counter changes. docs/exe/park-engine.md, "The End feature's view" (Q551), and "The full-screen view: F3".
Read-only.
"""
import struct, sys, time

FLAGS, MODEL, LOOKX, LOOKY, EYE = 0x00790ab0, 0x00790a44, 0x00790adc, 0x00790ae0, 0x007909c0
LAYER0, LAYER1, DIP, END, COMP, STEPS = 0x007cb2ac, 0x007cb2b0, 0x00803ad0, 0x007cf4f4, 0x007a1a8c, 0x00877d34

def main():
    pid = int(sys.argv[1]); secs = float(sys.argv[2]) if len(sys.argv) > 2 else 60
    step = float(sys.argv[3]) if len(sys.argv) > 3 else 0.1
    f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
    def rd(a, fmt='I'):
        f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
    def vis(layer):
        p = rd(layer)
        return '-' if p == 0 else str(rd(p + 0x48) & 1)
    t0, last = time.time(), None
    while time.time() - t0 < secs:
        try:
            eye = tuple(rd(EYE + 4 * i, 'f') for i in range(3))
            row = (f'flags 0x{rd(FLAGS):04x} model {rd(MODEL)} look ({rd(LOOKX, "f"):.3f}, {rd(LOOKY, "f"):.3f}) '
                   f'eye ({eye[0]:.2f}, {eye[1]:.2f}, {eye[2]:.2f}) layer0 {vis(LAYER0)} layer1 {vis(LAYER1)} '
                   f'dip {rd(DIP, "H")} end {rd(END, "H")} comp 0x{rd(COMP):x}')
        except (OSError, struct.error) as e:
            print(f'{time.time() - t0:7.2f}s  read failed: {e}'); break
        if row != last:
            print(f'{time.time() - t0:7.2f}s step {rd(STEPS)}  {row}', flush=True); last = row
        time.sleep(step)

main()
