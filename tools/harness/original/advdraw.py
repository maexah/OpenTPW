#!/usr/bin/env python3
"""advdraw.py PID [SECONDS] [INTERVAL] - is the running ORIGINAL drawing its advisor? Read-only, a line a change
(a POLL for loadfile.sh; SECONDS default 60, INTERVAL default 0.05).

Each line: seconds, mGameTick, gui_CameraFlags [0x00790ab0], g_Advisor_Busy [0x00f7966c], the speaking slot
[0x00f79674], the one view's flags
([[0x0087a6d0]+0x48], 0x20 draws render lists 1-6), and for advisor slots 0 and 1 (records 0x00790398 + slot*0xb0,
the model at +4): the model's +4 flags, its node word (bit 1 in a render list, 0x10 list 1, 8 list 2), whether its
renderable (node +0x1c, its virtual base the list entry) is in render list 1 ([[0x0087a6d0]+0x14]), and the frame
pair it was last drawn on (renderable +0x90/+0x94, -1 until drawn, stamped by its draw FUN_0057a750). A slot drawn within the last two frames reads `drawn`
(the first counts frames, FUN_00583790; the second the views drawn in the frame, reset each frame).
The line ends with the wall clock (record.sh's films are timed by it). docs/exe/ui.md, "The advisor on screen"; docs/exe/park-engine.md, "The End feature's view"."""
import struct, sys, time

FLAGS, BUSY, SLOT, F0, F1, MGR, REC = 0x00790ab0, 0x00f7966c, 0x00f79674, 0x008bcc40, 0x008bcc44, 0x0087a6d0, 0x0079039c

def main():
    pid = int(sys.argv[1]); secs = float(sys.argv[2]) if len(sys.argv) > 2 else 60
    step = float(sys.argv[3]) if len(sys.argv) > 3 else 0.05
    f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
    def rd(a, fmt='I'):
        f.seek(a); return struct.unpack('<' + fmt, f.read(struct.calcsize('<' + fmt)))[0]
    def in_list(head, obj):
        n, guard = head, 0
        while n and guard < 4096:
            if rd(n) == obj: return True
            n, guard = rd(n + 4), guard + 1
        return False
    t0, last = time.time(), None
    while time.time() - t0 < secs:
        try:
            g = rd(0x0080239c); tick = rd(g + 0x1da70c) if g else -1
            mgr = rd(MGR); frame = (rd(F0), rd(F1))
            parts = [f'flags 0x{rd(FLAGS):04x} busy {rd(BUSY)} slot {rd(SLOT)} view 0x{rd(mgr + 0x48):x}']
            for s in (0, 1):
                m = rd(REC + s * 0xb0)
                if not m:
                    parts.append(f's{s} -'); continue
                node = rd(m + 8); word = rd(node); r = rd(node + 0x1c)
                vb = r + rd(rd(r) + 4) if r else 0
                stamp = (rd(r + 0x90), rd(r + 0x94)) if r else (0xffffffff, 0)
                drawn = 'drawn' if 0 <= frame[0] - stamp[0] <= 2 else ('never' if stamp[0] == 0xffffffff else 'stale')
                lst = in_list(rd(mgr + 0x14), vb) if vb else False
                parts.append(f's{s} m+4 0x{rd(m + 4):x} node 0x{word:x} list1 {int(lst)} {drawn}')
            row = '  '.join(parts)
        except (OSError, struct.error) as e:
            print(f'{time.time() - t0:7.2f}s  read failed: {e}', flush=True); time.sleep(step); continue
        if row != last:
            print(f'{time.time() - t0:7.2f}s tick {tick}  {row}  wall {time.time():.2f}', flush=True); last = row
        time.sleep(step)

main()
