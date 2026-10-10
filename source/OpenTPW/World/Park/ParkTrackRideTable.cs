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
/// sale and a purchase, as the original's is. Laying track is not built (<c>PLACED_TRACK_RIDE_FIRST_TRACK_CELLS</c>),
/// so a ride bought here never has a section.
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

	/// <summary>One section on a ride's list: its type dword, and the cell it lies on in map cells times <c>0xc00</c>.</summary>
	private readonly record struct Section( int Type, int X, int Y );

	/// <summary>Each slot's <c>entry[0]</c>, the BumperType, nought where the slot is free.</summary>
	private readonly int[] _bumperType = new int[Slots];

	/// <summary>Each slot's sections in circuit order (<c>+0xbc</c>, linked through <c>+0x24</c>).</summary>
	private readonly List<Section>[] _sections = new List<Section>[Slots];

	/// <summary>The bumper family's records and the cars every ride shares - see <see cref="ParkBumperCars"/>.</summary>
	public ParkBumperCars Cars { get; }

	/// <param name="saved">The save's track-rides module, or null for a park with none.</param>
	public ParkTrackRideTable( ParkTrackRides? saved = null )
	{
		Cars = new ParkBumperCars( this );

		for ( var slot = 0; slot < Slots; ++slot )
			_sections[slot] = [];

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
		Cars.Open( ride.Handle, bumperType );
		Cars.Place( ride.Handle, ride.X + (ParkBumperCars.CellUnits / 2), ride.Y + (ParkBumperCars.CellUnits / 2) );
		Cars.Restore( ride );
	}

	/// <summary>
	/// A section appended to its ride's list - <c>FUN_0054b2f0</c>, which the loader calls for each saved one
	/// (<c>0x00544061</c>). It refuses a stale handle, a cell the ride's list already holds ("Duplicate track section
	/// - IGNORE") and a full pool.
	/// </summary>
	/// <returns>Whether the section was laid.</returns>
	internal bool Lay( int handle, int type, int x, int y )
	{
		if ( !Holds( handle ) )
		{
			Log.Info( $"Track rides: a section for 0x{handle:x8}, which the table does not hold, is not laid" );
			return false;
		}

		var list = _sections[handle & 0xff];

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

		list.Add( new Section( type, x, y ) );

		return true;
	}

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
			Cars.Open( HandleOf( slot ), bumperType );

			return HandleOf( slot );
		}

		// A 65th: the original reads through a null entry (0x00546225) and does not survive it.
		Unimplemented.Report( "TRACK_RIDE_TABLE_FULL" );

		return 0;
	}

	/// <summary>
	/// Lets a ride's slot go - <c>FUN_00545610</c>, which the demolisher calls for any object with a handle
	/// (<c>0x00528584</c>): nothing for a stale handle, else its cars go, the ride closes, its sections go and
	/// <c>entry[0]</c> is nought.
	/// </summary>
	public void Free( int handle )
	{
		if ( !Holds( handle ) )
			return;

		var slot = handle & 0xff;

		Cars.Close( handle );

		_bumperType[slot] = 0;
		_sections[slot].Clear();
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
