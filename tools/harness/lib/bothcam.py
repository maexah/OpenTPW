#!/usr/bin/env python3
"""bothcam.py - one camera, both games (Q440). The numbers are the ORIGINAL's, as original/camera.py prints them: the
point looked at (x and z, world units, a cell is 10), the zoom, and the rotation in degrees.

    console( x, z, zoom, rot )            this game's console line, `camera X Z ZOOM YAW` with YAW = -ROT: the original's
                                          eye is the point + (-h sin r, ., -h cos r), this game's EyeAt + (h sin yaw,
                                          -h cos yaw) (park-engine.md, "The park camera"), so the same picture is the
                                          rotation negated. At 0 and 180 the two agree either way
    ours( reply, x, z, zoom, rot )        exec'd after parkrun.py: the line sent through its reply(); answers the reply
    original( pid, x, z, zoom, rot, follow='off' )   original/setcam.py on the running original (the follow stopped
                                          first unless follow is None); answers (exit code, its output). Exit 2: the
                                          point did not stay or the eye is off the formula (read the output)
    both( reply, pid, x, z, zoom, rot )   both, this game first; answers (our reply, (code, output))
    to_original( x, y, w=1280, h=720 )    this game's window pixel as the original's 1024 x 768 one at the same camera (Q478)
    The original's update clamps the zoom to 70..130 (setcam.py reads it back): above 130 it stands at 130 while this
    game takes the number, so the two pictures differ by the ratio (Q478's first round)

    bothcam.py X,Z ZOOM ROT [PID]         prints the console line; with PID sets the original too and prints setcam's lines

At the same numbers the eye is the same in both games (Q435, Q440) and so is the lens (53.13 degrees top to bottom,
Q260, built Q441): this game's 1280 x 720 pixel is (640, 360) + (the original's 1024 x 768 pixel - (512, 384)) x 720 / 768,
and its middle 960 x 720 is the original's whole frame. Measured at rotations 0, 90 and 180 (q441/)."""
import os, subprocess, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths

SETCAM = os.path.join(paths.ORIGINAL_TOOLS, "setcam.py")

def console(x, z, zoom, rot):
    yaw = -float(rot)
    return f"camera {float(x):g} {float(z):g} {float(zoom):g} {yaw + 0.0:g}"

def to_original(x, y, w=1280, h=720):
    return (round(512 + (x - w / 2) * 768 / h), round(384 + (y - h / 2) * 768 / h))

def ours(reply, x, z, zoom, rot):
    return reply(console(x, z, zoom, rot), 0.6, 1)

def original(pid, x, z, zoom, rot, follow='off'):
    args = [sys.executable, SETCAM, str(pid), f"{float(x)},{float(z)}", str(float(zoom)), str(float(rot))]
    if follow: args.append(follow)
    r = subprocess.run(args, capture_output=True, text=True)
    return r.returncode, r.stdout + r.stderr

def both(reply, pid, x, z, zoom, rot):
    return ours(reply, x, z, zoom, rot), original(pid, x, z, zoom, rot)

if __name__ == "__main__":
    if len(sys.argv) < 4:
        print(__doc__); sys.exit(1)
    x, z = sys.argv[1].split(","); zoom, rot = sys.argv[2], sys.argv[3]
    print(console(x, z, zoom, rot))
    if len(sys.argv) > 4:
        code, out = original(int(sys.argv[4]), x, z, zoom, rot)
        print(out, end=""); sys.exit(code)
