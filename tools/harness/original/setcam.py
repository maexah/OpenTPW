#!/usr/bin/env python3
"""setcam.py - the running ORIGINAL's park camera WRITTEN: a point, a zoom and a rotation, and the follow switched (Q440).

  setcam.py PID X,Z ZOOM ROT [off|on]   the point looked at (world units, a cell is 10: camera.py's first and third
                                        coordinates), the zoom and the rotation IN DEGREES; with `off` the follow is
                                        stopped first, with `on` started after (it needs a person on the locator)
  setcam.py PID off|on                  the follow alone: gui_CameraFlags [0x00790ab0] bit 0x1000 cleared or set

What it writes is what the game's own snap writes (FUN_0042aab0, docs/exe/park-engine.md, "The camera goes to a thing"):
the three scroll terms 0x00790a88..0x00790a90 noughted, the point 0x007908f0 and 0x007908f8, the zoom 0x007909ec, and
the rotation into both 0x00790a38 (the one asked for) and 0x007909e0 (the one drawn, which the update eases towards the
other at about dt/140 a frame), so the next frame is drawn at the new camera with no swing. The update clamps the zoom
to 70..130 and the rotation is kept in 0..360. The original's eye is the point + (-h sin r, sin(p) zoom, -h cos r),
h = cos(p) zoom; this game's `camera X Z ZOOM YAW` puts its eye at +h sin, so the same picture is YAW = -ROT
(lib/bothcam.py does the turn; park-engine.md, "The park camera").

With the follow on (a locator's person, or a window opened from its strip) the locator's frame handler FUN_004b3640
puts the point back on the person every frame: a point written then lasts one frame, so the tool says so and leaves
the follow alone unless told `off`. Any pan stops the follow too: the keypad's 8/2/4/6, or the pointer past 0.95 of
the way to an edge while the game has the focus (0x0042ba37).

After the write it waits 0.3 s and reads back: the words, the follow bit, the camera's matrix's eye (0x0087aff0, its
last row) beside the formula's on the horizontal (the height also carries the eased ground under the point), and the
difference. Exit 2 where the eye is more than 0.05 units from the formula or the point did not stay."""
import math, struct, sys, time

FLAGS, FOLLOW = 0x00790ab0, 0x1000
POINT, ZOOM, ROT, ROT_DRAWN, SCROLL = 0x007908f0, 0x007909ec, 0x00790a38, 0x007909e0, 0x00790a88
MATRIX = 0x0087aff0

def main():
    a = sys.argv[1:]
    if len(a) < 2:
        print(__doc__); sys.exit(1)
    pid = int(a[0]); f = open(f'/proc/{pid}/mem', 'r+b', buffering=0)
    def rd(addr, fmt='<I'):
        f.seek(addr); v = struct.unpack(fmt, f.read(struct.calcsize(fmt))); return v[0] if len(v) == 1 else v
    def wr(addr, fmt, *v):
        f.seek(addr); f.write(struct.pack(fmt, *v))
    def follow(on):
        flags = rd(FLAGS); wr(FLAGS, '<I', (flags | FOLLOW) if on else (flags & ~FOLLOW))
        print('follow %s: gui_CameraFlags 0x%x -> 0x%x' % ('on' if on else 'off', flags, rd(FLAGS)))
    if a[1] in ('off', 'on'):
        follow(a[1] == 'on'); return
    x, z = (float(v) for v in a[1].split(',')); zoom = float(a[2]); deg = float(a[3]) % 360
    switch = a[4] if len(a) > 4 else None
    if zoom < 70 or zoom > 130: print('zoom %.1f: the update clamps it to 70..130' % zoom)
    if switch == 'off': follow(False)
    if rd(FLAGS) & FOLLOW and switch != 'on':
        print('the follow is ON: the point written lasts one frame (add `off`)')
    r = math.radians(deg)
    wr(SCROLL, '<3f', 0.0, 0.0, 0.0)
    wr(POINT, '<f', x); wr(POINT + 8, '<f', z)
    wr(ZOOM, '<f', zoom); wr(ROT, '<f', r); wr(ROT_DRAWN, '<f', r)
    if switch == 'on': follow(True)
    time.sleep(0.3)
    p = rd(POINT, '<3f'); zm = rd(ZOOM, '<f'); ro = rd(ROT, '<f'); rd2 = rd(ROT_DRAWN, '<f'); fl = rd(FLAGS)
    eye = rd(MATRIX, '<16f')[12:15]
    pitch = math.radians(45 + 20 * min(1, max(0, (zm - 70) / 60)))
    h = math.cos(pitch) * zm
    want = (p[0] - h * math.sin(rd2), p[2] - h * math.cos(rd2))
    off = math.hypot(eye[0] - want[0], eye[2] - want[1])
    print('written: point (%.3f, %.3f) zoom %.3f rotation %.2f deg' % (x, z, zoom, deg))
    print('read:    point (%.3f, %.3f, %.3f) zoom %.3f rotation %.5f (drawn %.5f) = %.2f deg, follow %s' % (
        *p, zm, ro, rd2, math.degrees(rd2), 'on' if fl & FOLLOW else 'off'))
    print('eye (%.3f, %.3f, %.3f); the formula\'s horizontal (%.3f, %.3f); %.3f units apart' % (*eye, *want, off))
    stayed = abs(p[0] - x) < 0.01 and abs(p[2] - z) < 0.01
    if not stayed: print('the point did NOT stay: (%.3f, %.3f) where (%.3f, %.3f) was written' % (p[0], p[2], x, z))
    sys.exit(0 if stayed and off < 0.05 else 2)

main()
