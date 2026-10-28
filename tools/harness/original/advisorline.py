#!/usr/bin/env python3
"""advisorline.py PID SECS - the ORIGINAL's speaker, read-only, a line a change with mGameTick (a POLL for loadfile.sh
and fsload.sh). Q707.

What Advisor_SayResponse (0x00599050) and Advisor_Update (0x00599880) keep (docs/exe/scenes.md, "A row and the voice's
start"): busy (0x00f7966c), the line's model slot (0x00f79674), sample (0x00f7967c), its two props (0x00f79684,
0x00f79688), the line's start on the advisor clock less 200 (0x00f79664), the voice's due time after a lead-in
(0x00f79694, printed less the start: 0 while a lead-in runs, for SayResponse moves the start by
the same 1000 it set the voice's time by, and `none` once the voice has started or when the row opens on no clip 14), the clips' end (0x00f79668), the cue timer
(0x00f79778, nought for none or once fired), the clip words (0x00f79718..) and the index of the next (0x00f79768), the
running clip's flags (0x00f79660), UI particle channel 0's count (0x00f8692c), and the lobby island panel's pointer
(0x007cc4b4, which IslandPanel_Refresh 0x004b9340 needs). The clip lengths table (0x00f796c4, clip n at +4n, filled
by Advisor_ResetState from slot 1's model) is printed once at the start. Times print relative to the line's start."""
import struct, sys, time
pid = int(sys.argv[1]); secs = float(sys.argv[2])
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='I'):
    f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
def words():
    out = []
    for i in range(20):
        w = rd(0x00f79718 + 4 * i)
        if w == 0: break
        out.append(hex(w))
    return out
t0 = time.time()
print('clip lengths', [rd(0x00f796c4 + 4 * n, 'i') for n in range(1, 21)], flush=True)
last = None
while time.time() - t0 < secs:
    try:
        tick = rd(rd(0x0080239c) + 0x1da70c)
        start = rd(0x00f79664, 'i')
        rel = lambda a: (lambda v: v - start if v else 0)(rd(a, 'i'))
        due = rd(0x00f79694, 'i')
        now = dict(busy=rd(0x00f7966c), slot=rd(0x00f79674), sample=rd(0x00f7967c), props=(rd(0x00f79684, 'i'), rd(0x00f79688, 'i')),
                   voiceDue=(due - start if due else 'none'), clipsEnd=rel(0x00f79668), cue=rel(0x00f79778), words=words(),
                   next=rd(0x00f79768), flags=hex(rd(0x00f79660)), channel0=rd(0x00f8692c), island=hex(rd(0x007cc4b4)))
    except (OSError, struct.error) as e:
        print(f'{time.time() - t0:7.2f} gone ({e})', flush=True); break
    if now != last:
        print(f'{time.time() - t0:7.2f} tick {tick} ' + ' '.join(f'{k}={v}' for k, v in now.items()), flush=True)
        last = now
    time.sleep(0.02)
