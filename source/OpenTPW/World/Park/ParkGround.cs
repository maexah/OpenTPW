namespace OpenTPW;

/// <summary>
/// The ground a park is built on: the 96x85 cell heightfield out of <c>base.MD2</c>, turned into a
/// surface. <see cref="ParkTerrain"/> is the scenery standing on top of this.
///
/// <para>
/// <b>The colours are not the park's textures.</b> Each cell carries a texture index, and nothing yet
/// traces how that index reaches a named texture - it is the largest single gap left in the park work,
/// and it wants its own pass over the original rather than a guess. So the ground is drawn a flat
/// colour per distinct index, which is honestly a diagram rather than a picture, and is deliberately
/// the step before texturing: it shows the shape of the land and where its surfaces change without
/// pretending to know what they are made of.
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

	private readonly string _themeName;

	public ParkGround( string themeName )
	{
		_themeName = themeName;
		Name = $"{themeName} ground";

		Build();

		Current = this;
	}

	/// <summary>
	/// Lets go of <see cref="Current"/>, but only if it is still this one - a scene that builds its
	/// replacement before tearing down its predecessor would otherwise have the old one clear the new.
	/// </summary>
	protected override void OnDelete()
	{
		if ( Current == this )
			Current = null;
	}

	/// <summary>
	/// Placeholder colours, one per distinct cell texture index, in ascending index order. Enough of
	/// them for every park the game ships - the jungle uses seven - and chosen only to be told apart
	/// from one another, not to resemble anything. The material carries sixteen texture slots, which
	/// is the ceiling here.
	///
	/// <para>
	/// What the jungle's 8,160 cells actually hold, which is worth knowing before reading a picture of
	/// them: index 27 on 5,260 cells, 57 on 1,376, 0 on 1,159, and 56/58/59/60 sharing the last 365.
	/// So the ground is mostly two indices alternating - 40% of neighbouring cells differ, which is
	/// why it reads as a check rather than as a field - and index 0 is not ground at all but the holes
	/// in it: its 1,159 cells are exactly the 1,159 whose flag word is 0x1, and they trace out the
	/// river and the paths.
	/// </para>
	/// </summary>
	private static readonly byte[][] Palette =
	[
		[ 108, 148,  74, 255 ],   // index 0 - the holes: river, paths, whatever covers the ground
		[ 150, 120,  80, 255 ],
		[  96, 132, 168, 255 ],
		[ 176, 152,  96, 255 ],
		[ 128, 104, 136, 255 ],
		[  84, 156, 140, 255 ],
		[ 168, 112,  92, 255 ],
		[ 120, 128, 148, 255 ],
		[ 144, 168, 104, 255 ],
		[  92, 116,  96, 255 ],
		[ 160, 136, 160, 255 ],
		[ 104, 144, 124, 255 ],
		[ 148,  96, 108, 255 ],
		[ 112, 160, 168, 255 ],
		[ 132, 132,  84, 255 ],
		[ 100, 100, 112, 255 ],
	];

	private void Build()
	{
		var path = $"levels/{_themeName.ToLowerInvariant()}/terrain/base.MD2";

		using ( var stream = FileSystem.OpenRead( path ) )
			Heightfield = new HeightfieldFile( stream );

		var field = Heightfield;

		Log.Info( $"{_themeName}: landscape {field.CellsX}x{field.CellsY} cells at " +
			$"{field.CellSizeX}x{field.CellSizeY} units, {field.VertexCount} heights, block at 0x{field.BlockOffset:x}" );

		// Which texture indices the park actually uses, in ascending order, each becoming one slot of
		// the material. More than sixteen would need the ground splitting into several models; no
		// shipped park comes close, and this says so rather than drawing the excess wrong.
		var indices = new List<ushort>();

		for ( var i = 0; i < field.Cells.Length; ++i )
		{
			var texture = (ushort)(field.Cells[i] >> 16);

			if ( !indices.Contains( texture ) )
				indices.Add( texture );
		}

		indices.Sort();

		if ( indices.Count > Palette.Length )
			Log.Warning( $"{_themeName}: {indices.Count} ground texture indices, only {Palette.Length} colours - the rest share the last one" );

		var textures = new Texture[16];

		for ( var slot = 0; slot < textures.Length; ++slot )
			textures[slot] = new Texture( Palette[Math.Min( slot, Palette.Length - 1 )], 1, 1 );

		// Four corners a cell, not a shared grid. A shared vertex could only carry one texture index
		// and one pair of texture coordinates, and a cell needs its own of both - now for the colour,
		// and later for the rotation and mirror bits its flag word carries.
		var vertices = new Vertex[field.CellCount * 4];
		var elements = new uint[field.CellCount * 6];

		var vertex = 0;
		var element = 0;

		for ( var y = 0; y < field.CellsY; ++y )
		{
			for ( var x = 0; x < field.CellsX; ++x )
			{
				var slot = indices.IndexOf( (ushort)(field.Cells[(y * field.CellsX) + x] >> 16) );
				slot = Math.Clamp( slot, 0, textures.Length - 1 );

				var corner = vertex;

				vertices[vertex++] = Corner( field, x, y, 0f, 0f, slot );
				vertices[vertex++] = Corner( field, x + 1, y, 1f, 0f, slot );
				vertices[vertex++] = Corner( field, x + 1, y + 1, 1f, 1f, slot );
				vertices[vertex++] = Corner( field, x, y + 1, 0f, 1f, slot );

				// Two triangles over those four corners. Which diagonal to split on is a bit of the
				// cell's flag word in the original; nothing in the shipped parks sets the bit that
				// turns that choice on, so both go the same way here and this is where to change it
				// when a park turns up that does.
				elements[element++] = (uint)corner;
				elements[element++] = (uint)(corner + 1);
				elements[element++] = (uint)(corner + 2);

				elements[element++] = (uint)corner;
				elements[element++] = (uint)(corner + 2);
				elements[element++] = (uint)(corner + 3);
			}
		}

		// The same shader and the same culling rule the rest of the world uses - nothing in this game
		// is one-sided, so the ground is not either.
		var material = new Material<ObjectUniformBuffer>( "content/shaders/test.shader", MaterialFlags.DisableCulling );
		material.Set( "Color", textures );

		Model = new Model( vertices, elements, material );
	}

	/// <summary>
	/// One corner of one cell. The grid lies flat in X and Y with height in Z, because this engine has
	/// Z up where the original has Y up - the same swap <see cref="LobbyModel"/> makes when it reads a
	/// model, which is what puts the ground under the scenery rather than beside it.
	/// </summary>
	private static Vertex Corner( HeightfieldFile field, int x, int y, float u, float v, int slot )
		=> new()
		{
			Position = new Vector3( x * field.CellSizeX, y * field.CellSizeY, field.HeightAt( x, y ) ),
			Normal = NormalAt( field, x, y ),
			TexCoords = new Vector2( u, v ),
			TexIndex = slot,
			MatFlags = 0
		};

	/// <summary>
	/// The surface normal at a grid vertex, from how the land falls away either side of it. Central
	/// differences rather than a face normal, so that a corner shared by four cells is lit as one
	/// surface and the ground does not facet along its own grid.
	///
	/// <para>
	/// <b>Returned with Y and Z swapped, on purpose.</b> Every other normal in the game arrives from a
	/// .md2, where Y is up, and content/shaders/test.shader knows that: it swaps them back itself,
	/// with <c>vec3(normal.x, normal.z, normal.y)</c>, because the positions beside them have already
	/// been swapped by <see cref="LobbyModel"/>. A normal worked out here is in the engine's own Z-up
	/// space and would be turned on its side by that same line - a flat (0,0,1) becoming (0,1,0), so
	/// level ground lights as though it were a wall, which is exactly how it looked.
	/// </para>
	/// </summary>
	private static Vector3 NormalAt( HeightfieldFile field, int x, int y )
	{
		var slopeX = (field.HeightAt( x + 1, y ) - field.HeightAt( x - 1, y )) / (2f * field.CellSizeX);
		var slopeY = (field.HeightAt( x, y + 1 ) - field.HeightAt( x, y - 1 )) / (2f * field.CellSizeY);

		// Engine space would be (-slopeX, -slopeY, 1); this is that with Y and Z exchanged.
		return new Vector3( -slopeX, 1f, -slopeY ).Normal;
	}
}
