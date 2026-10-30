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
/// <b>Engine and content.</b> Drawing world sprites is engine; it carries the people, their balloons and their
/// thought bubbles, and does not carry litter. Which sprites exist, and where, is the park's.
/// </para>
/// </summary>
public sealed class ParkGuestSprites : ModelEntity
{
	/// <summary>
	/// Every picture stores 128 in the two fields the engine divides its size by, so a picture's size is its
	/// pixels over 128, times the span below.
	///
	/// <para>
	/// <b>The span is measured rather than chosen.</b> The draw path <c>SpriteShadow_BuildQuad</c> scales a picture
	/// by its instance's own scale times <c>0x0070200c</c>, which holds <b>20.0</b> and sits in read-only
	/// <c>.rdata</c>. Every other term on the vertical chain is one: the factor at <c>0x00768ab4</c> is
	/// 1.0 and has no writer anywhere in the image, the call site <c>SpriteShadow_Draw</c> passes 1.0f for both
	/// of its multipliers, and all eighteen of the shipped park's sprites carry scale 1.0/1.0.
	/// </para>
	/// <para>
	/// <b>The span is the original's clip space, not its world.</b> It is drawn the span over
	/// <see cref="Camera.ClipPerWorld"/> in the world (<c>docs/exe/park-engine.md</c>, "The size a sprite is drawn",
	/// where the law is written once).
	/// </para>
	/// </summary>
	private const float PictureReference = 128f;

	/// <summary>The span, <c>0x0070200c</c>, in the original's clip space.</summary>
	private const float SpanPerReference = 20f;

	/// <summary>The span drawn in the world, square on to the eye of the park's lens.</summary>
	internal static readonly float WorldPerReference = SpanPerReference / Camera.ClipPerWorld( ParkOrbitCameraMode.HandedAngle );

	/// <summary>How many ways round the compass a heading is folded into before a set is asked for.</summary>
	private const int Compass = 8;

	/// <summary>How many bursting balloons the quads are sized for before <see cref="Build"/> is asked for more.</summary>
	private const int BurstingRoom = 16;

	private const int AtlasWidth = 1024;

	private const int Padding = 4;

	[StructLayout( LayoutKind.Sequential )]
	internal struct ObjectUniformBuffer
	{
		public Matrix4x4 g_mModel;
		public Matrix4x4 g_mView;
		public Matrix4x4 g_mProj;

		/// <summary>The theme's fog colour, and first person's band (<see cref="Level.LinearFog"/>).</summary>
		public System.Numerics.Vector4 g_vFogColour;
		public System.Numerics.Vector4 g_vLinearFog;
	}

	/// <summary>
	/// What the people's and the shadows' draws hand people.shader. World space already, so the model matrix is identity -
	/// the quads were turned to face the camera as they were written. Both are fogged in first person, as the original's
	/// sprite batch fogs them (SpriteBatch_AddQuad, and FUN_00589770's shadow through FUN_00565f90, under render-word
	/// 0x2000; docs/exe/park-engine.md, "First person's fog"). The orbit view's silhouette shadow is unfogged there.
	/// </summary>
	internal static ObjectUniformBuffer DrawUniforms() => new()
	{
		g_mModel = Matrix4x4.Identity,
		g_mView = Camera.ViewMatrix,
		g_mProj = Camera.ProjMatrix,
		g_vFogColour = new( Level.FogColour.X, Level.FogColour.Y, Level.FogColour.Z, 0f ),
		g_vLinearFog = Level.LinearFog
	};

	/// <summary>Where a picture sits in the atlas, and the size and origin it was authored with.</summary>
	internal readonly record struct Region(
		float Left, float Top, float Right, float Bottom, int Width, int Height, int OriginX, int OriginY );

	private sealed record Loaded( SpriteBankFile Bank, Region[] Pictures );

	private readonly List<(ParkWorld.Person Person, ParkWorld.Sprite Sprite)> _people = [];

	/// <summary>
	/// The save's people who have a sprite of their own, each with it. Only the people: the table's balloons are
	/// taken by their guests (ParkPeople, by mBalloonScript) and drawn by DrawBalloons, and a thought bubble is its
	/// person's (DrawBubble); litter has no thing of its own to take a position from yet.
	/// </summary>
	/// <remarks>
	/// Slot nought is nobody's: a person with no sprite holds it, a rider on a thing that keeps none and a member
	/// of staff resting, and a played park holds many.
	/// </remarks>
	internal static List<(ParkWorld.Person Person, ParkWorld.Sprite Sprite)> Taken( IParkInitialState park )
	{
		var byPerson = park.People.Where( person => person.SpriteSlot != 0 )
			.ToDictionary( person => person.SpriteSlot, person => person );

		var taken = new List<(ParkWorld.Person Person, ParkWorld.Sprite Sprite)>();

		foreach ( var sprite in park.Sprites )
		{
			if ( byPerson.TryGetValue( sprite.Slot, out var person ) )
				taken.Add( (person, sprite) );
		}

		return taken;
	}
	private readonly Dictionary<(int Type, int Bank), Loaded> _banks = [];

	/// <summary>How many banks of each guest kind the park draws over, or null - see the constructor.</summary>
	private readonly ParkSpriteBanks? _counts;
	private string? _themeName;
	private IParkInitialState? _park;

	private Texture? _atlas;
	private Region? _plain;
	private Vertex[] _vertices = [];

	/// <summary>Each quad's depth from the eye at the point it is drawn at, the key <see cref="FurthestFirst(int)"/> sorts by.</summary>
	private float[] _depths = [];

	/// <summary>The quads' corners and depths as written, for a test of the frame's end (<see cref="Finish"/>).</summary>
	internal (Vertex[] Vertices, float[] Depths) Written => (_vertices, _depths);
	private int _uploaded;

	/// <summary>The detail file's <c>SPRITESHADOW</c>, read as the park loads (<see cref="ParkPersonShadows.Tier"/>).</summary>
	private readonly int _shadowTier = 1;

	/// <summary>One shadow quad a person, rewritten each frame and drawn after the people (<see cref="ParkPersonShadows"/>).</summary>
	private Vertex[] _shadowVertices = [];

	/// <summary>The frame's shadows as <see cref="WriteTheFrame"/> left them, for a test.</summary>
	internal Vertex[] ShadowsWritten => _shadowVertices;

	private int _shadowsUploaded;
	private Model? _shadowModel;
	private Texture? _silhouette;

	/// <summary>Each shadow the last frame drew: whose, and its four corners in the world, for the console's <c>shadows</c>.</summary>
	internal IReadOnlyList<(int Thing, Vector3[] Corners)> ShadowsDrawn => _shadowsDrawn;

	private readonly List<(int Thing, Vector3[] Corners)> _shadowsDrawn = [];

	/// <summary>Whether the people's shadows are left undrawn, for the debug console's <c>shadows 0</c>.</summary>
	internal static bool DebugShadowsHidden { get; set; }

	/// <summary>
	/// The balloons' shared bob: its phase and the placements counted toward its next step - the globals
	/// <c>0x007cedd4</c> and <c>0x007cedd8</c> (<see cref="Balloon.AdvanceBob"/>). Static, as those are: a park
	/// loaded after another carries on from where the last left them.
	/// </summary>
	private static int _bobPhase;

	private static float _bobCount;

	/// <summary>
	/// The pool this park is drawing with: the debug console reads the census back through it, <see cref="ParkPeople"/>
	/// adds and removes people, and the camcorder swaps the pictures. Same arrangement as <see cref="ParkGround.Current"/>.
	/// </summary>
	internal static ParkGuestSprites? Current { get; private set; }

	/// <summary>
	/// Draws a dash on the ground under each person pointing the way they face, coloured by what kind
	/// of person they are. Off unless the debug console turns it on.
	///
	/// <para>
	/// It exists because a facing that is wrong by a constant looks like people standing oddly rather
	/// than like a fault, and no amount of looking at fifteen-pixel figures settles it. The dash lies
	/// flat in the ground plane on purpose - it is the one thing here that should NOT face the camera,
	/// because its whole job is to show a direction in the world.
	/// </para>
	/// </summary>
	internal static bool DebugFacing { get; set; }

	/// <summary>
	/// For the debug console only: everything here written and recorded for the pick as usual, and nothing drawn, so
	/// a paused frame taken with and without it is the people's own pixels.
	/// </summary>
	internal static bool DebugHidden { get; set; }

	/// <summary>
	/// Enough colours to tell the <b>nine</b> kinds of people apart at a glance - see <see cref="DebugFacing"/>.
	/// <c>FolderFor</c> covers them, 0 to 8, then the thought bubbles, 9, and the balloons, 10, neither of which gets a dash; an index past the end clamps.
	/// </summary>
	private static readonly uint[] DebugColours =
	[
		0xff4fc3f7, // 0 kids
		0xff4fc3f7, // 1 kidsheads
		0xffba68c8, // 2 costumes
		0xffba68c8, // 3 costumeheads
		0xffffd54f, // 4 entertainers
		0xff81c784, // 5 handymen
		0xffff8a65, // 6 mechanics
		0xffe57373, // 7 guards
		0xffffffff, // 8 researchers
	];

	/// <summary>
	/// Which folder of <c>esprites.wad</c> holds a kind of sprite. The executable builds this table by
	/// scanning folders as it starts, so it is empty in the file itself and these names are the folders
	/// that exist, in the order the type numbers run.
	///
	/// <para>
	/// <b>A park sees its own theme and no other, which is measured rather than assumed.</b> The
	/// original's table at <c>0x763f88</c> is fourteen bare kind names with no theme among them, and
	/// <c>Sprites_LoadFolder</c> sweeps each kind under two roots in turn - <c>generic\</c> and the
	/// current theme's - both feeding one numbering. The archive keeps the two apart: <c>Generic</c>
	/// holds eleven kinds, eight of them named below, and each theme holds only costumes, costume heads and
	/// entertainers. So exactly one of those two sweeps ever finds anything, and a Jungle park can no
	/// more number a Fantasy entertainer than it can a second Generic.
	/// </para>
	/// </summary>
	internal static string? FolderFor( int type, string theme ) => type switch
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
		Thoughts.SpriteKind => Thoughts.Folder,
		Balloon.SpriteKind => Balloon.Folder,
		_ => null
	};

	/// <summary>
	/// The four banks the original loads <i>before</i> it sweeps the folder, in this order, and only for
	/// kids and kid heads. <c>Sprites_LoadFolder</c> matches this table - it is at <c>0x764030</c> in the
	/// executable - against the folder's files first, loads what it finds, and only then sweeps up
	/// whatever is left, in name order.
	///
	/// <para>
	/// It matters because a bank is numbered by the order it was loaded in, and that number is what the
	/// save writes down. The archive lists <c>Generic\Kids</c> as BE, BI, CH, FR, KI, SA, SU, TA, so
	/// sweeping it plainly makes bank 0 the BE child; the original makes bank 0 the BI child and pushes BE
	/// out to 4. Every one of the shipped park's thirteen guests wears a kids bank, and between them they
	/// wear banks 0, 2, 4, 5, 6 and 7 - so sweeping plainly dresses the entire park in the wrong children
	/// while still looking perfectly plausible.
	/// </para>
	/// </summary>
	private static readonly string[] AvatarFirst = ["SPR_BI", "SPR_KI", "SPR_TA", "SPR_SU"];

	/// <summary>
	/// A kind's banks in the order the original numbers them, which is the order it loads them in. Takes
	/// the file system rather than reaching for the global one so that a test can ask the same question of
	/// a mount of its own - the rule the park tests already follow.
	/// </summary>
	internal static string[] BanksIn( BaseFileSystem data, string folder, int type )
	{
		// By name, ignoring case: the loader sorts the folder with _stricmp before it loads anything (FUN_00541210,
		// called at 0x005419a4). _stricmp folds to lower case, so '_' sorts before the letters.
		var files = data.GetFiles( folder )
			.Where( file => file.EndsWith( ".esp", StringComparison.OrdinalIgnoreCase ) )
			.OrderBy( file => Path.GetFileName( file ).ToLowerInvariant(), StringComparer.Ordinal )
			.ToArray();

		// Kinds 0 and 1 are kids and kid heads, whose four avatars come first; every other folder is numbered in
		// name order.
		if ( type is not (0 or 1) )
			return files;

		var first = AvatarFirst
			.Select( name => files.FirstOrDefault( file =>
				Path.GetFileNameWithoutExtension( file ).Equals( name, StringComparison.OrdinalIgnoreCase ) ) )
			.OfType<string>()
			.ToArray();

		return [.. first, .. files.Except( first )];
	}

	/// <param name="banks">
	/// How many banks of each guest kind the park draws over, or null to pack only what the save's people wear. With
	/// them every child and costume bank is packed, as an arrival or a costume may wear any, and a guest is drawn in what
	/// they wear now (<see cref="LookOf"/>).
	/// </param>
	public ParkGuestSprites( string themeName, IParkInitialState? park, ParkSpriteBanks? banks = null )
	{
		_counts = banks;

		if ( park == null )
			return;

		_people.AddRange( Taken( park ) );
		_shadowTier = ParkPersonShadows.Tier( OpenTPW.Level.DetailFile );
		Log.Info( _shadowTier switch
		{
			0 => $"Shadows: {OpenTPW.Level.DetailFile}'s SPRITESHADOW 0, so nobody casts a shadow",
			1 => $"Shadows: {OpenTPW.Level.DetailFile}'s SPRITESHADOW 1, so every person casts the {ParkPersonShadows.Silhouette} silhouette",
			_ => $"Shadows: {OpenTPW.Level.DetailFile}'s SPRITESHADOW {_shadowTier}, the person's own picture: drawn in first person, not built elsewhere (none drawn)"
		} );

		Current = this;
		_themeName = themeName;
		_park = park;

		// Pack the counted guest and staff banks before anyone arrives or is hired, plus worn banks and balloons.
		// Counts follow the theme and detail cap rather than packing the entire archive.
		Load( themeName, park );

		if ( _people.Count > 0 && _atlas != null )
			Build();
	}

	/// <summary>
	/// Reads each bank a sprite in this park wears, and packs their pictures into one texture. Banks are
	/// numbered within their kind in the order <see cref="BanksIn"/> gives, which is the original's.
	/// </summary>
	private void Load( string themeName, IParkInitialState park )
	{
		var pictures = new List<SpritePicture>();
		var placed = new List<(int Type, int Bank, int First, int Count, SpriteBankFile File)>();
		var fromFirstPerson = new List<string>();

		foreach ( var key in BanksToPack( _people.Select( p => p.Sprite ), _counts ) )
		{
			var folder = FolderFor( key.Type, themeName );

			if ( folder == null )
				continue;

			var files = BanksIn( FileSystem, folder, key.Type );

			if ( key.Bank < 0 || key.Bank >= files.Length )
			{
				Log.Warning( $"Guests: sprite kind {key.Type} has no bank {key.Bank} in {folder}" );
				continue;
			}

			try
			{
				var bank = new SpriteBankFile( files[key.Bank] );
				var pack = new SpritePackFile( PackFor( files[key.Bank], bank, FirstPerson ) );

				if ( FirstPerson && bank.UsesFirstPersonPictures )
					fromFirstPerson.Add( $"{key.Type}:{Path.GetFileNameWithoutExtension( files[key.Bank] )}" );

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

		// A small white square for the debug dash, the way ScreenParticles carries one for effects
		// with no frames of their own: it makes solid-colour geometry possible without a second
		// material or a shader that can do without a texture.
		pictures.Add( new SpritePicture( 4, 4, 2, 2, [.. Enumerable.Repeat( (byte)255, 4 * 4 * 4 )] ) );

		_atlas = BuildAtlas( pictures, out var regions );
		_plain = regions[^1];

		foreach ( var (type, bank, first, count, file) in placed )
			_banks[(type, bank)] = new Loaded( file, regions[first..(first + count)] );

		Log.Info( $"Guests: {placed.Count} sprite banks packed, {fromFirstPerson.Count} from .FPC, {pictures.Count - 1} pictures" +
			(fromFirstPerson.Count > 0 ? $" ({string.Join( ", ", fromFirstPerson )})" : "") );
	}

	/// <summary>
	/// The picture pack a bank is drawn from: its <c>.FPC</c> in first person when the bank says it has one, otherwise its
	/// <c>.TPC</c> (<c>SpriteBank_Load</c> at <c>0x00540d90</c>; <c>FUN_00542420</c> and <c>FUN_00542640</c> swap them).
	/// </summary>
	internal static string PackFor( string bankPath, SpriteBankFile bank, bool firstPerson )
		=> Path.ChangeExtension( bankPath, firstPerson && bank.UsesFirstPersonPictures ? ".FPC" : ".TPC" );

	/// <summary>Whether the packs in the atlas are first person's. A park loads with the top view's (state 9 calls
	/// <c>0x00540900( 0, 0 )</c>).</summary>
	internal bool FirstPerson { get; private set; }

	/// <summary>
	/// Draws each bank whose flag says so from its pictures for first person, or back from the top view's, as entering and leaving first
	/// person do (<c>0x0042af85</c>, <c>0x0042afba</c>; docs/exe/park-engine.md, "Entering and leaving first person").
	/// <b>A deviation in how, not in what:</b> the original reloads only the flagged banks into their own textures; here
	/// every picture shares one atlas, so the atlas is packed again whole and the old one let go.
	/// </summary>
	internal void UseFirstPersonPictures( bool firstPerson )
	{
		if ( FirstPerson == firstPerson || _themeName == null || _park == null )
			return;

		FirstPerson = firstPerson;

		var old = _atlas;
		_atlas = null;
		_banks.Clear();

		Load( _themeName, _park );

		// A model already built still samples the old atlas, so it is built again before that atlas goes.
		if ( _atlas != null && (TranslucentModel != null || _people.Count > 0) )
			Build();

		old?.Delete();
	}

	/// <summary>
	/// Which banks the atlas packs. With the counts: what the save's people wear, each brought within its kind's banks
	/// (<see cref="ParkSpriteBanks.Reduce"/>), every child and costume bank - an arrival or a costume may wear any - and
	/// all counted staff banks (including in an empty park), the balloon bank and the two thought banks. Without: what the save's people wear, the balloon bank and the two thought banks. Each once.
	/// </summary>
	internal static IEnumerable<(int Type, int Bank)> BanksToPack( IEnumerable<ParkWorld.Sprite> worn, ParkSpriteBanks? counts )
	{
		var banks = worn.Select( sprite => (sprite.Type, Bank: sprite.Bank + sprite.BankOffset) )
			.Select( key => (key.Type, Bank: counts?.Reduce( key.Type, key.Bank ) ?? key.Bank) )
			.Append( (Type: Balloon.SpriteKind, Bank: 0) )
			.Append( (Type: Thoughts.SpriteKind, Bank: 0) )
			.Append( (Type: Thoughts.SpriteKind, Bank: 1) );

		if ( counts != null )
		{
			banks = banks
				.Concat( Enumerable.Range( 0, counts.KidBanks ).Select( bank => (Type: ParkSpriteBanks.ChildKind, Bank: bank) ) )
				.Concat( Enumerable.Range( 0, counts.CostumeBanks ).Select( bank => (Type: ParkSpriteBanks.CostumeKind, Bank: bank) ) )
				.Concat( Enumerable.Range( 0, counts.KidBanks ).Select( bank => (Type: ParkSpriteBanks.KidHeadKind, Bank: bank) ) )
				.Concat( Enumerable.Range( 0, counts.CostumeBanks ).Select( bank => (Type: ParkSpriteBanks.CostumeHeadKind, Bank: bank) ) );
		}

		if ( counts?.StaffBanks is { } staff )
			banks = banks.Concat( staff.SelectMany( pair =>
				Enumerable.Range( 0, pair.Value ).Select( bank => (Type: pair.Key, Bank: bank) ) ) );

		return banks.Distinct();
	}

	/// <summary>
	/// Takes somebody who was not in the save - a guest who has just arrived - so that they are drawn
	/// alongside everyone else.
	///
	/// <para>
	/// <b>Their bank has to be one this park already packs.</b> The atlas is built once and nothing here adds to it
	/// afterwards - so a guest wearing an unpacked bank has no picture at all, which reads as a broken arrival rather
	/// than as a missing texture. With the park's counts every child bank is packed (<see cref="BanksToPack"/>), which
	/// is what an arrival draws from.
	/// </para>
	/// <para>
	/// <see cref="Build"/> is re-run because the vertex array is sized from the size of the crowd. Left
	/// alone it would be written past the end of on the next frame, which throws rather than dropping
	/// the newcomer silently - the better of the two failures, but not one to lean on.
	/// </para>
	/// </summary>
	internal void Add( ParkWorld.Person person, ParkWorld.Sprite sprite )
	{
		_people.Add( (person, sprite) );

		if ( _atlas != null )
			Build();
	}

	/// <summary>
	/// Stops drawing somebody who has gone home. Answers whether there was one to stop drawing.
	///
	/// <para>
	/// <see cref="Build"/> is re-run, because the vertex array is sized from the crowd and leaving it long
	/// would waste an upload's worth of it every frame. The model it makes starts with every quad a point,
	/// so the departed one's triangles go with the old model and nothing of the last frame is left to fold
	/// (<see cref="Resize"/>).
	/// </para>
	/// </summary>
	internal bool Remove( int thingId )
	{
		var at = _people.FindIndex( entry => entry.Person.ThingId == thingId );

		if ( at < 0 )
			return false;

		_people.RemoveAt( at );

		if ( _atlas != null )
			Build();

		return true;
	}

	/// <summary>
	/// The quads <see cref="Build"/> allots each person: the sprite, the debug dash, a balloon and a thought bubble.
	///
	/// <para>
	/// <see cref="Build"/> makes one mesh of a quad per sprite, sized to the crowd and rewritten each frame - the
	/// arrangement <see cref="WeatherSprites"/> uses, and for the same reason: there is no instancing here, so a
	/// crowd has to be one mesh. It is re-run whenever the crowd changes size, not only at load - see
	/// <see cref="Add"/> - and when more balloons are bursting than it has room for.
	/// </para>
	/// </summary>
	private const int QuadsAPerson = 4;

	private void Build( int bursting = BurstingRoom )
	{
		// Four quads a person - the sprite, the debug dash that is usually collapsed to nothing, a balloon and a
		// thought bubble - and room for the balloons bursting, which outlast their guests.
		var quads = (_people.Count * QuadsAPerson) + bursting;

		Resize( quads );

		var indices = new uint[quads * 6];

		for ( uint i = 0; i < quads; ++i )
		{
			var at = i * 6;
			indices[at] = i * 4;
			indices[at + 1] = (i * 4) + 1;
			indices[at + 2] = (i * 4) + 2;
			indices[at + 3] = i * 4;
			indices[at + 4] = (i * 4) + 2;
			indices[at + 5] = (i * 4) + 3;
		}

		// Depth tested AND written, as the original's people are: their sprites carry no 0x80000, so Sprite_BuildQuad ORs
		// no 0x800 (0x00589e85) into the word their graded pictures give (0x556: alpha test at 16, blended;
		// docs/exe/park-engine.md, "What hides a person below their feet"). people.shader drops what the test drops, and
		// the quads go to the buffer furthest first (FurthestFirst), so a nearer person's picture never cuts a hole in one
		// behind. The heads, bubbles and balloons drawn here share the word: their sprite flags are not read for 0x80000
		// either, nor their pictures' texture flags. Two-sided because a quad turned to face the camera can end up wound
		// either way.
		var material = new Material( PeopleShader, PeopleMaterial );

		material.Set( "Color", _atlas! );

		// The model this replaces is let go with its buffers and material; Delete defers the release past frames in flight.
		TranslucentModel?.Delete();
		TranslucentModel = new Model( _vertices, indices, material );
		TranslucentModel.EnableFrequentUpdates( _vertices );

		BuildShadows();
	}

	/// <summary>
	/// The shadows' mesh, a quad a person: in the orbit view at medium's tier the silhouette multiplied onto what lies
	/// under it, tested against depth and writing none, both faces drawn (<see cref="ParkPersonShadows"/>); in first person
	/// at any tier but 0 the atlas (the bodies' own pictures) alpha-blended, again writing no depth. The shader is the
	/// people's: the picture times the vertex colour, dropped under alpha 16, as the original's word <c>0x...13</c>
	/// keeps the silhouette's alpha test, and fogged by <c>vFog</c> in first person, as <c>FUN_00589770</c>'s is.
	/// <para>
	/// <b>A deviation in order:</b> the shadows are all drawn after all the bodies. The original's first-person walk adds
	/// each shadow straight after its own body (<c>0x0058a7f1</c>, <c>0x0058a800</c>), which under an alpha blend differs
	/// only where a shadow overlaps another person's graded edge; the walk's batch sort is unread (park-engine.md).
	/// </para>
	/// <para>
	/// <b>A deviation: under a tool's squares the shadow is hidden.</b> <see cref="ParkBuildMarkers"/> draws before the
	/// people and writes depth 1.5 over the land, so a shadow 0.6 over it fails the test there, where the original's shows
	/// through its squares (Q520).
	/// </para>
	/// </summary>
	private void BuildShadows()
	{
		_shadowModel?.Delete();
		_shadowModel = null;

		var kind = ParkPersonShadows.KindFor( _shadowTier, FirstPerson );

		if ( kind is not (ParkPersonShadows.Kind.Silhouette or ParkPersonShadows.Kind.FirstPersonOwnPicture) || _people.Count == 0 )
			return;

		var indices = new uint[_people.Count * 6];

		for ( uint i = 0; i < _people.Count; ++i )
		{
			var at = i * 6;
			(indices[at], indices[at + 1], indices[at + 2]) = (i * 4, (i * 4) + 1, (i * 4) + 2);
			(indices[at + 3], indices[at + 4], indices[at + 5]) = (i * 4, (i * 4) + 2, (i * 4) + 3);
		}

		var firstPerson = kind == ParkPersonShadows.Kind.FirstPersonOwnPicture;
		var material = new Material( PeopleShader, ShadowMaterialFor( kind ) );
		material.Set( "Color", firstPerson ? _atlas! : _silhouette ??= new Texture( ParkPersonShadows.Silhouette ) );

		_shadowModel = new Model( _shadowVertices, indices, material );
		_shadowModel.EnableFrequentUpdates( _shadowVertices );
	}

	/// <summary>The people's shader, whose alpha test is the original's (<c>content/shaders/people.shader</c>).</summary>
	internal const string PeopleShader = "content/shaders/people.shader";

	/// <summary>The people's pipeline: depth tested and written, both faces drawn (<see cref="Build"/>).</summary>
	internal const MaterialFlags PeopleMaterial = MaterialFlags.DisableCulling;

	/// <summary>The silhouette shadow's: the people's with no depth written (word <c>0x...13</c> | <c>0x800</c>, <c>0x00589a51</c>) and the multiply blend.</summary>
	internal const MaterialFlags SilhouetteShadowMaterial = PeopleMaterial | MaterialFlags.DisableDepthWrite | MaterialFlags.Multiply;

	/// <summary>The pipeline a <paramref name="kind"/> of shadow is drawn with.</summary>
	internal static MaterialFlags ShadowMaterialFor( ParkPersonShadows.Kind kind )
		=> kind == ParkPersonShadows.Kind.FirstPersonOwnPicture ? OwnPictureShadowMaterial : SilhouetteShadowMaterial;

	/// <summary>First person's own-picture shadow: the people's alpha blend with no depth written (word <c>0x556</c> | <c>0x800</c>, <c>0x00589845</c>), no multiply.</summary>
	internal const MaterialFlags OwnPictureShadowMaterial = PeopleMaterial | MaterialFlags.DisableDepthWrite;

	/// <summary>
	/// A fresh vertex array of that many quads, every one a point, and no quad counted as uploaded: the model
	/// <see cref="Build"/> makes over it starts from the array, so there is nothing of the last frame to fold, and
	/// a count kept from a longer array would fold past this one's end.
	/// </summary>
	internal void Resize( int quads )
	{
		_vertices = new Vertex[quads * 4];
		_depths = new float[quads];
		_uploaded = 0;
		_shadowVertices = new Vertex[_people.Count * 4];
		_shadowsUploaded = 0;
	}

	/// <summary>
	/// The frame's quads made ready for the upload: the <paramref name="used"/> written put furthest first
	/// (<see cref="FurthestFirst(int)"/>), then the last frame's leftovers folded (<see cref="Fold"/>), whose answer
	/// this is.
	/// </summary>
	internal int Finish( int used )
	{
		FurthestFirst( used );
		return Fold( used );
	}

	/// <summary>
	/// Folds to nothing every quad the last frame wrote and this one did not, and answers how many quads the upload
	/// has to reach: the larger of the two frames' counts. A count past the array's end is the array's, as the
	/// writers skip a quad there and still count it.
	/// </summary>
	internal int Fold( int used )
	{
		used = Math.Min( used, _vertices.Length / 4 );

		for ( int i = used; i < _uploaded; ++i )
			Collapse( i );

		var reach = Math.Max( used, _uploaded );
		_uploaded = used;

		return reach;
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
		=> Facing( personFacing, CameraOctant( Camera.Rotation.Forward ) );

	/// <summary>
	/// Which of the eight ways round the camera is looking, from the direction it looks rather than from
	/// any one camera mode, so the park's orbit and camcorder cameras both work.
	///
	/// <para>
	/// <b>This numbering runs the OPPOSITE way round from a person's.</b> A person's octant comes from
	/// <see cref="ParkWorld.Person.OctantOf"/> and increases clockwise - north 0, east 2, south 4, west 6.
	/// This one increases anticlockwise - north 0, west 2, south 4, east 6 - because it is taken from the
	/// orbit camera's yaw, which turns the eye the other way. Both are pinned by tests, because the two
	/// running in opposite directions is the whole reason the two must be ADDED rather than subtracted.
	/// </para>
	/// </summary>
	internal static int CameraOctant( Vector3 forward )
	{
		var yaw = MathF.Atan2( -forward.X, forward.Y );

		// <b>Rounded to the nearest eighth, with the half added AFTER the scaling</b>, which makes a boundary
		// the middle of a bucket rather than its edge. The rotate keys turn the orbit camera in steps of
		// exactly PI/4, so the yaws they reach sit exactly on those boundaries, where a plain floor would be
		// decided by the last bit of a float: looking due south, atan2 comes back as 3.99999989 eighths.
		return (int)MathF.Floor( (yaw / MathF.Tau * Compass) + 0.5f ) & (Compass - 1);
	}

	/// <summary>
	/// Which of the eight ways round a sprite is seen from: its own heading, turned by the camera's.
	///
	/// <para>
	/// <b>Added, not subtracted, and that is the whole of it.</b> The two numberings run in opposite
	/// directions - a person's clockwise, the camera's anticlockwise (see <see cref="CameraOctant"/>) - so
	/// adding them is what cancels the camera's rotation. Subtracting them would apply it twice: the drawn
	/// picture would come out wrong by exactly twice the camera's angle - right from due north and south,
	/// exactly backwards from east and west, and swinging round to face the viewer as the camera orbits.
	/// </para>
	/// <para>
	/// Split from the camera so it can be tested without one - the same reason <see cref="Standing"/> takes
	/// a walk rather than the pool. The test that settles it needs no park and no eyesight: turn the camera
	/// and the person together and the drawn picture must not change, because the view between them has not.
	/// Both the right arithmetic and the wrong one move the picture by one per eighth of camera turn, so
	/// only that invariant tells them apart.
	/// </para>
	/// </summary>
	internal static int Facing( int personFacing, int cameraOctant )
		=> (cameraOctant + personFacing) & (Compass - 1);

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

	/// <summary>Every person's picture the last frame drew, for the pointer's pick (<see cref="SpritePick"/>): their
	/// bodies, as the original's people's sprites carry the pickable flag <c>0x200</c>.</summary>
	internal IReadOnlyList<SpritePick.Drawn> Drawn => _drawn;

	private readonly List<SpritePick.Drawn> _drawn = [];

	/// <summary>The light a frame's shadows are cast by where the sprites stand in no level: a test's.</summary>
	internal Vector3? ShadowLight { get; set; }

	/// <summary>Whether the last frame pulled the people's bodies toward the eye (<see cref="Pull"/>).</summary>
	internal bool Pulled { get; private set; }

	/// <summary>
	/// Whether the people's bodies are pulled toward the eye this frame: not in a view from inside the park
	/// (<see cref="Level.InsideView"/>; the original's render word <c>0x2000</c>, tested at <c>0x00589f24</c>).
	/// </summary>
	internal static bool PullsBodies => !Level.InsideView;

	protected override void OnRenderTranslucent()
	{
		_drawn.Clear();
		Pulled = PullsBodies;

		if ( TranslucentModel == null || _atlas == null )
			return;

		var (used, reach) = WriteTheFrame();
		var (shadows, shadowReach) = FoldShadows( _shadowsDrawn.Count );

		var buffer = DrawUniforms();

		if ( reach > 0 )
		{
			TranslucentModel.UpdateVertices( _vertices, reach * 4 );

			if ( used > 0 && !DebugHidden )
			{
				TranslucentModel.Material.Set( "g_oUbo", buffer );
				TranslucentModel.Draw();
			}
		}

		// The shadows after the people, as the orbit view's second pass follows its first: a body's picture has written
		// its depth, so the part of its shadow behind it stays hidden. First person's original adds each shadow straight
		// after its own body (BuildShadows says what differs).
		if ( _shadowModel != null && shadowReach > 0 )
		{
			_shadowModel.UpdateVertices( _shadowVertices, shadowReach * 4 );

			if ( shadows > 0 && !DebugShadowsHidden )
			{
				_shadowModel.Material.Set( "g_oUbo", buffer );
				_shadowModel.Draw();
			}
		}
	}

	/// <summary>
	/// Folds to nothing every shadow the last frame wrote and this one did not; answers this frame's count and how many
	/// the upload has to reach (<see cref="Fold"/>'s rule).
	/// </summary>
	private (int Used, int Reach) FoldShadows( int used )
	{
		used = Math.Min( used, _shadowVertices.Length / 4 );

		for ( var i = used; i < _shadowsUploaded; ++i )
			_shadowVertices.AsSpan( i * 4, 4 ).Clear();

		var reach = Math.Max( used, _shadowsUploaded );
		_shadowsUploaded = used;

		return (used, reach);
	}

	/// <summary>
	/// A body's shadow written at the frame's next free shadow, the picture's foot on <paramref name="centre"/>'s place
	/// (<see cref="ParkPersonShadows.Corners"/>), with a park light travelling downwards and room left: the silhouette at
	/// medium's tier in the orbit view, or in first person the body's own atlas picture in black at the darkness alpha
	/// (<see cref="ParkPersonShadows.Kind"/>). The orbit view's own picture of tier 2 and 3 is counted and draws none.
	/// </summary>
	private void WriteShadow( int thingId, Vector3 centre, Region picture, bool mirrored, Vector3? travels, HeightfieldFile? field )
	{
		var kind = ParkPersonShadows.KindFor( _shadowTier, FirstPerson );

		if ( kind == ParkPersonShadows.Kind.OwnPictureNotBuilt )
		{
			Unimplemented.Report( "SPRITE_SHADOW_OWN_PICTURE" );
			return;
		}

		var index = _shadowsDrawn.Count;

		if ( kind == ParkPersonShadows.Kind.None || travels is not { } light || ParkPersonShadows.Darkness( light ) is not { } darkness
			|| (index * 4) + 3 >= _shadowVertices.Length )
			return;

		var firstPerson = kind == ParkPersonShadows.Kind.FirstPersonOwnPicture;
		var corners = _shadowVertices.AsSpan( index * 4, 4 );
		var lift = firstPerson ? ParkPersonShadows.FirstPersonLift : ParkPersonShadows.Lift;
		var colour = firstPerson ? ParkPersonShadows.OwnPictureColour( darkness ) : ParkPersonShadows.Colour( darkness );
		(float, float, float, float)? uv = firstPerson ? (picture.Left, picture.Top, picture.Right, picture.Bottom) : null;

		if ( !ParkPersonShadows.Corners( corners, new Vector2( centre.X, centre.Y ), picture.Width, picture.Height, picture.OriginX,
			mirrored, light, ( x, y ) => field?.CornerPlaneAt( x, y ) ?? 0f, lift, colour, uv ) )
			return;

		_shadowsDrawn.Add( (thingId, [corners[0].Position, corners[1].Position, corners[2].Position, corners[3].Position]) );
	}

	/// <summary>
	/// Writes every quad of the frame and puts them in the order they go to the buffer (<see cref="Finish"/>): how many
	/// were written and how many the upload has to reach. Split from the draw because it needs no device, so a test can
	/// run the frame as <see cref="OnRenderTranslucent"/> does.
	/// </summary>
	internal (int Used, int Reach) WriteTheFrame()
	{
		var field = ParkGround.Current?.Heightfield;
		var used = 0;

		_shadowsDrawn.Clear();
		var travels = Level?.ParkLight?.Travels ?? ShadowLight;

		// The simulation, if this park is running one. Asked once a frame rather than once a person.
		var people = ParkPeople.Current;

		// Room for the balloons bursting, which the crowd's size does not cover, before anything is written.
		if ( people is { Bursting.Count: > BurstingRoom } && ((_people.Count * QuadsAPerson) + people.Bursting.Count) * 4 > _vertices.Length )
			Build( people.Bursting.Count + BurstingRoom );
		var cellX = field?.CellSizeX ?? 0f;
		var cellY = field?.CellSizeY ?? 0f;

		// How far through the current thing tick this frame is, so a walking person is drawn between the
		// two positions the simulation gave them rather than at the later one for eight ticks running.
		// One value for the whole frame, as the original computes one alpha per frame and hands it to
		// every peep (FUN_00518f90).
		var alpha = ParkPeople.ThingTickFraction;

		foreach ( var (person, sprite) in _people )
		{
			if ( InTheHand( people, person.ThingId ) )
				continue;

			var (kind, bank) = LookOf( people, person, sprite );

			// A rider in a bumper boat is drawn as their head on its seat, and no more.
			if ( ParkBumperBoats.Current?.SeatOf( person.ThingId ) is { } boat )
			{
				if ( DrawHead( used, boat.Seat, boat.Frame, kind, bank, sprite.Alpha ) )
					++used;

				used = DrawBubble( used, people, person.ThingId, boat.Seat );

				continue;
			}

			// So is a rider whose head ADDHEAD, or a walk under action 4, hung on a ride's node.
			if ( HeadOnRide( people, person.ThingId ) is { } hung )
			{
				if ( DrawHead( used, hung.At, hung.Frame, kind, bank, sprite.Alpha ) )
					++used;

				used = DrawBubble( used, people, person.ThingId, hung.At );

				continue;
			}

			// A visitor a walk holds is placed by it: on their leg, on their node, or not drawn at all.
			var walked = WalkedOnRide( people, person.ThingId );

			if ( walked is { Hidden: true } )
				continue;

			var (setNumber, frame, bankOffset) = Showing( people?.SpriteFor( person.ThingId ), sprite );

			if ( !_banks.TryGetValue( (kind, bank + bankOffset), out var loaded ) )
				continue;

			// AnyWalkFor rather than WalkFor: staff are drawn from this same list and their walks live in
			// a separate pool, so asking only the guests' one would draw every member of staff at the
			// position the save left them at - see ParkPeople.AnyWalkFor.
			var (x, y, angle) = StandingFrom( people, cellX, cellY, person, sprite, alpha );

			var set = loaded.Bank.Sets[setNumber & 0xf];
			var index = Picture( set, frame, Facing( walked?.Facing ?? ParkWorld.Person.OctantOf( angle ) ),
				out var mirrored );

			if ( index < 0 || index >= loaded.Pictures.Length )
				continue;

			var picture = loaded.Pictures[index];

			// The stored height is an offset above the ground rather than a height, so the land under the
			// sprite (FUN_004527f0, FUN_00542010's sampler) is what decides where its feet go - unless a ride is carrying them, in which case
			// the ride says where they are. The simulation never moves a rider, here or in the original,
			// so their walk still reports the cell they queued on; the script holds them in a bounce slot
			// naming a node of the ride's own model, and that node is where they are drawn, height and
			// all. See Centre for why the choice lives in a function of its own.
			var ground = StandsOn( field, x, y );
			var seat = Seated( people, person.ThingId );

			// The original lifts a bouncing rider by a phase-driven sine height (FUN_00557c75; docs/exe/ride-operation.md,
			// "The bounce riders' height"); here the rider stands on the node, unlifted.
			if ( seat != null )
				Unimplemented.Report( "RIDER_BOUNCE" );

			// The walk's point is the feet's own: FUN_004f9e60 stores its height above the ground under it, which the
			// draw adds back.
			var centre = walked?.At ?? Centre( seat, x, y, ground, sprite.Height );

			WriteQuad( used++, centre, picture, mirrored, sprite.Alpha, pulled: Pulled );
			_drawn.Add( new SpritePick.Drawn( person.ThingId, centre, picture.Width, picture.Height, picture.OriginX, picture.OriginY, mirrored ) );
			WriteShadow( person.ThingId, centre, picture, mirrored, travels, field );

			if ( DebugFacing && _plain is { } plain )
				WriteGroundDash( used++, centre, angle, kind, plain );

			used = DrawBubble( used, people, person.ThingId, centre );
		}

		used = DrawBalloons( people, field, cellX, cellY, alpha, used );

		return (used, Finish( used ));
	}

	/// <summary>
	/// Where the head <c>ADDHEAD</c> or a walk under action 4 hung for this guest stands in the park, and which of the 56 pictures its node shows the
	/// camera (<see cref="ParkBumperBoats.HeadFrame"/>), or null where no script holds a head of theirs on a node with a
	/// position. The head stands where the node is drawn this frame (<see cref="ParkObjects.TryDrawnNodeOn"/>): on the
	/// tentacle a morph has moved, or carried by the arm a clip turns. The picture is chosen by the node's turn at rest,
	/// counted as the head is hung (<see cref="RideScript.Heads"/>).
	/// </summary>
	internal static (Vector3 At, int Frame)? HeadOnRide( ParkPeople? people, int thingId )
	{
		if ( people == null || !people.TryHeadOf( thingId, out var script, out var node ) || script?.Nodes is not { } nodes )
			return null;

		var end = nodes.FindHead( node, out var world );

		if ( end is NodeEnd.Missing or NodeEnd.Unposed )
			return null;

		// The node's matrix is in the model's axes, y up, already turned and placed; the park's are z up. Where the thing is
		// drawn, the head goes where its node is drawn.
		var at = new Vector3( world.M41, world.M43, world.M42 );

		if ( ParkObjects.Current is { } objects
			&& objects.TryDrawnNodeOn( script.ThingId, nodes.HeadIndex( node ), out var drawn ) != DrawnNode.Missing )
			at = drawn;

		var seen = Camera.Position - at;
		var towards = new System.Numerics.Vector3( seen.X, seen.Z, seen.Y );
		var rotation = world with { M41 = 0, M42 = 0, M43 = 0 };

		if ( System.Numerics.Matrix4x4.Invert( rotation, out var inverse ) )
			towards = System.Numerics.Vector3.TransformNormal( towards, inverse );

		return (at, ParkBumperBoats.HeadFrame( towards ));
	}

	/// <summary>
	/// Where the walk holding this guest puts them this frame and which way they face (<see cref="WalkRiders"/>), whether
	/// it hides them, or null where no walk holds them or the placer cannot place them, and they are drawn as anyone else.
	/// The two nodes stand where the ride's players pose them now, the engine's stored matrix of the last drawn frame: a
	/// rider on the Mayan Spinner's node goes up with its drum. The clock is the park's, to the frame (<see cref="GameClock.PartialTick"/>),
	/// as the engine reads its own live.
	/// </summary>
	internal static (bool Hidden, Vector3 At, short Facing)? WalkedOnRide( ParkPeople? people, int thingId, int goingThing = 0 )
	{
		if ( people == null || !people.TryWalkOf( thingId, out var script, out var slot, goingThing ) || WalkRiders.Of( slot ) is not { } placed )
			return null;

		if ( placed.Hidden )
			return (true, default, placed.Facing);

		if ( script?.Nodes is not { } nodes
			|| NodeAt( script, nodes, placed.From, placed.FromSpace ) is not { } from
			|| NodeAt( script, nodes, placed.To, placed.ToSpace ) is not { } to )
		{
			Unimplemented.Report( "WALK_RIDER_NODE_MISS" );
			return null;
		}

		var now = (GameClock.Ticks + GameClock.PartialTick) * GameClock.TickSeconds * 1000f;
		var progress = WalkRiders.Progress( slot, placed, now );

		// The point in the model's axes, y up; the park's are z up.
		return WalkRiders.Point( placed, from, to, progress ) is { } point
			? (false, new Vector3( point.X, point.Z, point.Y ), placed.Facing)
			: null;
	}

	/// <summary>
	/// A walk's node of a ride where the ride's players pose it now (<see cref="RideNodes.Follow"/>), composed as the engine
	/// composes its stored matrix, in the model's axes (y up), or null where it has none. Not the drawn model's node: the
	/// original's saves hold a carried rider's sprite on the composed point to the thousandth, where the Jungle Spray's
	/// drawn lane node stands 0.073 aside.
	/// </summary>
	private static System.Numerics.Vector3? NodeAt( RideScript script, RideNodes nodes, int id, uint space )
	{
		if ( script.Animations is { } players )
			nodes.Follow( players );

		return nodes.Find( id, space, out var position, carried: true ) is NodeEnd.Posed or NodeEnd.RestPose or NodeEnd.OnAFace
			? position
			: null;
	}

	/// <summary>The head bank for a rider drawn in a kind and bank - <c>FUN_004fcac0</c>: a costume's head for a costume, else the child's.</summary>
	internal static (int Kind, int Bank) HeadOf( int kind, int bank )
		=> (kind == ParkSpriteBanks.CostumeKind ? ParkSpriteBanks.CostumeHeadKind : ParkSpriteBanks.KidHeadKind, bank);

	/// <summary>
	/// A rider's head is drawn at 0.685 of a body's size: <c>FUN_0044b510</c> writes the float at <c>0x0074ced0</c> into
	/// the head sprite's local 8 every frame (<c>0x0044ba1f</c>), its scale <c>+0xa4</c>, which the picker hands the draw
	/// as the record's <c>+0x2c</c> (<c>0x005422b2</c>) and <c>SpriteShadow_BuildQuad</c> multiplies by the span.
	/// </summary>
	internal const float HeadScale = 0.685f;

	/// <summary>
	/// A rider's head on a ride's seat node - what <c>FUN_0044b410</c> hangs there: the head of the rider's own child bank
	/// (kind 1), or of their costume's (kind 3) for a guest in costume, chosen by <c>FUN_004fcac0</c>; set 0, frame 0, the
	/// sprite script at <c>0x74f558</c>. Answers whether a quad was written.
	/// </summary>
	/// <remarks>
	/// <b>Two choices, said here.</b> The rider's body is not drawn: the original keeps it, standing, where the rider was
	/// when they boarded (admission's <c>+0x28</c> = 1 stops the guest draw placing it), and this draws a head in the boat
	/// and nothing else (<c>docs/exe/ride-operation.md</c>, "What the head is"). The picture is the one the seat node
	/// shows the camera (<see cref="ParkBumperBoats.HeadFrame"/>); the sprite's flag word <c>0x3000080</c> carries
	/// <c>0x2000000</c>, so the drawing adds no direction of its own (<c>FUN_00542010</c>). The original also rolls the
	/// quad to the node (locals 8 and 9), which is not drawn here (<c>RIDER_HEAD_ROLL</c>, counted as a boat is stood).
	/// </remarks>
	private bool DrawHead( int used, Vector3 seat, int frame, int kind, int bank, int alpha )
	{
		if ( !_banks.TryGetValue( HeadOf( kind, bank ), out var loaded ) || loaded.Bank.Sets.Length == 0 )
		{
			Unimplemented.Report( "RIDER_HEAD_BANK_MISSING" );
			return false;
		}

		var index = loaded.Bank.Sets[0].First + frame;

		if ( index < 0 || index >= loaded.Pictures.Length )
			return false;

		WriteQuad( used, seat, loaded.Pictures[index], false, alpha, HeadScale );

		return true;
	}

	/// <summary>
	/// A person's thought bubble, <see cref="Thoughts.Lift"/> above where they are drawn - the bubble's half of the
	/// per-frame placement (<c>FUN_004fa030</c>, guests and staff). The set's one picture, facing nowhere, at the
	/// alpha a new sprite is made with. Answers the next free quad.
	/// </summary>
	/// <remarks>
	/// A rider drawn as a head has the bubble over the head; the original places it over the body it leaves
	/// standing where they boarded.
	/// </remarks>
	private int DrawBubble( int used, ParkPeople? people, int thingId, Vector3 body )
	{
		if ( people?.ThoughtsOf( thingId )?.Bubble is not { } bubble
			|| !_banks.TryGetValue( (Thoughts.SpriteKind, bubble.Bank), out var bank )
			|| bubble.Set >= bank.Bank.Sets.Length )
			return used;

		var index = Picture( bank.Bank.Sets[bubble.Set], 0, 0, out _ );

		if ( index < 0 || index >= bank.Pictures.Length )
			return used;

		WriteQuad( used, body + new Vector3( 0f, 0f, Thoughts.Lift ), bank.Pictures[index], false, 0xff );

		return used + 1;
	}

	/// <summary>
	/// Places every held balloon and draws it, then every one bursting where it was left - the balloon half of the
	/// original's per-frame placement (<c>FUN_004fa030</c>, guests only), which runs whether or not the guest's own
	/// picture is placed. Answers the next free quad.
	/// </summary>
	private int DrawBalloons( ParkPeople? people, HeightfieldFile? field, float cellX, float cellY, float alpha, int used )
	{
		if ( people == null || !_banks.TryGetValue( (Balloon.SpriteKind, 0), out var bank ) )
			return used;

		foreach ( var peep in people.Guests.Values )
		{
			if ( peep.Balloon is not { } held )
				continue;

			// With no walk, the balloon stays where it was.
			if ( people.WalkFor( peep.ThingId ) is { } walk && cellX > 0f && cellY > 0f )
			{
				var placed = Balloon.Place( walk.Previous, walk.Position, alpha, cellX, cellY, peep.ThingId, _bobPhase,
					peep.BalloonLastX, peep.BalloonLastY );

				(held.X, held.Y, held.Height) = (placed.X, placed.Y, placed.Height);
				(peep.BalloonLastX, peep.BalloonLastY) = (placed.LastX, placed.LastY);

				// The shared phase counts every placement, on the frame clock here (Balloon.PlacementsPerSecond). Whether
				// the original's per-frame placement runs while a park's menu has paused its clock is not traced.
				(_bobPhase, _bobCount) = Balloon.AdvanceBob( _bobPhase, _bobCount, Balloon.PlacementsPerSecond * Time.Delta );
			}

			used = DrawBalloon( used, held, bank, field );
		}

		foreach ( var letGo in people.Bursting )
			used = DrawBalloon( used, letGo, bank, field );

		return used;
	}

	/// <summary>
	/// One balloon's quad, at its placed position above the ground under it (<c>FUN_00542010</c>: the ground plus
	/// <c>+0x8c</c>), at its own alpha, and only once its script has shown a frame. It faces nowhere: a balloon set has
	/// no directions, so the frame is the picture.
	/// </summary>
	private int DrawBalloon( int used, Balloon balloon, Loaded bank, HeightfieldFile? field )
	{
		var sprite = balloon.Sprite;

		if ( !sprite.Shown )
			return used;

		var set = bank.Bank.Sets[sprite.Set];
		var index = Picture( set, sprite.Frame, 0, out _ );

		if ( index < 0 || index >= bank.Pictures.Length )
			return used;

		var ground = StandsOn( field, balloon.X, balloon.Y );

		WriteQuad( used, new Vector3( balloon.X, balloon.Y, ground + balloon.Height ), bank.Pictures[index], false,
			sprite.Alpha );

		return used + 1;
	}

	/// <summary>
	/// Which kind and bank a person is drawn in: a guest's own, <see cref="Peep.SpriteKind"/> and
	/// <see cref="Peep.SpriteBank"/>, which a costume changes, as the original builds a guest's sprite again from
	/// <c>+0x24</c> and <c>+0x20</c> on leaving a thing (<c>FUN_004d4140</c>); anybody else's, or anybody with no
	/// simulation running them, the saved sprite's, brought within the banks loaded.
	/// </summary>
	private (int Kind, int Bank) LookOf( ParkPeople? people, ParkWorld.Person person, ParkWorld.Sprite sprite )
		=> LookOf( people, _counts, person, sprite );

	/// <summary>A packed bank by its kind and its number among the kind's, or null when it is not packed.</summary>
	internal SpriteBankFile? BankAt( int kind, int bank ) => _banks.TryGetValue( (kind, bank), out var loaded ) ? loaded.Bank : null;

	/// <summary>
	/// The bank a person's sprite is drawn from now, for a state animation's group (<c>FUN_00475b80</c> reads the
	/// sprite's kind <c>+0xac</c> and bank <c>+0xb0</c>), or null when it is not packed.
	/// </summary>
	internal SpriteBankFile? BankOf( int thingId )
	{
		var at = _people.FindIndex( entry => entry.Person.ThingId == thingId );

		if ( at < 0 )
			return null;

		var (person, sprite) = _people[at];
		var (kind, bank) = LookOf( ParkPeople.Current, person, sprite );

		return _banks.TryGetValue( (kind, bank + sprite.BankOffset), out var loaded ) ? loaded.Bank : null;
	}

	/// <inheritdoc cref="LookOf(ParkPeople?, ParkWorld.Person, ParkWorld.Sprite)"/>
	internal static (int Kind, int Bank) LookOf( ParkPeople? people, ParkSpriteBanks? counts, ParkWorld.Person person,
		ParkWorld.Sprite sprite )
		=> people != null && people.Guests.TryGetValue( person.ThingId, out var peep )
			? (peep.SpriteKind, peep.SpriteBank)
			: (sprite.Type, counts?.Reduce( sprite.Type, sprite.Bank ) ?? sprite.Bank);

	/// <summary>
	/// Where to draw this person, finding their walk for the caller.
	///
	/// <para>
	/// <b>The pool choice lives here so a test can reach it</b>; in the draw loop it would want a graphics
	/// device. It is <see cref="ParkPeople.AnyWalkFor"/> because staff are drawn from this same list and
	/// <see cref="ParkPeople.WalkFor"/> is the guests' pool alone. The arithmetic is <see cref="Standing"/>'s.
	/// </para>
	/// </summary>
	/// <remarks>
	/// <b>Named rather than overloaded, and the compiler is what settled it.</b> Written as a second
	/// <c>Standing</c> it was ambiguous with the one below: an existing test passes a bare <c>null</c> to
	/// say "no walk, so draw them where the save left them", and <c>null</c> fits
	/// <see cref="ParkPeople"/> and <see cref="PeepWalk"/> equally well. That test is a real one, so the
	/// name moved rather than the test.
	/// </remarks>
	internal static (float X, float Y, int Angle) StandingFrom( ParkPeople? people, float cellX, float cellY,
		ParkWorld.Person person, ParkWorld.Sprite sprite, float alpha = 1f )
		=> Standing( people?.AnyWalkFor( person.ThingId ), cellX, cellY, person, sprite, alpha );

	/// <summary>
	/// Where a person's picture is centred: on the ride carrying them when one is, and on the ground
	/// under them when none is.
	/// </summary>
	/// <remarks>
	/// <b>Split out so the preference can be tested.</b> Resolving a seat needs <see cref="ParkObjects"/>,
	/// which wants a graphics device; this much is arithmetic and needs none. That a seat is correctly
	/// RESOLVED rests on the screenshots and the ride census.
	/// <para>
	/// The sprite's own height is an offset above whatever it stands on, so it is added either way -
	/// a rider sits above their node exactly as a walker stands above the land.
	/// </para>
	/// </remarks>
	internal static Vector3 Centre( Vector3? seat, float x, float y, float ground, float spriteHeight )
		=> seat is { } on
			? new Vector3( on.X, on.Y, on.Z + spriteHeight )
			: new Vector3( x, y, ground + spriteHeight );

	/// <summary>
	/// Where a person stands in the world: their position on the ground under it, or null with no ground
	/// loaded. It is what the original reads off a walking person's sprite (<c>FUN_004754e0</c>).
	/// </summary>
	internal static Vector3? Feet( FixedVector at )
		=> ParkGround.Current?.Heightfield is { } field ? FeetOn( field, at ) : null;

	/// <summary>
	/// The ground a person, a balloon's anchor or a marker's feet stand on: the sampler the sprites' draw takes, <c>FUN_004527f0</c> (<c>0x005420ee</c>),
	/// nought with no ground loaded.
	/// </summary>
	internal static float StandsOn( HeightfieldFile? field, float x, float y )
		=> field?.ScapeHeight( x, y ) ?? 0f;

	/// <summary>Where a person at <paramref name="at"/> stands on <paramref name="field"/>: the ground under it by the sampler the sprites' draw takes (<c>FUN_004527f0</c>).</summary>
	internal static Vector3 FeetOn( HeightfieldFile field, FixedVector at )
	{
		var x = (at.X / (float)FixedVector.One) * field.CellSizeX;
		var y = (at.Y / (float)FixedVector.One) * field.CellSizeY;

		return new Vector3( x, y, StandsOn( field, x, y ) );
	}

	/// <summary>
	/// Where a ride is carrying this person, or null when none is.
	///
	/// <para>
	/// Three things have to agree for a seat to exist: the people have to know which ride holds them and
	/// on which node (<see cref="ParkPeople.TrySeatOf"/>, asked of the ride's own script), the park's
	/// objects have to have a model standing for that ride, and that model has to carry the node
	/// (<see cref="ParkObjects.TryNodeOn"/>). Any one of them missing means the guest is drawn where
	/// they are walking, which is the right answer for everybody not on a ride.
	/// </para>
	/// </summary>
	internal static Vector3? Seated( ParkPeople? people, int thingId )
	{
		if ( people == null || ParkObjects.Current is not { } objects )
			return null;

		if ( !people.TrySeatOf( thingId, out var ride, out var node ) )
			return null;

		return objects.TryNodeOn( ride, ParkPeople.BounceNodeName( node ), out var world ) ? world : null;
	}

	/// <summary>
	/// Where a person is drawn and which way they are turned: from the simulation when one is running them,
	/// and from the save when it is not.
	///
	/// <para>
	/// <b>Why this exists at all.</b> <see cref="_people"/> holds record structs copied out of the save when
	/// the park opened, and nothing writes to them, so without this a guest the simulation has walked half
	/// way across the park would be drawn where the file left them.
	/// </para>
	/// <para>
	/// <b>The scale is the heightfield's own, and that is the whole argument for it.</b> A position in the
	/// simulation is 16.16 fixed point where one is a map cell; <see cref="ParkGround"/> lays its ground
	/// vertices at <c>cell * CellSize</c>, so multiplying by the same <c>CellSizeX</c>/<c>CellSizeY</c> is
	/// what keeps a person's feet on the terrain rather than merely near it. The save's own sprite
	/// coordinates were measured against this and agree on x to about one part in ten thousand, but wander
	/// by up to a third of a per cent on y - so they are not the thing to calibrate against, and are used
	/// only as the fallback below.
	/// </para>
	/// <para>
	/// <b>It falls back rather than guessing.</b> With no simulation, or before the ground has loaded and a
	/// cell size is known, the saved position and heading are what get drawn.
	/// </para>
	/// </summary>
	/// <remarks>
	/// <b>It takes the walk rather than the pool of them</b>, which makes the choice and the arithmetic
	/// testable without a graphics device or a live <see cref="ParkPeople"/>; the lookup is the caller's.
	/// </remarks>
	/// <param name="alpha">
	/// How far through the current thing tick this frame is - <see cref="ParkPeople.ThingTickFraction"/>.
	/// <b>One by default, which is the position the simulation actually left them at</b>, so a caller with
	/// no frame to place - a test, or anything asking "where is this person" rather than "where do I draw
	/// them this frame" - gets that position.
	/// </param>
	internal static (float X, float Y, int Angle) Standing( PeepWalk? walk, float cellX, float cellY,
		ParkWorld.Person person, ParkWorld.Sprite sprite, float alpha = 1f )
	{
		if ( walk == null || cellX <= 0f || cellY <= 0f )
			return (sprite.X, sprite.Y, person.Angle);

		var at = walk.Position;
		var from = walk.Previous;

		// prev + (cur - prev) * alpha, per axis - FUN_004f9f00, whose two halves are at 0x004f9f89 and
		// 0x004f9fb6. The simulation moves somebody once every eight ticks, about four times a second;
		// this is what carries them there over the frames in between instead of jumping.
		var x = from.X + (at.X - from.X) * alpha;
		var y = from.Y + (at.Y - from.Y) * alpha;

		// <b>The heading is deliberately NOT blended</b>, and that is the original's own arrangement:
		// 0x004fa015 copies the octant straight off the thing into the out-param while the positions
		// either side of it are being interpolated. So a guest's position glides and their facing snaps.
		return ((x / FixedVector.One) * cellX,
			(y / FixedVector.One) * cellY,
			walk.Heading);
	}

	/// <summary>
	/// Which picture to draw: from the animation when one is playing, and from the save when it is not.
	///
	/// <para>
	/// <b>This is what stops a guest sliding.</b> Both numbers come from the little program the original
	/// runs, which chooses a set and steps a frame; the save record's pair alone would carry a guest across
	/// the park in a single frozen pose.
	/// </para>
	/// <para>
	/// <b>The bank offset comes from the same word as the set, and has to travel with it.</b> The original
	/// packs both into one number and its "choose a set" instruction writes the whole word, so taking the
	/// set from the animation and the bank from the save would be reading one number out of two places -
	/// and would put a guest in another child's clothes the moment their script ran.
	/// </para>
	/// <para>
	/// It takes the one animation rather than the pool of them for the same reason <see cref="Standing"/>
	/// does: so the choice can be tested without an entity or a graphics device.
	/// </para>
	/// </summary>
	internal static (int Set, int Frame, int BankOffset) Showing( SpriteScript? playing, ParkWorld.Sprite sprite )
		=> playing == null || playing.Script == SpriteScript.None
			? (sprite.Set, sprite.Frame, sprite.BankOffset)
			: (playing.Set, playing.Frame, playing.BankOffset);

	/// <summary>
	/// How far a picture reaches in the world from the point it is drawn at, square on to the eye: left of it, right of
	/// it, above it and below it, its origin on the point.
	/// </summary>
	internal static (float Left, float Right, float Above, float Below) Extent( int width, int height, int originX, int originY, float instanceScale = 1f )
	{
		var scale = instanceScale * WorldPerReference / PictureReference;
		return (originX * scale, (width - originX) * scale, originY * scale, (height - originY) * scale);
	}

	/// <summary>
	/// The two sides of a picture's quad across the eye, from the point it is drawn at, and the column of the picture
	/// each side shows (nought its left edge, the width its right). Reflected, the original negates both sides about the
	/// origin and leaves the texture as it is (<c>FUN_005890e0</c>, <c>0x005891e5</c> to <c>0x00589256</c>), so column c
	/// is drawn at the origin plus <c>originX - c</c>: the picture turns about the person's feet.
	/// </summary>
	internal static (float Left, int LeftColumn, float Right, int RightColumn) Across( int width, int height, int originX, int originY, bool mirrored, float instanceScale = 1f )
	{
		var (leftward, rightward, _, _) = Extent( width, height, originX, originY, instanceScale );
		return mirrored ? (-rightward, width, leftward, 0) : (-leftward, 0, rightward, width);
	}

	/// <summary>
	/// Writes a picture's quad at <paramref name="index"/>, its origin on <paramref name="centre"/>; a person's body is
	/// <paramref name="pulled"/> toward the eye (<see cref="Pull"/>).
	/// </summary>
	private void WriteQuad( int index, Vector3 centre, Region picture, bool mirrored, int alpha, float instanceScale = 1f, bool pulled = false )
	{
		if ( index < 0 || index * 4 >= _vertices.Length )
			return;

		var forward = Camera.Rotation.Forward;
		var corners = _vertices.AsSpan( index * 4, 4 );
		_depths[index] = (centre - Camera.Position).Dot( forward );

		if ( !Corners( corners, forward, centre, picture, mirrored, alpha, instanceScale ) )
		{
			Collapse( index );
			return;
		}

		if ( pulled )
			Pull( corners, Camera.Position, forward );
	}

	/// <summary>
	/// The original's draw matrix: <c>z = Q (e - near)</c>, <c>w = sin 45 e</c>, near 0.1 (<c>0x0042aa4e</c>) and Q built with a
	/// far of 1000, for an eye depth e (<c>FUN_00578be0</c>).
	/// </summary>
	private const float PullNear = 0.1f;

	private static readonly float PullQ = MathF.Sin( MathF.PI / 4f ) / (1f - (PullNear / 1000f));

	/// <summary>
	/// The eye depth the original tests a person's corner at when it is drawn at <paramref name="depth"/>:
	/// <c>Sprite_BuildQuad</c> (<c>FUN_00589b50</c>, <c>0x00589f17</c> to <c>0x00589f59</c>) moves each corner's clip z to
	/// <c>z - 0.2 / (z + 1)</c>, which with the draw matrix above is <c>e / (1 + 0.2 / (Q near (z + 1)))</c>: 110 is tested at
	/// 106.185, 3.81 nearer (docs/exe/park-engine.md, "What hides a person below their feet").
	/// </summary>
	internal static float PulledDepth( float depth )
	{
		var z = PullQ * (depth - PullNear);
		return depth / (1f + (0.2f / (PullQ * PullNear * (z + 1f))));
	}

	/// <summary>
	/// Moves each corner along its own line from the eye to <see cref="PulledDepth"/>, so the picture lands on the same
	/// pixels and is tested that much nearer: what lies under a person's origin and just in front of it, the ground
	/// and a tool's squares, no longer hides their feet. The original pulls only a sprite whose flags carry
	/// <c>0x200</c>, a person's body, and not under the render word's <c>0x2000</c>, set by the ride and first-person
	/// views; first person and the End's ride view are those built here, so the caller asks for no pull in either
	/// (<see cref="PullsBodies"/>).
	/// </summary>
	internal static void Pull( Span<Vertex> corners, Vector3 eye, Vector3 forward )
	{
		for ( var i = 0; i < corners.Length; ++i )
		{
			var seen = corners[i].Position - eye;
			var depth = seen.Dot( forward );

			if ( depth <= PullNear )
				continue;

			corners[i] = corners[i] with { Position = eye + (seen * (PulledDepth( depth ) / depth)) };
		}
	}

	/// <summary>
	/// Puts the first <paramref name="used"/> quads in the order the original draws its people's batch: furthest from
	/// the eye first (<c>SpriteList_Draw</c>, <c>FUN_0058a770</c>, each group by its <c>+8</c>), by the depth of the point each is
	/// drawn at. Their pictures write depth, so a nearer one drawn first would hide the edge of one behind it that
	/// its own clear fringe should have let through. Every quad is keyed by its unpulled depth; whether the original's order
	/// agrees for a pulled body against an unpulled head, bubble or balloon is not settled.
	/// </summary>
	private void FurthestFirst( int used ) => FurthestFirst( _vertices, _depths, used, _sortScratch );

	/// <inheritdoc cref="FurthestFirst(int)"/>
	internal static void FurthestFirst( Vertex[] vertices, float[] depths, int used )
		=> FurthestFirst( vertices, depths, used, new SortScratch() );

	/// <summary>
	/// What <see cref="FurthestFirst(int)"/> sorts with, kept from one frame to the next so a frame of people allocates
	/// none of it: the order, a copy of the keys, a copy of the quads, and the comparer itself (the keys' copy, furthest
	/// first), which the sort calls through an interface rather than a delegate that captures them.
	/// </summary>
	private sealed class SortScratch : IComparer<int>
	{
		public int[] Order = [];
		public float[] Keys = [];
		public Vertex[] Quads = [];

		public int Compare( int a, int b ) => Keys[b].CompareTo( Keys[a] );
	}

	private readonly SortScratch _sortScratch = new();

	private static void FurthestFirst( Vertex[] vertices, float[] depths, int used, SortScratch scratch )
	{
		used = Math.Min( used, Math.Min( vertices.Length / 4, depths.Length ) );

		if ( used < 2 )
			return;

		if ( scratch.Order.Length < used )
		{
			scratch.Order = new int[used];
			scratch.Keys = new float[used];
			scratch.Quads = new Vertex[used * 4];
		}

		var (order, keys, quads) = (scratch.Order, scratch.Keys, scratch.Quads);

		for ( var i = 0; i < used; ++i )
			order[i] = i;

		depths.AsSpan( 0, used ).CopyTo( keys );

		// The same introsort as sorting by a comparison, so equal depths fall in the same order as they always have.
		Array.Sort( order, 0, used, scratch );

		vertices.AsSpan( 0, used * 4 ).CopyTo( quads );

		for ( var i = 0; i < used; ++i )
		{
			quads.AsSpan( order[i] * 4, 4 ).CopyTo( vertices.AsSpan( i * 4, 4 ) );
			depths[i] = keys[order[i]];
		}
	}

	/// <summary>
	/// A picture's four corners square on to an eye looking along <paramref name="forward"/>, its origin on
	/// <paramref name="centre"/>: top left, top right, bottom right, bottom left, each with the picture's u and v there.
	/// False, writing nothing, looking straight down.
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
	internal static bool Corners( Span<Vertex> corners, Vector3 forward, Vector3 centre, Region picture, bool mirrored, int alpha, float instanceScale = 1f )
	{
		var across = forward.Cross( Vector3.Up );

		// Looking straight down there is no horizontal left to hang the sprite on, and every quad
		// would be edge-on anyway.
		if ( across.LengthSquared < 0.000001f )
			return false;

		across = across.Normal;

		// Square to both the view and that across, which is the camera's own up with no roll in it.
		var upward = across.Cross( forward ).Normal;

		var (_, _, above, below) = Extent( picture.Width, picture.Height, picture.OriginX, picture.OriginY, instanceScale );
		var (leftAt, leftColumn, rightAt, _) = Across( picture.Width, picture.Height, picture.OriginX, picture.OriginY, mirrored, instanceScale );

		var left = across * leftAt;
		var right = across * rightAt;
		var top = upward * above;
		var bottom = upward * -below;

		// White at the sprite's own alpha: the shader multiplies the picture by this, so anything else
		// would tint the art.
		var colour = ((uint)Math.Clamp( alpha, 0, 255 ) << 24) | 0x00ffffffu;

		var u0 = leftColumn == 0 ? picture.Left : picture.Right;
		var u1 = leftColumn == 0 ? picture.Right : picture.Left;

		corners[0] = new Vertex( centre + left + top, new Vector2( u0, picture.Top ) ) { MatFlags = colour };
		corners[1] = new Vertex( centre + right + top, new Vector2( u1, picture.Top ) ) { MatFlags = colour };
		corners[2] = new Vertex( centre + right + bottom, new Vector2( u1, picture.Bottom ) ) { MatFlags = colour };
		corners[3] = new Vertex( centre + left + bottom, new Vector2( u0, picture.Bottom ) ) { MatFlags = colour };

		return true;
	}

	/// <summary>
	/// A flat dash on the ground pointing the way a person faces, coloured by their kind. For the debug
	/// console only - see <see cref="DebugFacing"/>.
	///
	/// <para>
	/// Zero is towards <b>lower</b> y. That is not a choice - it falls out of
	/// <see cref="PeepHeading"/>, whose cardinals come from the branch structure of the original's own
	/// <c>FUN_006e7074</c>: <c>-y</c> is 0, <c>-x</c> is <c>0x200</c>, <c>+y</c> is <c>0x400</c> and
	/// <c>+x</c> is <c>0x600</c>. Reading the angle as a bearing from <c>+y</c> would negate both
	/// components of every direction in the park.
	/// </para>
	/// </summary>
	private void WriteGroundDash( int index, Vector3 feet, int angle, int type, Region plain )
	{
		if ( index < 0 || (index * 4) + 3 >= _vertices.Length )
			return;

		var turn = angle / (float)PeepHeading.FullTurn * MathF.Tau;
		var along = new Vector3( -MathF.Sin( turn ), -MathF.Cos( turn ), 0f );
		var side = along.Cross( Vector3.Up );

		if ( side.LengthSquared < 0.000001f )
		{
			Collapse( index );
			return;
		}

		side = side.Normal * 0.4f;

		// Just clear of the ground, or it fights the terrain it is drawn on.
		var start = feet + new Vector3( 0f, 0f, 0.15f );
		var end = start + (along * 4f);

		var colour = DebugColours[Math.Clamp( type, 0, DebugColours.Length - 1 )];

		// The MIDDLE of the white square, not its corner. Every region in the atlas is packed with
		// clear pixels around it so the smaller mip levels of one picture cannot bleed into the next;
		// sampling exactly on a region's edge blends with that padding instead, which would draw these
		// dashes as dark grey streaks rather than in the colour asked for.
		var u = (plain.Left + plain.Right) * 0.5f;
		var w = (plain.Top + plain.Bottom) * 0.5f;

		var v = index * 4;
		_depths[index] = (feet - Camera.Position).Dot( Camera.Rotation.Forward );

		Corner( v, start - side );
		Corner( v + 1, start + side );
		Corner( v + 2, end + side );
		Corner( v + 3, end - side );

		void Corner( int vertex, Vector3 position )
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

		if ( Current == this )
			Current = null;

		_atlas?.Delete();
		_atlas = null;

		// The silhouette came by its path, so the texture cache may be holding it for another: only the model goes.
		_shadowModel?.Delete();
		_shadowModel = null;
	}

	/// <summary>
	/// Every person in the park, one line each, for the debug console's <c>guests</c> command; the balloons are
	/// <see cref="BalloonCensus"/>'s. This is
	/// what a label over each head would have said, without needing a world-to-screen projection that
	/// does not exist in this engine.
	/// </summary>
	internal IEnumerable<string> Census()
	{
		// Exactly what OnRenderTranslucent asks, so this census reports where a person is actually
		// DRAWN rather than where the file left them.
		var field = ParkGround.Current?.Heightfield;
		var cellX = field?.CellSizeX ?? 0f;
		var cellY = field?.CellSizeY ?? 0f;
		var people = ParkPeople.Current;

		// The same fraction the drawing uses, for the same reason this census asks Standing at all: a
		// census computing a position of its own is a census that can disagree with the picture.
		var alpha = ParkPeople.ThingTickFraction;

		foreach ( var (person, sprite) in _people )
		{
			var walk = people?.AnyWalkFor( person.ThingId );
			var playing = people?.SpriteFor( person.ThingId );
			var (x, y, angle) = Standing( walk, cellX, cellY, person, sprite, alpha );

			// <b>The same override the drawing applies.</b> It computes a position of its own rather than
			// reading the one the renderer used, so without it a rider drawn up on the ride would be
			// reported at the cell they queued on.
			var head = ParkBumperBoats.Current?.SeatOf( person.ThingId );
			var walked = WalkedOnRide( people, person.ThingId );
			var seated = head?.Seat ?? HeadOnRide( people, person.ThingId )?.At ?? (walked is { Hidden: false } w ? w.At : (Vector3?)null)
				?? Seated( people, person.ThingId );

			if ( seated is { } seat )
				(x, y) = (seat.X, seat.Y);
			var (setNumber, frame, bankOffset) = Showing( playing, sprite );
			var (kind, bank) = LookOf( people, person, sprite );

			yield return $"thing {person.ThingId,2} model {person.Model} " +
				$"cell ({person.CellX},{person.CellY}) slot {sprite.Slot,2} " +
				$"type {kind} bank {bank}+{bankOffset} (saved {sprite.Type}/{sprite.Bank}) set {setNumber} " +
				$"frame {frame} (saved set {sprite.Set} frame {sprite.Frame}) " +
				$"script {(playing == null ? "none" : $"{playing.Script}@{playing.Pc} turns {playing.Turns} loops [{string.Join( ",", playing.Loops )}] flags 0x{playing.DrawFlags:x}")} " +
				$"facing {ParkWorld.Person.OctantOf( angle )} (angle {angle}) " +
				// Three decimals, not one: a guest covers about 0.24 of a cell per thing tick, so one
				// interpolated frame moves them roughly 0.16 world units - which at one decimal place is
				// barely above the printing granularity and could not be told from a jump.
				$"drawn ({x:0.000},{y:0.000}) alpha {alpha:0.00}" +
				$"{(seated is { } on ? $" SEATED z {on.Z:0.0}" : "")}{(head is { } h ? $" HEAD only frame {h.Frame}" : "")}" +
				$"{(walked is { } by ? (by.Hidden ? " WALK hides" : $" WALK at ({by.At.X:0.000},{by.At.Y:0.000},{by.At.Z:0.000}) facing {by.Facing}") : "")} " +
				$"saved ({sprite.X:0.0},{sprite.Y:0.0}) " +
				$"cellsize {cellX:0.##}x{cellY:0.##} walk {(walk == null ? "none" : "found")}" +
				$"{(InTheHand( people, person.ThingId ) ? " IN THE HAND, not drawn" : "")}";
		}
	}

	/// <summary>A member in the hand is not drawn in the park: the pickup frees their sprite (<c>FUN_00505c50</c>).</summary>
	internal static bool InTheHand( ParkPeople? people, int thingId ) => people is { CarriedStaff: not 0 } && people.CarriedStaff == thingId;

	/// <summary>
	/// Every balloon, held and bursting, one line each, where it was last placed: the ground under it plus its
	/// height; then one line for each thought bubble showing. The debug console's <c>guests</c> command prints them
	/// after the people.
	/// </summary>
	internal IEnumerable<string> BalloonCensus()
	{
		var field = ParkGround.Current?.Heightfield;

		if ( ParkPeople.Current is not { } people )
			yield break;

		foreach ( var peep in people.Guests.Values )
		{
			if ( peep.Balloon is { } held )
				yield return BalloonLine( $"balloon held by {peep.ThingId,2}", held, field );
		}

		foreach ( var letGo in people.Bursting )
			yield return BalloonLine( "balloon let go", letGo, field );

		foreach ( var (person, _) in _people )
		{
			if ( people.ThoughtsOf( person.ThingId ) is { Bubble: { } bubble } thoughts )
				yield return $"bubble over {person.ThingId,2} thought 0x{thoughts.Last:x} bank {bubble.Bank} set {bubble.Set} "
					+ $"since {thoughts.TimeBubbleShown} packed {_banks.ContainsKey( (Thoughts.SpriteKind, bubble.Bank) )}";
		}
	}

	private static string BalloonLine( string whose, Balloon balloon, HeightfieldFile? field )
		=> $"{whose} set {balloon.Sprite.Set} frame {balloon.Sprite.Frame} alpha {balloon.Sprite.Alpha} " +
			$"shown {balloon.Sprite.Shown} script {balloon.Sprite.Script}@{balloon.Sprite.Pc} " +
			$"at ({balloon.X:0.000},{balloon.Y:0.000}) height {balloon.Height:0.000} " +
			$"ground {StandsOn( field, balloon.X, balloon.Y ):0.000}";
}
