namespace OpenTPW;

/// <summary>
/// Which way a person is facing, worked out from the step they just took - the original's
/// <c>FUN_006e7074</c>, which is an arctangent done with a lookup table and integer division.
///
/// <para>
/// <b>A turn is <see cref="FullTurn"/> = 0x800, not 360 and not two pi.</b> Eleven bits to the circle, which
/// is what <see cref="ParkWorld.Person.Facing"/> folds into eight octants by biasing and shifting. Every
/// answer here is masked to those eleven bits.
/// </para>
/// <para>
/// <b>The table is the executable's own, copied out rather than computed, and that is deliberate.</b> Its
/// 257 entries are exactly <c>trunc( atan( i / 256 ) * 2048 / 2pi )</c> - verified against all 257 words at
/// <c>0x006e71b4</c>, matching on every one under <i>truncation</i> and on only 135 under rounding. So the
/// closed form is known and could have been used. It is not, for two reasons. The original performs no
/// floating-point arctangent at all, and the rest of the peep simulation is deliberately integer
/// throughout; and at <c>i = 256</c> the true value is exactly 256, which means a <c>Math.Atan</c> that
/// returns the nearest double below <c>pi/4</c> would truncate to <b>255</b> and silently put a person a
/// step out of true at a quadrant boundary. A table cannot drift.
/// </para>
/// <para>
/// The repeated entries near the end - two 167s, two 182s, two 191s and so on - are not errors. They are
/// what truncation of a flattening curve gives, and they are in the executable exactly so.
/// </para>
/// </summary>
public static class PeepHeading
{
	/// <summary>A whole turn, in the eleven-bit units everything here is measured in.</summary>
	public const int FullTurn = 0x800;

	/// <summary>A quarter turn, which is where each of the table's four branches is anchored.</summary>
	public const int QuarterTurn = FullTurn / 4;

	/// <summary>
	/// <c>atan( i / 256 )</c> for <c>i</c> in 0..256, in eleven-bit turn units - the 257 words at
	/// <c>0x006e71b4</c>, verbatim.
	/// </summary>
	private static readonly int[] Arctangent =
	[
		0, 1, 2, 3, 5, 6, 7, 8, 10, 11, 12, 13, 15, 16, 17, 19,
		20, 21, 22, 24, 25, 26, 27, 29, 30, 31, 32, 34, 35, 36, 38, 39,
		40, 41, 43, 44, 45, 46, 48, 49, 50, 51, 53, 54, 55, 56, 57, 59,
		60, 61, 62, 64, 65, 66, 67, 68, 70, 71, 72, 73, 75, 76, 77, 78,
		79, 81, 82, 83, 84, 85, 87, 88, 89, 90, 91, 92, 94, 95, 96, 97,
		98, 99, 101, 102, 103, 104, 105, 106, 107, 109, 110, 111, 112, 113, 114, 115,
		116, 118, 119, 120, 121, 122, 123, 124, 125, 126, 127, 129, 130, 131, 132, 133,
		134, 135, 136, 137, 138, 139, 140, 141, 142, 143, 144, 145, 147, 148, 149, 150,
		151, 152, 153, 154, 155, 156, 157, 158, 159, 160, 161, 162, 163, 164, 165, 166,
		167, 167, 168, 169, 170, 171, 172, 173, 174, 175, 176, 177, 178, 179, 180, 181,
		182, 182, 183, 184, 185, 186, 187, 188, 189, 190, 191, 191, 192, 193, 194, 195,
		196, 197, 198, 198, 199, 200, 201, 202, 203, 203, 204, 205, 206, 207, 208, 208,
		209, 210, 211, 212, 212, 213, 214, 215, 216, 216, 217, 218, 219, 220, 220, 221,
		222, 223, 223, 224, 225, 226, 226, 227, 228, 229, 229, 230, 231, 232, 232, 233,
		234, 235, 235, 236, 237, 237, 238, 239, 239, 240, 241, 242, 242, 243, 244, 244,
		245, 246, 246, 247, 248, 248, 249, 250, 250, 251, 252, 252, 253, 254, 254, 255,
		256
	];

	/// <summary>
	/// What a ratio is scaled to before indexing. <b>Not the table's length</b>: <c>Arctangent</c> runs
	/// 0 to 256 inclusive and so has <b>257</b> entries, which is what lets a ratio of exactly 1 index it
	/// without a bounds check. This said "how many entries the table has", which is off by one.
	/// </summary>
	public const int Steps = 256;

	/// <summary>The table itself, so a test can pin it against the values read out of the executable.</summary>
	public static int Arctan( int index ) => Arctangent[index];

	/// <summary>
	/// The direction a step went, as an eleven-bit turn.
	///
	/// <para>
	/// <b>The original is called with the across component NEGATED.</b> At <c>004fa34a</c> it computes
	/// <c>oldX - newX</c> for the first argument and <c>newY - oldY</c> for the second, so the two are not
	/// symmetric and feeding it a plain delta would mirror every person in the park. The caller here does
	/// the negating, as the original's does, and this takes what it is given.
	/// </para>
	/// <para>
	/// <b>Standing still keeps the way you were facing.</b> The original returns whatever happened to be in
	/// the register when both components are zero, which is not a meaningful answer; it is given the previous
	/// heading here instead, because a person who did not move has not turned.
	/// </para>
	/// </summary>
	/// <param name="across">The first argument the original passes, which is the <b>negated</b> x step.</param>
	/// <param name="down">The second, which is the y step as it stands.</param>
	/// <param name="ifStill">What to answer when the step was nothing at all.</param>
	public static int Of( int across, int down, int ifStill )
	{
		if ( across == 0 && down == 0 )
			return ifStill;

		// The eight cases are the original's own, in its order. Each picks the shorter component as the
		// numerator so the ratio lands inside the table, and each anchors on a different quarter turn.
		if ( across >= 0 )
		{
			if ( down < 0 )
			{
				return across < -down
					? Turn( Arctan( Ratio( across, -down ) ) )
					: Turn( QuarterTurn - Arctan( Ratio( -down, across ) ) );
			}

			return across < down
				? Turn( (2 * QuarterTurn) - Arctan( Ratio( across, down ) ) )
				: Turn( Arctan( Ratio( down, across ) ) + QuarterTurn );
		}

		var back = -across;

		if ( down < 0 )
		{
			return back < -down
				? Turn( FullTurn - Arctan( Ratio( back, -down ) ) )
				: Turn( Arctan( Ratio( -down, back ) ) + (3 * QuarterTurn) );
		}

		return back < down
			? Turn( Arctan( Ratio( back, down ) ) + (2 * QuarterTurn) )
			: Turn( (3 * QuarterTurn) - Arctan( Ratio( down, back ) ) );
	}

	/// <summary>
	/// The smaller component over the larger, scaled to the table's 0..256. Integer division, truncating,
	/// as the original's <c>IDIV</c> does.
	/// </summary>
	private static int Ratio( int smaller, int larger ) => (smaller << 8) / larger;

	/// <summary>Holds an answer to the eleven bits a turn has - <c>0x800 - 0</c> is none, not a whole turn.</summary>
	private static int Turn( int angle ) => angle & (FullTurn - 1);
}
