namespace OpenTPW;

/// <summary>Which way a step across the map goes. The numbers are the original's own.</summary>
public enum StepDirection
{
	North = 0,
	East = 1,
	South = 2,
	West = 3
}

/// <summary>
/// Whether a person may step from one cell to the next - the geometry of the original's
/// <c>FUN_00510510</c>, which is the check that owns the complaint <i>"A peep is walking so fast that
/// they tried to cross &gt; 1 cell in one step"</i>.
///
/// <para>
/// <b>The edge test itself is taken as a parameter</b>, so that this stays geometry over cell numbers
/// and nothing else; <see cref="CellEdge"/> is the real one. The original asks
/// <c>FUN_004d8750(x, y, direction, context)</c> whether a particular side of a particular cell is
/// closed, and most of that function compares the cell's type against a literal. Two cell fields decide
/// the rest, read in two different ways: <c>mNeighbours</c> is <b>bit-tested</b>, and the test reports
/// blocked when the bit is <i>clear</i> - so it says which ways a cell connects, not which ways it is
/// walled - while <c>mDirection</c> is compared for <b>equality</b> against a single value and is never
/// masked. The shipped park bears that out: <c>mNeighbours</c> takes 38 values with 82 cells carrying
/// more than one bit, and <c>mDirection</c> takes five and never carries two.
/// </para>
/// <para>
/// <b>The bit is read from the cell being ENTERED, about the side facing the cell being left.</b> The
/// numbering is fixed absolutely by
/// the original's own boundary guards - it refuses <c>x == 0</c> going 3, <c>y == 0</c> going 0,
/// <c>x == 0x7f</c> going 1 and <c>y == 0x7f</c> going 2 - so direction 0 is <c>-y</c>, 1 is <c>+x</c>,
/// 2 is <c>+y</c> and 3 is <c>-x</c>. Asked about direction 0 the original tests <c>0x10</c>, which is
/// the <c>+y</c> bit; read as a question about the cell being <i>left</i> that is its far side. It is
/// not that: the disassembly
/// loads the <b>destination</b> cell into <c>ECX</c> before the call, and the destination's <c>+y</c>
/// side is precisely the side facing the source. <b>No measurement on this park can tell those two
/// readings apart</b>, because <c>mNeighbours</c> is symmetric across all 65,024 of its adjacent pairs -
/// both score identically, so only the disassembly settles it.
/// </para>
/// <para>
/// <b>The names below are labels, and the axes are the definition.</b> The original names its directions
/// nothing at all - they are 0 to 3 - and whether <c>-y</c> is north or south is not something this park
/// answers: its entrance stands at <c>(47,17)</c> and <c>(48,17)</c> with the bus outside the map at
/// <c>(29.7,-11.5)</c>, so guests walk up from low <c>y</c>, which fixes the geometry without fixing a
/// compass. Every step computed here is in terms of the axes, so the labels can be read either way
/// round without changing a single answer.
/// </para>
/// <para>
/// <b>One guard is deliberately missing rather than guessed.</b> The original refuses a step where the
/// source row is zero and the destination row reads <c>0x3ff</c>, which looks like an off-the-map check.
/// As decompiled that value cannot occur - the destination is masked to sixteen bits before the shift,
/// which caps it at <c>0x1ff</c> - so rather than invent an interpretation that fits, it is recorded as
/// unexplained and left out.
/// </para>
/// </summary>
public static class MapStep
{
	/// <summary>How many cells across the map is.</summary>
	public const int MapSize = ParkWorld.MapSize;

	/// <summary>The highest cell coordinate, which is also where the map's far edges are.</summary>
	public const int LastCell = MapSize - 1;

	/// <summary>
	/// A cell's number, which is its position counted from one rather than zero - the <c>+1</c> is why a
	/// cell id and a cell index are not the same number, and reading one as the other is off by one
	/// everywhere while still looking plausible.
	/// </summary>
	public static int CellId( int x, int y ) => y * MapSize + x + 1;

	/// <summary>Where a cell number sits on the map.</summary>
	public static (int X, int Y) CellAt( int cellId )
	{
		var index = (cellId & 0xffff) - 1;

		return (index & (MapSize - 1), index >> 7);
	}

	/// <summary>
	/// The cell a step in this direction arrives at.
	///
	/// <para>
	/// These four agree with the executable's own step table at <c>0x007622b0</c>, which
	/// <see cref="CellLine.StepFor"/> carries - a second source for a numbering the boundary guards
	/// also fix.
	/// </para>
	/// </summary>
	public static (int X, int Y) Beyond( int x, int y, StepDirection direction ) => direction switch
	{
		StepDirection.North => (x, y - 1),
		StepDirection.East => (x + 1, y),
		StepDirection.South => (x, y + 1),
		_ => (x - 1, y)
	};

	/// <summary>
	/// Whether this side of this cell is the outside of the map. The original tests these four before it
	/// looks at anything the map holds, so the boundary closes every cell on it regardless of what is
	/// built there.
	/// </summary>
	public static bool LeavesTheMap( int x, int y, StepDirection direction ) => direction switch
	{
		StepDirection.North => y == 0,
		StepDirection.East => x == LastCell,
		StepDirection.South => y == LastCell,
		_ => x == 0
	};

	/// <summary>
	/// Whether these two cells are further apart than one step, in which case the original refuses the
	/// move and says so in the log rather than letting a person teleport.
	/// </summary>
	public static bool TooFarInOneStep( int fromCellId, int toCellId )
	{
		var (fromX, fromY) = CellAt( fromCellId );
		var (toX, toY) = CellAt( toCellId );

		return Away( fromX, toX ) > 1 || Away( fromY, toY ) > 1;
	}

	private static int Away( int a, int b ) => a > b ? a - b : b - a;

	/// <summary>
	/// Whether a person may make this step, given something that can say whether a side of a cell is
	/// closed.
	///
	/// <para>
	/// A straight step asks about the one side it crosses. <b>A diagonal asks about four</b>, because
	/// there is no such thing as a diagonal edge: the person has to get round the corner one way or the
	/// other, so the step is allowed if either of the two L-shaped routes is clear. The original tries the
	/// <b>lower-numbered direction first</b> - north before east, north before west, south before west,
	/// but east before south - which is worth stating because three of the four look like "up or down
	/// first" and the fourth is not.
	/// </para>
	/// </summary>
	/// <param name="edgeBlocked">Whether that side of the cell at those coordinates is closed.</param>
	public static bool CanStep( int fromCellId, int toCellId,
		Func<int, int, StepDirection, bool> edgeBlocked )
	{
		ArgumentNullException.ThrowIfNull( edgeBlocked );

		if ( TooFarInOneStep( fromCellId, toCellId ) )
			return false;

		var (x, y) = CellAt( fromCellId );
		var (toX, toY) = CellAt( toCellId );

		var north = y - toY == 1;
		var east = toX - x == 1;
		var south = toY - y == 1;
		var west = x - toX == 1;

		var crossings = (north ? 1 : 0) + (east ? 1 : 0) + (south ? 1 : 0) + (west ? 1 : 0);

		if ( crossings != 2 )
		{
			// A step straight across one side, or no step at all.
			foreach ( var (going, direction) in new[]
			{
				(north, StepDirection.North), (east, StepDirection.East),
				(south, StepDirection.South), (west, StepDirection.West)
			} )
			{
				if ( going && edgeBlocked( x, y, direction ) )
					return false;
			}

			return true;
		}

		var updown = north ? StepDirection.North : StepDirection.South;
		var across = east ? StepDirection.East : StepDirection.West;

		var first = (int)updown < (int)across ? updown : across;
		var second = first == updown ? across : updown;

		// Round the corner one way...
		var (afterFirstX, afterFirstY) = Beyond( x, y, first );

		if ( !edgeBlocked( x, y, first ) && !edgeBlocked( afterFirstX, afterFirstY, second ) )
			return true;

		// ...or the other, which needs the second side open from here before it is worth asking.
		if ( edgeBlocked( x, y, second ) )
			return false;

		var (afterSecondX, afterSecondY) = Beyond( x, y, second );

		return !edgeBlocked( afterSecondX, afterSecondY, first );
	}
}
