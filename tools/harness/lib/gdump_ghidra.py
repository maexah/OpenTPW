# gdump_ghidra.py - runs INSIDE Ghidra's run_python (the real project's mcp__ghidra__run_python, or the private copy's
# `gd run_python`); gdump.sh builds the call. ARGS = {"out": dir, "addrs": ["0x004fe1a0", ...]} is set before exec.
# For each address: the function holding it gets <entry>.asm (its disassembly, calls named), <entry>.c (the
# decompile) and <entry>.xrefs (who refers to its entry: address, the function there, the kind); an address in no
# function gets loose_<addr>.asm (20 instructions before, 60 after). index.txt lists address -> file. A file already
# there is kept (delete it to dump again). One call in place of one per function per kind.
import os, time
OUT = ARGS["out"]; os.makedirs(OUT, exist_ok=True)
sp = program.getAddressFactory().getDefaultAddressSpace()
def fn_name(a):
    g = functions.getFunctionContaining(a)
    return g.getName() if g is not None else "?"
def line(ins):
    s = "%s  %s" % (ins.getAddress(), ins)
    if ins.getMnemonicString() == "CALL":
        fl = ins.getFlows()
        if fl is not None and len(fl) > 0:
            g = functions.getFunctionAt(fl[0])
            if g is not None: s += "    ; " + g.getName()
    return s
index = []; t0 = time.time(); dumped = 0
for a in ARGS["addrs"]:
    ad = sp.getAddress(int(a, 16))
    f = functions.getFunctionContaining(ad)
    if f is None:
        p = OUT + "/loose_%08x.asm" % int(a, 16); index.append("%s loose %s" % (a, os.path.basename(p)))
        if os.path.exists(p): continue
        ins = listing.getInstructionContaining(ad); L = []
        if ins is None: L.append("# no instruction at %s (undefined bytes or data)" % a)
        else:
            b = ins; n = 0
            while n < 20:
                pb = listing.getInstructionBefore(b.getAddress())
                if pb is None or b.getAddress().subtract(pb.getAddress()) > 16: break
                b = pb; n += 1
            k = 0
            while b is not None and k < 80:
                L.append(("> " if b.getAddress() == ins.getAddress() else "  ") + line(b)); b = b.getNext(); k += 1
        open(p, "w").write("\n".join(L) + "\n"); dumped += 1; continue
    e = str(f.getEntryPoint()); base = OUT + "/" + e
    index.append("%s %s %s" % (a, f.getName(), e))
    if not os.path.exists(base + ".asm"):
        L = []; it = listing.getInstructions(f.getBody(), True)
        while it.hasNext(): L.append(line(it.next()))
        open(base + ".asm", "w").write("# %s %s\n" % (e, f.getName()) + "\n".join(L) + "\n")
    if not os.path.exists(base + ".c"):
        try: c = decompile(f)
        except Exception as x: c = "/* decompile failed: %s */" % x
        open(base + ".c", "w").write(str(c))
    if not os.path.exists(base + ".xrefs"):
        X = []
        for r in references.getReferencesTo(f.getEntryPoint()):
            fa = r.getFromAddress(); X.append("%s  %s  %s" % (fa, fn_name(fa), r.getReferenceType()))
        open(base + ".xrefs", "w").write("# who refers to %s %s\n" % (e, f.getName()) + "\n".join(X) + "\n")
    dumped += 1
open(OUT + "/index.txt", "a").write("\n".join(index) + "\n")
print("gdump: %d addresses, %d dumped now, %.1f s, in %s (index.txt)" % (len(ARGS["addrs"]), dumped, time.time() - t0, OUT))
