namespace OpenTPW;

/// <summary>
/// The park's track rides as the running game holds them - the original's table of 64 entries of <c>0xd0</c> bytes
/// (allocated at <c>0x005443d2</c>): each the ride's <c>Bumper.BumperType</c> and its list of track sections, found
/// by the handle an object carries at <c>+0x28</c>, the slot in the low byte and the BumperType above it.
/// <c>docs/exe/ride-operation.md</c>, "A coaster's, a track ride's and an upgraded ride's excitement".
///
/// <para>
/// <b>Seeded from the save</b> as its loader does it (<c>FUN_00543560</c>), record by record: each ride in the slot its
/// saved handle names, each section appended to its ride's list (<see cref="Lay"/>), each car and listed peep put
/// back by <see cref="ParkBumperCars"/>. <b>A purchase takes the first
/// free slot</b> (<see cref="Take"/>) and lays no section, and <b>a sale frees it</b> (<see cref="Free"/>); a move is a
/// sale and a purchase, as the original's is. A kart or water ride bought here lays its station and mouths on the track
/// grid (<see cref="ParkTrackPieces.LayStation"/>) and no section; the circuit's walk lays its sections, each with its
/// collision objects and buoy, when the track tool's loop closes (<see cref="ParkTrackTool.WalkCircuit"/>). The grid's
/// and start's buoys a kart or water ride's record lays (<c>Bumper_LayRide</c>) are not laid here (Q588).
/// </para>
/// </summary>
public sealed class ParkTrackRideTable
{
	/// <summary>How many rides the table holds - <c>FUN_00545890</c>'s search stops at <c>0x40</c> (<c>0x005458ea</c>).</summary>
	public const int Slots = 64;

	/// <summary>How many sections all the rides' lists hold between them - the pool at <c>DAT_00877b80</c>.</summary>
	public const int SectionPool = 0x400;

	/// <summary>The BumperTypes a template exists for: the 14 at <c>0x764178</c>, <c>0xd0</c> apart, -1 first.</summary>
	private const int LeastBumperType = -14;

	/// <summary>How many collision objects all the sections hold between them - the pool at <c>DAT_00877b70</c>, 2048 of <c>0x20</c>.</summary>
	public const int ObjectPool = 0x800;

	/// <summary>A section's type dword's flag for the station's two: it lays no buoy (<c>0x0054bae5</c>).</summary>
	public const int StationSection = 0x10000;

	/// <summary>
	/// A collision object of a section (<c>DAT_00877b70</c>'s <c>0x20</c>): its flags, its middle, and its size, a
	/// circle's radius in both or a rectangle's half its length along x and along z (<c>+0xc</c>, <c>+0x10</c>).
	/// Flags: 1 taken, 2 open below x, 4 open above x, 8 open above z, <c>0x10</c> open below z (a bend is the quarter
	/// with the other two), <c>0x20</c> a rectangle, <c>0x40</c> an obstacle, <c>0x80</c> a water crossing's round,
	/// <c>0x100</c> an add-on's straight.
	/// </summary>
	public readonly record struct CollisionObject( int Flags, int X, int Z, int RadiusX, int RadiusZ );

	/// <summary>
	/// A buoy on a ride's list (<c>DAT_00877b78</c>'s <c>0x1c</c>): its flags (1 taken, 2 a grid place, 4 the start),
	/// its place, and the object of its section a car must be in to have passed it (<c>+0x18</c>), none for the grid's.
	/// </summary>
	public readonly record struct Buoy( int Flags, int X, int Z, CollisionObject? Object );

	/// <summary>
	/// One section on a ride's list: its type dword, the cell it lies on in map cells times <c>0xc00</c>, and its
	/// collision objects, the last made first as the original's list holds them (<c>+0x1c</c>, linked through <c>+0x14</c>).
	/// </summary>
	private readonly record struct Section( int Type, int X, int Y, IReadOnlyList<CollisionObject> Objects );

	/// <summary>Each slot's <c>entry[0]</c>, the BumperType, nought where the slot is free.</summary>
	private readonly int[] _bumperType = new int[Slots];

	/// <summary>Each slot's sections in circuit order (<c>+0xbc</c>, linked through <c>+0x24</c>).</summary>
	private readonly List<Section>[] _sections = new List<Section>[Slots];

	/// <summary>Each slot's buoys in the order laid (<c>+0xa8</c>, linked through <c>+0x14</c>; the count <c>+0xac</c>).</summary>
	private readonly List<Buoy>[] _buoys = new List<Buoy>[Slots];

	/// <summary>The bumper family's records and the cars every ride shares - see <see cref="ParkBumperCars"/>.</summary>
	public ParkBumperCars Cars { get; }

	/// <param name="saved">The save's track-rides module, or null for a park with none.</param>
	public ParkTrackRideTable( ParkTrackRides? saved = null )
	{
		Cars = new ParkBumperCars( this );

		for ( var slot = 0; slot < Slots; ++slot )
		{
			_sections[slot] = [];
			_buoys[slot] = [];
		}

		if ( saved == null )
			return;

		// A rider record with no car of its ride before it: the loader hangs it on the record's last car, which is none.
		for ( var rider = 0; rider < saved.StrayRiders; ++rider )
			Unimplemented.Report( "SAVED_TRACK_RIDER_WITH_NO_CAR" );

		// The loader reads the records in file order, so a section, a car or a listed peep before its ride's record
		// finds no ride.
		int section = 0, car = 0, listed = 0;

		for ( var ride = 0; ride <= saved.Rides.Count; ++ride )
		{
			for ( ; section < saved.Sections.Count && saved.Sections[section].RidesBefore == ride; ++section )
			{
				var laid = saved.Sections[section];

				Lay( laid.Handle, laid.Type, laid.X, laid.Y );
			}

			for ( ; car < saved.Cars.Count && saved.Cars[car].RidesBefore == ride; ++car )
				Cars.Restore( saved.Cars[car] );

			for ( ; listed < saved.Listed.Count && saved.Listed[listed].RidesBefore == ride; ++listed )
				Cars.Restore( saved.Listed[listed] );

			if ( ride < saved.Rides.Count )
				Seat( saved.Rides[ride] );
		}
	}

	/// <summary>
	/// A saved ride put back in the slot its handle names - <c>FUN_00545890</c> handed the handle (<c>0x00543725</c>),
	/// which takes the slot without a free test and fills it from the BumperType's template, an empty list, its arena
	/// laid round the saved place; the record's performance, mesh words, duration and state are then the file's
	/// (<see cref="ParkBumperCars.Restore(SavedTrackRide)"/>).
	/// </summary>
	private void Seat( SavedTrackRide ride )
	{
		var slot = ride.Handle & 0xff;
		var bumperType = ride.Handle >> 8;

		// Past the table's end, or a BumperType with no template, the original writes where no entry is.
		if ( slot >= Slots || bumperType is < LeastBumperType or > -1 )
		{
			Unimplemented.Report( "SAVED_TRACK_RIDE_OUTSIDE_THE_TABLE" );
			return;
		}

		_bumperType[slot] = bumperType;
		_sections[slot].Clear();
		_buoys[slot].Clear();
		Cars.Open( ride.Handle, bumperType );
		Cars.Place( ride.Handle, ride.X + (ParkBumperCars.CellUnits / 2), ride.Y + (ParkBumperCars.CellUnits / 2) );
		Cars.Restore( ride );
	}

	/// <summary>
	/// A section appended to its ride's list - <c>TrackRide_AddSection</c> (<c>FUN_0054b2f0</c>), which the loader calls
	/// for each saved one (<c>0x00544061</c>) and the circuit's walk for each cell (<see cref="ParkTrackTool.WalkCircuit"/>).
	/// It refuses a stale handle, a cell the ride's list already holds ("Duplicate track section - IGNORE") and a full
	/// pool; then makes the section's collision objects by its type (<see cref="ObjectsFor"/>) and, but on the station's
	/// two, its buoy, appended to the ride's (<see cref="BuoyFor"/>). <c>docs/exe/park.md</c>, "A section".
	/// </summary>
	/// <returns>Whether the section was laid.</returns>
	internal bool Lay( int handle, int type, int x, int y )
	{
		if ( !Holds( handle ) )
		{
			Log.Info( $"Track rides: a section for 0x{handle:x8}, which the table does not hold, is not laid" );
			return false;
		}

		var slot = handle & 0xff;
		var list = _sections[slot];

		if ( list.Exists( section => section.X == x && section.Y == y ) )
		{
			Log.Info( $"Track rides: 0x{handle:x8} already has a section at ({x / 0xc00},{y / 0xc00}) - duplicate, ignored" );
			return false;
		}

		if ( _sections.Sum( held => held.Count ) >= SectionPool )
		{
			Log.Info( $"Track rides: every one of the {SectionPool} sections is laid, so 0x{handle:x8}'s is not" );
			return false;
		}

		var objects = ObjectsFor( _bumperType[slot], type, x, y );

		// The original takes each object from its pool and writes through what it answers, so a section past the
		// pool's 2048 stops it ("Out of Collision Objects"; 0x0054b1e0): here the section is laid with none. The pool
		// here holds the track's objects alone; the original's also holds a bumper arena's (+0xc0).
		if ( objects is { Count: > 0 } && _sections.Sum( held => held.Sum( section => section.Objects.Count ) ) + objects.Count > ObjectPool )
		{
			Unimplemented.Report( "TRACK_SECTION_OUT_OF_OBJECTS" );
			objects = [];
		}

		// A type outside 1 to 13 makes no object, and its buoy is tied to none, through which the original writes
		// (0x0054bb49); the station's two make no buoy, so theirs is safe.
		if ( objects == null )
		{
			if ( (type & StationSection) == 0 )
				Unimplemented.Report( "TRACK_SECTION_UNKNOWN_TYPE" );

			objects = [];
		}

		list.Add( new Section( type, x, y, objects ) );

		if ( (type & StationSection) != 0 || objects.Count == 0 )
			return true;

		// The buoy, its object the section's first made, the last of its list. The original's pool of 1024 refuses
		// one more ("Out of Buoys", 0x0054b170) and keeps the section; it also holds the Hot Pot's rings and the grid's
		// buoys (Q588), which are not laid into it here, so the sections' 1024 cannot fill it alone.
		var (bx, bz) = BuoyFor( type, x, y );
		_buoys[slot].Add( new Buoy( 1, bx, bz, objects[^1] ) );

		return true;
	}

	/// <summary>
	/// A section's collision objects as <c>TrackRide_AddSection</c> makes them (<c>0x0054b42a</c>..<c>0x0054bae5</c>), the
	/// last made first, or null for a type outside 1 to 13, which makes none. <c>w</c> is the template's <c>+0xc</c>
	/// and <c>r</c> the car's radius (<c>+0x8</c>), a cell 3072:
	/// <list type="bullet">
	/// <item>A bend (1 to 8; 1 and 5 the same, and so on) a quarter about the corner it turns round, radius
	/// <c>3072 + w - r</c>, open on two sides, and the same quarter of radius <c>3072 - w + r</c> an obstacle.</item>
	/// <item>A straight (9, 12 east-west; 10, 13 north-south; 12 and 13 an add-on's, <c>0x100</c>) one rectangle at its
	/// middle, 3104 along and <c>w - r</c> across.</item>
	/// <item>A crossing (11) a round at its middle, radius <c>ftol( sqrt( (w - r)² + 3072² ) )</c>, <c>0x80</c> in the
	/// water family, and an obstacle quarter of radius <c>3072 - w + r</c> at each corner.</item>
	/// </list>
	/// </summary>
	/// <returns>The objects, or null where the template is not known here, counted.</returns>
	public static IReadOnlyList<CollisionObject>? ObjectsFor( int bumperType, int type, int x, int y )
	{
		if ( (type & 0xffff) is < 1 or > 13 )
			return null;

		// The templates' r (+0x8) and w (+0xc) at 0x764178: the karts' 900 and 2048, the water ride's 1024 and 1281.
		(int R, int W)? template = bumperType switch
		{
			-4 => (900, 2048),
			-5 => (1024, 1281),
			_ => null
		};

		if ( template is not var (r, w) )
		{
			Unimplemented.Report( $"TRACK_SECTION_TEMPLATE_{-bumperType}" );
			return [];
		}

		const int Taken = 0x1f;
		const int Cell = 0xc00;
		var outer = Cell + w - r;
		var inner = Cell - w + r;

		// A bend's corner and the two open sides it clears (2 below x, 4 above x, 8 above z, 0x10 below z).
		(int Dx, int Dz, int Open)? bend = (type & 0xffff) switch
		{
			1 or 5 => (2 * Cell, 0, 0x14),
			2 or 6 => (0, 0, 0x12),
			3 or 7 => (0, 2 * Cell, 0x0a),
			4 or 8 => (2 * Cell, 2 * Cell, 0x0c),
			_ => null
		};

		if ( bend is var (dx, dz, open) )
			return
			[
				new CollisionObject( (Taken & ~open) | 0x40, x + dx, y + dz, inner, inner ),
				new CollisionObject( Taken & ~open, x + dx, y + dz, outer, outer )
			];

		var addOn = (type & 0xffff) is 12 or 13 ? 0x100 : 0;

		switch ( type & 0xffff )
		{
			case 9 or 12:
				return [new CollisionObject( Taken | addOn | 0x20, x + Cell, y + Cell, 0xc20, w - r )];

			case 10 or 13:
				return [new CollisionObject( Taken | addOn | 0x20, x + Cell, y + Cell, w - r, 0xc20 )];
		}

		// A crossing: its round first made, then the corners as the bends' obstacles, each put at the list's head.
		var round = (int)Math.Sqrt( (double)((w - r) * (w - r) + Cell * Cell) );
		var water = bumperType is -5 or -8 or -12 or -13 ? 0x80 : 0;

		return
		[
			new CollisionObject( (Taken & ~0x0c) | 0x40, x + 2 * Cell, y + 2 * Cell, inner, inner ),
			new CollisionObject( (Taken & ~0x0a) | 0x40, x, y + 2 * Cell, inner, inner ),
			new CollisionObject( (Taken & ~0x12) | 0x40, x, y, inner, inner ),
			new CollisionObject( (Taken & ~0x14) | 0x40, x + 2 * Cell, y, inner, inner ),
			new CollisionObject( Taken | water, x + Cell, y + Cell, round, round )
		];
	}

	/// <summary>
	/// Where a section's buoy is (<c>Bumper_MakeBuoy</c> from <c>0x0054bafd</c>): a bend's on its arc's middle, 0xf84 or
	/// 0x87c in from the section's sides by the corner it turns round, a straight's or crossing's at its middle.
	/// </summary>
	public static (int X, int Z) BuoyFor( int type, int x, int y )
	{
		var (dx, dz) = (type & 0xffff) switch
		{
			1 or 5 => (0xf84, 0xf84),
			2 or 6 => (0x87c, 0xf84),
			3 or 7 => (0x87c, 0x87c),
			4 or 8 => (0xf84, 0x87c),
			_ => (0xc00, 0xc00)
		};

		return (x + dx, y + 0x1800 - dz);
	}

	/// <summary>
	/// Lets a ride's sections go before the circuit's walk lays them again - <c>TrackRide_ClearSections</c>
	/// (<c>FUN_00545450</c>): nothing for a stale handle, else its cars removed (<c>FUN_0054ae50</c>), the ride closed
	/// (<c>BUMP 6</c>), the buoys not flagged 2 or 4 freed, and every section with its collision objects. Every buoy
	/// here is a section's, flagged 1: the grid's and the start's, flagged 2 and 4, which the original keeps, are laid
	/// by the ride's record (<c>Bumper_LayRide</c>, Q588).
	/// </summary>
	public void ClearSections( int handle )
	{
		if ( !Holds( handle ) )
			return;

		Cars.RemoveCarsOf( handle );
		Cars.Shut( handle );
		_buoys[handle & 0xff].Clear();
		_sections[handle & 0xff].Clear();
	}

	/// <summary>How many section buoys the track rides hold between them; the original's count (<c>DAT_00877b7c</c>) also holds the bumper rings (ParkBumperCars) and the grid's (Q588).</summary>
	public int BuoysInUse => _buoys.Sum( held => held.Count );

	/// <summary>A ride's buoys in the order laid; none for a stale handle.</summary>
	public IReadOnlyList<Buoy> BuoysOf( int handle ) => Holds( handle ) ? _buoys[handle & 0xff].ToArray() : [];

	/// <summary>The collision objects of a ride's section, by its place in the list, the last made first; none for a stale handle.</summary>
	public IReadOnlyList<CollisionObject> ObjectsOf( int handle, int section )
		=> Holds( handle ) && section >= 0 && section < _sections[handle & 0xff].Count ? _sections[handle & 0xff][section].Objects : [];

	/// <summary>A ride's sections in circuit order, each its type dword and cell times <c>0xc00</c>; none for a stale handle.</summary>
	public IReadOnlyList<(int Type, int X, int Y)> SectionsOf( int handle )
		=> Holds( handle ) ? _sections[handle & 0xff].ConvertAll( section => (section.Type, section.X, section.Y) ) : [];

	/// <summary>How many slots hold a ride.</summary>
	public int Count => _bumperType.Count( bumperType => bumperType != 0 );

	/// <summary>
	/// A slot for a ride being placed - <c>FUN_00545890</c> as the placer calls it (<c>0x00529e4d</c>): the first
	/// whose <c>entry[0]</c> is nought, filled from the BumperType's template, which lays no section. The handle is
	/// <c>slot | BumperType &lt;&lt; 8</c>, never nought, since every BumperType is negative.
	/// </summary>
	/// <returns>The handle, or nought with every slot taken, which is counted.</returns>
	public int Take( int bumperType )
	{
		for ( var slot = 0; slot < Slots; ++slot )
		{
			if ( _bumperType[slot] != 0 )
				continue;

			_bumperType[slot] = bumperType;
			_sections[slot].Clear();
			_buoys[slot].Clear();
			Cars.Open( HandleOf( slot ), bumperType );

			return HandleOf( slot );
		}

		// A 65th: the original reads through a null entry (0x00546225) and does not survive it.
		Unimplemented.Report( "TRACK_RIDE_TABLE_FULL" );

		return 0;
	}

	/// <summary>
	/// Lets a ride's slot go - <c>FUN_00545610</c>, which the demolisher calls for any object with a handle
	/// (<c>0x00528584</c>): nothing for a stale handle, else its cars go, the ride closes, every buoy goes, its sections
	/// go with their objects and <c>entry[0]</c> is nought.
	/// </summary>
	public void Free( int handle )
	{
		if ( !Holds( handle ) )
			return;

		var slot = handle & 0xff;

		Cars.Close( handle );

		_bumperType[slot] = 0;
		_sections[slot].Clear();
		_buoys[slot].Clear();
	}

	/// <summary>
	/// Whether the handle names a ride the table holds - the test <c>FUN_00545310</c> and <c>FUN_00545610</c> open
	/// with (<c>0x0054536d</c>, <c>0x0054566e</c>): the handle equals its slot's <c>slot | entry[0] &lt;&lt; 8</c>.
	/// </summary>
	public bool Holds( int handle )
	{
		var slot = handle & 0xff;

		return slot < Slots && handle == HandleOf( slot );
	}

	private int HandleOf( int slot ) => slot | (_bumperType[slot] << 8);

	/// <summary>What <c>FUN_00545310</c> answers of a ride's track: half its bends, its longest straight run and half its crossings.</summary>
	/// <param name="Sections">How many sections the list holds - the function's return, half of the two passes' visits.</param>
	public readonly record struct Layout( int HalfBends, int Longest, int HalfCrossings, int Sections );

	/// <summary>
	/// <c>FUN_00545310</c> of the ride a handle names, or null for a stale handle, which writes nothing.
	/// </summary>
	public Layout? LayoutOf( int handle )
		=> Holds( handle ) ? Walk( _sections[handle & 0xff].ConvertAll( section => section.Type ) ) : null;

	/// <summary>
	/// <c>FUN_00545310</c>'s walk of a section list (<c>0x00545375</c>..<c>0x005453e1</c>), by each section's type,
	/// its low 16 bits: 9, 10, 12 and 13 lengthen the run, 11 lengthens it and counts a crossing, and anything else
	/// ends it, keeping the longest, and counts a bend.
	/// </summary>
	/// <remarks>
	/// <b>It walks the list twice without a reset</b> (<c>0x0054538d</c>), so the bends and crossings are counted
	/// twice and halved, and a run can carry from the tail into the head: the longest is round the list as a circle.
	/// A run is kept only when a bend ends it, so a list with no bend answers nought. The fifth count, of the
	/// straights with an add-on (12 and 13 whose <c>+8</c> is nought), is left out: no caller reads it.
	/// </remarks>
	public static Layout Walk( IReadOnlyList<int> sections )
	{
		int visited = 0, run = 0, crossings = 0, longest = 0, bends = 0;

		for ( var pass = 0; pass < 2; ++pass )
		{
			foreach ( var section in sections )
			{
				switch ( section & 0xffff )
				{
					case 9 or 10 or 12 or 13:
						++run;
						break;

					case 11:
						++run;
						++crossings;
						break;

					default:
						if ( run > longest )
							longest = run;

						run = 0;
						++bends;
						break;
				}

				++visited;
			}
		}

		return new Layout( bends / 2, longest, crossings / 2, visited / 2 );
	}
}
