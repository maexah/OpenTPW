#!/usr/bin/env python3
"""Walks the track-rides module of park files. kart.py [-v] <file>..."""
import sys, struct, zlib
def payload(path):
    data = open(path, 'rb').read()
    at = data.find(b'BILZ')
    if at < 0: return None
    size, = struct.unpack_from('<I', data, at + 4)
    for skip in (0x1c, 0x18, 0x14, 0x10, 0xc):
        try:
            p = zlib.decompress(data[at + skip:])
            if len(p) == size: return p
        except Exception: pass
    return None
def chunks(p):
    start = p.find(b'SYSR') + 4
    t, own, whole = struct.unpack_from('<III', p, start)
    assert t == 1, t
    end = start + whole; at = start + own; out = []
    while at != end:
        t, own, whole = struct.unpack_from('<III', p, at)
        out.append((t, at, p[at + 12:at + whole])); at += whole
    assert p[end:end + 4] == b'KART'
    return start, end, out
if __name__ == '__main__':
    v = '-v' in sys.argv
    for path in [a for a in sys.argv[1:] if a != '-v']:
        p = payload(path)
        if p is None: print(path, 'NO PAYLOAD'); continue
        try: start, end, cs = chunks(p)
        except Exception as e: print(path, 'ERR', e); continue
        kinds = ' '.join(str(c[0]) for c in cs)
        if len(cs) > 1 or v: print(path, end - start, 'bytes:', kinds)
        if v:
            for t, at, body in cs:
                if t in (3, 6, 9, 7, 8, 4): print(' ', t, [hex(x) for x in struct.unpack_from('<%dI' % (len(body) // 4), body)])
                if t == 5:
                    print('  5 handle', hex(struct.unpack_from('<I', body)[0]))
                    w = struct.unpack_from('<43I', body, 4)
                    for i in range(0, 43, 8): print('     +%02x' % (i * 4), ' '.join('%08x' % x for x in w[i:i + 8]))
                    print('     tail', [hex(x) for x in struct.unpack_from('<5I', body, 4 + 0xac)])
