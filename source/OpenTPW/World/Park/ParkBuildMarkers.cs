namespace OpenTPW;

/// <summary>
/// The coloured squares the path and queue tools lay over the ground from the anchor to the pointer - the
/// original's <c>BlueprintMesh</c>, one of the three "dynamic faces" meshes <c>FUN_0053bfe0</c> builds.
///
/// <para>
/// <b>This is what makes the node a player lays a queue from visible before anything is clicked.</b>
/// The strip always starts at the anchor, so the moment a queued ride goes down the cell before its
/// entrance carries a square, and moving the pointer draws the run a click would lay: blue where it
/// may go, red where it may not, <c>m_link</c> where it would join a path and <c>m_end</c> where it
/// would close on the ride's own queue. See <see cref="ParkPathBuilding.QueueStrip"/> and
/// <see cref="ParkPathBuilding.PathStrip"/>, which the click itself obeys.
/// </para>
/// <para>
/// <b>One square a cell, 10 by 10, at the ground's own corner heights plus 1.5, see-through, and
/// waving</b> - the original's <c>FUN_0053df30</c> face 0, whose corners <c>FUN_0053ddd0</c> lifts by
/// <c>sin( phase + x + z )</c> from a 4,096-entry table (<c>FUN_004708d0</c>), the phase gaining 0.1 a
/// frame while the game is not paused (<c>docs/exe/park-engine.md</c>, "Placement feedback").
/// <para>
/// Two things are counted rather than guessed: the brightening and dimming that goes with the wave (the
/// same sine scales the vertex's up vector, which this shader normalises away), and the turning of <c>m_link</c> and
/// <c>m_end</c> to face the camera.
/// </para>
/// <para>
/// <b>Every red square blinks, seven frames on and two off, all together</b> (<see cref="AdvanceBlink"/>), and
/// <b>a stranded guest has one under them</b>: the same red square at their own cell, 1.0 over the ground and
/// never lifted over what is built there (<c>FUN_004fa030</c>, <c>0x004fa0e6</c>;
/// <c>docs/exe/ride-operation.md</c>, "The red square under a stranded person").
/// </para>
/// <para>
/// <b>A fenced member the staff locator follows has the same square on every cell of their patrol area's
/// outline</b>, blue, or red after a refused tool's pass (<see cref="PatrolSquares"/>), queued each frame by
/// <see cref="UI.ParkLocator"/> (<c>FUN_004b3640</c>, op <c>0x136</c>) and gone the frame it is not
/// (<see cref="QueuePatrolEdges"/>).
/// </para>
/// <para>
/// <b>Over a built cell the square lifts by the height of what is built there, rounded up to ten</b>
/// (<see cref="LiftOver"/>), and that lift is added to the ground's height again as the corner is laid, as
/// <c>FUN_0053ddd0</c> does. <b>A lifted square is walled down to each lower neighbour</b> (<see cref="Walls"/>): on
/// the edge they share, one wall ten high for every ten between the neighbour's lift and its own, the lowest first.
/// </para>
/// </para>
/// </summary>
public sealed class ParkBuildMarkers : ModelEntity
{
	/// <summary>The markers of the park currently loaded, or null outside one.</summary>
	public static ParkBuildMarkers? Current { get; private set; }

	/// <summary>How far above the ground a tool's square sits - the <c>1.5</c> the record writers take off (<c>- [0x00700d9c]</c>, -1.5), so a record holds the lift plus 1.5.</summary>
	private const float Lift = 1.5f;

	/// <summary>
	/// The marker table at <c>0x00763b38</c>, in its own order: an index there is the texture a square
	/// wears. Loaded from <c>data/generic/dynamic/textures</c> at every texture quality. <b>Deviation:</b> the
	/// original takes <c>STexture</c> (<c>0x00763ee4</c>) where <c>FUN_0054dcc0</c> answers 1, texture quality 0 or 1
	/// (<c>FUN_0053d790</c>, <c>0x0053d7b4</c>), as <see cref="ParkSea"/> and <see cref="ParkFlags"/> do; blue and red
	/// differ between the two folders. Not built: a change to what is drawn is confirmed in the running game.
	/// </summary>
	private static readonly string[] MarkerNames =
	[
		"blue", "red", "orange", "m_enter", "m_exit", "m_direct", "m_front", "m_inout", "m_link", "m_break",
		"m_cross", "m_end", "m_nocash", "m_erase", "red", "gby_sur1", "gby_sur2", "gby_sur3", "gte_lgo1", "gte_lgo1"
	];

	/// <summary>The markers the tools and a carried thing draw, in the material's slot order.</summary>
	private static readonly int[] Drawn =
	[
		ParkPathBuilding.MarkerBlue, ParkPathBuilding.MarkerRed, ParkPathBuilding.MarkerLink, ParkPathBuilding.MarkerEnd,
		ParkPathBuilding.MarkerEnter, ParkPathBuilding.MarkerExit, ParkPathBuilding.MarkerDirect, ParkPathBuilding.MarkerFront,
		ParkPathBuilding.MarkerInOut
	];

	/// <summary>The shader's own see-through bit - see <c>test.shader</c>'s <c>FLAG_TRANSLUCENT</c>.</summary>
	private const uint Translucent = 2;

	/// <summary>
	/// Whether the squares' ripple shading (the sine scaling each corner's up vector, <c>0x00700e28</c>) is a gap in this
	/// light: the original's lighter runs only behind render-word <c>0x0800</c> (<c>0x0057453a</c>, <c>0x00574689</c>), so
	/// low quality's <c>LIGHTING</c> 0 gives every corner the mask and the scale changes no colour there (Q516).
	/// </summary>
	internal static bool RipplesShade( ParkLight? light ) => light is not { Lit: false };

	private readonly Texture[] _textures = new Texture[Material.TextureSlots];

	/// <summary>The strip last built, so the mesh is laid again only when it changes.</summary>
	private string _built = string.Empty;

	/// <summary>How far above the ground a stranded guest's square sits - the 1.0 <c>FUN_004fa030</c> passes.</summary>
	private const float StrandedLift = 1f;

	/// <summary>
	/// One square of the mesh: its cell, its marker, how far over the ground its corners start, the compass bit its
	/// texture is turned by (<see cref="TurnedUvs"/>), and whether it glides (<see cref="Glide"/>).
	/// </summary>
	private readonly record struct Square( int X, int Y, int Marker, float Over, int Turn = 0, bool Glides = false, int Size = 1 );

	/// <summary>The squares the mesh holds now, and its vertices, so the wave can move them every frame.</summary>
	private List<Square> _strip = [];

	/// <summary>
	/// One wall under a lifted square: the square's cell, the edge it stands on (<see cref="FaceNorth"/> and the
	/// rest), its marker, how far over the ground its foot is, and how many cells long it is (2 under a track tool's
	/// square). It rises <see cref="WallHeight"/> from there.
	/// </summary>
	internal readonly record struct Wall( int X, int Y, int Face, int Marker, float Over, bool Glides = false, int Size = 1 );

	/// <summary>The walls the mesh holds now, in the order the original queues them: by square, then N, E, S, W, lowest first.</summary>
	private List<Wall> _walls = [];

	/// <summary>
	/// The face byte of a record (<c>FUN_0053df30</c>'s switch): 0 the flat square, and a wall on the edge toward each
	/// neighbour, named by that neighbour's direction bit - north the cell's least-y edge, toward y − 1.
	/// </summary>
	public const int FaceNorth = 0x01;

	/// <inheritdoc cref="FaceNorth"/>
	public const int FaceEast = 0x04;

	/// <inheritdoc cref="FaceNorth"/>
	public const int FaceSouth = 0x10;

	/// <inheritdoc cref="FaceNorth"/>
	public const int FaceWest = 0x40;

	/// <summary>How tall a wall is - the <c>-10.0</c> at <c>0x00700e1c</c> its top corners are raised by (<c>FUN_0053df30</c>).</summary>
	public const float WallHeight = 10f;

	private Vertex[] _vertices = [];

	/// <summary>How many of <see cref="_strip"/>, from its start, are the tool's; then the patrol edges'; the rest are under stranded guests.</summary>
	private int _toolSquares;

	/// <summary>How many of <see cref="_strip"/>, after the tool's, are a followed member's patrol edges.</summary>
	private int _patrolSquares;

	/// <summary>The patrol edges queued since the mesh was last laid, which <see cref="OnUpdate"/> takes and empties.</summary>
	private List<(int X, int Y)> _patrolQueued = [];

	/// <summary>
	/// Whether red squares are showing, and the frames counted toward the next change - <c>DAT_00763c98</c> and
	/// <c>DAT_00874fc8</c>, which the marker draw <c>FUN_0053c3f0</c> steps unless the game is paused.
	/// </summary>
	private bool _redShown = true;

	private float _blinkFrames;

	/// <summary>How many frames a red square shows for, and how many it is gone for (<c>0x0053c7b1</c>..<c>0x0053c7f6</c>).</summary>
	public const int BlinkOn = 7;

	/// <inheritdoc cref="BlinkOn"/>
	public const int BlinkOff = 2;

	/// <summary>
	/// How many frames a second the blink counts. <b>The original counts rendered frames</b>; this takes 30 a
	/// second, as the wave does (<see cref="WavePerSecond"/>), and runs off <see cref="Time.Delta"/>.
	/// </summary>
	private const float BlinkFramesPerSecond = 30f;

	/// <summary>
	/// The blink after some frames: showing, it stops once <see cref="BlinkOn"/> frames are counted; gone, it
	/// shows again once <see cref="BlinkOff"/> are; the count starts again at each change.
	/// </summary>
	internal static (bool Shown, float Frames) AdvanceBlink( bool shown, float frames, float passed )
	{
		frames += passed;

		while ( frames >= (shown ? BlinkOn : BlinkOff) )
		{
			frames -= shown ? BlinkOn : BlinkOff;
			shown = !shown;
		}

		return (shown, frames);
	}

	/// <summary>
	/// Where the wave is, in radians - <c>DAT_00874fc0</c>, which <c>FUN_0053c3f0</c> raises by 0.1 each
	/// rendered frame unless the clock is paused (<c>0x0053c755</c>..<c>0x0053c773</c>).
	/// </summary>
	private float _phase;

	/// <summary>
	/// How fast the wave moves. <b>The original steps 0.1 per rendered frame</b>, so its speed followed its
	/// frame rate; this takes 30 frames a second, the rate the menu colour ramp takes for its own
	/// per-frame step (<c>docs/exe/ui.md</c>; the lobby's per-frame rolls take 25,
	/// <see cref="LobbyScript.AssumedFrameRate"/>), and runs off
	/// <see cref="Time.Delta"/> so it is the same speed at any frame rate here.
	/// </summary>
	private const float WavePerSecond = 0.1f * 30f;

	/// <summary>
	/// Where a carried thing's squares are drawn from, in world units: the original's <c>DAT_00820afc</c> and
	/// <c>DAT_00820af8</c>, which <c>FUN_0053c3f0</c> moves half the way to the pointer's cell each rendered frame
	/// (<c>0x00700de4</c>, 0.5). In modes 4, <c>0xe</c> and <c>0x13</c> every square and wall is drawn that far from
	/// its own cell as this point is from the pointer's (<c>FUN_0053df30</c>, <c>0x0053df67</c>), so the footprint
	/// slides after the pointer rather than jumping a cell at a time.
	/// </summary>
	private (float X, float Y) _glide;

	/// <summary>The pointer's cell's corner, in world units, that <see cref="_glide"/> eases toward.</summary>
	private (float X, float Y) _glideTarget;

	/// <summary>
	/// How fast the glide closes. <b>The original halves the distance each rendered frame</b>, so its speed followed its
	/// frame rate; this halves it each thirtieth of a second, the rate the wave takes (<see cref="WavePerSecond"/>), through
	/// <see cref="Time.SmoothingFactor"/>.
	/// </summary>
	private static readonly float GlideRate = 30f * MathF.Log( 2f );

	/// <summary>
	/// The glide after some time: <paramref name="from"/> eased toward <paramref name="to"/> by
	/// <paramref name="factor"/>, as <c>FUN_0053c3f0</c> does it, <c>s += ( target − s ) × 0.5</c>.
	/// </summary>
	internal static (float X, float Y) Glide( (float X, float Y) from, (float X, float Y) to, float factor )
		=> (from.X + ((to.X - from.X) * factor), from.Y + ((to.Y - from.Y) * factor));

	public ParkBuildMarkers()
	{
		Name = "build markers";

		for ( var slot = 0; slot < _textures.Length; ++slot )
			_textures[slot] = slot < Drawn.Length ? Load( MarkerNames[Drawn[slot]] ) : Texture.Missing;

		Current = this;
	}

	private static Texture Load( string name )
	{
		try
		{
			return new Texture( $"generic/dynamic/textures/{name}.wct" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Build markers: {name}.wct would not load - {e.Message}" );
			return Texture.Missing;
		}
	}

	protected override void OnUpdate()
	{
		if ( !GameClock.Paused )
		{
			_phase = (_phase + (WavePerSecond * Time.Delta)) % (MathF.PI * 2f);
			(_redShown, _blinkFrames) = AdvanceBlink( _redShown, _blinkFrames, BlinkFramesPerSecond * Time.Delta );
		}

		var picked = ParkPicking.TryCell( out var x, out var y );
		var latched = false;
		var strip = picked ? ParkPathBuilding.Strip( x, y, out latched ) : [];
		var carried = picked ? ParkBuilding.CarrySquares( x, y ) : [];

		ParkBuildMode.RefusalLatched = LatchAfterFrame( ParkBuildMode.RefusalLatched, ParkBuildMode.Current, picked, latched );

		if ( picked && ParkGround.Current?.Heightfield is { } ground )
			_glideTarget = (x * ground.CellSizeX, y * ground.CellSizeY);

		_glide = Glide( _glide, _glideTarget, Time.SmoothingFactor( GlideRate ) );

		var stranded = Stranded();

		// The original's marker draw empties its list every frame, so a patrol edge queued for no frame is gone. A
		// carried thing that will not go down leaves the placer's failure set, which the edges' square arm reads first.
		var patrol = PatrolSquares( _patrolQueued, PatrolLatch( ParkBuildMode.RefusalLatched, carried ) );
		_patrolQueued = [];

		var built = string.Join( ";", strip.Select( square => $"{square.X},{square.Y},{square.Marker}{(square.Wide ? "#2" : "")}" ) )
			+ "|" + string.Join( ";", carried.Select( square => $"{square.X},{square.Y},{square.Marker},{square.Turn}" ) )
			+ "|" + string.Join( ";", patrol.Select( square => $"{square.X},{square.Y},{square.Marker}" ) )
			+ "|" + string.Join( ";", stranded.Select( cell => $"{cell.X},{cell.Y}" ) );

		if ( built != _built )
		{
			_built = built;
			Build( strip, carried, patrol, stranded );

			return;
		}

		// The same squares: only the wave and the blink have moved, so the corners are laid again in place.
		if ( TranslucentModel is { } model && ParkGround.Current?.Heightfield is { } field && Fill( field ) > 0 )
			model.UpdateVertices( _vertices );
	}

	/// <summary>
	/// A followed member's patrol edges for this frame, one square a cell, a cell given twice laid twice
	/// (<see cref="UI.ParkLocator.PatrolEdgeCells"/>), all red or all blue by the refusal latch
	/// (<see cref="PatrolSquares"/>).
	/// </summary>
	public void QueuePatrolEdges( List<(int X, int Y)> cells ) => _patrolQueued = cells;

	/// <summary>
	/// The refusal latch after a frame: an armed tool's pass over the park leaves it as its verdicts set it
	/// (<c>FUN_005234d0</c>, cleared at <c>0x005234f2</c> and set by them); with no tool armed none runs and it stands.
	/// </summary>
	internal static bool LatchAfterFrame( bool latch, int mode, bool picked, bool passLatched )
		=> picked && mode != ParkBuildMode.None ? passLatched : latch;

	/// <summary>
	/// The patrol edges' squares: the original's verdict for them (<c>FUN_00535670( 0x36 )</c>, <c>0x005356df</c>) is the
	/// refusal latch <c>DAT_00816d50</c> alone, so they are red after a tool's last pass refused in a way that latches
	/// (<see cref="ParkBuildMode.RefusalLatched"/>) and blue otherwise. The square's own arm reads the placer's failure
	/// <c>DAT_0081b604</c> first (<c>0x0053319a</c>), which putting a tool away clears, so a carried thing that will not go
	/// down reds them too (<see cref="CarriedRefused"/>). docs/exe/hud.md, "The patrol edges".
	/// </summary>
	internal static List<ParkPathBuilding.QueueSquare> PatrolSquares( IEnumerable<(int X, int Y)> cells, bool latched )
		=> cells.Select( cell => new ParkPathBuilding.QueueSquare( cell.X, cell.Y,
			latched ? ParkPathBuilding.MarkerRed : ParkPathBuilding.MarkerBlue ) ).ToList();

	/// <summary>What the patrol edges read as their refusal: the latch, or a carried thing that will not go down.</summary>
	internal static bool PatrolLatch( bool latched, List<ParkBuilding.CarrySquare> carried ) => latched || CarriedRefused( carried );

	/// <summary>Whether a carried thing's squares are all red: the placer refused it.</summary>
	internal static bool CarriedRefused( List<ParkBuilding.CarrySquare> carried )
		=> carried.Count > 0 && carried.All( square => square.Marker == ParkPathBuilding.MarkerRed );

	/// <summary>The patrol edges queued for the next frame's mesh, for a test.</summary>
	internal IReadOnlyList<(int X, int Y)> PatrolQueued => _patrolQueued;

	/// <summary>The cell of every stranded guest, one a guest, as the per-frame placement queues them.</summary>
	private static List<(int X, int Y)> Stranded()
	{
		if ( ParkPeople.Current is not { } people )
			return [];

		return people.Guests.Values
			.Where( peep => peep.StrandedTime != 0 )
			.Select( peep => people.WalkFor( peep.ThingId ) )
			.Where( walk => walk != null )
			.Select( walk => walk!.Position.Cell )
			.ToList();
	}

	/// <summary>
	/// The squares the mesh holds now, the tool's each with its lift after a <c>^</c>, then a followed member's patrol
	/// edges, each with its lift, after <c>| patrol</c>, then the walls under both, each with its face byte, its marker
	/// and the lift its foot stands at, after <c>| walls</c>, then those under stranded guests after a <c>|</c>, and
	/// whether red is showing, for the debug console.
	/// </summary>
	public string Census()
	{
		string Line( Square square ) => $"{square.X},{square.Y},{square.Marker}^{square.Over - Lift:0}"
			+ (square.Turn is 0 or 0x01 ? "" : $"@{square.Turn:x2}") + (square.Size == 1 ? "" : $"#{square.Size}");

		var tool = _strip.Take( _toolSquares ).Select( Line ).ToArray();
		var patrol = _strip.Skip( _toolSquares ).Take( _patrolSquares ).Select( Line ).ToArray();
		var walls = _walls.Select( wall => $"{wall.X},{wall.Y},{wall.Face},{wall.Marker}^{wall.Over - Lift:0}"
			+ (wall.Size == 1 ? "" : $"#{wall.Size}") ).ToArray();
		var stranded = _strip.Skip( _toolSquares + _patrolSquares ).Select( square => $"{square.X},{square.Y}" ).ToArray();

		var gliding = _strip.Any( square => square.Glides );
		var (glideX, glideY) = GlideOffset();

		return (tool.Length == 0 ? "none" : string.Join( ";", tool ))
			+ (gliding ? $" | glide {glideX:0.0},{glideY:0.0}" : "")
			+ (patrol.Length == 0 ? "" : $" | patrol {patrol.Length} {string.Join( ";", patrol )}")
			+ (walls.Length == 0 ? "" : $" | walls {walls.Length} {string.Join( ";", walls )}")
			+ (stranded.Length == 0 ? "" : $" | stranded {string.Join( ";", stranded )} red {(_redShown ? "on" : "off")}");
	}

	/// <summary>
	/// The walls under a square lifted by <paramref name="lift"/>, given each neighbour's lift (null off the map), as
	/// <c>FUN_00532fc0</c>'s <c>0x100</c> arm queues them after the square: for each neighbour in turn, north
	/// (<c>DAT_007cdba0</c>), east, south and west, while the neighbour's lift stands under the square's, a wall with its
	/// foot at the neighbour's lift, then ten higher, until it reaches the square's (the arm for a square not refused, north's loop <c>0x0053337a</c>..<c>0x005333dc</c>; the refused arm's is <c>0x00533ad0</c>..<c>0x00533b22</c>).
	/// A neighbour as high or higher gets none; both lifts are whole tens, so the last wall's top is the square's edge.
	/// </summary>
	internal static List<(int Face, float Foot)> Walls( float lift, float? north, float? east, float? south, float? west )
	{
		var walls = new List<(int Face, float Foot)>();

		foreach ( var (face, neighbour) in new[] { (FaceNorth, north), (FaceEast, east), (FaceSouth, south), (FaceWest, west) } )
		{
			if ( neighbour is not { } below )
				continue;

			for ( var foot = (int)below; foot < (int)lift; foot += (int)WallHeight )
				walls.Add( (face, foot) );
		}

		return walls;
	}

	/// <summary>
	/// The walls under the square on (<paramref name="x"/>, <paramref name="y"/>) wearing <paramref name="marker"/>, lifted
	/// by <paramref name="lift"/>, its neighbours' lifts asked of <paramref name="liftAt"/> (null off the map): north at
	/// y − 1 (<c>DAT_007cdba0</c>, (0, −1)), east at x + 1 (<c>DAT_007cdbc8</c>), south at y + 1 (<c>DAT_007cdba8</c>),
	/// west at x − 1 (<c>DAT_007cdbd0</c>). Each record sits 1.5 over its foot, as a square's over its lift. A square on
	/// the ground has none.
	/// </summary>
	internal static List<Wall> WallsAt( int x, int y, float lift, int marker, Func<int, int, float?> liftAt ) => lift > 0f
		? Walls( lift, liftAt( x, y - 1 ), liftAt( x + 1, y ), liftAt( x, y + 1 ), liftAt( x - 1, y ) )
			.Select( wall => new Wall( x, y, wall.Face, WallMarker( marker ), RecordHeight( wall.Foot ) ) )
			.ToList()
		: [];

	/// <summary>
	/// The marker a wall wears under a square wearing <paramref name="marker"/>: red under red, and blue under every
	/// other the tools draw, <c>m_link</c> and <c>m_end</c> too (<c>FUN_00532fc0</c>: texture 1 in the refused arm,
	/// <c>0x00533b07</c>, and in the other 2 under orange and otherwise 0, <c>0x00533214</c>; no tool here draws orange).
	/// </summary>
	internal static int WallMarker( int marker ) => marker == ParkPathBuilding.MarkerRed
		? ParkPathBuilding.MarkerRed
		: ParkPathBuilding.MarkerBlue;

	private void Build( List<ParkPathBuilding.QueueSquare> strip, List<ParkBuilding.CarrySquare> carried,
		List<ParkPathBuilding.QueueSquare> patrol, List<(int X, int Y)> stranded )
	{
		TranslucentModel?.Delete();
		TranslucentModel = null;
		_strip = [];
		_walls = [];
		_toolSquares = 0;
		_patrolSquares = 0;

		if ( strip.Count + carried.Count + patrol.Count + stranded.Count == 0 || ParkGround.Current?.Heightfield is not { } field )
			return;

		if ( RipplesShade( Level.Current?.ParkLight ) )
			Unimplemented.Report( "MARKER_RIPPLE_SHADING" );

		if ( strip.Any( square => square.Marker is ParkPathBuilding.MarkerLink or ParkPathBuilding.MarkerEnd ) )
			Unimplemented.Report( "MARKER_ICON_TURNS_WITH_CAMERA" );

		bool OnTheField( int x, int y ) => x >= 0 && y >= 0 && x < field.CellsX && y < field.CellsY;

		// A carried thing's square and a patrol edge are the same square as a tool's (FUN_00532fc0's 0x100 arm), lifted and
		// walled the same way; a carried thing's glide (mode 4), the others not.
		// Deviation (cosmetic, a tenth of a second after the pointer changes cell): the original's shift (FUN_0053df30,
		// 0x0053df67..0x0053dfcf) applies to every square and wall whose no-glide word is 0 whenever the mode is 4, 0xe or
		// 0x13, and the patrol edges' red arm queues its square with that word 0 (0x0053394b); so while a thing is
		// carried a followed member's patrol edges slide in the original, and here they stand still.
		var squares = strip.Select( square => (square.X, square.Y, square.Marker, Turn: 0, Glides: false, Size: square.Wide ? 2 : 1) )
			.Concat( carried.Select( square => (square.X, square.Y, square.Marker, square.Turn, Glides: true, Size: 1) ) )
			.Concat( patrol.Select( square => (square.X, square.Y, square.Marker, Turn: 0, Glides: false, Size: 1) ) );

		float LiftAt( int x, int y ) => ParkState.Current is { } state
			? LiftOver( state, Level.Current?.Catalogue, field, x, y )
			: 0f;

		// The track tool's squares, two by two, are lifted and walled by their own rule over each cell's whole height.
		float HeightAt( int x, int y ) => ParkState.Current is { } state
			? ParkItemHeights.AtCell( state, Level.Current?.Catalogue, field, x, y )
			: 0f;

		float SquareLift( int x, int y, int size ) => size == 1 ? LiftAt( x, y )
			: ParkState.Current is { } state ? TrackLiftOver( state, x, y, HeightAt ) : 0f;

		float? NeighbourLift( int x, int y ) => OnTheField( x, y ) ? LiftAt( x, y ) : null;

		var lifts = squares.Where( square => OnTheField( square.X, square.Y ) )
			.Select( square => (Square: square, Lift: SquareLift( square.X, square.Y, square.Size )) )
			.ToList();

		_strip = lifts.Select( square => new Square( square.Square.X, square.Square.Y, square.Square.Marker, RecordHeight( square.Lift ),
				square.Square.Turn, square.Square.Glides, square.Square.Size ) )
			.ToList();
		_toolSquares = strip.Count( square => OnTheField( square.X, square.Y ) ) + carried.Count( square => OnTheField( square.X, square.Y ) );
		_patrolSquares = _strip.Count - _toolSquares;

		_walls = lifts.SelectMany( square => (square.Square.Size == 1 || ParkState.Current is not { } state
					? WallsAt( square.Square.X, square.Square.Y, square.Lift, square.Square.Marker, NeighbourLift )
					: TrackWalls( state, square.Square.X, square.Square.Y, square.Lift, square.Square.Marker, HeightAt ))
				.Select( wall => wall with { Glides = square.Square.Glides } ) )
			.ToList();

		// Each wall under a square not refused also zeroes the refusal latches DAT_00816d50 and d58 (0x005333bd). That
		// changes nothing the path and queue tools read, so it is not done: such a square leaves both at nought already,
		// but for the queue's join, the strip's last square, whose path cell only a track thing lifts (the path cell of a path laid over a
		// kart piece, which ours' stamp lays, counted PATH_RUN_OVER_TRACK, lifted by the piece; or a boundary thing's, lifted
		// by its .hmp), and the d58 a join leaves is read next by a pass, which clears it first.
		// docs/exe/park-engine.md, "The walls".

		_strip.AddRange( stranded.Where( cell => OnTheField( cell.X, cell.Y ) )
			.Select( cell => new Square( cell.X, cell.Y, ParkPathBuilding.MarkerRed, StrandedLift ) ) );

		_vertices = new Vertex[(_strip.Count + _walls.Count) * 4];

		if ( Fill( field ) == 0 )
			return;

		var elements = new uint[(_strip.Count + _walls.Count) * 6];
		var element = 0;

		for ( var corner = 0u; corner < _vertices.Length; corner += 4 )
		{
			elements[element++] = corner;
			elements[element++] = corner + 1;
			elements[element++] = corner + 2;

			elements[element++] = corner;
			elements[element++] = corner + 2;
			elements[element++] = corner + 3;
		}

		// Both faces drawn, where the original culls (render-states.md, "Which draws the test covers"; Q613).
		var material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader", MaterialFlags.DisableCulling );
		material.Set( "Color", _textures );

		TranslucentModel = new Model( _vertices, elements, material );
		TranslucentModel.EnableFrequentUpdates( _vertices );
	}

	/// <summary>Lays the four corners of every square and wall at the wave's current height; answers how many were laid.</summary>
	private int Fill( HeightfieldFile field )
	{
		var vertex = 0;

		var glide = GlideOffset();

		foreach ( var (x, y, marker, over, turn, glides, size) in _strip )
		{
			var slot = Math.Max( 0, Array.IndexOf( Drawn, marker ) );
			var offset = glides ? glide : (0f, 0f);
			var uvs = TurnedUvs( turn );

			// A red square that is not showing is laid with no area: all four corners on its first.
			var gone = marker == ParkPathBuilding.MarkerRed && !_redShown;

			// A wide square is one quad over its cell's two by two, 20 by 20 (FUN_0053df30, 0x0053df39).
			_vertices[vertex++] = Corner( field, x, y, uvs[0], slot, over, offset );
			_vertices[vertex++] = Corner( field, gone ? x : x + size, y, uvs[1], slot, over, offset );
			_vertices[vertex++] = Corner( field, gone ? x : x + size, gone ? y : y + size, uvs[2], slot, over, offset );
			_vertices[vertex++] = Corner( field, x, gone ? y : y + size, uvs[3], slot, over, offset );
		}

		foreach ( var wall in _walls )
		{
			var slot = Math.Max( 0, Array.IndexOf( Drawn, wall.Marker ) );
			var gone = wall.Marker == ParkPathBuilding.MarkerRed && !_redShown;
			var offset = wall.Glides ? glide : (0f, 0f);

			foreach ( var (x, y, over, uv) in WallCorners( wall, gone ) )
				_vertices[vertex++] = Corner( field, x, y, uv, slot, over, offset );
		}

		return vertex;
	}

	/// <summary>
	/// A wall's four corners: the foot along its edge at the record's height, then the top a wall higher, u along the
	/// edge and v up it (<c>FUN_0053df30</c>'s UV pairs 0 to 3). The wave and the ground under each corner are added as
	/// any square's, alike at foot and top, so the wall stays a wall high. A wall that is <paramref name="gone"/> (red,
	/// blinked off) is laid with no area: all four on its first corner.
	/// </summary>
	internal static (int X, int Y, float Over, Vector2 Uv)[] WallCorners( Wall wall, bool gone )
	{
		var ((fromX, fromY), (toX, toY)) = Edge( wall.X, wall.Y, wall.Face, wall.Size );

		if ( gone )
			return [.. Enumerable.Repeat( (fromX, fromY, wall.Over, Vector2.Zero), 4 )];

		return
		[
			(fromX, fromY, wall.Over, new Vector2( 0f, 0f )),
			(toX, toY, wall.Over, new Vector2( 1f, 0f )),
			(toX, toY, wall.Over + WallHeight, new Vector2( 1f, 1f )),
			(fromX, fromY, wall.Over + WallHeight, new Vector2( 0f, 1f ))
		];
	}

	/// <summary>
	/// The two ground corners of the edge a wall stands on, in the order <c>FUN_0053df30</c> lays its foot: north from
	/// (x, y) to (x + 1, y), east from (x + 1, y) to (x + 1, y + 1), south from (x + 1, y + 1) to (x, y + 1), west from
	/// (x, y + 1) to (x, y), each <paramref name="size"/> cells for a wall that long (20 wide, <c>FUN_0053df30</c> adding its
	/// size, 20 where the record's <c>+0x18</c> is set, to the corner it lays first).
	/// </summary>
	internal static ((int X, int Y) From, (int X, int Y) To) Edge( int x, int y, int face, int size = 1 ) => face switch
	{
		FaceNorth => ((x, y), (x + size, y)),
		FaceEast => ((x + size, y), (x + size, y + size)),
		FaceSouth => ((x + size, y + size), (x, y + size)),
		FaceWest => ((x, y + size), (x, y)),
		_ => throw new ArgumentOutOfRangeException( nameof( face ), face, "not a wall's face" )
	};

	/// <summary>How far a gliding square is drawn from its own cell now (<see cref="GlideOffset(ValueTuple{float, float}, ValueTuple{float, float})"/>).</summary>
	private (float X, float Y) GlideOffset() => GlideOffset( _glide, _glideTarget );

	/// <summary>
	/// How far a gliding square is drawn from its own cell: the glide's point less the pointer's cell's corner, as
	/// <c>FUN_0053df30</c> draws it at <c>x − cell × 10 + DAT_00820afc</c>.
	/// </summary>
	internal static (float X, float Y) GlideOffset( (float X, float Y) glide, (float X, float Y) target )
		=> (glide.X - target.X, glide.Y - target.Y);

	/// <summary>
	/// The texture coordinates of a square's corners, in the order <see cref="Fill"/> lays them - (x, y), (x + 1, y),
	/// (x + 1, y + 1), (x, y + 1) - for the compass bit it is turned by: <c>FUN_0053df30</c>'s choice among the UV pairs
	/// <c>0x00871ee8</c> holds, (0, 0), (1, 0), (0, 1) and (1, 1) (<c>FUN_0053c3f0</c> writes them every frame). East's
	/// <c>0x04</c>, south's <c>0x10</c> and west's <c>0x40</c> turn it; anything else leaves it as north's <c>0x01</c> does.
	/// </summary>
	internal static Vector2[] TurnedUvs( int turn ) => turn switch
	{
		0x04 => [new( 0f, 1f ), new( 0f, 0f ), new( 1f, 0f ), new( 1f, 1f )],
		0x10 => [new( 1f, 1f ), new( 0f, 1f ), new( 0f, 0f ), new( 1f, 0f )],
		0x40 => [new( 1f, 0f ), new( 1f, 1f ), new( 0f, 1f ), new( 0f, 0f )],
		_ => [new( 0f, 0f ), new( 1f, 0f ), new( 1f, 1f ), new( 0f, 1f )]
	};

	/// <summary>
	/// The ground a square's corner stands on: the corner's own height, or, gliding, the ground under where it is drawn, as
	/// <c>FUN_0053ddd0</c> asks it after the shift (<c>FUN_004527f0</c>, <see cref="HeightfieldFile.ScapeHeight"/>).
	/// </summary>
	internal static float CornerGround( HeightfieldFile field, int x, int y, (float X, float Y) offset )
		=> offset == (0f, 0f) ? field.HeightAt( x, y ) : field.ScapeHeight( (x * field.CellSizeX) + offset.X, (y * field.CellSizeY) + offset.Y );

	private Vertex Corner( HeightfieldFile field, int x, int y, Vector2 uv, int slot, float over, (float X, float Y) offset )
	{
		var (worldX, worldY) = ((x * field.CellSizeX) + offset.X, (y * field.CellSizeY) + offset.Y);

		var ground = CornerGround( field, x, y, offset );

		return new()
		{
			Position = new Vector3( worldX, worldY, ground + over + Wave( _phase, worldX, worldY ) ),
			Normal = ParkGround.NormalAt( field, x, y ),
			TexCoords = uv,
			TexIndex = slot,
			MatFlags = Translucent
		};
	}

	/// <summary>
	/// How far a square over a cell is lifted before the ground under its corner is added - the original's
	/// <c>ceil10( trunc( FUN_00452ae0 ) )</c> (<c>0x005330ca</c>). Nought except over a cell of type 4, 7, 9, 10 or
	/// <c>0x1e</c> or a cell of track kind 11, 12, 16, 17 or 25, and then the height of what the cell holds, its
	/// track-layer object asked first (<see cref="ParkItemHeights.AtCell"/>). A kart or water piece or a boundary hoarding
	/// (and a 0xc or 0x11 cell parented to one) answers its own <c>.hmp</c>; a cell whose record names no piece is counted
	/// (<c>MARKER_LIFT_OVER_TRACK_THING</c>) and the square stays on the ground. <b>The height asked already
	/// includes the ground under the thing, so on raised ground the ground is counted twice</b>, once here and once
	/// as the corner is laid; this keeps that, as the original draws it.
	/// </summary>
	internal static float LiftOver( ParkState state, ParkItemCatalogue? catalogue, HeightfieldFile field, int x, int y,
		string? theme = null )
	{
		var cell = state.Record( x, y );

		if ( cell.Type is CellEdge.Footprint or 7 or CellEdge.RideEnd or CellEdge.RideFarEnd or CellEdge.Approach )
			return CeilingTen( (int)ParkItemHeights.AtCell( state, catalogue, field, x, y, theme ) );

		if ( cell.TrackType is not (11 or 12 or 16 or 17 or 25) )
			return 0f;

		if ( ParkItemHeights.PieceTopAt( state, field, x, y, theme ) is not { } top )
		{
			Unimplemented.Report( "MARKER_LIFT_OVER_TRACK_THING" );
			return 0f;
		}

		return CeilingTen( (int)top );
	}

	/// <summary>
	/// How far a track tool's square over the anchor (<paramref name="x"/>, <paramref name="y"/>) is lifted, each cell's
	/// height asked of <paramref name="heightAt"/> (<c>FUN_00452ae0( x, y, null )</c>, <see cref="ParkItemHeights.AtCell"/>).
	/// <c>Track_CellOp</c> starts from nought, or where the anchor's TRACK type is one of <see cref="LiftsTrackSquare"/> from
	/// the anchor's height rounded up to ten (<c>0x0053716f</c>..<c>0x00537239</c>); then <c>TrackSquare_Height</c>
	/// (<c>FUN_00536ca0</c>) asks the MAP type of its two by two, its own cell, south, east and the far corner
	/// (<c>DAT_007cdba8</c>, <c>DAT_007cdbc8</c>, <c>DAT_007cdbb8</c>), and each of the set replaces the lift with its own
	/// height rounded up to ten, so the last such cell gives it. Every type of the set is rounded alike.
	/// </summary>
	internal static float TrackLiftOver( ParkState state, int x, int y, Func<int, int, float> heightAt )
	{
		var lift = LiftsTrackSquare( state.Record( x, y ).TrackType ) ? CeilingTen( (int)heightAt( x, y ) ) : 0f;

		foreach ( var (cellX, cellY) in new[] { (x, y), (x, y + 1), (x + 1, y), (x + 1, y + 1) } )
		{
			if ( ParkState.OnMap( cellX, cellY ) && LiftsTrackSquare( state.Record( cellX, cellY ).Type ) )
				lift = CeilingTen( (int)heightAt( cellX, cellY ) );
		}

		return lift;
	}

	/// <summary>The types a track square's lift is read over: <c>0xb</c>, <c>0x10</c>, <c>0x19</c>, 7, 4, 9 and 10 (<c>FUN_00536ca0</c>).</summary>
	internal static bool LiftsTrackSquare( int type ) => type is 0xb or 0x10 or 0x19 or 7 or 4 or 9 or 10;

	/// <summary>
	/// The walls under a track tool's square over (<paramref name="x"/>, <paramref name="y"/>) lifted by
	/// <paramref name="lift"/>: <c>Track_CellOp</c> asks the anchors two cells away on the track grid (<c>FUN_0050c320</c>
	/// with 2), north, east, south and west, none off the map, each lifted by <see cref="TrackLiftOver"/>, and lays a wall
	/// on that edge of the square's two by two from the neighbour's lift up by tens while under the square's
	/// (north's loop <c>0x00537494</c>..<c>0x005374c8</c>), 20 wide as the square (<c>FUN_0053df30</c>'s <c>+0x18</c>). A
	/// wall is red under a red square (the refused arm's texture 1, <c>0x00537bf9</c>) and blue under every other.
	/// </summary>
	internal static List<Wall> TrackWalls( ParkState state, int x, int y, float lift, int marker, Func<int, int, float> heightAt )
	{
		float? Neighbour( int cellX, int cellY ) => ParkState.OnMap( cellX, cellY ) ? TrackLiftOver( state, cellX, cellY, heightAt ) : null;

		return Walls( lift, Neighbour( x, y - 2 ), Neighbour( x + 2, y ), Neighbour( x, y + 2 ), Neighbour( x - 2, y ) )
			.Select( wall => new Wall( x, y, wall.Face, WallMarker( marker ), RecordHeight( wall.Foot ), Size: 2 ) )
			.ToList();
	}

	/// <summary>The original's <c>(t + 9) / 10 * 10.0</c>, integer division towards nought.</summary>
	internal static float CeilingTen( int truncated ) => (truncated + 9) / 10 * 10f;

	/// <summary>
	/// The height a square's record carries over a cell lifted by <paramref name="lift"/>: <c>1.5</c> over it, as the
	/// original's records read (21.5 over the Belly Bounce, 11.5 and 1.5 beside it; <c>docs/exe/park-engine.md</c>,
	/// "Placement feedback").
	/// </summary>
	internal static float RecordHeight( float lift ) => Lift + lift;

	/// <summary>
	/// How far the wave lifts a corner - <c>sin( phase + x + z )</c> in the original's own axes, its
	/// ground-plane x and z being this project's x and y. One world unit either way, a tenth of a cell.
	/// </summary>
	internal static float Wave( float phase, float worldX, float worldY ) => MathF.Sin( phase + worldX + worldY );

	protected override void OnDelete()
	{
		base.OnDelete();

		if ( Current == this )
			Current = null;

		ParkItemHeights.Clear();
	}
}
