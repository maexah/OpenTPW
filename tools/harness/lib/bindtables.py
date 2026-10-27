#!/usr/bin/env python3
"""lib/bindtables.py - every row of the original's six binding tables (FUN_0040cb80), read from testme.exe: key and
modifier, so the letter keys no table binds can be listed (Q631)."""
import struct
import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
d=open(paths.exe(),'rb').read()
pe=struct.unpack_from('<I',d,0x3c)[0]
n=struct.unpack_from('<H',d,pe+6)[0]; opt=struct.unpack_from('<H',d,pe+20)[0]
base=struct.unpack_from('<I',d,pe+24+28)[0]
secs=[struct.unpack_from('<8sIIII',d,pe+24+opt+40*i)[1:] for i in range(n)]
def rd(addr,k):
    rva=addr-base
    for vs,va,rs,ra in secs:
        if va<=rva<va+max(vs,rs): return d[ra+rva-va:ra+rva-va+k]
tables=[('system',0x747f38,12),('game',0x748028,15),('camera',0x748158,25),('cheat',0x7484d0,12),('coaster',0x748350,2),('shortcuts',0x748378,17)]
used={}
for name,a,cnt in tables:
    for i in range(cnt):
        act,key,mod,lat,nm,h,u=struct.unpack('<HHHHIII',rd(a+20*i,20))
        s=rd(nm,24).split(b'\0')[0].decode() if nm else ''
        ch=chr(key) if 0x20<key<0x7f else ''
        print(f"{name:9} {i:2} act {act:#04x} key {key:#06x} {ch!r:5} mod {mod:#04x} {s:12} {h:#x}")
        if key: used.setdefault((key,mod),[]).append(f"{name}{i}")
print("letters bound with no modifier:", ''.join(c for c in map(chr,range(65,91)) if (ord(c),0) in used))
print("letters free with no modifier:", ''.join(c for c in map(chr,range(65,91)) if (ord(c),0) not in used))
