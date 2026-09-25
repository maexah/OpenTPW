namespace OpenTPW;

/// <summary>
/// Where a place in a queue stands on the ground - the original's <c>FUN_004de7e0</c>, which takes
/// <c>FUN_004de840</c> for a thing with a queue path (<see cref="ParkWorld.CatalogueObject.QueuePathFlag"/>) and
/// <c>FUN_004dec30</c> for one without. See <c>docs/exe/ride-operation.md</c>, "Walking to a new place in the
/// queue".
/// </summary>
/// <remarks>
/// A point is a cell and two sub bytes, each 0..255 of the cell: one <b>along</b> the queue, four places a cell at
/// 0, 63, 127 and 191 back from the front edge, and one <b>across</b> it, the jitter. A queue cell's
/// <c>mDirection</c> points at the front, so the direction decides which sub byte is which and from which edge
/// "along" is counted.
/// </remarks>
public static class ParkQueuePlace
{
	/// <summary>The least jitter across a queue - <c>rand % 28 + 114</c>, <c>ADD BL,0x72</c> at <c>0x004de8d8</c>.</summary>
	public const int JitterFrom = 114;

	/// <summary>How many jitters there are - the <c>28</c> of <c>rand % 28 + 114</c>, an unsigned <c>DIV</c>.</summary>
	public const int JitterSpread = 28;

	/// <summary>The middle of a cell, where a point stands when the direction switch writes neither sub byte.</summary>
	public const int Centre = 0x80;

	/// <summary>
	/// A point in a cell. <see cref="Written"/> is false where the direction switch wrote neither sub byte; the
	/// original then routes with whatever its stack held, and this stands the point at <see cref="Centre"/>.
	/// </summary>
	public readonly record struct Point( int Cell, int SubX, int SubY, bool Written )
	{
		/// <summary>
		/// The point in the simulation's 16.16: a sub byte s lands at s/256 of the cell, as
		/// <c>FUN_00510100( X &lt;&lt; 8, Y &lt;&lt; 8 )</c> places the 8.8 words <c>FUN_00501160</c> packs.
		/// </summary>
		public FixedVector Position
		{
			get
			{
				var (x, y) = MapStep.CellAt( Cell );

				return new FixedVector( (x << 16) | (SubX << 8), (y << 16) | (SubY << 8) );
			}
		}
	}

	/// <summary>The jitter from one draw of the generator - <c>draw % 28 + 114</c>, unsigned.</summary>
	public static int Jitter( int draw ) => (int)((uint)draw % JitterSpread) + JitterFrom;

	/// <summary>
	/// How far back from the front edge place <paramref name="n"/> stands, in 256ths of a cell -
	/// <c>(u8)__ftol( (u64)n × 0.25f × 255.0f )</c>, which is <c>((n × 255) &gt;&gt; 2) &amp; 0xff</c>: 0, 63, 127
	/// and 191 for 0 to 3, then 255, 62, 126 ...
	/// </summary>
	public static int Along( int n ) => (int)(((ulong)(uint)n * 255) >> 2) & 0xff;

	/// <summary>
	/// Where <paramref name="place"/> in <paramref name="item"/>'s queue stands, with <paramref name="jitter"/>
	/// across it. Cell nought is a place past the queue's cells, or a queue with no cells: the original packs it
	/// as x 127, y 255, which no route reaches.
	/// </summary>
	public static Point For( ParkWorld? park, ParkWorld.CatalogueObject item, int place, int jitter )
		=> item.HasQueuePath
			? AlongThePath( park, item, place, jitter )
			: InTheBackCell( park, item, place, jitter );

	/// <summary>
	/// <c>FUN_004de840</c>: from the front cell (<see cref="ParkRideChoice.StartOfQueue"/>), one queue cell further
	/// back for every four places. The first three places of a cell face this cell's own direction; the fourth,
	/// along above 128, turns to the next queue cell's, or at the back cell to the side opposite the first its path
	/// joins (<see cref="BackEnd"/>).
	/// </summary>
	private static Point AlongThePath( ParkWorld? park, ParkWorld.CatalogueObject item, int place, int jitter )
	{
		var cell = ParkRideChoice.StartOfQueue( park, item );
		var n = (uint)place;

		while ( n >= ParkRideChoice.QueueRoomPerCell && cell != 0 )
		{
			n -= ParkRideChoice.QueueRoomPerCell;
			cell = ParkRideChoice.StepToNextQueueCell( park, cell );
		}

		if ( cell == 0 )
			return new Point( 0, Centre, Centre, Written: false );

		var along = Along( (int)n );

		if ( along <= 0x80 )
			return Facing( cell, DirectionOf( park, cell ), along, jitter, turning: false );

		var next = ParkRideChoice.StepToNextQueueCell( park, cell );

		return Facing( cell, next != 0 ? DirectionOf( park, next ) : BackEnd( park, cell ), along, jitter,
			turning: true );
	}

	/// <summary>
	/// <c>FUN_004dec30</c>: every place in the back-of-queue cell (<see cref="ParkRideChoice.QueueCellsFor"/>),
	/// facing the ENTRY cell's direction (<c>0x004decd1</c>), with the whole place as n - no walk, no bound, so a
	/// fifth guest wraps to along 255 on the same cell.
	/// </summary>
	private static Point InTheBackCell( ParkWorld? park, ParkWorld.CatalogueObject item, int place, int jitter )
	{
		var (back, _) = ParkRideChoice.QueueCellsFor( park, item );

		if ( back == 0 )
			return new Point( 0, Centre, Centre, Written: false );

		var entry = ParkState.OnMap( item.EntryCellX, item.EntryCellY )
			? ParkState.CellFor( park, item.EntryCellX, item.EntryCellY ).Direction
			: 0;

		return Facing( back, entry, Along( place ), jitter, turning: false );
	}

	/// <summary>
	/// The two sub bytes for a direction, from the place's along and the jitter. The two switches of
	/// <c>FUN_004de840</c> differ only at <c>0x04</c>: the turning one adds 128 (<c>ADD AL,0x80</c> at
	/// <c>0x004deb26</c>), so 191 gives 63 where the other gives 64. Any other direction writes neither byte; the
	/// first switch logs <c>"Dodgy cell direction"</c> through the bare <c>RET</c>.
	/// </summary>
	private static Point Facing( int cell, int direction, int along, int jitter, bool turning ) => direction switch
	{
		0x01 => new Point( cell, jitter, along, Written: true ),
		0x04 => new Point( cell, turning ? (along + 0x80) & 0xff : 0xff - along, jitter, Written: true ),
		0x10 => new Point( cell, jitter, 0xff - along, Written: true ),
		0x40 => new Point( cell, along, jitter, Written: true ),
		_ => new Point( cell, Centre, Centre, Written: false )
	};

	/// <summary>
	/// The direction the fourth place of the back cell turns to - the side OPPOSITE the first of N, E, S and W
	/// (<c>01 04 10 40</c>, <c>0x004dea15</c>) that the cell's <c>mNeighbours</c> holds and whose neighbour is
	/// plain path (<c>FUN_00536310</c>); with none, the cell's own direction.
	/// </summary>
	private static int BackEnd( ParkWorld? park, int cell )
	{
		var (x, y) = MapStep.CellAt( cell );

		if ( !ParkState.OnMap( x, y ) )
			return 0;

		var here = ParkState.CellFor( park, x, y );

		for ( var side = 0; side < Compass.Length; ++side )
		{
			var (bit, acrossBy, downBy) = Compass[side];

			// Off the map the original reads GetNeighbouringCell's NULL as a cell (0x004dea4e) and faults; this
			// takes that side as not path.
			if ( (here.Neighbours & bit) == 0 || !ParkState.OnMap( x + acrossBy, y + downBy ) )
				continue;

			if ( ParkState.CellFor( park, x + acrossBy, y + downBy ).Type == CellEdge.Path )
				return Compass[(side + 2) % Compass.Length].Bit;
		}

		return here.Direction;
	}

	/// <summary>The four sides <see cref="BackEnd"/> asks, in its order: N, E, S, W, each opposite the one two on.</summary>
	private static readonly (int Bit, int AcrossBy, int DownBy)[] Compass =
	[
		(0x01, 0, -1),
		(0x04, 1, 0),
		(0x10, 0, 1),
		(0x40, -1, 0)
	];

	/// <summary>A queue cell's <c>mDirection</c>, as the running park holds it, or nought off the map.</summary>
	private static int DirectionOf( ParkWorld? park, int cell )
	{
		var (x, y) = MapStep.CellAt( cell );

		return ParkState.OnMap( x, y ) ? ParkState.CellFor( park, x, y ).Direction : 0;
	}
}
