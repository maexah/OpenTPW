#!/usr/bin/env python3
"""Count two numbers from the source and write them into the "Numbers" table of docs/STATUS.md.

  Opcodes      how many members of the Opcode enum RideScript.cs has a `case Opcode.` label for, of how many there are
  Known gaps   how many different names the source passes to Unimplemented.Report( "NAME" ), outside the tests

The website's Ride Status page reads these two rows, so they are counted, never typed. Run from anywhere in the repo;
the pre-commit hook (tools/hooks/pre-commit) runs it when a commit changes source. A row is rewritten, with today's
date, only when its number changed. `--check` writes nothing and exits 1 if a row is out of date.
"""
import datetime, os, re, subprocess, sys

root = subprocess.run( [ 'git', 'rev-parse', '--show-toplevel' ], capture_output=True, text=True, check=True ).stdout.strip()
status_path = os.path.join( root, 'docs', 'STATUS.md' )

def read( *parts ):
	return open( os.path.join( root, *parts ), encoding='utf-8', errors='replace' ).read()

# The enum's members, with comments taken out first so that a name in a comment is not counted.
enum = read( 'source', 'OpenTPW.Files', 'Formats', 'Script', 'Opcode.cs' )
enum = enum[enum.index( 'public enum Opcode' ):]
enum = re.sub( r'//.*|/\*.*?\*/', '', enum[enum.index( '{' ) + 1:enum.index( '}' )], flags=re.S )
members = set( re.findall( r'^\s*([A-Za-z_]\w*)\s*(?:=|,|$)', enum, flags=re.M ) )
built = set( re.findall( r'case Opcode\.(\w+)', read( 'source', 'OpenTPW', 'VM', 'RideScript.cs' ) ) ) & members

gaps = set()
for folder, folders, files in os.walk( os.path.join( root, 'source' ) ):
	folders[:] = [ name for name in folders if name not in ( 'bin', 'obj', 'OpenTPW.Tests' ) ]
	for name in files:
		if name.endswith( '.cs' ):
			gaps.update( re.findall( r'Unimplemented\.Report\(\s*"([^"]+)"', read( folder, name ) ) )

if not members or not built or not gaps:
	sys.exit( 'status-numbers: a count came out as nothing; the source has moved and this script must follow it' )

today = datetime.date.today().isoformat()
rows = {
	'Opcodes': ( f'**{len( built )}** of {len( members )}', '`case Opcode.` labels in `RideScript.cs` vs enum members; counted by `tools/status-numbers.py`' ),
	'Known gaps': ( f'**{len( gaps )}**', 'different names passed to `Unimplemented.Report`, outside the tests; counted by `tools/status-numbers.py`' ),
}

lines = read( 'docs', 'STATUS.md' ).split( '\n' )
start = next( i for i, line in enumerate( lines ) if line.startswith( '## Numbers' ) )
end = next( ( i for i in range( start + 1, len( lines ) ) if lines[i].startswith( '## ' ) ), len( lines ) )
changed = []

for label, ( value, how ) in rows.items():
	at = next( ( i for i in range( start, end ) if lines[i].startswith( f'| {label} |' ) ), None )
	if at is not None and lines[at].split( '|' )[2].strip() == value:
		continue
	row = f'| {label} | {value} | {today}, {how} |'
	if at is None:
		# A new row goes under the last row of the table.
		at = max( i for i in range( start, end ) if lines[i].startswith( '|' ) ) + 1
		lines.insert( at, row )
		end += 1
	else:
		lines[at] = row
	changed.append( f'{label}: {value}' )

if '--check' in sys.argv:
	sys.exit( 'status-numbers: out of date: ' + '; '.join( changed ) if changed else 0 )
if changed:
	open( status_path, 'w', encoding='utf-8' ).write( '\n'.join( lines ) )
	print( 'status-numbers: ' + '; '.join( changed ) )
