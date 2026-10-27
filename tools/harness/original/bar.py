#!/usr/bin/env python3
"""bar.py PID SECS - the message bar and the gadget's arm in the running ORIGINAL, read-only, a line a change with the
wall clock and mGameTick (a POLL for loadfile.sh).

[0x007ca098] is the bar's tree (stream 0x007501a0), [0x007ca090] the tag whose message is on show, [0x007ca094] the
tag being put on show, [0x007ca09c] how many tags are held, [0x007cb2b8] the gadget's body, [0x007cb2d0] the arm's
handle. A control: rectangle four shorts at +0xc (2048 x 1536 layout), flags +0x48 (bit 0 shown), next sibling +0x54,
first child +0x5c, parent +0x60, sixteen data words at +0xa0, depth +0xe0 (a short), a state byte at +0x134 (the
arm's: 0 at rest, 2 going out, 3 or 4 coming in; a tag's: its timers running) and +0x135 (a stream playing).
The bar's data: 1 the text, 2 the thing, 3 and 4 the two buttons' types, 5 the item. Prints the bar with its
children, every child of the body two deep (the arm is the one whose right edge moves), and each tag's rectangle
and timer byte (docs/exe/hud.md, "The message bar")."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))
def ctl(c): return (rd(c + 0xc, '4h'), rd(c + 0x48)[0] & 1, rd(c + 0x134, 'B')[0])
def kids(c):
    out = []; k = rd(c + 0x5c)[0]; n = 0
    while k and n < 40: out.append(k); k = rd(k + 0x54)[0]; n += 1
    return out
def text(p):
    if not p: return None
    f.seek(p); return f.read(0x200).decode('utf-16-le', 'replace').split('\0')[0]
last = None; t0 = time.time()
while time.time() - t0 < secs:
    try:
        g = rd(0x0080239c)[0]; tick = rd(g + 0x1da70c)[0]
        bar = rd(0x007ca098)[0]; body = rd(0x007cb2b8)[0]; handle = rd(0x007cb2d0)[0]
        now = {'on show': hex(rd(0x007ca090)[0]), 'pending': hex(rd(0x007ca094)[0]), 'held': rd(0x007ca09c)[0]}
        if bar:
            d = rd(bar + 0xa0, '6I')
            now['bar'] = (ctl(bar), 'parent', hex(rd(bar + 0x60)[0]), 'thing', d[2], 'buttons', d[3], d[4], 'item', d[5], text(d[1]))
            now['bar kids'] = [ctl(k)[:2] for k in kids(bar)]
        if body:
            now['body'] = ctl(body)
            now['body kids'] = [(hex(k), ctl(k), [(hex(j), ctl(j)) for j in kids(k)][:4]) for k in kids(body)][:8]
        if handle: now['handle'] = ctl(handle)[0]
        tags = []
        for i in range(10):
            c, used = rd(0x007c26b0 + i * 0x208, 'Ih')
            if used: tags.append((hex(c), ctl(c), 'playing', rd(c + 0x135, 'B')[0]))
        now['tags'] = tags
    except (OSError, struct.error): time.sleep(0.05); continue
    if now != last:
        print(f'{time.time() - t0:7.2f} s tick {tick}:', flush=True)
        for k, v in now.items():
            if last is None or last.get(k) != v: print(f'    {k}: {v}', flush=True)
        last = now
    time.sleep(0.02)
