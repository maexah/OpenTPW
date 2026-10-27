"""barsplit.py <png> x0 y0 x1 y1 ... : a type-9 bar's fill measured by where its grey track starts: the first pixel of
the middle row, from the left, that is track (channel spread <= 12, mean 40 to 75), as a share of the width; 1.0 when
none is. Unlike barshare's colour count it does not miss a ramp's greyish texels (happygrad's teal) and ignores a
highlighted row's band past the bar's end."""
import sys
from PIL import Image
im = Image.open(sys.argv[1]).convert("RGB"); w, h = im.size; s = h / 1536; ox = (w - 2048 * s) / 2
a = list(map(float, sys.argv[2:]))
for i in range(0, len(a), 4):
    x0, y0, x1, y1 = a[i:i + 4]; ym = int((y0 + y1) / 2 * s)
    xs = list(range(int(round(x0 * s + ox)), int(round(x1 * s + ox)))); px = [im.getpixel((x, ym)) for x in xs]
    track = lambda p: max(p) - min(p) <= 12 and 40 <= sum(p) / 3 <= 75
    j = next((k for k, p in enumerate(px) if track(p)), len(px))
    print(round(j / len(px), 3), len(px))
