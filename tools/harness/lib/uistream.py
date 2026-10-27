# Walks a compiled UI layout stream in testme.exe (docs/exe/hud.md, "The opcodes"). Usage: stream.py VA
import struct, sys
import os, sys; sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
exe = open(paths.exe(),'rb').read()
pe = struct.unpack_from('<I', exe, 0x3c)[0]
nsec = struct.unpack_from('<H', exe, pe+6)[0]
opt = struct.unpack_from('<H', exe, pe+20)[0]
base = struct.unpack_from('<I', exe, pe+24+28)[0]
secs = []
for i in range(nsec):
    o = pe+24+opt+40*i
    vs, va, rs, rp = struct.unpack_from('<IIII', exe, o+8)
    secs.append((va, max(vs,rs), rp))
def off(va):
    r = va-base
    for a,s,p in secs:
        if a <= r < a+s: return p+r-a
    raise SystemExit('no section')
va = int(sys.argv[1],16); p = off(va); start = p
def s16(): 
    global p; v = struct.unpack_from('<h', exe, p)[0]; p += 2; return v
def u32():
    global p; v = struct.unpack_from('<I', exe, p)[0]; p += 4; return v
def rect(): return tuple(s16() for _ in range(4))
depth = 0; stack=[]
while True:
    at = va + p - start
    op = s16(); ind = '  '*depth
    if op == 0:
        t = s16(); fl = u32(); i = u32(); r = rect()
        print(f'{at:08x} {ind}CONTROL type {t} flags {fl:#x} id {i:#x} rect {r}'); depth += 1; stack.append(t)
    elif op == 1: print(f'{at:08x} {ind}mesh {u32():#010x}')
    elif op == 2: print(f'{at:08x} {ind}skin {u32():#010x}')
    elif op == 3: print(f'{at:08x} {ind}textrect {rect()}')
    elif op == 4:
        sub = s16()
        if sub in (1,3): print(f'{at:08x} {ind}region sub {sub} rect {rect()}')
        elif sub == 2: print(f'{at:08x} {ind}region sub 2 {[s16() for _ in range(3)]}')
        elif sub == 4:
            n = s16(); pts = [(s16(), s16()) for _ in range(n)]
            print(f'{at:08x} {ind}region sub 4, {n} points at {va+p-start-4*n:08x}: {pts}')
        else: raise SystemExit(f'sub {sub}')
    elif op == 5:
        depth -= 1
        print(f'{at:08x} {"  "*depth}end')
        if depth <= 0 and not stack: break
        if stack: stack.pop()
        if depth < 0: break
    elif op in (6,7,8,0xe,0xf): print(f'{at:08x} {ind}op {op:#x} {rect()}'); depth += 1; stack.append(2)
    elif op in (0xc,0x10): n = s16(); print(f'{at:08x} {ind}op {op:#x} n {n} {rect()}'); depth += 1; stack.append(2)
    elif op == 0xd: print(f'{at:08x} {ind}op 0xd {s16()} {s16()}')
    elif op == 0xb: n = s16(); print(f'{at:08x} {ind}op 0xb {[ (s16(),s16()) for _ in range(n)]}')
    elif op == 0x11: print(f'{at:08x} {ind}help {u32()}')
    elif op == 0x12: print(f'{at:08x} {ind}id2 {u32():#x}')
    else: print(f'{at:08x} op {op:#x} ??'); break
    if p-start > 4000: break
