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
/// player's own paths are <b>not</b> among them and are drawn by <see cref="ParkPaths"/>.
/// </para>
///
/// <para>
/// A cell is not simply mapped corner to corner: its flag word says which way round the art goes, and
/// <see cref="PermuteCorners"/> applies that. The original does it in a handful of lines - it starts
/// with the four corners in order and shuffles them - so this does too, and the comment there carries
/// the shuffle exactly as the original writes it.
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

	/// <param name="world">
	/// The park's own save, or null where the theme ships none. It is asked one question only: which
	/// cells the player laid a path on, so that those can be left to <see cref="ParkPaths"/> instead of
	/// being drawn as grass underneath it.
	/// </param>
	public ParkGround( string themeName, ParkWorld? world )
	{
		_themeName = themeName;
		Name = $"{themeName} ground";

		Build( world );

		Current = this;
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
	/// with yellow markings, and it paved the river bed and every path with tarmac.
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
	/// <b>The player's own paths are NOT among these, though an earlier note here said they were.</b>
	/// Measured against the save: all 82 of Lost Kingdom's path and queue cells carry a real ground
	/// index - mostly 27, <c>jgr_bas1</c> - and not one is 0, while every one of the 66 cells the save
	/// marks as the fixed approach is 0. So a built path is ordinary drawn ground here, and the cells it
	/// covers are left out separately, by asking the save rather than the model - see
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

	private void Build( ParkWorld? world )
	{
		var path = $"levels/{_themeName.ToLowerInvariant()}/terrain/base.MD2";

		using ( var stream = FileSystem.OpenRead( path ) )
			Heightfield = new HeightfieldFile( stream );

		var field = Heightfield;

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

		// Sixteen is what the material holds and what the shader switches over. Every park the game
		// ships uses seven, so this has room to spare; a park that wanted more would need the ground
		// splitting across several models, and this says so rather than drawing the excess wrong.
		var textures = new Texture[16];
		var directory = $"levels/{_themeName.ToLowerInvariant()}/terrain/textures";

		if ( indices.Count > textures.Length )
			Log.Warning( $"{_themeName}: the ground uses {indices.Count} textures where a material holds {textures.Length}" );

		for ( var slot = 0; slot < textures.Length; ++slot )
			textures[slot] = slot < indices.Count
				? Resolve( field, indices[slot], directory )
				: Texture.Missing;

		Log.Info( $"{_themeName}: ground textures - " +
			string.Join( ", ", indices.Select( index => $"{index}:{TextureName( field, index )}" ) ) );

		// Four corners a cell, not a shared grid. A shared vertex could only carry one texture index
		// and one pair of texture coordinates, and a cell needs its own of both - now for the colour,
		// and later for the rotation and mirror bits its flag word carries.
		var vertices = new Vertex[field.CellCount * 4];
		var elements = new uint[field.CellCount * 6];

		var vertex = 0;
		var element = 0;

		// Which corner's texture coordinate each corner of the quad takes, rewritten per cell by
		// PermuteCorners. Held out here and reused rather than allocated eight thousand times.
		var corners = new int[4];

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
				// somebody else's: the ones the player laid a path on, which ParkPaths draws; the ones
				// something is built on, where the item's own model carries a floor plate as wide as its
				// footprint; and the ones a queue runs over, where each piece of queue brings its own
				// base. See ParkObjects.CoversGround and ParkQueues.IsQueue.
				//
				// All three are ordinary ground cells in the model rather than index 0, so without this
				// two surfaces are built over each other at identical heights and fight for the same
				// depth. The grass won, which is what left a shop standing on bare grass.
				if ( world != null )
				{
					var cell = world.CellAt( x, y );

					if ( ParkPaths.IsPath( cell ) || ParkQueues.IsQueue( cell ) || ParkObjects.CoversGround( cell ) )
						continue;
				}

				var slot = Math.Clamp( indices.IndexOf( texture ), 0, textures.Length - 1 );

				// Which way round this cell's art goes - the low half of the same word the texture
				// index came from.
				PermuteCorners( (ushort)field.Cells[(y * field.CellsX) + x], corners );

				var corner = vertex;

				vertices[vertex++] = Corner( field, x, y, CornerUvs[corners[0]], slot );
				vertices[vertex++] = Corner( field, x + 1, y, CornerUvs[corners[1]], slot );
				vertices[vertex++] = Corner( field, x + 1, y + 1, CornerUvs[corners[2]], slot );
				vertices[vertex++] = Corner( field, x, y + 1, CornerUvs[corners[3]], slot );

				// Two triangles over those four corners, always split the same way - which is not
				// what the original does, and this is where to fix it.
				//
				// The original chooses the diagonal from 0x0004, but only on cells whose flag word
				// has 0x0800. Reading that bit off the file finds it nowhere, which is what an
				// earlier note here concluded from - wrongly. It is never stored: the loader
				// computes it, building each cell's two triangle normals from its four corner
				// heights and setting 0x0800 where they diverge by more than about 0.81 degrees,
				// which is to say "this cell is not flat, so which way it is cut is visible". Doing
				// the same needs that pass over the heightfield first, so it is left for its own
				// change rather than smuggled into this one.
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

		// Sliced to what was actually filled. The arrays are sized for every cell because the count is
		// not known until the skipped ones have been counted, and handing the whole of them over would
		// ship several thousand zeroed vertices and a few thousand indices all pointing at vertex 0 -
		// degenerate triangles that the card would probably discard, which is not a reason to send them.
		Model = new Model( vertices[..vertex], elements[..element], material );

		Log.Info( $"{_themeName}: ground {element / 6} cells drawn, " +
			$"{field.CellCount - (element / 6)} left to the scenery, to the paths and to what is built on them" );
	}

	/// <summary>
	/// One corner of one cell. The grid lies flat in X and Y with height in Z, because this engine has
	/// Z up where the original has Y up - the same swap <see cref="LobbyModel"/> makes when it reads a
	/// model, which is what puts the ground under the scenery rather than beside it.
	/// </summary>
	private static Vertex Corner( HeightfieldFile field, int x, int y, Vector2 uv, int slot )
		=> new()
		{
			Position = new Vector3( x * field.CellSizeX, y * field.CellSizeY, field.HeightAt( x, y ) ),
			Normal = NormalAt( field, x, y ),
			TexCoords = uv,
			TexIndex = slot,
			MatFlags = 0
		};

	/// <summary>
	/// The texture coordinate belonging to each corner of a cell, in the order the four corners are
	/// emitted above: the origin corner, then across, then across and along, then along.
	/// </summary>
	private static readonly Vector2[] CornerUvs =
	{
		new( 0f, 0f ),
		new( 1f, 0f ),
		new( 1f, 1f ),
		new( 0f, 1f )
	};

	/// <summary>
	/// A cell's mirror bit, and its rotation - which is one field of three bits, not three flags.
	/// Named for the cell rather than plainly because a <see cref="ModelEntity"/> carries a Rotation
	/// of its own, and this is emphatically not it: it turns the art on one cell, not the object.
	/// </summary>
	private const ushort CellMirror = 0x40;

	private const ushort CellRotation = 0x38;

	/// <summary>
	/// Shuffles <paramref name="corners"/> - which starts as 0,1,2,3 - into the order this cell's art
	/// is laid in. Six thousand of the jungle's cells are mirrored and a hundred are turned, so a
	/// ground drawn without this is wrong nearly everywhere, if only subtly.
	///
	/// <para>
	/// This is <c>FUN_0056f4f0</c> written out. The original is handed the same 0,1,2,3 - the call site
	/// literally writes <c>0x03020100</c> onto the stack immediately before calling it - and moves the
	/// entries about, so what is being permuted is which corner supplies which texture coordinate
	/// rather than the coordinates themselves. Two details are easy to get backwards and both are the
	/// original's own order: <b>the mirror is applied first</b>, and it is a <b>diagonal</b> reflection
	/// - it exchanges the two off-diagonal corners, not a flip in x or in y.
	/// </para>
	///
	/// <para>
	/// The three rotation bits are one field (<c>0x38</c>), so they are exclusive and this reads as an
	/// else-chain even though the original writes three separate ifs. They are one, two and three
	/// quarter turns, which is the same cycle <c>0 -> 0x08 -> 0x10 -> 0x20 -> 0</c> that the original
	/// steps through when it rotates a ride's footprint.
	/// </para>
	///
	/// <para>
	/// <b>The one thing not settled is which corner the original calls 0</b>, and which way its ring
	/// runs. The permutation itself is exact, but if its ring starts elsewhere or turns the other way,
	/// a quarter turn here would be three quarters there. It shows up on a hundred of the jungle's
	/// cells and on none of its mirrors, so the mirror - which is the overwhelming majority of the
	/// effect - is right either way.
	/// </para>
	/// </summary>
	private static void PermuteCorners( ushort flags, int[] corners )
	{
		corners[0] = 0;
		corners[1] = 1;
		corners[2] = 2;
		corners[3] = 3;

		if ( (flags & (CellMirror | CellRotation)) == 0 )
			return;

		if ( (flags & CellMirror) != 0 )
			(corners[1], corners[3]) = (corners[3], corners[1]);

		switch ( flags & CellRotation )
		{
			case 0x08:
				(corners[0], corners[1], corners[2], corners[3])
					= (corners[3], corners[0], corners[1], corners[2]);
				break;

			case 0x10:
				(corners[0], corners[2]) = (corners[2], corners[0]);
				(corners[1], corners[3]) = (corners[3], corners[1]);
				break;

			case 0x20:
				(corners[0], corners[1], corners[2], corners[3])
					= (corners[1], corners[2], corners[3], corners[0]);
				break;
		}
	}

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
	internal static Vector3 NormalAt( HeightfieldFile field, int x, int y )
	{
		var slopeX = (field.HeightAt( x + 1, y ) - field.HeightAt( x - 1, y )) / (2f * field.CellSizeX);
		var slopeY = (field.HeightAt( x, y + 1 ) - field.HeightAt( x, y - 1 )) / (2f * field.CellSizeY);

		// Engine space would be (-slopeX, -slopeY, 1); this is that with Y and Z exchanged.
		return new Vector3( -slopeX, 1f, -slopeY ).Normal;
	}
}
