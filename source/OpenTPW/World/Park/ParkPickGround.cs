namespace OpenTPW;

/// <summary>
/// Where the pointer's line meets the land: the original's terrain test in <c>FUN_0045bf90</c> (from <c>0x0045c254</c>),
/// which intersects the line with the two planes of each cell cut by the cell's <c>0x2</c>, a plane's hit counting anywhere
/// inside the cell's square, and keeps the nearest by distance either way along the line
/// (<c>docs/exe/park-engine.md</c>, "The pick's terrain").
///
/// <para>
/// The original's heightfield axes are x, height (up) and z; here they are X, Z (up) and Y, so its z is Y and its height is Z.
/// The cells tried are the rectangle the line covers between the heightfield's low and high height (<c>+0x20</c>, <c>+0x24</c>),
/// held to the cells; a cell is tried only where the line crosses its square and then its heights' range (a sign test of two
/// opposite corners each, <c>0x0045c4f4</c> and <c>0x0045c648</c>); rows run outside and columns inside, the cell's first
/// triangle before its second, and a hit replaces the kept one only when nearer by a strict test.
/// </para>
/// </summary>
internal static class ParkPickGround
{
	/// <summary>
	/// The nearest hit of the line from <paramref name="eye"/> along <paramref name="direction"/> with the land, false where none
	/// of the cells the line covers is met. The line runs both ways: a plane behind the eye is met by distance like one ahead of it.
	/// </summary>
	public static bool Meet( HeightfieldFile field, Vector3 eye, Vector3 direction, out Vector3 hit )
	{
		hit = default;

		var (first, last) = CellsCovered( field, eye, direction );
		var (sizeX, sizeY) = (field.CellSizeX, field.CellSizeY);
		var stride = field.CellsX + 1;
		var nearest = float.MaxValue;
		var met = false;

		// Which corners of a cell the line has to pass between: with both of the line's horizontal components of one sign the
		// corner pair that rises across the cell, otherwise the pair that falls (and the same against the height).
		var alongTogether = SameSign( direction.X, direction.Y );
		var risesTogether = SameSign( direction.X, direction.Z );

		var acrossBase = -((eye.X * direction.Y) - (eye.Y * direction.X));
		var heightBase = -((eye.X * direction.Z) - (eye.Z * direction.X));

		for ( var cy = first.Y; cy <= last.Y; ++cy )
		{
			for ( var cx = first.X; cx <= last.X; ++cx )
			{
				var left = field.OriginX + (sizeX * cx);
				var right = left + sizeX;
				var near = field.OriginY + (sizeY * cy);
				var far = near + sizeY;

				if ( alongTogether
					? !Straddles( Across( left, far ), Across( right, near ) )
					: !Straddles( Across( left, near ), Across( right, far ) ) )
					continue;

				var at = (cy * stride) + cx;
				var (a, b, c, d) = (field.Heights[at], field.Heights[at + 1], field.Heights[at + stride], field.Heights[at + stride + 1]);
				var (low, high) = (MathF.Min( MathF.Min( a, b ), MathF.Min( c, d ) ), MathF.Max( MathF.Max( a, b ), MathF.Max( c, d ) ));

				if ( risesTogether
					? !Straddles( Up( left, high ), Up( right, low ) )
					: !Straddles( Up( left, low ), Up( right, high ) ) )
					continue;

				var diagonal = (field.Cells[(cy * field.CellsX) + cx] & 0x2) != 0;

				for ( var triangle = 0; triangle < 2; ++triangle )
				{
					var plane = Plane( diagonal, triangle, (left, near), (right, far), sizeX, sizeY, a, b, c, d );

					// The original makes the normal unit first; the quotient is the same without.
					var along = ((eye.X * plane.X) + (eye.Y * plane.Y) + (eye.Z * plane.Z)
						- ((plane.Origin.X * plane.X) + (plane.Origin.Y * plane.Y) + (plane.Origin.Z * plane.Z)))
						/ ((direction.X * plane.X) + (direction.Y * plane.Y) + (direction.Z * plane.Z));

					var point = new Vector3( eye.X - (along * direction.X), eye.Y - (along * direction.Y), eye.Z - (along * direction.Z) );

					if ( left <= point.X && near <= point.Y && point.X <= right && point.Y <= far && MathF.Abs( along ) < nearest )
					{
						met = true;
						hit = point;
						nearest = MathF.Abs( along );
					}
				}
			}
		}

		return met;

		float Across( float x, float y ) => (direction.Y * x) - (direction.X * y) + acrossBase;
		float Up( float x, float height ) => (direction.Z * x) - (direction.X * height) + heightBase;
	}

	/// <summary>The cells the line covers between the heightfield's low and high height, held to the cells.</summary>
	private static ((int X, int Y) First, (int X, int Y) Last) CellsCovered( HeightfieldFile field, Vector3 eye, Vector3 direction )
	{
		var (alongHigh, alongLow) = ((field.HeightHigh - eye.Z) / direction.Z, (field.HeightLow - eye.Z) / direction.Z);

		var xHigh = Held( ((alongHigh * direction.X) + eye.X - field.OriginX) / field.CellSizeX, field.CellsX );
		var xLow = Held( ((alongLow * direction.X) + eye.X - field.OriginX) / field.CellSizeX, field.CellsX );
		var yHigh = Held( ((alongHigh * direction.Y) + eye.Y - field.OriginY) / field.CellSizeY, field.CellsY );
		var yLow = Held( ((alongLow * direction.Y) + eye.Y - field.OriginY) / field.CellSizeY, field.CellsY );

		return ((Math.Min( xHigh, xLow ), Math.Min( yHigh, yLow )), (Math.Max( xHigh, xLow ), Math.Max( yHigh, yLow )));
	}

	/// <summary>A cell index as the original's float-to-int and two holds make it: truncated (a value the conversion cannot
	/// take is the integer indefinite, which is below nought), then nought below and the last cell above.</summary>
	private static int Held( float cell, int cells )
	{
		var index = float.IsFinite( cell ) && MathF.Abs( cell ) < 2147483648f ? (int)cell : int.MinValue;

		return index < 0 ? 0 : index >= cells ? cells - 1 : index;
	}

	private static bool SameSign( float a, float b ) => (a < 0f && b < 0f) || (0f < a && 0f < b);

	private static bool Straddles( float a, float b ) => (a < 0f && 0f < b) || (0f < a && b < 0f);

	/// <summary>
	/// One triangle's plane: a point on it and its normal, of the length the cross product gives
	/// (<c>0x0045c6b7</c>..<c>0x0045c7ea</c>). A cell with <c>0x2</c> clear is cut from (x+1, y) to (x, y+1), its triangles
	/// a-b-c and d-c-b; with it set from (x, y) to (x+1, y+1), its triangles c-d-a and b-a-d.
	/// </summary>
	private static (Vector3 Origin, float X, float Y, float Z) Plane( bool diagonal, int triangle, (float X, float Y) near, (float X, float Y) far,
		float sizeX, float sizeY, float a, float b, float c, float d )
	{
		var (corner, alongX, alongY, forward, behind) = (diagonal, triangle) switch
		{
			(false, 0) => (new Vector3( near.X, near.Y, a ), sizeX, sizeY, b - a, c - a),
			(false, _) => (new Vector3( far.X, far.Y, d ), -sizeX, -sizeY, c - d, b - d),
			(true, 0) => (new Vector3( near.X, far.Y, c ), sizeX, -sizeY, d - c, a - c),
			_ => (new Vector3( far.X, near.Y, b ), -sizeX, sizeY, a - b, d - b),
		};

		return (corner, alongY * forward, alongX * behind, -(alongY * alongX));
	}
}
