namespace OpenTPW;

/// <summary>
/// The World block of a park save: the first and by far the largest of the seventeen modules inside a
/// <c>.TPWI</c>, and the one that says what stands in the park and where.
///
/// <para>
/// The payload is a serialised <b>memory image</b> rather than a portable format - it saves live heap
/// pointers verbatim - so nothing in it can be found by searching. Every offset below is reached by
/// walking, and the walk is what makes the result trustworthy: the original writes a four-character tag
/// after each module and checks it on the way back in, so a walk that ends exactly on the next tag has
/// agreed with the game about every single byte in between. This one ends on <c>WRLD</c>, which is
/// stored little-endian and therefore reads <c>DLRW</c> in a dump - see <see cref="Trailer"/>.
/// </para>
///
/// <para>
/// <b>The map is stepped over, not decoded.</b> Between the header and the thing list sit 16,384 cells -
/// a 128x128 grid - of litter, hoardings and tile data, about 1.3MB of the 1.5MB block. None of it is
/// wanted here and all that is needed is its length, so each cell is measured and skipped. That is the
/// whole reason this reader is short.
/// </para>
///
/// <para>
/// What it is for: the park's placed objects. Each is a thing of model 3 carrying the catalogue id of
/// the item it is - the <c>Info.Id</c> out of that item's own <c>.sam</c> - and the cell it stands on.
/// The park's <i>fixed</i> items are in here too, but carry no position: see <see cref="ParkFixedItems"/>
/// for why, and <see cref="CatalogueObject.IsPlaced"/> for how they read.
/// </para>
/// </summary>
public sealed class ParkWorld
{
	/// <summary>
	/// One object standing in the park - a shop, a ride, a bin, a fountain.
	///
	/// <para>
	/// The position is in 256ths of a cell, which was settled by sweeping the shift and asking which one
	/// puts every value somewhere legal: at <c>&gt;&gt; 8</c> all of them land inside the map's own
	/// 96x85 extent and most land on a meaningful attribute, and at <c>&gt;&gt; 7</c> several fall off
	/// the map altogether.
	/// </para>
	/// </summary>
	public readonly record struct CatalogueObject( int ThingId, int CatalogueId, int RawX, int RawY, int Angle )
	{
		/// <summary>
		/// What both coordinates read when a thing has no place on the map. It is the raw value, not a
		/// cell: 128 is half a cell, so a sentinel read as a cell would look like a real object sitting
		/// at the origin.
		/// </summary>
		public const int Unplaced = 128;

		/// <summary>
		/// Whether this object stands anywhere. The three fixed items - the gate, the traffic lights and
		/// the bus - are saved as objects like everything else but carry the sentinel, because their
		/// positions are baked into their models rather than into the save.
		/// </summary>
		public bool IsPlaced => RawX != Unplaced || RawY != Unplaced;

		public int CellX => RawX >> 8;

		public int CellY => RawY >> 8;
	}

	/// <summary>Every catalogue object the walk found, placed or not, in the order the file lists them.</summary>
	public IReadOnlyList<CatalogueObject> Objects => _objects;

	private readonly List<CatalogueObject> _objects = [];

	/// <summary>
	/// The thing that <i>is</i> the park gate, and the one that is the traffic lights. These are handles,
	/// not positions and not list indices: the original compares them against a thing's own id with
	/// <c>==</c>, so eleven means "the thing whose id is 11" rather than "the eleventh thing".
	/// </summary>
	public int ParkGates { get; private set; }

	public int TrafficLights { get; private set; }

	/// <summary>The id of the first thing on the object list, the same kind of handle as the two above.</summary>
	public int FirstObject { get; private set; }

	public int RandomSeed { get; private set; }

	public int Weather { get; private set; }

	/// <summary>How many things the walk stepped through, of every model - people and managers included.</summary>
	public int ThingCount { get; private set; }

	/// <summary>
	/// Whether the walk ended exactly on the <c>WRLD</c> trailer. This is the check that matters: the
	/// block is 1.5MB of variable-length records, so landing on the next module's tag to the byte means
	/// every record size in between was right. False does not make the objects wrong - they are read
	/// long before the end - but it does mean something after them was not understood.
	/// </summary>
	public bool ClosedOnTrailer { get; private set; }

	/// <summary>What stopped the walk early, or null if nothing did.</summary>
	public string? Problem { get; private set; }

	/// <summary>
	/// The tag the block is followed by, as it appears in the file. The original writes these as
	/// little-endian dwords, so all seventeen of them read backwards in a byte dump - which is why a
	/// search for "WRLD" finds nothing and one for "DLRW" finds it immediately.
	/// </summary>
	public const string Trailer = "DLRW";

	/// <summary>
	/// How big a thing's record is on disk, by its model number.
	///
	/// <para>
	/// Models 1 and 3 to 8 are people and objects, and their sizes were <i>derived</i>: each reader
	/// declares every field before reading it, so running the original's own code under emulation and
	/// logging that one declaration gives the record layout by name, in order, including everything the
	/// nested readers contribute. A guest comes to 525 bytes and a handyman 505, each with the 8-byte
	/// prefix on top.
	/// </para>
	/// <para>
	/// Models 9 and 11 to 19 are the park's singleton managers - one ride system, one advisor, one
	/// tagging system - and these sizes are <b>measured from the one park the game ships</b>, because
	/// their readers have not been read. Several are certainly not fixed in general: model 13 is 73,544
	/// bytes of what is very likely another gated grid. They are here only so the walk can reach the
	/// trailer and prove itself; every object is read before the first of them.
	/// </para>
	/// </summary>
	private static readonly Dictionary<int, int> RecordSizes = new()
	{
		[1] = 533,   // guest
		[3] = 1099,  // catalogue object - a shop, ride, or piece of scenery
		[4] = 511,   // mechanic
		[5] = 513,   // handyman
		[6] = 509,   // entertainer
		[7] = 511,   // guard
		[8] = 509,   // researcher
		[9] = 103,   // the strike system
		[10] = 18,   // a bare map object
		[11] = 5846,
		[12] = 16,
		[13] = 73544,
		[14] = 4190,
		[15] = 99,
		[16] = 300,
		[17] = 16,
		[19] = 937
	};

	/// <summary>
	/// The World header, in the order the original reads it. The names are its own: each field is
	/// announced to a logging call that the release build compiles away to <c>return 0</c>, so the names
	/// never reach the file and the header cannot be found by searching for them - but they do survive in
	/// the executable, which is how the field list was recovered.
	/// </summary>
	private static readonly int[] HeaderFieldSizes =
	[
		4, // version
		2, 2, 2, // mArrivalVehicle_Size1..3
		2, // mBankAccount
		2, // mCurrentArrivalVehicle
		4, // mGameTick
		2, // mMechanicHQ
		2, // mParkAnalyser
		4, // mParkClosed
		4, // mNumberOfVisitorsToDate
		2, // mParkGates
		2, // mTrafficLights
		4, // mRandomSeed
		2, // mResearchLab
		2, // mStaffHQ
		2, // mTagSystem
		2, // mUIMsgReceiver
		2, // mWeather
		4, // mWorldState
		2, 2, 2, 2, 2, // mFirstHandyman, Mechanic, Entertainer, Guard, Researcher
		2  // mFirstObject
	];

	// Where the fields this class keeps sit in the list above, so the reader can name them rather than
	// counting along it.
	private const int ParkGatesField = 11;
	private const int TrafficLightsField = 12;
	private const int RandomSeedField = 13;
	private const int WeatherField = 18;
	private const int FirstObjectField = 25;

	/// <summary>
	/// Fixed-size tables between the header and the map. Each is an array with a compiled-in bound rather
	/// than a count in the file - <c>mNumObjectControls</c> is written <i>after</i> its array, which is
	/// what says the array's length is not read from anywhere.
	/// </summary>
	private const int ObjectControls = 150;

	private const int ObjectControlSize = 32;

	/// <summary>
	/// A pool of 32 twenty-byte records - type, name, pay grade, sub-type, valid, on-pointer, time
	/// signature and timeout - followed by a tail of arrival and clock fields that comes to 76 bytes:
	/// five people-per-category counts with their stop flags (25), a time signature, a staff-pool flag,
	/// two eight-byte timestamps, the month and day last updated, the funny-time rate, the arrival rate,
	/// another time signature, the target vehicle capacity, the people on the bus, and two flags.
	/// </summary>
	private const int PoolRecords = 32;

	private const int PoolRecordSize = 20;

	private const int ArrivalTailSize = 76;

	/// <summary>The map between the tables and the thing list - a 128x128 grid, whatever the park's own size is.</summary>
	private const int MapCells = 128 * 128;

	/// <summary>
	/// A cell opens with a status byte saying which of three optional sub-records follow it, one bit
	/// each. That is the original's own loop: it reads the byte, then a map cell if bit 0 is set, a track
	/// cell if bit 1 is, and an effects cell if bit 2 is.
	///
	/// <para>
	/// Only two combinations occur in the shipped park - 3 on 16,134 cells and 7 on the other 250, coming
	/// to 84 and 94 bytes - but adding the bits up costs nothing and reads the six combinations no
	/// shipped park happens to contain. A cell that is entirely default writes its status byte and
	/// nothing else, which is where the block's variable length comes from and why no fixed stride was
	/// ever going to walk it.
	/// </para>
	/// </summary>
	private const int MapCellSize = 52;

	private const int TrackCellSize = 31;

	private const int EffectsCellSize = 10;

	/// <summary>The bits of the status byte that mean something; any other one set is not understood.</summary>
	private const int KnownCellBits = 0x7;

	private readonly byte[] _data;
	private int _at;

	/// <summary>
	/// Walks the inflated payload of a <c>.TPWI</c> - what <see cref="SaveReader.ReadFile"/> hands back.
	///
	/// <para>
	/// A surprise stops the walk and is recorded in <see cref="Problem"/> rather than thrown, because
	/// everything worth having is read early: a park whose managers have changed shape should still show
	/// its shops.
	/// </para>
	/// </summary>
	public ParkWorld( byte[] inflatedPayload )
	{
		_data = inflatedPayload ?? throw new ArgumentNullException( nameof( inflatedPayload ) );

		try
		{
			Walk();
		}
		catch ( Exception e )
		{
			Problem ??= e.Message;
		}
	}

	private void Walk()
	{
		// The World block does not start at the beginning. An untagged ActionRec block is saved first -
		// a flag and then a recording, as a length and its bytes - so where World begins is derived from
		// that length rather than assumed. In the shipped park the length reads 1171, which puts World at
		// 0x49B; that the dword at offset 4 turned out to be a byte count is itself a check that could
		// have failed.
		_ = ReadInt32();
		var recording = ReadInt32();

		if ( recording < 0 || recording > _data.Length )
			throw new InvalidDataException( $"the ActionRec recording says it is {recording} bytes" );

		_at += recording;

		ReadHeader();

		// The fixed tables, stepped over: none of them says anything about what stands in the park.
		Skip( ObjectControls * ObjectControlSize );
		Skip( 4 );                                  // mNumObjectControls
		Skip( 2 );                                  // mPreviousSearchKey
		Skip( PoolRecords * PoolRecordSize );
		Skip( ArrivalTailSize );

		SkipMap();

		// The thing list proper. Its head is an id, not an offset - see ReadThings.
		var head = ReadInt32();

		ReadThings( head );

		ClosedOnTrailer = Problem == null
			&& _at + Trailer.Length <= _data.Length
			&& System.Text.Encoding.ASCII.GetString( _data, _at, Trailer.Length ) == Trailer;
	}

	private void ReadHeader()
	{
		var fields = new int[HeaderFieldSizes.Length];

		for ( var i = 0; i < HeaderFieldSizes.Length; ++i )
			fields[i] = HeaderFieldSizes[i] == 4 ? ReadInt32() : ReadUInt16();

		ParkGates = fields[ParkGatesField];
		TrafficLights = fields[TrafficLightsField];
		RandomSeed = fields[RandomSeedField];
		Weather = fields[WeatherField];
		FirstObject = fields[FirstObjectField];
	}

	/// <summary>
	/// Steps over the 128x128 map, measuring each cell by the status byte it opens with. This is the bulk
	/// of the block and none of it is read.
	/// </summary>
	private void SkipMap()
	{
		for ( var cell = 0; cell < MapCells; ++cell )
		{
			if ( _at >= _data.Length )
				throw new InvalidDataException( $"the map ran off the end of the payload at cell {cell}" );

			var status = _data[_at];

			if ( (status & ~KnownCellBits) != 0 )
				throw new InvalidDataException(
					$"map cell {cell} opens with status {status}, which sets a bit this does not know" );

			Skip( 1
				+ ((status & 1) != 0 ? MapCellSize : 0)
				+ ((status & 2) != 0 ? TrackCellSize : 0)
				+ ((status & 4) != 0 ? EffectsCellSize : 0) );
		}
	}

	/// <summary>
	/// Walks the things - every person, object and manager in the park - collecting the catalogue objects
	/// as it goes.
	///
	/// <para>
	/// The list is singly linked, and the link is the trap in it. Each record opens with a dword whose
	/// own name in the executable is <c>Used Thing Next</c>: it holds the id of the record that
	/// <i>follows</i>, so a thing's own id is the value stored in the one before it, and the first comes
	/// from the header's <c>Used Thing Head</c>. Reading it as the thing's own id instead is wrong in a
	/// way that still looks plausible - it is off by one everywhere, which turned the gate into the
	/// traffic lights.
	/// </para>
	/// <para>
	/// Two things say plainly that it is a next-pointer: the last record's is zero, a null terminator that
	/// no thing could have as an id; and the sequence is not monotonic - it runs 41, 40 ... 29, then 15,
	/// then 28 - which is what a list with something spliced into it looks like and what a counter cannot
	/// be. Read this way, eight separate handles out of the header land on objects that make sense.
	/// </para>
	/// </summary>
	private void ReadThings( int head )
	{
		var id = head;

		while ( true )
		{
			if ( _at + 8 > _data.Length )
			{
				Problem = "the thing list ran off the end of the payload";
				return;
			}

			var start = _at;
			var next = ReadInt32();
			var model = ReadInt32();

			if ( !RecordSizes.TryGetValue( model, out var size ) )
			{
				Problem = $"thing {id} is model {model}, which has no known size - the walk stopped there";
				return;
			}

			if ( model == CatalogueObjectModel )
				_objects.Add( ReadCatalogueObject( id, start ) );

			++ThingCount;

			_at = start + size;
			id = next;

			// Zero is the end of the list rather than a thing, so the record carrying it is the last one.
			if ( next == 0 )
				return;
		}
	}

	/// <summary>The model number of a thing that is a catalogue item rather than a person or a manager.</summary>
	private const int CatalogueObjectModel = 3;

	/// <summary>
	/// The head of every thing, which is the same for all of them - the map base the original gives each
	/// thing that has a place in the world - followed by what a catalogue object adds.
	///
	/// <para>
	/// <c>mX</c> and <c>mY</c> were unnamed in the decompiler and are named here because the executable's
	/// own string table says so: the two strings the base reader passes for those fields read exactly
	/// that. Everything after <c>mId</c> is left alone.
	/// </para>
	/// </summary>
	private CatalogueObject ReadCatalogueObject( int id, int start )
		=> new(
			ThingId: id,
			CatalogueId: ReadUInt16At( start + 20 ),   // mId - the item's Info.Id, from its own .sam
			RawX: ReadUInt16At( start + 8 ),           // mX, in 256ths of a cell
			RawY: ReadUInt16At( start + 10 ),          // mY
			Angle: ReadInt32At( start + 16 ) );        // mAngle, in degrees - 0, 90 or 270 in the shipped park

	private void Skip( int count )
	{
		if ( _at + count > _data.Length )
			throw new InvalidDataException( $"a {count}-byte field runs past the end of the payload" );

		_at += count;
	}

	private int ReadInt32()
	{
		var value = ReadInt32At( _at );
		_at += 4;
		return value;
	}

	private int ReadUInt16()
	{
		var value = ReadUInt16At( _at );
		_at += 2;
		return value;
	}

	private int ReadInt32At( int offset )
	{
		if ( offset + 4 > _data.Length )
			throw new InvalidDataException( $"a dword at 0x{offset:x} runs past the end of the payload" );

		return BitConverter.ToInt32( _data, offset );
	}

	private int ReadUInt16At( int offset )
	{
		if ( offset + 2 > _data.Length )
			throw new InvalidDataException( $"a word at 0x{offset:x} runs past the end of the payload" );

		return BitConverter.ToUInt16( _data, offset );
	}
}
