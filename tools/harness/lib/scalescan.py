"""Every clip carrying a scale track (flags 0x80|0x100), by wad: scalescan.py <data dir> [subdir...] (Q597)"""
import os, struct, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
import wadcat
root = sys.argv[1]; subs = sys.argv[2:] or ['']
tot = 0; clips = 0
for sub in subs:
    for dp, dn, fn in os.walk(os.path.join(root, sub)):
        for f in sorted(fn):
            if not f.lower().endswith('.wad'): continue
            p = os.path.join(dp, f)
            try: mem = wadcat.members(open(p, 'rb').read())
            except Exception as e: continue
            hits = []
            for n, d in mem:
                if not n.lower().endswith('.md2') or len(d) < 0x9c: continue
                b = struct.unpack_from('<I', d, 0x98)[0]
                if b < 0x9c or b + 0x30 > len(d): continue
                cnt = struct.unpack_from('<H', d, b + 0x12)[0]; t = struct.unpack_from('<I', d, b + 0x2c)[0]
                if t < 0x9c or t + 64 * cnt != b: continue
                k = sum(1 for i in range(cnt) if struct.unpack_from('<I', d, t + 64 * i + 4)[0] & 0x80)
                if k: hits.append('%s(%d)' % (n.split('\\')[-1], k)); tot += k; clips += 1
            if hits: print(os.path.relpath(p, root), len(hits), ' '.join(hits[:12]) + (' ...' if len(hits) > 12 else ''))
print('total scale tracks', tot, 'in clips', clips)
