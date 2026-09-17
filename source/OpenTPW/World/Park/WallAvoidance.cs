namespace OpenTPW;

/// <summary>
/// Being pushed away from walls - the original's <c>avoid_walls</c> behaviour at <c>FUN_0050e4a0</c>, and
/// the heaviest of the three forces on a walking person at a weight of nine tenths.
///
/// <para>
/// It looks one step ahead - where the person would be next tick - and works in the direction of that
/// step. Then it asks <see cref="CellEdge"/> about the four sides of the cell being stood in, and if the
/// one being walked towards is shut and the person is already closer to it than their own radius, it
/// pushes them back out by <b>twice</b> how far in they are. Only after all four of those does it
/// consider corners.
/// </para>
/// <para>
/// <b>The first push wins and the rest are never asked.</b> The order is east, west, south, north, and
/// then the corner - so a person wedged into an inside corner is answered about one wall only, on any
/// given tick.
/// </para>
/// <para>
/// <b>Each corner asks about its own cells and its own two sides</b>, and those are what matter: the
/// down-right corner asks <c>0,3</c> then <c>0,3</c>, down-left <c>0,1</c> then <c>0,1</c>, up-left
/// <c>2,1</c> then <c>1,2</c>, up-right <c>2,3</c> then <c>3,2</c>.
/// </para>
/// <para>
/// <b>The two upward cases list their second pair in the opposite order, and that turns out not to
/// matter</b> - said here because an earlier draft of this comment claimed it did. Both probes in that
/// pair are refusals, so the pair is an "and" and asking it either way round gives the same answer; the
/// first pair is an "or", equally order-blind. What would really go wrong is getting a <i>cell</i> or a
/// <i>side</i> wrong, which is why those are what the tests pin.
/// </para>
/// <para>
/// <b>Only one corner is ever possible.</b> The original writes four blocks that fall through into one
/// another, but the tests gating them are on the signs of the direction, and those are exclusive - so at
/// most one block can run. That is why this reads as a choice rather than a cascade.
/// </para>
/// </summary>
public static class WallAvoidance
{
	/// <summary>How hard a wall pushes: twice how far past it the person has got.</summary>
	public const int PushOut = 2;

	/// <summary>
	/// Which way this person should be pushed to keep them off the walls, or nothing if no wall is close
	/// enough to matter.
	/// </summary>
	/// <param name="radius">How big the person is, which is how close to a wall is too close.</param>
	/// <param name="blocked">Whether that side of that cell is shut - <see cref="CellEdge.Blocked"/>.</param>
	public static FixedVector Steer( FixedVector position, FixedVector velocity, int radius,
		Func<int, int, StepDirection, bool> blocked )
	{
		ArgumentNullException.ThrowIfNull( blocked );

		// Where the step would land, and which way that is. The original builds the look-ahead as
		// position plus velocity times one, then takes the difference back off again.
		var ahead = position + velocity;
		var (cellX, cellY) = position.Cell;
		var offset = ahead - position;
		var reach = offset.Length;
		var way = reach == 0 ? offset : offset.DividedBy( reach );

		var east = way.X >= 0;
		var west = way.X < 0;
		var south = way.Y >= 0;
		var north = way.Y < 0;

		// The four sides, in the original's order. The first one that pushes is the answer.
		if ( east && blocked( cellX, cellY, StepDirection.East ) )
		{
			var wall = FixedVector.AtCell( cellX + 1, 0 ).X - radius;

			if ( position.X > wall )
				return new FixedVector( (wall - position.X) * PushOut, 0 );
		}

		if ( west && blocked( cellX, cellY, StepDirection.West ) )
		{
			var wall = FixedVector.AtCell( cellX, 0 ).X + radius;

			if ( position.X < wall )
				return new FixedVector( (wall - position.X) * PushOut, 0 );
		}

		if ( south && blocked( cellX, cellY, StepDirection.South ) )
		{
			var wall = FixedVector.AtCell( 0, cellY + 1 ).Y - radius;

			if ( position.Y > wall )
				return new FixedVector( 0, (wall - position.Y) * PushOut );
		}

		if ( north && blocked( cellX, cellY, StepDirection.North ) )
		{
			var wall = FixedVector.AtCell( 0, cellY ).Y + radius;

			if ( position.Y < wall )
				return new FixedVector( 0, (wall - position.Y) * PushOut );
		}

		// At most one of these four can be reached - see the class remarks.
		// The cell the corner belongs to and the point the corner IS are not the same thing, and only
		// agree going down and to the right - see the remarks on Corner.
		if ( south && east )
			return Corner( position, way, reach, radius, blocked, cellX + 1, cellY + 1,
				cellX + 1, cellY + 1,
				StepDirection.North, StepDirection.West, StepDirection.North, StepDirection.West );

		if ( south && west )
			return Corner( position, way, reach, radius, blocked, cellX - 1, cellY + 1,
				cellX, cellY + 1,
				StepDirection.North, StepDirection.East, StepDirection.North, StepDirection.East );

		if ( north && west )
			return Corner( position, way, reach, radius, blocked, cellX - 1, cellY - 1,
				cellX, cellY,
				StepDirection.South, StepDirection.East, StepDirection.East, StepDirection.South );

		if ( north && east )
			return Corner( position, way, reach, radius, blocked, cellX + 1, cellY - 1,
				cellX + 1, cellY,
				StepDirection.South, StepDirection.West, StepDirection.West, StepDirection.South );

		return FixedVector.Zero;
	}

	/// <summary>
	/// Whether the corner of the diagonal cell is worth steering round, and how hard.
	///
	/// <para>
	/// Three things must hold. <b>At least one of the diagonal cell's two near sides must be shut</b>, or
	/// there is no corner there at all. <b>Both ways round it must be open</b> - if either is shut, the
	/// straight-sided tests above have already dealt with it, or would have. And the line the person is
	/// walking must actually meet a circle drawn round the corner, within the step being looked ahead.
	/// </para>
	/// <para>
	/// <b>The original also tests the answer against <c>0x7fffffff</c>, which it can never be.</b> That
	/// test is dead - see <see cref="RayCircle.Missed"/> - and is not reproduced, because the check after
	/// it catches the missing case anyway. Leaving it in would suggest it did something.
	/// </para>
	/// </summary>
	/// <remarks>
	/// <b>The cell the corner belongs to is not where the corner is.</b> A cell's position is its
	/// low-numbered corner, so the point shared with the diagonal neighbour is that neighbour's own
	/// position only when the neighbour is the higher-numbered one - going down and to the right. Going
	/// up, or left, the shared point belongs to the cell being stood in. The original writes this out four
	/// times and it is easy to miss: the down-right case builds the point with two increments, and the
	/// up-left case at <c>0050eab3</c> uses the standing cell's own coordinates with no adjustment at all.
	/// Getting it wrong puts the corner a whole cell away and it is then quietly judged out of reach,
	/// which is a silence rather than a wrong push - three of the four were wrong here until a test that
	/// went the other way round caught it.
	/// </remarks>
	private static FixedVector Corner( FixedVector position, FixedVector way, int reach, int radius,
		Func<int, int, StepDirection, bool> blocked, int cornerX, int cornerY,
		int cornerAtX, int cornerAtY,
		StepDirection acrossTheCorner, StepDirection alongTheCorner,
		StepDirection firstWayRound, StepDirection secondWayRound )
	{
		if ( !blocked( cornerX, cornerY, acrossTheCorner ) && !blocked( cornerX, cornerY, alongTheCorner ) )
			return FixedVector.Zero;

		// The two cells either side of the corner, each asked about the side facing past it.
		var (alongX, alongY) = firstWayRound == StepDirection.North || firstWayRound == StepDirection.South
			? (position.Cell.X, cornerY)
			: (cornerX, position.Cell.Y);

		if ( blocked( alongX, alongY, firstWayRound ) )
			return FixedVector.Zero;

		var (acrossX, acrossY) = secondWayRound == StepDirection.North || secondWayRound == StepDirection.South
			? (position.Cell.X, cornerY)
			: (cornerX, position.Cell.Y);

		if ( blocked( acrossX, acrossY, secondWayRound ) )
			return FixedVector.Zero;

		var corner = FixedVector.AtCell( cornerAtX, cornerAtY );
		var along = RayCircle.Along( position, way, corner, radius );

		if ( along <= 0 || along > reach )
			return FixedVector.Zero;

		// Where the line touches, measured out from the corner and scaled by how big the person is.
		return (position + way.ScaledBy( along ) - corner).DividedBy( radius );
	}
}
