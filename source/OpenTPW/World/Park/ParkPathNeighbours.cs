namespace OpenTPW;

/// <summary>
/// Joins a cell that has just become path to the cells around it - the original's
/// <c>FUN_005348d0</c>, and the answer to a question this project carried open for a long time.
///
/// <para>
/// <b>It is INCREMENTAL and ORDER-DEPENDENT, which is why no rule read off a finished map could ever
/// reproduce it.</b> A sweep of member sets against diagonal rules tops out at <b>67/78</b> on the shipped
/// park; splitting the member set so cardinals admit <c>{1,9,10}</c> and diagonals only <c>{1}</c> reaches
/// <b>73/78</b> and stops there (<c>docs/exe/park-engine.md</c>, "Why no sweep could ever reproduce
/// `mNeighbours`"). Neither can reach 78, because this is not a function of the map at all - it is a pass run
/// over one cell at the moment that cell is created, and the result depends on what already existed.
/// <b>Validate it by replaying creation order, never by evaluating a predicate over a finished park.</b>
/// </para>
///
/// <para>
/// <b>The caller stamps the type first, then calls this.</b> That is the original's order too: the
/// cell-op sequence is clear, stamp, <i>join</i>, flow direction, fix-ups, owner, retile.
/// </para>
/// </summary>
public static class ParkPathNeighbours
{
	/// <summary>The cell type a path is - <see cref="CellEdge.Path"/>, kept as one statement.</summary>
	private const int PathType = CellEdge.Path;

	/// <summary>A queue cell, which never forms a new link - <see cref="ParkRideChoice.QueueCellType"/>.</summary>
	private const int QueueType = ParkRideChoice.QueueCellType;

	/// <summary>
	/// The eight directions in <b>ring order</b>, each with the step it stands for. Measured three
	/// independent ways from the executable's own static initialisers (<c>0x007cdba0</c> onward), the
	/// switch in <c>FUN_004d97e0</c>, and each initialiser's init-once guard bit.
	///
	/// <para>
	/// <b>North is -Y.</b> This is the OUTWARD sense, and it is the mirror of
	/// <see cref="CellEdge.BitFor"/> - which answers about the cell being <i>entered</i>, on the side
	/// facing the cell being left. Both are right and mixing them inverts every answer, so this table
	/// is written out rather than shared. <see cref="CellEdge.Opposite"/> IS shared, because a nibble
	/// swap means the same thing in either sense.
	/// </para>
	/// </summary>
	private static readonly (int Bit, int AcrossBy, int DownBy)[] Ring =
	[
		(0x01, 0, -1),   // N
		(0x02, 1, -1),   // NE
		(0x04, 1, 0),    // E
		(0x08, 1, 1),    // SE
		(0x10, 0, 1),    // S
		(0x20, -1, 1),   // SW
		(0x40, -1, 0),   // W
		(0x80, -1, -1)   // NW
	];

	/// <summary>Whether a ring bit is one of the four cardinals rather than a diagonal.</summary>
	private static bool IsCardinal( int bit ) => bit is 0x01 or 0x04 or 0x10 or 0x40;

	/// <summary>
	/// Joins a cell that has just become path to everything around it.
	///
	/// <para>
	/// <b>The eight blocks run INTERLEAVED in ring order</b> - north cardinal, north-east diagonal,
	/// east cardinal, south-east diagonal, and so on - and only then the prune. The first reading of
	/// this had all four cardinals and then all four diagonals, which is a different pass; it happens
	/// to be benign in this function because the two families read different fields, but it is not
	/// what the original does and a later change could make the difference matter.
	/// </para>
	/// </summary>
	public static void LinkPath( ParkState state, ParkWorld park, int x, int y )
	{
		if ( state == null || park == null || !ParkState.OnMap( x, y ) )
			return;

		// A path laid over a queue cell DEMOTES it before any neighbour work - the original does this
		// at the top of the same function (0x00534906..0x00534913), so it happens in the linker as well as
		// in the stamp. It writes the type and nothing else: the direction, the flags and the owner stay.
		var self = ParkState.CellFor( park, x, y );

		if ( self.Type == QueueType )
			state.SetRecord( x, y, self with { Type = PathType } );

		foreach ( var (bit, acrossBy, downBy) in Ring )
		{
			if ( IsCardinal( bit ) )
				Cardinal( state, park, x, y, bit, acrossBy, downBy );
			else
				StrictDiagonal( state, park, x, y, bit, acrossBy, downBy );
		}

		Prune( state, park, x, y );
	}

	/// <summary>
	/// One cardinal side. <b>Whether it connects at all is decided by the NEIGHBOUR'S TYPE, and for a
	/// ride's own cells by that cell's direction byte</b> - which is the whole of why a sweep over
	/// types alone could not fit the shipped park.
	/// </summary>
	/// <remarks>
	/// Measured in all four cardinal blocks: type 1 links unconditionally; type 10 links only when
	/// <c>nb.Direction &amp; Opposite(D)</c>; type 9 links only when <c>nb.Direction &amp; D</c> - the
	/// two test <b>opposite senses</b> of the same byte; and type 3 <b>never forms a new link</b>, it
	/// only lets the neighbour retile. A link is symmetric: this cell gains D and the neighbour gains
	/// the opposite bit.
	/// </remarks>
	private static void Cardinal( ParkState state, ParkWorld park, int x, int y,
		int bit, int acrossBy, int downBy )
	{
		var (nx, ny) = (x + acrossBy, y + downBy);

		if ( !ParkState.OnMap( nx, ny ) )
			return;

		var nb = ParkState.CellFor( park, nx, ny );
		var back = CellEdge.Opposite( bit );

		// A queue already joined this way is retiled - which the caller's RetileAround does - with a
		// sound (0x8b), and is not linked again.
		if ( nb.Type == QueueType && (nb.Neighbours & back) != 0 )
			Unimplemented.Report( "PATH_LINK_SOUND_0x8B" );

		var links = nb.Type switch
		{
			PathType => true,
			CellEdge.RideFarEnd => (nb.Direction & back) != 0,
			CellEdge.RideEnd => (nb.Direction & bit) != 0,
			_ => false
		};

		if ( !links )
			return;

		var self = ParkState.CellFor( park, x, y );

		state.SetRecord( x, y, self with { Neighbours = (byte)(self.Neighbours | bit) } );
		state.SetRecord( nx, ny, nb with { Neighbours = (byte)(nb.Neighbours | back) } );

		WeakFixUp( state, park, nx, ny, back );
	}

	/// <summary>
	/// The one-sided diagonal fix-up applied to the NEIGHBOUR that has just gained this cell.
	///
	/// <para>
	/// <b>It is weaker than the strict rule and it sets ONE bit</b>, never the partner bit on the far
	/// cell - which is why a park's <c>mNeighbours</c> is legitimately <b>asymmetric</b> and why trying
	/// to "repair" it into a symmetric relation would be wrong. The intervening cardinal need only be
	/// <b>not a queue and not a ride entrance</b> here, where the strict rule below demands it be path.
	/// </para>
	/// </summary>
	private static void WeakFixUp( ParkState state, ParkWorld park, int nx, int ny, int back )
	{
		var at = IndexOf( back );

		// The two diagonals either side of the direction pointing back at the cell just linked, each
		// with the OTHER cardinal it sits between.
		foreach ( var (diagAt, interAt) in new[] { ((at + 1) % 8, (at + 2) % 8), ((at + 7) % 8, (at + 6) % 8) } )
		{
			var (diag, dax, day) = Ring[diagAt];
			var (_, iax, iay) = Ring[interAt];

			if ( !ParkState.OnMap( nx + dax, ny + day ) || !ParkState.OnMap( nx + iax, ny + iay ) )
				continue;

			if ( ParkState.CellFor( park, nx + dax, ny + day ).Type != PathType )
				continue;

			var between = ParkState.CellFor( park, nx + iax, ny + iay ).Type;

			if ( between == QueueType || between == CellEdge.RideEnd )
				continue;

			var nb = ParkState.CellFor( park, nx, ny );

			state.SetRecord( nx, ny, nb with { Neighbours = (byte)(nb.Neighbours | diag) } );
		}
	}

	/// <summary>
	/// One diagonal, by the strict rule: the diagonal cell <b>and both cells between</b> must all be
	/// path, all three looked up from this cell, and the pair that results is symmetric.
	/// </summary>
	private static void StrictDiagonal( ParkState state, ParkWorld park, int x, int y,
		int bit, int acrossBy, int downBy )
	{
		var at = IndexOf( bit );
		var (_, ax, ay) = Ring[(at + 1) % 8];
		var (_, bx, by) = Ring[(at + 7) % 8];

		foreach ( var (dx, dy) in new[] { (acrossBy, downBy), (ax, ay), (bx, by) } )
		{
			if ( !ParkState.OnMap( x + dx, y + dy ) )
				return;

			if ( ParkState.CellFor( park, x + dx, y + dy ).Type != PathType )
				return;
		}

		var self = ParkState.CellFor( park, x, y );
		var far = ParkState.CellFor( park, x + acrossBy, y + downBy );

		state.SetRecord( x, y, self with { Neighbours = (byte)(self.Neighbours | bit) } );
		state.SetRecord( x + acrossBy, y + downBy,
			far with { Neighbours = (byte)(far.Neighbours | CellEdge.Opposite( bit )) } );
	}

	/// <summary>
	/// The closing pass: <b>for every cardinal link that points at a queue or a ride entrance, the two
	/// diagonals flanking it are CLEARED.</b>
	///
	/// <para>
	/// This is the half that no member-set sweep could ever express, because it takes bits away again.
	/// Measured byte-exact from the original's own jump and index tables: north clears NE and NW, east
	/// clears NE and SE, south clears SE and SW, west clears NW and SW.
	/// </para>
	/// </summary>
	private static void Prune( ParkState state, ParkWorld park, int x, int y )
	{
		foreach ( var (bit, acrossBy, downBy) in Ring )
		{
			if ( !IsCardinal( bit ) )
				continue;

			var self = ParkState.CellFor( park, x, y );

			if ( (self.Neighbours & bit) == 0 || !ParkState.OnMap( x + acrossBy, y + downBy ) )
				continue;

			var target = ParkState.CellFor( park, x + acrossBy, y + downBy ).Type;

			if ( target != QueueType && target != CellEdge.RideEnd )
				continue;

			var at = IndexOf( bit );
			var clear = Ring[(at + 1) % 8].Bit | Ring[(at + 7) % 8].Bit;

			state.SetRecord( x, y, self with { Neighbours = (byte)(self.Neighbours & ~clear) } );
		}
	}

	/// <summary>Where a bit sits in <see cref="Ring"/>, which is what makes "the side next to it" sayable.</summary>
	private static int IndexOf( int bit )
	{
		for ( var at = 0; at < Ring.Length; ++at )
		{
			if ( Ring[at].Bit == bit )
				return at;
		}

		return 0;
	}
}
