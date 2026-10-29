namespace OpenTPW;

/// <summary>
/// The ground a park is built on: the 96x85 cell heightfield out of <c>base.MD2</c>, turned into a
/// surface. <see cref="ParkTerrain"/> is the scenery standing on top of this.
///
/// <para>
/// Each cell carries a texture index, and that index counts through the model's own frame table - so
/// the ground is drawn with the park's real textures, its own ground base set. Every theme uses six of
/// them: <c>jgr_bas1..6</c> for the jungle and fantasy, <c>hrk_bas1..6</c> for hallow,
/// <c>sfl_bas1..6</c> for space. Index 0 is the exception and is not a ground texture at all - it
/// covers the cells the river and the park's fixed approach run over, which the scenery draws. The
/// player's own paths are <b>not</b> among them: inside the procedural ground's box each is laid over its cell's tile
/// here, as the original's compositor lays it, and elsewhere <see cref="ParkPaths"/> draws it.
/// </para>
///
/// <para>
/// A cell is not simply mapped corner to corner: its flag word says which way round the art goes, and
/// <see cref="PermuteCorners"/> applies that as the original does, to the cell's own texture and to its composited
/// tile alike. Inside the procedural ground's box, at a detail that asks for it, every cell draws its tile
/// (<see cref="ProceduralGround"/>) in place of its own texture.
/// </para>
/// </summary>
public sealed class ParkGround : ModelEntity
{
	/// <summary>
	/// The ground of the park currently loaded, or null outside one - the same way the lobby's own
	/// systems publish themselves. It is how anything that needs to know where the land is finds it
	/// without being handed a reference: the camera rides on it.
	/// </summary>
	public static ParkGround? Current { get; private set; }

	/// <summary>The landscape this was built from - kept so that whatever needs a height can ask for one.</summary>
	public HeightfieldFile Heightfield { get; private set; } = null!;

	/// <summary>
	/// What each cell of this park <i>is</i> - the entrance, the bus road, buildable ground - or null if
	/// the map would not load. Note it is indexed the other way round from the heightfield beside it:
	/// attributes are <c>x * 128 + y</c> where heights are <c>y * width + x</c>. That is the game's own
	/// inconsistency, not a mistake here.
	/// </summary>
	public AttributeMapFile? Attributes { get; private set; }

	private readonly string _themeName;

	/// <summary>The park this was built from, so that it can be built again when a cell changes.</summary>
	private IParkInitialState? _world;

	/// <summary>
	/// <c>MapInfo.HeightfieldWidth</c> and <c>HeightfieldHeight</c> (95 and 84 in every theme), the limits
	/// <see cref="LandOutsideThePark"/> holds a ground cell's map cell below.
	/// </summary>
	private readonly int _mapWidth;

	private readonly int _mapHeight;

	/// <summary><c>MapInfo.HeightfieldWidth</c>, or nought without the balance: <see cref="ParkPicking"/> holds its column below it.</summary>
	public int MapInfoWidth => _mapWidth;

	/// <summary><c>MapInfo.HeightfieldHeight</c>, or nought without the balance: <see cref="ParkPicking"/> holds its row below it.</summary>
	public int MapInfoHeight => _mapHeight;

	/// <summary><c>GraphicalOptions.LIGHTING</c> from the detail file: the half light is drawn only while it is on.</summary>
	private readonly bool _lighting;

	/// <summary>
	/// The ground cells drawn at half light, by <c>y * CellsX + x</c>: the ground cell flag <c>0x0400</c>. Made
	/// once, as the park loads, and kept through every <see cref="Rebuild"/>, as the original sets the flag only
	/// on a load and after Buy Land (unbuilt, <c>BUY_LAND_TOOL</c>; built, its apply makes this again).
	/// </summary>
	private bool[]? _halfLight;

	/// <summary>The ground cells drawn at half light, by <c>y * CellsX + x</c>, or null where none is: the terrain model's meshes read it (<see cref="TerrainHalfLight"/>).</summary>
	internal bool[]? HalfLitMask => _halfLight;

	/// <summary>
	/// The procedural ground's tiles, every cell of the box in one texture, or null where the detail file turns the
	/// compositor off or the park has no <c>base.lnd</c>: every cell then draws its own texture.
	/// </summary>
	private GroundTiles? _tiles;

	private readonly ParkBalance? _balance;

	private readonly int _tileSize;

	/// <summary>The theme's texture table, read once, for the path tiles laid over the ground's tiles.</summary>
	private TextureTableFile? _pathTable;

	private bool _pathTableRead;

	/// <summary>Each path tile's texture as the compositor reads it, by tile index, null where it would not read.</summary>
	private readonly Dictionary<int, ProceduralGround.PathTexture?> _pathTextures = new();

	/// <summary>Each built thing's floor texture as the compositor reads it, by path, null where it would not read.</summary>
	private readonly Dictionary<string, ProceduralGround.PathTexture?> _floorTextures = new( StringComparer.OrdinalIgnoreCase );

	/// <param name="world">
	/// The park's own save, or null where the theme ships none. It is asked which cells something else draws - a
	/// path, a queue or a built thing's floor - so that those are left to their owners instead of being drawn as grass
	/// underneath them, and, on a path cell the compositor's tiles cover, which tile and which turn the path lays over
	/// the ground's own (<see cref="LaysPath"/>).
	/// </param>
	/// <param name="balance">The theme's balance, asked for <c>MapInfo</c>'s limits; the field's own less one without it.</param>
	/// <param name="lighting"><c>GraphicalOptions.LIGHTING</c>: off, no cell is drawn at half light.</param>
	/// <param name="tileSize">
	/// The procedural ground's tile size, or 0 where <c>GraphicalOptions.PROCEDURALTEXTURING</c> is 0 and every cell
	/// draws its own texture.
	/// </param>
	public ParkGround( string themeName, IParkInitialState? world, ParkBalance? balance = null, bool lighting = true,
		int tileSize = 0 )
	{
		_themeName = themeName;
		_world = world;
		_lighting = lighting;
		_mapWidth = balance?.Int( "MapInfo.HeightfieldWidth" ) ?? 0;
		_mapHeight = balance?.Int( "MapInfo.HeightfieldHeight" ) ?? 0;
		Name = $"{themeName} ground";

		_balance = balance;
		_tileSize = tileSize;

		Build( world );

		Current = this;
	}

	/// <summary>
	/// Builds the surface again, because a cell has changed underneath it - something built, something
	/// sold.
	///
	/// <para>
	/// <b>The whole surface, not the cell.</b> The ground is one model of every drawn cell in the park,
	/// so there is no per-cell edit to make: the arrays are sized, filled and handed over as one. Eight
	/// thousand cells is cheap enough to do on the rare frame a player builds something, and far
	/// cheaper than keeping a second structure that can disagree with this one.
	/// </para>
	/// <para>
	/// <b>The old model is let go of first.</b> <see cref="ModelEntity"/> owns its model and the
	/// material bound into it; building over the top without deleting would leave both for the life of
	/// the process.
	/// </para>
	/// </summary>
	public void Rebuild()
	{
		Model?.Delete();
		Model = null!;

		Build( _world );

		Log.Info( $"{_themeName}: the ground was rebuilt - {ParkState.Current?.ChangedCells ?? 0} cells have changed" );
	}

	/// <summary>
	/// Lets go of <see cref="Current"/>, but only if it is still this one - a scene that builds its
	/// replacement before tearing down its predecessor would otherwise have the old one clear the new.
	/// </summary>
	protected override void OnDelete()
	{
		// ModelEntity.OnDelete is what lets go of the model and, with it, the material - and an
		// override that does not chain to it keeps both for the life of the process. The ground is a model
		// and a material a park builds from scratch every time it is loaded.
		base.OnDelete();

		// The tiles' texture is the ground's own; the material does not let go of its textures.
		_tiles?.Texture.Delete();

		if ( Current == this )
			Current = null;
	}

	/// <summary>
	/// The texture index that means "this cell is not ground".
	///
	/// <para>
	/// It resolves to whatever texture happens to sit first in the model's table - <c>grd_ctr1</c> in
	/// the jungle, <c>jho_fnt1</c> in the other three - which is the first sign that it is a null slot
	/// rather than a choice. Drawing it settles the question: <c>grd_ctr1</c> is the road centre, black
	/// with yellow markings, and it paved the river bed and the approach with tarmac.
	/// </para>
	///
	/// <para>
	/// These are exactly the cells whose flag word is 0x1 - 1,159 of each in the jungle, the same cells
	/// both ways - and they trace the river and the park's fixed approach. Which is the point: <b>the
	/// park's own scenery already draws those surfaces.</b> base.MD2 carries the river with its water
	/// and its stone banks, the waterfall under the bridge, and the entrance road. A ground quad over a
	/// cell marked 0 is not filling a gap, it is putting a lid on what is underneath - which is why
	/// skipping them does not leave holes but uncovers the park.
	/// </para>
	///
	/// <para>
	/// <b>The player's own paths are NOT among these.</b>
	/// Measured against the save: all 82 of Lost Kingdom's path and queue cells carry a real ground
	/// index - mostly 27, <c>jgr_bas1</c> - and not one is 0, while every one of the 66 cells the save
	/// marks as the fixed approach is 0. So a built path is ordinary drawn ground here, and which surface
	/// draws its cell is decided by asking the save rather than the model - see <see cref="LaysPath"/> and
	/// <see cref="ParkPaths"/>.
	/// </para>
	/// </summary>
	private const ushort NotGround = 0;

	/// <summary>
	/// What the model calls the texture at an index, or empty if it names none there.
	/// </summary>
	private static string TextureName( HeightfieldFile field, int index )
		=> index >= 0 && index < field.TextureNames.Length ? field.TextureNames[index] : string.Empty;

	/// <summary>
	/// The texture a cell index draws with. The model names them <c>.tga</c> and the files beside it
	/// are the same stem as <c>.wct</c>, which is the one translation this needs. A name that will not
	/// load leaves that slot blank rather than taking the ground down with it - a park missing one
	/// ground texture is still worth looking at.
	/// </summary>
	private Texture Resolve( HeightfieldFile field, int index, string directory )
	{
		var name = TextureName( field, index );

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
			Log.Warning( $"{_themeName}: ground texture {index} ('{name}') would not load - {e.Message}" );
			return Texture.Missing;
		}
	}

	private void Build( IParkInitialState? world )
	{
		var path = $"levels/{_themeName.ToLowerInvariant()}/terrain/base.MD2";

		using ( var stream = FileSystem.OpenRead( path ) )
			Heightfield = new HeightfieldFile( stream );

		var field = Heightfield;

		// Made once, then laid again where the path over a tile has changed since (FUN_0056f090 under 0x0100).
		if ( _tileSize > 0 && _tiles == null )
		{
			_tiles = GroundTiles.Make( _themeName, _balance, field, _tileSize, ( x, y ) => PathOver( world, x, y ) );
		}
		else if ( _tiles != null )
		{
			var laid = _tiles.Remake( ( x, y ) => PathOver( world, x, y ), out var replaced );

			// The old model and its material went first (Rebuild), so nothing draws with the old texture now.
			replaced?.Delete();

			if ( laid > 0 )
				Log.Info( $"{_themeName}: {laid} ground tiles laid again, their path changed" );
		}

		if ( _halfLight == null && world != null && _lighting )
		{
			var width = _mapWidth > 0 ? _mapWidth : field.CellsX - 1;
			var height = _mapHeight > 0 ? _mapHeight : field.CellsY - 1;

			_halfLight = HalfLitCells( _lighting, field.CellsX, field.CellsY, width, height,
				( x, y ) => ParkState.CellFor( world, x, y ).Flags )!;

			Log.Info( $"{_themeName}: {_halfLight.Count( half => half )} of the {field.CellsX}x{field.CellsY} ground cells " +
				$"are land outside the park, drawn at half light (map cells held below {width}x{height})" );
		}

		Log.Info( $"{_themeName}: landscape {field.CellsX}x{field.CellsY} cells at " +
			$"{field.CellSizeX}x{field.CellSizeY} units, {field.VertexCount} heights, block at 0x{field.BlockOffset:x}" );

		// The attribute map beside it - what each cell IS, rather than how high it is or what it looks
		// like. A park that cannot read it is still worth looking at, so this reports and carries on
		// rather than taking the ground down with it.
		try
		{
			using ( var stream = FileSystem.OpenRead( $"levels/{_themeName.ToLowerInvariant()}/terrain/base.map" ) )
				Attributes = new AttributeMapFile( stream );

			Log.Info( $"{_themeName}: attributes {Attributes.Width}x{Attributes.Height}" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"{_themeName}: base.map would not load, so nothing can ask what a cell is - {e.Message}" );
		}

		// Which texture indices the park actually uses, in ascending order, each becoming one slot of
		// the material. More than sixteen would need the ground splitting into several models; no
		// shipped park comes close, and this says so rather than drawing the excess wrong.
		// Index 0 is left out, and the cells carrying it are not drawn at all - see NotGround.
		var indices = new List<ushort>();

		for ( var i = 0; i < field.Cells.Length; ++i )
		{
			var texture = (ushort)(field.Cells[i] >> 16);

			if ( texture != NotGround && !indices.Contains( texture ) )
				indices.Add( texture );
		}

		indices.Sort();

		// Material.TextureSlots is what the material holds and what the shader switches over. Every park
		// the game ships uses six, so this has room to spare; a park that wanted more would need the
		// ground splitting across several models, and this says so rather than drawing the excess wrong.
		var textures = new Texture[Material.TextureSlots];
		var directory = $"levels/{_themeName.ToLowerInvariant()}/terrain/textures";

		if ( indices.Count > textures.Length )
			Log.Warning( $"{_themeName}: the ground uses {indices.Count} textures where a material holds {textures.Length}" );

		for ( var slot = 0; slot < textures.Length; ++slot )
			textures[slot] = slot < indices.Count
				? Resolve( field, indices[slot], directory )
				: Texture.Missing;

		var tileSlot = Math.Min( indices.Count, textures.Length - 1 );

		if ( _tiles != null )
			textures[tileSlot] = _tiles.Texture;

		Log.Info( $"{_themeName}: ground textures - " +
			string.Join( ", ", indices.Select( index => $"{index}:{TextureName( field, index )}" ) ) );

		// Four corners a cell, not a shared grid. A shared vertex could only carry one texture index
		// and one pair of texture coordinates, and a cell needs its own of both - for the colour, and for
		// the rotation and mirror bits its flag word carries (PermuteCorners).
		var vertices = new Vertex[field.CellCount * 4];
		var elements = new uint[field.CellCount * 6];

		var vertex = 0;
		var element = 0;

		var halfLit = 0;
		var tiled = 0;

		for ( var y = 0; y < field.CellsY; ++y )
		{
			for ( var x = 0; x < field.CellsX; ++x )
			{
				var texture = (ushort)(field.Cells[(y * field.CellsX) + x] >> 16);

				// Not ground, so draw nothing: the park's own scenery already covers these cells, and a
				// ground quad here does not fill a gap, it puts a lid on the river.
				if ( texture == NotGround )
					continue;

				// A cell something else draws is not this surface's to draw. Three kinds of cell are
				// somebody else's: the ones the player laid a path on, which ParkPaths draws except where
				// the compositor's tiles cover them (LaysPath: there this draws the cell, the path laid over
				// its tile); the ones something is built on, where the item's own model carries a floor plate
				// as wide as its footprint; and the ones a queue runs over, where each piece of queue brings
				// its own base. See ParkObjects.CoversGround and ParkQueues.IsQueue.
				//
				// All three are ordinary ground cells in the model rather than index 0, so without this
				// two surfaces are built over each other at identical heights and fight for the same
				// depth, and the grass wins.
				if ( world != null )
				{
					// The RUNNING park's answer, not the file's - a cell built on, or laid with path,
					// since the park loaded has to stop being drawn as grass, and ParkWorld cannot
					// record that. ParkState.CellFor is the one statement of that rule, shared with the
					// paths, the queues, the edge test and the queue walk, so that none of them can
					// disagree about who owns a cell.
					var cell = ParkState.CellFor( world, x, y );

					// Inside the box the path is laid over the cell's tile, which is drawn here; outside it ParkPaths
					// draws the path alone, as the terrain pass draws the cell's own texture there.
					if ( LeavesToOthers( cell, LaysPath( x, y ) ) )
						continue;
				}

				var slot = Math.Clamp( indices.IndexOf( texture ), 0, textures.Length - 1 );

				// Which way round this cell's art goes - the low half of the same word the texture
				// index came from, as a path writes it on a path cell. A composited tile is turned the same way (0x0056fa0b).
				var uvs = CellUvs( DrawnWordAt( world, field, x, y, _tileSize > 0 ) );

				// Inside the box the cell draws its composited tile in place of its own texture (FUN_0056f380).
				var tile = _tiles?.Covers( x, y ) == true;

				if ( tile )
				{
					slot = tileSlot;
					++tiled;
				}

				var corner = vertex;
				var flags = CornerFlags( _halfLight, (y * field.CellsX) + x );

				if ( flags != 0 )
					++halfLit;

				vertices[vertex++] = Corner( field, x, y, Uv( x, y, uvs[0], tile ), slot, flags );
				vertices[vertex++] = Corner( field, x + 1, y, Uv( x, y, uvs[1], tile ), slot, flags );
				vertices[vertex++] = Corner( field, x + 1, y + 1, Uv( x, y, uvs[2], tile ), slot, flags );
				vertices[vertex++] = Corner( field, x, y + 1, Uv( x, y, uvs[3], tile ), slot, flags );

				// Two triangles over those four corners, always split from (x, y) to (x + 1, y + 1) -
				// which is not what the original does, and this is where to fix it (Q398). The original
				// cuts each cell on the diagonal its flag 0x0002 names, the other one where the cell
				// carries 0x0004 instead (docs/exe/park-engine.md, "The triangle diagonal").
				elements[element++] = (uint)corner;
				elements[element++] = (uint)(corner + 1);
				elements[element++] = (uint)(corner + 2);

				elements[element++] = (uint)corner;
				elements[element++] = (uint)(corner + 2);
				elements[element++] = (uint)(corner + 3);
			}
		}

		// The shader the models use, both faces drawn, where the original culls: its terrain renderer (0x0056f670) carries
		// its own copy of the face test (render-states.md, "Which draws the test covers"; Q613 builds it).
		var material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader", MaterialFlags.DisableCulling );
		material.Set( "Color", textures );

		// Sliced to what was actually filled. The arrays are sized for every cell because the count is
		// not known until the skipped ones have been counted, and handing the whole of them over would
		// ship several thousand zeroed vertices and a few thousand indices all pointing at vertex 0 -
		// degenerate triangles that the card would probably discard, which is not a reason to send them.
		Model = new Model( vertices[..vertex], elements[..element], material );

		Log.Info( $"{_themeName}: ground {element / 6} cells drawn, {tiled} of them with a composited tile, {halfLit} at half light, " +
			$"{field.CellCount - (element / 6)} left to the scenery, to the paths and to what is built on them" );
	}

	/// <summary>
	/// Whether a path on cell (x, y) is laid over the cell's composited tile, which the ground then draws: the cell is in
	/// the compositor's box and its path tile's texture reads. Where that texture will not read, <see cref="ParkPaths"/>
	/// draws the cell with the not-found texture, as it does outside the box.
	/// </summary>
	public bool LaysPath( int x, int y )
	{
		if ( _tiles?.Covers( x, y ) != true )
			return false;

		if ( _world == null )
			return true;

		var cell = ParkState.CellFor( _world, x, y );
		return !ParkPaths.IsPath( cell ) || PathTexture( cell.TileIndex ) != null;
	}

	/// <summary>
	/// Whether this surface leaves a cell to another: a path cell the compositor's tiles do not carry
	/// (<see cref="ParkPaths.DrawsPathCell"/> draws it), a queue's or a built thing's.
	/// </summary>
	internal static bool LeavesToOthers( ParkWorld.MapCell cell, bool laysPath )
		=> ParkPaths.DrawsPathCell( cell, laysPath ) || ParkQueues.IsQueue( cell ) || ParkObjects.CoversGround( cell );

	/// <summary>The word cell (x, y) is drawn with: the running park's (<see cref="WordOf"/>) as <see cref="DrawnWord"/> turns it.</summary>
	internal static ushort DrawnWordAt( IParkInitialState? world, HeightfieldFile field, int x, int y, bool compositor )
		=> DrawnWord( WordOf( world, field, x, y ), compositor );

	/// <summary>A cell's word as the running park holds it: the file's, or on a path cell what the path wrote (<see cref="PathWord"/>).</summary>
	internal static ushort WordOf( IParkInitialState? world, HeightfieldFile field, int x, int y )
	{
		var word = (ushort)field.Cells[(y * field.CellsX) + x];

		if ( world == null )
			return word;

		var cell = ParkState.CellFor( world, x, y );
		return ParkPaths.IsPath( cell ) ? PathWord( word, cell.TileAngle ) : word;
	}

	/// <summary>
	/// The word <c>FUN_0046df50</c> writes on a cell a path tile is laid on: its mirror, turn and <c>0x0001</c> cleared,
	/// <c>0x0180</c> set (a path cell, its tile stale), then the angle's turn, 90 <c>0x20</c>, 180 <c>0x10</c>, 270
	/// <c>0x08</c>, as the running original holds all 78 of the stock park's.
	/// </summary>
	internal static ushort PathWord( ushort word, int angle )
		=> (ushort)((word & 0xff86) | 0x0180 | angle switch { 90 => 0x20, 180 => 0x10, 270 => 0x08, _ => 0 });

	/// <summary>The path laid over cell (x, y)'s tile: its tile's texture and the word, or null where it is no path cell.</summary>
	private ProceduralGround.Overlay? PathOver( IParkInitialState? world, int x, int y )
	{
		if ( world == null )
			return null;

		var cell = ParkState.CellFor( world, x, y );

		if ( !ParkPaths.IsPath( cell ) || PathTexture( cell.TileIndex ) is not { } texture )
			return null;

		return new ProceduralGround.Overlay( texture, WordOf( world, Heightfield, x, y ) );
	}

	/// <summary>A path tile's texture as the compositor reads it, read once.</summary>
	private ProceduralGround.PathTexture? PathTexture( int index )
	{
		if ( _pathTextures.TryGetValue( index, out var known ) )
			return known;

		if ( !_pathTableRead )
		{
			_pathTable = ParkPaths.ReadTable( _themeName );
			_pathTableRead = true;
		}

		ProceduralGround.PathTexture? texture = null;

		if ( ParkPaths.TileFile( _themeName, _pathTable, index ) is { } file )
		{
			try
			{
				using var stream = FileSystem.OpenReadOrNull( file );
				var data = new TextureFile( stream ).Data;
				texture = ProceduralGround.PathTexture.FromRgba( data.Data, data.Width, data.Height );
			}
			catch ( Exception e )
			{
				Log.Warning( $"{_themeName}: path tile {index} would not read, so nothing is laid over its tiles - {e.Message}" );
			}
		}

		_pathTextures[index] = texture;
		return texture;
	}

	/// <summary>
	/// <c>TileCache_CompositeCell</c> (<c>0x00572630</c>) for one cell of a built thing's floor (<c>ProceduralBase_BlendFloor</c>): cell (x, y)'s ground tile at the
	/// tiles' own size, read by <paramref name="word"/>'s turn and mirror, with the floor's texture at
	/// <paramref name="texturePath"/> laid over it by its alpha, as a path is (<see cref="ProceduralGround.Tile"/>); null where
	/// there are no tiles (the compositor off), the cell is outside their box or the texture will not read. The caller owns
	/// the texture. The original hands the heightfield's x and y as they are, where its tiles' own are box-relative: one in
	/// every shipped park, whose box starts at nought.
	/// </summary>
	public Texture? BlendFloor( int x, int y, ushort word, string texturePath )
	{
		if ( _tiles?.Covers( x, y ) != true || FloorTexture( texturePath ) is not { } floor )
			return null;

		var n = _tiles.TileSize;
		var tile = _tiles.Ground.Tile( x, y, new ProceduralGround.Overlay( floor, word ) );
		return new Texture( TextureRows( tile, n ), n, n, TextureFlags.Repeat );
	}

	/// <summary>
	/// A tile's n x n RGB pixels as the texture holds them: opaque RGBA, the tile's row k at the texture's row n - 1 - k. The
	/// tile's row k is the surface's; the texture holds its rows bottom up, as every texture here is read (the shader turns v
	/// over), so a floor's UVs find it as they found the floor's own.
	/// </summary>
	internal static byte[] TextureRows( byte[] tile, int n )
	{
		var pixels = new byte[n * n * 4];

		for ( var k = 0; k < n; ++k )
		{
			for ( var j = 0; j < n; ++j )
			{
				var from = ((k * n) + j) * 3;
				var to = ((((n - 1 - k) * n) + j) * 4);
				pixels[to] = tile[from];
				pixels[to + 1] = tile[from + 1];
				pixels[to + 2] = tile[from + 2];
				pixels[to + 3] = 0xff;
			}
		}

		return pixels;
	}

	/// <summary>A floor's texture as the compositor reads it (<see cref="ProceduralGround.PathTexture"/>), read once a path.</summary>
	private ProceduralGround.PathTexture? FloorTexture( string path )
	{
		if ( _floorTextures.TryGetValue( path, out var known ) )
			return known;

		ProceduralGround.PathTexture? texture = null;

		try
		{
			using var stream = FileSystem.OpenReadOrNull( path );
			var data = new TextureFile( stream ).Data;
			texture = ProceduralGround.PathTexture.FromRgba( data.Data, data.Width, data.Height );
		}
		catch ( Exception e )
		{
			Log.Warning( $"{_themeName}: floor texture '{path}' would not read, so its floor draws its own - {e.Message}" );
		}

		_floorTextures[path] = texture;
		return texture;
	}

	/// <summary>
	/// The vertex flag <c>content/shaders/test.shader</c> halves a park-lit corner's colour under (its
	/// <c>FLAG_HALF_LIGHT</c>). Not from any file, so it sits in the high bits beside the shader's other flags of ours.
	/// </summary>
	public const uint HalfLight = 0x40000;

	/// <summary>
	/// The map cell flag that keeps land outside the park at full light: <c>base.map</c>'s attribute <c>0x10</c>,
	/// which <c>FUN_00536490</c> carries into <c>mFlags</c> as the map loads (<c>0x0053656d</c>). In Lost Kingdom it is the 552 cells of
	/// the approach before the gate, rows 0 to 16, every one of them outside the park too.
	/// </summary>
	public const ushort KeptLit = 0x80;

	/// <summary>
	/// Which ground cells are drawn at half light: <c>Terrain_MarkLandOutsidePark</c> (<c>0x00450d10</c>), run as a park loads (<c>FUN_005323f0</c>,
	/// <c>FUN_00415140</c>) and after Buy Land (<c>FUN_005327e0</c>). Each of the field's cells asks its map cell, held
	/// to 0 to <paramref name="mapWidth"/> - 1 and 0 to <paramref name="mapHeight"/> - 1 (<c>0x00450d3d</c> to
	/// <c>0x00450d64</c>), so the field's last column and row ask the map's last; the cell is half lit where that one's
	/// <c>mFlags</c> carries <see cref="ParkPathBuilding.OutsideThePark"/> and not <see cref="KeptLit"/>
	/// (docs/exe/park-engine.md, "The park view's colours").
	/// </summary>
	internal static bool[] LandOutsideThePark( int cellsX, int cellsY, int mapWidth, int mapHeight, Func<int, int, ushort> mapFlags )
	{
		var half = new bool[cellsX * cellsY];

		for ( var y = 0; y < cellsY; ++y )
		{
			for ( var x = 0; x < cellsX; ++x )
			{
				var flags = mapFlags( Math.Clamp( x, 0, mapWidth - 1 ), Math.Clamp( y, 0, mapHeight - 1 ) );
				half[(y * cellsX) + x] = (flags & ParkPathBuilding.OutsideThePark) != 0 && (flags & KeptLit) == 0;
			}
		}

		return half;
	}

	/// <summary>
	/// The cells drawn at half light, or null where <c>GraphicalOptions.LIGHTING</c> is off and none is
	/// (<see cref="LandOutsideThePark"/> otherwise).
	/// </summary>
	internal static bool[]? HalfLitCells( bool lighting, int cellsX, int cellsY, int mapWidth, int mapHeight, Func<int, int, ushort> mapFlags )
		=> lighting ? LandOutsideThePark( cellsX, cellsY, mapWidth, mapHeight, mapFlags ) : null;

	/// <summary>The vertex flags a ground cell's four corners carry: <see cref="HalfLight"/> where the cell is half lit.</summary>
	internal static uint CornerFlags( bool[]? halfLight, int cell ) => halfLight?[cell] == true ? HalfLight : 0u;

	/// <summary>
	/// One corner of one cell. The grid lies flat in X and Y with height in Z, because this engine has
	/// Z up where the original has Y up - the same swap <see cref="LobbyModel"/> makes when it reads a
	/// model, which is what puts the ground under the scenery rather than beside it.
	/// </summary>
	private static Vertex Corner( HeightfieldFile field, int x, int y, Vector2 uv, int slot, uint flags )
		=> new()
		{
			Position = new Vector3( x * field.CellSizeX, y * field.CellSizeY, field.HeightAt( x, y ) ),
			Normal = NormalAt( field, x, y ),
			TexCoords = uv,
			TexIndex = slot,
			MatFlags = flags
		};

	/// <summary>
	/// The texture coordinate of each corner of the original's ring (<c>ESP+0x4c</c> to <c>0x68</c> in <c>FUN_0056f670</c>),
	/// which runs (x, y), (x, y + 1), (x + 1, y + 1), (x + 1, y): u along x and v along y on a cell nothing turns.
	/// </summary>
	private static readonly Vector2[] RingUvs =
	{
		new( 0f, 0f ),
		new( 0f, 1f ),
		new( 1f, 1f ),
		new( 1f, 0f )
	};

	/// <summary>
	/// A corner's texture coordinate: on the cell's own texture as it is, and on its tile from texel centre to texel
	/// centre (<see cref="GroundTiles.Uv"/>).
	/// </summary>
	private Vector2 Uv( int x, int y, Vector2 uv, bool tile ) => tile ? _tiles!.Uv( x, y, uv ) : uv;

	/// <summary>
	/// A cell's mirror bit, and its rotation - which is one field of three bits, not three flags.
	/// Named for the cell rather than plainly because a <see cref="ModelEntity"/> carries a Rotation
	/// of its own, and this is emphatically not it: it turns the art on one cell, not the object.
	/// </summary>
	private const ushort CellMirror = 0x40;

	private const ushort CellRotation = 0x38;

	/// <summary>
	/// Shuffles <paramref name="corners"/> - which starts as 0,1,2,3 - into the ring corners this cell's art is laid by:
	/// <c>FUN_0056f4f0</c>, handed <c>0x03020100</c> (<c>0x0056fa0b</c>). Grid corner k of the ring takes the texture
	/// coordinate of ring corner <c>corners[k]</c> (<see cref="RingUvs"/>), on the cell's own texture and on its tile
	/// alike. The mirror is applied first and is diagonal (ring corners 1 and 3 change places); then the first of
	/// <c>0x08</c>, <c>0x10</c>, <c>0x20</c> set turns it a quarter, a half or three quarters
	/// (docs/exe/park-engine.md, "The procedural ground").
	/// </summary>
	internal static void PermuteCorners( ushort flags, int[] corners )
	{
		corners[0] = 0;
		corners[1] = 1;
		corners[2] = 2;
		corners[3] = 3;

		if ( (flags & (CellMirror | CellRotation)) == 0 )
			return;

		if ( (flags & CellMirror) != 0 )
			(corners[1], corners[3]) = (corners[3], corners[1]);

		if ( (flags & 0x08) != 0 )
			(corners[0], corners[1], corners[2], corners[3]) = (corners[3], corners[0], corners[1], corners[2]);
		else if ( (flags & 0x10) != 0 )
			(corners[0], corners[1], corners[2], corners[3]) = (corners[2], corners[3], corners[0], corners[1]);
		else if ( (flags & 0x20) != 0 )
			(corners[0], corners[1], corners[2], corners[3]) = (corners[1], corners[2], corners[3], corners[0]);
	}

	/// <summary>
	/// A cell's word as the terrain pass reads it: with the compositor on, every cell with neither <c>0x80</c> nor
	/// <c>0x01</c> loses its mirror and turn (<c>FUN_0046ded0(1)</c>, run by the park's load at <c>0x00450813</c> and by
	/// <c>FUN_00457a00</c>), so its tile is never turned; with it off the file's word stands.
	/// </summary>
	internal static ushort DrawnWord( ushort word, bool compositor )
		=> compositor && (word & 0x81) == 0 ? (ushort)(word & 0xff87) : word;

	/// <summary>The texture coordinates of grid corners (x, y), (x + 1, y), (x + 1, y + 1), (x, y + 1) for a cell's word.</summary>
	internal static Vector2[] CellUvs( ushort flags )
	{
		var corners = new int[4];
		PermuteCorners( flags, corners );
		return [RingUvs[corners[0]], RingUvs[corners[3]], RingUvs[corners[2]], RingUvs[corners[1]]];
	}

	/// <summary>
	/// The normal the original lights a grid vertex with (<c>FUN_0056ef10</c>; docs/exe/park-engine.md, "The
	/// lighting model"): the height either side of it, nearer minus further, times one over the field's
	/// height range, with 1.0 up - and <b>not normalised</b>. The range is wider than two cells, so a
	/// slope lights flatter than it stands (jungle: one seventieth against a true one twentieth), and a
	/// vertex at the grid's edge takes the one-sided difference.
	///
	/// <para>
	/// The last row takes its other height 128 floats back rather than one row back: the original
	/// steps by the map's 128 there where every other row steps by the field's own width.
	/// </para>
	///
	/// <para>
	/// <b>Returned with Y and Z swapped, on purpose.</b> Every other normal in the game arrives from a
	/// .md2, where Y is up, and content/shaders/test.shader swaps them back itself, so this is given in
	/// that same Y-up form.
	/// </para>
	/// </summary>
	internal static Vector3 NormalAt( HeightfieldFile field, int x, int y )
	{
		var span = field.HeightHigh - field.HeightLow;
		var k = span == 0f ? 1f / 16f : 1f / span;

		var stride = field.CellsX + 1;
		var here = (y * stride) + x;

		var acrossX = field.HeightAt( x - 1, y ) - field.HeightAt( x + 1, y );
		var acrossY = y < field.CellsY
			? field.HeightAt( x, y - 1 ) - field.HeightAt( x, y + 1 )
			: field.Heights[Math.Max( here - 128, 0 )] - field.Heights[here];

		return new Vector3( acrossX * k, 1f, acrossY * k );
	}
}
