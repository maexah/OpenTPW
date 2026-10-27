#!/usr/bin/env python3
"""wctcensus.py <dir> : every .wct under dir counted by type byte, colour codec (BILZ or other), alpha byte, bit count,
alpha chunk (BILZ, other or none) and version, with an example path of each (Q355; FileFormats texture.md)."""
import os, struct, sys, collections
c = collections.Counter(); ex = {}
for root, _, fs in os.walk(sys.argv[1]):
    for f in fs:
        if not f.lower().endswith('.wct'): continue
        b = open(os.path.join(root, f), 'rb').read()
        if len(b) < 28: c[('short',)] += 1; continue
        kind, alpha, bits, ver, w, h = struct.unpack_from('<BBBBhh', b, 0); cs, as_ = struct.unpack_from('<ii', b, 16)
        mag = 'BILZ' if b[28:32] == b'BILZ' else 'other'
        amag = '-' if as_ <= 0 else ('BILZ' if b[28+cs:32+cs] == b'BILZ' else 'other')
        k = ('0x%02x' % kind, mag, 'alpha%d' % alpha, 'bits%d' % bits, amag, 'v%d' % ver); c[k] += 1; ex.setdefault(k, os.path.join(root, f))
for k, n in sorted(c.items()): print(n, k, ex.get(k, ''))
