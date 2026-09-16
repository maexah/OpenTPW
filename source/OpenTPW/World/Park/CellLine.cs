namespace OpenTPW;

/// <summary>
/// A straight line across the map, one cell at a time - the original's <c>FUN_00511350</c> and the step
/// it chooses at the top of both the pathfinder and the path straightener.
///
/// <para>
/// <b>This is what the pathfinder actually does first</b>, and it is worth saying plainly because the name
/// suggests otherwise: there is no search here, no open list, no cost and no heap. The original walks
/// straight at where it is going, and only when the way is shut does it start feeling round the obstacle.
/// This is that walk.
/// </para>
/// <para>
/// It is ordinary Bresenham with one accumulator shared between the axes, and every part of it truncates
/// the way the original's integer division does. The error starts at <b>half of each distance added
/// together</b> rather than at half the larger, which is not the usual formulation and is reproduced here
/// rather than tidied: it is what decides which cells a diagonal line actually passes through, and a
/// tidier seed walks a visibly different line.
/// </para>
/// <para>
/// <b>The degenerate cases are the original's own and they are not symmetric.</b> On a line that does not
/// move across, the across direction is set to the down one; on a line that does not move down, the down
/// direction is set to the across one - and that second assignment happens <i>after</i> the first, so a
/// line going nowhere at all ends up with both set to the vertical direction for a zero <c>dy</c>. That
/// falls out of the order the original writes them in, and is kept.
/// </para>
/// </summary>
public sealed class CellLine
{
	/// <summary>
	/// The four steps, as the executable keeps them: interleaved <c>dx, dy</c> bytes at <c>0x007622b0</c>,
	/// reading <c>00 ff | 01 00 | 00 01 | ff 00</c>.
	///
	/// <para>
	/// This is a <b>second and independent</b> source for the direction numbering, which until now rested
	/// on the boundary guards of the step check. The two agree exactly: 0 is <c>-y</c>, 1 is <c>+x</c>, 2
	/// is <c>+y</c> and 3 is <c>-x</c>.
	/// </para>
	/// </summary>
	public static (int Dx, int Dy) StepFor( StepDirection direction ) => direction switch
	{
		StepDirection.North => (0, -1),
		StepDirection.East => (1, 0),
		StepDirection.South => (0, 1),
		_ => (-1, 0)
	};

	/// <summary>How far the line runs across, as a count of cells and never negative.</summary>
	public int AcrossDistance { get; }

	/// <summary>How far it runs up or down.</summary>
	public int DownDistance { get; }

	/// <summary>Which way across the line goes.</summary>
	public StepDirection Across { get; }

	/// <summary>Which way up or down it goes.</summary>
	public StepDirection Down { get; }

	/// <summary>
	/// Which axis is the longer one. The original works this out and then does not use it here, so it is
	/// exposed rather than acted on - something else reads it.
	/// </summary>
	public bool AcrossIsLonger { get; }

	/// <summary>How far the walk has drifted from the true line, which is what picks each step.</summary>
	public int Error { get; private set; }

	public CellLine( int fromX, int fromY, int toX, int toY )
	{
		var dx = toX - fromX;
		var dy = toY - fromY;

		AcrossDistance = dx < 0 ? -dx : dx;
		DownDistance = dy < 0 ? -dy : dy;
		AcrossIsLonger = DownDistance < AcrossDistance;

		Across = dx > 0 ? StepDirection.East : StepDirection.West;
		Down = dy > 0 ? StepDirection.South : StepDirection.North;

		// In that order, because the original writes them in that order.
		if ( fromX == toX )
			Across = Down;

		if ( fromY == toY )
			Down = Across;

		Error = (AcrossDistance / 2) + (DownDistance / 2);
	}

	/// <summary>
	/// The next step along the line, moving the accumulator on. <b>Taking the step is the caller's
	/// business</b> - the original asks whether the way is shut before it moves, and does not undo the
	/// accumulator when it is, so the line keeps its place whether or not the walker got anywhere.
	/// </summary>
	public StepDirection Next()
	{
		if ( AcrossDistance <= Error )
		{
			Error -= AcrossDistance;

			return Down;
		}

		Error += DownDistance;

		return Across;
	}
}
