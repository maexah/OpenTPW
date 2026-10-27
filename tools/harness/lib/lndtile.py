#!/usr/bin/env python3
"""The original's procedural ground tile, made by the ORIGINAL's own code under unicorn (Q483).

    $OPENTPW_EMU_PYTHON lndtile.py <base.lnd> <N> <x> <y> [out.png]   one tile, N = 64, 32 or 16
    $OPENTPW_EMU_PYTHON lndtile.py <base.lnd> <N> sheet X0 Y0 X1 Y1 out.png   a box of tiles side by side

As a module: `t = Tiler( path, N )`, `t.tile( x, y )` -> N*N*4 bytes as the compositor leaves them (byte 0 nought,
1..3 = R, G, B: FUN_00572440 reverses each pixel's four bytes into the locked surface, whose X8R8G8B8 memory order is
B, G, R, X). `t.rgb( x, y )` -> an N x N x 3 numpy array (R, G, B). x, y are box-relative cells (the box
starts at MapInfo's XStart/YStart plus ThemeEngine.ProceduralTexturingX/YOffset, 0 in every stock park).

What it runs, unchanged, from testme.exe (the 2.0 build): FUN_0055e710 (the five 256-byte ramps from the file's
bytes 1..10, at 0x008795a8, 0x008796a8, 0x008793a8, 0x008797a8, 0x008794a8), then FUN_0055f780 (thiscall, ECX the lnd
object; dest, x, y, record), with its three upsample buffers (0x008798a8, 0x008798b8, 0x00879398) and the scratch tile
(0x008798c8) laid out as FUN_005e7840 and FUN_0055f720 leave them. The lnd object is built here from the file the way
FUN_00560ff0 + FUN_0055f470 fill it: +0 N, +1 the set (0 for 64, 1 for 32, 2 for 16), +2..+0xb the file's bytes 1..10,
+0xc/+0x10 (w-1)>>2 and (h-1)>>2, +0x14 the map {w, h, depth, data}, +0x24 + 0xd8*k set k {size, level, 13 textures of
{w, h, depth, data} at +8 + 16*slot}, the file's thirteen in slot order 0,1,3,4,8,9,2,5,10,6,7,11,12.

The path laid over (Q489): `t.tile( x, y, rec )` with rec = (texels, w, h, depth, word) hands the function the record
FUN_0056f090 builds for a cell whose word has 0x80 ({texel pointer, width, height, bytes a texel, word}; the texels as
the locked surface holds them: depth 2 A4R4G4B4, depth 4 A8R8G8B8, little-endian), so its last pass lays them over the
tile by their alpha, turned by the word. Without rec the record's texture is 0 and the scratch is copied out.
docs/exe/park-engine.md, "The procedural ground".
"""
import struct, sys, os
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
import numpy as np
import pefile
from unicorn import Uc, UC_ARCH_X86, UC_MODE_32
from unicorn.x86_const import *

EXE = paths.exe()
SLOT = [0, 1, 3, 4, 8, 9, 2, 5, 10, 6, 7, 11, 12]
RAMPS = [(0x008795a8, 1), (0x008796a8, 3), (0x008793a8, 5), (0x008797a8, 7), (0x008794a8, 9)]

def read_lnd(path):
    b = open(path, 'rb').read(); o = 11
    def dims():
        nonlocal o
        a, c, d = struct.unpack_from('<III', b, o); o += 12; return a, c, d
    w, h, d = dims(); m = (w, h, d, b[o:o + w*h*d]); o += w*h*d
    sets = []
    for k in range(3):
        size, lvl = struct.unpack_from('<IB', b, o); o += 5
        texs = []
        for i in range(13):
            a, c, dd = dims(); texs.append((a, c, dd, b[o:o + a*c*dd])); o += a*c*dd
        sets.append((size, lvl, texs))
    assert o == len(b), (o, len(b))
    return b[0], b[1:11], m, sets

class Tiler:
    def __init__(self, path, n=64):
        self.n = n
        ver, ramps, m, sets = read_lnd(path)
        assert ver == 3
        pe = pefile.PE(EXE, fast_load=True); ib = pe.OPTIONAL_HEADER.ImageBase
        mu = self.mu = Uc(UC_ARCH_X86, UC_MODE_32)
        hi = max(ib + s.VirtualAddress + max(s.Misc_VirtualSize, s.SizeOfRawData) for s in pe.sections)
        mu.mem_map(ib, ((hi + 0xfff) & ~0xfff) - ib)
        mu.mem_write(ib, pe.header[:0x400])
        for s in pe.sections:
            mu.mem_write(ib + s.VirtualAddress, s.get_data())
        self.STACK = 0x30000000; mu.mem_map(self.STACK, 0x200000)
        self.SENT = 0x50000000; mu.mem_map(self.SENT, 0x1000); mu.mem_write(self.SENT, b'\xf4')
        heap = 0x60000000; mu.mem_map(heap, 0x4000000); self.hp = heap
        mu.reg_write(UC_X86_REG_FPCW, 0x027F)
        for addr, i in RAMPS:
            self.call(0x0055e710, addr, ramps[i - 1], ramps[i])
        k = {64: 0, 32: 1, 16: 2}[n]
        obj = self.alloc(0x2ac)
        hdr = bytes([n, k]) + bytes(ramps) + struct.pack('<II', (m[0] - 1) >> 2, (m[1] - 1) >> 2)
        mu.mem_write(obj, hdr)
        mu.mem_write(obj + 0x14, struct.pack('<IIII', m[0], m[1], m[2], self.blob(m[3])))
        for kk, (size, lvl, texs) in enumerate(sets):
            base = obj + 0x24 + 0xd8 * kk
            mu.mem_write(base, struct.pack('<IB', size, lvl))
            for i, (a, c, dd, data) in enumerate(texs):
                mu.mem_write(base + 8 + 16 * SLOT[i], struct.pack('<IIII', a, c, dd, self.blob(data)))
        self.obj = obj
        for o in (0x008798a8, 0x008798b8, 0x00879398):
            mu.mem_write(o, struct.pack('<IIII', n, n, 1, self.alloc(n * n)))
        mu.mem_write(0x008798c8, struct.pack('<I', self.alloc(n * n * 4)))
        self.dest = self.alloc(n * n * 4); self.rec = self.alloc(0x20); self._texels = {}
        mu.mem_write(self.rec, b'\0' * 0x20)

    def alloc(self, size):
        a = self.hp; self.hp += (size + 0xff) & ~0xff; return a

    def blob(self, data):
        a = self.alloc(len(data) + 64); self.mu.mem_write(a, bytes(data)); return a

    def call(self, fn, *args, ecx=0):
        sp = self.STACK + 0x100000
        for a in reversed(args):
            sp -= 4; self.mu.mem_write(sp, struct.pack('<I', a & 0xffffffff))
        sp -= 4; self.mu.mem_write(sp, struct.pack('<I', self.SENT))
        self.mu.reg_write(UC_X86_REG_ESP, sp); self.mu.reg_write(UC_X86_REG_ECX, ecx)
        self.mu.emu_start(fn, self.SENT)

    def tile(self, x, y, rec=None):
        self.mu.mem_write(self.dest, b'\xcd' * (self.n * self.n * 4))
        if rec is None:
            self.mu.mem_write(self.rec, b'\0' * 0x20)
        else:
            texels, w, h, depth, word = rec
            key = bytes(texels)
            ptr = self._texels.get(key)
            if ptr is None:
                ptr = self._texels[key] = self.blob(key)
            self.mu.mem_write(self.rec, struct.pack('<IIIII', ptr, w, h, depth, word))
        self.call(0x0055f780, self.dest, x, y, self.rec, ecx=self.obj)
        return bytes(self.mu.mem_read(self.dest, self.n * self.n * 4))

    def rgb(self, x, y, rec=None):
        a = np.frombuffer(self.tile(x, y, rec), np.uint8).reshape(self.n, self.n, 4)
        return a[:, :, [1, 2, 3]]        # reversed into the surface as B, G, R, X: byte 1 is R

    def ramp(self, i):
        return bytes(self.mu.mem_read(RAMPS[i][0], 256))

if __name__ == '__main__':
    from PIL import Image
    p, n = sys.argv[1], int(sys.argv[2]); t = Tiler(p, n)
    if sys.argv[3] == 'sheet':
        x0, y0, x1, y1 = map(int, sys.argv[4:8])
        img = np.zeros(((y1 - y0 + 1) * n, (x1 - x0 + 1) * n, 3), np.uint8)
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                img[(y - y0) * n:(y - y0 + 1) * n, (x - x0) * n:(x - x0 + 1) * n] = t.rgb(x, y)
        Image.fromarray(img).save(sys.argv[8]); print('wrote', sys.argv[8], img.shape)
    else:
        x, y = int(sys.argv[3]), int(sys.argv[4]); a = t.rgb(x, y)
        print('tile', x, y, 'mean RGB', a.reshape(-1, 3).mean(0).round(1))
        if len(sys.argv) > 5: Image.fromarray(a).save(sys.argv[5])
