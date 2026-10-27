"""Every scale track's keys across the game: first key, last key against the declared end, the pad word, order, and which other channels share the track. scalekeys.py <data dir> (Q599)"""
import os, struct, sys, collections
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
import wadcat
root = sys.argv[1]
c = collections.Counter(); first = collections.Counter(); lastvs = collections.Counter(); ones = 0
for dp, dn, fn in os.walk(root):
    for f in sorted(fn):
        if not f.lower().endswith('.wad'): continue
        try: mem = wadcat.members(open(os.path.join(dp, f), 'rb').read())
        except Exception: continue
        for n, d in mem:
            if not n.lower().endswith('.md2') or len(d) < 0x9c: continue
            b = struct.unpack_from('<I', d, 0x98)[0]
            if b < 0x9c or b + 0x30 > len(d): continue
            cnt = struct.unpack_from('<H', d, b + 0x12)[0]; t = struct.unpack_from('<I', d, b + 0x2c)[0]
            if t < 0x9c or t + 64 * cnt != b: continue
            end = struct.unpack_from('<i', d, b + 8)[0]
            for i in range(cnt):
                o = t + 64 * i; fl = struct.unpack_from('<I', d, o + 4)[0]
                if not fl & 0x80: continue
                c['tracks'] += 1
                if fl & 0x100 == 0: c['0x80 without 0x100'] += 1
                if fl & 8: c['with rotation'] += 1
                if fl & 1: c['with position'] += 1
                k = struct.unpack_from('<H', d, o + 0x12)[0]; at = struct.unpack_from('<I', d, o + 0x20)[0]
                if k == 0 or at < 0x9c or at + 16 * k > len(d): c['bad slot'] += 1; continue
                keys = [struct.unpack_from('<HH3f', d, at + 16 * j) for j in range(k)]
                if any(kk[1] for kk in keys): c['pad nonzero'] += 1
                if any(keys[j + 1][0] <= keys[j][0] for j in range(k - 1)): c['not ascending'] += 1
                if k == 1: ones += 1
                first[keys[0][0]] += 1
                lastvs['last ' + ('=' if keys[-1][0] == end else '<' if keys[-1][0] < end else '>') + ' end'] += 1
                mn = min(min(kk[2:]) for kk in keys); mx = max(max(kk[2:]) for kk in keys)
                c['min<=0'] += mn <= 0
                c['max'] = max(c['max'], mx)
print(dict(c)); print('one-key tracks', ones); print('first key frames', dict(first)); print(dict(lastvs))
