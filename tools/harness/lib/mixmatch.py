#!/usr/bin/env python3
"""mixmatch.py - one sample measured in OpenTPW's own mix (parkrun's AUDIO_FILE: f32le stereo 22050).
  import mixmatch; name, t = mixmatch.template(sdt_path, index)  the bank's entry (read as MP3) decoded to 22050 mono (ffmpeg), with its name
  mixmatch.pitched(t, pitch)                                     the template as the game plays it at a pitch in 96ths of an octave
        (ParkCarSounds.Rate: 2^((p + sign p) / 96), 1 at nought): a voice drawn off pitch nought matches only so (Q612)
  mixmatch.window(capture, byte_from, byte_to)                   the capture between two sizes, as mono
  mixmatch.match(mono, t, secs=1.5) -> (score, gain, at_seconds, rms)   best normalised match of the template's first
        `secs`, its least-squares gain there, and the window's rms. A real hit scores 0.5 to 0.9; gain ratios between
        windows are the steady reading (verifying-audio-by-capture).
  mixmatch.steady(mono, t, step=2.0, piece=0.3) -> (gain, steps)   a LOOPED sample under other sounds: 0.3 s pieces of the template (one from every 0.4 s of it, the best kept) matched
        in 2 s steps; the median gain of the steps scoring over 0.8 (None with none), and every step's (score, gain).
        A short piece still matches in the gaps between other sounds, where the whole template does not.
  mixmatch.py <capture> <sdt> <index> [from to]                  prints one window's match (the two byte marks together: one alone is
                                                                 ignored and the whole capture is matched). Needs numpy, and ffmpeg on PATH.
Take a window's byte marks with os.path.getsize(AUDIO_FILE) either side of a sleep."""
import numpy as np, struct, subprocess, sys
R = 22050
def entries(path):
    b = open(path, 'rb').read(); n = struct.unpack_from('<I', b, 0)[0]; out = []
    for o in struct.unpack_from('<%dI' % n, b, 4):
        hs, ds = struct.unpack_from('<II', b, o); out.append((b[o + 8:o + 24].split(b'\0')[0].decode('latin1'), b[o + hs:o + hs + ds]))
    return out
def template(sdt, index):
    name, data = entries(sdt)[index]
    raw = subprocess.run(['ffmpeg', '-v', 'error', '-f', 'mp3', '-i', '-', '-ac', '1', '-ar', str(R), '-f', 'f32le', '-'], input=data, capture_output=True).stdout
    return name, np.frombuffer(raw, dtype='<f4').astype(np.float64)
def pitched(t, pitch):
    r = 1.0 if pitch == 0 else 2.0 ** ((pitch + (1 if pitch > 0 else -1)) / 96.0); n = int(len(t) / r)
    return np.interp(np.arange(n) * r, np.arange(len(t)), t)
def window(capture, a, b):
    a -= a % 8; x = np.fromfile(capture, dtype='<f4', offset=a, count=max(b - a, 0) // 4)
    return x[:len(x) - len(x) % 2].reshape(-1, 2).mean(axis=1).astype(np.float64)
def match(cap, t, secs=1.5):
    x = t[:int(R * secs)]; n = len(x); N = len(cap); rms = float(np.sqrt((cap * cap).mean())) if N else 0.0
    if N <= n: return 0.0, 0.0, 0.0, rms
    L = 1 << int(np.ceil(np.log2(N + n))); cc = np.fft.irfft(np.fft.rfft(cap, L) * np.conj(np.fft.rfft(x, L)), L)[:N - n]
    c2 = np.concatenate(([0.0], np.cumsum(cap * cap))); win = c2[n:N] - c2[:N - n]; xx = (x * x).sum()
    s = np.where(win > n * 1e-12, cc / np.sqrt(np.maximum(win, 1e-20) * xx), 0.0); i = int(s.argmax())
    return float(s[i]), float(cc[i] / xx), i / R, rms
def steady(cap, t, step=2.0, piece=0.3):
    steps = []
    for a in range(0, max(len(cap) - int(R * step) + 1, 1), int(R * step)):
        seg = cap[a:a + int(R * step)]
        steps.append(max((match(seg, t[int(o * R):], piece)[:2] for o in np.arange(0.0, max(len(t) / R - piece, 0.1), 0.4)), key=lambda r: r[0]))
    good = sorted(g for s_, g in steps if s_ > 0.8)
    return (good[len(good) // 2] if good else None), steps
if __name__ == '__main__':
    name, t = template(sys.argv[2], int(sys.argv[3])); import os
    a, b = (int(sys.argv[4]), int(sys.argv[5])) if len(sys.argv) > 5 else (0, os.path.getsize(sys.argv[1]))
    print(name, 'score %.2f gain %.4f at %.2f s, window rms %.5f' % match(window(sys.argv[1], a, b), t))
