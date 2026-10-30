namespace OpenTPW;

/// <summary>
/// What the pointer is over in the park: which cell of the map, and which thing is standing on it.
///
/// <para>
/// <b>Every verb that starts with pointing at the ground asks this</b> - buying, placing, moving,
/// selling, dropping a member of staff.
/// </para>
///
/// <para>
/// <b>The original picks by RAY CAST, not by inverting a grid.</b> <c>FUN_0045bf90</c> builds a ray
/// from the cursor, walks the terrain height grid testing the two triangles of each cell, then marches
/// the ray again against object meshes. <c>FUN_0045d560</c> turns the hit point into a packed cell id
/// and writes it to the one global everything else reads - and that packing is
/// <c>y * 0x80 + 1 + x</c>, which is exactly the <c>y * 128 + x + 1</c> this project already uses.
/// </para>
///
/// <para>
/// <b>The terrain is the original's two planes a cell</b> (<see cref="ParkPickGround"/>): each cell's triangles cut by its
/// <c>0x2</c>, a plane's hit counting anywhere in the cell's square, the nearest kept - so where a cell folds into a ridge
/// the pick lands where the original's does, above the drawn ground.
/// </para>
///
/// <para>
/// <b>The ground is only the heightfield, and the cell never leaves the map</b> (<c>docs/exe/park-engine.md</c>, "The
/// picker never hands the mode a cell off the map"). The terrain test walks the heightfield's own cells (96 x 85), so
/// past them nothing is met; the hit is held to the heightfield's last cell and then below <c>MapInfo</c>'s
/// <c>HeightfieldWidth</c> and <c>HeightfieldHeight</c> (columns 0 to 94, rows 0 to 83); and a ray that meets nothing
/// leaves the last cell and point standing (<c>FUN_0045bf90</c> writes its point only on a hit, <c>FUN_0045d560</c>
/// packs it every frame).
/// </para>
///
/// <para>
/// <b>A model nearer the eye than the ground gives the point</b> (<see cref="ParkPickMarch"/>): after the terrain the
/// ray is marched a cell at a time and each cell's placed thing has its triangles tested, so the pointer on the park
/// gate's arch answers the gate's cells, not the ground behind it.
/// </para>
///
/// <para>
/// <b>The camera's own roll may NOT be used to build the ray.</b> <see cref="Camera.Rotation"/> comes
/// from <c>Rotation.LookAt</c>, whose roll is arbitrary (<c>AudioListener.Facing</c> says why) - only its forward
/// vector means anything. The view matrix is built by <c>CreateLookTo</c> against the world's up, so
/// the basis here is rebuilt the same way. Taking <c>Rotation.Right</c> instead would work only while
/// the roll happened to be zero.
/// </para>
/// </summary>
public static class ParkPicking
{
	/// <summary>
	/// The cell under the pointer, packed as <c>y * 128 + x + 1</c>: where the ray meets nothing, the last cell it met.
	/// <b>Nought before the first meeting, and again after <see cref="Forget"/></b> - a deviation: the original's word is
	/// never nought. It starts at 1 (cell (0,0), <c>FUN_0046c0b0</c> at start-up) and its point P (<c>0x007a1c70</c>)
	/// starts at (0,0,0) and is carried from one park to the next, so it is never emptied (<c>docs/exe/park-engine.md</c>,
	/// "The picker never hands the mode a cell off the map"). The difference is a frame or two before the first meeting
	/// over the sea at (0,0), where there are no peeps and no row.
	/// </summary>
	public static int Cell { get; private set; }

	/// <summary>Where the ray last met the ground, for anything that wants more than a cell.</summary>
	public static Vector3 WorldPoint { get; private set; }

	/// <summary>
	/// The person whose picture is under the pointer, taken as the original takes them (<see cref="SpritePick"/>:
	/// latched by the draw, then found on the hovered cell or a neighbour, <c>FUN_00532bd0</c>, <c>[0x007b05ac]</c>), or
	/// nought.
	/// </summary>
	public static int PersonUnderCursor { get; private set; }

	/// <summary>
	/// The thing on the hovered cell that is not a person (<see cref="ThingOn"/>), or nought: the original's object
	/// is the first thing on the cell <c>[0x007b05cc]</c> (<c>FUN_00527e80</c>), which <c>FUN_0046c110</c> stores at
	/// <c>[0x007b05b0]</c> each frame and the click snapshots (<c>docs/exe/park-engine.md</c>), whoever the person is, so a guest
	/// under the pointer does not hide the ride their feet are beside.
	/// </summary>
	public static int ObjectUnderCursor { get; private set; }

	/// <summary>The person whose picture the last frame's draw latched for the pointer, taken or not, for the debug
	/// console.</summary>
	public static int Latched { get; private set; }

	/// <summary>The cell under the pointer as a pair, or false before the ray has first met the ground.</summary>
	public static bool TryCell( out int cellX, out int cellY )
	{
		if ( Cell <= 0 )
		{
			cellX = cellY = 0;
			return false;
		}

		cellX = (Cell - 1) % ParkWorld.MapSize;
		cellY = (Cell - 1) / ParkWorld.MapSize;

		return true;
	}

	/// <summary>
	/// Works out what the pointer is over, once a frame.
	/// </summary>
	/// <remarks>
	/// <b>It reads the camera as the last frame left it.</b> The camera moves in <see cref="Level.Render"/>,
	/// after this runs, so the ray is built from the previous frame's eye - which is what the interface
	/// is already drawn against, and is invisible while the camera only moves on input. The alternative,
	/// picking after the camera, would put the answer a frame ahead of the picture instead.
	/// </remarks>
	public static void Update()
	{
		var answer = Resolve( Input.Mouse.Position );

		if ( answer.Cell > 0 )
		{
			if ( answer.Met != PointerMet )
			{
				Log.Info( answer.Met ? $"pick: the ray meets the ground again, cell {answer.Cell}"
					: $"pick: the ray meets nothing, cell {answer.Cell} stands" );
			}

			PointerMet = answer.Met;
		}

		// A cell pinned from the debug console stands in for the pointer's, for the reason `worldclick`
		// exists: the console cannot move the pointer, so without it nothing that
		// follows the pointer - the queue tool's squares and cursor - could be driven or photographed.
		if ( Pinned is { } pinned )
			answer = answer with { Cell = MapStep.CellId( pinned.X, pinned.Y ) };

		if ( answer.Marched != Marched )
			Log.Info( answer.Marched != 0 ? $"pick: the march meets thing {answer.Marched}, cell {answer.Cell}" : "pick: the march meets nothing" );

		Marched = answer.Marched;
		Cell = answer.Cell;
		WorldPoint = answer.World;
		PersonUnderCursor = answer.Person;
		ObjectUnderCursor = answer.Object;
		Latched = answer.Latched;
	}

	/// <summary>A cell the debug console's <c>hover</c> holds the pointer over, or null to follow the mouse.</summary>
	public static (int X, int Y)? Pinned { get; set; }

	/// <summary>The thing whose model the pointer's object march met, whether or not its point beat the ground's (<c>[0x007a1cd4]</c>, and <c>[0x007afda0]</c> through <c>FUN_00469340</c>), or nought.</summary>
	public static int Marched { get; private set; }

	/// <summary>Whether the pointer's last ray met the ground: false where its cell is the one left standing.</summary>
	public static bool PointerMet { get; private set; } = true;

	/// <summary>Lets go of the last cell and the console's pin, as a park ends, so the next park's picker starts with none.</summary>
	public static void Forget()
	{
		Pinned = null;
		Cell = 0;
		WorldPoint = default;
		PersonUnderCursor = ObjectUnderCursor = Latched = Marched = 0;
		PointerMet = true;
	}

	/// <summary>What the ray through one point of the window meets: <c>Met</c> false where the cell is the one left standing.</summary>
	internal readonly record struct Answer( int Cell, Vector3 World, int Person, int Object, int Latched, bool Met = true,
		int Marched = 0 );

	/// <summary>
	/// The whole of the picking arithmetic, given a position on the window. <see cref="Update"/> hands
	/// it the pointer; <see cref="PickAt"/> hands it whatever it is asked to. Where the ray meets nothing, the
	/// pointer's last cell and point stand, so a console question about a point answers what the pointer would there.
	/// </summary>
	private static Answer Resolve( Vector2 screen )
	{
		if ( ParkGround.Current is not { Heightfield: not null } )
			return default;

		// Zero until the camera has run once - see Camera.CalcViewProjMatrix. A ray built from it on
		// the first frame would be a line straight down the forward axis.
		if ( Camera.HorizontalTangent <= 0f )
			return default;

		return Cast( screen, Camera.Position, RayThrough( screen ) );
	}

	/// <summary>What a ray from <paramref name="from"/> meets, the ground and then the object march, for <see cref="Resolve"/>
	/// and the console's <see cref="PickRay"/>; <paramref name="screen"/> is the window point a person's picture is latched by.</summary>
	private static Answer Cast( Vector2 screen, Vector3 from, Vector3 direction )
	{
		if ( ParkGround.Current is not { Heightfield: { } field } ground )
			return default;

		var state = Level.Current?.ParkState;

		var (cell, met, hit) = Meet( field, from, direction, ground.MapInfoWidth, ground.MapInfoHeight );

		var marched = 0;

		if ( state != null && March( from, direction, met ? hit : null, ground.MapInfoWidth, ground.MapInfoHeight,
			( x, y ) => CandidateOn( state, Level.Current?.Catalogue, ParkObjects.Current, field, x, y ) ) is { } thing )
		{
			marched = thing.Thing;

			if ( thing.Nearer )
			{
				(hit, met) = (thing.Point, true);
				var (x, y) = CellOf( field, hit, ground.MapInfoWidth, ground.MapInfoHeight );
				cell = MapStep.CellId( x, y );
			}
		}

		if ( cell <= 0 )
			return default;

		return Assemble( cell, met, hit, screen, ParkGuestSprites.Current?.Drawn, state ) with { Marched = marched };
	}

	/// <summary>
	/// The thing the object march met and where, and whether that point is nearer <paramref name="from"/> than
	/// <paramref name="ground"/> (or the ray met no ground). The thing is kept either way, as the original stores it before
	/// its nearer test (<c>0x0045cccf</c>, <c>[0x007a1cd4]</c>; <c>FUN_00469340</c>, <c>[0x007afda0]</c>); its point replaces
	/// the ground's only where strictly nearer, the ground's winning a tie (<c>0x0045ccf7</c>..<c>0x0045cd35</c>). The march
	/// asks <paramref name="thingOn"/> each cell held to <paramref name="mapWidth"/> by <paramref name="mapHeight"/>.
	/// </summary>
	internal static (int Thing, Vector3 Point, bool Nearer)? March( Vector3 from, Vector3 direction, Vector3? ground, int mapWidth,
		int mapHeight, Func<int, int, ParkPickMarch.Candidate?> thingOn )
	{
		var start = from.GetSystemVector3();

		if ( ParkPickMarch.March( start, direction.GetSystemVector3(), ground?.GetSystemVector3(),
			( x, y ) => ParkPickMarch.Hold( x, y, mapWidth, mapHeight ) is var (heldX, heldY) ? thingOn( heldX, heldY ) : null )
			is not { } met )
			return null;

		var nearer = ground is not { } g
			|| System.Numerics.Vector3.Distance( met.Point, start ) < System.Numerics.Vector3.Distance( g.GetSystemVector3(), start );

		return (met.Thing, (Vector3)met.Point, nearer);
	}

	/// <summary>
	/// What the object march asks of a cell (<c>FUN_00452ae0</c> handed its thing back): the thing the cell answers
	/// (<see cref="ParkItemHeights.ThingAnswering"/>), its <c>.hmp</c> height there over its model's root, the floor of
	/// its <c>.hmp</c>'s box as the file has it (the model's <c>+0x84</c>, which the loader copies from it), and its model
	/// as drawn. <c>FUN_0053bf30</c> answers a cell's track-layer object, of any kind (<c>+4</c>, or for kinds 12 and 17 the
	/// <c>+0x10</c> parent's), before <c>FUN_00527e80</c>. This park builds none, so a cell whose track kind is one of the
	/// five the lift test names (11, 12, 16, 17 and 25, the boundary's hoarding among them) is counted and answers nothing;
	/// another kind holding an object is not tested (a deviation, Q214).
	/// </summary>
	private static ParkPickMarch.Candidate? CandidateOn( ParkState state, ParkItemCatalogue? catalogue, ParkObjects? objects,
		HeightfieldFile field, int x, int y )
	{
		if ( state.Record( x, y ).TrackType is 11 or 12 or 16 or 17 or 25 )
		{
			Unimplemented.Report( "PICK_OBJECT_MARCH_TRACK_THING" );
			return null;
		}

		var gate = ParkFixedItems.Current?.ThingFor( "gates" ) ?? 0;
		var (entranceA, entranceB) = Level.Current?.Balance is { } balance
			? (ParkAdmission.Cell( balance, "EntranceA" ), ParkAdmission.Cell( balance, "EntranceB" ))
			: ((-9, -9), (-9, -9));

		var thing = ParkItemHeights.ThingAnswering( state, entranceA, entranceB, gate, x, y );

		if ( PlacedThing( state, thing ) is not { } placed || objects?.ModelFor( thing ) is not { } model || catalogue == null
			|| !catalogue.TryGet( placed.CatalogueId, out var item ) || ParkItemHeights.For( item ) is not { } file )
			return null;

		var root = RootOf( model );

		if ( ParkItemHeights.AboveBase( file, root, placed.Angle, field, x, y ) is not { } above )
			return null;

		return CandidateOf( thing, above + root.Z, file.BoxMin.Y, model );
	}

	/// <summary>
	/// A candidate of the march with the model to meet. Made here, not in <see cref="CandidateOn"/>, so the closure over
	/// the model is built only for a thing that is met: a lambda in that method makes its closure on entry, for every cell.
	/// </summary>
	private static ParkPickMarch.Candidate CandidateOf( int thing, float top, float floor, LobbyModel model )
		=> new( thing, top, floor, ( from, direction ) => ParkPickMarch.MeetModel( model, from, direction ) );

	/// <summary>
	/// The first placed thing with this id, or null for nought or one not placed. Walked by index: the object march asks it
	/// of every cell a ray crosses, every frame, and a search through the list as a sequence is a delegate and an
	/// enumerator a cell.
	/// </summary>
	internal static ParkWorld.CatalogueObject? PlacedThing( ParkState state, int thing )
	{
		for ( var i = 0; thing != 0 && i < state.Objects.Count; ++i )
		{
			if ( state.Objects[i].ThingId == thing )
				return state.Objects[i];
		}

		return null;
	}

	/// <summary>
	/// Where a standing model's root node is in the world (the original's root matrix, <c>+0x40</c> to <c>+0x48</c>):
	/// its place in the file, turned and moved as the model was stood. A placed thing's root is its file's origin, so
	/// this is where it was stood; the gate's carries the gate's place in the park.
	/// </summary>
	private static System.Numerics.Vector3 RootOf( LobbyModel model )
	{
		var file = model.Source;
		var root = file.Nodes.FindIndex( node => node.ParentIndex < 0 );
		var at = root >= 0 ? file.Nodes[root].WorldTransform.Translation : default;

		return System.Numerics.Vector3.Transform( new System.Numerics.Vector3( at.X, at.Z, at.Y ), model.PlacedRotation )
			+ model.PlacedOrigin.GetSystemVector3();
	}

	/// <summary>
	/// What the pointer is over once the ray has handed on a cell: the picture the window's pixel latches
	/// (<see cref="LatchFor"/>, against the window's width and height and the camera's matrices), the person and the
	/// object taken on that cell (<see cref="Take"/>), and the point, the ray's own where it met the ground and the
	/// one standing where it did not. Apart from <see cref="Resolve"/> so that its wiring can be tested without a camera
	/// and a heightfield.
	/// </summary>
	internal static Answer Assemble( int cell, bool met, Vector3 hit, Vector2 screen, IEnumerable<SpritePick.Drawn>? drawn,
		ParkState? state )
	{
		var (cellX, cellY) = MapStep.CellAt( cell );

		var latched = drawn is not null
			? LatchFor( drawn, screen, Screen.Width, Screen.Height, Camera.ViewMatrix * Camera.ProjMatrix )
			: 0;

		var (person, placed) = state is not null ? Take( state, latched, cellX, cellY ) : (0, 0);

		return new Answer( cell, met ? hit : WorldPoint, person, placed, latched, met );
	}

	/// <summary>
	/// What a ray from <paramref name="from"/> hands on: the packed cell (<see cref="Answered"/>, the pointer's own
	/// <see cref="Cell"/> standing where it meets nothing), whether it met the ground, and where.
	/// </summary>
	internal static (int Cell, bool Met, Vector3 Hit) Meet( HeightfieldFile field, Vector3 from, Vector3 direction,
		int mapWidth, int mapHeight )
	{
		var met = MeetGround( field, from, direction, out var hit );

		return (Answered( field, met ? hit : null, Cell, mapWidth, mapHeight ), met, hit);
	}

	/// <summary>
	/// The packed cell the picker hands on: the cell of a ground <paramref name="hit"/> (<see cref="CellOf"/>), or, where
	/// the ray met nothing, the <paramref name="standing"/> one, as <c>FUN_0045d560</c> packs the point
	/// <c>FUN_0045bf90</c> left unwritten.
	/// </summary>
	internal static int Answered( HeightfieldFile field, Vector3? hit, int standing, int mapWidth, int mapHeight )
	{
		if ( hit is not { } at )
			return standing;

		var (x, y) = CellOf( field, at, mapWidth, mapHeight );

		return MapStep.CellId( x, y );
	}

	/// <summary>
	/// The cell a ground hit falls in, as <c>FUN_0045d560</c> packs it: across and down the heightfield from its origin,
	/// truncated, held to 0 and to the heightfield's last cell, then below <paramref name="mapWidth"/> and
	/// <paramref name="mapHeight"/> (<c>MapInfo</c>'s, <c>[0x007a0314]</c> and <c>[0x007a0374]</c>; nought holds nothing).
	/// </summary>
	internal static (int X, int Y) CellOf( HeightfieldFile field, Vector3 hit, int mapWidth, int mapHeight )
	{
		var x = Math.Clamp( (int)((hit.X - field.OriginX) / field.CellSizeX), 0, field.CellsX - 1 );
		var y = Math.Clamp( (int)((hit.Y - field.OriginY) / field.CellSizeY), 0, field.CellsY - 1 );

		if ( mapWidth > 0 )
			x = Math.Clamp( x, 0, mapWidth - 1 );

		if ( mapHeight > 0 )
			y = Math.Clamp( y, 0, mapHeight - 1 );

		return (x, y);
	}

	/// <summary>
	/// The person whose picture a frame's draw latches for a point of a window <paramref name="width"/> by
	/// <paramref name="height"/> (<see cref="SpritePick.Latch"/>), against the camera that frame was drawn with: the point
	/// and the pictures in the box of the 4:3 frame the lens draws the original's picture in (<see cref="SpritePick.Frame"/>),
	/// so the circle is the original's at every window shape.
	/// </summary>
	internal static int LatchFor( IEnumerable<SpritePick.Drawn> drawn, Vector2 screen, float width, float height,
		System.Numerics.Matrix4x4 viewProjection )
		=> SpritePick.Latch( drawn, SpritePick.Box( screen, width, height ), viewProjection, SpritePick.Frame( width, height ) );

	/// <summary>
	/// What the pointer takes on a cell, as two values the way the original keeps them: the latched person if they
	/// stand on it or a neighbour (<see cref="SpritePick.Taken"/>, <c>[0x007b05ac]</c>), and the cell's thing that is not a
	/// person (<see cref="ThingOn"/>, <c>FUN_00527e80</c> on <c>[0x007b05cc]</c>).
	/// </summary>
	internal static (int Person, int Object) Take( ParkState state, int latched, int cellX, int cellY )
		=> (SpritePick.Taken( state, latched, cellX, cellY ), ThingOn( state, cellX, cellY, people: false ));

	/// <summary>
	/// What one cell holds: whoever heads its list, or - failing that - the object that owns it; with
	/// <c>people</c> false, the first on its list that is not a person, or the owner.
	/// </summary>
	/// <remarks>
	/// <b>A placed thing is on ONE cell's occupancy list, and it is not usually the one you click.</b>
	/// The save names the thing on its anchor cell alone; every other cell of its footprint carries the
	/// owner in <c>mParentID</c> instead, as the packed cell that owner stands on. Measured over the
	/// shipped park: the Belly Bounce's twelve cells all read <c>par 2996</c> and only (51,23) reads
	/// <c>occ 13</c>; the Jungle Spray's nine read <c>par 3892</c>; the Drinks Shop's four <c>par
	/// 3884</c>. So a click that asked occupancy alone answered on <b>1 of 12</b> cells of a ride and
	/// nothing anywhere else on it.
	/// <para>
	/// <b>Occupancy is asked FIRST</b>: a thing on the cell's own list comes before the owner, which is the
	/// fallback. With <c>people</c> true a guest heading a ride's anchor cell answers there instead of the ride.
	/// </para>
	/// <para>
	/// <b>Dead by CODE:</b> the <c>people: true</c> arm is called by tests alone. The pointer asks with <c>people: false</c>
	/// (<see cref="Take"/>), and the console's <c>worldclick</c> reads the cell's head itself and asks this with
	/// <c>people: false</c> for the rest.
	/// </para>
	/// <para>
	/// The lookup is <see cref="ParkPathBuilding"/>'s own, which a queue cell already uses to name the
	/// thing it serves - the same field, read the same way.
	/// </para>
	/// </remarks>
	/// <remarks>
	/// Internal rather than private only so that it can be tested, the same reason
	/// <see cref="ParkFixedItems.ThingByCatalogue"/> is: a test that re-derived this over the picking
	/// ray would need a camera, a heightfield and a loaded park, and would pass just as well with the
	/// owner lookup put back.
	/// </remarks>
	/// <param name="people">Whether a person on the cell's list answers. The pointer takes people by their pictures
	/// (<see cref="SpritePick"/>) and asks the cell for the rest (<see cref="Take"/>); true takes whoever heads the cell,
	/// which nothing outside the tests does.</param>
	internal static int ThingOn( ParkState state, int cellX, int cellY, bool people = true )
	{
		var crowd = ParkPeople.Current;

		for ( int standing = state.CellAt( cellX, cellY ).Occupant; standing != 0; standing = state.NextOnCell( standing ) )
		{
			if ( people || crowd == null || !(crowd.Guests.ContainsKey( standing ) || crowd.IsStaff( standing )) )
				return standing;
		}

		var parent = state.Record( cellX, cellY ).ParentId;

		if ( parent == 0 )
			return 0;

		var (ownerX, ownerY) = MapStep.CellAt( parent );

		foreach ( var placed in state.Objects )
		{
			if ( placed.CellX == ownerX && placed.CellY == ownerY )
				return placed.ThingId;
		}

		return 0;
	}

	/// <summary>
	/// What the ray through an arbitrary point of the window meets, without disturbing what the
	/// pointer is over.
	///
	/// <para>
	/// <b>It exists because the console cannot move the pointer.</b> Picking is a function of
	/// the mouse, so asking the running game "what is under the cursor" only ever reports wherever the
	/// cursor was left - and a harness that warps the pointer is then testing the window system as
	/// much as the arithmetic. This separates them: hand it a position and the answer depends on
	/// nothing but the camera and the land. It is the same justification the <c>arrive</c>,
	/// <c>load</c> and <c>thirst</c> commands already carry - an instrument that creates the condition
	/// it needs to measure.
	/// </para>
	/// </summary>
	public static string PickAt( float screenX, float screenY )
	{
		var answer = Resolve( new Vector2( screenX, screenY ) );

		return Describe( answer, $"asked ({screenX:F0},{screenY:F0})" );
	}

	/// <summary>
	/// What a ray from <paramref name="from"/> through <paramref name="toward"/> meets, without disturbing what the pointer
	/// is over: an instrument for handing this picker the original's own ray (its eye and its point P), so that what the
	/// two games' models and march make of one ray is compared apart from their two cameras.
	/// </summary>
	public static string PickRay( Vector3 from, Vector3 toward )
		=> Describe( Cast( new Vector2( -1f, -1f ), from, (toward - from).Normal ), $"ray from ({from.X:F1},{from.Y:F1},{from.Z:F1})" );

	/// <summary>
	/// The cell under an arbitrary point of the window, without disturbing what the pointer is over.
	/// False only before the ray has first met the ground: where it meets nothing, the pointer's last cell stands.
	/// </summary>
	/// <remarks>
	/// The same justification <see cref="PickAt"/> carries, one step further on: the console cannot press
	/// the mouse button either, so a console-driven click has to be able to say which cell it means.
	/// </remarks>
	public static bool TryCellAt( float screenX, float screenY, out int cellX, out int cellY )
	{
		var answer = Resolve( new Vector2( screenX, screenY ) );

		cellX = cellY = 0;

		if ( answer.Cell <= 0 )
			return false;

		cellX = (answer.Cell - 1) % ParkWorld.MapSize;
		cellY = (answer.Cell - 1) / ParkWorld.MapSize;

		return true;
	}

	/// <summary>The person and the cell's object under an arbitrary point of the window, each nought for none.</summary>
	public static (int Person, int Object) ThingsAt( float screenX, float screenY )
	{
		var answer = Resolve( new Vector2( screenX, screenY ) );

		return (answer.Person, answer.Object);
	}

	/// <summary>
	/// The direction the pointer points, in world space.
	///
	/// <para>
	/// The basis is rebuilt against the world's up rather than taken from the camera's rotation - see
	/// the class remarks for why that is not a tidying. <see cref="Camera.HorizontalTangent"/> is how
	/// far the frustum reaches sideways one unit ahead, and it already carries the whole of the
	/// aspect-ratio rule this game applies to windows that are not 4:3, so the vertical reach is simply
	/// that over the aspect.
	/// </para>
	/// </summary>
	private static Vector3 RayThrough( Vector2 screen )
	{
		var forward = Camera.Rotation.Forward.Normal;

		// The same handedness the engine's own constants use: facing +X with Z up, this gives
		// (0,-1,0), which is exactly Vector3.Right.
		var right = Cross( forward, Vector3.Up ).Normal;
		var up = Cross( right, forward ).Normal;

		// Pixels to the -1..1 box. Down the screen is positive in pixels and negative here, which is
		// the flip every projection makes.
		var acrossFraction = (2f * (screen.X / MathF.Max( Screen.Width, 1f ))) - 1f;
		var downFraction = 1f - (2f * (screen.Y / MathF.Max( Screen.Height, 1f )));

		var across = Camera.HorizontalTangent;
		var vertical = across / MathF.Max( Screen.Aspect, 0.01f );

		return (forward + (right * (acrossFraction * across)) + (up * (downFraction * vertical))).Normal;
	}

	private static Vector3 Cross( Vector3 a, Vector3 b )
		=> new(
			(a.Y * b.Z) - (a.Z * b.Y),
			(a.Z * b.X) - (a.X * b.Z),
			(a.X * b.Y) - (a.Y * b.X) );

	/// <summary>
	/// Where the ray's line meets the land, by the original's two planes a cell (<see cref="ParkPickGround"/>). Answers false where
	/// none of the heightfield's cells the line covers is met - past its last column or row, over the sea before its first, or straight
	/// up: a ray aimed above the horizon meets the land behind the eye, as the original's does.
	/// </summary>
	internal static bool MeetGround( HeightfieldFile field, Vector3 from, Vector3 direction, out Vector3 hit )
		=> ParkPickGround.Meet( field, from, direction, out hit );

	/// <summary>
	/// A one-line summary for the debug console. The cell is printed both packed and as a pair,
	/// because the packed form is what every engine-side note is written in and the pair is what a
	/// person reads.
	/// </summary>
	/// <remarks>
	/// <b>It reports the pointer it READ, not only the answer.</b> A probe that warps the pointer and
	/// then asks for a cell cannot tell "the ray is wrong" from "the game never saw the mouse move" -
	/// both look like an answer that will not follow the cursor. Printing the position the picker
	/// actually used separates them in one line.
	/// </remarks>
	public static string State()
	{
		var mouse = Input.Mouse.Position;

		return Describe( new Answer( Cell, WorldPoint, PersonUnderCursor, ObjectUnderCursor, Latched, PointerMet, Marched ),
			$"mouse ({mouse.X:F0},{mouse.Y:F0})" );
	}

	private static string Describe( Answer answer, string asked )
	{
		var size = $"of {Screen.Width:F0}x{Screen.Height:F0}";

		if ( answer.Cell <= 0 )
			return $"pick: no cell yet - {asked} {size}";

		var cellX = (answer.Cell - 1) % ParkWorld.MapSize;
		var cellY = (answer.Cell - 1) / ParkWorld.MapSize;
		var occupant = answer.Person != 0 ? $"thing {answer.Person}" : "nothing";
		var placed = answer.Object != 0 ? $"thing {answer.Object}" : "nothing";

		return $"pick: cell ({cellX},{cellY}) packed {answer.Cell} " +
			$"world ({answer.World.X:F1},{answer.World.Y:F1},{answer.World.Z:F1}) " +
			$"exact ({answer.World.X:F3},{answer.World.Y:F3},{answer.World.Z:F3}) " +
			$"standing {occupant} object {placed} latched {answer.Latched}{(answer.Met ? "" : " (nothing met: the last cell stands)")}" +
			$"{(answer.Marched != 0 ? $" marched thing {answer.Marched}" : "")} - " +
			$"{asked} {size}";
	}
}
