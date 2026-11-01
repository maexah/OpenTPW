#!/usr/bin/env python3
"""headpause.py PID OUT ITEM ROLE:FRAME ... - the ORIGINAL's Mumbo paused by the Escape menu at the first moment its
first channel passes (role, frame - LEAD), then read twice (1.5 s and 2.5 s into the pause, which must agree), then resumed
by Escape. One JSON line per pause to OUT (and, with SHOTDIR set, a frame of the paused screen): role, clip, frame, each held head's (record, handle, place), and (Q940) its stored matrix, node flags, picture and roll under `pictures` and the view matrix under `view`. Q848 (a harness for loadfile.sh: `loadfile.sh load <file> <outdir> <tag>` with a file lib/headseries.py saved, then setcam.py on the ride; the
Escape menu pauses the original's clock and its channels). The camera must already be on the ride (setcam.py): a head's sprite place is written when it is drawn."""
import json, struct, subprocess, sys, time, os
pid = int(sys.argv[1]); out = open(sys.argv[2], 'a'); item = int(sys.argv[3]); targets = [tuple(float(x) for x in a.split(':')) for a in sys.argv[4:]]
LEAD = 6.0
f = open(f'/proc/{pid}/mem', 'rb', buffering=0)
def rd(a, fmt='<I'):
    f.seek(a); return struct.unpack(fmt, f.read(struct.calcsize(fmt)))[0]
def sprite(s):
    t, n = rd(0x007b49f0), rd(0x007b49f4)
    a = rd(t + 4 * s) if t and 0 < s < n else 0
    return [round(rd(a + o, '<f'), 4) for o in (0x88, 0x8c, 0x90)] if a else None
def mat(p):
    return [round(rd(p + 4 * i, '<f'), 5) for i in range(16)] if p else None
def extra(s, rec):
    """Q940: a head's stored matrix (record +4), the node flags (record +0), the picture its sprite shows (+0xb8, FUN_0044b510's local 0xd) and its roll (+0xa8), and the view matrix [0x790a98] the picture is chosen against."""
    t, n = rd(0x007b49f0), rd(0x007b49f4)
    a = rd(t + 4 * s) if t and 0 < s < n else 0
    return {'flags': rd(rec), 'matrix': mat(rd(rec + 4)), 'picture': rd(a + 0xb8, '<i') if a else None, 'roll': round(rd(a + 0xa8, '<f'), 5) if a else None}
def read():
    for slot in range(rd(0x007a4084) + rd(0x007a3ebc)):
        m = rd(0x007a4614 + 4 * slot)
        if not m or rd(m + 0xf8) != item: continue
        inst = rd(m + 8); mesh = rd(inst + 4); looks = rd(mesh + 0x48, '<H'); recs = rd(rd(inst + 0x28) + 4); ch = rd(m + 0x10)
        if not ch: continue
        f.seek(ch); w = struct.unpack('<16I', f.read(64))
        heads = [(r, rd(recs + 20 * r + 12, '<i')) for r in range(looks) if rd(recs + 20 * r) & 2]
        return {'flags': w[0], 'role': w[1], 'clip': w[2], 'length': struct.unpack('<f', struct.pack('<I', w[7]))[0],
                'frame': struct.unpack('<f', struct.pack('<I', w[8]))[0], 'heads': [(r, h, sprite(h)) for r, h in heads],
                'pictures': [(r, extra(h, recs + 20 * r)) for r, h in heads], 'view': mat(rd(0x00790a98))}
def key():
    env = dict(os.environ, DISPLAY=':77')
    subprocess.run(['xdotool', 'keydown', 'Escape'], env=env); time.sleep(0.15); subprocess.run(['xdotool', 'keyup', 'Escape'], env=env)
for role, frame in targets:
    t0 = time.time(); trig = False; last = None
    while time.time() - t0 < 60:
        s = read()
        if s and s['role'] == role and s['frame'] >= frame - LEAD and s['frame'] < frame + 3 and s['frame'] > (last or -1) - 1e-6:
            trig = True; break
        last = s['frame'] if s else None; time.sleep(0.02)
    if not trig: print('no trigger for', role, frame); continue
    key(); time.sleep(1.5); a = read()
    if os.environ.get('SHOTDIR'):
        subprocess.run(['import', '-window', 'root', '-display', ':77', '-crop', '1024x768+0+0', f"{os.environ['SHOTDIR']}/orig-role{int(role)}-frame{int(frame)}.png"])
    time.sleep(1.0); b = read(); key(); time.sleep(0.5)
    rec = {'want': [role, frame], 'a': a, 'b': b, 'agree': a == b}
    out.write(json.dumps(rec) + '\n'); out.flush(); print(json.dumps(rec)[:300], flush=True)
    time.sleep(1.5)
