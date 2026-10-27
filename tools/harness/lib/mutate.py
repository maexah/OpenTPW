#!/usr/bin/env python3
"""mutate.py - put bugs back, one at a time, in copies of the tree and run the tests (CLAUDE.md rule 6), in parallel.

    mutate.py <mutations.py> [--copies N] [--only NAME ...] [--log FILE] [--keep] [--tree DIR] [--root DIR]

<mutations.py> defines  M = [(name, path, old, new), ...]  (path from the repo root; `old` must occur exactly once)
and may define  FILTER = "FullyQualifiedName~ParkFileWriter|..."  (the tests to run; all of them when absent).

What it does, in order, and stops at the first that fails:
  1. every `old` is looked for in the main tree: a pattern found 0 or 2+ times is reported and nothing is run;
  2. N copies of the tree are made under <root> (content/ goes with them: the build copies fonts);
  3. each copy is built and its tests run UNCHANGED: a copy that is not green proves nothing, so the run ends;
  4. the bugs are dealt round the copies; each is put in, built, tested, taken out.
A bug that does not compile is listed apart: it was never run, so it counts for nothing.
The copies (8 unless --copies, never more than there are bugs) are removed at the end unless --keep; a run that ends at
step 3 or is interrupted leaves them, and the next run uses them again. They are <root>/c0.. (--root, default $OPENTPW_MUTATE_ROOT or <work>/mutate),
and the end removes those c<N> folders alone (a --root holding anything else is refused): run one mutate.py at a time, or give each lane of a two-lane run its own
--root and --tree. Never point --tree at a tree a game run is using."""
import argparse, re, concurrent.futures as cf, os, shutil, subprocess, sys, threading, time
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "lib")); import paths

ap = argparse.ArgumentParser(); ap.add_argument("mutations"); ap.add_argument("--copies", type=int, default=8)
ap.add_argument("--only", nargs="*", default=[]); ap.add_argument("--log"); ap.add_argument("--keep", action="store_true")
ap.add_argument("--tree", default=paths.REPO)
ap.add_argument("--root", default=os.environ.get("OPENTPW_MUTATE_ROOT") or os.path.join(paths.WORK, "mutate")); a = ap.parse_args()
ns = {}; exec(open(a.mutations).read(), ns); M = [m for m in ns["M"] if not a.only or m[0] in a.only]; FILTER = ns.get("FILTER")
ROOT = os.path.expanduser(a.root)
if os.path.isdir(ROOT) and any(not re.fullmatch(r"c\d+", e) for e in os.listdir(ROOT)):
	sys.exit(f"--root {ROOT} holds more than c<N> copies: refused")
DN = paths.dotnet()
ENV = dict(os.environ, DOTNET_ROOT=paths.dotnet_env()['DOTNET_ROOT'], OPENTPW_GAME_PATH=paths.game())
out = open(a.log, "w") if a.log else None; lock = threading.Lock()
def say(text):
    with lock:
        print(text, flush=True)
        if out: out.write(text + "\n"); out.flush()

bad = [(n, open(os.path.join(a.tree, p)).read().count(old)) for n, p, old, new in M]
bad = [(n, c) for n, c in bad if c != 1]
if bad:
    for n, c in bad: say(f"{n}: PATTERN x{c}")
    sys.exit("fix the patterns first: nothing was run")
copies = min(a.copies, len(M))
if copies == 0: sys.exit("no mutations")

def build(tree): return subprocess.run([DN, "build", "source/OpenTPW.sln", "-m:2"], cwd=tree, env=ENV, capture_output=True, text=True)
def test(tree):
    # capped.sh: a mutant that runs away is killed alone at 6G, never the machine (2026-10-21 OOM).
    cmd = [os.path.join(paths.LIB, "capped.sh"), DN, "test", "source/OpenTPW.sln", "--no-build"] + (["--filter", FILTER] if FILTER else [])
    t = subprocess.run(cmd, cwd=tree, env=ENV, capture_output=True, text=True)
    failed = [l.strip().split()[1] for l in t.stdout.splitlines() if l.strip().startswith("Failed ") and "[" in l]
    if t.returncode == 137: failed = failed + ["<killed at the memory cap: the tests ran away>"]
    return failed, ("Passed!" in t.stdout or bool(failed))
def make(i):
    tree = os.path.join(ROOT, f"c{i}"); os.makedirs(tree, exist_ok=True)
    subprocess.run(["rsync", "-a", "--delete", "--exclude", ".git", "--exclude", "*.iso", "--exclude", "ReferenceScreenshots",
                    "--exclude", "/shots", "--exclude", ".claude", a.tree + "/", tree + "/"], check=True)
    b = build(tree)
    if " error " in b.stdout or "0 Error(s)" not in b.stdout: return tree, "the unchanged copy does not build"
    failed, ran = test(tree)
    if failed or not ran: return tree, f"the unchanged copy's tests are not green ({', '.join(failed[:3]) or 'none ran'})"
    return tree, None

t0 = time.time(); say(f"{len(M)} bugs, {copies} copies of {a.tree}" + (f", tests {FILTER}" if FILTER else ", every test"))
with cf.ThreadPoolExecutor(copies) as ex: made = list(ex.map(make, range(copies)))
for tree, why in made:
    if why: say(f"{tree}: {why}"); sys.exit("the run is void: nothing was put back")
say(f"every copy builds and is green unchanged ({time.time() - t0:.0f} s)")

results = {}
def work(i):
    tree = made[i][0]
    for name, path, old, new in M[i::copies]:
        p = os.path.join(tree, path); src = open(p).read(); open(p, "w").write(src.replace(old, new))
        try:
            b = build(tree)
            if " error " in b.stdout: results[name] = "does not compile"
            else:
                failed, ran = test(tree); results[name] = ("FAILS " + ", ".join(failed[:4])) if failed else ("SURVIVES" if ran else "tests did not run")
        finally:
            open(p, "w").write(src)
        say(f"{name}: {results[name]}")
with cf.ThreadPoolExecutor(copies) as ex: list(ex.map(work, range(copies)))

fails = [n for n, r in results.items() if r.startswith("FAILS")]; lives = [n for n, r in results.items() if r == "SURVIVES"]
other = [n for n, r in results.items() if n not in fails and n not in lives]
say(f"== {len(M)} put back: {len(fails)} fail a test, {len(lives)} survive, {len(other)} never ran; {time.time() - t0:.0f} s")
for n in lives: say(f"   SURVIVES {n}")
for n in other: say(f"   {results[n].upper()} {n}")
if not a.keep:
	for e in os.listdir(ROOT):
		if re.fullmatch(r"c\d+", e): shutil.rmtree(os.path.join(ROOT, e), ignore_errors=True)
	if not os.listdir(ROOT): os.rmdir(ROOT)
