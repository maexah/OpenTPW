# gclosure_ghidra.py - runs INSIDE Ghidra's run_python; gclosure.sh builds the call. ARGS = {"start": "0x00414d40",
# "watch": ["0x0087906c", "0x00587380", ...]} is set before exec.
# Walks the call graph from `start` through direct calls and jumps out of a function. Indirect calls are counted, not
# followed, so "not reached" means not by a direct call. A watched address that is a function's prints whether it was
# reached and by what chain; any other prints every reference to it, marked IN-CLOSURE where the referring function was reached.
sp = program.getAddressFactory().getDefaultAddressSpace()
refs = program.getReferenceManager()
def callees(f):
    out = set(); indirect = 0
    for ins in listing.getInstructions(f.getBody(), True):
        m = ins.getMnemonicString()
        if m != "CALL" and m != "JMP":
            continue
        fl = ins.getFlows()
        if fl is None or len(fl) == 0:
            if m == "CALL": indirect += 1
            continue
        g = functions.getFunctionContaining(fl[0])
        if g is not None and g != f:
            out.add(g)
    return out, indirect
start = functions.getFunctionContaining(sp.getAddress(int(ARGS["start"], 16)))
seen = {start: None}; queue = [start]; sites = 0; total = 0
while queue:
    f = queue.pop(0)
    cs, ind = callees(f)
    if ind: sites += 1; total += ind
    for g in cs:
        if g not in seen:
            seen[g] = f; queue.append(g)
print("from %s: %d functions by direct calls; %d indirect calls in %d functions not followed" % (start.getName(), len(seen), total, sites))
def chain(f):
    names = []
    while f is not None:
        names.append(f.getName()); f = seen[f]
    return " <- ".join(names)
for a in ARGS["watch"]:
    ad = sp.getAddress(int(a, 16)); g = functions.getFunctionAt(ad)
    if g is not None:
        print("%s %s: %s" % (a, g.getName(), chain(g) if g in seen else "not reached"))
        continue
    print("%s data:" % a)
    for r in refs.getReferencesTo(ad):
        h = functions.getFunctionContaining(r.getFromAddress())
        print("  %s %s %s%s" % (r.getFromAddress(), r.getReferenceType(), h.getName() if h else "?", " IN-CLOSURE" if h in seen else ""))
