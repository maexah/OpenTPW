#!/usr/bin/env python3
"""leavers.py PID SECS [EVERY] - the running ORIGINAL's guests setting off home, each with its cause, read-only, polled
every 50 ms (a POLL for loadfile.sh; Q668).

A guest is a thing of type 1 ([0x007cfb90 + id * 0x14], +2); its state the int at +0x220, its happiness the float at
+0x19c, mExitLevel the int at +0x1bc, its cell bytes +5 and +7. The park's door is world +0x1da710 (world [0x0080239c]).

A line each guest entering state 0x12 (HeadingForExit), with the state it came from, its happiness and exit level at the
poll before and the poll after, the door, and the cause: from state 6 (the deciding turn's leaving arm, 0x004fee5b)
"day" when the exit level read nought on either side (it loses one a needs turn, so it is 0 at the turn whichever side
of the decrement the turn fell), else "miserable" when the door is open (the only other arm; "miserable?" when the poll
before still read a happiness byte over nought, i.e. it fell between the polls), else "shut"; from any other state
"from <state>" (the gate's arms, states 3 and 4). Each guest is "loaded" (there at the start, or at the last load) or
"arrived" (made since). A line each guest made or gone.

Every EVERY sweeps of mGameTick (242, about a minute) from the start or the load, a census: the guests, those leaving
(0x12..0x15), those deciding (6), the mean happiness of the guests not leaving, how many of those read a happiness byte
of nought and under 25, how many read an exit level under nought (spent: they leave only for misery or the door) and
how many over it, the setters-off so far by cause, loaded and arrived apart, and every way out of state 6 so far by the
state it went to ("0x8/anim4" is the chooser finding nothing, 0x004ff480: spot animation 4 and -5 happiness; "0xa" a
choice routed; "0x7" a wander), and the happiness the guests still here gained and lost between polls. At the end the
same totals, and that LEDGER by the state at the poll before (">new" when the state changed across the change), most
lost first: where the crowd's happiness goes. Summed per poll, so changes inside one 50 ms poll net out. Each census
also names every entertainer (a thing of model 6), his cell, his state (+0x19c; 0xe performing) and his watchers: the
guests deciding on the nine cells round him, whom the deciding turn's watching arm holds there (0x004fefcf).

mGameTick going BACKWARDS is taken as a load: the counts restart and the guests then present are the loaded ones.
Compare with this game's "Person N: leaving, <why>, exit level E, happiness H to H2" log lines and its `peeps` census
(q668/ours.py, lib/peepcensus.py)."""
import struct, sys, time, collections
pid = int(sys.argv[1]); secs = float(sys.argv[2]); every = int(sys.argv[3]) if len(sys.argv) > 3 else 242
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def block(a, n):
    f.seek(a); return f.read(n)
def snapshot():
    world = rd(0x0080239c); tick = rd(world + 0x1da70c); closed = rd(world + 0x1da710)
    g = {}; ents.clear()
    for i in range(1, 2000):
        p = rd(0x007cfb90 + i * 0x14)
        if not p: continue
        try:
            b = block(p, 0x224)
        except OSError: continue
        if b[2] == 6: ents[i] = ((b[5], b[7]), struct.unpack_from('<i', b, 0x19c)[0])
        if b[2] != 1: continue
        g[i] = (struct.unpack_from('<i', b, 0x220)[0], struct.unpack_from('<f', b, 0x19c)[0],
                struct.unpack_from('<i', b, 0x1bc)[0], (b[5], b[7]), struct.unpack_from('<i', b, 0x10)[0])
    return tick, closed, g
def hbyte(h):
    try: return int(h) & 0xff
    except (ValueError, OverflowError): return -1
ents = {}
t0 = time.time(); last = None; loaded = set(); start = None
causes = collections.Counter(); turns = collections.Counter(); ledger = collections.Counter(); made = gone = 0
def census(tk, g, label="CENSUS"):
    here = {i: v for i, v in g.items() if not 0x12 <= v[0] <= 0x15}
    hs = [v[1] for v in here.values()]
    mean = sum(hs) / len(hs) if hs else 0
    watch = " ".join(f"entertainer {e} at {c} st {st:#x} watchers {sum(1 for v in g.values() if v[0] == 6 and max(abs(v[3][0] - c[0]), abs(v[3][1] - c[1])) <= 1)}"
                     for e, (c, st) in sorted(ents.items()))
    print(f"{label} {time.time() - t0:7.1f} s tick {tk} (+{tk - start}): {watch} guests {len(g)} leaving {len(g) - len(here)} "
          f"deciding {sum(1 for v in g.values() if v[0] == 6)} loaded-left {len(loaded & set(g))} mean-happiness {mean:.1f} "
          f"byte0 {sum(1 for h in hs if hbyte(h) == 0)} under25 {sum(1 for h in hs if h < 25)} "
          f"exit<0 {sum(1 for v in here.values() if v[2] < 0)} exit>0 {sum(1 for v in here.values() if v[2] > 0)} "
          f"made {made} gone {gone} setoff {dict(sorted(causes.items()))} from6 {dict(sorted(turns.items()))} "
          f"lost {-sum(n for n in ledger.values() if n < 0):.0f} gained {sum(n for n in ledger.values() if n > 0):.0f} "
          f"ledger {' '.join(f'{k}:{n:+.0f}' for k, n in sorted(ledger.items(), key=lambda kv: kv[1]) if abs(n) >= 0.5)}", flush=True)
nextc = None
while time.time() - t0 < secs:
    try:
        tk, closed, g = snapshot()
    except (OSError, struct.error):
        time.sleep(0.05); continue
    if last is None or tk < last[0]:
        start = tk; loaded = set(g); causes.clear(); turns.clear(); ledger.clear(); made = gone = 0; nextc = tk + every
        print(f"{'START' if last is None else 'LOAD'} {time.time() - t0:7.2f} s mGameTick {tk}: {len(g)} guests, door {closed}", flush=True)
        census(tk, g); last = (tk, closed, g); time.sleep(0.05); continue
    _, _, lg = last
    for i, v in g.items():
        if i not in lg:
            made += 1; print(f"tick {tk}: guest {i} MADE state {v[0]:#x} on {v[3]}", flush=True); continue
        was = lg[i]
        dh = v[1] - was[1]
        if dh: ledger[f"{was[0]:#x}" + (f">{v[0]:#x}" if v[0] != was[0] else "")] += dh
        if was[0] == 6 and v[0] != 6:
            turns[f"{v[0]:#x}" + (f"/anim{v[4]}" if v[0] == 8 else "")] += 1
        if v[0] == 0x12 and was[0] != 0x12:
            who = "loaded" if i in loaded else "arrived"
            if was[0] == 6:
                why = ("day" if was[2] == 0 or v[2] == 0 else
                       ("miserable" if hbyte(was[1]) == 0 else "miserable?") if not closed else "shut")
            else:
                why = f"from {was[0]:#x}"
            causes[(why, who)] += 1
            print(f"tick {tk}: guest {i} SETS OFF ({who}) {why}: from state {was[0]:#x}, happiness {was[1]:.1f} -> {v[1]:.1f}, "
                  f"exit {was[2]} -> {v[2]}, door {closed}, cell {v[3]}", flush=True)
    for i, v in lg.items():
        if i not in g: gone += 1; print(f"tick {tk}: guest {i} GONE from state {v[0]:#x} on {v[3]}", flush=True)
    last = (tk, closed, g)
    if tk >= nextc:
        census(tk, g); nextc += every
    time.sleep(0.05)
census(last[0], last[2], "END")
for (why, who), n in sorted(causes.items()):
    print(f"SETOFF {who} {why}: {n}")
for k, n in sorted(turns.items()):
    print(f"FROM6 {k}: {n}")
for k, n in sorted(ledger.items(), key=lambda kv: kv[1]):
    print(f"LEDGER {k}: {n:+.1f}")
