#!/usr/bin/env python3
"""progress.py PID [SECS] - the original's memory: the player's progress object ([0x00f7c58c], PlayerProgress), the
current park's record (its +0x48, PlayerProgress_SetCurrentPark 0x005b08a0) and every park record in its tree (+0x3c, an
MSVC map keyed by the theme's name; nil node [0x00f7c968]; node +0xc the key's string object, length +0x10, its
bytes +0x14; the record at node +0x20), each with its six tickets (+0..+5), its four records' flags (+6..+9) and values
(+0xc..), mNameChanged (+0xb8) and mAllResearchCompleted (+0xb9), which
PlayerProgress_MarkParkAllResearched (0x005b0ca0) sets and PlayerProgress_AllParksAllResearched (0x005b0c40) reads.
gms.dat saves both as each theme record's last two bytes (docs/exe/saves.md, "gms.dat"). With SECS, a line each time
any of it changes, with mGameTick, stamped from the start; without, one dump. The player's own: the four record
tickets (+0x18), the two secret (+0x26), spent (+0x1c), keys given (+0x20) and the ticket-bought ride ids, the set the
mystery row and the purchase ask (FUN_004d4b70; head +0x2c, nil [0x00f7c970], a node's id its +0xc word; Q709). Read-only. A POLL for loadfile.sh (Q550;
the tickets Q706)."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2]) if len(sys.argv) > 2 else 0
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def name(node):
    n, buf = rd(node + 0x10), rd(node + 0x14)
    if n <= 0 or n > 64 or buf == 0: return '?'
    f.seek(buf); return f.read(n).decode('latin-1')
def snapshot():
    prog = rd(0x00f7c58c)
    if prog == 0: return 'no progress object'
    nil, head, cur = rd(0x00f7c968), rd(prog + 0x3c), rd(prog + 0x48)
    parks, node, guard = [], rd(head), 0
    while node != head and node not in (0, nil) and guard < 32:
        rec = rd(node + 0x20)
        tk = [rd(rec + i, 'B') for i in range(6)]
        rv = [rd(rec + 0xc + 4 * i, 'i') if rd(rec + 6 + i, 'B') else '-' for i in range(4)]
        parks.append(f"{name(node)}{'*' if rec == cur else ''} tickets {tk} records {rv} renamed {rd(rec + 0xb8, 'B')} all-researched {rd(rec + 0xb9, 'B')}")
        guard += 1
        right = rd(node + 8)
        if right != nil:
            node = right
            while rd(node) != nil: node = rd(node)
        else:
            parent = rd(node + 4)
            while node == rd(parent + 8): node, parent = parent, rd(parent + 4)
            if rd(node + 8) != parent: node = parent
    current = f"current record {cur:#x} all-researched {rd(cur + 0xb9, 'B')}" if cur else "no current record"
    own = (f"records {[rd(prog + 0x18 + i, 'B') for i in range(4)]} secret {[rd(prog + 0x26 + i, 'B') for i in range(2)]} "
           f"spent {rd(prog + 0x1c, 'i')} keys given {rd(prog + 0x20, 'i')} rides {rides(prog)}")
    return f"{own}; {current}; parks: " + "; ".join(parks)
def rides(prog):
    snil, ids, todo = rd(0x00f7c970), [], [rd(rd(prog + 0x2c) + 4)]
    while todo and len(ids) < 300:
        n = todo.pop()
        if n in (0, snil): continue
        ids.append(rd(n + 0xc, 'H')); todo += [rd(n), rd(n + 8)]
    return sorted(ids)
def tick():
    try: return rd(rd(0x0080239c) + 0x1da70c)
    except (OSError, struct.error, ValueError): return -1
t0 = time.time(); last = None
while True:
    try: s = snapshot()
    except (OSError, struct.error, ValueError) as e: s = f'unreadable ({e.__class__.__name__})'
    if s != last:
        print(f"{time.time() - t0:7.2f} s tick {tick()} {s}", flush=True); last = s
    if time.time() - t0 >= secs: break
    time.sleep(0.05)
