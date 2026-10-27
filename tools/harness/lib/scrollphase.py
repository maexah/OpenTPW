#!/usr/bin/env python3
"""How far a repeating texture has scrolled in each picture of a rectfilm.py film (Q677).

    scrollphase.py FILMDIR PERIOD [--axis x|y] [--from S] [--to S] [--hann] [--move R]

FILMDIR is a lib/rectfilm.py output (changes.tsv and its pic-NNN.png). PERIOD is the texture's repeat on the screen in
pixels along the axis (a research pump: the quad's width / 1.998, its u span). Each picture is averaged across the other
axis to one line of brightness, cut to the most whole repeats it holds (round( n x PERIOD ) pixels from its start), and
the phase of its component at PERIOD read: the picture's offset in repeats, unwrapped from change to change (a change
must move under half a repeat). Prints the speed (a least-squares line through offset against the wall clock, in
repeats a second, + to larger x), the RMS and largest distance from that line in repeats, the steps between changes
(largest, mean, how many backwards, how many over a twentieth of a repeat), the gaps over 30 ms, and the STAND: from
each change, the time until the offset first stands R repeats (--move, default 1/62, half of a 31-picture loop's step)
from it, largest and median, the change it was longest at; and the residual's three strongest frequencies (resampled at
the changes' mean rate) beside the PIXEL RATE, speed x PERIOD: a sub-pixel scroll drawn on the pixel grid ripples once
a pixel crossed (Q677's round B: 15.73 Hz at a 15.69 Hz pixel rate), a stepped one at its pictures' rate. Writes FILMDIR/scroll.tsv (wall clock from the first
change, offset in repeats). --from/--to keep the changes between those seconds of the film (to leave out a start or a
stop).

A stepped animation stands a whole picture's time (the research pump in the original: 62 to 167 ms); a smooth scroll
moves R in R / speed (at 0.426 repeats a second, 38 ms) give or take the reading's noise and, where R is under a
pixel, the pixel grid's ripple (round B: a largest of 62.6 ms for a median of 41.7). A single step between two
drawn frames is a tenth of a pixel and reads within a few thousandths of a repeat of noise, so judge by the stand, not
the steps. The fundamental's phase shifts exactly with a pure shift of a periodic picture over whole repeats; the
window's rounding to whole pixels leaves a ripple of about a thousandth (--hann, a Hann window over the whole line,
leaves one of about a hundredth with a fractional count: Q677's round A). Needs numpy and PIL: review-codex/venv/bin/python3.
"""
import argparse, math, os
import numpy as np
from PIL import Image


def main():
	ap = argparse.ArgumentParser()
	ap.add_argument("film"); ap.add_argument("period", type=float)
	ap.add_argument("--axis", default="x", choices=("x", "y"))
	ap.add_argument("--from", dest="start", type=float, default=None); ap.add_argument("--to", dest="end", type=float, default=None)
	ap.add_argument("--hann", action="store_true"); ap.add_argument("--move", type=float, default=1 / 62)
	a = ap.parse_args()

	rows = [l.split("\t") for l in open(os.path.join(a.film, "changes.tsv")).read().split("\n")[1:] if l]
	t0 = float(rows[0][0])
	cache = {}

	def offset(pic):
		if pic not in cache:
			img = np.asarray(Image.open(os.path.join(a.film, "pic-%03d.png" % pic)).convert("L"), dtype=float)
			line = img.mean(axis=0 if a.axis == "x" else 1)
			if a.hann:
				n = len(line); line = (line - line.mean()) * np.hanning(n)
			else:
				n = int(round(max(1, int(len(line) / a.period)) * a.period)); line = line[:n] - line[:n].mean()
			x = np.arange(n)
			c = np.sum(line * np.exp(-2j * math.pi * x / a.period))
			cache[pic] = -np.angle(c) / (2 * math.pi)
		return cache[pic]

	ts, offs, gaps = [], [], []
	last = None; unwrapped = 0.0
	for r in rows:
		t = float(r[0]) - t0
		if (a.start is not None and t < a.start) or (a.end is not None and t > a.end):
			continue
		o = offset(int(r[3]))
		if last is not None:
			d = o - last
			d -= round(d)
			unwrapped += d
			gaps.append(float(r[1]))
		last = o
		ts.append(t); offs.append(unwrapped)

	ts = np.array(ts); offs = np.array(offs)
	speed, b = np.polyfit(ts, offs, 1)
	resid = offs - (speed * ts + b)
	steps = np.abs(np.diff(offs))
	with open(os.path.join(a.film, "scroll.tsv"), "w") as f:
		f.write("t\toffset\n")
		for t, o in zip(ts, offs):
			f.write("%.4f\t%.5f\n" % (t, o))
	long_gaps = [round(g, 1) for g in gaps[1:] if g > 30]
	stands = []
	for i in range(len(ts)):
		j = next((j for j in range(i + 1, len(ts)) if abs(offs[j] - offs[i]) >= a.move), None)
		if j is None:
			break
		stands.append((ts[j] - ts[i], ts[i]))
	print("changes %d over %.2f s; speed %+.5f repeats a second; from the line RMS %.5f, largest %.5f repeats" % (
		len(ts), ts[-1] - ts[0], speed, float(np.sqrt(np.mean(resid ** 2))), float(np.max(np.abs(resid)))))
	print("steps: largest %.5f, mean %.5f repeats; %d backwards; %d over 0.05 repeats; gaps over 30 ms: %d %s" % (
		float(steps.max()), float(np.mean(np.diff(offs))), int(np.sum(np.diff(offs) < 0)), int(np.sum(steps > 0.05)), len(long_gaps), long_gaps[:20]))
	if len(ts) > 16:
		rate = (len(ts) - 1) / (ts[-1] - ts[0]); tu = np.arange(ts[0], ts[-1], 1 / rate); ru = np.interp(tu, ts, resid)
		spec = np.abs(np.fft.rfft(ru - ru.mean())); freq = np.fft.rfftfreq(len(ru), 1 / rate); top = np.argsort(spec[1:])[::-1][:3] + 1
		print("the residual's strongest: %s Hz; the pixel rate %.2f Hz" % (", ".join("%.2f" % freq[i] for i in top), abs(speed) * a.period))
	if stands:
		worst = max(stands)
		print("stand (to move %.4f repeats): largest %.1f ms at %.2f s, median %.1f ms, over 55 ms %d of %d" % (
			a.move, worst[0] * 1000, worst[1], float(np.median([s for s, _ in stands])) * 1000, sum(1 for s, _ in stands if s > 0.055), len(stands)))


if __name__ == "__main__":
	main()
