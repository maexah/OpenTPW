#!/usr/bin/env python3
"""The original's queue tile rule and queue piece models, its own code run under unicorn (exeleaf.py).

    $OPENTPW_EMU_PYTHON -u queuetile.py            # the report: the rows, seven cells, the model per index, index 8
    from queuetile import Rules
    r = Rules()
    r.tile( 0x11, 0x10, { 0x01, 0x10 }, flags=0 )  # (set, index, angle) of a type 3 cell, mask 0x11, flow 0x10,
                                                   # its north and south neighbours paths linked back: (2, 8, 0)
    r.model( 9 )                                   # 'gk_trckQ.md2': the model FUN_005229e0 makes for that index

`tile` runs `FUN_00535dd0` (the tile rule) on one cell at (60,60) of a zeroed world: each neighbour the mask names is
laid as a path (type 1) when its bit is in `paths`, else as a queue (type 3), with the opposite bit set, so a path
link is mutual; every track record is zero, so no link is refused unless `flags` is given: it is written to the byte at
`[0x008023a0] + id x 0x28 + 0x10ffd8 + 0xe` of every cell laid (id = y*128 + x + 1), the flags nibble a played park can put there. `model` runs `FUN_005229e0( x, y, 2, index, angle,
0 )` with `FUN_00463060` answering the handle it is given, after the queue and kart loaders (`FUN_00522900`,
`TrackKit_LoadKartPieces`) filled their tables with `RideSystem_RegisterType` answering its name argument, so a
handle is its model's name (0: none). `docs/exe/park-engine.md`, "An index past the queue table". Stubbed in the
emulator's memory only: `rand` (0), the theme folder, `sprintf`, the wad open, the item id, the registration. The
direction vectors the C runtime's static initialisers write are laid in by hand (`0x004d9a65`..`0x004d9c3f`).

Needs `$OPENTPW_EMU_PYTHON` and `testme.exe` (`OPENTPW_EXE`, or the game folder's).
"""
import os, sys, struct
sys.path.insert( 0, os.path.dirname( os.path.abspath( __file__ ) ) )
from exeleaf import Exe
from unicorn import UcError, UC_HOOK_MEM_READ_UNMAPPED
from unicorn.x86_const import UC_X86_REG_EIP

RET = b'\xc3'
STEP = { 0x01: ( 0, -1 ), 0x04: ( 1, 0 ), 0x10: ( 0, 1 ), 0x40: ( -1, 0 ) }
OPPOSITE = { 0x01: 0x10, 0x04: 0x40, 0x10: 0x01, 0x40: 0x04 }
VECTORS = ( ( 0x7cdba0, 0 ), ( 0x7cdba4, -1 ), ( 0x7cdba8, 0 ), ( 0x7cdbac, 1 ), ( 0x7cdbb0, -1 ), ( 0x7cdbb4, 1 ),
	( 0x7cdbc8, 1 ), ( 0x7cdbcc, 0 ), ( 0x7cdbd0, -1 ), ( 0x7cdbd4, 0 ), ( 0x7cdbd8, 1 ), ( 0x7cdbdc, -1 ) )
X, Y = 60, 60
WORLD_SIZE = 0x1b2000


class Rules:
	def __init__( self, make_stubbed=True ):
		x = self.x = Exe( tib=True )
		x.write( 0x0067b5c0, b'\x31\xc0' + RET )                 # rand -> 0
		x.write( 0x0040ef40, b'\x31\xc0' + RET )                 # the theme's folder
		x.write( 0x0067a650, RET )                               # sprintf
		x.write( 0x00461a60, b'\xb8\x01\x00\x00\x00' + RET )     # the wad opened
		x.write( 0x0046b3d0, RET )                               # the item id
		x.write( 0x004629d0, b'\x8b\x44\x24\x08' + RET )         # RideSystem_RegisterType -> its name argument
		if make_stubbed:
			x.write( 0x00463060, b'\x8b\x44\x24\x04' + RET )     # FUN_00463060 -> the handle it was given
		x.call( 0x00522900 )
		x.call( 0x00522aa0 )
		self.world = x.alloc( WORLD_SIZE )
		for at in ( 0x007cf83c, 0x0080239c, 0x008023a0 ):        # cells: [0x80239c] + 0x294 + id x 0x44
			x.write( at, struct.pack( '<I', self.world ) )       # track records: [0x8023a0] + id x 0x28 + 0x10ffd8
		for at, v in VECTORS:
			x.write( at, struct.pack( '<i', v ) )

	def name( self, handle ):
		return None if handle == 0 else self.x.read( handle, 40 ).split( b'\0' )[0].decode( 'latin1' )

	def row( self, index ):
		h, = struct.unpack( '<I', self.x.read( 0x00763388 + 12 * index, 4 ) )
		return self.name( h ), self.x.read( 0x00763390 + 12 * index, 1 )[0]

	def _cell( self, cx, cy ):
		return self.world + 0x294 + ( cy * 128 + cx + 1 ) * 0x44

	def _lay( self, cx, cy, typ, mask, direction ):
		c = self._cell( cx, cy )
		self.x.write( c, struct.pack( '<H', cy * 128 + cx + 1 ) )
		self.x.write( c + 0x08, struct.pack( '<i', typ ) )
		self.x.write( c + 0x0c, bytes( [ mask, direction ] ) )

	def _flag( self, cx, cy, flags ):
		record, = struct.unpack( '<I', self.x.read( 0x008023a0, 4 ) )
		self.x.write( record + ( cy * 128 + cx + 1 ) * 0x28 + 0x10ffd8 + 0xe, bytes( [ flags ] ) )

	def tile( self, mask, direction, paths, typ=3, flags=0 ):
		self.x.write( self.world, b'\0' * WORLD_SIZE )
		self._lay( X, Y, typ, mask, direction )
		self._flag( X, Y, flags )
		for b, ( dx, dy ) in STEP.items():
			if mask & b:
				self._lay( X + dx, Y + dy, 1 if b in paths else 3, OPPOSITE[ b ], 0 )
				self._flag( X + dx, Y + dy, flags )
		out = self.x.alloc( 4 )
		r = self.x.call( 0x00535dd0, out, ecx=self._cell( X, Y ) )
		angle, = struct.unpack( '<i', self.x.read( out, 4 ) )
		return r >> 16, r & 0xffff, angle

	def model( self, index, angle=90 ):
		return self.name( self.x.call( 0x005229e0, X, Y, 2, index, angle, 0 ) )


def report():
	r = Rules()
	print( "The rows FUN_005229e0 indexes, as the two loaders leave them (0x00763388 + 12 x index)" )
	for i in range( 17 ):
		n, m = r.row( i )
		print( "    index %2d  row %#x  model %-14s  mask %#04x" % ( i, 0x00763388 + 12 * i, n or "none", m ) )
	print( "FUN_00535dd0, the tile rule, on type 3 cells" )
	for what, mask, direction, paths in (
			( "straight N-S, both ends paths", 0x11, 0x10, { 0x01, 0x10 } ),
			( "straight N-S, one end a path", 0x11, 0x10, { 0x01 } ),
			( "corner E+S, flow 0x04, both paths", 0x14, 0x04, { 0x04, 0x10 } ),
			( "corner E+S, flow 0x10 (bumped), both paths", 0x14, 0x10, { 0x04, 0x10 } ),
			( "T N+E+S, three paths", 0x15, 0x10, { 0x01, 0x04, 0x10 } ),
			( "cross, four paths", 0x55, 0x10, { 0x01, 0x04, 0x10, 0x40 } ),
			( "one link, to a path", 0x10, 0x10, { 0x10 } ) ):
		s, i, a = r.tile( mask, direction, paths )
		print( "    %-44s mask %#04x flow %#04x -> set %d index %2d angle %3d" % ( what, mask, direction, s, i, a ) )
	print( "FUN_005229e0( 60, 60, 2, index, 90, 0 ): the model it makes" )
	for i in ( 5, 8, 9, 10, 11, 14 ):
		print( "    index %2d -> %s" % ( i, r.model( i ) or "none (handle 0)" ) )
	y = Rules( make_stubbed=False )
	pos = y.x.alloc( 16 )
	y.x.write( pos, struct.pack( '<IIII', X, Y, 270, 0x00816d68 ) )
	read = y.x.reads( 0x00463060, 0, 0x33a, pos, 0, 0, 0, 0, 0, 0xffffffff, 0 )
	got = y.x.call( 0x00463060, 0, 0x33a, pos, 0, 0, 0, 0, 0, 0xffffffff, 0 )
	print( "The real FUN_00463060 given handle 0 (index 8 in play, the retile FUN_005365d0's call)" )
	print( "    answers %d; read outside the stack: %s" % ( got, [ hex( a ) for a in read ] or "nothing" ) )
	print( "FUN_005229e0( 60, 60, 2, 8, 90, 1 ), the post-load rebuild's call (FUN_005366e0), into the real FUN_00463ab0" )
	bad = []
	y.x.mu.hook_add( UC_HOOK_MEM_READ_UNMAPPED, lambda mu, acc, addr, size, val, d: bad.append( ( mu.reg_read( UC_X86_REG_EIP ), addr ) ) or False )
	try:
		print( "    answers %#x, no fault" % y.x.call( 0x005229e0, X, Y, 2, 8, 90, 1 ) )
	except UcError as e:
		print( "    FAULT: %s, at EIP %#x reading %#x" % ( e, bad[0][0], bad[0][1] ) if bad else "    FAULT: %s" % e )
	print( "    the type table's slot 0 [0x007ae6a8] = %#x" % struct.unpack( '<I', y.x.read( 0x007ae6a8, 4 ) ) )


if __name__ == "__main__":
	report()
