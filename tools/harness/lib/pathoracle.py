"""pathoracle.py N - (usage: $OPENTPW_EMU_PYTHON pathoracle.py 16|32) the expected values of OpenTPW's path-laid-over tests (Q489), made by the ORIGINAL's FUN_0055f780 under
unicorn (lib/lndtile.py) with each path texture as the original's surface holds it (measured, PREDICTION.txt a3'): our
TextureFile decode (wct2raw), rows bottom up, each channel & 0xf0, as A4R4G4B4. Q489 made it for 16 (q489/oracle.py); Q618 took the tile size N as its argument (32 for OpenTPW's default). Needs the q483 lnd and q489 path-texture folders."""
import sys, hashlib, struct, numpy as np
import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
import lndtile
N = int(sys.argv[1])
LND = os.path.join(paths.WORK, 'q483/lnd/jungle/base.lnd')
TCT = {2: 'jpa_str1', 3: 'jpa_cnr2', 4: 'jpa_tju1', 5: 'jpa_xrd1', 6: 'jpa_tju2', 7: 'jpa_tju3', 9: 'jpa_cnr1', 10: 'jpa_edg1', 15: 'jpa_tju4', 19: 'jpa_str2', 20: 'jpa_edg2'}
TURN = {0: 0, 90: 0x20, 180: 0x10, 270: 0x08}
def texels(name):
    b = open(os.path.join(paths.WORK, 'q489/wct/pathtex\\%s.rgba' % name), 'rb').read(); w, h = struct.unpack_from('<II', b)
    a = np.flipud(np.frombuffer(b[8:], np.uint8).reshape(h, w, 4)).astype(np.uint16) >> 4
    return ((a[:, :, 3] << 12) | (a[:, :, 0] << 8) | (a[:, :, 1] << 4) | a[:, :, 2]).astype('<u2').tobytes(), w, h
t = lndtile.Tiler(LND, N)
def rgb(x, y, rec=None): return np.frombuffer(t.tile(x, y, rec), np.uint8).reshape(N, N, 4)[:, :, 1:].tobytes()
cells = [tuple(map(int, l.split())) for l in open(os.path.join(paths.WORK, 'q489/easy-paths.txt')).read().split('\n')[2:] if l.strip()]
assert len(cells) == 78
cells.sort(key=lambda c: (c[1], c[0]))
cache = {k: texels(v) for k, v in TCT.items()}
allp = b''.join(rgb(x, y, (*cache[ti], 2, 0x80 | TURN[ang])) for x, y, ti, ang in cells)
print('all 78 path tiles:', hashlib.sha256(allp).hexdigest())
print('no path laid over the same 78:', hashlib.sha256(b''.join(rgb(x, y) for x, y, _, _ in cells)).hexdigest())
tex = (*cache[4], 2)
for word in [0x80, 0x88, 0x90, 0xa0, 0xc0, 0xc8, 0xd0, 0xe0, 0x98]:
    print('cell (10,10) jpa_tju1 word %#x: %s' % (word, hashlib.sha256(rgb(10, 10, (*tex, word))).hexdigest()))
print('grey cell (28,0) with jpa_tju1:', hashlib.sha256(rgb(28, 0, (*tex, 0x80))).hexdigest(), 'plain', hashlib.sha256(rgb(28, 0)).hexdigest())
for word in [0xb0, 0xf0]:
    print('cell (10,10) jpa_tju1 word %#x: %s' % (word, hashlib.sha256(rgb(10, 10, (*tex, word))).hexdigest()))
