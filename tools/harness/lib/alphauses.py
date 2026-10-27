#!/usr/bin/env python3
"""alphauses.py <materials.txt> [<wct dump dir> <wads.txt>] : every model material naming a .wct whose alpha chunk sits
under a header byte 1 of nought (the original draws it cut out, exe/textures.md "The loader"), split by the material's
0x2 bit, the one this game's world shader lets alpha through on (q358).

materials.txt: `wct2raw <model.md2>` run over each model of a dump, each block headed `== <path>` (q358/materials.txt:
751 models of the jungle's wads, lobby.wad and ui.wad; run it with `while IFS= read -r f` or a backslash in a lobby
name is eaten). The dump dir and wads.txt default to q355's (one folder a wad, numbered by wads.txt's lines); only wads
whose path holds /jungle/, or that are lobby.wad or ui.wad, are taken."""
import os, re, sys, collections
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
H = paths.WORK
MAT = sys.argv[1]; A = sys.argv[2] if len(sys.argv) > 2 else H + '/q355/alldump'
wads = [l.strip() for l in open(sys.argv[3] if len(sys.argv) > 3 else H + '/q355/wads.txt')]
tex = collections.defaultdict(list)
for i, w in enumerate(wads, 1):
    if not ('/jungle/' in w or w.endswith('/lobby.wad') or w.endswith('/ui.wad')): continue
    d = '%s/%03d' % (A, i)
    if not os.path.isdir(d): continue
    for f in os.listdir(d):
        if not f.lower().endswith('.wct'): continue
        b = open(os.path.join(d, f), 'rb').read(3)
        tex[f.replace('\\', '/').split('/')[-1].lower()[:-4]].append((b[1], b[2]))
dropped = {k for k, v in tex.items() if any(a == 0 and bits == 32 for a, bits in v)}
print('names with an alpha chunk under byte 1 nought in these wads:', len(dropped))
uses = collections.defaultdict(set); cur = None
for l in open(MAT, errors='replace'):
    if l.startswith('=='): cur = l[3:].strip(); continue
    if ': ' not in l: continue
    mesh, rest = l.split(': ', 1)
    for m in re.finditer(r'\[\d+\] (\S+) 0x([0-9a-f]+)', rest):
        t = m.group(1).lower(); t = t[:-4] if t.endswith('.wct') else t
        if t in dropped: uses[(t, int(m.group(2), 16) & 2 != 0)].add('/'.join(cur.split('/')[1:3]) + ':' + mesh.strip())
for flag in (True, False):
    print('\n== material 0x2 %s (this game %s)' % ('SET' if flag else 'clear', 'draws the alpha' if flag else 'ignores the alpha'))
    for (t, f), s in sorted(uses.items()):
        if f == flag: print('%-14s %3d uses  %s' % (t, len(s), ' '.join(sorted(s))[:200]))
print('\nnever named by a material:', ' '.join(sorted(k for k in dropped if (k, True) not in uses and (k, False) not in uses)))
