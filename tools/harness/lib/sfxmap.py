#!/usr/bin/env python3
"""A sound category's effects and variation headers, read from its cat_<name>SFX.map.

    sfxmap.py <data dir>                 every category: the spread of each variation's range (+0x22)
    sfxmap.py <data dir> <file> [id]     one category's effects (id, flags, param, its variations' ranges), or one effect's
                                         variations, each with its sample records named: the bank (from the BANK file
                                         beside <file>), the entry (0-based) and the name the .sdt stores (cut to 15
                                         characters); <file> is opened as given, not looked for under <data dir>, which
                                         this form does not read (Q632)

Importable: Category( path ).effects is a list of Effect( id, flags, param, variations ), each
variation a Variation( samples, zones, volume, pitch, wait, range ); Category.records[( effect id, variation )] is
that variation's sample records, Record( entry, odds, length, bank ), entry 0-based and bank 1-based as stored;
banks( path ) the BANK file's paths beside it, sdt( path, bank ) a bank's .sdt found case-blind, names( sdt ) its
entries' names. The walk is the loader's
(0x006c0510): after the effect table each effect's 42-byte headers, then for each of its
variations its 16-byte sample records and its 8-byte zone records. `whole` says the walk ended
at the file's last byte.
"""
import collections, struct, sys, os

Effect = collections.namedtuple( "Effect", "id flags param variations" )
Variation = collections.namedtuple( "Variation", "samples zones volume pitch wait range" )
Record = collections.namedtuple( "Record", "entry odds length bank" )

class Category:
	def __init__( self, path ):
		d = open( path, "rb" ).read()
		self.path = path
		count = struct.unpack_from( "<i", d, 0x1c )[0]
		at = 0x34 + count * 20
		self.effects = []
		self.records = {}
		for i in range( count ):
			r = 0x34 + i * 20
			eid, nvar = struct.unpack_from( "<ii", d, r )
			flags = struct.unpack_from( "<H", d, r + 0x10 )[0]
			heads = []
			for v in range( nvar ):
				h = at + v * 42
				ns, = struct.unpack_from( "<H", d, h )
				nz, = struct.unpack_from( "<I", d, h + 4 )
				vol = ( d[h + 0xc], d[h + 0xd] )
				pitch = struct.unpack_from( "<bb", d, h + 0xe )
				wait = struct.unpack_from( "<HH", d, h + 0x10 )
				rng, = struct.unpack_from( "<f", d, h + 0x22 )
				heads.append( Variation( ns, nz, vol, pitch, wait, rng ) )
			at += nvar * 42
			for vi, v in enumerate( heads ):
				recs = []
				for k in range( v.samples ):
					idx, odds, _, ms, bank = struct.unpack_from( "<iHHii", d, at + k * 16 )
					recs.append( Record( idx - 1, odds, ms, bank ) )
				self.records[( eid, vi )] = recs
				at += v.samples * 16 + v.zones * 8
			self.effects.append( Effect( eid, flags, d[r + 0x12], heads ) )
		self.whole = at == len( d )

def banks( path ):
	"""The BANK file beside an SFX map: its paths, in order (records are 11 bytes after the count at 0x18)."""
	b = open( path[:-len( "SFX.map" )] + "BANK.map", "rb" ).read()
	n = struct.unpack_from( "<i", b, 0x18 )[0]; at = 0x1c + n * 11; out = []
	for _ in range( n ):
		l = struct.unpack_from( "<i", b, at )[0]; out.append( b[at + 4:at + 4 + l - 1].decode( "latin1" ) ); at += 4 + l
	return out

def sdt( path, bank ):
	"""A bank's .sdt: its path is relative to the level folder (the map's folder's parent) and omits HD.sdt; looked up case-blind."""
	here = os.path.dirname( os.path.dirname( os.path.abspath( path ) ) )
	parts = banks( path )[bank - 1].replace( "\\", "/" ).split( "/" )
	parts[-1] += "HD.sdt"
	for part in parts:
		hit = [f for f in os.listdir( here ) if f.lower() == part.lower()]
		if not hit: return None
		here = os.path.join( here, hit[0] )
	return here

def names( path ):
	"""An .sdt's entries' names, as stored (16 bytes at +8 of each entry, cut at the NUL)."""
	b = open( path, "rb" ).read(); n = struct.unpack_from( "<I", b, 0 )[0]
	return [b[o + 8:o + 24].split( b"\0" )[0].decode( "latin1" ) for o in struct.unpack_from( "<%dI" % n, b, 4 )]

def categories( data ):
	out = []
	for root, _, files in os.walk( data ):
		for f in files:
			if f.lower().startswith( "cat_" ) and f.lower().endswith( "sfx.map" ):
				out.append( os.path.join( root, f ) )
	return sorted( out )

if __name__ == "__main__":
	data = sys.argv[1]
	if len( sys.argv ) > 2:
		c = Category( sys.argv[2] )
		want = int( sys.argv[3] ) if len( sys.argv ) > 3 else None
		for e in c.effects:
			if want is None:
				print( f"effect {e.id:4d} flags 0x{e.flags:04x} param {e.param} ranges {sorted( set( v.range for v in e.variations ) )}" )
			elif e.id == want:
				print( f"effect {e.id} flags 0x{e.flags:04x} param {e.param}" )
				for vi, v in enumerate( e.variations ):
					print( v )
					for r in c.records[( e.id, vi )]:
						f = sdt( sys.argv[2], r.bank )
						name = names( f )[r.entry] if f else "?"
						print( f"    sample: bank {r.bank} {banks( sys.argv[2] )[r.bank - 1]} entry {r.entry} {name} {r.length} ms odds {r.odds}" )
		sys.exit( 0 )
	total = collections.Counter()
	nvar = 0
	for p in categories( data ):
		c = Category( p )
		spread = collections.Counter()
		byflag = collections.defaultdict( collections.Counter )
		for e in c.effects:
			for v in e.variations:
				spread[v.range] += 1; total[v.range] += 1; nvar += 1
				byflag[e.flags][v.range] += 1
		print( f"{os.path.relpath( p, data )}: whole={c.whole} effects={len( c.effects )} ranges={dict( sorted( spread.items() ) )}" )
		for fl, s in sorted( byflag.items() ):
			print( f"    flags 0x{fl:04x}: {dict( sorted( s.items() ) )}" )
	print( f"ALL: {nvar} variations, ranges {dict( sorted( total.items() ) )}" )
