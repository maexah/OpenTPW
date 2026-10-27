#!/usr/bin/env python3
"""usedlist.py - the ORIGINAL's thing numbering, read live: the used list, the free list's head and the deleted list.

  usedlist.py [--pid N] once            one snapshot: tick, the used list from its head (id:model), the free list's
                                        first ids, the ids deleted this sweep
  usedlist.py [--pid N] watch SECONDS   one line per change: each id made (with its model byte) or gone from the used
                                        list, the deleted list whenever it holds anything, and the free list's head after
  usedlist.py [--pid N] free [COUNT]    the free list's first COUNT ids (default 8)

The used list [0x007cf56c] (node: +0 thing, +4 id, +8 next), made at its head by FUN_00516270; the free list
[0x00801bbc] (+0x10 next); the deleted list object 0x00801f38 (head +4, count +0xc; node: +0 id u16, +4 next), pushed
and popped at its head (FUN_0051ad40, FUN_0051ad90). docs/exe/ride-operation.md, "Where a ride's turn comes from".
Read-only. Q238's usedlist.sh is the slow shell walk of the first of these.
"""
import os, sys, time
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import tpwmem

USED, FREE, DELETED = 0x007cf56c, 0x00801bbc, 0x00801f38

def used(rd):
    out, n = [], rd(USED)
    while n and len(out) < 12000:
        thing = rd(n); out.append((rd(n + 4), rd(thing + 2, 'B') if thing else -1)); n = rd(n + 8)
    return out

def free(rd, count=8):
    out, n = [], rd(FREE)
    while n and len(out) < count:
        out.append(rd(n + 4)); n = rd(n + 0x10)
    return out

def deleted(rd):
    out, n = [], rd(DELETED + 4)
    while n and len(out) < 1000:
        out.append(rd(n, 'H')); n = rd(n + 4)
    return out

def main():
    pid, args = tpwmem.find_pid(sys.argv[1:]); rd = tpwmem.reader(pid)
    cmd = args[0] if args else 'once'
    if cmd == 'once':
        print(f"tick {rd(tpwmem.TICKS)}; used {' '.join(f'{i}:{m}' for i, m in used(rd))}; free {free(rd)}; deleted {deleted(rd)}")
    elif cmd == 'free':
        print(free(rd, int(args[1]) if len(args) > 1 else 8))
    elif cmd == 'watch':
        end = time.time() + float(args[1] if len(args) > 1 else 60)
        last = dict(used(rd)); lastdel = []
        print(f"t0 tick {rd(tpwmem.TICKS)}: {len(last)} things; free {free(rd, 4)}", flush=True)
        while time.time() < end:
            now = dict(used(rd)); tick = rd(tpwmem.TICKS); dl = deleted(rd)
            made = [i for i in now if i not in last]; gone = [i for i in last if i not in now]
            if dl != lastdel and dl:
                print(f"tick {tick}: deleted list {dl}", flush=True)
            if made or gone:
                print(f"tick {tick}: made {[f'{i}:{now[i]}' for i in made]} gone {gone}; free {free(rd, 4)}", flush=True)
            last, lastdel = now, dl
            time.sleep(0.01)
    else:
        sys.exit(__doc__)

if __name__ == '__main__':
    main()
