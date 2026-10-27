#!/usr/bin/env python3
"""cellfind.py PID X,Y [PX,PY] - the pixel over map cell (X,Y) in the running ORIGINAL, found by moving the real pointer
and reading the build preview's cell ([0x008186b8], [0x008186b4]): the original's side of lib/cellprobe.py (Q439).

The preview's cell follows the pointer with a tool armed or with none (Q260), but reads (0, 0) while a park screen is
open: PICK=1 reads the picker's cell [0x007b05cc] (y*128 + x + 1, FUN_0045d560) instead, which follows the pointer with
a member's window up (Q440). Probes stay inside 0.95 of the way to each edge (27..996, 20..747): past it the pointer
pans the camera and stops its follow (0x0042ba37, Q440).
It starts at PX,PY, or at `camera.py --aim X,Y`'s pixel, which can be cells out (Q439: (53,15) aimed, (50,17) under it);
two probes 60 px apart give the slope, then Newton steps, 14 at most. Prints only 'px py' on stdout (feed it to
gmove.py as camera.py --aim's is), each probe on stderr; exits 1 where it does not get there. The camera may be moving
(the original's follows a person whose window was opened): click straight after it.
Display GDISPLAY (default :77), the 1024 x 768 off-screen picture at its origin. Needs xdotool."""
import os, struct, subprocess, sys, time
os.environ['DISPLAY'] = os.environ.get('GDISPLAY', ':77')
HERE = os.path.dirname(os.path.abspath(__file__))
PICK = os.environ.get('PICK') == '1'

def loc():
    out = subprocess.check_output(['xdotool', 'getmouselocation']).decode().split()
    return int(out[0][2:]), int(out[1][2:])

def glide(tx, ty, step=15):
    x, y = loc()
    while (x, y) != (tx, ty):
        x += max(-step, min(step, tx - x)); y += max(-step, min(step, ty - y))
        subprocess.run(['xdotool', 'mousemove', str(x), str(y)]); time.sleep(0.02)

def main():
    pid = int(sys.argv[1]); tx, ty = map(int, sys.argv[2].split(','))
    f = open(f'/proc/{pid}/mem', 'rb', buffering=0)   # tpwmem.py runs its main() on import, so its reader is not used
    def rd(addr, fmt):
        f.seek(addr); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
    if len(sys.argv) > 3:
        px, py = map(int, sys.argv[3].split(','))
    else:
        px, py = map(lambda v: int(float(v)), subprocess.check_output([sys.executable, os.path.join(HERE, 'camera.py'), str(pid), '--aim', f'{tx},{ty}']).split())
    def probe(x, y):
        x, y = max(27, min(996, x)), max(20, min(747, y)); glide(x, y); time.sleep(0.35)
        if PICK:
            v = rd(0x007b05cc, 'H'); c = ((v - 1) % 128, (v - 1) // 128) if v else (0, 0)
        else:
            c = (rd(0x008186b8, 'i'), rd(0x008186b4, 'i'))
        print(f'probe ({x},{y}) -> {c}', file=sys.stderr); return c
    step = 60
    for _ in range(14):
        c = probe(px, py)
        if c == (tx, ty): print(px, py); return
        cx, cy = probe(px + step, py), probe(px, py + step)
        j = [[(cx[0] - c[0]) / step, (cy[0] - c[0]) / step], [(cx[1] - c[1]) / step, (cy[1] - c[1]) / step]]
        det = j[0][0] * j[1][1] - j[0][1] * j[1][0]
        if abs(det) < 1e-6: step = int(step * 1.6); continue
        dx, dy = tx - c[0], ty - c[1]
        sx = (j[1][1] * dx - j[0][1] * dy) / det; sy = (-j[1][0] * dx + j[0][0] * dy) / det
        px, py = int(px + max(-300, min(300, sx))), int(py + max(-300, min(300, sy)))
    sys.exit(1)

main()
