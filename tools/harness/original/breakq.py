#!/usr/bin/env python3
"""breakq.py PID SECS THING [all] - breaks a ride of the running ORIGINAL (VAR_BREAKSTAT, variable 4, as
scriptvar.py writes it) each time it is open with a queue that has stood four seconds, so the break puts a queue out.
With `all`, FIRST writes nought over the mask byte of `TEST byte ptr [EAX],0x7` at 0x0050133f (FUN_005012f0's test of
the guest's id), so EVERY guest dismissed from a queue plays kids 0x80, not one in eight: the sound's own path
(FUN_004faa00 on) is not touched. WRITES the original's memory; stop the original afterwards."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]); thing = int(sys.argv[3]); every = len(sys.argv) > 4
f = open(f'/proc/{pid}/mem', 'r+b', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
if every:
    was = rd(0x0050133d, 'B'), rd(0x0050133e, 'B'), rd(0x0050133f, 'B')
    assert was == (0xf6, 0x00, 0x07), was
    f.seek(0x0050133f); f.write(b'\x00'); print('mask at 0x0050133f was 7, written 0, reads', rd(0x0050133f, 'B'), flush=True)
def script():
    s = rd(0x008791b0); k = 0
    while s and k < 4000:
        k += 1
        if rd(s + 0xac) & 0xffff == thing and rd(s + 0x1c) and 4 < rd(s + 0x8c) < 400: return rd(s + 0x1c)
        s = rd(s)
t0 = time.time(); since = None; breaks = 0
while time.time() - t0 < secs:
    try:
        e = rd(0x007cfb90 + thing * 0x14); state = rd(e + 0x19c, 'i'); first = rd(e + 0x3c, 'H'); tick = rd(rd(0x0080239c) + 0x1da70c)
        if state == 0 and first:
            since = since or time.time()
            if time.time() - since >= 4:
                v = script(); f.seek(v + 16); f.write(struct.pack('<i', 1)); breaks += 1; since = None
                print(f"{time.time() - t0:7.2f} s tick {tick}: break {breaks}, the queue's head was guest {first}", flush=True); time.sleep(3)
        else: since = None
    except (OSError, struct.error) as x: print('read failed', x, flush=True)
    time.sleep(0.1)
