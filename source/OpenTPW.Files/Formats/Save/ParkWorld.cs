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
/// <b>The map is the bulk of it.</b> Between the header and the thing list sit 16,384 cells - a 128x128
/// grid - of litter, hoardings and tile data, about 1.3MB of the 1.5MB block. Each is measured by the
/// status byte it opens with, which is what makes the block variable length and why no fixed stride ever
/// walked it. See <see cref="MapCell"/> for what a cell says and <see cref="CellAt"/> for how to ask.
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
	/// One of the park's people: a guest, or one of the five kinds of staff.
	///
	/// <para>
	/// A person is not built from a model the way a shop is - they are a sprite - and the sprite they
	/// wear is <b>named</b> by <see cref="SpriteSlot"/> rather than worked out from what they are. See
	/// <see cref="Sprite"/> for why it has to be read back rather than chosen again.
	/// </para>
	/// <para>
	/// <b>The trap worth naming.</b> <c>+0x10</c> is where a catalogue object keeps its <c>mAngle</c>, and
	/// a person keeps their sprite slot there instead, so the two readers must never be pointed at each
	/// other's records. A person's facing is <see cref="Angle"/>, somewhere else entirely, and is not in
	/// degrees.
	/// </para>
	/// <para>
	/// <see cref="Guest"/> is what they were doing, and only a guest has it: the five kinds of staff
	/// carry a different block in the same place, and none of its fields is read here.
	/// </para>
	/// </summary>
	public readonly record struct Person(
		int ThingId, int Model, int RawX, int RawY, int SpriteSlot, int Angle, GuestState? Guest )
	{
		/// <inheritdoc cref="CatalogueObject.CellX"/>
		public int CellX => RawX >> 8;

		/// <inheritdoc cref="CatalogueObject.CellY"/>
		public int CellY => RawY >> 8;

		/// <summary>
		/// Which of eight ways this person faces.
		///
		/// <para>
		/// The stored angle is an <b>eleven-bit turn</b> - 2048 to the circle - and not the degrees a
		/// catalogue object keeps. The game biases it by <c>0x380</c> before taking the top three bits
		/// (<c>FUN_004fa030</c>): half an octant of rounding, then three octants of turn. Over the shipped
		/// park this reproduces, for all eighteen people, the octant the save separately stores on the
		/// sprite itself - and that is not a mostly-zero column agreeing with itself, because the
		/// eighteen land on five different octants.
		/// </para>
		/// </summary>
		public int Facing => ((Angle - 0x380) & 0x7ff) >> 8;
	}

	/// <summary>
	/// What a guest was doing when the park was saved: the behaviour they are in, the needs driving
	/// them, and what they are carrying.
	///
	/// <para>
	/// <b>These are not the offsets a decompiler shows, and the difference is not small.</b> A thing is
	/// written field by field in the order its reader asks for them, so a field's place in the file is
	/// the sum of the sizes before it and bears no relation to where it sits in memory: <c>mState</c> is
	/// at <c>+0x220</c> in the running game and at <c>+505</c> in the record. Lifting the memory offsets
	/// off the executable and using them as file offsets produces something that parses and is wrong.
	/// </para>
	/// <para>
	/// <b>The six needs are floats the original clamps to 0..100</b>, and every one of them reads as a
	/// whole number in the shipped park. Four are named by the game's own logging, which prints thirst,
	/// hunger, toilet and illness by name while scoring which ride a guest will choose; litter is named
	/// by the line it prints over it. The balance file agrees independently - its
	/// <c>PeepInfo.DecisionVar…Weight</c> keys run Dist, Queue, Excitement, Thirst, Hunger, Toilet,
	/// Illness, the same terms in the same order the scoring code multiplies them.
	/// </para>
	/// <para>
	/// A seventh float sits between <see cref="Toilet"/> and the needs above it and is deliberately not
	/// read: it is zero on every guest in the shipped park and nothing has named it, so reading it would
	/// mean giving it a meaning it has not earned.
	/// </para>
	/// </summary>
	public readonly record struct GuestState(
		int State, int SavedState, int PersonType, int Cash, int ExitLevel,
		float Happiness, float Thirst, float Hunger, float Toilet, float Illness, float Litter,
		int MajorDest, int QueuePos, int PrankeryIndex )
	{
		/// <summary>
		/// The behaviour a guest returns to after a one-off animation. A new guest is constructed with
		/// this set to <see cref="Deciding"/>, which is why it reads 6 on every guest in a park that has
		/// only just opened.
		/// </summary>
		public const int Deciding = 6;

		/// <summary>How many behaviours there are, so a state outside the range reads as a bad record.</summary>
		public const int States = 22;

		/// <summary>How many kinds of guest the balance file describes, as <c>PeepTypes[0..7]</c>.</summary>
		public const int PersonTypes = 8;
	}

	/// <summary>Every person the walk found, in the order the file lists them.</summary>
	public IReadOnlyList<Person> People => _people;

	private readonly List<Person> _people = [];

	/// <summary>Every live sprite in the park's sprite table, in slot order.</summary>
	public IReadOnlyList<Sprite> Sprites => _sprites;

	private readonly List<Sprite> _sprites = [];

	/// <summary>
	/// Whether the sprite table ended exactly on the tag that follows it. The same check as
	/// <see cref="ClosedOnTrailer"/> and worth as much: the table is a slot count and a run of fixed
	/// records, so landing on the next module's tag to the byte says the count and the record size were
	/// both right.
	/// </summary>
	public bool ClosedOnSpriteTrailer { get; private set; }

	/// <summary>
	/// One cell of the park's 128x128 map - what is built on it, which way it faces, and which of its
	/// neighbours it joins. This is where a park's <b>paths</b> are: the ground model carries none of them.
	///
	/// <para>
	/// <c>Neighbours</c> and <c>Direction</c> are <b>stored, not computed</b>, so nothing here has to work
	/// out a neighbour mask from the cells around it. They share one compass: over the shipped park's path
	/// cells <c>Direction</c> only ever reads 0, 1, 4, 16 or 64 - bits 0, 2, 4 and 6 of
	/// <c>N NE E SE S SW W NW</c>, which is to say the four cardinals and nothing else.
	/// </para>
	/// <para>
	/// The three tile fields are the original's single <c>mTileData</c>, which is twelve bytes and holds
	/// three dwords. Splitting them this way is a reading of the shipped park rather than something the
	/// executable says, and it is a well-supported one: <c>TileSet</c> is 1 on all 78 path cells, 2 on all
	/// 4 queue cells and 0 on the other 16,302 - which is exactly the split the theme's <c>.tct</c> makes
	/// with its <c>PathTex</c> and <c>QueueTex</c> sections - while <c>TileAngle</c> is 0, 90, 180 or 270
	/// on every one of the 16,384 cells and never anything else.
	/// </para>
	/// <para>
	/// <c>Type</c> is the original's <c>mType</c>. Over Lost Kingdom it reads 7 on 9,077 cells, 0 on 6,875,
	/// 2 on 240, <b>1 on the 78 that are path</b>, 30 on 66, <b>4 on the 35 covered by something built</b>,
	/// 9 on 8, 3 on 4 and 10 on one. Only 1 and 4 are firmly identified - 1 by drawing it, which gives a
	/// connected loop with an avenue down to the park entrance, and 4 by its cells landing on the placed
	/// objects' own footprints.
	/// </para>
	/// </summary>
	public readonly record struct MapCell(
		int Type, ushort Flags, byte Neighbours, byte Direction,
		int TileSet, int TileIndex, int TileAngle, byte Status )
	{
		/// <summary>
		/// Whether this cell carried a map record at all. A cell that did not is left at its default, and
		/// <c>Type 0</c> is a real type rather than a "no answer", so this is the field that tells the two
		/// apart. Every cell of the one park the game ships carries one.
		/// </summary>
		public bool IsMapped => (Status & MapRecord) != 0;
	}

	/// <summary>
	/// The map, in the order the file lists it. <b>Indexed <c>y * 128 + x</c></b> - the opposite way round
	/// from the attribute map in <c>base.map</c>, which is <c>x * 128 + y</c>, and the same way as the
	/// heightfield. That is the game's own inconsistency and getting it backwards produces a map that still
	/// looks like a map; it was settled by drawing both and checking them against <c>base.map</c>'s own bus
	/// road, ticket booths and entrance column, which only the y-major reading reproduces.
	///
	/// <para>Empty if the walk stopped before it reached the map.</para>
	/// </summary>
	public IReadOnlyList<MapCell> Cells => _cells;

	private MapCell[] _cells = [];

	/// <summary>How many cells the map is across and down, whatever size the park inside it is.</summary>
	public const int MapSize = 128;

	/// <summary>The cell at a grid position, or a default cell for anywhere off the map.</summary>
	public MapCell CellAt( int x, int y )
		=> x < 0 || y < 0 || x >= MapSize || y >= MapSize || _cells.Length != MapCellCount
			? default
			: _cells[(y * MapSize) + x];

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
	private const int MapCellCount = MapSize * MapSize;

	/// <summary>
	/// A cell opens with a status byte saying which of three optional sub-records follow it, one bit
	/// each. That is the original's own loop: it reads the byte, then a map record, then a track record,
	/// then an effects record, each only if its own bit is set.
	///
	/// <para>
	/// Only two combinations occur in the shipped park - 3 on 16,134 cells and 7 on the other 250, coming
	/// to 84 and 94 bytes - but adding the bits up costs nothing and reads the six combinations no
	/// shipped park happens to contain. A cell that is entirely default writes its status byte and
	/// nothing else, which is where the block's variable length comes from and why no fixed stride was
	/// ever going to walk it.
	/// </para>
	/// <para>
	/// These sizes are confirmed cell by cell and not merely in total: measured this way, all 16,384 of
	/// them land on the next cell's status byte every single time. That is a sharper check than the walk's
	/// own trailer test, which a pair of compensating errors could still pass.
	/// </para>
	/// </summary>
	private const int MapCellSize = 52;

	private const int TrackCellSize = 31;

	private const int EffectsCellSize = 10;

	private const int MapRecord = 0x1;

	private const int TrackRecord = 0x2;

	private const int EffectsRecord = 0x4;

	/// <summary>The bits of the status byte that mean something; any other one set is not understood.</summary>
	private const int KnownCellBits = MapRecord | TrackRecord | EffectsRecord;

	/// <summary>
	/// Where each field sits inside a cell's map record, which begins at the byte after the status. The
	/// record opens with a twenty-nine byte tile base - the track record repeats it field for field - and
	/// closes with twenty-three bytes of litter and pylon bookkeeping that nothing here wants.
	/// </summary>
	private const int CellDirection = 0;

	private const int CellFlags = 1;

	private const int CellNeighbours = 7;

	private const int CellTileData = 12;

	private const int CellType = 24;

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

		ReadMap();

		// The thing list proper. Its head is an id, not an offset - see ReadThings.
		var head = ReadInt32();

		ReadThings( head );

		ClosedOnTrailer = Problem == null
			&& _at + Trailer.Length <= _data.Length
			&& System.Text.Encoding.ASCII.GetString( _data, _at, Trailer.Length ) == Trailer;

		// And straight on into the sprite table, which needs no searching for: it begins at the four
		// bytes after the World block's own trailer.
		ReadSprites();
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
	/// Reads the 128x128 map, measuring each cell by the status byte it opens with. This is the bulk of
	/// the block - about 1.3MB of its 1.5MB.
	/// </summary>
	private void ReadMap()
	{
		_cells = new MapCell[MapCellCount];

		for ( var cell = 0; cell < MapCellCount; ++cell )
		{
			if ( _at >= _data.Length )
				throw new InvalidDataException( $"the map ran off the end of the payload at cell {cell}" );

			var status = _data[_at];

			if ( (status & ~KnownCellBits) != 0 )
				throw new InvalidDataException(
					$"map cell {cell} opens with status {status}, which sets a bit this does not know" );

			var size = 1
				+ ((status & MapRecord) != 0 ? MapCellSize : 0)
				+ ((status & TrackRecord) != 0 ? TrackCellSize : 0)
				+ ((status & EffectsRecord) != 0 ? EffectsCellSize : 0);

			if ( _at + size > _data.Length )
				throw new InvalidDataException( $"map cell {cell} runs past the end of the payload" );

			// Only the map record is read. A cell carrying a track record and no map record would open
			// with the same twenty-nine byte tile base, but those are a track's fields rather than a
			// tile's, and no cell of the one park the game ships is shaped that way.
			if ( (status & MapRecord) != 0 )
				_cells[cell] = ReadCell( _at + 1, status );

			_at += size;
		}
	}

	/// <summary>
	/// Reads one cell's map record. The three tile fields are the original's single twelve-byte
	/// <c>mTileData</c> - see <see cref="MapCell"/> for what says they are three dwords rather than one
	/// opaque run.
	/// </summary>
	private MapCell ReadCell( int at, byte status )
		=> new(
			Type: ReadInt32At( at + CellType ),
			Flags: (ushort)ReadUInt16At( at + CellFlags ),
			Neighbours: _data[at + CellNeighbours],
			Direction: _data[at + CellDirection],
			TileSet: ReadInt32At( at + CellTileData ),
			TileIndex: ReadInt32At( at + CellTileData + 4 ),
			TileAngle: ReadInt32At( at + CellTileData + 8 ),
			Status: status );

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
			else if ( Array.IndexOf( PersonModels, model ) >= 0 )
				_people.Add( ReadPerson( id, model, start ) );

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
	/// The models that are people: a guest, then the five kinds of staff. The shipped park holds thirteen
	/// guests and one of each staff, which is eighteen - exactly how many sprites its table has live, and
	/// the reconciliation the tests pin.
	/// </summary>
	private static readonly int[] PersonModels = [1, 4, 5, 6, 7, 8];

	/// <summary>
	/// The model number of a guest, as opposed to a member of staff. They share the 390-byte person base
	/// and then part company: a guest adds the 135 bytes <see cref="GuestState"/> reads, and each kind of
	/// staff adds a 105-byte staff base and a handful of its own fields. That is where the size table's
	/// numbers come from, and they close exactly - a guest is <c>8 + 390 + 135</c> = 533.
	/// </summary>
	private const int GuestModel = 1;

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

	/// <summary>
	/// A person's record: the same head every thing has, and the two fields that make them drawable.
	///
	/// <para>
	/// <c>mSpriteScript</c> at <c>+0x10</c> is the slot of their sprite in the table at the end of the
	/// block. It is <b>not</b> a pointer and not an index into any list here: the table's own handles are
	/// stale heap addresses that appear nowhere else in the payload, so the slot is the only join there
	/// is. Nor is it the order either list happens to be in - the two disagree - so pairing people to
	/// sprites by position gives the wrong guests while still looking plausible. Read this way the
	/// eighteen people carry eighteen distinct slots which are exactly the eighteen live ones.
	/// </para>
	/// <para>
	/// <c>mSpriteAngle</c> at <c>+0xf2</c> is their heading - see <see cref="Person.Facing"/>.
	/// </para>
	/// </summary>
	private Person ReadPerson( int id, int model, int start )
		=> new(
			ThingId: id,
			Model: model,
			RawX: ReadUInt16At( start + 8 ),            // mX, in 256ths of a cell, as an object's is
			RawY: ReadUInt16At( start + 10 ),           // mY
			SpriteSlot: ReadInt32At( start + 0x10 ),    // mSpriteScript
			Angle: ReadUInt16At( start + 0xf2 ),        // mSpriteAngle
			Guest: model == GuestModel ? ReadGuest( start ) : null );

	/// <summary>
	/// A guest's own block, which begins at <c>+398</c> - after the eight-byte thing head and the
	/// 390-byte person base - and runs the 135 bytes that make a guest's record 533.
	///
	/// <para>
	/// The order is the original's own, and it is <b>alphabetical by field name</b>, which is why
	/// <c>mCash</c> precedes <c>mExitLevel</c> and <c>mState</c> comes after <c>mSavedState</c>. That is
	/// worth knowing because it is what makes the offsets derivable at all: each one is the sum of the
	/// sizes before it, and a field inserted anywhere shifts every field after it.
	/// </para>
	/// </summary>
	private GuestState ReadGuest( int start )
		=> new(
			State: ReadInt32At( start + 505 ),          // mState
			SavedState: ReadInt32At( start + 501 ),     // mSavedState
			PersonType: ReadByteAt( start + 468 ),      // mPersonType, an index into PeepTypes[0..7]
			Cash: ReadInt32At( start + 414 ),           // mCash
			ExitLevel: ReadInt32At( start + 418 ),      // mExitLevel, the countdown to going home
			Happiness: ReadSingleAt( start + 422 ),
			Thirst: ReadSingleAt( start + 509 ),
			Hunger: ReadSingleAt( start + 426 ),
			Toilet: ReadSingleAt( start + 525 ),
			Illness: ReadSingleAt( start + 529 ),
			Litter: ReadSingleAt( start + 438 ),
			MajorDest: ReadUInt16At( start + 442 ),     // mMajorDest - the thing they have chosen, or none
			QueuePos: ReadByteAt( start + 494 ),        // mQueuePos
			PrankeryIndex: ReadByteAt( start + 469 ) ); // mPrankeryIndex

	/// <summary>
	/// One live sprite: the picture a person is drawn as, and the state the park was saved in.
	///
	/// <para>
	/// <b>The art was chosen once and written down.</b> When a person is made, the game picks a bank of
	/// their kind at random and then a set within it at random, and stores both. Nothing recomputes them,
	/// so a reader must read them back rather than roll again - rolling again would change every guest's
	/// clothes on each load.
	/// </para>
	/// <para>
	/// <b><see cref="SpriteNumber"/> is two numbers in one.</b> Its low four bits are the set and the
	/// rest is how far past its kind's first bank this sprite's bank sits; the engine takes it apart
	/// exactly that way before it looks a picture up.
	/// </para>
	/// <para>
	/// <see cref="Height"/> is an offset above the ground rather than a height - it reads zero on every
	/// person in the shipped park, and the engine adds the land under them to it as it draws.
	/// </para>
	/// </summary>
	public readonly record struct Sprite(
		int Slot, int Type, int Bank, int SpriteNumber,
		float X, float Height, float Y, int Facing, int Frame, int Alpha, int State )
	{
		/// <summary>How far past its kind's first bank this sprite's bank is.</summary>
		public int BankOffset => SpriteNumber >> 4;

		/// <summary>Which set of that bank is being drawn - the stand, the walk, and so on.</summary>
		public int Set => SpriteNumber & 0xf;
	}

	/// <summary>
	/// The sprite table's tag as it appears in the file. Like every other tag here it is written as a
	/// little-endian dword, so it reads backwards in a dump.
	/// </summary>
	public const string SpriteTag = "TPCS";

	/// <summary>The tag written after the sprite table.</summary>
	public const string SpriteTrailer = "CSPS";

	/// <summary>
	/// How big one sprite record is. The original checks this number on the way in and refuses the block
	/// if it differs, so it is the file's own statement rather than a measurement.
	/// </summary>
	private const int SpriteRecordSize = 0x118;

	// Where each field sits in a sprite record, from the code that writes them.
	private const int SpriteState = 0x18;

	private const int SpriteX = 0x88;

	private const int SpriteHeight = 0x8c;

	private const int SpriteY = 0x90;

	private const int SpriteAlpha = 0xa0;

	private const int SpriteType = 0xac;

	private const int SpriteBank = 0xb0;

	private const int SpriteNumberAt = 0xb4;

	private const int SpriteFrame = 0xb8;

	private const int SpriteFacing = 0xc0;

	/// <summary>
	/// Reads the table of world sprites that follows the World block.
	///
	/// <para>
	/// It needs no searching for: it begins at the four bytes after the World trailer. The shape is a
	/// tag, the size of one record, how many slots the table has, one handle per slot, and then one
	/// record for each handle that is not zero - so the records are counted by the handles rather than
	/// by a number of their own, and slot zero is never used.
	/// </para>
	/// <para>
	/// Like the World walk this proves itself by landing on the next module's tag: a wrong slot count or
	/// record size misses <c>CSPS</c> by a whole number of records. A surprise is recorded rather than
	/// thrown, because the people and objects are worth having even when their pictures are not.
	/// </para>
	/// </summary>
	private void ReadSprites()
	{
		// Only worth trying where the walk arrived somewhere known. Without the trailer there is no
		// reason to believe _at points at anything at all.
		if ( !ClosedOnTrailer )
			return;

		_at += Trailer.Length;

		if ( _at + 12 > _data.Length )
		{
			Problem ??= "the sprite table runs past the end of the payload";
			return;
		}

		var tag = System.Text.Encoding.ASCII.GetString( _data, _at, SpriteTag.Length );

		if ( tag != SpriteTag )
		{
			Problem ??= $"the block after the world is tagged '{tag}' rather than {SpriteTag}";
			return;
		}

		_at += SpriteTag.Length;

		var recordSize = ReadInt32();
		var slots = ReadInt32();

		if ( recordSize != SpriteRecordSize )
		{
			Problem ??= $"a sprite record says it is {recordSize} bytes rather than {SpriteRecordSize}";
			return;
		}

		if ( slots < 0 || _at + (slots * 4) > _data.Length )
		{
			Problem ??= $"the sprite table says it has {slots} slots";
			return;
		}

		var handles = new int[slots];

		for ( var slot = 0; slot < slots; ++slot )
			handles[slot] = ReadInt32();

		for ( var slot = 0; slot < slots; ++slot )
		{
			if ( handles[slot] == 0 )
				continue;

			if ( _at + recordSize > _data.Length )
			{
				Problem ??= $"sprite slot {slot} runs past the end of the payload";
				return;
			}

			_sprites.Add( new Sprite(
				Slot: slot,
				Type: ReadInt32At( _at + SpriteType ),
				Bank: ReadInt32At( _at + SpriteBank ),
				SpriteNumber: ReadInt32At( _at + SpriteNumberAt ),
				X: ReadSingleAt( _at + SpriteX ),
				Height: ReadSingleAt( _at + SpriteHeight ),
				Y: ReadSingleAt( _at + SpriteY ),
				Facing: ReadInt32At( _at + SpriteFacing ),
				Frame: ReadInt32At( _at + SpriteFrame ),
				Alpha: ReadInt32At( _at + SpriteAlpha ),
				State: ReadInt32At( _at + SpriteState ) ) );

			_at += recordSize;
		}

		ClosedOnSpriteTrailer = _at + SpriteTrailer.Length <= _data.Length
			&& System.Text.Encoding.ASCII.GetString( _data, _at, SpriteTrailer.Length ) == SpriteTrailer;
	}

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

	private float ReadSingleAt( int offset )
	{
		if ( offset + 4 > _data.Length )
			throw new InvalidDataException( $"a float at 0x{offset:x} runs past the end of the payload" );

		return BitConverter.ToSingle( _data, offset );
	}

	private int ReadUInt16At( int offset )
	{
		if ( offset + 2 > _data.Length )
			throw new InvalidDataException( $"a word at 0x{offset:x} runs past the end of the payload" );

		return BitConverter.ToUInt16( _data, offset );
	}

	private int ReadByteAt( int offset )
	{
		if ( offset >= _data.Length )
			throw new InvalidDataException( $"a byte at 0x{offset:x} runs past the end of the payload" );

		return _data[offset];
	}
}
