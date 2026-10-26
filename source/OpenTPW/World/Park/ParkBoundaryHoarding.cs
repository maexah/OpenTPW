using System.Numerics;

namespace OpenTPW;

/// <summary>
/// The wall between the park and the land not yet bought: one model for each anchor of the boundary's hoarding track
/// (track type 25), made again from the park file's model records as the original's loader makes every model
/// (<c>FUN_004647a0</c>; <c>docs/exe/park.md</c>, "The boundary hoarding's piece"). The original then clears each
/// anchor's saved handle and re-claims its piece by matching item, cell and angle (<c>FUN_0053b1a0</c> through
/// <c>FUN_00522f20</c> and <c>FUN_00463ab0</c>); this draws every record of items 17300 on without that claim, which no
/// park file to hand tells apart (every anchor has its record and no such record lacks an anchor). A park made fresh has
/// no records: its pieces are those its walk chose for its anchors (<see cref="PieceFor"/>, <see cref="PieceOf"/>).
///
/// <para>
/// A piece's record carries its item, 17300 plus its row of the hoarding table (<c>FUN_00522e40</c>, <c>0x00522ea0</c>), the
/// cell it was made on and its angle. That cell is the anchor moved by the turn and the angle is 360 less the piece's own
/// (<c>FUN_00522f20</c>), so the record is stood as it is, by the rule every made model stands by
/// (<see cref="ParkObjects.OriginFor(int, int, int)"/>), at the height its two cells square give it (flags <c>0x33a</c> with
/// the footprint at <c>0x008179f8</c>, 2 by 2, every cell counted).
/// </para>
/// </summary>
public sealed class ParkBoundaryHoarding : Entity
{
	/// <summary>The item of the hoarding table's row 0; row r is item <c>FirstItem + r</c> (<c>0x4394</c>).</summary>
	public const int FirstItem = 17300;

	/// <summary>
	/// The hoarding table at <c>0x00763608</c>, row by row, as <c>FUN_00522e40</c> fills it from the theme's
	/// <c>hoarding.wad</c>: a row with no name takes the row before's model (<c>0x00522ed2</c>), so its own item is never
	/// made and a record names the named row above it.
	/// </summary>
	private static readonly string?[] Rows =
	[
		"ho_fos1", "ho_end1", "ho_fos1", "ho_fob1", "ho_fos1", null, null, null, null, "ho_fos4", null, null, "ho_fos4",
		null, null, "ho_fos4", null, null, "ho_fos4", "ho_fob2", "ho_fos2", null, null, "ho_fos3", null, null, "ho_fob3",
		"ho_fob4"
	];

	/// <summary>The cells a piece stands its height on: two across and two down, every one counted.</summary>
	private static readonly ItemShape Square = new( 2, 2, [[1, 1], [1, 1]] );

	/// <summary>The model a row draws, or null for a row outside the table.</summary>
	public static string? ModelOf( int row ) => NamedRowOf( row ) is { } named ? Rows[named] : null;

	/// <summary>The named row at or above a row, whose item a piece of that row is made as; null outside the table.</summary>
	public static int? NamedRowOf( int row )
	{
		if ( row < 0 || row >= Rows.Length )
			return null;

		while ( Rows[row] == null )
			--row;

		return row;
	}

	/// <summary>One piece standing: its anchor's model, the cell and angle its record was made at.</summary>
	public readonly record struct Piece( string Model, int Row, int CellX, int CellY, int Angle );

	/// <summary>
	/// An anchor of a boundary made here: its cell, the track record's neighbour mask (<c>+0xc</c>, the file's
	/// <c>mHoardingNeighbours</c>), direction (<c>+0xd</c>) and flag word (<c>+0xe</c>), and the row and turn its piece takes
	/// (<c>+0x18</c>, <c>+0x1c</c>; the file's <c>mTileData</c>).
	/// </summary>
	public readonly record struct Anchor( int X, int Y, byte Mask, byte Direction, ushort Flags, int Row, int Turn );

	/// <summary>
	/// The chooser's two words for the straights' variants, the original's <c>[0x00820aec]</c> (the next roll) and
	/// <c>[0x00820af0]</c> (the variant last given), nought at the program's start and kept from park to park.
	/// </summary>
	public sealed class Variants
	{
		public int Roll;
		public int Last;

		/// <summary>The program's one pair, as the original keeps its two globals.</summary>
		public static Variants Session { get; } = new();
	}

	/// <summary>
	/// Each mask's row and turn, the table at <c>0x00762ff0</c>. Every mask of the four bits has its own row, so the
	/// original's search for a subset after the exact match never answers.
	/// </summary>
	private static readonly (byte Mask, int Row, int Turn)[] PieceRows =
	[
		(0x11, 2, 0), (0x44, 2, 90), (0x10, 1, 0), (0x40, 1, 90), (0x01, 1, 180), (0x04, 1, 270), (0x14, 3, 0), (0x50, 3, 90),
		(0x41, 3, 180), (0x05, 3, 270), (0x15, 5, 0), (0x45, 6, 0), (0x51, 7, 0), (0x54, 8, 0), (0x55, 4, 0), (0x00, 0, 0)
	];

	/// <summary>
	/// The row and turn of an anchor's piece, as <c>Track_PieceForCell</c> (<c>FUN_0053a540</c>) chooses one for track type
	/// 25 (<c>docs/exe/park.md</c>, "The boundary hoarding's piece"). A straight whose flags' low four bits are clear takes the
	/// variant its roll names, unless it is the last one given, and draws the next roll from the park's generator.
	/// </summary>
	public static (int Row, int Turn) PieceFor( byte mask, byte direction, ushort flags, bool gateEnd, ref uint seed, Variants variants )
	{
		var neighbours = ((mask & 0x01) != 0 ? 1 : 0) + ((mask & 0x04) != 0 ? 1 : 0) + ((mask & 0x10) != 0 ? 1 : 0)
			+ ((mask & 0x40) != 0 ? 1 : 0);
		var straight = neighbours == 2 && ((mask & 0x11) == 0x11 || (mask & 0x44) == 0x44);
		var corner = neighbours == 2 && !straight;

		// A gate's end looks itself up with the side it lacks (FUN_004d8c00).
		var lookup = gateEnd ? (byte)(mask | (mask == 0 ? 0 : mask < 0x10 ? (mask << 4) & 0xff : mask >> 4)) : mask;
		var (_, row, turn) = PieceRows.First( entry => entry.Mask == lookup );

		if ( straight && direction is 0x40 or 0x10 )
			turn += 180;

		if ( !corner && neighbours < 3 )
		{
			if ( (flags & 1) != 0 )
				row += 9;
			if ( (flags & 2) != 0 )
				row += 12;
			if ( (flags & 8) != 0 )
				row += 15;
			if ( (flags & 4) != 0 )
				row = 0x12;
		}

		if ( corner )
		{
			// Eight direction and mask pairs take the corner's other model; 0x40 with 0x14 by a compare of BL with itself (0x0053a8c4).
			if ( (direction, mask) is (0x01, 0x41) or (0x10, 0x14) or (0x04, 0x05) or (0x40, 0x50) or (0x40, 0x14) or (0x01, 0x50)
				or (0x04, 0x41) or (0x10, 0x05) )
				row = (flags & 0xf) != 0 ? 0x1b : row + 0x10;
			else if ( (flags & 0xf) != 0 )
				row = 0x1a;
		}

		if ( straight && (flags & 0xf) == 0 )
		{
			var variant = variants.Roll < 18 ? 0 : variants.Roll < 24 ? 20 : 23;

			if ( variant != variants.Last )
				row += variant;

			variants.Last = variant;
			variants.Roll = (int)(ParkGenerator.Draw( ref seed ) % 30);
		}

		return (row, turn);
	}

	/// <summary>
	/// The piece an anchor's record makes (<c>FUN_00522f20</c>): its row's named item, the cell moved one on by the turn (90
	/// moves x, 180 x and y, 270 y) and the angle 360 less the turn.
	/// </summary>
	public static Piece PieceOf( Anchor anchor )
	{
		var named = NamedRowOf( anchor.Row ) ?? throw new ArgumentOutOfRangeException( nameof( anchor ), $"row {anchor.Row} is no row of the hoarding table" );
		var turn = anchor.Turn % 360;

		return new Piece( Rows[named]!, named, anchor.X + (turn is 90 or 180 ? 1 : 0), anchor.Y + (turn is 180 or 270 ? 1 : 0),
			(360 - turn) % 360 );
	}

	/// <summary>The boundary of the park currently loaded, or null outside one.</summary>
	public static ParkBoundaryHoarding? Current { get; private set; }

	/// <summary>The pieces standing, in the file's order.</summary>
	public IReadOnlyList<Piece> Pieces => _pieces;

	/// <summary>The boundary's anchors, the cells of track type 25.</summary>
	public int Anchors { get; }

	private readonly List<Piece> _pieces = [];
	private readonly List<LobbyModel> _models = [];
	private readonly string _theme;

	public ParkBoundaryHoarding( string themeName, IParkInitialState? park, ParkItemCatalogue? catalogue )
	{
		_theme = themeName.ToLowerInvariant();
		Name = $"{themeName} boundary hoarding";

		Anchors = AnchorsOf( park );

		// A park made fresh has no records: its pieces are the ones its walk chose (FreshParkBoundary).
		var pieces = park is FreshPark fresh ? fresh.Boundary.Select( PieceOf ).ToList() : Read( park, catalogue );

		foreach ( var piece in pieces )
			Place( piece );

		// An anchor no record names; no park file to hand holds one.
		for ( var unmade = Anchors - pieces.Count; unmade > 0; --unmade )
			Unimplemented.Report( "BOUNDARY_HOARDING_UNMADE" );

		if ( Anchors > 0 )
			Log.Info( $"{themeName}: {_pieces.Count} boundary hoarding pieces stand on {Anchors} anchors" );

		Current = this;
	}

	/// <summary>The boundary's anchors in a park's cells: those of track type 25.</summary>
	public static int AnchorsOf( IParkInitialState? park ) => park?.Cells.Count( cell => cell.TrackType == 25 ) ?? 0;

	/// <summary>
	/// The pieces a park file's model records make, in the file's order: each record of an item from 17300 on, by its row's
	/// model, its cell and its angle, whether or not an anchor names it (the original's claim is not made, see the class).
	/// None where the park has no file or its model module does not read.
	/// </summary>
	public static List<Piece> Read( IParkInitialState? park, ParkItemCatalogue? catalogue )
	{
		var pieces = new List<Piece>();

		if ( park?.Save is not { } save )
			return pieces;

		var states = save.ThingStates( id => catalogue != null && catalogue.TryGet( id, out var item ) ? item.AnimationChannels : 1 );

		if ( states.Problem != null )
			return pieces;

		foreach ( var record in states.Things )
		{
			var row = record.CatalogueId - FirstItem;

			if ( row < 0 || row >= 100 )
				continue;

			// Items 17328 to 17399 are no row of the table, and no file to hand holds one.
			if ( ModelOf( row ) is not { } model )
			{
				Unimplemented.Report( "BOUNDARY_HOARDING_ROW" );
				continue;
			}

			pieces.Add( new Piece( model, row, record.CellX, record.CellY, record.Angle ) );
		}

		return pieces;
	}

	/// <summary>
	/// Where a piece's model stands and how it is turned: its record's cell and angle as every made model takes them
	/// (<see cref="ParkObjects.OriginFor(int, int, int)"/>), at the middle of the lowest and highest ground under its two
	/// cells square, or the ground at its cell where there is none to ask.
	/// </summary>
	internal static (Vector3 Origin, Quaternion Turn) StandOf( Piece piece, HeightfieldFile? field )
	{
		var origin = ParkObjects.OriginFor( piece.CellX, piece.CellY, piece.Angle, field );

		if ( field != null && ParkObjects.BaseHeight( Square, origin, piece.Angle, field ) is { } height )
			origin = origin with { Z = height };

		return (origin, ParkObjects.Turn( piece.Angle ));
	}

	private void Place( Piece piece )
	{
		var directory = $"levels/{_theme}/hoarding";

		try
		{
			var model = new LobbyModel( $"{directory}/{piece.Model}.MD2", $"{directory}/textures", Vector3.Zero,
				sharedTextureDirectory: $"levels/{_theme}/sharetex" );

			var (origin, turn) = StandOf( piece, ParkGround.Current?.Heightfield );

			model.SetTransform( origin, turn );

			_models.Add( model );
			_pieces.Add( piece );
		}
		catch ( Exception e )
		{
			Log.Warning( $"{_theme}: boundary piece '{piece.Model}' would not load, so ({piece.CellX},{piece.CellY}) is missing - {e.Message}" );
		}
	}

	/// <summary>The console's <c>boundary</c>: the pieces by model, the anchors, and with a cell the pieces made on or beside it.</summary>
	public IEnumerable<string> Census( (int X, int Y)? near = null )
	{
		yield return $"boundary pieces {_pieces.Count} anchors {Anchors} unmade {Math.Max( 0, Anchors - _pieces.Count )} "
			+ string.Join( " ", _pieces.GroupBy( piece => piece.Model ).OrderBy( group => group.Key ).Select( group => $"{group.Key} {group.Count()}" ) );

		if ( near is not { } cell )
			yield break;

		foreach ( var piece in _pieces.Where( piece => Math.Abs( piece.CellX - cell.X ) <= 2 && Math.Abs( piece.CellY - cell.Y ) <= 2 ) )
			yield return $"  {piece.Model} row {piece.Row} at ({piece.CellX},{piece.CellY}) angle {piece.Angle}";
	}

	/// <summary>Takes the pieces down and lets go of <see cref="Current"/> where it is still this one.</summary>
	protected override void OnDelete()
	{
		base.OnDelete();

		foreach ( var model in _models )
		{
			foreach ( var entity in model.Entities )
				entity.Delete();
		}

		_models.Clear();

		if ( Current == this )
			Current = null;
	}

	protected override void OnUpdate()
	{
		foreach ( var model in _models )
			model.Update( GameClock.Delta );
	}
}
