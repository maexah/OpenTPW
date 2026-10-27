#!/usr/bin/env python3
"""advisor.py PID SECS MESSAGE[,MESSAGE...] [THING] - the park advisor of the running ORIGINAL, read-only, a line a
change with mGameTick (a POLL for loadfile.sh).

  advisor.py %PID% 900 0x42 13     message 0x42's own record, the eight slots, the last action, and thing 13's State of
                                   repair beside them (an object's +0x44)

The advisor thing is the id at [[0x0080239c] + 0x1da71c] (FUN_005194d0) through [0x007cfb90 + id * 0x14]. A message's
record is advisor + id * 0x10 + 0xe0: the tick last said, the line (-1 before any), said, slaps. A slot is advisor +
0x14 + n * 0x18: subject thing (u16), a word, the line asked for, the message, the score, occupied (+0x14). The last
action's start and duration (ms) are +0xd8 and +0xdc (docs/exe/advisor-park.md).

A last argument `poll` ends each line with ` poll=N`, the poll's cursor [0x00fb3540]: the ROW of the metadata table
the next tick starts from (FUN_0059c680). It moves every tick, so it is printed and not counted as a change."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]); msgs = [int(m, 0) for m in sys.argv[3].split(',')]
POLL = sys.argv[-1] == 'poll'
if POLL: sys.argv.pop()
thing = int(sys.argv[4]) if len(sys.argv) > 4 else None
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
last = None; t0 = time.time()
while time.time() - t0 < secs:
    try:
        g = rd(0x0080239c); tick = rd(g + 0x1da70c); aid = rd(g + 0x1da71c, 'H'); adv = rd(0x007cfb90 + aid * 0x14)
        now = {'advisor': aid}
        for m in msgs:
            r = adv + m * 0x10 + 0xe0
            now[hex(m)] = (rd(r), rd(r + 4, 'i'), rd(r + 8, 'B'), rd(r + 0xc))
        slots = []
        for n in range(8):
            s = adv + 0x14 + n * 0x18
            if rd(s + 0x14, 'B'): slots.append((n, rd(s, 'H'), rd(s + 4), rd(s + 8, 'i'), hex(rd(s + 0xc)), rd(s + 0x10, 'i')))
        now['slots'] = slots; now['action'] = (rd(adv + 0xd8), rd(adv + 0xdc))
        cur = rd(0x00fb3540) if POLL else None
        if thing is not None:
            e = rd(0x007cfb90 + thing * 0x14); now['repair'] = round(rd(e + 0x44, 'f'), 3) if e else None
    except (OSError, struct.error): time.sleep(0.05); continue
    if now != last: print(f'{time.time() - t0:7.2f} s tick {tick}: {now}' + (f' poll={cur}' if POLL else ''), flush=True); last = now
    time.sleep(0.02)
