#!/usr/bin/env python3
"""wctpng.py <wad> <member> <out.png> - one .wct texture out of a wad as this game's reader decodes it (OpenTPW.Files'
TextureFile, through ../wct2raw), saved as a PNG and an 8x copy beside it, with its header (compression, alpha, version,
size, the Y, Cr (offset 10), Cb (offset 12) and A scales: docs/exe/textures.md). <member> is matched as the END of the member's path, so
'textures\\ridestatbar.wct' does not take 'stexture\\ridestatbar.wct'. Needs wadcat (CLAUDE.local.md) and Pillow.
The colours are the READER's: a type 0x12 texture's row ends are not the original's (Q354); wctorig.py decodes the original's way."""
import os, struct, subprocess, sys, tempfile
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
from PIL import Image
wad, member, out = sys.argv[1:4]
W = paths.tool("wadcat"); R = paths.tool("wct2raw") + ".dll"
env = dict(os.environ, DOTNET_ROOT=paths.dotnet_env()['DOTNET_ROOT'])
with tempfile.TemporaryDirectory() as t:
    subprocess.run([W, "--dump", member, wad], cwd=t, check=True, capture_output=True)
    got = os.listdir(t); assert len(got) == 1, f"{len(got)} members end with {member}: {got}"
    src = os.path.join(t, got[0]); head = open(src, "rb").read(16)
    kind, alpha, bits, ver, w, h, y, cr, cb, a = struct.unpack("<BBBBhhhhhh", head)
    print(f"{got[0]}: type 0x{kind:02x} alpha {alpha} bits {bits} version {ver} {w}x{h} scales Y {y} Cr(@10) {cr} Cb(@12) {cb} A {a}")
    raw = os.path.join(t, "out.rgba"); subprocess.run([paths.dotnet(), R, src, raw], check=True, env=env, capture_output=True)
    d = open(raw, "rb").read(); w, h = struct.unpack("<ii", d[:8])
    im = Image.frombytes("RGBA", (w, h), d[8:]); im.save(out); im.resize((w * 8, h * 8), Image.NEAREST).save(out[:-4] + "-8x.png")
print(out)
