namespace OpenTPW;

/// <summary>
/// The park's generator - <c>FUN_00516330</c>, a linear congruential step rotated right thirteen, whose state
/// <c>FUN_00516370</c> sets (<c>docs/exe/park.md</c>, "`RAND` (28)").
///
/// <para>
/// <b>The original keeps one, in the world (<c>[world+0x1da708]</c>), and every system draws it</b>; a balloon,
/// a costume's return and a guest's arrival each reseed it with a guest's id first. Here the park's one is
/// <see cref="ParkState.RandomSeed"/>, drawn by the leavers' summons and reseeded and drawn by a litter drop
/// (<c>0x004d94e2</c>); every other caller holds a state of
/// its own and says at its site where that departs.
/// </para>
/// </summary>
public static class ParkGenerator
{
	/// <summary>
	/// One draw: the state stepped and rotated, and the magnitude of what it became. The <c>NEG</c> that takes
	/// the magnitude hands a state of <c>0x80000000</c> back as it is (<c>0x0051635f</c>).
	/// </summary>
	public static uint Draw( ref uint state )
	{
		state = (state * 0x19660Du) + 0x3C6EF35Fu;
		state = (state >> 13) | (state << 19);

		return (int)state < 0 ? 0u - state : state;
	}
}
