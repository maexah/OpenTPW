#!/usr/bin/env python3
"""retrigger.py PID SECS THING [TIMES] - in the running ORIGINAL, send an arrival vehicle round again as a summons does:
each time its script's VAR_STATUS (variable 1) reads 0 after having read anything else, write its VAR_TRIGGER
(variable 0) 1, which is what FUN_0051a2f0 writes (docs/exe/park.md, "The spent vehicle"). TIMES rounds (1), polled
every 0.2 s for SECS. The bus, the ferry and the seaplane each declare VAR_TRIGGER, VAR_STATUS first. A POLL for
loadfile.sh (%PID%). Prints each status change with mGameTick. Q622: the seaplane's second visit."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]); thing = int(sys.argv[3]); times = int(sys.argv[4]) if len(sys.argv) > 4 else 1
def script(f):
    def rd(a, fmt='<I'):
        f.seek(a); return struct.unpack(fmt, f.read(struct.calcsize(fmt)))[0]
    s = rd(0x008791b0); k = 0
    while s and k < 4000:
        k += 1
        if rd(s + 0xac) & 0xffff == thing:
            n = rd(s + 0x8c); v = rd(s + 0x1c)
            if v and 2 <= n < 400: return v
        s = rd(s)
    return None
t0 = time.time(); last = None; armed = False; done = 0
while time.time() - t0 < secs and done < times:
    try:
        with open(f'/proc/{pid}/mem', 'r+b', buffering=0) as f:
            f.seek(0x0080239c); world = struct.unpack('<I', f.read(4))[0]
            f.seek(world + 0x1da70c); tick = struct.unpack('<i', f.read(4))[0] if world else -1
            v = script(f)
            if v is None: time.sleep(0.2); continue
            f.seek(v); trig, status = struct.unpack('<ii', f.read(8))
            if status != last: print(f"retrigger: tick {tick} thing {thing} VAR_STATUS {status} VAR_TRIGGER {trig}", flush=True); last = status
            if status != 0: armed = True
            elif armed:
                f.seek(v); f.write(struct.pack('<i', 1)); done += 1; armed = False
                print(f"retrigger: tick {tick} thing {thing} spent: VAR_TRIGGER written 1 ({done} of {times})", flush=True)
    except (FileNotFoundError, ProcessLookupError): break
    time.sleep(0.2)
