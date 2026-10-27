"""advisorrow.py MESSAGE... - an advisor message's metadata row and its lines: polled or posted, category, game mode,
score function, copies, first response and line count, then each line's response row (lib/responses.py).

    advisorrow.py 0xd7 0x137 281        ids in hex or decimal; the row is 0x0076e300 + id x 0x38

The table is zeros in the image and filled before WinMain; this reads the emulated fill, q177/skeptic-analyser/meta_full.json
(OpenTPW docs/exe/advisor-park.md, "Response metadata at 0x0076e300": +0x00 non-zero posted only, +0x0c 0 game type 0
alone, 1 game type 2, 2 either; +0x10 the score function, whose ECX is the Advisor.sam object, lib/advisorschema.py).
Importable: row(id) -> dict. Written for Q141.
"""
import json, os, sys
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import responses
META = os.path.join(paths.WORK, 'q177/skeptic-analyser/meta_full.json')
_d = json.load(open(META))
def row(i):
    b = 0x76e300 + i * 0x38; dw = lambda k: _d.get('%#x' % (b + k), 0)
    return dict(id=dw(4), posted=bool(dw(0)), category=dw(8), mode=dw(0xc), score=dw(0x10), copies=dw(0x20),
                response=dw(0x24), lines=dw(0x28), selection=dw(0x2c))
if __name__ == '__main__':
    table = {r[0]: r for r in responses.rows()}
    for s in sys.argv[1:]:
        i = int(s, 0); r = row(i)
        print('message %#x (%d): %s, category %d, mode %d, score %#x, copies %d, first response %d, %d lines, selection %d%s' % (
            i, i, 'posted only' if r['posted'] else 'polled', r['category'], r['mode'], r['score'], r['copies'], r['response'],
            r['lines'], r['selection'], '' if r['id'] == i else ' (the row id reads %d: no row)' % r['id']))
        for k in range(r['lines']):
            x = table.get(r['response'] + k)
            if x: print('   response %d sample %d lip %d gesture %d slot %d bank %d props %d %d group %d' % (x[0], x[1], x[2], x[3], x[4] & 0xffff, x[4] >> 16, x[5], x[6], x[7]))
