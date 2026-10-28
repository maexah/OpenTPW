using System.Numerics;

namespace OpenTPW;

/// <summary>
/// The karts' and the water rides' track: what a bought ride lays on the track grid, which piece each anchor takes, and
/// the pieces drawn (<c>docs/exe/park.md</c>, "The karts' and the water ride's track, buoys and race" and "A track piece's
/// model, price and open end").
///
/// <para>
/// <b>The grid.</b> A piece is a 2 x 2 of cells: its top-left, the anchor, carries the track type (0xb karts, 0x10 water;
/// the stations 0xd and 0x12), the links to the anchors two cells away (<c>+0xc</c>), the way on (<c>+0xd</c>), the
/// owning ride's cell (<c>+0x10</c>) and the piece its retile chose (family, row, turn); the other three cells are 0xc (0xc
/// in both kits, op 0x83's stamp) and name the anchor as their parent. The running park holds them on the cells' track fields
/// (<see cref="ParkWorld.MapCell.TrackType"/> and the rest), the save's track records.
/// </para>
/// <para>
/// <b>The pieces are drawn from the anchors</b>, each by the row and turn its track record holds, where the original makes
/// each model as it retiles the cell and a loaded park makes them again from its model records; in every anchor of the
/// park file to hand that holds a kart track the two agree (<c>ParkTrackPieceTests</c>). The open end the mouths' walks
/// stop on pulses red to white and back (<see cref="SetPulse"/>). An add-on's anchor takes no piece: the add-on is a placed
/// thing whose model stands over its cells (<see cref="ParkObjects"/>), its end caps shown and hidden by the walk
/// (<see cref="ParkObjects.ShowAddOnCaps"/>). Not built here: the drape of a piece's vertices over the ground
/// (<c>TRACK_PIECE_DRAPE</c>). Each piece is made at rest, and the pieces' walks after a close or an add-on's put-down
/// play its <c>M</c> clip or rest it (<see cref="PlayClip"/>).
/// </para>
/// </summary>
public sealed class ParkTrackPieces : Entity
{
	public const int KartAnchor = 0xb;
	public const int KartCell = 0xc;
	public const int KartStation = 0xd;
	public const int WaterAnchor = 0x10;
	public const int WaterCell = 0x11;
	public const int WaterStation = 0x12;

	/// <summary>The boundary's anchor type on the track grid; <see cref="ParkBoundaryHoarding"/> draws its things.</summary>
	public const int Boundary = 0x19;

	/// <summary>The family a kart piece's retile answers (<c>0x00762f30</c>'s rows carry <c>0x30000</c>).</summary>
	public const int KartFamily = 3;

	/// <summary>The family a water piece's retile answers (<c>BoundaryPieceRows</c>' rows carry <c>0x40000</c>).</summary>
	public const int WaterFamily = 4;

	/// <summary>The flag a cell with an add-on carries (the placer's shape kinds 0xb and 0x10, <c>FUN_0053a510( 0x10 )</c>); the
	/// kit makes no piece there, the add-on's own model standing over it.</summary>
	public const ushort AddOnFlag = 0x10;

	/// <summary>
	/// The karts' kit, <c>0x007633e8</c>, as <c>TrackKit_LoadKartPieces</c> fills it from <c>rides\gokarts</c>: the model of
	/// each row (<c>gk_trck</c> and the name), a row with no name taking the row before's.
	/// </summary>
	private static readonly string?[] KartRows =
		[null, "Q", "S", "B", "X", null, null, null, null, "H", null, null, "H_U", null, null, "H_D", null, null, "H_A", "B2"];

	/// <summary>The water rides' kit, <c>0x007634d8</c>, from <c>rides\wateride</c>: the karts' rows and five of its own.</summary>
	private static readonly string?[] WaterRows =
	[
		null, "Q", "S", "B", "X", null, null, null, null, "H", null, null, "H_U", null, null, "H_D", null, null, "H_A", "B2",
		"X2", null, null, null, null
	];

	/// <summary>Each row's mask, which hides the end caps on the sides its track carries on (both kits' tables).</summary>
	private static readonly uint[] RowMasks =
		[0, 0x01, 0x11, 0, 0x55, 0x15, 0x54, 0x51, 0x45, 0, 0x01, 0x11, 0, 0x01, 0x11, 0, 0x01, 0x11, 0, 0, 0x55, 0x15, 0x54, 0x51, 0x45];

	/// <summary>The anchor type a ride of <c>Bumper.WhichTrackType</c> 1 or 2 lays its track as, or nought.</summary>
	public static int AnchorOf( int trackType ) => trackType switch { 1 => KartAnchor, 2 => WaterAnchor, _ => 0 };

	/// <summary>Whether a cell's track type is a kart or water anchor that takes a piece.</summary>
	public static bool IsPieceAnchor( int type ) => type is KartAnchor or WaterAnchor;

	/// <summary>The model a row of a kit draws, without its folder, or null for row 0 and a row outside the kit.</summary>
	public static string? ModelOf( int anchorType, int row )
	{
		var rows = anchorType == WaterAnchor ? WaterRows : KartRows;

		if ( row <= 0 || row >= rows.Length )
			return null;

		while ( rows[row] == null )
			--row;

		return (anchorType == WaterAnchor ? "wr_trck" : "gk_trck") + rows[row];
	}

	/// <summary>The end caps a row hides, or nought outside the table.</summary>
	public static uint MaskOf( int row ) => row >= 0 && row < RowMasks.Length ? RowMasks[row] : 0;

	/// <summary>The end caps a standing piece's model hides: its row's mask (<see cref="MaskOf"/>).</summary>
	public static uint CapMaskOf( Piece piece ) => MaskOf( piece.Row );

	/// <summary>
	/// The row and turn a kart (0xb) or water (0x10) anchor takes - <c>Track_PieceForCell</c> (<c>FUN_0053a540</c>): its
	/// links looked up in its table (the karts' at <c>0x00762f30</c> holds the boundary's rows and turns, so one table
	/// serves), a straight turned 180 more where its way on is 0x40 or 0x10, an end or a straight raised to a hill row by
	/// its flags, a bend that the flow leaves the other way round the second bend (+0x10), and a water crossing or
	/// junction turned by its way on.
	/// </summary>
	public static (int Row, int Turn) PieceFor( int type, byte mask, byte direction, ushort flags )
	{
		var links = ((mask & 0x01) != 0 ? 1 : 0) + ((mask & 0x04) != 0 ? 1 : 0) + ((mask & 0x10) != 0 ? 1 : 0)
			+ ((mask & 0x40) != 0 ? 1 : 0);
		var straight = links == 2 && ((mask & 0x11) == 0x11 || (mask & 0x44) == 0x44);
		var bend = links == 2 && !straight;
		var half = straight && direction is 0x40 or 0x10 ? 180 : 0;

		var (row, turn) = ParkBoundaryHoarding.RowFor( mask );
		turn += half;

		// The hill rows (0x0053a73f..0x0053a75e), for anything but a bend of fewer than three links.
		if ( !bend && links < 3 )
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

		if ( bend && (direction, mask) is (0x40, 0x50) or (0x10, 0x14) or (0x01, 0x41) or (0x04, 0x05) )
			row += 0x10;

		if ( type == WaterAnchor && links >= 3 )
		{
			if ( direction == 0x14 )
				turn = 90;

			if ( direction == 0x41 )
				row += 0x10;

			if ( direction == 0x50 )
			{
				row += 0x10;
				turn = 270;
			}
		}

		return (row, turn);
	}

	/// <summary>
	/// The way a bought ride's track runs out of its station at each turn - <c>TrackRide_LayStation</c>'s
	/// <c>DAT_00818c50</c>: east, north, west, south for 0, 90, 180 and 270.
	/// </summary>
	public static byte HeadingOf( int angle ) => (((angle % 360) + 360) % 360) switch { 90 => 0x01, 180 => 0x40, 270 => 0x10, _ => 0x04 };

	/// <summary>
	/// The four anchors a bought ride lays, from the ride's own cell: the other mouth, the station's two sections and the
	/// mouth, each two cells on along <see cref="HeadingOf"/> - <c>TrackRide_LayStation</c> (<c>FUN_0052a1a0</c>),
	/// <c>FUN_0052a3a0</c> and <c>FUN_0052a420</c>.
	/// </summary>
	public static ((int X, int Y) OtherMouth, (int X, int Y) First, (int X, int Y) Second, (int X, int Y) Mouth) StationOf( int x, int y, int angle )
	{
		var (first, dx, dy) = (((angle % 360) + 360) % 360) switch
		{
			90 => ((x + 1, y - 1), 0, -2),
			180 => ((x - 1, y - 2), -2, 0),
			270 => ((x - 2, y), 0, 2),
			_ => ((x, y + 1), 2, 0)
		};

		return ((first.Item1 - dx, first.Item2 - dy), first, (first.Item1 + dx, first.Item2 + dy), (first.Item1 + (2 * dx), first.Item2 + (2 * dy)));
	}

	/// <summary>
	/// Lays a bought kart or water ride's station and both mouths on the track grid, as the place commit does
	/// (<c>0x00524db7</c>..<c>0x0052518c</c>): the station's two sections of 0xd (0x12) run from the first to the second,
	/// the mouth past it linked back, and the other mouth before it linked on; each anchor's way on the heading, its owner
	/// the ride's cell, and the mouths' pieces chosen (the stations are never retiled). Nothing is charged: the put-down's
	/// mode lays track free. Answers how many anchors were laid; one off the map is not.
	/// </summary>
	public static int LayStation( ParkState state, int trackType, int x, int y, int angle )
	{
		var anchor = AnchorOf( trackType );

		if ( anchor == 0 )
			return 0;

		var station = anchor == KartAnchor ? KartStation : WaterStation;
		var heading = HeadingOf( angle );
		var back = (byte)CellEdge.Opposite( heading );
		var owner = (ushort)MapStep.CellId( x, y );
		var (otherMouth, first, second, mouth) = StationOf( x, y, angle );
		var laid = 0;

		laid += Lay( state, otherMouth, anchor, heading, heading, owner );
		laid += Lay( state, first, station, (byte)(heading | back), heading, owner );
		laid += Lay( state, second, station, (byte)(heading | back), heading, owner );
		laid += Lay( state, mouth, anchor, back, heading, owner );

		return laid;
	}

	/// <summary>
	/// Writes one anchor and its three cells. A piece's 2 x 2 runs right and down from its anchor at every turn. The
	/// original's put-down first checks each mouth's two by two (op <c>0x20b</c> or <c>0x210</c> from
	/// <c>TrackRide_CarrySquares</c>, <c>0x005286a0</c>) and refuses the placement on its refusal; that check is not built,
	/// so an anchor whose two by two holds track already is left as it is, and counted (<c>PUT_DOWN_TRACK_OVER_TRACK</c>).
	/// </summary>
	private static int Lay( ParkState state, (int X, int Y) at, int type, byte mask, byte direction, ushort owner )
	{
		if ( !ParkState.OnMap( at.X, at.Y ) )
			return 0;

		foreach ( var (dx, dy) in new[] { (0, 0), (1, 0), (0, 1), (1, 1) } )
		{
			if ( ParkState.OnMap( at.X + dx, at.Y + dy ) && state.Record( at.X + dx, at.Y + dy ).TrackType != 0 )
			{
				Unimplemented.Report( "PUT_DOWN_TRACK_OVER_TRACK" );
				Log.Info( $"track: ({at.X},{at.Y}) is not laid, ({at.X + dx},{at.Y + dy}) holds track type "
					+ $"0x{state.Record( at.X + dx, at.Y + dy ).TrackType:x} already" );
				return 0;
			}
		}

		var (family, row, turn) = IsPieceAnchor( type )
			? (type == KartAnchor ? KartFamily : WaterFamily, PieceFor( type, mask, direction, 0 ).Row, PieceFor( type, mask, direction, 0 ).Turn)
			: (0, 0, 0);

		state.SetRecord( at.X, at.Y, state.Record( at.X, at.Y ) with
		{
			TrackType = type,
			TrackNeighbours = mask,
			TrackDirection = direction,
			TrackFlags = 0,
			TrackParentId = owner,
			TrackTileSet = family,
			TrackTileIndex = row,
			TrackTileAngle = turn
		} );

		// Op 0x83 stamps the three 0xc in both kits (0x00538895).
		var cell = KartCell;
		var parent = (ushort)MapStep.CellId( at.X, at.Y );

		foreach ( var (dx, dy) in new[] { (1, 0), (0, 1), (1, 1) } )
		{
			if ( ParkState.OnMap( at.X + dx, at.Y + dy ) )
				state.SetRecord( at.X + dx, at.Y + dy, Bare( state.Record( at.X + dx, at.Y + dy ) ) with { TrackType = cell, TrackParentId = parent } );
		}

		return 1;
	}

	/// <summary>A cell's record with its track fields nought.</summary>
	private static ParkWorld.MapCell Bare( ParkWorld.MapCell cell ) => cell with
	{
		TrackType = 0, TrackNeighbours = 0, TrackDirection = 0, TrackFlags = 0, TrackParentId = 0, TrackTileSet = 0,
		TrackTileIndex = 0, TrackTileAngle = 0
	};

	/// <summary>
	/// What a sale of a kart or water ride does to its track, where all it owns is the station and the two mouths it was
	/// bought with: each cell of the four anchors cleared, the two mouths refunded at their price and two pieces' worth
	/// withdrawn (<c>FUN_00527ee0</c>, <c>0x00528179</c>; the refund's percentage is the scrap value's, taken as the whole,
	/// as the sale's own is). Answers null where the ride owns more track, which is not cleared (<c>SALE_TRACK_TEARDOWN</c>),
	/// else the money the two moves come to (nought).
	/// </summary>
	public static int? SellBareStation( ParkState state, int trackType, int x, int y, int price )
	{
		var owner = (ushort)MapStep.CellId( x, y );
		var anchors = new List<(int X, int Y)>();

		for ( var cellY = 0; cellY < ParkWorld.MapSize; ++cellY )
		{
			for ( var cellX = 0; cellX < ParkWorld.MapSize; ++cellX )
			{
				var cell = state.Record( cellX, cellY );

				if ( cell.TrackParentId == owner && cell.TrackType is KartAnchor or KartStation or WaterAnchor or WaterStation )
					anchors.Add( (cellX, cellY) );
			}
		}

		var mouths = anchors.Count( at => IsPieceAnchor( state.Record( at.X, at.Y ).TrackType ) );

		// A run laid from a mouth links it on beyond the station; the bought ride's mouths link back alone.
		if ( anchors.Count != 4 || mouths != 2
			|| anchors.Any( at => state.Record( at.X, at.Y ) is { } cell && IsPieceAnchor( cell.TrackType )
				&& CellEdge.Opposite( cell.TrackDirection ) != cell.TrackNeighbours && cell.TrackDirection != cell.TrackNeighbours ) )
			return null;

		foreach ( var at in anchors )
		{
			var parent = MapStep.CellId( at.X, at.Y );

			state.SetRecord( at.X, at.Y, Bare( state.Record( at.X, at.Y ) ) );

			foreach ( var (dx, dy) in new[] { (1, 0), (0, 1), (1, 1) } )
			{
				if ( ParkState.OnMap( at.X + dx, at.Y + dy ) && state.Record( at.X + dx, at.Y + dy ).TrackParentId == parent )
					state.SetRecord( at.X + dx, at.Y + dy, Bare( state.Record( at.X + dx, at.Y + dy ) ) );
			}
		}

		state.Deposit( 2 * price );
		state.Spend( 2 * price );

		return 0;
	}

	/// <summary>What one new piece of a kart or water ride's track costs in the park now loaded - <c>Costs.KartTrackCell</c>
	/// (200 in Lost Kingdom) or <c>Costs.WaterTrackCell</c> (250); nought, the image's value, where the balance has none.</summary>
	public static int PieceCost( Level? level, int trackType )
		=> level?.Balance?.Int( trackType == 2 ? "Costs.WaterTrackCell" : "Costs.KartTrackCell", 0 ) ?? 0;

	/// <summary>One piece standing: its anchor, its kit's model, row and turn, and the cell and angle its model is made at.</summary>
	public readonly record struct Piece( int AnchorX, int AnchorY, int Type, string Model, int Row, int Turn, int CellX, int CellY, int Angle );

	/// <summary>
	/// The piece an anchor's record names, or null where it takes none: row 0, a row outside its kit, or an add-on's cell. The
	/// model is turned 360 less the turn and made on the cell one on by it, as a boundary piece's (<c>FUN_00522b90</c>).
	/// </summary>
	public static Piece? PieceAt( int x, int y, ParkWorld.MapCell cell )
	{
		if ( !IsPieceAnchor( cell.TrackType ) )
			return null;

		if ( (cell.TrackFlags & AddOnFlag) != 0 )
			return null;

		var row = cell.TrackTileIndex & 0xffff;

		if ( ModelOf( cell.TrackType, row ) is not { } model )
			return null;

		var turn = ((cell.TrackTileAngle % 360) + 360) % 360;

		return new Piece( x, y, cell.TrackType, model, row, turn, x + (turn is 90 or 180 ? 1 : 0), y + (turn is 180 or 270 ? 1 : 0),
			(360 - turn) % 360 );
	}

	/// <summary>The folder a kit's pieces are read from: <c>rides\gokarts</c> for the karts', <c>rides\wateride</c> for the water's.</summary>
	public static string KitDirectory( string theme, int anchorType )
		=> $"levels/{theme}/rides/{(anchorType == WaterAnchor ? "wateride" : "gokarts")}";

	/// <summary>The track of the park currently loaded, or null outside one.</summary>
	public static ParkTrackPieces? Current { get; private set; }

	/// <summary>The pieces standing, in the map's order.</summary>
	public IReadOnlyList<Piece> Pieces => _pieces;

	private readonly List<Piece> _pieces = [];

	/// <summary>Each standing piece's model, kept while its anchor's record names the same piece.</summary>
	private readonly Dictionary<Piece, LobbyModel> _models = [];

	/// <summary>The models pulsing, the original's model flag <c>0x4000000</c> and its list <c>[0x007afe14]</c>.</summary>
	private readonly HashSet<LobbyModel> _pulsing = [];

	/// <summary>
	/// The models whose channel plays role 5, the <c>M</c> clip, looped (<see cref="PlayClip"/>); every other one stands at
	/// rest, as a piece is made (<c>FUN_004732a0( model, 0xc, ... )</c>: 0xc names no role, so the rest pose).
	/// </summary>
	private readonly HashSet<LobbyModel> _playing = [];

	/// <summary>
	/// The pulse's phase, 0 to 2, shared by every pulsing model and never set back (<c>[0x007afd04]</c>): each frame it
	/// moves on by twice the frame's seconds, wrapped at 2 (<c>Model_PulseTick</c>, <c>0x00469840</c>).
	/// </summary>
	private static float _pulsePhase;
	private readonly string _theme;

	/// <summary>The theme the pieces are read from, lower-case.</summary>
	public string Theme => _theme;
	private readonly IParkInitialState? _park;

	public ParkTrackPieces( string themeName, IParkInitialState? park )
	{
		_theme = themeName.ToLowerInvariant();
		_park = park;
		Name = $"{themeName} track pieces";

		Build();

		Current = this;
	}

	/// <summary>
	/// Stands the pieces again, because a cell has changed: a piece its anchor still names keeps its model, one it no
	/// longer names is taken down, and only a new one is made, as the original's retile makes a model for the cell it
	/// changes alone.
	/// </summary>
	public void Rebuild() => Build();

	private void Build()
	{
		if ( _park == null )
			return;

		var wanted = new List<Piece>();

		for ( var y = 0; y < ParkWorld.MapSize; ++y )
		{
			for ( var x = 0; x < ParkWorld.MapSize; ++x )
			{
				if ( PieceAt( x, y, ParkState.CellFor( _park, x, y ) ) is { } piece )
					wanted.Add( piece );
			}
		}

		var keep = wanted.ToHashSet();

		foreach ( var gone in _models.Keys.Where( piece => !keep.Contains( piece ) ).ToList() )
		{
			_pulsing.Remove( _models[gone] );
			_playing.Remove( _models[gone] );
			Delete( _models[gone] );
			_models.Remove( gone );
		}

		var made = 0;
		_pieces.Clear();

		foreach ( var piece in wanted )
		{
			if ( !_models.ContainsKey( piece ) && Make( piece ) is { } model )
			{
				_models[piece] = model;
				++made;
			}

			if ( _models.ContainsKey( piece ) )
				_pieces.Add( piece );
		}

		if ( _pieces.Count > 0 || made > 0 )
			Log.Info( $"{_theme}: {_pieces.Count} track pieces stand, {made} made - {string.Join( ", ", Census() )}" );
	}

	/// <summary>Makes one piece's model where it stands, or null where it would not load.</summary>
	private LobbyModel? Make( Piece piece )
	{
		var directory = KitDirectory( _theme, piece.Type );

		try
		{
			var model = new LobbyModel( $"{directory}/{piece.Model}.MD2", $"{directory}/textures", Vector3.Zero,
				sharedTextureDirectory: $"levels/{_theme}/sharetex" );

			var (origin, turn) = ParkBoundaryHoarding.StandOf( new ParkBoundaryHoarding.Piece( piece.Model, piece.Row, piece.CellX,
				piece.CellY, piece.Angle ), ParkGround.Current?.Heightfield );

			model.SetTransform( origin, turn );
			model.HideCaps( CapMaskOf( piece ) );

			// The original drapes a piece's every vertex over the ground under it at its making (0x00469940), which is not
			// built. A water piece is then put to clip 0xc, no role, so it stands at rest (FUN_004732a0 at its making), as
			// every piece here does until a walk plays its clip (PlayClip).
			Unimplemented.Report( "TRACK_PIECE_DRAPE" );

			return model;
		}
		catch ( Exception e )
		{
			Log.Warning( $"{_theme}: track piece '{piece.Model}' would not load, so ({piece.AnchorX},{piece.AnchorY}) is missing - {e.Message}" );
			return null;
		}
	}

	/// <summary>
	/// Starts or stops the pulse of the piece standing on an anchor, as a mouth's walk does on the open end it stops on
	/// (<c>Model_PulseOn</c>, <c>0x00469580</c>, where the model is not pulsing; <c>Model_PulseOff</c>, <c>0x00469650</c>,
	/// where it is, which gives its vertices their lit colours back). Answers whether a piece stands there; a new model
	/// made for a changed piece does not pulse until a walk says so, as the original's retile makes a fresh one.
	/// </summary>
	public bool SetPulse( int x, int y, bool on )
	{
		if ( _pieces.FirstOrDefault( piece => piece.AnchorX == x && piece.AnchorY == y ) is not { Model: not null } piece
			|| !_models.TryGetValue( piece, out var model ) )
			return false;

		if ( on && _pulsing.Add( model ) )
		{
			// The tick writes the colour only on the next update; a click is dealt after it, so write it now.
			var value = PulseValue( _pulsePhase ) / 255f;

			model.SetVertexColour( new Vector4( 1f, value, value, 1f ) );
			Log.Info( $"{_theme}: the track piece on ({x},{y}) pulses" );
		}
		else if ( !on && _pulsing.Remove( model ) )
		{
			model.SetVertexColour( null );
			Log.Info( $"{_theme}: the track piece on ({x},{y}) stops pulsing" );
		}

		return true;
	}

	/// <summary>
	/// Puts the piece standing on an anchor to a clip, as the pieces' walks do (<see cref="ParkTrackTool.PlayPieces"/>):
	/// with <paramref name="running"/> role 5, the <c>M</c> clip, looped from its start (<c>FUN_004732a0( model, 5, 0, 7,
	/// 1.0, 0 )</c>, flags 7: loop, start now), else 0xc, which names no role and puts the model back at rest. A model
	/// with no <c>M</c> clip (every Lost Kingdom kart piece) rests either way, as the original's trigger of a role a model
	/// lacks restores the rest pose (<c>FUN_00472f60</c>). Answers whether a piece stands there.
	/// </summary>
	public bool PlayClip( int x, int y, bool running )
	{
		if ( _pieces.FirstOrDefault( piece => piece.AnchorX == x && piece.AnchorY == y ) is not { Model: not null } piece
			|| !_models.TryGetValue( piece, out var model ) )
			return false;

		if ( running && model.Clips.Count > 0 )
		{
			foreach ( var animator in model.Animators )
				animator.Pause();

			_playing.Add( model );
		}
		else
		{
			_playing.Remove( model );

			foreach ( var animator in model.Animators )
				animator.Rest();

			if ( model.Clips.Count > 0 )
				model.Rotator?.Rest( model.Clips[0] );
		}

		return true;
	}

	/// <summary>
	/// The green and blue a pulsing model's every vertex takes at a phase, red and alpha being 255: the phase's triangle, 0
	/// at 0, 255 at 1 and back to 0 at 2, times 255 and cut to a whole number (<c>0x00469840</c>..<c>0x00469871</c>,
	/// <c>__ftol</c>), so the piece goes red to white and back once a second.
	/// </summary>
	public static int PulseValue( float phase ) => (int)((phase <= 1f ? phase : 2f - phase) * 255f);

	/// <summary>The phase after a frame of <paramref name="seconds"/>: twice the seconds on, wrapped at 2 (<c>fmod</c>).</summary>
	public static float PulsePhaseAfter( float phase, float seconds ) => (phase + (2f * seconds)) % 2f;

	/// <summary>The console's <c>track</c>: each piece's anchor, model, row and turn, and whether it pulses.</summary>
	public IEnumerable<string> Census()
		=> _pieces.Select( piece => $"({piece.AnchorX},{piece.AnchorY}) {piece.Model} row {piece.Row} turn {piece.Turn}"
			+ (_models.TryGetValue( piece, out var model ) && _pulsing.Contains( model ) ? $" pulsing {PulseValue( _pulsePhase )}" : "")
			+ (model != null && _playing.Contains( model ) ? " clip 5" : "") );

	private static void Delete( LobbyModel model )
	{
		foreach ( var entity in model.Entities )
			entity.Delete();
	}

	private void TakeDown()
	{
		foreach ( var model in _models.Values )
			Delete( model );

		_models.Clear();
		_pieces.Clear();
		_pulsing.Clear();
		_playing.Clear();
	}

	protected override void OnDelete()
	{
		base.OnDelete();

		TakeDown();

		if ( Current == this )
			Current = null;
	}

	protected override void OnUpdate()
	{
		foreach ( var model in _playing )
			model.Update( GameClock.Delta );

		// Once a frame, on the frame clock as the original's drawn frame runs it (0x0054e301): the value is taken at the
		// phase before it moves on, and written over every vertex of every pulsing model.
		var value = PulseValue( _pulsePhase ) / 255f;

		foreach ( var model in _pulsing )
			model.SetVertexColour( new Vector4( 1f, value, value, 1f ) );

		_pulsePhase = PulsePhaseAfter( _pulsePhase, Time.Delta );
	}
}
