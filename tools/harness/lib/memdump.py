"""memdump.py (import): `dump( pid, outdir, keep=False )` collects a `dotnet-dump` heap dump of a running game and
writes the output of the `DUMPCMD` commands (`|` between them, default `dumpheap -stat`) to `DUMPOUT` (default
heapstat.txt) in `outdir`; the .dmp is removed unless `keep`, else its path is returned. The tool is `OPENTPW_DOTNET_DUMP`
(default `~/.cache/dn-tmp/tools/dotnet-dump`); the dump goes under `TMPDIR`, else `~/.cache/dn-tmp` (never `/tmp`, which
is RAM). `gcroot` needs an object address, so keep the dump and run `dotnet-dump analyze <file>` by hand after. Used by
`memrun.py` and `roundtrip.py` (`DUMP=1`; Q832, Q833)."""
import os, subprocess
import paths

def dump(pid, outdir, keep=False):
    tool = os.environ.get("OPENTPW_DOTNET_DUMP", os.path.expanduser("~/.cache/dn-tmp/tools/dotnet-dump"))
    env = dict(os.environ, DOTNET_ROOT=paths.dotnet_env()["DOTNET_ROOT"])
    folder = os.environ.get("TMPDIR", os.path.expanduser("~/.cache/dn-tmp"))
    file = os.path.join(folder, f"memrun-{pid}.dmp")
    r = subprocess.run([tool, "collect", "-p", str(pid), "--type", "Heap", "-o", file], capture_output=True, text=True, env=env)
    print("dump:", (r.stdout + r.stderr)[-200:].strip())
    cmds = os.environ.get("DUMPCMD", "dumpheap -stat").split("|")
    r = subprocess.run([tool, "analyze", file, *sum([["-c", c] for c in cmds], []), "-c", "exit"], capture_output=True, text=True, env=env)
    open(os.path.join(outdir, os.environ.get("DUMPOUT", "heapstat.txt")), "w").write(r.stdout + r.stderr)
    if keep: return file
    os.remove(file)
