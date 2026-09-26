namespace OpenTPW;

/// <summary>
/// Finding a way from one cell to another - the original's <c>FUN_00511470</c>, and the thing everything
/// that walks is waiting on.
///
/// <para>
/// <b>It is not a search, whatever the name suggests.</b> There is no open list, no cost, no frontier and
/// no heap anywhere in it. It walks straight at the goal (<see cref="CellLine"/>), and the moment the way
/// is shut it sets two <see cref="CellTrace"/>s going from that cell, one turning each way, and takes
/// whichever comes back to the line first. Then it straightens what it has
/// (<see cref="CellRoute.Straighten"/>), re-aims at the goal from the last waypoint it kept, and carries
/// on. A step budget is the only thing that stops it.
/// </para>
/// <para>
/// <b>The two traces differ in exactly two ways and the pairing is not symmetric</b>: one starts facing a
/// quarter turn <i>with</i> the direction of travel and turns by <c>-1</c>, the other a quarter turn
/// against it and turns by <c>+1</c>. The first is written out inline in the original and the second goes
/// through the shared routine, which is why it reads as two algorithms and is one. <b>The inline one is
/// asked first and so wins a tie</b>, on the same tick.
/// </para>
/// <para>
/// <b>Its corner rule is not the straightener's, but the difference is not where it first appears to
/// be.</b> Both ask the same two questions - can the turn be taken one cell late, or one cell early. The
/// real differences are <i>when</i> and <i>what for</i>: this one asks after taking the step, about the
/// cell just entered, and a yes writes a waypoint down rather than rejecting anything. A turn that
/// cannot be rounded smoothly is a corner worth keeping.
/// </para>
/// <para>
/// <b>The early question here can only ever be answered yes, and that is worth knowing before someone
/// tidies it away.</b> It is reached only when the way out of the entered cell by the old direction is
/// shut. What it then asks is whether that very side is shut - because the cell it measures from, plus
/// one step in the new direction, <i>is</i> the entered cell. Its other half asks about the edge the walk
/// has just crossed, which is open by construction or the walk would not be here. So both of its edge
/// tests have answers that are already known. They are kept because the original makes them, and the
/// if/else is an optimisation rather than a second opinion: asking the two ways round independently, as the
/// straightener does, gives the same answer every time.
/// </para>
/// </summary>
public sealed class CellSearch
{
	/// <summary>How many waypoints a route may already hold for the goal still to be written on the end.</summary>
	public const int Room = 0xfa;

	/// <summary>
	/// What a route and a trace's cells may come to between them, with the two extra the splice needs. The
	/// original compares against this signed, having widened both counts from bytes.
	/// </summary>
	public const int SpliceRoom = 0xfc;

	private readonly CellRoute _route;
	private readonly Func<int, int, StepDirection, bool> _blocked;
	private readonly int _budget;

	/// <summary>How many turns it has taken, which is what it answers with when it gets there.</summary>
	public int Steps { get; private set; }

	/// <param name="budget">The original passes 60000 from every one of its call sites.</param>
	/// <param name="blocked">Whether that side of that cell is shut - <see cref="CellEdge.Blocked"/>.</param>
	public CellSearch( CellRoute route, int budget, Func<int, int, StepDirection, bool> blocked )
	{
		_route = route ?? throw new ArgumentNullException( nameof( route ) );
		_blocked = blocked ?? throw new ArgumentNullException( nameof( blocked ) );
		_budget = budget;
	}

	/// <summary>
	/// Walk from here to the route's <see cref="CellRoute.Goal"/>, filling in its waypoints, and say
	/// whether it got there.
	///
	/// <para>
	/// <b>There are two kinds of failure and they do not leave the same thing behind.</b> Giving up part
	/// way - boxed in, out of room, or the two traces chasing each other - puts the cell it had reached on
	/// the end, because the caller walks what it is given either way. <b>Running out of turns does not</b>:
	/// that test sits at the top of the walk beside the arrival test, and writes nothing unless it has
	/// actually arrived. The asymmetry is the original's, not a convenience.
	/// </para>
	/// </summary>
	public bool Run( int fromX, int fromY )
	{
		var x = fromX;
		var y = fromY;
		var line = new CellLine( x, y, _route.Goal.X, _route.Goal.Y );
		var first = true;
		var last = StepDirection.North;

		while ( true )
		{
			if ( Steps >= _budget || (x == _route.Goal.X && y == _route.Goal.Y) )
				return Finish( x, y );

			var going = line.Next();

			if ( !_blocked( x, y, going ) )
			{
				var wasX = x;
				var wasY = y;
				var (dx, dy) = CellLine.StepFor( going );

				x += dx;
				y += dy;

				if ( first )
				{
					first = false;
				}
				else if ( going != last && IsACorner( x, y, wasX, wasY, going, last ) )
				{
					_route.Waypoints.Add( (x, y) );

					// The only cap on the walking half, and it is tested after the count has moved.
					if ( _route.Waypoints.Count > Room )
						return GiveUp( x, y );
				}

				last = going;
			}
			else
			{
				// The way is shut. Write down where we are - no cap is tested here - and feel round it.
				_route.Waypoints.Add( (x, y) );

				if ( !FeelRound( x, y, going ) )
					return GiveUp( x, y );

				if ( Steps == _budget )
					return GiveUp( x, y );

				// Straighten what we have, then aim again from the last waypoint we kept.
				_route.Straighten( 0, _blocked );

				(x, y) = _route.Waypoints[^1];
				line = new CellLine( x, y, _route.Goal.X, _route.Goal.Y );
				first = true;
			}

			++Steps;
		}
	}

	/// <summary>
	/// Whether the turn just taken is one worth writing down: a turn that can be started one way round but
	/// not finished that way. See the class remarks for how this differs from the straightener's.
	/// </summary>
	private bool IsACorner( int x, int y, int wasX, int wasY, StepDirection going, StepDirection last )
	{
		if ( !_blocked( x, y, last ) )
		{
			// Turning late can be started from the cell just entered - so it has to finish.
			var (dx, dy) = CellLine.StepFor( last );

			return _blocked( x + dx, y + dy, going );
		}

		// It cannot, so the early turn is the only one left to ask about.
		if ( _blocked( wasX, wasY, going ) )
			return false;

		var (edx, edy) = CellLine.StepFor( going );

		return _blocked( wasX + edx, wasY + edy, last );
	}

	/// <summary>
	/// Set two traces going from this cell, one turning each way, and splice in whichever rejoins the line
	/// first. False means neither did and there is nothing to carry on from.
	/// </summary>
	private bool FeelRound( int x, int y, StepDirection going )
	{
		// A quarter turn each way from the direction of travel, each turning the other way round.
		var alongside = new CellTrace( x, y, CellTrace.TurnedFrom( going, 1 ), -1,
			_route.Goal.X, _route.Goal.Y );

		var against = new CellTrace( x, y, CellTrace.TurnedFrom( going, -1 ), 1,
			_route.Goal.X, _route.Goal.Y );

		while ( true )
		{
			// The original guards the first of these and lets the second guard itself. Both do here.
			alongside.Step( _blocked );
			against.Step( _blocked );

			// Asked in this order, so a tie on the same tick goes to the one turning alongside.
			if ( alongside.State == TraceState.Rejoined )
				return Splice( alongside );

			// About to step back onto the cell the pair set off from, facing the way the other one faces:
			// the two are chasing each other round the same obstacle and neither will get anywhere.
			// <b>This is asked BEFORE the other trace is looked at</b>, so if that one rejoined on this
			// same tick it does not get to count - the original gives up instead.
			if ( alongside.State == TraceState.Running )
			{
				var (dx, dy) = CellLine.StepFor( alongside.Facing );

				if ( alongside.X + dx == x && alongside.Y + dy == y
					&& CellTrace.TurnedFrom( alongside.Facing, 2 ) == against.Facing )
					return false;
			}

			if ( against.State == TraceState.Rejoined )
				return Splice( against );

			if ( alongside.State == TraceState.Full && against.State == TraceState.Full )
				return false;

			++Steps;

			if ( Steps >= _budget )
				return false;
		}
	}

	/// <summary>Put a trace's corners on the end of the route, and where it finished after them.</summary>
	private bool Splice( CellTrace trace )
	{
		// The two extra are the cell the trace finished on and the one the walk will go on to write.
		if ( _route.Waypoints.Count + trace.Been.Count + 2 > SpliceRoom )
			return false;

		foreach ( var cell in trace.Been )
			_route.Waypoints.Add( cell );

		_route.Waypoints.Add( (trace.X, trace.Y) );

		return true;
	}

	/// <summary>Arriving, or running out of turns on the way.</summary>
	private bool Finish( int x, int y )
	{
		if ( _route.Waypoints.Count >= Room || x != _route.Goal.X || y != _route.Goal.Y )
			return false;

		_route.Waypoints.Add( (x, y) );

		return true;
	}

	/// <summary>Giving up, which still leaves the cell it had reached on the end of the route.</summary>
	private bool GiveUp( int x, int y )
	{
		_route.Waypoints.Add( (x, y) );

		return false;
	}
}
