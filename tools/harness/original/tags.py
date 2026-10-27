#!/usr/bin/env python3
"""tags.py PID SECS - the message tags on the gadget's aerial in the running ORIGINAL, read-only, a line a change with
the wall clock and mGameTick (a POLL for loadfile.sh).

The ten entries are records of 0x208 bytes at 0x007c26b0 (FUN_0048eaa0): the tag's control (+0), in use (+4, a short),
its text (+6, UTF-16). [0x007ca09c] is how many are held, [0x007cb2c4] the aerial's mast (control 0x2d), [0x007ca090]
the tag whose message is on the bar. A control's rectangle is four shorts at +0xc (left, top, right, bottom), on the
2048 x 1536 layout. Says when a said advisor line leaves a tag, where it stands, how far it lifts the mast's top, and
when it goes (docs/exe/advisor-park.md, "The tag a line leaves on the aerial")."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))
def rect(c): return rd(c + 0xc, '4h') if c else None
last = None; t0 = time.time()
while time.time() - t0 < secs:
    try:
        g = rd(0x0080239c)[0]; tick = rd(g + 0x1da70c)[0]
        now = {'held': rd(0x007ca09c)[0], 'mast': rect(rd(0x007cb2c4)[0]), 'on the bar': hex(rd(0x007ca090)[0])}
        tags = []
        for i in range(10):
            e = 0x007c26b0 + i * 0x208; c, used = rd(e, 'Ih')
            if used:
                f.seek(e + 6); text = f.read(0x200).decode('utf-16-le', 'replace').split('\0')[0]
                tags.append((i, rect(c), text))
        now['tags'] = tags
    except (OSError, struct.error): time.sleep(0.05); continue
    if now != last: print(f'{time.time() - t0:7.2f} s tick {tick}: tags {now}', flush=True); last = now
    time.sleep(0.02)
