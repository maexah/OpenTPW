#!/usr/bin/env python3
"""capmatch.py - one sample of a bank measured in the ORIGINAL's own mix, between byte marks of its capture (Q442, from
Q439's match.py). The capture is what an ORIG_ASOUND file device writes (f32 stereo 48 kHz under a WAV header saying
s32, unpaced: its file's time axis, not the wall clock's); mark it with `stat -c %s` either side of what is measured.

  capmatch.py <capture.wav> <bank.sdt> <entry> <from> <to> [<from> <to> ...]
  capmatch.py <capture.wav> <bank.sdt> <entry> --scan <from> [<step seconds>]   every window of <step> (default 2 s) from
                                                                                 <from> to the end: the quiet stretch's
                                                                                 best score to beat

Each window prints the entry's name, its best score (0..1), the least-squares gain there, where in the window, and the
window's rms (lib/mixmatch.py's match). The music masks a short clip (Q439: 0.32 under it): switch it off first."""
import os, sys, subprocess, numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths; import mixmatch
R = 48000
cap, sdt, entry = sys.argv[1], sys.argv[2], int(sys.argv[3])
name, data = mixmatch.entries(sdt)[entry]
t = np.frombuffer(subprocess.run(['ffmpeg', '-v', 'error', '-f', 'mp3', '-i', '-', '-ac', '1', '-ar', str(R), '-f', 'f32le', '-'],
    input=data, capture_output=True).stdout, dtype='<f4').astype(np.float64)
mixmatch.R = R

def window(a, b):
    a -= a % 8; x = np.fromfile(cap, dtype='<f4', offset=a, count=(b - a) // 4)
    x = x[:len(x) - len(x) % 2].reshape(-1, 2).mean(axis=1).astype(np.float64)
    s, g, at, rms = mixmatch.match(x, t, 0.6)
    print(f'{name} {a}..{b} ({(b - a) / 8 / R:.2f} s of file): score {s:.3f} gain {g:.4f} at {at:.2f} s, rms {rms:.5f}')
    return s

if sys.argv[4] == '--scan':
    a = int(sys.argv[5]); step = int(float(sys.argv[6]) * R) * 8 if len(sys.argv) > 6 else 2 * R * 8
    end = os.path.getsize(cap); best = 0.0
    while a + step <= end: best = max(best, window(a, a + step)); a += step
    print(f'best {best:.3f}')
else:
    m = list(map(int, sys.argv[4:]))
    for a, b in zip(m[0::2], m[1::2]): window(a, b)
