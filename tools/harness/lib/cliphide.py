#!/usr/bin/env python3
"""cliphide.py <data dir> [path filter] - per model, the MESH nodes (index < mesh count) a clip's visibility track leaves hidden at the clip's end, and
which of the model's clips name each in their hide list: what a change of clip shows again and hides (Q622)."""
import os, sys, struct, collections
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
import wadcat, md2surf
root = sys.argv[1]; filt = sys.argv[2] if len(sys.argv) > 2 else ''
def clipinfo(d):
    b = struct.unpack_from('<I', d, 0x98)[0]
    n = struct.unpack_from('<H', d, b + 0x12)[0]; t = struct.unpack_from('<I', d, b + 0x2c)[0]
    hn = struct.unpack_from('<H', d, b + 0x1a)[0]; hp = struct.unpack_from('<I', d, b + 0x38)[0]
    hl = [struct.unpack_from('<H', d, hp + 2 * k)[0] for k in range(hn)]
    ends = {}
    for i in range(n):
        o = t + 64 * i; fl = struct.unpack_from('<I', d, o + 4)[0]; tg = struct.unpack_from('<H', d, o + 0x14)[0]
        if fl & 0x20000:
            c = struct.unpack_from('<H', d, o + 0x16)[0]; at = struct.unpack_from('<I', d, o + 0x30)[0]
            es = [struct.unpack_from('<h', d, at + 2 * k)[0] for k in range(c)]
            if es: ends[tg] = max(es, key=abs)
    return hl, ends
total = 0
for dp, dn, fn in os.walk(root):
    for f in sorted(fn):
        if not f.lower().endswith('.wad'): continue
        p = os.path.join(dp, f)
        if filt not in p: continue
        try: M = dict((n.split('\\')[-1].lower(), b) for n, b in wadcat.members(open(p, 'rb').read()))
        except Exception: continue
        stems = [m[:-4] for m in M if m.endswith('.md2')]
        for s in stems:
            clips = {m: M[m] for m in M if m.endswith('.md2') and m[:-4] != s and m[:-4].startswith(s)
                     and not any(o != s and len(o) > len(s) and m[:-4].startswith(o) and o + '.md2' in M and len(o) < len(m) - 4 for o in stems)}
            if not clips: continue
            try: meshes = md2surf.Model(M[s + '.md2']).mesh_count
            except Exception: continue
            info = {}
            for m, d in clips.items():
                try: info[m] = clipinfo(d)
                except Exception: pass
            for m, (hl, ends) in sorted(info.items()):
                hid = [tg for tg, e in ends.items() if e < 1 and tg < meshes]
                if hid:
                    total += 1
                    named = {tg: [c for c, (h2, _) in info.items() if tg in h2] for tg in hid}
                    print(os.path.relpath(p, root), s, 'meshes', meshes, m, 'leaves hidden', hid, 'in hide lists of', {k: len(v) for k, v in named.items()}, 'of', len(info))
print('clips leaving a mesh hidden:', total)
