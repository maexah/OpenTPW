namespace OpenTPW;

/// <summary>
/// Going back over a route and searching each waypoint away again - the original's <c>FUN_005108a0</c>,
/// and the last thing done to a route before anyone is asked to walk it.
///
/// <para>
/// <b>It works on one waypoint at a time, and what it tries is to delete it.</b> For each waypoint it
/// searches afresh between that waypoint's two <i>neighbours</i> - skipping the waypoint itself - and if
/// the search gets there, the answer takes the place of both the waypoint and the one after it. So a
/// waypoint only survives if no better way past it can be found.
/// </para>
/// <para>
/// <b>Three things stop a splice</b>, and the middle one is the interesting one: the search failing, the
/// answer being too long to fit, or <b>the answer running back through the very waypoint being cut out</b>
/// - which would trade a route for the same route. That test looks at every cell the search found
/// <i>except the last</i>, because the last one is the neighbour it was aiming at.
/// </para>
/// <para>
/// <b>A splice costs a round, and there are nine.</b> After one it backs up four waypoints, straightens
/// from there, and starts the scan again from the beginning. A scan that gets all the way through without
/// splicing anything is the other way out. The two exits are not equivalent in the original: a scan that
/// changes nothing falls into a further pass that breaks diagonal steps in two, and running out of rounds
/// returns without it. <b>That diagonal pass is not built yet</b> and is named here rather than quietly
/// skipped - see the class remarks on what it would do.
/// </para>
/// <para>
/// <b>The route's own <see cref="CellRoute.Goal"/> is never read here.</b> Every search this runs is
/// between two waypoints, so the goal only reaches it through the scratch route it builds for each one.
/// </para>
/// </summary>
public static class CellReroute
{
	/// <summary>How many splices it will make before it stops, spent only when one actually lands.</summary>
	public const int Rounds = 9;

	/// <summary>The step budget the original hands every search it starts, at all three of its call sites.</summary>
	public const int Budget = 60000;

	/// <summary>How far back it goes before straightening again, once a splice has landed.</summary>
	public const int BackUp = 4;

	/// <summary>
	/// Improve this route in place. It is straightened first, because the original straightens before it
	/// looks at anything.
	/// </summary>
	/// <param name="blocked">Whether that side of that cell is shut - <see cref="CellEdge.Blocked"/>.</param>
	public static void Run( CellRoute route, Func<int, int, StepDirection, bool> blocked )
	{
		ArgumentNullException.ThrowIfNull( route );
		ArgumentNullException.ThrowIfNull( blocked );

		route.Straighten( 0, blocked );

		var rounds = Rounds;

		while ( true )
		{
			var index = 0;
			var spliced = false;

			while ( index + 1 < route.Waypoints.Count )
			{
				var from = route.Before( index );
				var found = new CellRoute { Start = from, Goal = route.Waypoints[index + 1] };

				var reached = new CellSearch( found, Budget, blocked ).Run( from.X, from.Y );

				if ( !reached
					|| GoesBackThrough( found, route.Waypoints[index] )
					|| route.Waypoints.Count + found.Waypoints.Count - 2 > CellSearch.SpliceRoom )
				{
					++index;

					continue;
				}

				// The answer stands in for this waypoint and the one after it, which is the one it was
				// aiming at and so is already the last cell of the answer.
				route.Waypoints.RemoveRange( index, 2 );
				route.Waypoints.InsertRange( index, found.Waypoints );

				route.Straighten( index < BackUp ? 0 : index - BackUp, blocked );

				spliced = true;

				break;
			}

			// A whole scan that spliced nothing is the end of it.
			if ( !spliced )
				return;

			if ( --rounds == 0 )
				return;
		}
	}

	/// <summary>
	/// Whether a search's answer passes through the waypoint it was supposed to make unnecessary. Every
	/// cell but the last is considered - the last is the neighbour it was aiming at.
	/// </summary>
	private static bool GoesBackThrough( CellRoute found, (int X, int Y) waypoint )
	{
		for ( var i = 0; i + 1 < found.Waypoints.Count; ++i )
		{
			if ( found.Waypoints[i] == waypoint )
				return true;
		}

		return false;
	}
}
