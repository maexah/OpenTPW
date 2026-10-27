#!/usr/bin/env python3
"""camto.py PID X,Y [TOL] - pan the running ORIGINAL's park camera until the point it looks at (0x007908f0, x and z
over ten) is within TOL cells (default 1.5) of cell (X,Y)'s middle, by resting the pointer on an edge of the 1024 x 768
off-screen frame (its arrow keys turn the view and do not pan: TOOLING.md, "The patrol tool in either game"); prints
the point after each push on stderr and the last on stdout; exits 1 after 10 pushes short of it (Q439).

The edges' sense is learnt, not assumed: each push is held for the distance at the last measured rate (8 cells a second
to start) and the point read again, so a rotated camera still gets there. The pointer ends in the middle. The
original's camera trails a person whose window was opened (Q435, Q438): aim and click straight after it (cellfind.py).
Display GDISPLAY (default :77). Needs xdotool."""
import os, struct, subprocess, sys, time
os.environ['DISPLAY'] = os.environ.get('GDISPLAY', ':77')

def loc():
    out = subprocess.check_output(['xdotool', 'getmouselocation']).decode().split()
    return int(out[0][2:]), int(out[1][2:])

def glide(tx, ty, step=15):
    x, y = loc()
    while (x, y) != (tx, ty):
        x += max(-step, min(step, tx - x)); y += max(-step, min(step, ty - y))
        subprocess.run(['xdotool', 'mousemove', str(x), str(y)]); time.sleep(0.02)

EDGES = {'right': (1022, 384), 'left': (2, 384), 'down': (512, 766), 'up': (512, 2)}

def main():
    pid = int(sys.argv[1]); tx, ty = (float(v) + 0.5 for v in sys.argv[2].split(','))
    tol = float(sys.argv[3]) if len(sys.argv) > 3 else 1.5
    f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
    def poi():
        f.seek(0x007908f0); x, _, z = struct.unpack('<3f', f.read(12)); return x / 10, z / 10
    # edge -> [axis, sign, cells a second], guessed until measured: screen right +x, screen down +y
    sense = {'right': [0, 1, 8.0], 'left': [0, -1, 8.0], 'down': [1, 1, 8.0], 'up': [1, -1, 8.0]}
    glide(512, 384); time.sleep(0.3)
    for _ in range(10):
        p = poi(); print('poi cell (%.2f, %.2f)' % p, file=sys.stderr)
        d = (tx - p[0], ty - p[1])
        if abs(d[0]) <= tol and abs(d[1]) <= tol: print('%.2f %.2f' % p); return
        axis = 0 if abs(d[0]) >= abs(d[1]) else 1
        want = 1 if d[axis] > 0 else -1
        name = next((k for k, v in sense.items() if v[0] == axis and v[1] == want), None) \
            or {(0, 1): 'right', (0, -1): 'left', (1, 1): 'down', (1, -1): 'up'}[(axis, want)]
        secs = min(3.0, max(0.1, abs(d[axis]) / sense[name][2]))
        t0 = time.time(); glide(*EDGES[name]); held = max(0.0, secs - (time.time() - t0)); time.sleep(held)
        glide(512, 384); time.sleep(0.4)
        q = poi(); moved = (q[0] - p[0], q[1] - p[1])
        print(f'{name} edge {secs:.2f} s moved ({moved[0]:.2f}, {moved[1]:.2f})', file=sys.stderr)
        big = 0 if abs(moved[0]) >= abs(moved[1]) else 1
        if abs(moved[big]) > 0.3:
            sense[name] = [big, 1 if moved[big] > 0 else -1, max(1.0, abs(moved[big]) / max(secs, 0.1))]
            other = {'right': 'left', 'left': 'right', 'up': 'down', 'down': 'up'}[name]
            sense[other] = [big, -sense[name][1], sense[name][2]]   # the opposite edge the opposite way
    print('%.2f %.2f' % poi()); sys.exit(1)

main()
