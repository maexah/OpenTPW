namespace OpenTPW;

/// <summary>
/// A position or a force in the fixed-point arithmetic the peep simulation is written in, where
/// <see cref="One"/> is a whole map cell - the original's vector helpers around
/// <c>0x0050cd80</c>-<c>0x0050f870</c>, which everything that steers a person is built out of.
///
/// <para>
/// <b>Every one of these was read off the disassembly rather than the decompiled form</b>, because they
/// are <c>thiscall</c> and the decompiler drops <c>ECX</c> - so each appears to take one argument fewer
/// than it does, and which operand is the subtrahend is exactly the sort of thing that gets reversed.
/// Subtraction is <c>this - other</c>, confirmed by <c>0050cd80</c> loading <c>ECX</c> first and
/// returning with <c>RET 0x8</c>.
/// </para>
/// <para>
/// <b>Multiplying and dividing go through 64 bits and come back.</b> The original writes both as a pair
/// of halves recombined, which is what a 32-bit compiler does with a <c>long long</c>; there is no
/// rounding anywhere, only truncation, and that is kept.
/// </para>
/// </summary>
public readonly record struct FixedVector( int X, int Y )
{
	/// <summary>One whole map cell, and the scale every fraction here is expressed in.</summary>
	public const int One = 1 << 16;

	public static FixedVector Zero => new( 0, 0 );

	public static FixedVector operator +( FixedVector a, FixedVector b ) => new( a.X + b.X, a.Y + b.Y );

	public static FixedVector operator -( FixedVector a, FixedVector b ) => new( a.X - b.X, a.Y - b.Y );

	public static FixedVector operator -( FixedVector a ) => new( -a.X, -a.Y );

	/// <summary>Multiply by a fraction, keeping the whole product before shifting it back down.</summary>
	public FixedVector ScaledBy( int by )
		=> new( (int)(((long)X * by) >> 16), (int)(((long)Y * by) >> 16) );

	/// <summary>
	/// Divide by a fraction. <b>Dividing by <see cref="One"/> is an identity</b>, which matters: the
	/// steering step does exactly that every tick, so the step that looks like "divide by the mass" turns
	/// out to divide by one.
	/// </summary>
	public FixedVector DividedBy( int by )
		=> new( (int)(((long)X << 16) / by), (int)(((long)Y << 16) / by) );

	/// <summary>
	/// How long this is, by the original's own three-range method.
	///
	/// <para>
	/// <b>The ranges are what keep the squares in range</b>, rather than an optimisation: inside the first
	/// one both parts are under <c>0x1000</c>, so the sum of squares cannot reach much past 25 bits, and
	/// each wider range shifts down far enough to say the same. Anything bigger is measured shifted and
	/// shifted back, losing precision rather than wrapping.
	/// </para>
	/// <para>
	/// The root itself takes its argument <i>unsigned</i> - <c>0x004d49a0</c> writes it into the low half
	/// of a stack qword, zeroes the high half, and only then does <c>FILD</c>. That is reproduced; but it
	/// is worth being straight that <b>no guarded range actually needs the thirty-second bit</b>, and an
	/// earlier draft of this comment implied they did.
	/// </para>
	/// </summary>
	public int Length
	{
		get
		{
			var ax = X < 0 ? -X : X;
			var ay = Y < 0 ? -Y : Y;
			var both = (uint)(ax | ay);

			if ( (both & 0xfffff000u) == 0 )
				return Root( (uint)((ax * ax) + (ay * ay)) );

			if ( (both & 0xffc00000u) == 0 )
				return Root( (uint)(((ax >> 8) * (ax >> 8)) + ((ay >> 8) * (ay >> 8))) ) << 8;

			return Root( (uint)(((ax >> 18) * (ax >> 18)) + ((ay >> 18) * (ay >> 18))) ) << 18;
		}
	}

	/// <summary>
	/// How far this is by the eight-sided measure the simulation uses wherever it only needs to know
	/// roughly - <c>|x| + |y| - (the smaller of them halved)</c>. It is not the true length and is not
	/// meant to be; a diagonal comes out about three per cent long.
	/// </summary>
	public int OctagonalLength
	{
		get
		{
			var ax = X < 0 ? -X : X;
			var ay = Y < 0 ? -Y : Y;

			return ax + ay - ((ax < ay ? ax : ay) >> 1);
		}
	}

	/// <summary>
	/// This, shortened to a length if it is longer than one.
	///
	/// <para>
	/// <b>A vector exactly at the limit is still put through the arithmetic</b>, because the original's
	/// test is "shorter than", not "no longer than". It comes back unchanged either way, but through a
	/// multiply by one rather than by returning early, and that is kept.
	/// </para>
	/// </summary>
	public FixedVector ClampedTo( int limit )
	{
		var length = Length;

		if ( length < limit )
			return this;

		// A departure, named rather than hidden: with nothing to measure the original divides by zero.
		// No caller can reach it - a vector of no length is only clamped to no length - and faulting
		// here would be a worse reproduction than answering with the vector itself.
		if ( length == 0 )
			return this;

		return ScaledBy( (int)(((long)limit << 16) / length) );
	}

	/// <summary>Which cell this position falls in - the whole part, with the fraction dropped.</summary>
	public (int X, int Y) Cell => (X >> 16, Y >> 16);

	/// <summary>The corner of a cell, which is the exact inverse of <see cref="Cell"/>.</summary>
	public static FixedVector AtCell( int x, int y ) => new( x << 16, y << 16 );

	/// <summary>
	/// The whole part of a square root, taken of an <b>unsigned</b> value - see <see cref="Length"/> for
	/// why that matters. The original loads it as a 64-bit integer with the high half zeroed, takes
	/// <c>FSQRT</c>, and truncates.
	/// </summary>
	internal static int Root( uint of ) => (int)Math.Sqrt( of );
}
