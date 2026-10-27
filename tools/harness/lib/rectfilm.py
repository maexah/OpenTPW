#!/usr/bin/env python3
"""Film one rectangle of a display as fast as it can be read, and time each change of its picture (Q640).

    rectfilm.py X Y W H SECS OUTDIR [--display :0] [--min 4] [--keep]

Reads the root window's rectangle (screen pixels; root.get_image, as the capture recipe: grabbing a Vulkan window
itself reads black) in a tight loop for SECS seconds. A sample is a CHANGE when its mean absolute difference from the
last change's picture is over --min (0 to 255 a channel; 4 keeps a compositor's dither out). Writes OUTDIR/changes.tsv
(the change's wall clock, its gap from the last change in ms, the mean difference, and which distinct picture it is,
the first of each saved as OUTDIR/pic-NNN.png), OUTDIR/samples.txt (how many reads, and the reads a second), and
prints the summary: reads a second, changes, the gaps' histogram in 10 ms bins, their mean, and the gaps in order.
--keep saves every change's picture (chg-NNNN.png) and not only the first of each distinct one.

The reads a second must be well over the rate the picture can change at (a 144 Hz screen: 7 ms; a read of a 150 x 60
rectangle takes about 0.05 ms here), or two changes merge into one. A change is timed to the read that saw it, so each gap is good to one
read's interval. Needs python-xlib and PIL: review-codex/venv/bin/python3.
"""
import argparse, hashlib, os, sys, time
from Xlib import display as xdisplay, X
from PIL import Image


def main():
	ap = argparse.ArgumentParser()
	ap.add_argument("x", type=int); ap.add_argument("y", type=int); ap.add_argument("w", type=int); ap.add_argument("h", type=int)
	ap.add_argument("secs", type=float); ap.add_argument("out")
	ap.add_argument("--display", default=":0"); ap.add_argument("--min", type=float, default=4.0)
	ap.add_argument("--keep", action="store_true")
	a = ap.parse_args()
	os.makedirs(a.out, exist_ok=True)
	d = xdisplay.Display(a.display); root = d.screen().root
	n = a.w * a.h

	def grab():
		return root.get_image(a.x, a.y, a.w, a.h, X.ZPixmap, 0xffffffff).data

	def diff(p, q):
		# BGRX: every fourth byte is padding; the mean of the other three's absolute differences, sampled every 4th pixel.
		s = 0; c = 0
		for i in range(0, len(p), 16):
			s += abs(p[i] - q[i]) + abs(p[i + 1] - q[i + 1]) + abs(p[i + 2] - q[i + 2]); c += 3
		return s / c if c else 0.0

	def save(data, name):
		Image.frombytes("RGB", (a.w, a.h), data, "raw", "BGRX").save(os.path.join(a.out, name))

	pics = {}; rows = []; reads = 0
	last = grab(); t0 = time.time(); last_t = t0
	h = hashlib.md5(last).hexdigest(); pics[h] = 0; save(last, "pic-000.png")
	rows.append((t0, 0.0, 0.0, 0))
	end = t0 + a.secs
	while True:
		now = time.time()
		if now >= end:
			break
		cur = grab(); reads += 1
		if cur == last:
			continue
		m = diff(cur, last)
		if m <= a.min:
			continue
		h = hashlib.md5(cur).hexdigest()
		if h not in pics:
			pics[h] = len(pics); save(cur, "pic-%03d.png" % pics[h])
		if a.keep:
			save(cur, "chg-%04d.png" % len(rows))
		rows.append((now, (now - last_t) * 1000.0, m, pics[h])); last = cur; last_t = now
	span = time.time() - t0
	with open(os.path.join(a.out, "changes.tsv"), "w") as f:
		f.write("wall\tgap_ms\tmeandiff\tpic\n")
		for t, g, m, p in rows:
			f.write("%.4f\t%.1f\t%.2f\t%d\n" % (t, g, m, p))
	with open(os.path.join(a.out, "samples.txt"), "w") as f:
		f.write("reads %d over %.3f s = %.1f a second; rect %d,%d %dx%d on %s\n" % (reads, span, reads / span, a.x, a.y, a.w, a.h, a.display))
	gaps = [r[1] for r in rows[2:]]   # the first change's gap is from the film's start, not from a change
	print("reads %d over %.2f s = %.0f a second; changes %d; distinct pictures %d" % (reads, span, reads / span, len(rows) - 1, len(pics)))
	if gaps:
		hist = {}
		for g in gaps:
			hist[int(g // 10) * 10] = hist.get(int(g // 10) * 10, 0) + 1
		print("gaps: mean %.1f ms, min %.1f, max %.1f; changes a second %.2f" % (sum(gaps) / len(gaps), min(gaps), max(gaps), (len(rows) - 2) / (rows[-1][0] - rows[1][0]) if len(rows) > 2 else 0))
		print("histogram (10 ms bins): " + " ".join("%d:%d" % (k, hist[k]) for k in sorted(hist)))
		print("in order: " + " ".join("%.0f" % g for g in gaps))


if __name__ == "__main__":
	main()
