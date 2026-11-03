#!/usr/bin/env python3
"""lobbypick.py PID EX EY EZ LX LY LZ [--slot X Y] [--pre S] [--post S] - the ORIGINAL's lobby, a pick out of the attract flight
FROM A CHOSEN STATE (Q920). `lobbypick.py PID natural [--lo 34 --hi 47 --secs 150 --near K --within R]` writes nothing: it waits for the flight's own
view to stand 34 to 47 degrees below the horizon with the nearest island not the first (Lost Kingdom's), or island K when --near is given,
and with --within the eye no more than R units (level) from island 0's centre, and then clicks, printing the state at the press. The attract object (found as attractcam.py finds it) keeps its flight eye at +0x78 and its look point at
+0xb4 (words 0x1e and 0x2d, y-up axes); every attract pass ends by copying them into the island camera's eased eye and look point
(`lobby.md`, "The two modes share one eye"), so a player picked out of the flight eases from whatever stands there.

It writes the six floats every millisecond for --pre seconds (default 0.6), presses and releases the pointer on the player's slot
(game point --slot, default 660 58 in the 1024 x 768 frame; the pointer is already lined up, `gmove.py align`), keeps writing
until --post seconds after the release (default 0.15: the orbit's first pass has read them by then) and stops. The flight's own
eye moves 10 units a second, so the state the pass reads at the click is the written one plus at most one frame's step.
Prints the wall clocks of the press and the release (the film's and rendereye.py's clock). The desktop only: GDISPLAY (default :0),
the picture 1920 x 1440 at x = 320 on a 2560 x 1440 screen (GX0, GH to change)."""
import os, re, struct, subprocess, sys, time

args = sys.argv[1:]
pid = int(args[0]); natural = args[1] == 'natural'
eye = look = (0.0, 0.0, 0.0) if natural else None
if not natural: eye = tuple(map(float, args[1:4])); look = tuple(map(float, args[4:7]))
def opt(name, default, n=1):
	if name not in args: return default
	i = args.index(name); v = tuple(float(x) for x in args[i + 1:i + 1 + n]); return v if n > 1 else v[0]
slot = opt('--slot', (660.0, 58.0), 2); pre = opt('--pre', 0.6); post = opt('--post', 0.15)
GD = os.environ.get('GDISPLAY', ':0'); GX0 = int(os.environ.get('GX0', 320)); GH = int(os.environ.get('GH', 1440))
os.environ['DISPLAY'] = GD
f = open(f'/proc/{pid}/mem', 'r+b', buffering=0)
def rd(a, fmt='I'): f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def rdf(a, n): f.seek(a); return struct.unpack('<%df' % n, f.read(4 * n))
def find():
	for line in open(f'/proc/{pid}/maps'):
		m = re.match(r'([0-9a-f]+)-([0-9a-f]+) rw', line)
		if not m: continue
		lo, hi = int(m[1], 16), int(m[2], 16)
		if hi - lo > 256 << 20 or lo >= 0x80000000: continue
		try: f.seek(lo); buf = f.read(hi - lo)
		except OSError: continue
		for vt in (b'\xc0\x2e\x70\x00', b'\xb0\x2c\x70\x00'):
			i = buf.find(vt)
			while i >= 0:
				a = lo + i; i = buf.find(vt, i + 4)
				if a & 3: continue
				try: lst = rd(a + 8); n = rd(lst + 0xc); bx = rdf(a + 0x16 * 4, 6)
				except (OSError, struct.error): continue
				if 0 < n < 10 and abs(bx[0] - 500) < 1 and abs(bx[3] - 400) < 1: return a
	return None
obj = find()
if obj is None: sys.exit('lobbypick: no attract object (is the lobby up, nobody picked?)')
blob = struct.pack('<3f', *eye); lblob = struct.pack('<3f', *look)
def put(): f.seek(obj + 0x1e * 4); f.write(blob); f.seek(obj + 0x2d * 4); f.write(lblob)
sx = int(GX0 + slot[0] * GH / 768); sy = int(slot[1] * GH / 768)
subprocess.run(['xdotool', 'mousemove', str(sx), str(sy)]); time.sleep(0.3)
print(f'object 0x{obj:08x} eye {eye} look {look} slot screen ({sx},{sy})', flush=True)
import math
def pitch_now():
	e = rdf(obj + 0x1e * 4, 3); l = rdf(obj + 0x2d * 4, 3)
	return math.degrees(math.atan2(e[1] - l[1], math.hypot(e[0] - l[0], e[2] - l[2]))), e, l
if natural:
	lst = rd(obj + 8); node = rd(lst + 4); cent = []
	while node: cent.append(rdf(rd(node) + 0x3c, 3)); node = rd(node + 4)
	lo, hi, secs = opt('--lo', 34.0), opt('--hi', 47.0), opt('--secs', 150.0); t0 = time.time()
	while time.time() - t0 < secs:
		p, e, l = pitch_now(); near = min(range(len(cent)), key=lambda k: math.dist(e, cent[k]))
		ok_near = near == int(opt('--near', -1)) if '--near' in args else near != 0
		ok_in = ('--within' not in args) or math.hypot(e[0] - cent[0][0], e[2] - cent[0][2]) <= opt('--within', 1e9)
		if lo <= p <= hi and ok_near and ok_in: break
		time.sleep(0.002)
	else: sys.exit('lobbypick: no start in range within the time')
	print(f'natural start pitch {p:.2f} eye {e} look {l} nearest island {near}', flush=True)
	pre = 0.0
t0 = time.time(); pressed = released = None
while True:
	if not natural: put()
	t = time.time() - t0
	if pressed is None and t >= pre: subprocess.Popen(['xdotool', 'mousedown', '1']); pressed = time.time()
	if pressed and released is None and time.time() - pressed >= 0.12: subprocess.Popen(['xdotool', 'mouseup', '1']); released = time.time()
	if released and time.time() - released >= post: break
	time.sleep(0.001)
print(f'press {pressed:.4f} release {released:.4f}', flush=True)
