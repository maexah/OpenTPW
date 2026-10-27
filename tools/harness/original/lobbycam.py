#!/usr/bin/env python3
"""lobbycam.py - poll the running ORIGINAL's island-lobby camera, one line a frame (Q670).

  lobbycam.py PID [SECONDS] [POLL_MS]      SECONDS default 30, POLL_MS default 1

Finds the island lobby camera by its vtable 0x00702ec0 (the derived class, docs/exe/lobby.md, "The derived vtable") in the
process's writable mappings, then prints a line each time its angle +0x18, radius +0x20 or vertical +0x24 changes (one
line a camera pass): seconds since start, state +0x14 (0 orbit, 1 homing, 2 flying in), angle, radius, vertical, the
delta FUN_005e0470 steps by ([0x00f82884]+8), the render camera's fade word ([0x00f83778]+0x60), and the island
heading (current island [+0x0c] +0x14). A last summary per state: passes, seconds, passes a second, the median delta,
and the angle turned. Read-only. The first line gives the wall clock of its start (to set against lobbypanel.sh's click.t).
"""
import re, statistics, struct, sys, time

VT, SCENE, LENS = 0x00702ec0, 0x00f82884, 0x00f83778

def main():
    pid = int(sys.argv[1]); secs = float(sys.argv[2]) if len(sys.argv) > 2 else 30
    poll = (float(sys.argv[3]) if len(sys.argv) > 3 else 1) / 1000
    f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
    def rd(a, fmt='I'):
        f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
    def find():
        hits = []
        for line in open(f'/proc/{pid}/maps'):
            m = re.match(r'([0-9a-f]+)-([0-9a-f]+) rw', line)
            if not m: continue
            lo, hi = int(m[1], 16), int(m[2], 16)
            if hi - lo > 256 << 20 or lo >= 0x80000000: continue
            try:
                f.seek(lo); buf = f.read(hi - lo)
            except OSError:
                continue
            i = buf.find(b'\xc0\x2e\x70\x00')
            while i >= 0:
                a, i = lo + i, buf.find(b'\xc0\x2e\x70\x00', i + 4)
                if a & 3: continue
                try:
                    st, isl, rad = rd(a + 0x14), rd(a + 0x0c), rd(a + 0x20, 'f')
                except (OSError, struct.error):
                    continue
                if st in (0, 1, 2) and 0x10000 < isl < 0x80000000 and 0 <= rad < 1000:
                    hits.append(a)
        return hits
    t0 = time.time(); obj = None
    print(f'start wall {t0:.6f}', flush=True)
    while obj is None and time.time() - t0 < secs:
        hits = find()
        if hits:
            obj = hits[0]
            print(f'{time.time() - t0:7.3f}s camera object 0x{obj:08x} (candidates: {" ".join(hex(h) for h in hits)})', flush=True)
        else:
            time.sleep(0.05)
    if obj is None:
        print('no island camera found'); return
    last, rows = None, []
    while time.time() - t0 < secs:
        try:
            st = rd(obj + 0x14); ang, rad, ver = rd(obj + 0x18, 'f'), rd(obj + 0x20, 'f'), rd(obj + 0x24, 'f')
            delta = rd(rd(SCENE) + 8, 'f'); fade = rd(rd(LENS) + 0x60)
            isl = rd(obj + 0x0c); head = rd(isl + 0x14, 'f') if isl else float('nan')
        except (OSError, struct.error) as e:
            print(f'{time.time() - t0:7.3f}s read failed: {e}'); break
        key = (st, ang, rad, ver)
        if key != last:
            t = time.time() - t0
            print(f'{t:7.3f}s state {st} angle {ang:.5f} radius {rad:.4f} vertical {ver:.4f} delta {delta:.5f} '
                  f'fade 0x{fade:x} heading {head:.5f}', flush=True)
            rows.append((t, st, ang, rad, ver, delta)); last = key
        time.sleep(poll)
    for s in (0, 1, 2):
        r = [x for x in rows if x[1] == s]
        if len(r) < 2: continue
        dur = r[-1][0] - r[0][0]
        turned = sum(abs(((b[2] - a[2] + 3.14159265) % 6.2831853) - 3.14159265) for a, b in zip(r, r[1:]))
        print(f'state {s}: {len(r)} passes over {dur:.3f} s, {(len(r) - 1) / dur if dur else 0:.2f} a second, '
              f'median delta {statistics.median(x[5] for x in r):.5f}, angle turned {turned:.4f}, '
              f'radius {r[0][3]:.3f} -> {r[-1][3]:.3f}')

main()
