#!/usr/bin/env python3
"""lens.py PID [SECS] - the ORIGINAL's lens at this moment, from its memory (Q260; docs/exe/park-engine.md, "The lens").

The angle handed to the projection builder FUN_00578be0 (DAT_0087af58), its near and far (DAT_0087b1dc, 0x00768acc), the
projection 0x0087b110 it built (m00 = m11 = cos(a/2), m23 = sin(a/2) when perspective), the screen scale 0x0087b0d0 that
FUN_005783b0 multiplies into the draw matrix [0x0087b088] (2 x DAT_008bcbcc across, -2 down), DAT_008bcbcc itself, the
lobby render camera's own angle ([0x00f83778] + 4, the one FUN_005d8cb0 passes) and gui_CameraFlags [0x00790ab0].
Then the picture: half-tangent = tan(a/2) / scale, each way, and the angles top to bottom and side to side.
With SECS, one line a change for SECS seconds (the first line always). Read-only."""
import math, struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]) if len(sys.argv) > 2 else 0
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt):
	f.seek(a); v = struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt))); return v[0] if len(v) == 1 else v

def line():
	ang = rd(0x0087af58, 'f'); near = rd(0x0087b1dc, 'f'); far = rd(0x00768acc, 'f')
	P = rd(0x0087b110, '16f'); S = rd(0x0087b0d0, '16f'); asp = rd(0x008bcbcc, 'f'); flags = rd(0x00790ab0, 'I')
	lob = rd(0x00f83778, 'I'); lang = rd(lob + 4, 'f') if lob else float('nan')
	half = math.atan2(P[11], P[0]) if P[11] else float('nan')        # P[11]: m23, the sin(a/2) the w column takes
	t = math.tan(half); sx, sy = abs(S[0]), abs(S[5])
	v = 2 * math.degrees(math.atan(t / sy)) if sy else float('nan'); h = 2 * math.degrees(math.atan(t / sx)) if sx else float('nan')
	return ('angle %.3f near %.3f far %.1f | proj m00 %.5f m11 %.5f m22 %.5f m23 %.5f m32 %.4f m33 %.1f -> %.3f deg | '
	        'scale %.3f %.3f %.3f %.3f aspect %.4f | lobby cam %#x angle %.3f | flags %#x | picture %.2f deg down, %.2f across'
	        % (ang, near, far, P[0], P[5], P[10], P[11], P[14], P[15], 2 * math.degrees(half), S[0], S[5], S[10], S[15], asp,
	           lob, lang, flags, v, h))

last = None; end = time.time() + secs
while True:
	s = line()
	if s != last: print('%.2f %s' % (time.time(), s), flush=True); last = s
	if time.time() >= end: break
	time.sleep(0.25)
