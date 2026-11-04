#!/usr/bin/env python3
"""The original's queue tool on a made-up world, its own code run under unicorn (exeleaf.py, queuetile.py's set-up):
the stamp, a click's passes over a line, the queue walk and the clear, and what each leaves in a cell's counter.

    $OPENTPW_EMU_PYTHON -u queuerun.py               # the report: C1 to C6 of q860/PREDICTION.txt
    from queuerun import QueueWorld
    q = QueueWorld()
    q.entrance( 59, 60, 0x04 )                      # a type 9 entrance linked east to the placer's node (60,60)
    q.click( ( 60, 60 ), ( 64, 60 ) )               # one click of the queue tool: the eight passes over the line
    q.cell( 64, 60 )                                # {'type', 'mask', 'dir', 'flags', 'owner', 'ctr'}
    q.walk()                                        # FUN_00530120, the queue button's walk: the pending list
    q.clear( 64, 60, 1, 0 )                         # FUN_005367a0( 1, 0 ) on one cell: what it answers
    q.line( 0x32, ( 64, 63 ), ( 64, 60 ) )          # one op over a line (0x32 is Backspace's clear)

The world is zeroed but for every cell's own id at +0; the tool's mode is 3 (queue), laying is free
(`[0x008186d4]`), the bank (thing slot 0 of `[0x007cfb90]`) holds 100000, and the selected thing (`[0x008186ac]`)
names the entrance at +0x36. Stubbed in the emulator's memory only, beside queuetile.py's: the model release
`FUN_00463c80` (the stub's handles are names) and the resale percentage `FUN_004e2290` (reads the clock; 100).
`docs/exe/park-engine.md`, "A queue cell's counter". Needs `$OPENTPW_EMU_PYTHON` and `testme.exe`.
"""
import os, sys, struct
sys.path.insert( 0, os.path.dirname( os.path.abspath( __file__ ) ) )
from queuetile import Rules, WORLD_SIZE

PASSES = ( 0x87, 3, 0x80, 0x82, 0x85, 0x86, 0x83, 0x81 )     # the click's ops, each over the whole line (0x005273fe..)


class QueueWorld:
	def __init__( self ):
		r = Rules()
		x = self.x = r.x
		self.world = r.world
		self._put( 0x0081ae2c, '<I', 3 )
		self._put( 0x008186d4, '<B', 1 )
		bank = x.alloc( 0x200 )
		self._put( bank + 0xc, '<i', 100000 )
		self._put( 0x007cfb90, '<I', bank )
		x.write( 0x004e2290, b'\xb8\x64\x00\x00\x00\xc3' )
		x.write( 0x00463c80, b'\xc3' )
		self.thing = x.alloc( 0x400 )
		self._put( 0x008186ac, '<I', self.thing )
		ids = bytearray( 0x44 * 0x4001 )
		for i in range( 1, 0x4001 ):
			ids[ i * 0x44 : i * 0x44 + 2 ] = struct.pack( '<H', i )
		self._ids = bytes( ids )
		self.wipe()

	def _put( self, a, fmt, v ):
		self.x.write( a, struct.pack( fmt, v ) )

	def _get( self, a, fmt ):
		return struct.unpack( fmt, self.x.read( a, struct.calcsize( fmt ) ) )[0]

	def addr( self, cx, cy ):
		return self.world + 0x294 + ( cy * 128 + cx + 1 ) * 0x44

	def wipe( self ):
		self.x.write( self.world, b'\0' * WORLD_SIZE )
		self.x.write( self.world + 0x294, self._ids )

	def snapshot( self ):
		return self.x.read( self.world, WORLD_SIZE )

	def restore( self, snap ):
		self.x.write( self.world, snap )

	def lay( self, cx, cy, typ, mask=0, direction=0, owner=0, counter=0 ):
		c = self.addr( cx, cy )
		self._put( c + 8, '<i', typ )
		self.x.write( c + 0xc, bytes( [ mask, direction ] ) )
		self._put( c + 0x10, '<H', owner )
		self._put( c + 0x20, '<h', counter )

	def entrance( self, cx, cy, bit ):
		"""A type 9 entrance at (cx, cy) linked by `bit` to a type 3 node (the placer's), both owned by the entrance's
		cell, which op 0x83 writes on every cell laid (`[0x00818698]`)."""
		dx, dy = { 0x01: ( 0, -1 ), 0x04: ( 1, 0 ), 0x10: ( 0, 1 ), 0x40: ( -1, 0 ) }[ bit ]
		back = { 0x01: 0x10, 0x04: 0x40, 0x10: 0x01, 0x40: 0x04 }[ bit ]
		own = cy * 128 + cx + 1
		self.lay( cx, cy, 9, bit, bit, own )
		self.lay( cx + dx, cy + dy, 3, back, bit, own )
		self._put( 0x00818698, '<I', own )
		self._put( self.thing + 0x36, '<H', own )
		self._put( 0x00763ac0, '<I', 0x80 )
		self._put( 0x00763ac4, '<I', 0x80 )
		return cx + dx, cy + dy

	def cell( self, cx, cy ):
		c = self.addr( cx, cy )
		return dict( type=self._get( c + 8, '<i' ), mask=self.x.read( c + 0xc, 1 )[0], dir=self.x.read( c + 0xd, 1 )[0],
			flags=self._get( c + 0xe, '<H' ), owner=self._get( c + 0x10, '<H' ), ctr=self._get( c + 0x20, '<h' ) )

	def stamp( self, cx, cy, typ=3 ):
		return self.x.call( 0x005346d0, typ, ecx=self.addr( cx, cy ) )

	def line( self, op, a, b ):
		bx, by = self.x.alloc( 4 ), self.x.alloc( 4 )
		self._put( bx, '<i', b[0] )
		self._put( by, '<i', b[1] )
		return self.x.call( 0x00536100, op, a[0], a[1], bx, by )

	def click( self, a, b ):
		self._put( 0x0081ede4, '<I', a[0] )
		self._put( 0x0081ede8, '<I', a[1] )
		got = None
		for op in PASSES:
			got = self.line( op, a, b )
		return got

	def walk( self ):
		self.x.call( 0x00530120, self.thing )
		n = self._get( 0x00820a8c, '<i' )
		return [ ( self._get( 0x0081b740 + 8 * i, '<i' ), self._get( 0x0081b744 + 8 * i, '<i' ) ) for i in range( n ) ]

	def clear( self, cx, cy, dx, dy, force=False ):
		self._put( 0x0081d7a8, '<I', 1 if force else 0 )
		try:
			return self.x.call( 0x005367a0, dx & 0xffffffff, dy & 0xffffffff, ecx=self.addr( cx, cy ) )
		finally:
			self._put( 0x0081d7a8, '<I', 0 )


def report():
	q = QueueWorld()

	def show( label, cells ):
		print( "    %s" % label )
		for cx, cy in cells:
			s = q.cell( cx, cy )
			print( "      (%d,%d) type %d mask %#04x dir %#04x owner %d counter %d" % ( cx, cy, s['type'], s['mask'], s['dir'], s['owner'], s['ctr'] ) )

	print( "C1  the stamp FUN_005346d0( 3 )" )
	q.lay( 60, 60, 3 )
	v1 = q.stamp( 60, 60 ); a = q.cell( 60, 60 )['ctr']
	v2 = q.stamp( 60, 60 ); b = q.cell( 60, 60 )['ctr']
	print( "    type 3 cell holding 0: answers %s, counter %d; again: answers %s, counter %d" % ( v1, a, v2, b ) )
	v3 = q.stamp( 61, 60 ); s = q.cell( 61, 60 )
	print( "    type 0 cell: answers %s, type %d counter %d" % ( v3, s['type'], s['ctr'] ) )

	print( "C2  a queue run, the click's eight passes over each line" )
	q.wipe()
	A = q.entrance( 59, 60, 0x04 )
	B, C, D = ( 64, 60 ), ( 64, 63 ), ( 61, 63 )
	l1 = [ ( i, 60 ) for i in range( 60, 65 ) ]
	l2 = [ ( 64, j ) for j in range( 61, 64 ) ]
	l3 = [ ( i, 63 ) for i in range( 63, 60, -1 ) ]
	print( "    click 1 A->B answers %s" % q.click( A, B ) )
	show( "after click 1", l1 )
	print( "    click 2 B->C answers %s" % q.click( B, C ) )
	show( "after click 2", l1 + l2 )
	two = q.snapshot()
	print( "    click 3 C->D answers %s" % q.click( C, D ) )
	show( "after click 3", l1 + l2 + l3 )

	print( "C3  the queue walk FUN_00530120( thing ) over click 3's queue" )
	print( "    list: %s" % q.walk() )
	show( "after the walk", [ ( 59, 60 ) ] + l1 + l2 + l3 )

	print( "C4  the clear FUN_005367a0( dx, dy ), one type 3 cell, owner 0" )
	for tag, mask, ctr, step, force in ( ( 'a straight 0x11 holding 1, step (1,0)', 0x11, 1, ( 1, 0 ), False ),
			( 'b straight 0x11 holding 0, step (1,0)', 0x11, 0, ( 1, 0 ), False ),
			( 'c corner 0x14 holding 1, step (1,0)', 0x14, 1, ( 1, 0 ), False ),
			( 'd straight 0x11 holding 3, step (0,0)', 0x11, 3, ( 0, 0 ), False ),
			( 'e one-link 0x01 holding 1, step (0,1)', 0x01, 1, ( 0, 1 ), False ),
			( 'f force, straight 0x11 holding 5, step (1,0)', 0x11, 5, ( 1, 0 ), True ) ):
		q.wipe()
		q.lay( 60, 60, 3, mask, 0x10, counter=ctr )
		v = q.clear( 60, 60, step[0], step[1], force )
		s = q.cell( 60, 60 )
		print( "    %-46s answers %s -> type %d mask %#04x counter %d" % ( tag, v, s['type'], s['mask'], s['ctr'] ) )

	print( "C5  Backspace's line FUN_00536100( 0x32, C, B ) after click 2" )
	q.restore( two )
	print( "    answers %s" % q.line( 0x32, C, B ) )
	show( "after the clear", l1 + l2 )

	print( "C6  the same Backspace line with B's counter set to 0 first (what OpenTPW's lay leaves)" )
	q.restore( two )
	q._put( q.addr( *B ) + 0x20, '<h', 0 )
	print( "    answers %s" % q.line( 0x32, C, B ) )
	show( "after the clear", l1 + l2 )


if __name__ == "__main__":
	report()
