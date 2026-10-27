#!/usr/bin/env python3
"""wctorig.py <file.wct> [out.png] - a .wct of type 0x12 (zlib) decoded the way the original's FUN_00593ab0 does it, not
the way OpenTPW's TextureFile does (docs/exe/textures.md, "The .wct decode"): each channel's values (a signed byte, or
0x80 and an int16) times its multiplier (Y 1 + n/2 of offset 8, Cb 1 + n/4 of offset 12, Cr 1 + n/4 of offset 10,
A n + 1 of offset 14), each row then each column of the n x n plane (n the larger side, a power of two, at least 8;
chroma n/2) inverted with plain D4 (smooth = first half, detail = second, wrapping), BT.601 to RGB with Cb/Cr taken
nearest, rounded and clamped. Prints the header; writes an RGBA PNG and an 8x copy when given out.png. A .sgn's artwork
(by its extension) goes the same way with the four floats at its +0x0C as the multipliers. The LZSS type (0x13,
FUN_00595920) is NOT done here."""
import math, struct, sys, zlib
def plane_values(buf, pos, count, q):
    out = []
    while len(out) < count:
        b = buf[pos]
        if b == 0x80: v = struct.unpack_from("<h", buf, pos + 1)[0]; pos += 3
        else: v = b - 256 if b > 127 else b; pos += 1
        out.append(v * q)
    return out, pos
S3, R2 = math.sqrt(3), math.sqrt(2)
H0, H1, H2, H3 = (1 + S3) / (4 * R2), (3 + S3) / (4 * R2), (3 - S3) / (4 * R2), (1 - S3) / (4 * R2)
def inv(a):
    n = len(a); h = n // 2; s, d = a[:h], a[h:]; o = [0.0] * n
    for k in range(h):
        p = (k - 1) % h
        o[2 * k] = s[p] * H2 + s[k] * H0 + d[p] * H1 + d[k] * H3
        o[2 * k + 1] = s[p] * H3 + s[k] * H1 - d[p] * H0 - d[k] * H2
    return o
def plane(vals, n):
    rows = [inv(vals[r * n:(r + 1) * n]) for r in range(n)]
    cols = [inv([rows[r][c] for r in range(n)]) for c in range(n)]
    return [[cols[c][r] for c in range(n)] for r in range(n)]
def align(v):
    n = 1; v -= 1
    while v >> 1: v >>= 1; n += 1
    return 1 << max(n, 3)
def decode(data):
    kind, alpha, bits, ver, w, h, y, s10, s12, a = struct.unpack_from("<BBBBhhhhhh", data, 0)
    csize, asize = struct.unpack_from("<ii", data, 16); body = 28
    assert kind == 0x12, f"type 0x{kind:02x}: only 0x12 is done"
    w, h, px = chunks(data[body:body + csize], data[body + csize:body + csize + asize] if asize > 0 else None, w, h,
                      (1 + y * 0.5, 1 + s12 * 0.25, 1 + s10 * 0.25, a + 1.0))
    return (kind, alpha, ver, w, h, y, s10, s12, a), w, h, px
def sign(data):
    """A .sgn's artwork (sgn.md): the floats at +0x0C handed to the decoder as they are (FUN_005e7990 -> FUN_00592730),
    decoded at 256 x 256, the top 128 rows kept."""
    o = data.index(b"BILZ") - 0x24; y, cb, cr, a = struct.unpack_from("<4f", data, o + 12); c, al = struct.unpack_from("<ii", data, o + 28)
    w, h, px = chunks(data[o + 0x24:o + 0x24 + c], data[o + 0x24 + c:o + 0x24 + c + al], 256, 256, (y, cb, cr, a))
    return (y, cb, cr, a), 256, 128, px[:256 * 128]
def chunks(colour, alphachunk, w, h, q):
    unz = lambda c: zlib.decompress(c[0x1c:])
    col = unz(colour); n = max(align(w), align(h))
    yv, p = plane_values(col, 0, n * n, q[0])
    cbv, p = plane_values(col, p, (n // 2) ** 2, q[1])
    crv, p = plane_values(col, p, (n // 2) ** 2, q[2])
    Y, Cb, Cr = plane(yv, n), plane(cbv, n // 2), plane(crv, n // 2)
    A = None
    if alphachunk:
        av, _ = plane_values(unz(alphachunk), 0, n * n, q[3]); A = plane(av, n)
    cl = lambda v: max(0, min(255, int(math.floor(v + 0.5))))
    px = []
    for r in range(h):
        for c in range(w):
            yy, cb, cr = Y[r][c], Cb[r // 2][c // 2], Cr[r // 2][c // 2]
            px.append((cl(yy + 1.402 * cr), cl(yy - 0.344136 * cb - 0.714136 * cr), cl(yy + 1.772 * cb), cl(A[r][c]) if A else 255))
    return w, h, px
if __name__ == "__main__":
    raw = open(sys.argv[1], "rb").read()
    if sys.argv[1].lower().endswith(".sgn"): head, w, h, px = sign(raw); print("sign multipliers Y %g Cb %g Cr %g A %g" % head)
    else: head, w, h, px = decode(raw); print("type 0x%02x alpha %d version %d %dx%d scales Y %d @10 %d @12 %d A %d" % head)
    if len(sys.argv) > 2:
        from PIL import Image
        im = Image.new("RGBA", (w, h)); im.putdata(px); im.save(sys.argv[2]); im.resize((w * 8, h * 8), Image.NEAREST).save(sys.argv[2][:-4] + "-8x.png")
