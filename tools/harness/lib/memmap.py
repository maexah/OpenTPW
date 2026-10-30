"""A running process's memory, from /proc: where its resident bytes sit. Importable.

    import memmap
    memmap.rollup( pid )        # {"Rss": kB, "Pss": kB, "Anonymous": kB, ...} from smaps_rollup
    memmap.status( pid )        # {"VmRSS": kB, "VmHWM": kB, "RssAnon": kB, "RssFile": kB, "RssShmem": kB, ...}
    memmap.by_mapping( pid )    # [(name, Rss kB, Private_Dirty kB, count)] largest first, mappings grouped by backing file / kind
    memmap.drm( pid )           # {"vram": kB, "gtt": kB, ...} the DRM fdinfo's video memory for the process (amdgpu)
    memmap.report( pid, tag )   # one printed block of all of them (what Q954's runs print at each stage)

Read-only. A mapping with no file name is grouped as "[anon]"; the .NET runtime and the Vulkan driver both keep most of
their memory there, so `by_mapping` alone cannot tell them apart: pair it with the game's own `mem` console line (managed
heap, the textures and meshes it uploaded)."""
import re


def status( pid ):
	out = {}
	for line in open( f"/proc/{pid}/status" ):
		m = re.match( r"(Vm\w+|Rss\w+):\s+(\d+) kB", line )
		if m:
			out[m.group( 1 )] = int( m.group( 2 ) )
	return out


def rollup( pid ):
	out = {}
	for line in open( f"/proc/{pid}/smaps_rollup" ):
		m = re.match( r"(\w+):\s+(\d+) kB", line )
		if m:
			out[m.group( 1 )] = int( m.group( 2 ) )
	return out


def by_mapping( pid ):
	groups = {}
	name = None
	for line in open( f"/proc/{pid}/smaps" ):
		head = re.match( r"^[0-9a-f]+-[0-9a-f]+ \S+ \S+ \S+ \S+\s*(.*)$", line )
		if head:
			name = head.group( 1 ).strip() or "[anon]"
			if name.startswith( "/memfd:" ) or name.startswith( "/dev/shm" ):
				name = name.split( " (" )[0]
			continue
		m = re.match( r"(Rss|Private_Dirty):\s+(\d+) kB", line )
		if m and name is not None:
			g = groups.setdefault( name, [0, 0, 0] )
			if m.group( 1 ) == "Rss":
				g[0] += int( m.group( 2 ) )
				g[2] += 1
			else:
				g[1] += int( m.group( 2 ) )
	rows = [(n, g[0], g[1], g[2]) for n, g in groups.items()]
	return sorted( rows, key=lambda r: -r[1] )


def drm( pid ):
	"""The video memory the kernel's DRM fdinfo says this process holds: {"vram": kB, "gtt": kB, "cpu": kB} summed over its render fds
	(amdgpu; empty where the driver prints none, as lavapipe's, whose device memory is ordinary host memory in Rss)."""
	import os
	seen, out = set(), {}
	try:
		fds = os.listdir( f"/proc/{pid}/fd" )
	except OSError:
		return out
	for fd in fds:
		try:
			if not os.readlink( f"/proc/{pid}/fd/{fd}" ).startswith( "/dev/dri/" ):
				continue
			info = open( f"/proc/{pid}/fdinfo/{fd}" ).read()
		except OSError:
			continue
		cid = re.search( r"drm-client-id:\s+(\d+)", info )
		if cid and cid.group( 1 ) in seen:
			continue
		if cid:
			seen.add( cid.group( 1 ) )
		for m in re.finditer( r"drm-memory-(\w+):\s+(\d+) KiB", info ):
			out[m.group( 1 )] = out.get( m.group( 1 ), 0 ) + int( m.group( 2 ) )
	return out


def report( pid, tag, top=12 ):
	s = status( pid )
	print( f"== memory {tag}: VmRSS {s.get( 'VmRSS', 0 ) / 1048576:.2f} GB (anon {s.get( 'RssAnon', 0 ) / 1048576:.2f}, file {s.get( 'RssFile', 0 ) / 1048576:.2f}, "
		+ f"shmem {s.get( 'RssShmem', 0 ) / 1048576:.2f}), VmHWM {s.get( 'VmHWM', 0 ) / 1048576:.2f} GB" )
	g = drm( pid )
	if g:
		print( "   drm fdinfo: " + ", ".join( f"{k} {v / 1048576:.2f} GB" for k, v in sorted( g.items() ) ) )
	for n, rss, priv, cnt in by_mapping( pid )[:top]:
		print( f"   {rss / 1024:9.1f} MB rss {priv / 1024:9.1f} MB private-dirty  x{cnt:<4} {n[:90]}" )
