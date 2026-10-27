#!/usr/bin/env python3
"""patchmean.py IMG NAME=X0,Y0,X1,Y1 ... [--vs IMG2] - the mean colour of named pixel boxes in a frame of either game
(R G B, their spread, the mean brightness), a line a box; with --vs a second frame, the same boxes in it and the
difference, and `mad`, the mean over the box's pixels of each one's absolute difference averaged over R, G and B: how
much moved between two frames of one camera, where the means can agree (Q486: the sea's wave). For a colour compared
across two games or two runs at one camera (Q479: grass, sea, brick).

  patchmean.py orig/A-fog.png grass=300,400,500,500 --vs orig/A-nofog.png

Boxes are frame pixels, X1 and Y1 exclusive. Importable: mean( img, box ) answers ((r, g, b), (sr, sg, sb));
mad( img, img2, box ) the mean absolute difference."""
import sys
from PIL import Image

def mean( img, box ):
	im = img if isinstance( img, Image.Image ) else Image.open( img )
	px = list( im.convert( 'RGB' ).crop( box ).getdata() )
	n = len( px ); m = [ sum( p[ c ] for p in px ) / n for c in range( 3 ) ]
	s = [ ( sum( ( p[ c ] - m[ c ] ) ** 2 for p in px ) / n ) ** 0.5 for c in range( 3 ) ]
	return tuple( m ), tuple( s )

def mad( img, img2, box ):
	a, b = ( ( i if isinstance( i, Image.Image ) else Image.open( i ) ).convert( 'RGB' ).crop( box ).getdata() for i in ( img, img2 ) )
	return sum( abs( p[ c ] - q[ c ] ) for p, q in zip( a, b ) for c in range( 3 ) ) / ( 3 * len( a ) )

def fmt( m, s ):
	return '(%5.1f %5.1f %5.1f) sd (%4.1f %4.1f %4.1f) lum %5.1f' % ( *m, *s, 0.299 * m[ 0 ] + 0.587 * m[ 1 ] + 0.114 * m[ 2 ] )

if __name__ == '__main__':
	args = sys.argv[ 1: ]; other = None
	if '--vs' in args:
		i = args.index( '--vs' ); other = args[ i + 1 ]; del args[ i:i + 2 ]
	img = args[ 0 ]
	for spec in args[ 1: ]:
		name, b = spec.split( '=' ); box = tuple( int( v ) for v in b.split( ',' ) )
		m, s = mean( img, box ); line = '%-8s %s' % ( name, fmt( m, s ) )
		if other:
			m2, s2 = mean( other, box )
			line += '  |  %s  d (%+.1f %+.1f %+.1f) mad %.2f' % ( fmt( m2, s2 ), *( m2[ c ] - m[ c ] for c in range( 3 ) ), mad( img, other, box ) )
		print( line )
