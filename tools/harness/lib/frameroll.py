#!/usr/bin/env python3
"""How far one frame's picture is turned and shifted against another of the same game (Q1109).

    frameroll.py REST.png MOVED.png [--box x0,y0,x1,y1] [--range N] [--reach DEG] [--step DEG]

REST and MOVED are two frames of one game at one size, MOVED taken with the camera turned a little (the orbit's lean
mid-pan) and moved by up to N pixels (80) each way. Both are turned into edge pictures (the gradient's size of the grey
level, as `sceneshift.py` does); MOVED's is turned about the frame's middle by each angle within DEG (1.5) each way, in
steps of DEG (0.05), and the shift that best matches REST's inside BOX (the middle half, by default) is found by an FFT
cross-correlation. Prints the angle that matches best, refined between its neighbours by a parabola, as how far MOVED's
picture is turned CLOCKWISE on the screen against REST's (negative: counter-clockwise), with its shift and correlation,
and the scores by angle. A roll about the view's middle is the same angle at any frame size, so the two games' numbers
compare directly. Where the shift is not whole pixels the angle reads up to about 0.04 degrees off (a 0.5 turn with a
shift of (30, -7) read 0.468; the same turn alone read 0.500).
"""
import sys
import numpy as np
from PIL import Image


def edges(source):
	image = source if isinstance(source, Image.Image) else Image.open(source)
	grey = np.asarray(image.convert("L")).astype(float)
	gy, gx = np.gradient(grey)
	return np.hypot(gx, gy)


def turned(e, degrees):
	# PIL turns a picture counter-clockwise for a positive angle, about its middle.
	return np.asarray(Image.fromarray(e.astype(np.float32), "F").rotate(degrees, Image.BILINEAR))


def shift(rest, moved, box, reach):
	x0, y0, x1, y1 = box
	t = rest[y0:y1, x0:x1]
	t = (t - t.mean()) / (t.std() + 1e-9)
	region = moved[y0 - reach:y1 + reach, x0 - reach:x1 + reach]
	region = (region - region.mean()) / (region.std() + 1e-9)
	h, w = region.shape
	pad = np.zeros_like(region); pad[:t.shape[0], :t.shape[1]] = t
	corr = np.real(np.fft.ifft2(np.fft.fft2(region) * np.conj(np.fft.fft2(pad))))
	corr = corr[:2 * reach + 1, :2 * reach + 1]
	dy, dx = np.unravel_index(int(np.argmax(corr)), corr.shape)
	part = moved[y0 + dy - reach:y1 + dy - reach, x0 + dx - reach:x1 + dx - reach]
	part = (part - part.mean()) / (part.std() + 1e-9)
	return float((t * part).mean()), int(dx - reach), int(dy - reach)


def rest_point(moved, frame_at, points, axis, box=None, reach=80):
	"""Where a camera moving along one map axis stood when MOVED was taken, for a rest frame to compare it with: the
	frame at each of POINTS (frame_at( point ) -> a PIL image of the camera put there and settled) is matched against
	MOVED with no turn, and the point where the screen shift along AXIS (0 across, 1 down) crosses nought is found by a
	line through the two candidates either side of it. A rest frame taken where the camera stood leaves no perspective
	change for the turn's fit to take up (a forward pan's frame against one 2.5 units away read a turn of 0.12 degrees
	that was not there). Answers (point, [(point, shift, correlation), ...])."""
	target = edges(moved)
	h, w = target.shape
	box = box or (w // 4, h // 4, 3 * w // 4, 3 * h // 4)
	seen = []
	for point in points:
		score, dx, dy = shift(edges(frame_at(point)), target, box, reach)
		seen.append((point, (dx, dy)[axis], score))
	# The crossing whose two points either side run the same way as it: a point whose true shift is past REACH answers a
	# wild one (the first of Q1109's had -12 against a true -140), which must not make a false crossing.
	best, agreed = None, 0
	for i in range(len(seen) - 1):
		(p0, s0, _), (p1, s1, _) = seen[i], seen[i + 1]
		if abs(s0) >= reach or abs(s1) >= reach or s0 == s1 or (s0 < 0) == (s1 < 0) and s1 != 0:
			continue
		way = 1 if s1 > s0 else -1
		run = [seen[j][1] for j in range(max(0, i - 2), min(len(seen), i + 4))]
		steps = sum(1 for a, b in zip(run, run[1:]) if abs(a) < reach and abs(b) < reach and (b - a) * way > 0)
		if steps > agreed:
			best, agreed = p0 + (p1 - p0) * s0 / (s0 - s1), steps
	if best is None:
		best = min(seen, key=lambda s: abs(s[1]))[0]
	return best, seen


def main(argv):
	args = list(argv)
	box, reach, span, step = None, 80, 1.5, 0.05
	for flag in ("--box", "--range", "--reach", "--step"):
		if flag in args:
			i = args.index(flag); value = args[i + 1]; del args[i:i + 2]
			if flag == "--box": box = tuple(int(v) for v in value.split(","))
			elif flag == "--range": reach = int(value)
			elif flag == "--reach": span = float(value)
			else: step = float(value)
	if len(args) != 2:
		print(__doc__); return 2
	rest, moved = edges(args[0]), edges(args[1])
	if rest.shape != moved.shape:
		print("the two frames differ in size", rest.shape, moved.shape); return 2
	h, w = rest.shape
	box = box or (w // 4, h // 4, 3 * w // 4, 3 * h // 4)
	if box[0] - reach < 0 or box[1] - reach < 0 or box[2] + reach > w or box[3] + reach > h:
		print("the box and the range reach past the frame"); return 2
	angles = np.round(np.arange(-span, span + step / 2, step), 4)
	scores = [shift(rest, turned(moved, a), box, reach) for a in angles]
	i = int(np.argmax([s[0] for s in scores]))
	best = angles[i]
	if 0 < i < len(angles) - 1:
		a, b, c = scores[i - 1][0], scores[i][0], scores[i + 1][0]
		bend = a - 2 * b + c
		if bend < 0:
			best = angles[i] + step * 0.5 * (a - c) / bend
	score, dx, dy = scores[i]
	print(f"turned clockwise {best:+.3f} deg (MOVED against REST; best step {angles[i]:+.2f}), shift dx {dx:+d} dy {dy:+d} "
		f"(MOVED's scene that far right and down once turned back), correlation {score:.4f}, box {box}, range {reach}")
	print("by angle: " + "; ".join(f"{a:+.2f} {s[0]:.4f}" for a, s in zip(angles, scores)))
	return 0


if __name__ == "__main__":
	sys.exit(main(sys.argv[1:]))
