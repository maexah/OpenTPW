namespace OpenTPW;

/// <summary>
/// The walkways a park was laid out with - the paths the player built, drawn from the park's own save.
///
/// <para>
/// They are not in the ground model and not in the attribute map. They are in the World block's map
/// cells, one tile a cell, and each cell stores <b>which tile it draws and which way that tile is
/// turned</b> rather than leaving either to be worked out from its neighbours. See
/// <see cref="ParkWorld.MapCell"/>.
/// </para>
///
/// <para>
/// <b>This draws instead of the ground, never on top of it.</b> A path cell is ordinary drawn ground in
/// <c>base.MD2</c> - measured, and worth stating because the opposite was believed for a while: all 82
/// of Lost Kingdom's path and queue cells carry a real ground texture index and none is the
/// "something covers this" index 0, which is the river and the fixed roads. So <see cref="ParkGround"/>
/// skips the cells that land here, and the two surfaces meet edge to edge at the same heights rather
/// than fighting over the same depth.
/// </para>
///
/// <para>
/// A second reason not to fold this into the ground: the ground's material holds sixteen textures and
/// the jungle already spends six on its grass. Eleven more for the paths would not fit, and a surface
/// of its own costs nothing else.
/// </para>
///
/// <para>
/// <b>Queues are drawn elsewhere, by <see cref="ParkQueues"/>, and are not paths at all.</b> This said
/// they were "deliberately left out" because their tile index of 5 exceeded the theme's <c>QueueTex</c>
/// rows 0 to 3 and "what a queue tile is indexed by is not yet known". It is known: a queue is built
/// from the railed models the theme keeps in its own <c>queue.wad</c>, chosen by the exe's piece table,
/// not from a ground tile - which is why the index never fitted the texture rows.
/// </para>
/// </summary>
public sealed class ParkPaths : ModelEntity
{
	/// <summary>
	/// The paths of the park currently loaded, or null outside one - the same way the ground publishes
	/// itself.
	/// </summary>
	public static ParkPaths? Current { get; private set; }

	/// <summary>How many cells were drawn as path - for the log, and for anything wanting to check.</summary>
	public int Drawn { get; private set; }

	private readonly string _themeName;

	/// <summary>The park this was built from, so that it can be built again when a cell changes.</summary>
	private readonly ParkWorld? _world;

	public ParkPaths( string themeName, ParkWorld? world )
	{
		_themeName = themeName;
		_world = world;
		Name = $"{themeName} paths";

		Build( world );

		Current = this;
	}

	/// <summary>
	/// Lays the walkways again, because a cell has changed - a path built, a path deleted.
	///
	/// <para>
	/// <b>The same shape as <see cref="ParkGround.Rebuild"/>, deliberately.</b> The paths are one model
	/// of every path cell in the park, so there is no per-cell edit to make, and the two surfaces have
	/// to be rebuilt together or they disagree about which cell belongs to whom: the ground stops
	/// drawing grass on a cell the moment the overlay calls it a path, so a path that did not rebuild
	/// alongside it would leave <b>a hole</b> rather than a walkway.
	/// </para>
	/// <para>
	/// <b>The old model is let go of first</b>, for the reason the ground gives: a
	/// <see cref="ModelEntity"/> owns its model and the material bound into it, and building over the
	/// top keeps both for the life of the process.
	/// </para>
	/// </summary>
	public void Rebuild()
	{
		Model?.Delete();
		Model = null!;

		Build( _world );
	}

	/// <summary>
	/// Lets go of <see cref="Current"/>, but only if it is still this one - the same guard the ground
	/// uses, for a scene that builds its replacement before tearing down its predecessor.
	/// </summary>
	protected override void OnDelete()
	{
		// ModelEntity.OnDelete is what lets go of the model and, with it, the material - and an
		// override that does not chain to it keeps both for the life of the process. The path network is a model
		// and a material a park builds from scratch every time it is loaded.
		base.OnDelete();

		if ( Current == this )
			Current = null;
	}

	/// <summary>
	/// The tile set a cell draws from: 1 is a path and 2 a queue, and 0 means the cell draws no tile at
	/// all. Confirmed by the split it makes - every one of the 78 path cells reads 1, every one of the 4
	/// queue cells reads 2, and all 16,302 others read 0 - which is exactly the boundary the theme's
	/// <c>.tct</c> draws between its <c>PathTex</c> and <c>QueueTex</c> sections.
	/// </summary>
	public const int PathTileSet = 1;

	/// <summary>The section of the theme's <c>.tct</c> that names a path tile.</summary>
	private const string PathSection = "PathTex";

	/// <summary>
	/// Whether a cell is one this draws, which is the same question <see cref="ParkGround"/> asks in
	/// order to leave it alone. Kept here so the two can never disagree about which cells are whose.
	/// </summary>
	public static bool IsPath( ParkWorld.MapCell cell ) => cell.TileSet == PathTileSet;

	private void Build( ParkWorld? world )
	{
		// Without a save there are no paths - which is the ordinary case for three of the four themes,
		// since only the jungle ships a park. Not a failure, and it says so once rather than warning.
		if ( world == null || world.Cells.Count == 0 )
		{
			Log.Info( $"{_themeName}: no saved park, so it has no paths of its own" );
			return;
		}

		// The ground read the heightfield already, and the paths sit on exactly the same grid at exactly
		// the same heights, so asking it is both cheaper and safer than reading base.MD2 a second time.
		var field = ParkGround.Current?.Heightfield;

		if ( field == null )
		{
			Log.Warning( $"{_themeName}: the ground is not built, so its paths have nothing to lie on" );
			return;
		}

		// Which tiles this park actually uses, in ascending order, each becoming one slot of the material.
		var indices = new List<int>();
		var cells = new List<(int X, int Y, ParkWorld.MapCell Cell)>();

		for ( var y = 0; y < field.CellsY; ++y )
		{
			for ( var x = 0; x < field.CellsX; ++x )
			{
				// The RUNNING park's answer, not the file's - a cell a player has laid a path on since the
				// park loaded has to be drawn as one, and ParkWorld cannot record that. It falls through
				// to the save for every cell nobody has changed, which is all of them until somebody
				// builds something. This is the same seam ParkGround reads, and the two have to agree:
				// the ground stops drawing grass the moment the overlay calls a cell a path, so a path
				// that did not read the overlay too would leave a hole exactly where the walkway belongs.
				var cell = ParkState.CellFor( world, x, y );

				if ( !IsPath( cell ) )
					continue;

				cells.Add( (x, y, cell) );

				if ( !indices.Contains( cell.TileIndex ) )
					indices.Add( cell.TileIndex );
			}
		}

		if ( cells.Count == 0 )
		{
			Log.Info( $"{_themeName}: the saved park has no paths in it" );
			return;
		}

		indices.Sort();

		var textures = new Texture[16];
		var directory = $"levels/{_themeName.ToLowerInvariant()}/terrain/pathtex";
		var table = ReadTable();

		if ( indices.Count > textures.Length )
			Log.Warning( $"{_themeName}: the paths use {indices.Count} tiles where a material holds {textures.Length}" );

		for ( var slot = 0; slot < textures.Length; ++slot )
			textures[slot] = slot < indices.Count
				? Resolve( table, indices[slot], directory )
				: Texture.Missing;

		Log.Info( $"{_themeName}: path tiles - " + string.Join( ", ",
			indices.Select( index => $"{index}:{TileName( table, index )}" ) ) );

		var vertices = new Vertex[cells.Count * 4];
		var elements = new uint[cells.Count * 6];

		var vertex = 0;
		var element = 0;
		var corners = new int[4];

		foreach ( var (x, y, cell) in cells )
		{
			var slot = Math.Clamp( indices.IndexOf( cell.TileIndex ), 0, textures.Length - 1 );

			TurnCorners( cell.TileAngle, corners );

			var corner = vertex;

			vertices[vertex++] = Corner( field, x, y, CornerUvs[corners[0]], slot );
			vertices[vertex++] = Corner( field, x + 1, y, CornerUvs[corners[1]], slot );
			vertices[vertex++] = Corner( field, x + 1, y + 1, CornerUvs[corners[2]], slot );
			vertices[vertex++] = Corner( field, x, y + 1, CornerUvs[corners[3]], slot );

			// Split the same way the ground splits its own quads, so a path and the grass beside it are
			// cut alike and the seam between them cannot show.
			elements[element++] = (uint)corner;
			elements[element++] = (uint)(corner + 1);
			elements[element++] = (uint)(corner + 2);

			elements[element++] = (uint)corner;
			elements[element++] = (uint)(corner + 2);
			elements[element++] = (uint)(corner + 3);
		}

		var material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader", MaterialFlags.DisableCulling );
		material.Set( "Color", textures );

		Model = new Model( vertices[..vertex], elements[..element], material );
		Drawn = element / 6;

		Log.Info( $"{_themeName}: {Drawn} path cells drawn from {indices.Count} tiles" );
	}

	/// <summary>
	/// The theme's texture table, or null where it will not read - in which case every tile falls back to
	/// the not-found texture and the paths are still laid in the right places, which is far more useful
	/// than no paths at all.
	/// </summary>
	private TextureTableFile? ReadTable()
	{
		var theme = _themeName.ToLowerInvariant();
		var path = $"levels/{theme}/terrain/{theme}.tct";

		try
		{
			using var stream = FileSystem.OpenRead( path );
			return new TextureTableFile( stream );
		}
		catch ( Exception e )
		{
			Log.Warning( $"{_themeName}: {path} would not read, so no path tile can be named - {e.Message}" );
			return null;
		}
	}

	private static string TileName( TextureTableFile? table, int index )
		=> table?.NameFor( PathSection, index ) ?? string.Empty;

	/// <summary>
	/// The texture a tile index draws with. The table names them <c>.tga</c> and the files beside it are
	/// the same stem as <c>.wct</c>, which is the one translation this needs - the same swap the ground
	/// makes for its own names.
	/// </summary>
	private Texture Resolve( TextureTableFile? table, int index, string directory )
	{
		var name = TileName( table, index );

		if ( string.IsNullOrEmpty( name ) )
			return Texture.Missing;

		var dot = name.LastIndexOf( '.' );
		var stem = dot > 0 ? name[..dot] : name;

		try
		{
			return new Texture( $"{directory}/{stem}.wct", TextureFlags.Repeat );
		}
		catch ( Exception e )
		{
			Log.Warning( $"{_themeName}: path tile {index} ('{name}') would not load - {e.Message}" );
			return Texture.Missing;
		}
	}

	/// <summary>
	/// One corner of one cell, on the same grid and at the same height as the ground's own corners - the
	/// normal included, which is why it comes from <see cref="ParkGround.NormalAt"/> rather than being
	/// worked out again here. A path lies on the land, so it is lit as the land is.
	/// </summary>
	private static Vertex Corner( HeightfieldFile field, int x, int y, Vector2 uv, int slot )
		=> new()
		{
			Position = new Vector3( x * field.CellSizeX, y * field.CellSizeY, field.HeightAt( x, y ) ),
			Normal = ParkGround.NormalAt( field, x, y ),
			TexCoords = uv,
			TexIndex = slot,
			MatFlags = 0
		};

	/// <summary>
	/// The texture coordinate belonging to each corner of a cell, in the order the four are emitted: the
	/// origin corner, then across, then across and along, then along. The same ring the ground uses.
	/// </summary>
	private static readonly Vector2[] CornerUvs =
	{
		new( 0f, 0f ),
		new( 1f, 0f ),
		new( 1f, 1f ),
		new( 0f, 1f )
	};

	/// <summary>
	/// Shuffles <paramref name="corners"/> - which starts as 0,1,2,3 - to turn this tile's art by the
	/// quarter turns the cell asks for. The stored angle is 0, 90, 180 or 270 on every one of a park's
	/// 16,384 cells and never anything else, so this is a ring shift and not a rotation matrix.
	///
	/// <para>
	/// <b>Which way the ring runs is checkable from the art itself, and this is the reading it supports.</b>
	/// A cell whose neighbour mask is E and W must draw a walkway running east to west, and one whose
	/// mask is N and S must draw it north to south; the jungle has both, twenty-eight and sixteen of
	/// them, so a turn the wrong way shows immediately as paths crossing their own junctions. That is the
	/// check to repeat if these ever look wrong, rather than reasoning about it again.
	/// </para>
	/// </summary>
	private static void TurnCorners( int angle, int[] corners )
	{
		corners[0] = 0;
		corners[1] = 1;
		corners[2] = 2;
		corners[3] = 3;

		// Negative or oversized angles cannot occur in any shipped park, but a park that is edited later
		// should not be able to index this out of its own array.
		var quarters = ((angle / 90) % 4 + 4) % 4;

		for ( var turn = 0; turn < quarters; ++turn )
			(corners[0], corners[1], corners[2], corners[3])
				= (corners[3], corners[0], corners[1], corners[2]);
	}
}
