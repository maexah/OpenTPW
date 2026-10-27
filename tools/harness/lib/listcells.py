"""lib/listcells.py (Q371): a list row's cells in a frame of either game. bars(png, ys, x0, x1, hgt) - barsplit.py on each bar rect (x0, y, x1, y + hgt), virtual units;
aligns(png, y0, y1, cols) - where a row band's white lettering stands in each column (left, right, mid as shares of the
column, and 'left'/'centre'/'right'). Either game's frame: virtual 2048 x 1536 scaled by the frame's height."""
import subprocess, sys, os
from PIL import Image
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
def bars(png, ys, x0, x1, hgt):
    args = [str(v) for y in ys for v in (x0, y, x1, y + hgt)]
    out = subprocess.run([sys.executable, paths.LIB + "/barsplit.py", png] + args, capture_output=True, text=True).stdout.split("\n")
    return [float(l.split()[0]) for l in out if l.strip()]
def aligns(png, y0, y1, cols, white=150):
    im = Image.open(png).convert("RGB"); w, h = im.size; s = h / 1536; ox = (w - 2048 * s) / 2; got = []
    for c0, c1 in cols:
        X0, X1 = int(round(c0 * s + ox)), int(round(c1 * s + ox)); xs = []
        for y in range(int(y0 * s) + 2, int(y1 * s) - 2):
            for x in range(X0, X1):
                p = im.getpixel((x, y))
                if min(p) >= white: xs.append(x)
        if not xs: got.append(None); continue
        wd = X1 - X0; l = (min(xs) - X0) / wd; r = (max(xs) - X0) / wd; m = (l + r) / 2
        kind = "right" if r >= 0.92 else "left" if l <= 0.10 else "centre" if abs(m - 0.5) <= 0.06 else "?"
        got.append((round(l, 3), round(r, 3), round(m, 3), kind))
    return got
if __name__ == "__main__":
    print(aligns(sys.argv[1], float(sys.argv[2]), float(sys.argv[3]), [tuple(map(float, c.split(","))) for c in sys.argv[4:]]))
def colour(png, ys, x0, x1, hgt, spread=60):
    """The share of each bar rect's middle row in colour (channel spread over `spread`): a ramp is, lettering is not."""
    im = Image.open(png).convert("RGB"); w, h = im.size; s = h / 1536; ox = (w - 2048 * s) / 2; got = []
    for y in ys:
        ym = int((y + hgt / 2) * s); xs = range(int(round(x0 * s + ox)), int(round(x1 * s + ox)))
        px = [im.getpixel((x, ym)) for x in xs]; got.append(round(sum(max(p) - min(p) > spread for p in px) / len(px), 3))
    return got
def ink(png, x0, x1, y0, y1, white=150, gap=6):
    """The bands of white lettering down one column (Q377): [(top, bottom)] in virtual units, a band a lettered row, from
    the pixel rows holding 2 or more white pixels between x0 and x1, split where `gap` virtual units or more hold none
    (a size, not a pixel count, so a frame scaled up from 640 x 480 or 800 x 600 reads as a native one). The
    pitch of a list's rows is the median step between band middles; a font's height the median band's (same text)."""
    im = Image.open(png).convert("RGB"); w, h = im.size; s = h / 1536; ox = (w - 2048 * s) / 2
    X0, X1 = int(round(x0 * s + ox)), int(round(x1 * s + ox)); rows = []
    for y in range(int(y0 * s), min(h, int(y1 * s) + 1)):
        n = sum(1 for x in range(X0, X1) if min(im.getpixel((x, y))) >= white)
        rows.append((y, n >= 2))
    bands, start, gaps = [], None, 0
    for y, lit in rows:
        if lit:
            if start is None: start = y
            last, gaps = y, 0
        elif start is not None:
            gaps += 1
            if gaps * 1.0 / s >= gap: bands.append((start / s, (last + 1) / s)); start = None
    if start is not None: bands.append((start / s, (last + 1) / s))
    return [(round(a, 1), round(b, 1)) for a, b in bands]
def pitch(bands):
    """The median step between neighbouring bands' middles, virtual units (None under two bands)."""
    mids = [(a + b) / 2 for a, b in bands]; steps = sorted(b - a for a, b in zip(mids, mids[1:]))
    return round(steps[len(steps) // 2], 2) if steps else None
