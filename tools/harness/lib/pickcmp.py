"""pickcmp.py OURS_pick-eye.txt ORIG_eye.txt [EX EY EZ] - the two games' eye/look after a pick from the same held flight state, on a clock from each one's first moved row."""
import sys, re, math
EYE = re.compile(r"eye \(([-\d.]+), ([-\d.]+), ([-\d.]+)\) look \(([-\d.]+), ([-\d.]+), ([-\d.]+)\) placed=(\w+) orbit=([-\d.]+) time=([-\d.]+)")
def load(f, cmd="eye"):
    out = []; hdr = None
    for l in open(f):
        p = l.split(" ", 3)
        if len(p) < 4 or not p[1].replace(".", "").isdigit(): hdr = l.strip(); continue
        tag, t, c, r = p[0], float(p[1]), p[2], p[3].strip()
        if c != cmd: continue
        m = EYE.search(r)
        if not m: continue
        v = list(map(float, m.groups()[:6])) + [m.group(7)] + list(map(float, m.groups()[7:]))
        out.append((tag, t, v))
    return hdr, out
def _pitch5(v): return math.degrees(math.atan2(v[1] - v[4], math.hypot(v[0] - v[3], v[2] - v[5])))
def d3(a, b): return math.dist(a[:3], b[:3])

def pitch(e, l): return math.degrees(math.atan2(e[1]-l[1], math.hypot(e[0]-l[0], e[2]-l[2])))
hdr, rows = load(sys.argv[1]); ours = [(r[1], r[2][:3], r[2][3:6]) for r in rows]
orig = []
for l in open(sys.argv[2]):
    m = re.match(r"(\d+\.\d+)\s+([\d.]+)s obj 0x\w+ eye \(([-\d.e+]+), ([-\d.e+]+), ([-\d.e+]+)\) look \(([-\d.e+]+), ([-\d.e+]+), ([-\d.e+]+)\)", l)
    if m: orig.append((float(m[1]), [float(m[i]) for i in (3,4,5)], [float(m[i]) for i in (6,7,8)]))
def first_move(rs, start):
    # the held start state: last row at/near start; first row whose eye is 1.0 away from it
    for k, (t, e, l) in enumerate(rs):
        if t > 0 and math.dist(e, start) > 1.0 and math.dist(e, start) < 40 and k > 0 and math.dist(rs[k-1][1], start) < 1.0: return k
    return None
S = [float(x) for x in sys.argv[3:6]] if len(sys.argv) > 5 else [479.466, 86.613, 363.595]
ko, kg = first_move(ours, S), first_move(orig, S)
to, tg = ours[ko][0], orig[kg][0]
def at(rs, t0, s):
    t = t0 + s; k = min(range(len(rs)), key=lambda i: abs(rs[i][0]-t)); return rs[k]
print("first moved row: ours eye", [round(x,1) for x in ours[ko][1]], "orig eye", [round(x,1) for x in orig[kg][1]])
print(" t(s)  ours pitch  orig pitch   eye gap   look gap")
mx = {}
for name, rs, t0 in (("ours", ours, to), ("orig", orig, tg)):
    pr = [(r[0]-t0, pitch(r[1], r[2])) for r in rs if -0.0 <= r[0]-t0 <= 4.0]
    pk = max(pr, key=lambda x: x[1]); ov = [t for t, p in pr if p > 45]
    print(f"{name}: peak {pk[1]:.1f} at +{pk[0]:.2f} s; over 45 deg {min(ov):.2f}..{max(ov):.2f} s (span {max(ov)-min(ov):.2f})")
worst_e = worst_l = 0
for s in [i/10 for i in range(0, 41, 1)]:
    a = at(ours, to, s); b = at(orig, tg, s)
    ge = math.dist(a[1], b[1]); gl = math.dist(a[2], b[2]); worst_e = max(worst_e, ge) if s <= 1.0 else worst_e; worst_l = max(worst_l, gl) if s <= 1.0 else worst_l
    if s <= 1.5 or round(s*10) % 5 == 0:
        print(f"{s:4.1f}  {pitch(a[1],a[2]):8.1f}  {pitch(b[1],b[2]):8.1f}  {ge:8.1f}  {gl:8.1f}")
print(f"worst eye gap in the first second {worst_e:.1f} units, look gap {worst_l:.1f}")
# a clock offset for the original (its rows are one 33 ms frame apart and its first moved row is the frame's END): the best shift in [-0.1, 0.1]
best = None
for sh in [x/1000 for x in range(-100, 101, 5)]:
    g = sum(math.dist(at(ours, to, s)[1], at(orig, tg + sh, s)[1]) for s in [i/20 for i in range(0, 41)])/41
    if best is None or g < best[0]: best = (g, sh)
sh = best[1]; print(f"best clock shift of the original: {sh*1000:+.0f} ms (mean eye gap {best[0]:.2f} over 0..2 s)")
we = wl = 0
for s in [i/10 for i in range(0, 11)]:
    a = at(ours, to, s); b = at(orig, tg + sh, s); we = max(we, math.dist(a[1], b[1])); wl = max(wl, math.dist(a[2], b[2]))
    print(f"{s:4.1f}  ours {pitch(a[1],a[2]):5.1f}  orig {pitch(b[1],b[2]):5.1f}  eye gap {math.dist(a[1],b[1]):5.1f}  look gap {math.dist(a[2],b[2]):5.1f}")
print(f"with the shift: worst eye gap in the first second {we:.1f}, look gap {wl:.1f}")
