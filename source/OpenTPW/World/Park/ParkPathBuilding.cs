namespace OpenTPW;

/// <summary>
/// Laying and lifting path, which is the verb the player actually performs - the original's
/// <c>FUN_005346d0</c> (the stamp) driven along a line by <c>FUN_00536100</c>.
///
/// <para>
/// <b>There is no drag.</b> Both drag slots of the build-tool interaction mode are bare <c>RET</c>
/// stubs, so a run of path is <b>click to anchor, click to commit</b>: the second click snaps its
/// target to the dominant axis and the line is walked one cell at a time. The action recorder
/// corroborates it from the other side - one record per click carrying a single cell, where a real
/// drag would have to record every intermediate cell or a start/end pair.
/// </para>
///
/// <para>
/// <b>The order inside one cell is the original's and it is not arbitrary:</b> the type gate first,
/// then the same-type shortcut, then the price, then affordability, then the write, and only then the
/// debit. Money moves <i>after</i> the cell changes, and a cell that is already path costs nothing at
/// all - so dragging back over your own path is free, which is a thing a player will do constantly.
/// </para>
/// </summary>
public static class ParkPathBuilding
{
	/// <summary>The cell type a path is.</summary>
	private const int PathType = CellEdge.Path;

	/// <summary>Bare ground, and the type a lifted cell goes back to.</summary>
	private const int NothingType = CellEdge.Nothing;

	/// <summary>
	/// What one path cell costs - <c>Costs.PathCell</c>, <b>20</b> in Lost Kingdom.
	/// </summary>
	/// <remarks>
	/// <b>Measured from the game's own data, which is the only place it exists.</b> The executable
	/// holds the key names and an uninitialised dword; the value is read wholesale from
	/// <c>data/levels/Standard.sam</c>, where <c>Costs.PathCell</c> is 20 and <c>Costs.QueueCell</c>
	/// is 75. Jungle's <c>Easy_Standard.sam</c> overrides <c>MapCell</c>, <c>KartTrackCell</c> and
	/// <c>WaterTrackCell</c> but <b>not</b> those two, so 20 stands for this park.
	/// </remarks>
	public const string PathCellCostKey = "Costs.PathCell";

	/// <summary>The fallback if the balance file is missing the key, which no shipped theme is.</summary>
	private const int PathCellCostFallback = 20;

	/// <summary>What one path cell costs in the park now loaded.</summary>
	public static int CellCost( Level? level )
		=> level?.Balance?.Int( PathCellCostKey, PathCellCostFallback ) ?? PathCellCostFallback;

	/// <summary>
	/// Whether a cell may become <paramref name="wanted"/> - the original's <c>FUN_00535600</c>, the
	/// gate the stamp consults before anything else happens.
	/// </summary>
	/// <remarks>
	/// It permits only: clearing to nothing; the type it already is; building on bare ground; path
	/// over queue; a footprint over path; and a coaster footprint over itself. <b>Notably 4-over-4 is
	/// refused</b>, which is a carve-out from the "same type is allowed" rule it otherwise follows.
	/// </remarks>
	public static bool MayBecome( int existing, int wanted )
	{
		if ( wanted == NothingType )
			return true;

		if ( wanted == CellEdge.Footprint && existing == CellEdge.Footprint )
			return false;

		if ( wanted == existing || existing == NothingType )
			return true;

		if ( wanted == PathType && existing == ParkRideChoice.QueueCellType )
			return true;

		if ( wanted == CellEdge.Footprint && existing == PathType )
			return true;

		return wanted == 0x15 && existing == 0x15;
	}

	/// <summary>
	/// Lays one cell of path, and answers a line saying what happened.
	///
	/// <para>
	/// <b>A cell that is already path costs nothing and changes nothing</b>, which is the original's
	/// own shortcut: it bumps a re-stamp counter and returns success before any price is fetched. This
	/// keeps that meaning without the counter, which nothing here reads yet.
	/// </para>
	/// </summary>
	public static string Lay( int cellX, int cellY )
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state || level.Park is not { } park )
			return "path: a park has to be loaded";

		if ( !ParkState.OnMap( cellX, cellY ) )
			return $"path: ({cellX},{cellY}) is off the map";

		var cell = ParkState.CellFor( park, cellX, cellY );

		// Already path: free, and the original returns success rather than refusing.
		if ( cell.Type == PathType )
			return $"path: ({cellX},{cellY}) is already path - nothing charged";

		if ( !MayBecome( cell.Type, PathType ) )
			return $"path: ({cellX},{cellY}) is type {cell.Type}, which path may not be laid over";

		// NOMODIFY marks a cell the level owns. It covers 18 of Lost Kingdom's 78 path cells, NOT all
		// of them - see the remarks on NoModify, which record why the two differ.
		if ( (cell.Flags & NoModify) != 0 )
			return $"path: ({cellX},{cellY}) is marked NOMODIFY - the level owns that cell";

		var price = CellCost( level );

		if ( state.Balance < price )
			return $"path: a cell costs {price} and the park has {state.Balance}";

		// The write, then the money - the original's order.
		state.SetRecord( cellX, cellY, cell with { Type = PathType, TileSet = ParkPaths.PathTileSet } );

		ParkPathNeighbours.LinkPath( state, park, cellX, cellY );

		state.Spend( price );

		RetileAround( state, park, cellX, cellY );

		ParkSurfaces.Rebuild();

		return $"path: laid at ({cellX},{cellY}) for {price}, balance {state.Balance}";
	}

	/// <summary>
	/// Lifts one cell of path back to bare ground. <b>It refunds nothing</b>, and that asymmetry is the
	/// original's: only a queue cell's removal credits anything back.
	/// </summary>
	public static string Lift( int cellX, int cellY )
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state || level.Park is not { } park )
			return "delpath: a park has to be loaded";

		if ( !ParkState.OnMap( cellX, cellY ) )
			return $"delpath: ({cellX},{cellY}) is off the map";

		var cell = ParkState.CellFor( park, cellX, cellY );

		if ( cell.Type != PathType )
			return $"delpath: ({cellX},{cellY}) is type {cell.Type}, not path";

		if ( (cell.Flags & NoModify) != 0 )
			return $"delpath: ({cellX},{cellY}) is marked NOMODIFY - the level owns that cell";

		// The neighbours are unlinked BEFORE the cell is reset, while its own mask is still intact -
		// the original's order, and the reason it can find who to unlink at all.
		Unlink( state, park, cellX, cellY );

		state.SetRecord( cellX, cellY, cell with
		{
			Type = NothingType,
			Neighbours = 0,
			Direction = 0,
			TileSet = 0,
			TileIndex = 0,
			TileAngle = 0
		} );

		ParkSurfaces.Rebuild();

		return $"delpath: lifted ({cellX},{cellY}) - nothing refunded, which is what the original does";
	}

	/// <summary>
	/// The flag that marks a cell the level itself owns - bit <c>0x20</c>, named NOMODIFY by the
	/// game's own debug string.
	///
	/// <para>
	/// <b>It covers 18 of Lost Kingdom's 78 path cells, not all of them, and that was measured after
	/// two guesses went the other way.</b> The executable's loader rebuilds cells from the level's
	/// design map and sets this flag on the path cells it makes there; OpenTPW reads the <b>save's
	/// stored</b> flags, and the two do not agree. So the refusal below is real but partial here,
	/// where in the original's runtime it covers every cell the level laid.
	/// </para>
	/// <para>
	/// <b>A hypothesis, and left as one:</b> the 18 are plausibly the author's own fixed paths and the
	/// other 60 were laid while the scenario was authored - which is what a shipped scenario save
	/// would look like. Checking the 18 against <c>base.map</c>'s design bits would settle it.
	/// </para>
	/// </summary>
	public const int NoModify = 0x20;

	/// <summary>
	/// Clears this cell's bit from every neighbour it was joined to, and retiles each of them.
	/// </summary>
	private static void Unlink( ParkState state, ParkWorld park, int x, int y )
	{
		var cell = ParkState.CellFor( park, x, y );

		foreach ( var (bit, acrossBy, downBy) in Sides )
		{
			if ( (cell.Neighbours & bit) == 0 || !ParkState.OnMap( x + acrossBy, y + downBy ) )
				continue;

			var nb = ParkState.CellFor( park, x + acrossBy, y + downBy );

			state.SetRecord( x + acrossBy, y + downBy,
				nb with { Neighbours = (byte)(nb.Neighbours & ~CellEdge.Opposite( bit )) } );

			Retile( state, park, x + acrossBy, y + downBy );
		}
	}

	/// <summary>
	/// Retiles a cell <b>and every cell around it</b>, because joining up changed their masks too.
	///
	/// <para>
	/// <b>This was found by playing, not by a test, and the test suite could not have found it.</b>
	/// Laying a run of three left the middle cell drawing <c>jpa_end1</c> - the tile its mask asked for
	/// at the moment it was laid, when it still had only one neighbour - because only the cell being
	/// laid was retiled while <see cref="ParkPathNeighbours.LinkPath"/> had also rewritten its
	/// neighbours' masks. The masks were right the whole time; the art was a step behind.
	/// </para>
	/// <para>
	/// The lifting path never had the bug, and that is what identified it: <see cref="Unlink"/> already
	/// retiles each neighbour it detaches, so a cell read correctly after a delete and wrongly after a
	/// build.
	/// </para>
	/// </summary>
	private static void RetileAround( ParkState state, ParkWorld park, int x, int y )
	{
		Retile( state, park, x, y );

		foreach ( var (_, acrossBy, downBy) in Sides )
		{
			if ( ParkState.OnMap( x + acrossBy, y + downBy ) )
				Retile( state, park, x + acrossBy, y + downBy );
		}
	}

	/// <summary>Works out what a cell should draw now and records it - the original's <c>FUN_005365d0</c>.</summary>
	private static void Retile( ParkState state, ParkWorld park, int x, int y )
	{
		var cell = ParkState.CellFor( park, x, y );

		if ( cell.Type != PathType )
			return;

		// The art variant is a coin the original carries between calls, so a straight is not a
		// function of its neighbours at all - see ParkPathTiles.Vary. Nothing here reproduces the
		// original's RNG sequence, and it is cosmetic either way.
		var (set, index, angle) = ParkPathTiles.TileFor( cell.Type, cell.Neighbours, cell.Direction );

		state.SetRecord( x, y, cell with { TileSet = set, TileIndex = index, TileAngle = angle } );
	}

	/// <summary>The eight sides, in ring order, matching <see cref="ParkPathNeighbours"/>'s own table.</summary>
	private static readonly (int Bit, int AcrossBy, int DownBy)[] Sides =
	[
		(0x01, 0, -1), (0x02, 1, -1), (0x04, 1, 0), (0x08, 1, 1),
		(0x10, 0, 1), (0x20, -1, 1), (0x40, -1, 0), (0x80, -1, -1)
	];
}
