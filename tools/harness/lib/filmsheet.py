#!/usr/bin/env python3
"""filmsheet.py FILM WALL0 FROM TO STEP OUT.png [W] : frames of a wall-clock film (loadtime.py's) every STEP s from WALL0+FROM to WALL0+TO, labelled
with their time after WALL0, as a contact sheet of 4 columns."""
import subprocess as sp, sys, numpy as np
from PIL import Image, ImageDraw
film, w0, a, b, step, out = sys.argv[1], float(sys.argv[2]), float(sys.argv[3]), float(sys.argv[4]), float(sys.argv[5]), sys.argv[6]
W = int(sys.argv[7]) if len(sys.argv) > 7 else 320
info = sp.check_output(["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries", "stream=width,height", "-of", "csv=p=0", film]).decode().strip().split(",")
fw, fh = int(info[0]), int(info[1])
pts = [float(x) for x in sp.check_output(["ffprobe", "-v", "error", "-select_streams", "v:0", "-show_entries", "frame=pts_time", "-of", "csv=p=0", film]).decode().replace(",", " ").split()]
raw = sp.check_output(["ffmpeg", "-v", "error", "-i", film, "-f", "rawvideo", "-pix_fmt", "rgb24", "-"])
fr = np.frombuffer(raw, np.uint8).reshape(-1, fh, fw, 3); n = min(len(pts), len(fr)); pts = np.array(pts[:n])
want = np.arange(a, b + 1e-6, step); tiles = []
for t in want:
    i = int(np.argmin(np.abs(pts - (w0 + t)))); im = Image.fromarray(fr[i]); im = im.resize((W, int(W * fh / fw)))
    ImageDraw.Draw(im).text((4, 4), f"{pts[i] - w0:+.3f}", fill=(255, 255, 0)); tiles.append(im)
cols = 4; rows = (len(tiles) + cols - 1) // cols; th = tiles[0].size[1]
sheet = Image.new("RGB", (cols * W, rows * th))
for k, im in enumerate(tiles): sheet.paste(im, ((k % cols) * W, (k // cols) * th))
sheet.save(out); print(out, sheet.size)
