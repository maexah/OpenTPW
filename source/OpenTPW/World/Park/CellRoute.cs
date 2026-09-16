namespace OpenTPW;

/// <summary>
/// A route across the map and the pass that pulls the slack out of it - the original's
/// <c>FUN_005110d0</c>, which every one of the pathfinder's three stages runs before it hands a route on.
///
/// <para>
/// <b>The record is the original's own and it has four parts.</b> A count, then the cell the route sets
/// off from, then the cell it is going to, then the waypoints themselves. That layout is not a reading:
/// <c>FUN_00511420</c> starts the search from the slot at <c>+2</c>, <c>FUN_00511470</c> tests arrival
/// against the slot at <c>+4</c>, and <c>FUN_005108a0</c> builds a scratch copy of exactly that shape
/// before filling it in. Three sites, the same four fields.
/// </para>
/// <para>
/// <b>What the pass does.</b> From an anchor it tries to reach four waypoints ahead, then three, then two,
/// then one, taking the first that a straight line gets to and throwing away everything it jumped over.
/// The anchor does <i>not</i> move when a jump lands - only when every jump has failed - so a run of
/// successful jumps keeps measuring from the same place and the straightening is as aggressive as the map
/// allows. Whole passes repeat until one changes nothing.
/// </para>
/// <para>
/// <b>The anchor for the very first waypoint is the route's own start</b>, not the waypoint before it -
/// there is none. The original writes that as two different offsets off the same pointer, which looks
/// like an inconsistency until the record's shape is known and then is exactly right.
/// </para>
/// </summary>
public sealed class CellRoute
{
	/// <summary>How far ahead the pass will try to reach before it starts giving ground.</summary>
	public const int MostToJump = 4;

	/// <summary>The cell the route sets off from, which is the anchor for the first waypoint.</summary>
	public (int X, int Y) Start { get; set; }

	/// <summary>
	/// The cell the route is going to. <b>The straightening pass never reads this</b> - it is part of the
	/// record because the search either side of it does, and leaving it out would misdescribe the shape.
	/// </summary>
	public (int X, int Y) Goal { get; set; }

	/// <summary>The route itself, in order.</summary>
	public List<(int X, int Y)> Waypoints { get; } = [];

	/// <summary>
	/// The cell a waypoint is measured from: the one before it, or the route's <see cref="Start"/> when
	/// there is no one before it.
	/// </summary>
	public (int X, int Y) Before( int index )
	{
		ArgumentOutOfRangeException.ThrowIfNegative( index );

		return index == 0 ? Start : Waypoints[index - 1];
	}

	/// <summary>
	/// Whether a straight line from one cell to another gets there without meeting anything shut - the
	/// original's inner loop, and the walk <see cref="CellLine"/> already provides.
	///
	/// <para>
	/// <b>It is not only the sides the line crosses that are asked about.</b> Wherever the line turns, the
	/// two ways of taking that turn one cell early and one cell late are each tried, and <b>a way that is
	/// open at its first step must be open at its second as well</b>. A way that is shut at its first step
	/// is simply not available and is held against nothing. That is stricter than
	/// <see cref="MapStep.CanStep"/>, which needs only one of its two routes to work: here a route that
	/// starts open and then closes rejects the whole line.
	/// </para>
	/// </summary>
	/// <param name="blocked">Whether that side of that cell is shut - <see cref="CellEdge.Blocked"/>.</param>
	public static bool Reaches( int fromX, int fromY, int toX, int toY,
		Func<int, int, StepDirection, bool> blocked )
	{
		ArgumentNullException.ThrowIfNull( blocked );

		var line = new CellLine( fromX, fromY, toX, toY );

		var x = fromX;
		var y = fromY;
		var wasX = fromX;
		var wasY = fromY;

		// The original seeds this with -1, which no direction can equal. It is never read before the first
		// step has set it, because the flag below skips the only thing that would read it.
		var last = StepDirection.North;
		var first = true;

		while ( x != toX || y != toY )
		{
			var going = line.Next();

			if ( blocked( x, y, going ) )
				return false;

			if ( first )
				first = false;
			else if ( going != last && !RoundsTheCorner( x, y, wasX, wasY, going, last, blocked ) )
				return false;

			wasX = x;
			wasY = y;

			var (dx, dy) = CellLine.StepFor( going );

			x += dx;
			y += dy;

			last = going;
		}

		return true;
	}

	/// <summary>
	/// Whether a turn may be taken, asked of the cell being turned on and of the one just left.
	///
	/// <para>
	/// <b>This is the clause that had to be read off the disassembly rather than the decompiler</b>, and it
	/// is worth recording why. Two of its four questions are asked while arguments are already on the
	/// stack, so the offsets written down do not mean what they would mean anywhere else in the function -
	/// and the two reads of <c>[ESP+0x30]</c> four instructions apart are the <i>same literal offset</i>
	/// naming two different variables, because a push happens between them. Taken at face value the clause
	/// reads as being about one cell. It is about two.
	/// </para>
	/// </summary>
	private static bool RoundsTheCorner( int x, int y, int wasX, int wasY,
		StepDirection going, StepDirection last, Func<int, int, StepDirection, bool> blocked )
	{
		// Turning late: carry on past this cell the way we came, and round the corner from there.
		if ( !blocked( x, y, last ) )
		{
			var (dx, dy) = CellLine.StepFor( last );

			if ( blocked( x + dx, y + dy, going ) )
				return false;
		}

		// Turning early: round the corner from the cell we have just left, before reaching this one.
		if ( !blocked( wasX, wasY, going ) )
		{
			var (dx, dy) = CellLine.StepFor( going );

			if ( blocked( wasX + dx, wasY + dy, last ) )
				return false;
		}

		return true;
	}

	/// <summary>
	/// Drop every waypoint a straight line from the anchor already reaches, repeating whole passes until
	/// one of them changes nothing, and say whether anything was dropped at all.
	/// </summary>
	/// <param name="from">
	/// The first waypoint to consider. Everything before it is left exactly as it stands, which is what
	/// lets the search re-straighten only the part of a route it has just meddled with.
	/// </param>
	/// <param name="blocked">Whether that side of that cell is shut - <see cref="CellEdge.Blocked"/>.</param>
	public bool Straighten( int from, Func<int, int, StepDirection, bool> blocked )
	{
		ArgumentNullException.ThrowIfNull( blocked );
		ArgumentOutOfRangeException.ThrowIfNegative( from );

		var changedAnything = false;

		while ( true )
		{
			// The original writes the index straight into the count here. Where that count is the one it
			// already has - which is the only case its own three callers can produce, since they pass
			// either zero or an index they have just read out of the route - that changes nothing.
			if ( from >= Waypoints.Count )
				return changedAnything;

			var anchor = Before( from );
			var index = from;
			var kept = from;
			var changedThisPass = false;

			// The count is read once for the whole pass, as the original reads it: waypoints are written
			// back over the ones already passed, so the route never shrinks underneath the walk.
			var count = Waypoints.Count;

			while ( index < count )
			{
				var jumped = false;

				for ( var jump = MostToJump; jump >= 1; --jump )
				{
					if ( index + jump >= count )
						continue;

					var (toX, toY) = Waypoints[index + jump];

					if ( !Reaches( anchor.X, anchor.Y, toX, toY, blocked ) )
						continue;

					// Note what does NOT happen: the anchor stays where it is, and the waypoint just
					// reached is not written down. It only becomes the anchor once a jump from here fails.
					index += jump;
					changedThisPass = true;
					changedAnything = true;
					jumped = true;

					break;
				}

				if ( jumped )
					continue;

				anchor = Waypoints[index];
				Waypoints[kept] = anchor;

				++kept;
				++index;
			}

			Waypoints.RemoveRange( kept, Waypoints.Count - kept );

			if ( !changedThisPass )
				return changedAnything;
		}
	}
}
