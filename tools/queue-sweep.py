#!/usr/bin/env python3
"""Move every ticked item of docs/QUEUE.md, verbatim, to the end of its section in docs/history/queue-done.md.

Run from anywhere in the repo; the pre-commit hook (tools/hooks/pre-commit) runs it on every commit. An item is its
'- [x]' line plus every following blank or indented line, up to the next unindented one. Prints what it moved and
refuses to write if any Q-number would end up missing or doubled across the two files.
"""
import collections, os, re, subprocess, sys

ITEM = re.compile( r'- \[([ x])\] ' )
QNUM = re.compile( r'- \[[ x]\] \*\*(Q\d+[a-z]*)\.' )

root = subprocess.run( [ 'git', 'rev-parse', '--show-toplevel' ], capture_output=True, text=True, check=True ).stdout.strip()
queue_path = os.path.join( root, 'docs', 'QUEUE.md' )
done_path = os.path.join( root, 'docs', 'history', 'queue-done.md' )

queue = open( queue_path ).read().split( '\n' )
done = open( done_path ).read().split( '\n' )

keep, moved, section = [], [], None   # moved: (section heading, item lines)
i = 0
while i < len( queue ):
	line = queue[i]
	if line.startswith( '## ' ):
		section = line
	m = ITEM.match( line )
	if not m:
		keep.append( line )
		i += 1
		continue
	j = i + 1
	while j < len( queue ) and ( queue[j] == '' or queue[j][0] in ' \t' ):
		j += 1
	k = j
	while k > i + 1 and queue[k - 1] == '':
		k -= 1
	if m.group( 1 ) == 'x':
		moved.append( ( section, queue[i:k] ) )
	else:
		keep += queue[i:k]
	keep += queue[k:j]
	i = j

order = [ l for l in queue if l.startswith( '## ' ) ]

if not moved:
	sys.exit( 0 )

out = []
for line in keep:
	if line == '' and out and out[-1] == '':
		continue
	out.append( line )

for heading, block in moved:
	if heading not in done:
		# a new section goes before the first section that follows it in the queue, or at the end
		later = order[order.index( heading ) + 1:]
		at = next( ( n for n, l in enumerate( done ) if l in later ), len( done ) )
		while at > 0 and done[at - 1] == '':
			at -= 1
		done[at:at] = [ '', heading, '' ]
	first = done.index( heading ) + 2
	at = first
	while at < len( done ) and not done[at].startswith( '## ' ):
		at += 1
	while at > first and done[at - 1] == '':
		at -= 1
	done[at:at] = block

def qnums( lines ):
	return collections.Counter( m.group( 1 ) for l in lines for m in [ QNUM.match( l ) ] if m )

before = qnums( queue ) + qnums( open( done_path ).read().split( '\n' ) )
after = qnums( out ) + qnums( done )
if before != after or any( v != 1 for v in after.values() ):
	sys.exit( 'queue-sweep: Q-numbers would not resolve once each; nothing written' )

if done[-1] != '':
	done.append( '' )
open( queue_path, 'w' ).write( '\n'.join( out ) )
open( done_path, 'w' ).write( '\n'.join( done ) )
for heading, block in moved:
	q = QNUM.match( block[0] )
	print( f"queue-sweep: moved {q.group( 1 ) if q else block[0][:40]} to docs/history/queue-done.md" )
