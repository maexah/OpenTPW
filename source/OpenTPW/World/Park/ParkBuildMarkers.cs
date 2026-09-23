namespace OpenTPW;

/// <summary>
/// The coloured squares the queue tool lays over the ground from its anchor to the pointer - the
/// original's <c>BlueprintMesh</c>, one of the three "dynamic faces" meshes <c>FUN_0053bfe0</c> builds.
///
/// <para>
/// <b>This is what makes the node a player lays a queue from visible before anything is clicked.</b>
/// The strip always starts at the anchor, so the moment a queued ride goes down the cell before its
/// entrance carries a square, and moving the pointer draws the run a click would lay: blue where it
/// may go, red where it may not, <c>m_link</c> where it would join a path and <c>m_end</c> where it
/// would close on the ride's own queue. See <see cref="ParkPathBuilding.QueueStrip"/>, which the
/// click itself obeys.
/// </para>
/// <para>
/// <b>One square a cell, flat, 10 by 10, at the ground's own corner heights plus 1.5</b> - the
/// original's <c>FUN_0053df30</c> face 0. Four things it does are counted rather than guessed, because
/// their numbers are not decoded: the ripple (a sine term whose amplitude table was not read), the
/// blink of a red square (a counter of unestablished unit), the turning of <c>m_link</c> and
/// <c>m_end</c> to face the camera, and the extra lift over a cell of type 4, 9, 10, 7 or <c>0x1e</c>
/// (<c>ceil10( FUN_00452ae0 )</c>, whose answer is not decoded).
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
		var strip = ParkPicking.TryCell( out var x, out var y )
			? ParkPathBuilding.QueueStrip( x, y )
			: [];

		var built = string.Join( ";", strip.Select( square => $"{square.X},{square.Y},{square.Marker}" ) );

		if ( built == _built )
			return;

		_built = built;
		Build( strip );
	}

	/// <summary>The squares the pointer is over now, for the debug console - the same list the mesh is built from.</summary>
	public string Census() => _built.Length == 0 ? "none" : _built;

	private void Build( List<ParkPathBuilding.QueueSquare> strip )
	{
		TranslucentModel?.Delete();
		TranslucentModel = null;

		if ( strip.Count == 0 || ParkGround.Current?.Heightfield is not { } field )
			return;

		Unimplemented.Report( "MARKER_RIPPLE_AMPLITUDE" );

		if ( strip.Any( square => square.Marker is ParkPathBuilding.MarkerLink or ParkPathBuilding.MarkerEnd ) )
			Unimplemented.Report( "MARKER_ICON_TURNS_WITH_CAMERA" );

		if ( strip.Any( square => square.Marker == ParkPathBuilding.MarkerRed ) )
			Unimplemented.Report( "MARKER_RED_BLINK_TIMING" );

		if ( Level.Current?.Park is { } park && strip.Any( square => ParkState.OnMap( square.X, square.Y )
			&& ParkState.CellFor( park, square.X, square.Y ).Type is CellEdge.Footprint or CellEdge.RideEnd
				or CellEdge.RideFarEnd or 7 or 0x1e ) )
			Unimplemented.Report( "MARKER_LIFT_OVER_BUILT_CELL" );

		var vertices = new Vertex[strip.Count * 4];
		var elements = new uint[strip.Count * 6];
		var vertex = 0;
		var element = 0;

		foreach ( var (x, y, marker, _, _) in strip )
		{
			if ( x < 0 || y < 0 || x >= field.CellsX || y >= field.CellsY )
				continue;

			var slot = Math.Max( 0, Array.IndexOf( Drawn, marker ) );
			var corner = (uint)vertex;

			vertices[vertex++] = Corner( field, x, y, new Vector2( 0f, 0f ), slot );
			vertices[vertex++] = Corner( field, x + 1, y, new Vector2( 1f, 0f ), slot );
			vertices[vertex++] = Corner( field, x + 1, y + 1, new Vector2( 1f, 1f ), slot );
			vertices[vertex++] = Corner( field, x, y + 1, new Vector2( 0f, 1f ), slot );

			elements[element++] = corner;
			elements[element++] = corner + 1;
			elements[element++] = corner + 2;

			elements[element++] = corner;
			elements[element++] = corner + 2;
			elements[element++] = corner + 3;
		}

		if ( element == 0 )
			return;

		var material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader", MaterialFlags.DisableCulling );
		material.Set( "Color", _textures );

		TranslucentModel = new Model( vertices[..vertex], elements[..element], material );
	}

	private static Vertex Corner( HeightfieldFile field, int x, int y, Vector2 uv, int slot )
		=> new()
		{
			Position = new Vector3( x * field.CellSizeX, y * field.CellSizeY, field.HeightAt( x, y ) + Lift ),
			Normal = ParkGround.NormalAt( field, x, y ),
			TexCoords = uv,
			TexIndex = slot,
			MatFlags = Translucent
		};

	protected override void OnDelete()
	{
		base.OnDelete();

		if ( Current == this )
			Current = null;
	}
}
