#!/usr/bin/env python3
"""lib/texmatch.py (Q373): which of a set of textures a frame of either game draws in a cell.

texmatch.py <frame.png> <x0> <y0> <x1> <y1> <texture.png>... - one cell, virtual 2048 x 1536 units (the frame scaled
by its height and centred, as listcells.py), against each texture (a PNG with alpha: wctpng.py's output).
Importable: match( png, rect, textures ) answers None where the cell holds no picture, else
(best name, its score, the next best's score, the picture's box in pixels), and ranked( ... ) every score.

How: inside the cell, the picture is the box round the pixels brighter than LIGHT (the thought bubbles' cloud; a list's
row, black or the selection's teal, is darker). Each texture's own opaque box (alpha over half) is stretched onto that
box, and the score is the mean absolute difference of the colour where the texture is opaque and darker than LIGHT
(the icon, not the cloud every bubble shares), 0 to 255: the least is the picture. A cell with fewer than MIN light pixels holds no picture. The margin between the best and the next is the
measure's confidence: read it, never the best alone."""
import os, sys
from PIL import Image

LIGHT = 170
MIN = 12


def _cell(png, rect):
    im = Image.open(png).convert("RGB"); w, h = im.size; s = h / 1536; ox = (w - 2048 * s) / 2
    x0, y0, x1, y1 = rect
    return im, (int(round(x0 * s + ox)), int(round(y0 * s)), int(round(x1 * s + ox)), int(round(y1 * s)))


def _box(im, box):
    X0, Y0, X1, Y1 = box; xs = []; ys = []
    for y in range(Y0, Y1):
        for x in range(X0, X1):
            p = im.getpixel((x, y))
            if sum(p) / 3 >= LIGHT:
                xs.append(x); ys.append(y)
    if len(xs) < MIN:
        return None
    return min(xs), min(ys), max(xs) + 1, max(ys) + 1


def _texture(path):
    t = Image.open(path).convert("RGBA"); a = t.getchannel("A").point(lambda v: 255 if v > 127 else 0)
    return t.crop(a.getbbox())


def ranked(png, rect, textures):
    im, box = _cell(png, rect); found = _box(im, box)
    if found is None:
        return None, []
    crop = im.crop(found); w, h = crop.size; scores = []
    for path in textures:
        t = _texture(path).resize((w, h), Image.BILINEAR); n = 0; d = 0
        for y in range(h):
            for x in range(w):
                r, g, b, a = t.getpixel((x, y))
                if a > 127 and (r + g + b) / 3 < LIGHT:
                    p = crop.getpixel((x, y)); d += abs(p[0] - r) + abs(p[1] - g) + abs(p[2] - b); n += 3
        scores.append((round(d / max(n, 1), 1), os.path.basename(path)[:-4]))
    return found, sorted(scores)


def match(png, rect, textures):
    found, scores = ranked(png, rect, textures)
    if found is None:
        return None
    return scores[0][1], scores[0][0], scores[1][0] if len(scores) > 1 else None, found


if __name__ == "__main__":
    print(match(sys.argv[1], tuple(float(v) for v in sys.argv[2:6]), sys.argv[6:]))
