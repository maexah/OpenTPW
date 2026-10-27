#!/usr/bin/env python3
"""tpwmem.py - read the running ORIGINAL game's memory (testme.exe addresses; image base 0x400000, no relocations).

  tpwmem.py read ADDR[:fmt] ...   fmt: I (u32, default) i h H B f d, or sN for N raw bytes shown as hex
  tpwmem.py watch ADDR[:fmt] [SECONDS]   print each change for SECONDS (default 10)
  tpwmem.py clock [SECONDS]        is the park clock running true? Only meaningful inside a park. SECONDS default 10;
                                   exits 0 when true, 2 when not (1: no such command, or the original is not running)

ADDR is always read as hex, with or without 0x: a decimal one is misread without a word.

Read-only. The pid comes from `original.sh pid`, or pass --pid N first.
Addresses and meanings: docs/TOOLING.md, "The original under Proton", and the docs/exe/ rows they cite.
"""
import os, struct, subprocess, sys, time

TICKS, ACCUM, SAMPLE = 0x00877d34, 0x00785988, 0x008786bc   # park tick counter, park clock double, per-frame sample
TICK_HZ = 1000 / 31                                         # 32.26 ticks a second when the clock runs true

def find_pid(args):
    if args[:1] == ['--pid']:
        return int(args[1]), args[2:]
    here = os.path.dirname(os.path.abspath(__file__))
    out = subprocess.run([os.path.join(here, 'original.sh'), 'pid'], capture_output=True, text=True)
    if out.returncode != 0:
        sys.exit('The original is not running (original.sh pid found nothing).')
    return int(out.stdout.strip()), args

def reader(pid):
    f = open(f'/proc/{pid}/mem', 'rb', buffering=0)   # unbuffered: a buffered reader serves a repeat read from its cache
    def rd(addr, fmt='I'):
        if fmt.startswith('s'):
            f.seek(addr); return f.read(int(fmt[1:])).hex(' ')
        size = struct.calcsize('<' + fmt)
        f.seek(addr); return struct.unpack('<' + fmt, f.read(size))[0]
    return rd

def parse(spec):
    a, _, fmt = spec.partition(':')
    return int(a, 16), fmt or 'I'

def main():
    pid, args = find_pid(sys.argv[1:])
    rd = reader(pid)
    cmd = args[0] if args else ''
    if cmd == 'read':
        for spec in args[1:]:
            a, fmt = parse(spec)
            v = rd(a, fmt)
            print(f'0x{a:08x}:{fmt}  {v}' + (f'  (0x{v:x})' if isinstance(v, int) else ''))
    elif cmd == 'watch':
        a, fmt = parse(args[1]); secs = float(args[2]) if len(args) > 2 else 10
        t0, last = time.time(), None
        while time.time() - t0 < secs:
            v = rd(a, fmt)
            if v != last: print(f'{time.time() - t0:7.2f}s  {v}'); last = v
            time.sleep(0.05)
    elif cmd == 'clock':
        secs = float(args[1]) if len(args) > 1 else 10
        up = float(open('/proc/uptime').read().split()[0]) / 86400
        k0, a0, t0 = rd(TICKS), rd(ACCUM, 'd'), time.time()
        time.sleep(secs)
        k1, a1, t1 = rd(TICKS), rd(ACCUM, 'd'), time.time()
        rate, ratio = (k1 - k0) / (t1 - t0), (a1 - a0) / 1000 / (t1 - t0)
        ok = abs(rate / TICK_HZ - 1) < 0.05 and abs(ratio - 1) < 0.05
        print(f'uptime {up:.1f} days; ticks {rate:.1f}/s (true {TICK_HZ:.1f}); park clock {ratio:.2f}x real time')
        if k1 == k0:
            print('NOT TRUE: the tick counter did not move. Not in a park, or the clock is frozen (uptime + frame rate).')
        elif not ok:
            print('NOT TRUE: timings measured now are distorted. Restart the computer (uptime) and keep the 30 fps cap.')
        else:
            print('TRUE: the park clock runs at real time; timings are usable.')
        sys.exit(0 if ok else 2)
    else:
        print(__doc__); sys.exit(1)

if __name__ == '__main__':
    main()
