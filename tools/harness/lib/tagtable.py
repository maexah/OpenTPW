"""The tag table (0x0075eca8, stride 0x20, to 0x00761c68; FUN_0050af40 finds a row by its id) read from testme.exe
on disk: id, key (+4), picture (+8, a short), string (+0xc), the bar's two buttons (+0x10, +0x14), lifetime in ms
(+0x18), text kind (+0x1c). A row is the tag a said advisor line leaves beside him: the line's response's group is
the row's id. `tagtable.py` prints the spread of pictures, buttons, lifetimes and kinds; `tagtable.py ID...` prints
those rows; `tagtable.py groups` prints, for every response group in the response table, its tag row or `none`."""
import struct, sys, collections
from responses import d, off, rows as responses
FIRST, END = 0x0075eca8, 0x00761c68
def rows():
    for va in range(FIRST, END, 0x20):
        i, key, pic, pad, string, b1, b2, life, kind = struct.unpack_from('<iihhiiiii', d, off(va))
        yield dict(id=i, key=key, picture=pic, pad=pad, string=string, button1=b1, button2=b2, life=life, kind=kind)
def find(i):
    """FUN_0050af40: none for 0x17e and 0x17f, the row at the id's own place where it holds the id, else the first holding it."""
    if i in (0x17e, 0x17f):
        return None
    R = list(rows())
    if 0 <= i < len(R) and R[i]['id'] == i:
        return R[i]
    return next((r for r in R if r['id'] == i), None)
def line(r):
    return 'id %(id)d key %(key)d picture %(picture)d string %(string)d buttons %(button1)d %(button2)d life %(life)d kind %(kind)d' % r
if __name__ == '__main__':
    R = list(rows())
    if sys.argv[1:] == ['groups']:
        for g in sorted(set(r[7] for r in responses())):
            t = find(g)
            print('group %d: %s' % (g, line(t) if t else 'none'))
    elif len(sys.argv) > 1:
        for a in sys.argv[1:]:
            t = find(int(a, 0))
            print(line(t) if t else '%s: none' % a)
    else:
        print(len(R), 'rows;', sum(1 for i, r in enumerate(R) if r['id'] == i), 'at their own place; pad not nought:', sum(1 for r in R if r['pad']))
        for f in ('picture', 'button1', 'button2', 'life', 'kind'):
            c = collections.Counter(r[f] for r in R)
            print(f + ':', ', '.join('%d x%d' % kv for kv in sorted(c.items())))
        print('key is the id:', sum(1 for r in R if r['key'] == r['id']), 'of', len(R))
