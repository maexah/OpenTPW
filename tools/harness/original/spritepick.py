#!/usr/bin/env python3
"""spritepick.py PID SECS [census] | spritepick.py PID where THING [depth] | spritepick.py PID census - the original's sprite pick, read-only, a line a change (a POLL for loadfile.sh).

The sprite draw latches the nearest pickable sprite whose circle holds the pointer (FUN_005890e0, 0x00589259 to
0x0058931a; park-engine.md, "The thing under a sprite"); FUN_00532bd0 then takes the thing of that sprite's script on
the hovered cell or a neighbour. Each line: the pointer (raw 0x00faa540/4 and as the -1..1 box 0x008bd4f0/4), the
latch [0x008bd4fc] and its owner, the latch this script PREDICTS from the decoded test over every sprite carrying
0x200 (the nearest by w of those whose d2 < r2), the pick's answer [0x00877350] and the hover's person [0x007b05ac],
both as thing ids, the hovered cell, and the predicted latch's d2 and r2. 'census' first prints every sprite of the
list ([0x008c47a0], count +0x2404) once, by flags and owner kind. 'PID census' alone (Q470) prints the list now and
exits: each flags word with the owner thing's kind of ANY kind ('none' where no thing's script owns it), how many, the
picture sizes and scales seen, and whether 0x200 (pickable) is set.

Sprite: +0x0 x y z w (projected), +0x10 flags, +0x28 frame (+0xc width, +0xe height, +0x10/+0x12 the reference,
+0x14/+0x16 the origin, signed), +0x2c scale. Owner: the script whose +0xd0 is the sprite (table [0x007b49f0], count
[0x007b49f4], +0x114 nonzero), then the thing whose +0xc is that script's index."""
import struct, sys, time
pid = int(sys.argv[1])
WHERE = sys.argv[3] if len(sys.argv) > 3 and sys.argv[2] == 'where' else None
NOW = len(sys.argv) > 2 and sys.argv[2] == 'census'
secs = 0 if WHERE or NOW else float(sys.argv[2]); census = 'census' in sys.argv[3:]
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]

def things():
    """Thing id by address and by script index, kinds 1, 4-8, 0x12 only (FUN_00532bd0's)."""
    by_addr, by_script = {}, {}
    for tid in range(1, 4096):
        p = rd(0x007cfb90 + tid * 0x14)
        if not p: continue
        by_addr[p] = tid
        kind = rd(p + 2, 'B')
        if kind in (1, 4, 5, 6, 7, 8, 0x12):
            by_script.setdefault(rd(p + 0xc, 'i'), (tid, kind))
    return by_addr, by_script

def scripts():
    table, n = rd(0x007b49f0), rd(0x007b49f4)
    out = {}
    for i in range(1, n):
        s = rd(table + i * 4)
        if s and rd(s + 0x114):
            out[s + 0xd0] = i
    return out

def sprite(s, px, py, G):
    x, y, z, w = (rd(s + o, 'f') for o in (0, 4, 8, 12))
    flags = rd(s + 0x10); fr = rd(s + 0x28); scale = rd(s + 0x2c, 'f')
    wd, ht, rw, rh = (rd(fr + o, 'H') for o in (0xc, 0xe, 0x10, 0x12))
    hx, hy = rd(fr + 0x14, 'h'), rd(fr + 0x16, 'h')
    sx, sy = scale * 20.0 * G, scale * 20.0
    L, R = hx / rw * sx, (wd + hx) / rw * sx
    T, B = hy / rh * sy, (ht + hy) / rh * sy
    if flags & 0x10: L, R = -L, -R
    if flags & 0x60: T, B = -T, -B
    d2 = (px - (x + L) / w) ** 2 + (py - y / w) ** 2 if w else 1e9
    r2 = ((B - T) ** 2 + (R - L) ** 2) * 0.0002
    return dict(s=s, flags=flags, w=w, d2=d2, r2=r2, size=(wd, ht), origin=(hx, hy), ref=(rw, rh), scale=scale,
                ndc=((x + L) / w if w else 0, y / w if w else 0))

def depth(s, flags):
    """Q477: how the sprite pass meets the depth buffer (park-engine.md, "What hides a person below their feet"). The
    picture's texture flags (frame +0 -> texture +8) give the state word (Texture_BlendStateWord's four by & 0x300),
    Sprite_BuildQuad (FUN_00589b50) ORs 0x800 (no depth write, 0x00589e85) for sprite flag 0x80000 (SpriteList_Draw's own
    0x4000 -> 0x800 only keys its groups); FUN_00589b50 pulls each corner's z by
    K1 / (z - K2) ([0x00702044], [0x00702038]) when flags carry 0x200, neither 0x40000 nor 0x80000, and [0x008bcbc8] lacks 0x2000; FUN_00579690
    case 0 maps z to the buffer: (uint)z < [0x008bcc28] -> [0x008bcc34] z + [0x008bcc38], else z/w x [0x008bcc2c] +
    [0x008bcc30]. The pull is also given in eye units (the projection: z = Q (e - near), w = sin45 e)."""
    x, y, z, w = (rd(s + o, 'f') for o in (0, 4, 8, 12))
    tex = rd(rd(s + 0x28)); tf = rd(tex + 8) if tex else 0
    word = {0: 0x1256, 0x100: 0x2756, 0x200: 0x556, 0x300: 0x200552}[tf & 0x300] if tex else 0x1256
    if flags & 4: word = 0x200522 + (0x30 if flags & 0x2000 else 0)
    for bit, add in ((0x200000, 0x200000), (0x20000, 0x20000), (0x80000, 0x800), (0x10000, 0x8000000)):
        if flags & bit: word |= add
    rs = rd(0x008bcbc8); k1, k2 = rd(0x00702044, 'f'), rd(0x00702038, 'f')
    pulled = z - k1 / (z - k2) if (flags & 0x200) and not (flags & 0xc0000) and not (rs & 0x2000) else z
    thr = rd(0x008bcc28); A, B, C, D = (rd(0x008bcc2c + 4 * k, 'f') for k in range(4))
    def sz(v): return C * v + D if struct.unpack('<I', struct.pack('<f', v))[0] < thr else v / w * A + B
    near, far = rd(0x0087b1dc, 'f'), rd(0x00768acc, 'f'); sn = 0.7071067811865476
    # Q from the sprite's own z and w (e = w / sin45): the draw matrix's z row is not built with the far [0x00768acc]
    # holds at the time (Q477: it implies a far near 1000).
    e0 = w / sn; Q = z / (e0 - near) if e0 > near else sn
    def eye(v): return Q * near / (Q - sn * v / w) if w else 0
    print(f'depth: texture flags 0x{tf:x} word 0x{word:x} (zwrite {"off" if word & 0x800 else "on"}, ztest {"off" if word & 0x8000 else "on"}, '
          f'{"sorted later" if not (word & 0x3000) else "drawn at its flush"}) render 0x{rs:x} K1 {k1} K2 {k2} z {z:.5f} pulled {pulled:.5f} '
          f'near {near:.4f} far {far:.2f} (matrix\'s {near / (1 - sn / Q) if Q > sn else 0:.0f}) buffer thr 0x{thr:x} A {A:.6g} B {B:.6g} C {C:.6g} D {D:.6g} sz {sz(z):.7f} -> {sz(pulled):.7f} '
          f'eye {eye(z):.3f} -> {eye(pulled):.3f} (pull {eye(z) - eye(pulled):.3f})')

DEPTH = 'depth' in sys.argv[4:]
if WHERE:
    # where THING: the thing's sprite now - its pick circle's middle as a pixel of the 1024 x 768 frame, its w, and the
    # draw matrix [0x0087b088] applied to the sprite's +0x14 place, to set beside +0xc (which w the latch uses).
    by_addr, by_script = things(); owner_of = scripts(); G = rd(0x008bcbcc, 'f')
    want = int(WHERE); mgr = rd(0x008c47a0)
    for s in (rd(mgr + i * 4) for i in range(rd(mgr + 0x2404))):
        i = owner_of.get(s)
        if not i or by_script.get(i, (None,))[0] != want: continue
        p = sprite(s, 0.0, 0.0, G); m = rd(0x0087b088); M = [rd(m + 4 * k, 'f') for k in range(16)]
        wx, wy, wz = (rd(s + o, 'f') for o in (0x14, 0x18, 0x1c))
        clip = [M[r] * wx + M[4 + r] * wy + M[8 + r] * wz + M[12 + r] for r in range(4)]
        px, py = (p['ndc'][0] + 1) * 512, (p['ndc'][1] + 1) * 384
        print(f'where {want}: circle middle pixel {px:.0f} {py:.0f} r2 {p["r2"]:.4f} w {p["w"]:.3f} size {p["size"]} origin {p["origin"]} '
              f'flags 0x{p["flags"]:x} place ({wx:.2f},{wy:.2f},{wz:.2f}) matrix clip ({clip[0]:.3f},{clip[1]:.3f},{clip[2]:.3f},{clip[3]:.3f}) '
              f'xyzw ({rd(s, "f"):.3f},{rd(s + 4, "f"):.3f},{rd(s + 8, "f"):.3f},{rd(s + 12, "f"):.3f})')
        if DEPTH: depth(s, p['flags'])
        break
    else:
        print(f'where {want}: no sprite')
    sys.exit(0)
if NOW:
    # census now: every sprite of the list by flags and its owner thing's kind, whatever the kind.
    # A manager's +0xc is not a script index and can equal a person's (the analyser, thing 5, matched a member of
    # staff's), so a person's claim is taken first and a manager's only where no person claims the script.
    kinds = {}
    for people in (True, False):
        for tid in range(1, 4096):
            p = rd(0x007cfb90 + tid * 0x14)
            if p and (rd(p + 2, 'B') in (1, 4, 5, 6, 7, 8, 0x12)) == people:
                kinds.setdefault(rd(p + 0xc, 'i'), (tid, rd(p + 2, 'B')))
    owner_of = scripts(); G = rd(0x008bcbcc, 'f'); mgr = rd(0x008c47a0); n = rd(mgr + 0x2404)
    rows = {}
    for s in (rd(mgr + i * 4) for i in range(n)):
        i = owner_of.get(s); tid, kind = kinds.get(i, (None, 'none')) if i else (None, 'none')
        p = sprite(s, 0.0, 0.0, G)
        r = rows.setdefault((hex(p['flags']), kind), dict(n=0, sizes=set(), scales=set(), things=set()))
        r['n'] += 1; r['sizes'].add(p['size']); r['scales'].add(round(p['scale'], 3)); r['things'].add(tid)
    print(f'census now: {n} sprites, G {G:.4f}, gflags 0x{rd(0x008bcbc8):x}')
    for (fl, kind), r in sorted(rows.items(), key=lambda r: -r[1]['n']):
        print(f'   flags {fl:>10} pickable {"yes" if int(fl, 16) & 0x200 else "no "} owner kind {kind}: {r["n"]} '
              f'sizes {sorted(r["sizes"])[:6]} scales {sorted(r["scales"])} things {sorted(t for t in r["things"] if t)[:8]}')
    sys.exit(0)
last = None; t0 = time.time(); did_census = False
while time.time() - t0 < secs:
    try:
        mx, my = rd(0x00faa540, 'h'), rd(0x00faa544, 'h')
        px, py = rd(0x008bd4f0, 'f'), rd(0x008bd4f4, 'f')
        G, gflags = rd(0x008bcbcc, 'f'), rd(0x008bcbc8)
        latch = rd(0x008bd4fc); mgr = rd(0x008c47a0); n = rd(mgr + 0x2404)
        sprites = [rd(mgr + i * 4) for i in range(n)]
        by_addr, by_script = things(); owner_of = scripts()
        def owner(s):
            i = owner_of.get(s); return by_script.get(i, (None, None)) if i else (None, None)
        if census and not did_census:
            did_census = True
            rows = {}
            for s in sprites:
                fl = rd(s + 0x10); tid, kind = owner(s)
                key = (hex(fl), kind); rows[key] = rows.get(key, 0) + 1
            print(f'census: {n} sprites, G {G:.4f}, gflags 0x{gflags:x}', flush=True)
            for (fl, kind), c in sorted(rows.items(), key=lambda r: -r[1]):
                print(f'   flags {fl:>10} owner kind {kind}: {c}', flush=True)
        pick = [sprite(s, px, py, G) for s in sprites if rd(s + 0x10) & 0x200]
        inside = [p for p in pick if p['d2'] < p['r2']]
        pred = min(inside, key=lambda p: p['w'])['s'] if inside else 0
        pp = next((p for p in pick if p['s'] == (pred or latch)), None)
        answer = rd(0x00877350); person = rd(0x007b05ac); cell = rd(0x007b05cc, 'H')
        tick = rd(rd(0x0080239c) + 0x1da70c)
        now = (mx, my, latch, pred, answer, person, cell)
        if now != last:
            lo = owner(latch) if latch else (None, None); po = owner(pred) if pred else (None, None)
            extra = (f" d2 {pp['d2']:.4f} r2 {pp['r2']:.4f} at ({pp['ndc'][0]:.3f},{pp['ndc'][1]:.3f}) w {pp['w']:.2f}"
                     f" size {pp['size']} origin {pp['origin']} ref {pp['ref']} scale {pp['scale']:.2f} flags 0x{pp['flags']:x}"
                     if pp else '')
            cx, cy = ((cell - 1) % 128, (cell - 1) // 128) if cell else (None, None)
            print(f'{time.time() - t0:7.2f} s tick {tick}: mouse ({mx},{my}) ndc ({px:.3f},{py:.3f}) '
                  f'latch {"%x" % latch if latch else 0} thing {lo[0]} kind {lo[1]} | predicted {"%x" % pred if pred else 0} '
                  f'thing {po[0]} | inside {len(inside)} of {len(pick)} | pick {by_addr.get(answer, answer)} '
                  f'person {by_addr.get(person, person)} cell ({cx},{cy}){extra}', flush=True)
            last = now
    except (OSError, struct.error, ZeroDivisionError):
        time.sleep(0.05); continue
    time.sleep(0.1)
