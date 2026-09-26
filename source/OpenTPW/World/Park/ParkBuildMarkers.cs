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
/// frame while the game is not paused. Alexah, who played the original: "they were translucent. They
/// waved like a flag/water."
/// <para>
/// Four things are counted rather than guessed: the brightening and dimming that goes with the wave (the
/// same sine scales the vertex's up vector, which this shader normalises away), the blink of a red square
/// (a counter of unestablished unit), the turning of <c>m_link</c> and <c>m_end</c> to face the camera,
/// and the extra lift over a cell of type 4, 9, 10, 7 or <c>0x1e</c> (<c>ceil10( FUN_00452ae0 )</c>, whose
/// answer is not decoded).
/// </para>
/// </para>
/// </summary>
public sealed class ParkBuildMarkers : ModelEntity
{
	/// <summary>The markers of the park currently loaded, or null outside one.</summary>
	public static ParkBuildMarkers? Current { get; private set; }

	/// <summary>How far above the ground a square sits - the <c>1.5</c> in <c>FUN_0053df30</c>'s corner height.</summary>
	private const float Lift = 1.5f;

	/// <summary>
	/// The marker table at <c>0x00763b38</c>, in its own order: an index there is the texture a square
	/// wears. Loaded from <c>data/generic/dynamic/textures</c>, the folder <c>FUN_0053d790</c> searches
	/// first.
	/// </summary>
	private static readonly string[] MarkerNames =
	[
		"blue", "red", "orange", "m_enter", "m_exit", "m_direct", "m_front", "m_inout", "m_link", "m_break",
		"m_cross", "m_end", "m_nocash", "m_erase", "red", "gby_sur1", "gby_sur2", "gby_sur3", "gte_lgo1", "gte_lgo1"
	];

	/// <summary>The four the queue tool draws, in the material's slot order.</summary>
	private static readonly int[] Drawn =
	[
		ParkPathBuilding.MarkerBlue, ParkPathBuilding.MarkerRed, ParkPathBuilding.MarkerLink, ParkPathBuilding.MarkerEnd
	];

	/// <summary>The shader's own see-through bit - see <c>test.shader</c>'s <c>FLAG_TRANSLUCENT</c>.</summary>
	private const uint Translucent = 2;

	private readonly Texture[] _textures = new Texture[16];

	/// <summary>The strip last built, so the mesh is laid again only when it changes.</summary>
	private string _built = string.Empty;

	/// <summary>The squares the mesh holds now, and its vertices, so the wave can move them every frame.</summary>
	private List<ParkPathBuilding.QueueSquare> _strip = [];

	private Vertex[] _vertices = [];

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
			_phase = (_phase + (WavePerSecond * Time.Delta)) % (MathF.PI * 2f);

		var strip = ParkPicking.TryCell( out var x, out var y )
			? ParkPathBuilding.Strip( x, y )
			: [];

		var built = string.Join( ";", strip.Select( square => $"{square.X},{square.Y},{square.Marker}" ) );

		if ( built != _built )
		{
			_built = built;
			Build( strip );

			return;
		}

		// The same squares: only the wave has moved, so the heights are laid again in place.
		if ( TranslucentModel is { } model && ParkGround.Current?.Heightfield is { } field && Fill( field ) > 0 )
			model.UpdateVertices( _vertices );
	}

	/// <summary>The squares the pointer is over now, for the debug console - the same list the mesh is built from.</summary>
	public string Census() => _built.Length == 0 ? "none" : _built;

	private void Build( List<ParkPathBuilding.QueueSquare> strip )
	{
		TranslucentModel?.Delete();
		TranslucentModel = null;

		if ( strip.Count == 0 || ParkGround.Current?.Heightfield is not { } field )
			return;

		Unimplemented.Report( "MARKER_RIPPLE_SHADING" );

		if ( strip.Any( square => square.Marker is ParkPathBuilding.MarkerLink or ParkPathBuilding.MarkerEnd ) )
			Unimplemented.Report( "MARKER_ICON_TURNS_WITH_CAMERA" );

		if ( strip.Any( square => square.Marker == ParkPathBuilding.MarkerRed ) )
			Unimplemented.Report( "MARKER_RED_BLINK_TIMING" );

		if ( Level.Current?.Park is { } park && strip.Any( square => ParkState.OnMap( square.X, square.Y )
			&& ParkState.CellFor( park, square.X, square.Y ).Type is CellEdge.Footprint or CellEdge.RideEnd
				or CellEdge.RideFarEnd or 7 or 0x1e ) )
			Unimplemented.Report( "MARKER_LIFT_OVER_BUILT_CELL" );

		_strip = strip.Where( square => square.X >= 0 && square.Y >= 0 && square.X < field.CellsX && square.Y < field.CellsY ).ToList();
		_vertices = new Vertex[_strip.Count * 4];

		if ( Fill( field ) == 0 )
			return;

		var elements = new uint[_strip.Count * 6];
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

		var material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader", MaterialFlags.DisableCulling );
		material.Set( "Color", _textures );

		TranslucentModel = new Model( _vertices, elements, material );
		TranslucentModel.EnableFrequentUpdates( _vertices );
	}

	/// <summary>Lays the four corners of every square at the wave's current height; answers how many were laid.</summary>
	private int Fill( HeightfieldFile field )
	{
		var vertex = 0;

		foreach ( var (x, y, marker, _, _) in _strip )
		{
			var slot = Math.Max( 0, Array.IndexOf( Drawn, marker ) );

			_vertices[vertex++] = Corner( field, x, y, new Vector2( 0f, 0f ), slot );
			_vertices[vertex++] = Corner( field, x + 1, y, new Vector2( 1f, 0f ), slot );
			_vertices[vertex++] = Corner( field, x + 1, y + 1, new Vector2( 1f, 1f ), slot );
			_vertices[vertex++] = Corner( field, x, y + 1, new Vector2( 0f, 1f ), slot );
		}

		return vertex;
	}

	private Vertex Corner( HeightfieldFile field, int x, int y, Vector2 uv, int slot )
	{
		var (worldX, worldY) = (x * field.CellSizeX, y * field.CellSizeY);

		return new()
		{
			Position = new Vector3( worldX, worldY, field.HeightAt( x, y ) + Lift + Wave( _phase, worldX, worldY ) ),
			Normal = ParkGround.NormalAt( field, x, y ),
			TexCoords = uv,
			TexIndex = slot,
			MatFlags = Translucent
		};
	}

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
	}
}
