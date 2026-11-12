#!/usr/bin/env python3
"""How far this game's frame stands from the original's at one camera, in the original's pixels (Q1107).

    sceneshift.py ORIGINAL.png OURS.png [--box x0,y0,x1,y1] [--range N] [--out SIDE.png]

ORIGINAL is the original's 1024 x 768 frame, OURS this game's frame of any 16:9 or 4:3 size, both at the same camera
(`bothcam.py`). OURS is laid on the original's pixels as `bothcam.py` says: its middle 4:3 (960 x 720 of 1280 x 720)
scaled to 1024 x 768. Both are turned into edge pictures (the gradient's size of the grey level, so lighting and
texture tiles count for little) and the shift (dx, dy) of OURS, within N pixels each way (24), that best matches the
original's inside BOX (the middle half, by default) is printed with its correlation, and the next best rows. A
positive dy means this game draws the scene lower than the original. `--out` writes the original, this game mapped,
and the two blended (red the original's edges, green this game's), side by side.
"""
import sys
import numpy as np
from PIL import Image


def mapped(path, size=(1024, 768)):
	image = Image.open(path).convert("RGB")
	w, h = image.size
	across = h * 4 // 3
	left = (w - across) // 2
	return image.crop((left, 0, left + across, h)).resize(size, Image.BICUBIC)


def edges(image):
	grey = np.asarray(image.convert("L")).astype(float)
	gy, gx = np.gradient(grey)
	e = np.hypot(gx, gy)
	return e / (e.mean() + 1e-9)


def best(a, b, box, reach):
	x0, y0, x1, y1 = box
	target = a[y0:y1, x0:x1]
	target = (target - target.mean()) / (target.std() + 1e-9)
	scores = []
	for dy in range(-reach, reach + 1):
		for dx in range(-reach, reach + 1):
			part = b[y0 + dy:y1 + dy, x0 + dx:x1 + dx]
			part = (part - part.mean()) / (part.std() + 1e-9)
			scores.append((float((target * part).mean()), dx, dy))
	scores.sort(reverse=True)
	return scores


def main(argv):
	args = list(argv)
	box, reach, out = None, 24, None
	if "--box" in args:
		i = args.index("--box"); box = tuple(int(v) for v in args[i + 1].split(",")); del args[i:i + 2]
	if "--range" in args:
		i = args.index("--range"); reach = int(args[i + 1]); del args[i:i + 2]
	if "--out" in args:
		i = args.index("--out"); out = args[i + 1]; del args[i:i + 2]
	if len(args) != 2:
		print(__doc__); return 2
	original = Image.open(args[0]).convert("RGB")
	ours = mapped(args[1], original.size)
	w, h = original.size
	box = box or (w // 4, h // 4, 3 * w // 4, 3 * h // 4)
	if box[0] - reach < 0 or box[1] - reach < 0 or box[2] + reach > w or box[3] + reach > h:
		print("the box and the range reach past the frame"); return 2
	scores = best(edges(original), edges(ours), box, reach)
	score, dx, dy = scores[0]
	print(f"best shift dx {dx:+d} dy {dy:+d} (this game draws the scene that far right and down of the original), correlation {score:.3f}, box {box}, range {reach}")
	print("next: " + "; ".join(f"dx {x:+d} dy {y:+d} {s:.3f}" for s, x, y in scores[1:5]))
	if out:
		ea, eb = edges(original), edges(ours)
		blend = np.zeros((h, w, 3), np.uint8)
		blend[..., 0] = np.clip(ea * 60, 0, 255); blend[..., 1] = np.clip(eb * 60, 0, 255)
		side = Image.new("RGB", (w * 3, h))
		side.paste(original, (0, 0)); side.paste(ours, (w, 0)); side.paste(Image.fromarray(blend), (2 * w, 0))
		side.save(out)
		print("wrote", out)
	return 0


if __name__ == "__main__":
	sys.exit(main(sys.argv[1:]))
