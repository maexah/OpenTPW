#!/usr/bin/env python3
"""spritesheet.py <bank.ESP> <out.png> <set>... - a sprite bank's picture sets as the game decodes them, WITH their alpha:
each set two rows, its frames blended over black above and over white below (the sheet stays dark green where a frame writes nothing), enlarged four times; and for each frame its size, its
pixels, the spread of its alpha and its mean colour. The .TPC is read from beside the .ESP (`wadcat --dump SPR_PA.ESP
esprites.wad` and `--dump SPR_PA.TPC` write both to the working directory, named with their folder and a backslash: rename them). The particles' sets are in
Generic\\Particles\\SPR_PA: set = an effect's sprite set word (`particles` prints it). Decoder: q98/sheet.py's, plus the
palette's fourth byte as alpha (SpritePackFile.Put).
`spritesheet.py <bank.TPC>... --find W H OX OY`: every picture of that size and origin (the stored origin is negative;
either sign matches) in the packs named, with the columns and rows its alpha reaches (the figure's own edges, not its
box), and the columns its alpha of 128 or more reaches over the origin's row (what a frame's difference with the people
hidden shows of him, the console's `sprites 0`): what a frame's measured edges are predicted from (Q475)."""
import os, struct, sys
from PIL import Image
def load_pack(path):
    b = open(path, "rb").read(); ver, _, count = struct.unpack_from("<HHI", b, 0); pal = b[8:8 + 1024]; at = 8 + 1024; pics = []
    for i in range(count):
        size, = struct.unpack_from("<I", b, at); at += 4; w, h = struct.unpack_from("<HH", b, at); at += 8
        ox, oy = struct.unpack_from("<ii", b, at); at += 8; pics.append((w, h, b[at:at + size])); ORIGINS.append((ox, oy)); at += size
    return pal, pics
ORIGINS = []
def decode(pic, pal):
    """{(x, y): (r, g, b, a)} of every pixel a row's codes write, palette index nought included."""
    w, h, data = pic; pos = 0; out = {}
    for y in range(h):
        if pos >= len(data): break
        rowend = pos + 1 + data[pos]; pos += 1; x = 0
        while pos < rowend and pos < len(data):
            c = data[pos]; pos += 1
            if c > 127:
                idx = data[pos] if pos < len(data) else 0; pos += 1
                for _ in range(256 - c):
                    if x < w: out[(x, y)] = (pal[idx * 4 + 2], pal[idx * 4 + 1], pal[idx * 4], pal[idx * 4 + 3])
                    x += 1
            else:
                for _ in range(c):
                    if pos < len(data):
                        idx = data[pos]; pos += 1
                        if x < w: out[(x, y)] = (pal[idx * 4 + 2], pal[idx * 4 + 1], pal[idx * 4], pal[idx * 4 + 3])
                        x += 1
        pos = rowend
    return out
if __name__ == "__main__" and "--find" in sys.argv:
    at = sys.argv.index("--find"); W, Hh, OX, OY = (int(v) for v in sys.argv[at + 1:at + 5])
    for tpc in sys.argv[1:at]:
        ORIGINS.clear(); pal, pics = load_pack(tpc)
        for k, pic in enumerate(pics):
            ox, oy = ORIGINS[k]
            if (pic[0], pic[1], abs(ox), abs(oy)) != (W, Hh, abs(OX), abs(OY)): continue
            seen = [xy for xy, v in decode(pic, pal).items() if v[3]]
            xs = [x for x, _ in seen]; ys = [y for _, y in seen]
            solid = [x for (x, y), v in decode(pic, pal).items() if v[3] >= 128 and y < abs(oy) - 1]
            print("%s picture %d: %dx%d origin %d,%d: alpha in columns %d..%d, rows %d..%d (%d pixels); alpha 128 or more over "
                  "the origin's row in columns %d..%d" % (os.path.basename(tpc), k, pic[0], pic[1], ox, oy, min(xs), max(xs),
                  min(ys), max(ys), len(seen), min(solid), max(solid)))
    sys.exit(0)
if __name__ == "__main__":
    esp = sys.argv[1]; raw = open(esp, "rb").read(); pal, pics = load_pack(os.path.splitext(esp)[0] + ".TPC"); sets = [int(v) for v in sys.argv[3:]]
    print("pack: %d pictures; palette alphas %s" % (len(pics), sorted(set(pal[i * 4 + 3] for i in range(256)))[:20]))
    rows = []
    for s in sets:
        first, fpd, dirn = struct.unpack_from("<HBB", raw, 0x10E + s * 4); rows.append((s, list(range(first, first + fpd))))
    C = max(max(pics[k][0], pics[k][1]) for _, ks in rows for k in ks) + 4; n = max(len(ks) for _, ks in rows)
    sheet = Image.new("RGB", (C * n, C * 2 * len(rows)), (40, 70, 40))
    for r, (s, ks) in enumerate(rows):
        for c, k in enumerate(ks):
            px = decode(pics[k], pal); al = sorted(v[3] for v in px.values()); seen = [v for v in px.values() if v[3]]
            for half, back in ((0, 0), (1, 255)):
                for (x, y), (R, G, B, A) in px.items():
                    sheet.putpixel((c * C + 2 + x, (r * 2 + half) * C + 2 + y), tuple((v * A + back * (255 - A)) // 255 for v in (R, G, B)))
            m = [sum(v[i] for v in seen) // max(1, len(seen)) for i in range(3)]
            print("set %d frame %d picture %d: %dx%d, %d pixels written, alpha least %d middle %d most %d, %d of them between 1 and 239, mean colour of the seen %s"
                  % (s, c, k, pics[k][0], pics[k][1], len(px), al[0] if al else 0, al[len(al) // 2] if al else 0, al[-1] if al else 0, sum(1 for a in al if 0 < a < 240), m))
    sheet.resize((sheet.width * 4, sheet.height * 4), Image.NEAREST).save(sys.argv[2])
