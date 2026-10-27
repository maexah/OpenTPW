#!/usr/bin/env python3
"""original/camrate.py LOG FIELD - the rate a campoi.py log's FIELD (zoom, rot) moves while it moves: a least-squares line
through the polls strictly between its first and last change, the first and last 0.1 s of that dropped; also the
point's largest move from its first poll."""
import sys
rows=[]
for l in open(sys.argv[1]):
    w=l.split()
    if len(w)>4 and w[1]=='poi': rows.append((float(w[0]),float(w[2]),float(w[3]),float(w[w.index(sys.argv[2])+1])))
v=[r[3] for r in rows]
ch=[i for i in range(1,len(v)) if v[i]!=v[i-1]]
print(f"{len(rows)} polls, {sys.argv[2]} {v[0]:.2f} -> {v[-1]:.2f}, point moved at most {max(abs(r[1]-rows[0][1])+abs(r[2]-rows[0][2]) for r in rows):.4f}")
if ch:
    t0,t1=rows[ch[0]][0]+0.1,rows[ch[-1]-1][0]-0.1
    s=[(r[0],r[3]) for r in rows if t0<=r[0]<=t1]
    if len(s)>2:
        n=len(s); mx=sum(a for a,_ in s)/n; my=sum(b for _,b in s)/n
        k=sum((a-mx)*(b-my) for a,b in s)/sum((a-mx)**2 for a,_ in s)
        print(f"moving {rows[ch[0]][0]:.3f} to {rows[ch[-1]][0]:.3f} s; slope {k/1000:+.5f} a ms over {n} polls")
    else: print(f"changed at {[round(rows[i][0],3) for i in ch]} s: {[v[i] for i in ch]}")
