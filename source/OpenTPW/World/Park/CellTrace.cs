namespace OpenTPW;

/// <summary>How a trace ended, if it has.</summary>
public enum TraceState
{
	/// <summary>Still feeling its way round.</summary>
	Running = 0,

	/// <summary>Come back to the line it left, which is what the caller is waiting for.</summary>
	Rejoined = 1,

	/// <summary>Run out of room to record where it has been.</summary>
	Full = 2
}

/// <summary>
/// Feeling a way round an obstacle by keeping a hand on it - the original's <c>FUN_00511c90</c>, and the
/// same algorithm written inline a second time inside the pathfinder with the turn reversed.
///
/// <para>
/// <b>This is what the pathfinder does instead of searching.</b> It walks straight at where it is going
/// (<see cref="CellLine"/>), and when the way is shut it sets two of these going, one turning each way,
/// and takes whichever comes back to the line first. There is no cost, no frontier and no heap anywhere
/// in it.
/// </para>
/// <para>
/// <b>The original keeps the facing and the ending in one field</b>, holding 0 to 3 while it runs and
/// <c>0x32</c> or <c>0x33</c> once it is done. They are separate here, and the two numbers are kept as
/// constants so the correspondence survives. That split forces one named departure, below.
/// </para>
/// <para>
/// <b>A departure, deliberately.</b> The original only refuses to run on when it has run out of room; a
/// trace that has already rejoined would, if stepped again, hand <c>0x32</c> to the edge test as though it
/// were a direction. Its own caller never does that. Here a finished trace of either kind does nothing,
/// which cannot change a run the original would have made and does stop a nonsense direction reaching the
/// map.
/// </para>
/// </summary>
public sealed class CellTrace
{
	/// <summary>The original's value for having come back to the line.</summary>
	public const int RejoinedMarker = 0x32;

	/// <summary>The original's value for having run out of room.</summary>
	public const int FullMarker = 0x33;

	/// <summary>How many cells it will record before giving up, which the original compares against.</summary>
	public const int Room = 0xfb;

	/// <summary>Where the trace has got to.</summary>
	public int X { get; private set; }

	/// <summary>Where the trace has got to.</summary>
	public int Y { get; private set; }

	/// <summary>Which way it is pointing.</summary>
	public StepDirection Facing { get; private set; }

	/// <summary>
	/// Which way it turns when it can: <c>1</c> or <c>-1</c>. The two traces the pathfinder sets going
	/// differ in this and in nothing else.
	/// </summary>
	public int Turn { get; }

	/// <summary>Where it set off from, which it must not stop on.</summary>
	public int StartX { get; }

	/// <summary>Where it set off from, which it must not stop on.</summary>
	public int StartY { get; }

	/// <summary>Where the walk as a whole is going, which is what "back to the line" is measured against.</summary>
	public int GoalX { get; }

	/// <summary>Where the walk as a whole is going.</summary>
	public int GoalY { get; }

	public TraceState State { get; private set; }

	/// <summary>Every cell it has written down, in order.</summary>
	public IReadOnlyList<(int X, int Y)> Been => _been;

	private readonly List<(int X, int Y)> _been = [];

	public CellTrace( int x, int y, StepDirection facing, int turn, int goalX, int goalY )
	{
		X = StartX = x;
		Y = StartY = y;
		Facing = facing;
		Turn = turn;
		GoalX = goalX;
		GoalY = goalY;
	}

	/// <summary>The direction this many quarter turns round from the one given.</summary>
	public static StepDirection TurnedFrom( StepDirection facing, int by )
		=> (StepDirection)(((int)facing + by) & 3);

	/// <summary>The way back.</summary>
	public static StepDirection Opposite( StepDirection facing ) => TurnedFrom( facing, 2 );

	/// <summary>
	/// Whether a point lies further along a direction than another does - the comparison both of the
	/// original's jump tables are made of, one taken straight and one taken reversed.
	/// </summary>
	public static bool Ahead( StepDirection direction, int fromX, int fromY, int atX, int atY )
		=> direction switch
		{
			StepDirection.North => atY < fromY,
			StepDirection.East => atX > fromX,
			StepDirection.South => atY > fromY,
			_ => atX < fromX
		};

	/// <summary>Whether a coordinate lies between two others, whichever way round they are.</summary>
	public static bool Between( int value, int one, int other )
		=> one <= other ? value >= one && value <= other : value >= other && value <= one;

	/// <summary>
	/// One tick of the trace.
	///
	/// <para>
	/// Shut ahead, and it turns <i>against</i> its own sense and writes down where it is standing without
	/// moving. Clear ahead, and it moves, then tries to turn <i>with</i> its sense - taking that turn only
	/// if the way is open, and writing itself down only when it does. So a straight run along a wall
	/// records nothing at all, and only the corners are kept.
	/// </para>
	/// </summary>
	/// <param name="blocked">Whether that side of that cell is shut - <see cref="CellEdge.Blocked"/>.</param>
	public void Step( Func<int, int, StepDirection, bool> blocked )
	{
		ArgumentNullException.ThrowIfNull( blocked );

		if ( State != TraceState.Running )
			return;

		if ( blocked( X, Y, Facing ) )
		{
			Facing = TurnedFrom( Facing, -Turn );
			Record();
		}
		else
		{
			var (dx, dy) = CellLine.StepFor( Facing );

			X += dx;
			Y += dy;

			var turned = TurnedFrom( Facing, Turn );

			if ( !blocked( X, Y, turned ) )
			{
				Facing = turned;
				Record();
			}
		}

		if ( State == TraceState.Full )
			return;

		// Not yet level with where the walk is going, on either of the two directions it is between.
		if ( Ahead( TurnedFrom( Facing, Turn ), X, Y, GoalX, GoalY ) )
			return;

		if ( Ahead( Opposite( Facing ), X, Y, GoalX, GoalY ) )
			return;

		if ( !Between( X, StartX, GoalX ) || !Between( Y, StartY, GoalY ) )
			return;

		// Back on the line - unless it has merely not left yet.
		if ( X != StartX || Y != StartY )
			State = TraceState.Rejoined;
	}

	private void Record()
	{
		_been.Add( (X, Y) );

		if ( _been.Count > Room )
			State = TraceState.Full;
	}
}
