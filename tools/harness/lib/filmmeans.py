"""filmmeans.py FILMDIR (Q1037): the grey mean of every picture of FILMDIR/film.mkv (1024 x 768), one row a frame: index, seconds, mean; a frame whose mean jumps is a flash.
Also writes FILMDIR/gray.pkl (the means and every 4th pixel of every frame)."""
import subprocess, sys, numpy as np, pickle
d = sys.argv[1]; W, H = 1024, 768
p = subprocess.Popen(['ffmpeg','-loglevel','error','-i',f'{d}/film.mkv','-f','rawvideo','-pix_fmt','gray','-'], stdout=subprocess.PIPE)
means = []; prev = None; diffs = []
small = []
while True:
    b = p.stdout.read(W*H)
    if len(b) < W*H: break
    a = np.frombuffer(b, np.uint8).reshape(H, W).astype(np.float32)
    means.append(float(a.mean()))
    small.append(a[::4, ::4].copy())
pickle.dump((means, np.array(small)), open(f'{d}/gray.pkl', 'wb'))
for i, m in enumerate(means): print(i, round(i/30,3), round(m,1))
