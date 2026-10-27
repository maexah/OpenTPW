#!/usr/bin/env python3
"""soundsweep.py [THEME] [all] - every THEME script's ADDOBJ/EVENT sound (types 3-9) over the .RSE corpus (~/.cache/tpw-rse-corpus,
rse-listing's rselisting): its effect's flags from its category's SFX map, its class, and each variation's sample lengths (ms,
the sample record's +0x08). Without `all`, only the one-shots (flags without 0x4; looped under 0x8) and ids the category lacks.
THEME defaults to jungle. Read-only. (Q565)"""
import subprocess, glob, os, re, struct, collections, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
THEME = next((a for a in sys.argv[1:] if a != 'all'), 'jungle'); ALL = 'all' in sys.argv[1:]
D = os.path.join(paths.game(), "data")
cats = {3: f'{D}/levels/{THEME}/Sound/cat_ridesSFX.map', 4: f'{D}/levels/{THEME}/Sound/cat_ambientSFX.map',
        5: f'{D}/global/sound/cat_ridesSFX.map', 6: f'{D}/global/sound/cat_kidsSFX.map', 7: f'{D}/global/sound/cat_staffSFX.map',
        8: f'{D}/global/sound/cat_ambientSFX.map', 9: f'{D}/global/sound/cat_uiSFX.map'}
def read(path):
    d = open(path, 'rb').read(); count, = struct.unpack_from('<i', d, 0x1c); at = 0x34 + count * 20; out = {}
    for i in range(count):
        r = 0x34 + i * 20; eid, nvar = struct.unpack_from('<ii', d, r); flags, = struct.unpack_from('<H', d, r + 0x10)
        heads = []
        for v in range(nvar):
            h = at + v * 42; ns, = struct.unpack_from('<H', d, h); nz, = struct.unpack_from('<I', d, h + 4); heads.append((ns, nz))
        at += nvar * 42; lens = []
        for ns, nz in heads:
            lens.append([struct.unpack_from('<I', d, at + s * 16 + 8)[0] for s in range(ns)]); at += ns * 16 + nz * 8
        out[eid] = (flags, lens)
    return out
C = {t: read(p) for t, p in cats.items()}
B = paths.tool("rselisting")
env = dict(os.environ, DOTNET_ROOT=paths.dotnet_env()['DOTNET_ROOT'])
rows = collections.Counter()
for f in sorted(glob.glob(os.path.expanduser(f'~/.cache/tpw-rse-corpus/levels__{THEME}__*/*'))):
    if not f.lower().endswith('.rse'): continue
    txt = subprocess.run([B, f], capture_output=True, text=True, env=env).stdout
    for line in txt.splitlines():
        m = re.match(r'\s*(\d+)\s+(ADDOBJ|EVENT)\s+(.*)', line)
        if not m: continue
        a = [x.strip() for x in m.group(3).split(',')]
        try: t = int(a[0]); e = int(a[2])
        except (ValueError, IndexError): continue
        if t not in C: continue
        fl, lens = C[t].get(e, (None, []))
        kind = 'ABSENT' if fl is None else ('looped' if fl & 8 else 'one-shot') if not fl & 4 else 'class'
        if kind == 'class' and not ALL: continue
        w = os.path.basename(os.path.dirname(f)).replace(f'levels__{THEME}__', '')
        print(f'{w}/{os.path.basename(f)} @{m.group(1)} {m.group(2)} {", ".join(a)}  flags 0x{(fl or 0):x} {kind} lengths ms {lens}')
