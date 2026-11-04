"""wallseed.py - how many draws of the park's generator a fresh park's boundary wall took, read from the park file (Q872).

    wallseed.py [--per-straight N] [--per-corner N] [--reverse] [--back B] [--all [--jobs J]] <park file>...

A fresh park's straights choose their variant from the park's generator (FUN_00516330, the state mRandomSeed): each
straight with two opposite links and no flag takes the class of the roll it holds (under 18 none, under 24 20, else 23),
adds it to row 2 where it differs from the last straight's, and draws the next roll (`docs/exe/park.md`, "The boundary
hoarding's piece"). This reads the anchors in walk order (the model slots' order) and the rows they were given (2, 22 or
25). The first straight's own roll is carried from the park before, so its row only bounds the last variant.

By default it steps the file's own mRandomSeed back 1 to B draws (default 2000; the generator is a bijection) and
prints every count whose state, taken as the one the first straight's draw starts from, gives every later straight's
row: `fits after N` says the generator stood N draws past it when the file was written. `--all` instead tries every one
of the 2^32 states (about two minutes a park on 8 jobs, numpy) and prints each that fits, with its draws to the seed.
The options try other rules as controls: N draws a straight, N more at each corner, the walk taken backwards.
Q872 ran it over the original's 96 fresh Lost Kingdom parks (`original/freshfs.sh make`'s proton-restart.INTS).
"""
import os, struct, sys
from concurrent.futures import ProcessPoolExecutor
import numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pk import Park

A, C = 0x19660d, 0x3c6ef35f
A_INV = pow(A, -1, 1 << 32)

def wall(path):
    """The anchors (track type 25) in model-slot order as (row, mask, flags), and the file's mRandomSeed."""
    p = Park(path); out = []
    for i in range(16384):
        a = p.cell[i]; s = p.b[a]
        if not s & 2: continue
        t = a + 1 + (52 if s & 1 else 0)
        d, fl, mesh, nb = struct.unpack_from('<BHiB', p.b, t)
        tset, tidx, tang, typ = struct.unpack_from('<iiii', p.b, t + 12)
        if typ == 25: out.append((mesh, tidx, p.b[t + 28], fl))
    return [a[1:] for a in sorted(out)], p.get('seed')

def events(anchors):
    """A drawing straight's row, or -1 for a corner; ends and flagged straights draw nothing."""
    ev = []
    for row, mask, fl in anchors:
        if mask in (0x11, 0x44) and not fl & 0xf:
            assert row in (2, 22, 25), (row, mask, fl); ev.append(row)
        elif bin(mask).count('1') == 2 and mask not in (0x11, 0x44): ev.append(-1)
    return ev

def gen(s):
    u = (s * A + C) & 0xffffffff; return ((u >> 13) | (u << 19)) & 0xffffffff

def back(s):
    u = ((s << 13) | (s >> 19)) & 0xffffffff; return ((u - C) * A_INV) & 0xffffffff

def roll(s): return ((-s if s >= 0x80000000 else s) & 0xffffffff) % 30

def cls(r): return 0 if r < 18 else 20 if r < 24 else 23

def fits(state, ev, per_straight=1, per_corner=0):
    """The draws from state to the wall's end if its rolls give every straight after the first, else None."""
    first = next(i for i, e in enumerate(ev) if e >= 0)
    for last in ([0, 20, 23] if ev[first] == 2 else [ev[first] - 2]):
        s, n, ok = state, 0, True
        for _ in range(per_straight): s = gen(s); n += 1
        for e in ev[first + 1:]:
            if e < 0:
                for _ in range(per_corner): s = gen(s); n += 1
                continue
            v = cls(roll(s)); row = 2 + (v if v != last else 0)
            if row != e: ok = False; break
            last = v
            for _ in range(per_straight): s = gen(s); n += 1
        if ok: return n
    return None

def step(s):
    s = s * np.uint32(A) + np.uint32(C)
    s = (s >> np.uint32(13)) | (s << np.uint32(19))
    r = np.where(s >= np.uint32(0x80000000), np.uint32(0) - s, s)
    return s, r % np.uint32(30)

def scan(job):
    ev, lo, hi, per_straight, per_corner = job
    first = next(i for i, e in enumerate(ev) if e >= 0)
    hits = []
    for base in range(lo, hi, 1 << 16):
        S = np.arange(base, min(hi, base + (1 << 16)), dtype=np.uint64).astype(np.uint32)
        lasts = [0, 20, 23] if ev[first] == 2 else [ev[first] - 2]
        S = np.tile(S, len(lasts)); L = np.repeat(np.array(lasts, np.uint8), len(S) // len(lasts))
        s = S.copy()
        for _ in range(per_straight): s, R = step(s)
        for e in ev[first + 1:]:
            if e < 0:
                for _ in range(per_corner): s, R = step(s)
                continue
            v = np.where(R < 18, 0, np.where(R < 24, 20, 23)).astype(np.uint8)
            keep = 2 + np.where(v != L, v, 0) == e
            if not keep.any(): S = S[:0]; break
            S, s, L, R = S[keep], s[keep], v[keep], R[keep]
            for _ in range(per_straight): s, R = step(s)
        hits += [int(x) for x in S]
    return hits

def main(argv):
    per_straight, per_corner, reverse, limit, every, jobs, files = 1, 0, False, 2000, False, 8, []
    it = iter(argv)
    for a in it:
        if a == '--per-straight': per_straight = int(next(it))
        elif a == '--per-corner': per_corner = int(next(it))
        elif a == '--reverse': reverse = True
        elif a == '--back': limit = int(next(it))
        elif a == '--all': every = True
        elif a == '--jobs': jobs = int(next(it))
        else: files.append(a)
    pool = ProcessPoolExecutor(jobs) if every else None
    for f in files:
        anchors, seed = wall(f); ev = events(anchors)
        if reverse: ev = ev[::-1]
        head = '%s: %d anchors, %d drawing straights, %d corners; mRandomSeed %08x;' % (f, len(anchors),
            sum(1 for e in ev if e >= 0), ev.count(-1), seed)
        if every:
            parts = [(ev, k << 28, (k + 1) << 28, per_straight, per_corner) for k in range(16)]
            states = sorted(set(sum(pool.map(scan, parts), [])))
            out = []
            for x in states:
                s, k = x, 0
                while s != seed and k < 100000: s = gen(s); k += 1
                out.append('%08x seed after %s' % (x, k if s == seed else 'none in 100000'))
            print(head, 'states %d' % len(states), *out, flush=True)
        else:
            s, hits = seed, []
            for b in range(1, limit + 1):
                s = back(s)
                n = fits(s, ev, per_straight, per_corner)
                if n is not None: hits.append('fits after %d (state %08x, the wall drew %d)' % (b, s, n))
            print(head, 'stepped back %d: %d fit' % (limit, len(hits)), *hits, flush=True)
    if pool: pool.shutdown()

if __name__ == '__main__':
    main(sys.argv[1:])
