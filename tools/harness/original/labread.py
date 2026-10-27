#!/usr/bin/env python3
"""labread.py - the ORIGINAL's research lab, live: its projects, its five kinds, and each project finished as it happens.

    labread.py PID [SECONDS]      one dump, then (with SECONDS) a poll: a line per project finished, per kind's current
                                  project or highest group changed, and every 10 s the points of each kind's current project
    labread.py PID 0              the dump alone

The lab is the thing [0x007cfb90 + 20 * world[+0x1da722]] (FUN_00519550), world [0x007cf83c]; mGameTick is world
[+0x1da70c]. In memory a project is 20 bytes from +0xc (+0 item word, +4 mCat, +8 group byte, +9 tier byte, +0xc mCost,
+0x10 mResearchDone, floats), a kind 16 bytes from +0x140c (effort, output, current, highest group byte), mNumProjects
+0x145c, mWorkLoad +0x1460 (OpenTPW docs/exe/park-engine.md, "The lab at work"). Read-only. Made for Q701.
"""
import struct, sys, time

WORLD, THINGS, LAB_ID, TICK = 0x007cf83c, 0x007cfb90, 0x1da722, 0x1da70c


def reader(pid):
    f = open(f'/proc/{pid}/mem', 'rb', buffering=0)

    def rd(addr, fmt):
        n = struct.calcsize('<' + fmt)
        f.seek(addr)
        return struct.unpack('<' + fmt, f.read(n))
    return rd


def lab(rd):
    world = rd(WORLD, 'I')[0]
    if not world:
        return None
    tick = rd(world + TICK, 'I')[0]
    lid = rd(world + LAB_ID, 'H')[0]
    at = rd(THINGS + 20 * lid, 'I')[0]
    if not at:
        return None
    n = rd(at + 0x145c, 'i')[0]
    if not 0 <= n <= 256:
        return None
    projects = [rd(at + 0xc + 20 * i, 'HxxiBBxxff') for i in range(n)]
    kinds = [rd(at + 0x140c + 16 * k, 'iiiB') for k in range(5)]
    return dict(tick=tick, at=at, n=n, work=rd(at + 0x1460, 'i')[0], active=rd(at + 0x1468, 'B')[0],
                projects=projects, kinds=kinds)


def dump(l):
    print('tick %d lab 0x%x projects %d work load %d active %d' % (l['tick'], l['at'], l['n'], l['work'], l['active']))
    for k, (effort, output, current, highest) in enumerate(l['kinds']):
        print('  kind %d effort %d output %d current %d highest %d' % (k, effort, output, current, highest))
    for i, (item, cat, group, tier, cost, done) in enumerate(l['projects']):
        print('  project %d item %d kind %d group %d tier %d %.4f of %g' % (i, item, cat, group, tier, done, cost))
    sys.stdout.flush()


def main():
    pid = int(sys.argv[1])
    secs = float(sys.argv[2]) if len(sys.argv) > 2 else 0
    rd = reader(pid)
    t0 = time.time()
    l = None
    while l is None and time.time() - t0 < max(secs, 1):
        l = lab(rd)
        if l is None:
            time.sleep(0.5)
    if l is None:
        sys.exit('no lab: no park is up')
    dump(l)
    last, beat = l, time.time()
    while time.time() - t0 < secs:
        time.sleep(0.05)
        l = lab(rd)
        if l is None:
            continue
        stamp = '%8.2f s tick %d' % (time.time() - t0, l['tick'])
        for i, p in enumerate(l['projects']):
            was = last['projects'][i] if i < len(last['projects']) else None
            if p[5] >= p[4] and (was is None or was[5] < was[4]):
                print('%s FINISHED project %d item %d kind %d tier %d at %.4f of %g' % (stamp, i, p[0], p[1], p[3], p[5], p[4]))
        if l['n'] != last['n']:
            print('%s projects %d -> %d' % (stamp, last['n'], l['n']))
        for k in range(5):
            if l['kinds'][k][2:] != last['kinds'][k][2:]:
                print('%s kind %d current %d -> %d, highest %d -> %d' % (stamp, k, last['kinds'][k][2], l['kinds'][k][2],
                                                                        last['kinds'][k][3], l['kinds'][k][3]))
        if time.time() - beat >= 10:
            beat = time.time()
            cur = ['%d:%.2f' % (k, l['projects'][c][5]) if 0 <= c < l['n'] else '%d:-' % k
                   for k, (_, _, c, _) in enumerate(l['kinds'])]
            print('%s points %s' % (stamp, ' '.join(cur)))
        sys.stdout.flush()
        last = l


if __name__ == '__main__':
    main()
