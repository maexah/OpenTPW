# funcnames_ghidra.py - runs inside Ghidra's run_python: writes OUT (set before exec) as "entry<TAB>end<TAB>name" for
# every function. lib/funcnames.sh calls it on the private copy.
fs = functions.getFunctions(True); n = 0
with open(OUT, "w") as fh:
    for f in fs:
        b = f.getBody(); fh.write("%s\t%s\t%s\n" % (f.getEntryPoint(), b.getMaxAddress(), f.getName())); n += 1
print("funcnames: %d functions -> %s" % (n, OUT))
