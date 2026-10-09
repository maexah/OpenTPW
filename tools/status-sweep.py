#!/usr/bin/env python3
"""Keep docs/STATUS.md's "Not verified on screen" to the item just landed.

Moves every bullet of that section but the first, verbatim, to the top of docs/history/not-verified.md, so the
newest stays first there too. The pre-commit hook (tools/hooks/pre-commit) runs it on a commit that stages
docs/STATUS.md. A new item's account is written ABOVE the one before it; the hook does the rest. Prints what it
moved and refuses to write if a bullet would be lost.
"""
import os, subprocess, sys

HEADING = '## Not verified on screen'

root = subprocess.run( [ 'git', 'rev-parse', '--show-toplevel' ], capture_output=True, text=True, check=True ).stdout.strip()
status_path = os.path.join( root, 'docs', 'STATUS.md' )
history_path = os.path.join( root, 'docs', 'history', 'not-verified.md' )

status = open( status_path ).read().split( '\n' )
history = open( history_path ).read().split( '\n' )

if HEADING not in status:
	sys.exit( f'status-sweep: no "{HEADING}" in docs/STATUS.md' )

start = status.index( HEADING ) + 1
end = next( ( i for i in range( start, len( status ) ) if status[i].startswith( '## ' ) ), len( status ) )
bullets = [ i for i in range( start, end ) if status[i].startswith( '- ' ) ]

if len( bullets ) <= 1:
	sys.exit( 0 )

moved = [ status[i] for i in bullets[1:] ]
kept = status[:bullets[1]] + [ line for i, line in enumerate( status[bullets[1]:end], bullets[1] ) if i not in bullets ] + status[end:]

# The history file's head runs to its first bullet; the moved go in there, above the older.
first = next( ( i for i, line in enumerate( history ) if line.startswith( '- ' ) ), len( history ) )
while first > 0 and history[first - 1] == '' and first == len( history ):
	first -= 1
grown = history[:first] + moved + history[first:]

before = sum( 1 for line in status + history if line.startswith( '- ' ) )
after = sum( 1 for line in kept + grown if line.startswith( '- ' ) )
if before != after:
	sys.exit( f'status-sweep: {before} bullets before and {after} after; nothing written' )

open( status_path, 'w' ).write( '\n'.join( kept ) )
open( history_path, 'w' ).write( '\n'.join( grown ) )
print( f'status-sweep: moved {len( moved )} earlier accounts to docs/history/not-verified.md' )
