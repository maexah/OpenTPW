#!/usr/bin/env python3
"""citecheck.py <sha> [--repo DIR] [--all] - check every executable address a commit's added lines cite, with no
model call. For each address in the image: a code address (.text, cseg) must start an instruction in
lib/testme.objdump (else MID-INSTRUCTION or NO-INSTRUCTION); a data address must fall in .rdata or .data (else
OUTSIDE). Where the text names a function next to it ("`Name` (`0x...`)", "Name 0x...", FUN_xxxxxxxx), the address
must lie in a function of that name in lib/funcnames.tsv (refresh with lib/funcnames.sh after the private Ghidra copy
is refreshed); a name the copy does not have yet is NAME-NOT-IN-COPY, one that differs is NAME-MISMATCH. Prints only
the problems unless --all, then a count. Reads docs/, source/ and tools/ lines the commit adds."""
import re, subprocess, sys, os, bisect
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths
H = paths.WORK   # testme.objdump and funcnames.tsv (funcnames.sh) are made from your own testme.exe, never committed
args = [a for a in sys.argv[1:] if not a.startswith("--")]
repo = sys.argv[sys.argv.index("--repo") + 1] if "--repo" in sys.argv else paths.REPO
if "--repo" in sys.argv: args.remove(repo)
showall = "--all" in sys.argv
sha = args[0]
CODE = [(0x401000, 0x401000 + 0x2e5a7a), (0x6e7000, 0x6e7000 + 0x156b2)]
DATA = [(0x6fd000, 0x727000), (0x727000, 0xfb5000), (0xfb5000, 0xfc1000)]  # .rdata, .data with its bss, small sections
starts = set()
for l in open(H + "/testme.objdump"):
    if l[:2] == "  " and ":" in l[:10]:
        try: starts.add(int(l.split(":")[0], 16))
        except ValueError: pass
F = []
for l in open(H + "/funcnames.tsv"):
    e, end, name = l.rstrip("\n").split("\t"); F.append((int(e, 16), int(end, 16), name))
F.sort(); FE = [f[0] for f in F]; byname = {}
for e, end, n in F: byname.setdefault(n, []).append((e, end))
def func_of(a):
    i = bisect.bisect_right(FE, a) - 1
    return F[i] if i >= 0 and F[i][0] <= a <= F[i][1] else None
diff = subprocess.run(["git", "-C", repo, "show", "--format=", "-U0", sha, "--", "docs", "source", "tools"],
	capture_output=True, text=True).stdout
ADDR = re.compile(r"(?:`?([A-Za-z][A-Za-z0-9]*_[A-Za-z0-9_]+)`?\s*\(?\s*)?`?0x0*([0-9a-fA-F]{6,8})\b")
FUNREF = re.compile(r"\bFUN_([0-9a-fA-F]{8})\b")
path = None; ln = 0; n = 0; bad = 0; rows = []
for l in diff.split("\n"):
	if l.startswith("+++ "): path = l[6:]; continue
	if l.startswith("@@"):
		ln = int(re.search(r"\+(\d+)", l).group(1)) - 1; continue
	if not l.startswith("+") or l.startswith("+++"): continue
	ln += 1; text = l[1:]
	found = [(m.group(1), int(m.group(2), 16)) for m in ADDR.finditer(text)]
	found += [("FUN_" + m.group(1).lower(), int(m.group(1), 16)) for m in FUNREF.finditer(text)]
	for name, a in found:
		if not (0x401000 <= a < 0xfc6000): continue
		n += 1; verdict = "OK"; note = ""
		if any(lo <= a < hi for lo, hi in CODE):
			if a not in starts:
				f = func_of(a); verdict = "MID-INSTRUCTION" if f else "NO-INSTRUCTION"
			if name and verdict == "OK":
				f = func_of(a)
				if name.startswith("FUN_"):
					if not f or f[0] != int(name[4:], 16) and not (f[0] <= int(name[4:], 16) <= f[1]): verdict = "NAME-MISMATCH"; note = "in %s" % (f[2] if f else "no function")
				elif name in byname:
					if not any(lo <= a <= hi for lo, hi in byname[name]): verdict = "NAME-MISMATCH"; note = "in %s" % (f[2] if f else "no function")
				elif re.match(r"^[A-Z][A-Za-z0-9]*_[A-Z][A-Za-z0-9]*$", name):
					verdict = "NAME-NOT-IN-COPY"; note = "the copy has %s" % (f[2] if f else "no function")
		elif any(lo <= a < hi for lo, hi in DATA): verdict = "OK"
		else: verdict = "OUTSIDE"
		rows.append([verdict, path, ln, name, a, note])
# objdump's straight sweep loses step after data inside code: ask Ghidra (the private copy) about every doubted code
# address in ONE call, and keep only what Ghidra also has no instruction start for.
doubt = sorted({r[4] for r in rows if r[0] in ("MID-INSTRUCTION", "NO-INSTRUCTION")})
if doubt:
	code = ("res=[]\nfor v in %s:\n    a=program.getAddressFactory().getDefaultAddressSpace().getAddress(v)\n"
		"    i=listing.getInstructionAt(a)\n    res.append('%%x %%d' %% (v, 1 if i is not None else 0))\nprint(' '.join(res))") % doubt
	out = subprocess.run([paths.need("OPENTPW_GHIDRA_CLIENT"), "run_python", "code=" + code],
		capture_output=True, text=True).stdout
	ok = {int(t.split()[0], 16) for t in re.findall(r"[0-9a-f]+ 1", out)}
	for r in rows:
		if r[0] in ("MID-INSTRUCTION", "NO-INSTRUCTION") and r[4] in ok: r[0] = "OK"; r[5] = "(Ghidra: an instruction starts here)"
for verdict, path, ln, name, a, note in rows:
	if verdict != "OK": bad += 1
	if verdict != "OK" or showall:
		print("%-17s %s:%d  %s0x%08x  %s" % (verdict, path, ln, (name + " ") if name else "", a, note))
print("citecheck %s: %d addresses, %d to look at" % (sha, n, bad))
