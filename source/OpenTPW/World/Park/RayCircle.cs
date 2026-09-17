namespace OpenTPW;

/// <summary>
/// How far along a direction you get before touching a circle - the original's <c>FUN_0050cc20</c>, which
/// the wall-avoiding behaviour uses to decide whether a corner is in the way.
///
/// <para>
/// It is an ordinary quadratic, solved in the same fixed point as everything else around it, but there are
/// three things about it that are worth writing down because none of them is what you would write from
/// scratch.
/// </para>
/// <para>
/// <b>It halves rather than dividing by twice the first coefficient.</b> The two roots of
/// <c>a.t^2 + b.t + c</c> are <c>(-b ± root) / 2a</c>, and this divides by two flat - so it is only right
/// when <c>a</c> is one, which is to say when the direction handed to it is a unit vector. Its one caller
/// does normalise first, so the assumption holds there; it is an assumption all the same.
/// </para>
/// <para>
/// <b>It answers with the nearer of the two roots, which may be behind you.</b> There is no test for a
/// negative answer here - the caller does that, and does it with a plain "not greater than zero".
/// </para>
/// <para>
/// <b>The scaling of the square root does not match the scaling of everything around it, and that is
/// reproduced rather than corrected.</b> Every product here is shifted back down by sixteen, so the
/// discriminant is a fixed-point quantity - but the root is then taken of it directly, which for a
/// fixed-point value gives an answer in a different scale from the <c>b</c> it is then added to. The
/// effect is real and measurable: a ray fired at a circle of one cell's radius whose centre is four cells
/// away answers about four cells rather than three. <b>Whether that is intended is NOT established</b>, so
/// it is left exactly as the original computes it and recorded here instead of being quietly fixed.
/// </para>
/// </summary>
public static class RayCircle
{
	/// <summary>
	/// What comes back when the line never touches the circle.
	///
	/// <para>
	/// <b>Its one caller tests for the wrong value.</b> The original compares the answer against
	/// <c>0x7fffffff</c>, which this can never be, so that test is dead. It costs nothing only because the
	/// check straight after it - "not greater than zero" - catches this, which is negative. The dead test
	/// is not reproduced here; the behaviour it fails to affect is.
	/// </para>
	/// </summary>
	public const int Missed = int.MinValue;

	/// <summary>A product in the fixed point the simulation uses, formed whole before it is shifted back.</summary>
	private static int Times( int a, int b ) => (int)(((long)a * b) >> 16);

	/// <summary>
	/// How far along <paramref name="direction"/> from <paramref name="from"/> the circle is first touched,
	/// or <see cref="Missed"/> if it never is.
	/// </summary>
	public static int Along( FixedVector from, FixedVector direction, FixedVector centre, int radius )
	{
		var offset = from - centre;

		var a = Times( direction.X, direction.X ) + Times( direction.Y, direction.Y );
		var b = (Times( direction.X, offset.X ) + Times( direction.Y, offset.Y )) * 2;
		var c = Times( offset.X, offset.X ) + Times( offset.Y, offset.Y ) - Times( radius, radius );

		var discriminant = Times( b, b ) - (Times( a, c ) * 4);

		// Strictly greater than nothing: a line that merely grazes the circle counts as missing it.
		if ( discriminant <= 0 )
			return Missed;

		var root = FixedVector.Root( (uint)discriminant );

		var behind = -(root + b);
		var ahead = root - b;

		// The nearer of the two, then halved - see the class remarks on why it is not halved by twice a.
		return (behind <= ahead ? behind : ahead) >> 1;
	}
}
