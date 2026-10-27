#!/usr/bin/env python3
"""keylatch.py - poll the running ORIGINAL's key tables: each table's enable and every row whose held latch is set (Q692).

  keylatch.py PID [SECONDS] [INTERVAL] [TABLE...]   SECONDS default 5 (0 prints one line), INTERVAL default 0.03,
                                                    TABLE system game camera cheat coaster shortcuts (default camera)

One line a poll: seconds since start; per table its name, its enable (the table object's +0x06; FUN_0040cfa0 zeroes
it, FUN_0040cf60 sets it) and the rows whose +0x06 latch is 1 as action:key (FUN_0040c900 sets it on a key's down,
FUN_0040c990 clears it on its up, each only while the table is enabled; the camera update reads it through
FUN_0040ca20); then the park camera's point 0x007908f0/0x007908f8, its scroll terms 0x00790a88/0x00790a90, the zoom
0x007909ec, g_ParkRunning 0x00786ba4, the game menu 0x007c2534, the options screen 0x007cb2fc and the open park screen
0x007c24c8. docs/exe/park-engine.md, the key tables. Read-only."""
import struct, sys, time

TABLES = { 'system': 0x0078718c, 'game': 0x00787a60, 'camera': 0x00787c14, 'cheat': 0x0078733c,
	'coaster': 0x00787294, 'shortcuts': 0x0078721c }

def main():
	pid = int( sys.argv[1] ); secs = float( sys.argv[2] ) if len( sys.argv ) > 2 else 5
	step = float( sys.argv[3] ) if len( sys.argv ) > 3 else 0.03
	names = sys.argv[4:] or ['camera']
	f = open( f'/proc/{pid}/mem', 'rb', buffering=0 )
	def rd( a, fmt='I' ):
		f.seek( a ); return struct.unpack( '<' + fmt, f.read( struct.calcsize( '<' + fmt ) ) )[0]
	t0 = time.time()
	while True:
		t = time.time() - t0
		parts = []
		for name in names:
			table = rd( TABLES[name] )
			rows, count, enable = rd( table ), rd( table + 4, 'h' ), rd( table + 6, 'H' )
			held = []
			for i in range( count ):
				action, key, mods, latch = struct.unpack( '<HHHH', ( f.seek( rows + i * 20 ), f.read( 8 ) )[1] )
				if latch:
					held.append( f"{action:#x}:{key:#06x}" + ( f"+{mods:#x}" if mods else "" ) )
			parts.append( f"{name} on {enable} held [{' '.join( held )}]" )
		print( f"{t:7.3f} {' | '.join( parts )} | poi {rd( 0x007908f0, 'f' ):9.3f} {rd( 0x007908f8, 'f' ):9.3f} "
			f"v {rd( 0x00790a88, 'f' ):+.5f} {rd( 0x00790a90, 'f' ):+.5f} zoom {rd( 0x007909ec, 'f' ):6.2f} "
			f"running {rd( 0x00786ba4 )} menu {rd( 0x007c2534 ):#x} options {rd( 0x007cb2fc ):#x} "
			f"screen {rd( 0x007c24c8 ):#x}", flush=True )
		if t >= secs: break
		time.sleep( step )

main()
