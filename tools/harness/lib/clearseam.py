#!/usr/bin/env python3
"""clearseam.py <png> ... : how many pixels of a frame are the park's clear colour showing through the ground, a line a frame:
`<file>: N`. The colour is the fog and clear colour 0x44DDFF as the frame holds it by day (r 55 to 100, g 195 to 235, b over
234, g - r over 120); the gadget's own corner (x under 240, y over 380 of a 1280 x 720 frame) is left out. A hole in the
floor or the ground shows it as a thin light-blue sliver (Q804); a frame without one reads 0 (the original's reads 0 or 1).
Not a hue test of the whole frame: a lit sky or water of the same blue would count, so look at the frame too."""
import sys
from PIL import Image
for f in sys.argv[1:]:
    im = Image.open(f).convert("RGB"); w, h = im.size; px = im.load(); n = 0
    s = h / 720
    for y in range(h):
        for x in range(w):
            if x < 240 * s and y > 380 * s:
                continue
            r, g, b = px[x, y]
            if 55 <= r <= 100 and 195 <= g <= 235 and b >= 235 and g - r > 120:
                n += 1
    print(f"{f}: {n}")
