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
	/// size is its pixels over 128, times the span below.
	///
	/// <para>
	/// <b>The span is measured rather than chosen.</b> The draw path <c>FUN_00589410</c> scales a picture
	/// by its instance's own scale times <c>0x0070200c</c>, which holds <b>20.0</b> and sits in read-only
	/// <c>.rdata</c>. Every other term on the vertical chain is one: the factor at <c>0x00768ab4</c> is
	/// 1.0 and has no writer anywhere in the image, the call site <c>FUN_00589990</c> passes 1.0f for both
	/// of its multipliers, and all eighteen of the shipped park's sprites carry scale 1.0/1.0. A picture's
	/// height in the world is therefore its pixels times twenty over a hundred and twenty-eight, exactly.
	/// </para>
	/// <para>
	/// <b>The one term that is not one belongs to the viewport, and is deliberately not applied here.</b>
	/// The horizontal chain also multiplies by <c>0x008bcbcc</c>, which reads zero in the static image
	/// because it is written at device setup. It is the aspect ratio: <c>FUN_0056b790</c> builds the
	/// frustum corners at unit depth as <c>y = 0.5 * tan(fov/2)</c> and <c>x = y / 0x008bcbcc</c>, and
	/// since <c>x = y * aspect</c> for any camera, that factor is height over width. Two other users
	/// agree - one scales only the horizontal of a screen-space quad by it, the other applies it only to a
	/// default horizontal size. Our projection matrix already carries the aspect, so applying it a second
	/// time here would squash every person in the park by it.
	/// </para>
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

	private readonly List<(ParkWorld.Person Person, ParkWorld.Sprite Sprite)> _people = [];
	private readonly Dictionary<(int Type, int Bank), Loaded> _banks = [];

	private Texture? _atlas;
	private Region? _plain;
	private Vertex[] _vertices = [];
	private int _uploaded;

	/// <summary>
	/// The pool this park is drawing with, so the debug console can read the census back. Same
	/// arrangement as <see cref="ParkGround.Current"/>, and it exists for the console alone.
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
	/// Enough colours to tell the <b>nine</b> sprite kinds this maps apart at a glance - see
	/// <see cref="DebugFacing"/>. This said fourteen, which is the original's table size rather than
	/// this array's: <c>FolderFor</c> covers 0 to 8, and an index past the end clamps.
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
	/// holds the eleven kinds below that name it, and each theme holds only costumes, costume heads and
	/// entertainers. So exactly one of those two sweeps ever finds anything, and a Jungle park can no
	/// more number a Fantasy entertainer than it can a second Generic.
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

	/// <summary>
	/// The four banks the original loads <i>before</i> it sweeps the folder, in this order, and only for
	/// kids and kid heads. <c>Sprites_LoadFolder</c> matches this table - it is at <c>0x764030</c> in the
	/// executable - against the folder's files first, loads what it finds, and only then sweeps up
	/// whatever is left.
	///
	/// <para>
	/// It matters because a bank is numbered by the order it was loaded in, and that number is what the
	/// save writes down. The archive lists <c>Generic\Kids</c> as BE, BI, CH, FR, KI, SA, SU, TA, so
	/// sweeping it plainly makes bank 0 the BE child; the original makes bank 0 the BI child and pushes BE
	/// out to 4. Every one of the shipped park's thirteen guests wears a kids bank, and between them they
	/// wear banks 0, 2, 4, 5, 6 and 7 - so sweeping plainly dresses the entire park in the wrong children
	/// while still looking perfectly plausible, which is exactly why it went unnoticed.
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
		var files = data.GetFiles( folder )
			.Where( file => file.EndsWith( ".esp", StringComparison.OrdinalIgnoreCase ) )
			.ToArray();

		// Kinds 0 and 1 are kids and kid heads; every other folder is numbered just as it comes.
		if ( type is not (0 or 1) )
			return files;

		var first = AvatarFirst
			.Select( name => files.FirstOrDefault( file =>
				Path.GetFileNameWithoutExtension( file ).Equals( name, StringComparison.OrdinalIgnoreCase ) ) )
			.OfType<string>()
			.ToArray();

		return [.. first, .. files.Except( first )];
	}

	public ParkGuestSprites( string themeName, ParkWorld? park )
	{
		if ( park == null )
			return;

		// Only the people. The table can hold litter and balloons and thought bubbles too, and those
		// have no thing of their own to take a position from yet.
		var byPerson = park.People.ToDictionary( person => person.SpriteSlot, person => person );

		foreach ( var sprite in park.Sprites )
		{
			if ( byPerson.TryGetValue( sprite.Slot, out var person ) )
				_people.Add( (person, sprite) );
		}

		Current = this;

		// Only the banks those sprites actually wear. Loading every person bank in the archive would be
		// 5,316 pictures and an atlas 13,885 pixels tall, past what a good many devices will allocate at
		// all; the shipped park wears nine banks and comes to well under two thousand.
		Load( themeName, park );

		if ( _people.Count > 0 && _atlas != null )
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

		foreach ( var key in _people.Select( p => (p.Sprite.Type, Bank: p.Sprite.Bank + p.Sprite.BankOffset) ).Distinct() )
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

		// A small white square for the debug dash, the way ScreenParticles carries one for effects
		// with no frames of their own: it makes solid-colour geometry possible without a second
		// material or a shader that can do without a texture.
		pictures.Add( new SpritePicture( 4, 4, 2, 2, [.. Enumerable.Repeat( (byte)255, 4 * 4 * 4 )] ) );

		_atlas = BuildAtlas( pictures, out var regions );
		_plain = regions[^1];

		foreach ( var (type, bank, first, count, file) in placed )
			_banks[(type, bank)] = new Loaded( file, regions[first..(first + count)] );
	}

	/// <summary>
	/// Takes somebody who was not in the save - a guest who has just arrived - so that they are drawn
	/// alongside everyone else.
	///
	/// <para>
	/// <b>Their bank has to be one this park already packs.</b> The atlas is built once, from the banks
	/// the save's own people wear, and nothing here adds to it afterwards - so a guest wearing an
	/// unpacked bank has no picture at all. That reads as a broken arrival rather than as a missing
	/// texture, which is the worst kind of fault to introduce. Lost Kingdom packs six banks of type 0,
	/// measured from a running park, and an arrival should choose from those.
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
	/// A crowd that shrinks needs no more care than one that grows: the draw pass counts what it wrote
	/// this frame and collapses every quad between that and what it wrote last, so the departed one's
	/// triangles are folded to nothing rather than left hanging. <see cref="Build"/> is still re-run,
	/// because the vertex array is sized from the crowd and leaving it long would waste an upload's
	/// worth of it every frame.
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
	/// A quad per sprite, sized to the crowd and rewritten each frame - the arrangement
	/// <see cref="WeatherSprites"/> uses, and for the same reason: there is no instancing here, so a
	/// crowd has to be one mesh.
	///
	/// <para>
	/// Re-run whenever the crowd changes size, not only at load - see <see cref="Add"/>.
	/// </para>
	/// </summary>
	private void Build()
	{
		// Two quads a person: the sprite, and the debug dash that is usually collapsed to nothing.
		var quads = _people.Count * 2;

		_vertices = new Vertex[quads * 4];

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

		// <b>Rounded to the nearest eighth, and the arithmetic that was here only looked like it was.</b>
		// It read ((yaw - PI/8) / TAU * 8) + 0.5, in which the two corrections CANCEL exactly - PI/8 is a
		// sixteenth of a turn, so dividing it by TAU and scaling by 8 gives precisely the 0.5 that is then
		// added back. What was left was a plain floor of yaw in eighths. That would be harmless if the
		// camera ever sat between two eighths, and it never does: the orbit camera turns in steps of
		// exactly PI/4, so EVERY position it can hold lands exactly on a boundary, where the answer is
		// decided by the last bit of a float. Looking due south, atan2 came back as 3.99999989 eighths and
		// floored to 3. Adding the half AFTER the scaling is what makes a boundary the middle of a bucket
		// rather than its edge.
		return (int)MathF.Floor( (yaw / MathF.Tau * Compass) + 0.5f ) & (Compass - 1);
	}

	/// <summary>
	/// Which of the eight ways round a sprite is seen from: its own heading, turned by the camera's.
	///
	/// <para>
	/// <b>Added, not subtracted, and that is the whole of it.</b> The two numberings run in opposite
	/// directions - a person's clockwise, the camera's anticlockwise (see <see cref="CameraOctant"/>) - so
	/// adding them is what cancels the camera's rotation. Subtracting them, which is what this did, applies
	/// it twice: the drawn picture then came out wrong by exactly twice the camera's angle, which is why a
	/// guest looked right from due north and south and exactly backwards from east and west, and appeared
	/// to swing round to keep facing the viewer as the camera orbited.
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

	protected override void OnRenderTranslucent()
	{
		if ( TranslucentModel == null || _atlas == null )
			return;

		var field = ParkGround.Current?.Heightfield;
		var used = 0;

		// The simulation, if this park is running one. Asked once a frame rather than once a person.
		var people = ParkPeople.Current;
		var cellX = field?.CellSizeX ?? 0f;
		var cellY = field?.CellSizeY ?? 0f;

		foreach ( var (person, sprite) in _people )
		{
			var (setNumber, frame, bankOffset) = Showing( people?.SpriteFor( person.ThingId ), sprite );

			if ( !_banks.TryGetValue( (sprite.Type, sprite.Bank + bankOffset), out var loaded ) )
				continue;

			// AnyWalkFor rather than WalkFor: staff are drawn from this same list and their walks live in
			// a separate pool, so asking only the guests' one drew every member of staff at the position
			// the save left them at - see ParkPeople.AnyWalkFor.
			var (x, y, angle) = StandingFrom( people, cellX, cellY, person, sprite );

			var set = loaded.Bank.Sets[setNumber & 0xf];
			var index = Picture( set, frame, Facing( ParkWorld.Person.OctantOf( angle ) ),
				out var mirrored );

			if ( index < 0 || index >= loaded.Pictures.Length )
				continue;

			var picture = loaded.Pictures[index];

			// The stored height is an offset above the ground rather than a height, so the land under the
			// sprite is what decides where its feet go - unless a ride is carrying them, in which case
			// the ride says where they are. The simulation never moves a rider, here or in the original,
			// so their walk still reports the cell they queued on; the script holds them in a bounce slot
			// naming a node of the ride's own model, and that node is where they are drawn, height and
			// all. See Centre for why the choice lives in a function of its own.
			var ground = field?.HeightAtWorld( x, y ) ?? 0f;
			var centre = Centre( Seated( people, person.ThingId ), x, y, ground, sprite.Height );

			WriteQuad( used++, centre, picture, mirrored, sprite.Alpha );

			if ( DebugFacing && _plain is { } plain )
				WriteGroundDash( used++, centre, angle, sprite.Type, plain );
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
	/// Where a person is drawn and which way they are turned: from the simulation when one is running them,
	/// and from the save when it is not.
	///
	/// <para>
	/// <b>Why this exists at all.</b> <see cref="_people"/> holds record structs copied out of the save when
	/// the park opened, and nothing writes to them. Before this, a guest the simulation had walked half way
	/// across the park was still drawn where the file left them.
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
	/// cell size is known, the saved position and heading are what get drawn - which is what this did
	/// before, so a park without people still looks exactly as it did.
	/// </para>
	/// </summary>
	/// <remarks>
	/// <b>It takes the walk rather than the pool of them, and a control run is why.</b> Written the other
	/// way it needed a live <see cref="ParkPeople"/> - an entity - to exercise at all, so nothing tested it;
	/// a mutation making this ignore the simulation entirely and draw everyone at their saved position left
	/// the whole suite green. Handing in the one walk makes the choice and the arithmetic testable without a
	/// graphics device or an entity, and the lookup moves to the caller, which is where it belongs anyway.
	/// </remarks>
	/// <summary>
	/// Where to draw this person, finding their walk for the caller.
	///
	/// <para>
	/// <b>This overload exists because the pool choice was the bug, and the pool choice was the one part
	/// nothing could test.</b> The arithmetic below has been covered since it was written; the LOOKUP sat
	/// in the draw loop, which wants a graphics device, so no test could reach it. Reverting it to
	/// <see cref="ParkPeople.WalkFor"/> - the guests-only pool, which is exactly the defect Alexah saw as
	/// "the staff still don't walk" - left all 763 tests green. Moving the choice here, and only the
	/// choice, makes it answerable without a device while leaving the split the overload below describes.
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
		ParkWorld.Person person, ParkWorld.Sprite sprite )
		=> Standing( people?.AnyWalkFor( person.ThingId ), cellX, cellY, person, sprite );

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
	/// <summary>
	/// Where a person's picture is centred: on the ride carrying them when one is, and on the ground
	/// under them when none is.
	/// </summary>
	/// <remarks>
	/// <b>Split out because the choice was the change, and the choice was the part nothing could
	/// test.</b> Resolving a seat needs <see cref="ParkObjects"/>, which wants a graphics device, so
	/// neutering <see cref="Seated"/> left all 769 tests green - riders would have gone back to standing
	/// at the front of the queue and the suite would not have said a word. That is the same shape as the
	/// staff bug found the same day: a call site no test could reach. This much is arithmetic and needs
	/// no device, so the preference itself is now pinned; that a seat is correctly RESOLVED still rests
	/// on the screenshots and the ride census.
	/// <para>
	/// The sprite's own height is an offset above whatever it stands on, so it is added either way -
	/// a rider sits above their node exactly as a walker stands above the land.
	/// </para>
	/// </remarks>
	internal static Vector3 Centre( Vector3? seat, float x, float y, float ground, float spriteHeight )
		=> seat is { } on
			? new Vector3( on.X, on.Y, on.Z + spriteHeight )
			: new Vector3( x, y, ground + spriteHeight );

	internal static Vector3? Seated( ParkPeople? people, int thingId )
	{
		if ( people == null || ParkObjects.Current is not { } objects )
			return null;

		if ( !people.TrySeatOf( thingId, out var ride, out var node ) )
			return null;

		return objects.TryNodeOn( ride, ParkPeople.BounceNodeName( node ), out var world ) ? world : null;
	}

	/// <inheritdoc cref="StandingFrom"/>
	internal static (float X, float Y, int Angle) Standing( PeepWalk? walk, float cellX, float cellY,
		ParkWorld.Person person, ParkWorld.Sprite sprite )
	{
		if ( walk == null || cellX <= 0f || cellY <= 0f )
			return (sprite.X, sprite.Y, person.Angle);

		var at = walk.Position;

		return ((at.X / (float)FixedVector.One) * cellX,
			(at.Y / (float)FixedVector.One) * cellY,
			walk.Heading);
	}

	/// <summary>
	/// Which picture to draw: from the animation when one is playing, and from the save when it is not.
	///
	/// <para>
	/// <b>This is what stops a guest sliding.</b> Both numbers used to come straight off the save record and
	/// nothing ever moved them, so a guest crossed the park in a single frozen pose. They now come from the
	/// little program the original runs, which chooses a set and steps a frame.
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

	/// <summary>
	/// A flat dash on the ground pointing the way a person faces, coloured by their kind. For the debug
	/// console only - see <see cref="DebugFacing"/>.
	///
	/// <para>
	/// <b>Which way zero points IS now established, and this used to have it exactly backwards.</b> The
	/// paragraph here said the convention was "chosen, not a measurement", and that if every dash in the
	/// park were wrong by the same amount that would be the answer it was drawn to show. It showed it: the
	/// first time guests actually walked, every dash pointed half a turn away from the way its owner was
	/// going.
	/// </para>
	/// <para>
	/// Zero is towards <b>lower</b> y. That is not a choice either - it falls out of
	/// <see cref="PeepHeading"/>, whose cardinals come from the branch structure of the original's own
	/// <c>FUN_006e7074</c>: <c>-y</c> is 0, <c>-x</c> is <c>0x200</c>, <c>+y</c> is <c>0x400</c> and
	/// <c>+x</c> is <c>0x600</c>. Reading the angle as a bearing from <c>+y</c>, as this did, negates both
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
		// sampling exactly on a region's edge blends with that padding instead, which drew these
		// dashes as dark grey streaks rather than in the colour asked for.
		var u = (plain.Left + plain.Right) * 0.5f;
		var w = (plain.Top + plain.Bottom) * 0.5f;

		var v = index * 4;

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
	}

	/// <summary>
	/// Every person in the park, one line each, for the debug console's <c>guests</c> command. This is
	/// what a label over each head would have said, without needing a world-to-screen projection that
	/// does not exist in this engine.
	/// </summary>
	internal IEnumerable<string> Census()
	{
		// Exactly what OnRenderTranslucent asks, so this census reports where a person is actually
		// DRAWN rather than where the file left them. Those were the same thing until the simulation
		// started moving people, and this printing the saved cell is why a park where nobody moved
		// looked, from here, identical to one where everybody did.
		var field = ParkGround.Current?.Heightfield;
		var cellX = field?.CellSizeX ?? 0f;
		var cellY = field?.CellSizeY ?? 0f;
		var people = ParkPeople.Current;

		foreach ( var (person, sprite) in _people )
		{
			var walk = people?.AnyWalkFor( person.ThingId );
			var playing = people?.SpriteFor( person.ThingId );
			var (x, y, angle) = Standing( walk, cellX, cellY, person, sprite );

			// <b>The same override the drawing applies, and this census lied without it.</b> It computes
			// a position of its own rather than reading the one the renderer used, so while a rider was
			// being drawn up on the ride this still reported the cell they queued on - and a run of it
			// read exactly like a build where the seat did nothing at all.
			var seated = Seated( people, person.ThingId );

			if ( seated is { } seat )
				(x, y) = (seat.X, seat.Y);
			var (setNumber, frame, bankOffset) = Showing( playing, sprite );

			yield return $"thing {person.ThingId,2} model {person.Model} " +
				$"cell ({person.CellX},{person.CellY}) slot {sprite.Slot,2} " +
				$"type {sprite.Type} bank {sprite.Bank}+{bankOffset} set {setNumber} " +
				$"frame {frame} (saved set {sprite.Set} frame {sprite.Frame}) " +
				$"script {(playing == null ? "none" : $"{playing.Script}@{playing.Pc}")} " +
				$"facing {ParkWorld.Person.OctantOf( angle )} (angle {angle}) " +
				$"drawn ({x:0.0},{y:0.0}){(seated is { } on ? $" SEATED z {on.Z:0.0}" : "")} " +
				$"saved ({sprite.X:0.0},{sprite.Y:0.0}) " +
				$"cellsize {cellX:0.##}x{cellY:0.##} walk {(walk == null ? "none" : "found")}";
		}
	}
}
