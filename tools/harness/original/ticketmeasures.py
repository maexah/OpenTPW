#!/usr/bin/env python3
"""ticketmeasures.py PID SECS - the ORIGINAL's memory: what golden ticket Locals 0 to 2 read, a line on each mGameTick that
is a multiple of 100 (GoldenTicket_Tick's test, 0x004d7b54) and on each change of the current park's six tickets:
the cumulative visitors (FUN_004c94c0, analyser +0x21c08), the guests inside (FUN_004c9130: things of type 1 on a cell of
type 0, 1, 3, 9 or 10), their mean happiness as FUN_004c7bb0 takes it (each one's float +0x19c cut to a whole number's low
byte, summed, over the count; 0.0 with none or the park shut) and mParkClosed, beside the current park record's six
tickets (progress [0x00f7c58c] +0x48, +0..+5; progress.py). docs/exe/ride-operation.md, "Golden tickets". Read-only. A
POLL for freshfs.sh / fsload.sh / loadfile.sh. Q629."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
last_tick = None; last_tickets = None; t0 = time.time()
while time.time() - t0 < secs:
    try:
        world = rd(0x0080239c); tick = rd(world + 0x1da70c); closed = rd(world + 0x1da710); cells = rd(0x008023a0)
        visitors = rd(rd(0x007cfb90 + rd(world + 0x1da720, 'H') * 0x14) + 0x21c08, 'i')
        prog = rd(0x00f7c58c); rec = rd(prog + 0x48) if prog else 0
        tickets = tuple(rd(rec + i, 'B') for i in range(6)) if rec else None
        if tick == last_tick and tickets == last_tickets: time.sleep(0.05); continue
        if tickets == last_tickets and (tick % 100 != 0): last_tick = tick; time.sleep(0.05); continue
        inside = 0; total = 0
        for thing in range(1, 600):
            base = rd(0x007cfb90 + thing * 0x14)
            if not base or rd(base + 2, 'B') != 1: continue
            x = rd(base + 5, 'B'); y = rd(base + 7, 'B')
            if rd(cells + (y * 128 + x) * 68 + 8) not in (0, 1, 3, 9, 10): continue
            inside += 1; total += int(rd(base + 0x19c, 'f')) & 0xff
        mean = 0.0 if closed or not inside else total / inside
        print(f"{time.time() - t0:7.2f} s tick {tick}: visitors {visitors} inside {inside} happiness {mean:.3f} closed {closed} tickets {list(tickets) if tickets else None}", flush=True)
        last_tick = tick; last_tickets = tickets
    except (OSError, struct.error, ValueError, OverflowError): time.sleep(0.05); continue
    time.sleep(0.05)
