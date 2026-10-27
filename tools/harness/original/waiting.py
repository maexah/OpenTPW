#!/usr/bin/env python3
"""waiting.py PID SECS - the original's memory: the park analyser's count of guests waiting for the park to open
(its +0x1f0d4, written by FUN_004c91c0 each time the advisor's row 1 is scored in a shut park with a ride), beside
the same count taken live here (things of type 1, mState 3, on TicketBoothA's or B's cell), mParkClosed, and
mGameTick; a line a change. A POLL for loadfile.sh."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
last = None; t0 = time.time()
while time.time() - t0 < secs:
    try:
        world = rd(0x0080239c); tick = rd(world + 0x1da70c); closed = rd(world + 0x1da710)
        booths = {(rd(0x0078559c), rd(0x007855a0)), (rd(0x007855a4), rd(0x007855a8))}
        analyser = rd(0x007cfb90 + rd(world + 0x1da720, 'H') * 0x14); kept = rd(analyser + 0x1f0d4)
        live = []; states = {}
        for thing in range(1, 400):
            base = rd(0x007cfb90 + thing * 0x14)
            if not base or rd(base + 2, 'B') != 1: continue
            state = rd(base + 0x220); cell = (rd(base + 5, 'B'), rd(base + 7, 'B')); states[state] = states.get(state, 0) + 1
            if state == 3 and cell in booths: live.append(thing)
    except (OSError, struct.error): time.sleep(0.05); continue
    now = (closed, kept, tuple(live))
    if now != last:
        print(f'{time.time():.2f} tick {tick} waiting: closed {closed} analyser {kept} live {len(live)} {live} booths {sorted(booths)} states {dict(sorted(states.items()))}', flush=True); last = now
    time.sleep(0.05)
