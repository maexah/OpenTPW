#!/usr/bin/env python3
# gmove.py X Y [click] : glide the X pointer to X,Y in 15-pixel steps (the game reads relative motion); any third
#   argument then clicks button 1, 0.3 s on.
# gmove.py align : sweeps (GX0+2, 2) -> (GX0+GW, GH) -> (GX0+2, 2) so the game's cursor lines up with the pointer; no click.
#   GX0, GW, GH default 0, 637, 477: give GW=1022 GH=766 for the 1024 x 768 off-screen picture.
# The display is GDISPLAY (default :77). Needs xdotool.
import subprocess, sys, time, os
os.environ['DISPLAY'] = os.environ.get('GDISPLAY', ':77')
def loc():
    out = subprocess.check_output(['xdotool', 'getmouselocation']).decode().split()
    return int(out[0][2:]), int(out[1][2:])
def glide(tx, ty, step=15):
    x, y = loc()
    while (x, y) != (tx, ty):
        dx = max(-step, min(step, tx - x)); dy = max(-step, min(step, ty - y))
        x += dx; y += dy
        subprocess.run(['xdotool', 'mousemove', str(x), str(y)])
        time.sleep(0.02)
if sys.argv[1] == 'align':
    x0 = int(os.environ.get('GX0', 0)); glide(x0 + 2, 2); glide(x0 + int(os.environ.get('GW', 637)), int(os.environ.get('GH', 477))); glide(x0 + 2, 2)
else:
    glide(int(sys.argv[1]), int(sys.argv[2]))
    if len(sys.argv) > 3:
        time.sleep(0.3); subprocess.run(['xdotool', 'click', '1'])
