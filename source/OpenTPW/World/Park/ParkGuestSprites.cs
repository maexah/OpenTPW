using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace OpenTPW;

/// <summary>
/// The park's people, drawn as the flat pictures they are: the guests on the bus road, the handyman,
/// the mechanic, the entertainer, the guard and the researcher.
///
/// <para>
/// <b>They are sprites, not models.</b> There is no peep <c>.md2</c> anywhere in the game's data. A
/// person is a picture out of <c>esprites.wad</c>, turned to face the camera, and which picture is a
/// question the save has already answered: the park ships with its people in place, each one already
/// wearing a bank and a set that were rolled at random when that person was made and then written
/// down. So this reads them back and must never roll again - re-rolling would change every guest's
/// clothes each time the park was loaded.
/// </para>
/// <para>
/// <b>Where each one stands</b> comes from the thing that owns the sprite rather than from the sprite:
/// a person's <c>mX</c> and <c>mY</c>, in 256ths of a cell. The sprite record does carry a world
/// position of its own, and it agrees with the thing's to within a third of a unit on all eighteen of
/// the shipped park's people, but it is the runtime's copy - the thing is where the park says they are.
/// The ground underneath is asked for separately, because the stored height is an offset above it.
/// </para>
/// <para>
/// <b>Which way each one faces</b> is the part that has to be redone every frame. The direction is not
/// the person's heading alone but their heading plus the camera's, folded into eight
/// (<see cref="Facing"/>), so turning the camera turns every sprite in the park. A set stores fewer
/// pictures than that - five for a body - and the rest are the same pictures reflected, which is why
/// the far half of the compass is drawn mirrored.
/// </para>
/// <para>
/// <b>Engine and content.</b> Drawing world sprites is engine, and the same path will carry litter,
/// balloons and thought bubbles when those arrive. Which sprites exist, and where, is the park's.
/// </para>
/// </summary>
public sealed class ParkGuestSprites : ModelEntity
{
	/// <summary>
	/// Every picture stores 128 in the two fields the engine divides its size by, so a picture's world
	/// size is its pixels over 128, times the span below. The span is read from the draw path
	/// (<c>FUN_00589410</c>) and is <b>provisional</b>: that function carries a second factor which is
	/// filled in at device setup and reads zero in the static image, so it could not be pinned. What
	/// says this number is about right is the art itself - a guest comes out around six units against a
	/// ten-unit cell, which is the proportion the shipped park's own meshes keep.
	/// </summary>
	private const float PictureReference = 128f;

	private const float WorldPerReference = 20f;

	/// <summary>How many ways round the compass a heading is folded into before a set is asked for.</summary>
	private const int Compass = 8;

	private const int AtlasWidth = 1024;

	private const int Padding = 4;

	[StructLayout( LayoutKind.Sequential )]
	private struct ObjectUniformBuffer
	{
		public Matrix4x4 g_mModel;
		public Matrix4x4 g_mView;
		public Matrix4x4 g_mProj;
	}

	/// <summary>Where a picture sits in the atlas, and the size and origin it was authored with.</summary>
	private readonly record struct Region(
		float Left, float Top, float Right, float Bottom, int Width, int Height, int OriginX, int OriginY );

	private sealed record Loaded( SpriteBankFile Bank, Region[] Pictures );

	private readonly List<ParkWorld.Sprite> _sprites = [];
	private readonly Dictionary<(int Type, int Bank), Loaded> _banks = [];

	private Texture? _atlas;
	private Vertex[] _vertices = [];
	private int _uploaded;

	/// <summary>
	/// Which folder of <c>esprites.wad</c> holds a kind of sprite. The executable builds this table by
	/// scanning folders as it starts, so it is empty in the file itself and these names are the folders
	/// that exist, in the order the type numbers run.
	///
	/// <para>
	/// The three per-theme kinds are <b>provisional</b>: costumes, costume heads and entertainers exist
	/// once per theme, and whether a park numbers only its own theme's or all four together was not
	/// settled. Only its own is the reading taken here, because the alternative puts a Fantasy
	/// entertainer in a Jungle park.
	/// </para>
	/// </summary>
	private static string? FolderFor( int type, string theme ) => type switch
	{
		0 => "esprites/Generic/Kids",
		1 => "esprites/Generic/Kidsheads",
		2 => $"esprites/{theme}/Costumes",
		3 => $"esprites/{theme}/Costumeheads",
		4 => $"esprites/{theme}/Entertainers",
		5 => "esprites/Generic/Handymen",
		6 => "esprites/Generic/Mechanics",
		7 => "esprites/Generic/Guards",
		8 => "esprites/Generic/Researchers",
		_ => null
	};

	public ParkGuestSprites( string themeName, ParkWorld? park )
	{
		if ( park == null )
			return;

		// Only the people. The table can hold litter and balloons and thought bubbles too, and those
		// have no thing of their own to take a position from yet.
		var byPerson = park.People.ToDictionary( person => person.SpriteSlot, person => person );

		foreach ( var sprite in park.Sprites )
		{
			if ( byPerson.ContainsKey( sprite.Slot ) )
				_sprites.Add( sprite );
		}

		// Only the banks those sprites actually wear. Loading every person bank in the archive would be
		// 5,316 pictures and an atlas 13,885 pixels tall, past what a good many devices will allocate at
		// all; the shipped park wears nine banks and comes to well under two thousand.
		Load( themeName, park );

		if ( _sprites.Count > 0 && _atlas != null )
			Build();
	}

	/// <summary>
	/// Reads each bank a sprite in this park wears, and packs their pictures into one texture. Banks are
	/// numbered within their kind by the order the folder's files come, which is the order the archive
	/// itself lists them.
	/// </summary>
	private void Load( string themeName, ParkWorld park )
	{
		var pictures = new List<SpritePicture>();
		var placed = new List<(int Type, int Bank, int First, int Count, SpriteBankFile File)>();

		foreach ( var key in _sprites.Select( sprite => (sprite.Type, Bank: sprite.Bank + sprite.BankOffset) ).Distinct() )
		{
			var folder = FolderFor( key.Type, themeName );

			if ( folder == null )
				continue;

			var files = FileSystem.GetFiles( folder )
				.Where( file => file.EndsWith( ".esp", StringComparison.OrdinalIgnoreCase ) )
				.ToArray();

			if ( key.Bank < 0 || key.Bank >= files.Length )
			{
				Log.Warning( $"Guests: sprite kind {key.Type} has no bank {key.Bank} in {folder}" );
				continue;
			}

			try
			{
				var bank = new SpriteBankFile( files[key.Bank] );
				var pack = new SpritePackFile( Path.ChangeExtension( files[key.Bank], ".TPC" ) );

				placed.Add( (key.Type, key.Bank, pictures.Count, pack.Pictures.Length, bank) );
				pictures.AddRange( pack.Pictures );
			}
			catch ( Exception e )
			{
				Log.Warning( $"Guests: sprite bank '{files[key.Bank]}' would not load - {e.Message}" );
			}
		}

		if ( pictures.Count == 0 )
			return;

		_atlas = BuildAtlas( pictures, out var regions );

		foreach ( var (type, bank, first, count, file) in placed )
			_banks[(type, bank)] = new Loaded( file, regions[first..(first + count)] );
	}

	/// <summary>
	/// A quad per sprite, built once and rewritten each frame - the arrangement
	/// <see cref="WeatherSprites"/> uses, and for the same reason: there is no instancing here, so a
	/// crowd has to be one mesh.
	/// </summary>
	private void Build()
	{
		_vertices = new Vertex[_sprites.Count * 4];

		var indices = new uint[_sprites.Count * 6];

		for ( uint i = 0; i < _sprites.Count; ++i )
		{
			var at = i * 6;
			indices[at] = i * 4;
			indices[at + 1] = (i * 4) + 1;
			indices[at + 2] = (i * 4) + 2;
			indices[at + 3] = i * 4;
			indices[at + 4] = (i * 4) + 2;
			indices[at + 5] = (i * 4) + 3;
		}

		// Depth-tested so a guest behind a hill is behind it, but not depth-writing: these are blended
		// and drawn in whatever order the table happens to be in, so letting them occlude one another
		// would show as sprite-shaped holes where two overlap. Two-sided because a quad turned to face
		// the camera can end up wound either way.
		var material = new Material( "content/shaders/guests.shader",
			MaterialFlags.DisableDepthWrite | MaterialFlags.DisableCulling );

		material.Set( "Color", _atlas! );

		TranslucentModel = new Model( _vertices, indices, material );
		TranslucentModel.EnableFrequentUpdates( _vertices );
	}

	/// <summary>
	/// Which of the eight ways round a sprite is seen from, which is its own heading turned by the
	/// camera's. The camera term is its yaw rounded to the nearest eighth, taken from the direction it
	/// looks rather than from any one camera mode, so the park's orbit and camcorder cameras both work.
	///
	/// <para>
	/// <b>Where this could be wrong:</b> the original's zero heading and ours need not point the same
	/// way. If they do not, every sprite in the park is turned by the same number of eighths - which
	/// looks like guests facing consistently the wrong way rather than like nonsense, and is the one
	/// thing here that only looking at the screen can settle.
	/// </para>
	/// </summary>
	private static int Facing( int personFacing )
	{
		var forward = Camera.Rotation.Forward;
		var yaw = MathF.Atan2( -forward.X, forward.Y );

		var octant = (int)MathF.Floor( ((yaw - (MathF.PI / Compass)) / MathF.Tau * Compass) + 0.5f );

		return (Compass - (octant & (Compass - 1)) + personFacing) & (Compass - 1);
	}

	/// <summary>
	/// The picture for a set at a heading, and whether it has to be drawn reflected.
	///
	/// <para>
	/// A set stores <see cref="SpriteSet.Directions"/> views and the engine covers the rest of the
	/// compass by reflecting: a heading past the last stored one becomes <c>8 - heading</c> and is drawn
	/// mirrored. One shipped set - a four-direction entertainer - folds to a heading that is still out
	/// of range, and the original walks off into the next set's pictures. This clamps instead, which is
	/// a deliberate departure: a wrong frame of the right person beats a picture of somebody else.
	/// </para>
	/// </summary>
	private static int Picture( SpriteSet set, int frame, int heading, out bool mirrored )
	{
		mirrored = false;

		if ( set.Directions <= 0 )
			return set.First + frame;

		var direction = heading;

		if ( direction > set.Directions - 1 )
		{
			direction = Compass - direction;
			mirrored = true;
		}

		direction = Math.Clamp( direction, 0, set.Directions - 1 );

		return set.First + frame + (set.FramesPerDirection * direction);
	}

	protected override void OnRenderTranslucent()
	{
		if ( TranslucentModel == null || _atlas == null )
			return;

		var field = ParkGround.Current?.Heightfield;
		var used = 0;

		foreach ( var sprite in _sprites )
		{
			if ( !_banks.TryGetValue( (sprite.Type, sprite.Bank + sprite.BankOffset), out var loaded ) )
				continue;

			var set = loaded.Bank.Sets[sprite.Set & 0xf];
			var index = Picture( set, sprite.Frame, Facing( sprite.Facing ), out var mirrored );

			if ( index < 0 || index >= loaded.Pictures.Length )
				continue;

			var picture = loaded.Pictures[index];

			// The stored height is an offset above the ground rather than a height, so the land under
			// the sprite is what decides where its feet go.
			var ground = field?.HeightAtWorld( sprite.X, sprite.Y ) ?? 0f;
			var centre = new Vector3( sprite.X, sprite.Y, ground + sprite.Height );

			WriteQuad( used++, centre, picture, mirrored, sprite.Alpha );
		}

		for ( int i = used; i < _uploaded; ++i )
			Collapse( i );

		var reach = Math.Max( used, _uploaded );
		_uploaded = used;

		if ( reach == 0 )
			return;

		TranslucentModel.UpdateVertices( _vertices, reach * 4 );

		if ( used == 0 )
			return;

		// World space already, so the model matrix is identity - the quads were turned to face the
		// camera as they were written.
		TranslucentModel.Material.Set( "g_oUbo", new ObjectUniformBuffer
		{
			g_mModel = Matrix4x4.Identity,
			g_mView = Camera.ViewMatrix,
			g_mProj = Camera.ProjMatrix
		} );

		TranslucentModel.Draw();
	}

	/// <summary>
	/// One sprite's quad, stood upright at <paramref name="centre"/> and turned to face the camera.
	///
	/// <para>
	/// <b>Square-on to the camera, not merely upright.</b> A quad spanned by world up and a horizontal
	/// across - which is how rain is built - faces the camera only in plan: tilt the camera down and it
	/// foreshortens, until a guest is squashed to a fraction of their height and the sprite shows its
	/// edge. A park camera looks down between 45 and 65 degrees, so that costs between a third and more
	/// than half of every person's height. The original keeps its people turned to face the camera, so
	/// the basis has to lean with the camera's pitch rather than stand in world up.
	/// </para>
	/// <para>
	/// The basis comes from the view direction and world up rather than from <c>Rotation.Right</c> and
	/// <c>Rotation.Up</c>, because <see cref="Rotation.LookAt"/> leaves roll arbitrary and those two
	/// carry it - the same trap <see cref="AudioListener"/> and <see cref="Audio"/> both record against
	/// their own use of it. <c>Forward</c> is the one of the three that can be trusted, and a roll-free
	/// basis follows from it and world up.
	/// </para>
	/// <para>
	/// The picture's own origin decides where the quad sits about that point: it is stored negated in
	/// the file and read back positive, measured from the picture's top left, so the sprite reaches
	/// <c>origin</c> above the point and the rest of its height below. Every person's origin sits 85% or
	/// more down their picture, which is to say on their feet.
	/// </para>
	/// </summary>
	private void WriteQuad( int index, Vector3 centre, Region picture, bool mirrored, int alpha )
	{
		if ( index < 0 || index * 4 >= _vertices.Length )
			return;

		var forward = Camera.Rotation.Forward;
		var across = forward.Cross( Vector3.Up );

		// Looking straight down there is no horizontal left to hang the sprite on, and every quad
		// would be edge-on anyway.
		if ( across.LengthSquared < 0.000001f )
		{
			Collapse( index );
			return;
		}

		across = across.Normal;

		// Square to both the view and that across, which is the camera's own up with no roll in it.
		var upward = across.Cross( forward ).Normal;

		var scale = WorldPerReference / PictureReference;

		var left = across * (-picture.OriginX * scale);
		var right = across * ((picture.Width - picture.OriginX) * scale);
		var top = upward * (picture.OriginY * scale);
		var bottom = upward * (-(picture.Height - picture.OriginY) * scale);

		// White at the sprite's own alpha: the shader multiplies the picture by this, so anything else
		// would tint the art.
		var colour = ((uint)Math.Clamp( alpha, 0, 255 ) << 24) | 0x00ffffffu;

		var u0 = mirrored ? picture.Right : picture.Left;
		var u1 = mirrored ? picture.Left : picture.Right;

		var v = index * 4;

		Corner( v, centre + left + top, u0, picture.Top );
		Corner( v + 1, centre + right + top, u1, picture.Top );
		Corner( v + 2, centre + right + bottom, u1, picture.Bottom );
		Corner( v + 3, centre + left + bottom, u0, picture.Bottom );

		void Corner( int vertex, Vector3 position, float u, float w )
			=> _vertices[vertex] = new Vertex( position, new Vector2( u, w ) ) { MatFlags = colour };
	}

	/// <summary>Shrinks a quad to a point, which rasterises to nothing.</summary>
	private void Collapse( int index )
	{
		var v = index * 4;

		for ( int i = 0; i < 4; ++i )
			_vertices[v + i] = new Vertex();
	}

	/// <summary>Every picture into one texture, with clear pixels round each so the smaller mip levels do not bleed.</summary>
	private static Texture BuildAtlas( List<SpritePicture> pictures, out Region[] regions )
	{
		var places = new (int X, int Y)[pictures.Count];
		int x = Padding, y = Padding, rowHeight = 0;

		for ( int i = 0; i < pictures.Count; ++i )
		{
			if ( x + pictures[i].Width + Padding > AtlasWidth )
			{
				x = Padding;
				y += rowHeight + Padding;
				rowHeight = 0;
			}

			places[i] = (x, y);
			x += pictures[i].Width + Padding;
			rowHeight = Math.Max( rowHeight, pictures[i].Height );
		}

		var height = y + rowHeight + Padding;
		var pixels = new byte[AtlasWidth * height * 4];
		regions = new Region[pictures.Count];

		for ( int i = 0; i < pictures.Count; ++i )
		{
			var picture = pictures[i];
			var (left, top) = places[i];

			for ( int row = 0; row < picture.Height; ++row )
				Array.Copy( picture.Rgba, row * picture.Width * 4, pixels,
					((top + row) * AtlasWidth + left) * 4, picture.Width * 4 );

			regions[i] = new Region(
				left / (float)AtlasWidth, top / (float)height,
				(left + picture.Width) / (float)AtlasWidth, (top + picture.Height) / (float)height,
				picture.Width, picture.Height, picture.OriginX, picture.OriginY );
		}

		return new Texture( pixels, AtlasWidth, height );
	}

	/// <summary>
	/// Lets the model go through <see cref="ModelEntity.OnDelete"/>, and the atlas with it. The atlas is
	/// built from pictures rather than loaded by a path, so it is in no cache and nothing else can be
	/// holding it - the same reasoning <see cref="UI.ScreenParticles"/> records.
	/// </summary>
	protected override void OnDelete()
	{
		base.OnDelete();

		_atlas?.Delete();
		_atlas = null;
	}
}
