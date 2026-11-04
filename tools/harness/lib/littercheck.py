"""littercheck.py <park file>... (Q899): every littered cell of each file against FUN_004d93b0's decode (docs/exe/ride-operation.md,
"A cell's litter"). For each cell with mLitter (the cell record's +29) set: its two handles (+35 the sprite, +39 a stink bomb's
particle), and the saved sprite at the first: its kind (11, "litter"), bank, program word (inside the record of the cell's kind,
0x0074f050 + 0x28 x (kind - 1), as a word of the table at 0x0074dab8; the original's rest on the record's word 7), flags (0x80000)
and place (+0x88, +0x90), against (10 x + 1 + r1 % 9, 10 y + 1 + r2 % 9), r1 and r2 the park generator's two draws after a reseed
with the cell id. A line a cell, OK or DIFF, and a TOTAL of what matched. A file this game writes can be checked the same way."""
import os, sys, struct
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import parkfile
def draw(s):
    s = (s * 0x19660D + 0x3C6EF35F) & 0xffffffff
    s = ((s >> 13) | (s << 19)) & 0xffffffff
    return s, ((-s) & 0xffffffff if s & 0x80000000 else s)
SLOT = {1: 0, 2: 1, 3: 2, 4: 3, 5: 4, 6: 5, 7: 6, 8: 7}
tot = dict(cells=0, kind11=0, word=0, place=0, flags={}, kinds={}, part={})
for path in sys.argv[1:]:
    try: p = parkfile.Park(path)
    except Exception as e: print('SKIP', path, e); continue
    rows = []
    for i in range(16384):
        at = p.cell[i] + 1
        kind, = struct.unpack_from('<i', p.b, at + 29)
        if not kind: continue
        h1, h2 = struct.unpack_from('<ii', p.b, at + 35)
        x, y = i % 128, i // 128; cid = i + 1
        s, r1 = draw(cid); s, r2 = draw(s)
        want = (10 * x + 1 + r1 % 9, 10 * y + 1 + r2 % 9)
        want_word = (0x0074f050 + 0x28 * SLOT.get(kind, 5) - 0x0074dab8) // 4
        hd = parkfile.head(p, h1)
        tot['cells'] += 1; tot['kinds'][kind] = tot['kinds'].get(kind, 0) + 1; tot['part'][h2] = tot['part'].get(h2, 0) + 1
        if hd:
            ok_k = hd['kind'] == 11; ok_p = abs(hd['place'][0] - want[0]) < 1e-3 and abs(hd['place'][2] - want[1]) < 1e-3
            tot['kind11'] += ok_k; tot['place'] += ok_p; tot['flags'][hd['flags']] = tot['flags'].get(hd['flags'], 0) + 1
            rows.append('  (%d,%d) kind %d h %d/%d sprite kind %d bank %d word %d (decode %d..%d) flags %#x place (%.3f, %.3f, %.3f) want (%d, 0, %d) %s'
                        % (x, y, kind, h1, h2, hd['kind'], hd['bank'], hd['word'], want_word, want_word + 9, hd['flags'], *hd['place'], *want, 'OK' if ok_k and ok_p else 'DIFF'))
            tot['word'] += want_word <= hd['word'] <= want_word + 9
        else:
            rows.append('  (%d,%d) kind %d h %d/%d NO SPRITE at handle' % (x, y, kind, h1, h2))
    if rows: print(path); print('\n'.join(rows))
print('TOTAL', tot)
