#!/usr/bin/env python3
"""waitdate.py PID MONTH DAY [SECS] - waits until the ORIGINAL's published calendar (0x007ced58, written by
Calendar_Update every sweep; docs/exe/weather.md) reads MONTH (1-12) and DAY, then prints one line, the date and
mGameTick, and exits 0; exits 1 after SECS (default 120) without it. Read-only. Polls every 20 ms, so a day
(about 23 ticks, 5.7 s, in the stock park) is never missed. `waitdate.py PID now` prints today's date and exits."""
import struct, sys, time
pid = int(sys.argv[1]); f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def now():
    return rd(0x007ced68, 'i') + 1, rd(0x007ced64, 'i'), rd(0x007ced6c, 'i') + 1900, rd(rd(0x0080239c) + 0x1da70c)
if sys.argv[2] == 'now':
    m, d, y, t = now(); print(f'date {m}/{d}/{y} tick {t}'); sys.exit(0)
month, day = int(sys.argv[2]), int(sys.argv[3]); secs = float(sys.argv[4]) if len(sys.argv) > 4 else 120
t0 = time.time()
while time.time() - t0 < secs:
    try:
        m, d, y, t = now()
        if (m, d) == (month, day):
            print(f'{time.strftime("%H:%M:%S")} date {m}/{d}/{y} tick {t}', flush=True); sys.exit(0)
    except (OSError, struct.error): pass
    time.sleep(0.02)
print(f'no {month}/{day} in {secs:.0f} s', flush=True); sys.exit(1)
