#!/usr/bin/env python3
"""Every procedural ground tile of a park, made by the ORIGINAL's own FUN_0055f780 under unicorn (lndtile.py), in one
file (Q488). Run with $OPENTPW_EMU_PYTHON.

    lndsheet.py <base.lnd> <N> <out.rgb>     cells y 0..84 outer, x 0..95 inner; each tile N x N x 3 (R, G, B), row 0
                                             first; prints the SHA-256 of the whole (OpenTPW's ProceduralGroundTests hold
                                             ProceduralGround to these)
    lndsheet.py <base.lnd> <N> <out.rgb> --paths CELLS RGBADIR
                                             the same with a path laid over each cell CELLS names (Q489): its lines are
                                             'X Y NAME WORD' (NAME a .wct stem, WORD the cell's word, 0x80 | turn), and
                                             RGBADIR holds NAME.rgba as wct2raw writes it; each texture handed to the
                                             function as the original's surface holds it (rows bottom up, each channel's
                                             top four bits, A4R4G4B4: park-engine.md, "A path laid over")

Read back: numpy.fromfile(out, uint8).reshape(85, 96, N, N, 3)[y, x]."""
import sys, os, hashlib, struct
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, lndtile

def surface(path):
    """A wct2raw .rgba file as the original's 16-bit surface holds it: (texels, width, height, 2)."""
    b = open(path, 'rb').read(); w, h = struct.unpack_from('<II', b)
    a = np.flipud(np.frombuffer(b[8:], np.uint8).reshape(h, w, 4)).astype(np.uint16) >> 4
    return ((a[:, :, 3] << 12) | (a[:, :, 0] << 8) | (a[:, :, 1] << 4) | a[:, :, 2]).astype('<u2').tobytes(), w, h, 2

if __name__ == '__main__':
    n = int(sys.argv[2]); t = lndtile.Tiler(sys.argv[1], n); out = bytearray(); paths = {}
    if '--paths' in sys.argv:
        i = sys.argv.index('--paths'); cells, rgba = sys.argv[i + 1], sys.argv[i + 2]; tex = {}
        for line in open(cells):
            if not line.strip(): continue
            x, y, name, word = line.split()
            if name not in tex: tex[name] = surface(os.path.join(rgba, name + '.rgba'))
            paths[(int(x), int(y))] = (*tex[name], int(word, 0))
    for y in range(85):
        for x in range(96):
            out += np.ascontiguousarray(t.rgb(x, y, paths.get((x, y)))).tobytes()
    open(sys.argv[3], 'wb').write(out); print(sys.argv[1], n, len(paths), 'paths laid over', hashlib.sha256(out).hexdigest())
