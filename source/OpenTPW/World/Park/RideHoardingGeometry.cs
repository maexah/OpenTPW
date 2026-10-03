using V2 = System.Numerics.Vector2;
using V3 = System.Numerics.Vector3;

namespace OpenTPW;

/// <summary>Generated edge panels, including the second-nearest source-vertex fitting in FUN_00452e70.</summary>
internal static class RideHoardingGeometry
{
	internal readonly record struct Panel( V2 A, V2 B, V2 GridA, V2 GridB, V3 Normal, float Order );

	public static Panel[] Build( ItemHoarding outline, IReadOnlyList<V3> sourceVertices )
	{
		if ( (outline.Width != 1 || outline.Depth != 1) && sourceVertices.Count < 2 )
		{
			// The original reads unset corner corrections in this malformed case. Refuse them deterministically.
			Unimplemented.Report( "HOARDING_DEGENERATE_BASE" );
			return [];
		}
		var panels = new List<Panel>();
		for ( var y = 0; y < outline.Depth; ++y )
		for ( var x = 0; x < outline.Width; ++x )
		{
			var bits = outline.At( x, y );
			if ( bits == 0 ) continue;
			var X = x * 10f;
			var Y = y * 10f;
			V2[] c = outline.Width == 1 && outline.Depth == 1
				? [new( 0.3f, -0.3f ), new( -0.3f, -0.3f ), new( 0.3f, 0.3f ), new( 0.3f, 0.3f )]
				: [Corner( new( X, Y + 10 ), sourceVertices ), Corner( new( X + 10, Y + 10 ), sourceVertices ),
					Corner( new( X + 10, Y ), sourceVertices ), Corner( new( X, Y ), sourceVertices )];
			bool Has( int bit ) => (bits & bit) != 0;
			void Add( int bit, V2 a, V2 b, V2 ga, V2 gb, V3 normal, V2 middle )
			{
				if ( !Has( bit ) ) return;
				var angle = (float)((Math.Atan2( middle.Y - outline.Depth * 0.5, middle.X - outline.Width * 0.5 )
					+ 3.9269909858703613) % 6.2831854820251465);
				panels.Add( new( a, b, ga, gb, normal, angle ) );
			}
			Add( 1, new( X + 10 + (Has( 4 ) ? c[1].X : 0), Y + 10 + c[0].Y ),
				new( X + (Has( 64 ) ? c[0].X : 0), Y + 10 + c[0].Y ),
				new( x + 1, y + 1 ), new( x, y + 1 ), new( 0, 1, 0 ), new( x + 0.5f, y + 1 ) );
			Add( 4, new( X + 10 + c[1].X, Y + (Has( 16 ) ? c[2].Y : 0) ),
				new( X + 10 + c[1].X, Y + 10 + (Has( 1 ) ? c[1].Y : 0) ),
				new( x + 1, y ), new( x + 1, y + 1 ), new( 1, 0, 0 ), new( x + 1, y + 0.5f ) );
			Add( 16, new( X + (Has( 64 ) ? c[3].X : 0), Y + c[3].Y ),
				new( X + 10 + (Has( 4 ) ? c[2].X : 0), Y + c[3].Y ),
				new( x, y ), new( x + 1, y ), new( 0, -1, 0 ), new( x + 0.5f, y ) );
			Add( 64, new( X + c[0].X, Y + 10 + (Has( 1 ) ? c[0].Y : 0) ),
				new( X + c[0].X, Y + (Has( 16 ) ? c[3].Y : 0) ),
				new( x, y + 1 ), new( x, y ), new( -1, 0, 0 ), new( x, y + 0.5f ) );
		}
		// OrderBy is stable, as is FUN_00470630 when two keys compare equal.
		return panels.OrderBy( p => p.Order ).ToArray();
	}

	private static V2 Corner( V2 target, IReadOnlyList<V3> vertices )
	{
		var nearest = float.MaxValue;
		var second = float.MaxValue;
		var nearIndex = -1;
		var secondIndex = -1;
		for ( var i = 0; i < vertices.Count; ++i )
		{
			// Mirrors the original's float stores in the x87 loop. Double intermediates stand in for x87 extended precision.
			var dx = (double)vertices[i].X - target.X;
			var dz = (double)vertices[i].Z - target.Y;
			var distance = Math.Sqrt( dx * dx + dz * (float)dz );
			var rounded = (float)distance;
			if ( distance < nearest )
			{
				second = nearest;
				secondIndex = nearIndex;
				nearest = rounded;
				nearIndex = i;
			}
			else if ( rounded < second )
			{
				second = rounded;
				secondIndex = i;
			}
		}
		return secondIndex < 0 ? V2.Zero : (new V2( vertices[secondIndex].X, vertices[secondIndex].Z ) - target) * 0.5f;
	}
}
