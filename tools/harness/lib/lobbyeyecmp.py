#!/usr/bin/env python3
"""lobbyeyecmp.py (Q749, run in the folder holding after/eye.txt and orig/a-eye.txt, orig/a-click.t): ours' first lobby seconds after the quit (after/eye.txt) against the original's (orig/a-eye.txt), by time since the island
camera's first pass (the original's first row with delta 5.0; ours' first row = the seed). Prints the eye of each every 1 s, the
distance, and the shift of ours' clock (0..1.5 s) that minimises the distance (the original's first pass took a 0.5 s loading delta)."""
import re, math, sys
def rows_ours(path):
    L=open(path).read().splitlines()[1:]; out=[]
    for l in L:
        p=l.split(None,2)
        if p[0]!='post': continue
        m=re.search(r'eye \(([^,]+), ([^,]+), ([^)]+)\) look \(([^,]+), ([^,]+), ([^)]+)\)',p[2]); out.append((float(p[1]),tuple(map(float,m.groups()[:3])),tuple(map(float,m.groups()[3:]))))
    t0=out[0][0]; return [(t-t0,e,k) for t,e,k in out]
def rows_orig(path):
    out=[]; seen=False
    for l in open(path):
        if not l[:1].isdigit(): continue
        m=re.match(r'(\S+) \s*\S+ obj \S+ eye \(([^,]+), ([^,]+), ([^)]+)\) look \(([^,]+), ([^,]+), ([^)]+)\)',l)
        if not m or 'delta 5.0000' in l and not seen: pass
        if not m: continue
        w=float(m[1]); e=tuple(float(m[i]) for i in (2,3,4)); k=tuple(float(m[i]) for i in (5,6,7))
        if abs(e[0])>1e6: continue
        out.append((w,e,k))
    return out
ours=rows_ours('after/eye.txt'); orig=rows_orig('orig/a-eye.txt')
click=float(open('orig/a-click.t').read())
orig=[r for r in orig if r[0]>click+0.4]            # the new render camera
first=[i for i,r in enumerate(orig) if abs(r[1][0]-500)>1][0]   # the first island pass
t1=orig[first][0]; orig=[(t-t1,e,k) for t,e,k in orig[first-1:]]
def at(rows,t):
    return min(rows,key=lambda r:abs(r[0]-t))
d=lambda a,b: math.dist(a,b)
print('t     ours eye                       original eye                   dist')
for t in (0,0.5,1,2,3,4,5,6,8):
    o=at(ours,t); g=at(orig,t); print(f'{t:3.1f}  {o[1][0]:8.2f} {o[1][1]:7.2f} {o[1][2]:8.2f}   {g[1][0]:8.2f} {g[1][1]:7.2f} {g[1][2]:8.2f}   {d(o[1],g[1]):6.2f}')
best=min(((sum(d(at(ours,t)[1],at(orig,t+s)[1]) for t in [x/10 for x in range(5,60)])/55),s) for s in [x/20 for x in range(0,31)])
print('best shift of the original ahead of ours: %.2f s, mean eye distance %.2f' % (best[1],best[0]))
print('no shift: mean eye distance %.2f' % (sum(d(at(ours,t)[1],at(orig,t)[1]) for t in [x/10 for x in range(5,60)])/55))
