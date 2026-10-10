using System.Buffers.Binary;

namespace OpenTPW;

/// <summary>
/// The park file's people, written back: the third stage of the writer (<c>docs/exe/saves.md</c>, "OpenTPW's
/// writer, the people"). The thing list is taken apart and put together again around the running park's guests
/// and staff, and with it the three parts of the file that follow the list: the sprite table, the cell chains
/// and the message sets.
/// </summary>
public sealed partial class ParkWorld
{
	/// <summary>One record of the thing list as the file holds it: the thing's id, its model, and where it lies.</summary>
	internal readonly record struct ThingRecord( int Id, int Model, int At, int Size );

	private readonly List<ThingRecord> _records = [];

	/// <summary>Every thing's record, in the file's order.</summary>
	internal IReadOnlyList<ThingRecord> ThingRecords => _records;

	/// <summary>Where <c>Used Thing Head</c> sits, the dword before the first record; -1 where the walk never got there.</summary>
	internal int ThingHeadAt { get; private set; } = -1;

	/// <summary>Where the thing list ends, on the world's trailer.</summary>
	internal int WorldEndAt { get; private set; } = -1;

	/// <summary>Where the sprite table's tag sits, and its trailer's.</summary>
	internal int SpritesAt { get; private set; } = -1;

	/// <inheritdoc cref="SpritesAt"/>
	internal int SpritesEndAt { get; private set; } = -1;

	/// <summary>How many slots the sprite table has, live or not.</summary>
	internal int SpriteSlots { get; private set; }

	private readonly Dictionary<int, int> _spriteRecords = [];

	/// <summary>
	/// The things on a cell, as the file chains them: the cell's <c>mWho</c>, then each thing's <c>mMapChild</c>
	/// (<c>FUN_0050b090</c>). Empty off the map, on a cell with nobody, or where the list was not read.
	/// </summary>
	public IReadOnlyList<int> ChainAt( int x, int y )
	{
		var chain = new List<int>();

		if ( x < 0 || y < 0 || x >= MapSize || y >= MapSize || _cells.Length != MapCellCount )
			return chain;

		for ( var who = (int)_cells[(y * MapSize) + x].Occupant; who != 0 && chain.Count <= _records.Count; )
		{
			var at = _records.FindIndex( record => record.Id == who );

			if ( at < 0 )
				break;

			chain.Add( who );
			who = ReadUInt16At( _records[at].At + MapChildAt );
		}

		return chain;
	}

	/// <summary>A copy of a thing's record as the file holds it, its eight-byte list head first; null for an id the file does not hold.</summary>
	public byte[]? RecordOf( int thingId )
	{
		var at = _records.FindIndex( record => record.Id == thingId );

		return at < 0 ? null : _data.AsSpan( _records[at].At, _records[at].Size ).ToArray();
	}

	/// <summary>A copy of a live sprite slot's 280-byte record; null for an empty slot.</summary>
	public byte[]? SpriteRecordOf( int slot ) =>
		_spriteRecords.TryGetValue( slot, out var at ) ? _data.AsSpan( at, SpriteRecordSize ).ToArray() : null;

	/// <summary>
	/// A person's <c>subpath_buffer</c> and <c>subpath_dist</c>, the navigator's five waypoints and the leg after
	/// each, raw (FileFormats <c>saves.md</c>, "The navigator"): only the first <c>path_buffer_count</c> waypoints
	/// and one leg fewer mean anything. Empty for a thing that is no person of the file's.
	/// </summary>
	public IReadOnlyList<(int X, int Y, int Leg)> SubpathOf( int thingId )
	{
		var at = _records.FindIndex( record => record.Id == thingId && IsPerson( record.Model ) );
		var slots = new List<(int, int, int)>();

		for ( var i = 0; at >= 0 && i < NavigatorState.SubpathSlots; ++i )
		{
			var slot = _records[at].At + SubpathAt + (12 * i);

			slots.Add( (ReadInt32At( slot ), ReadInt32At( slot + 4 ), ReadInt32At( slot + 8 )) );
		}

		return slots;
	}

	/// <summary>Where <c>subpath_buffer[0]</c> sits in a person's record; a slot is twelve bytes.</summary>
	private const int SubpathAt = 152;

	/// <summary>
	/// A staff kind's list, by its thing model: the header's head for the kind (<c>mFirstHandyman</c> to
	/// <c>mFirstResearcher</c>), then each member's <c>mNext</c>. Empty for a model that is not staff.
	/// </summary>
	public IReadOnlyList<int> StaffList( int model )
	{
		var list = new List<int>();
		var kind = Array.IndexOf( StaffHeadModels, model );

		if ( kind < 0 || HeaderAt < 0 )
			return list;

		for ( var id = ReadUInt16At( HeaderAt + HeaderFieldAt( FirstHandymanField + kind ) ); id != 0 && list.Count <= _records.Count; )
		{
			var at = _records.FindIndex( record => record.Id == id && record.Model == model );

			if ( at < 0 )
				break;

			list.Add( id );
			id = ReadUInt16At( _records[at].At + StaffNextAt( model ) );
		}

		return list;
	}

	/// <summary>
	/// The message centre's listener sets, each the ids told of one message type, in the file's order (FileFormats
	/// <c>saves.md</c>, "The message centre module"); null where the file's sprites or particles did not close.
	/// </summary>
	public IReadOnlyList<IReadOnlyList<int>>? MessageSets()
	{
		if ( !ClosedOnSpriteTrailer )
			return null;

		try
		{
			return ReadMessageSets( _data, SpritesEndAt + SpriteTrailer.Length ).Sets;
		}
		catch ( InvalidOperationException )
		{
			return null;
		}
	}

	/// <summary>The highest id a thing can have: the original's node table holds 10,239 (<c>FUN_005179c0</c>).</summary>
	public const int HighestThingId = 10239;

	/// <summary>How many slots the original's sprite table grows by when it is full (<c>FUN_00475a10</c>).</summary>
	public const int SpriteSlotsStep = 50;

	/// <summary>
	/// One of the running park's people, as the writer takes them.
	///
	/// <para>
	/// <see cref="Person"/> carries what the reader's own records hold; its <see cref="Person.SpriteSlot"/> is not
	/// read, because the writer keeps the file's slot or gives out one of its own. <see cref="Sprite"/> is null for
	/// somebody who is not drawn, and only its picture is read (<see cref="Sprite.Type"/> to
	/// <see cref="Sprite.Pc"/>): where the sprite stands is the navigator's position.
	/// </para>
	/// <para>
	/// <see cref="Waypoints"/> and <see cref="LegLengths"/> are the navigator's <c>subpath_buffer</c> and
	/// <c>subpath_dist</c>, in its fixed point; null leaves the file's, which is what a person who has walked no new
	/// route since the load still holds.
	/// </para>
	/// </summary>
	/// <param name="SetDestSuccessfully"><c>mSetDestSuccessfully</c>; null leaves the record's, for a kind that keeps none here.</param>
	/// <param name="SpriteInterval">The sprite record's <c>+0x80</c>, the milliseconds between its turns.</param>
	/// <param name="MadeSetByte">
	/// The sprite record's <c>+0xbc</c> as its constructor leaves it (<c>FUN_004758f0</c>): the frames a direction
	/// of the set the sprite is made on, which for a person is their bank's set 0. Written to a made sprite; null
	/// where the bank is not to hand.
	/// </param>
	/// <param name="StateSetByte">
	/// The same field as a state's animation has left it since the sprite was made or loaded
	/// (<c>FUN_00475b80</c> handed 0 to 3), written to a made sprite and a kept one alike; null with none started.
	/// </param>
	/// <param name="Balloon">The balloon a guest holds, a sprite of its own on the slot <c>mBalloonScript</c> names.</param>
	/// <param name="Bubble">The thought bubble over them, a sprite of its own on the slot <c>mThoughtScript</c> names.</param>
	/// <param name="LastRecordedMapId">
	/// The record's <c>mLastRecordedMapId</c>, written for a member of staff; null leaves the record's, the file's
	/// on a kept person and nought on a made one, as an arrival's is in the original's own files.
	/// </param>
	/// <param name="SpriteLoops">
	/// The loop starts the person's own program has pushed and not popped, the oldest first
	/// (<see cref="SpriteLoopsOf"/>): one word inside a state script's loop, none on any other. Null leaves the
	/// record's stack as it is.
	/// </param>
	/// <param name="SpriteFlags">
	/// The drawing flags of the person's own sprite, its local 16 (<see cref="SpriteFlagsOf"/>). Null leaves the
	/// record's.
	/// </param>
	public readonly record struct WrittenPerson( Person Person, Sprite? Sprite,
		IReadOnlyList<(int X, int Y)>? Waypoints = null, IReadOnlyList<int>? LegLengths = null,
		int PreviousX = 0, int PreviousY = 0, int NextAnim = 0, int NextServiceInterval = 0,
		bool? SetDestSuccessfully = null, int LastThought = 0, int TimeBubbleShown = 0,
		uint StrandedTime = 0, int SpriteInterval = 0x3e, int? MadeSetByte = null, int? StateSetByte = null,
		WrittenSprite? Balloon = null, WrittenSprite? Bubble = null, int? LastRecordedMapId = null,
		IReadOnlyList<int>? SpriteLoops = null, int? SpriteFlags = null );

	/// <summary>
	/// A sprite that is no person's own picture, as the writer takes it: a balloon or a thought bubble. All of
	/// <see cref="Picture"/> but its slot and state is written, its place among it.
	/// </summary>
	/// <param name="Interval">The sprite record's <c>+0x80</c>.</param>
	/// <param name="MadeSetByte">The record's <c>+0xbc</c> where the sprite is made; see <see cref="WrittenPerson"/>.</param>
	/// <param name="Loops">
	/// The loop starts its program has pushed and not popped, the oldest first, each a word of the programs' array
	/// (the record's stack, <see cref="SpriteLoopsOf"/>). Null leaves the record's stack and state as they are,
	/// which is right for a program with no loop; given, the record is written as one that has run: state 2 and
	/// shown, or with <paramref name="Ended"/> state 4 and hidden, as the end word leaves it (<c>0x0047509d</c>).
	/// </param>
	public readonly record struct WrittenSprite( Sprite Picture, int Interval = 0x3e, int? MadeSetByte = null,
		IReadOnlyList<int>? Loops = null, bool Ended = false );

	/// <summary>
	/// A rider's head hung on a ride's node, as the writer takes it: a sprite of its own on <paramref name="Slot"/>,
	/// which the node's lookup record names (<c>docs/exe/saves.md</c>, "OpenTPW's writer, a rider's head").
	/// </summary>
	/// <param name="Kind">The sprite kind: 1, a child's head, or 3, a costume's (<c>FUN_0044b410</c>).</param>
	/// <param name="Bank">The rider's own bank of that kind.</param>
	public readonly record struct WrittenHead( int Slot, int Kind, int Bank );

	/// <summary>
	/// The heads to write and the file's to let go: <see cref="Hung"/> each on its slot, and <see cref="Gone"/> the
	/// slots of the file's heads that hang no longer.
	/// </summary>
	public sealed record HeadEdits( IReadOnlyList<WrittenHead> Hung, IReadOnlySet<int> Gone );

	/// <summary>
	/// The lowest <paramref name="count"/> slots of the sprite table that the file leaves empty and
	/// <paramref name="taken"/> does not name, from 1, as <c>FUN_00475a10</c> looks for one.
	/// </summary>
	public int[] EmptySpriteSlots( int count, IReadOnlySet<int> taken )
	{
		var slots = new int[count];
		var slot = 0;

		for ( var i = 0; i < count; ++i )
		{
			do
				++slot;
			while ( _spriteRecords.ContainsKey( slot ) || taken.Contains( slot ) );

			slots[i] = slot;
		}

		return slots;
	}

	/// <summary>The sprite kind in the file's slot, or null for an empty slot.</summary>
	public int? SpriteKindIn( int slot )
		=> _spriteRecords.TryGetValue( slot, out var at ) ? ReadInt32At( at + SpriteType ) : null;

	/// <summary>What <see cref="PutPeople"/> did, for the log.</summary>
	/// <param name="UnmatchedSpriteSets">
	/// Made sprites that were handed no <c>+0xbc</c>, their bank not being to hand, and found no sprite of the same
	/// kind, bank and set in the file to copy it from: it was taken from a sprite of another set or kind, or left
	/// at one.
	/// </param>
	/// <param name="Balloons">Balloons written, each on its guest's <c>mBalloonScript</c>.</param>
	/// <param name="Bubbles">Thought bubbles written, each on its person's <c>mThoughtScript</c>.</param>
	/// <param name="LetGo">Balloons let go and still bursting written, each on a slot nobody names.</param>
	/// <param name="Heads">Riders' heads written, each on the slot its node's lookup record names.</param>
	public readonly record struct PeopleWritten( int Kept, int Made, int Gone, int SpriteSlots, int LiveSprites,
		int CellsHeaded, int UnmatchedSpriteSets, int Balloons = 0, int Bubbles = 0, int LetGo = 0, int Heads = 0 );

	/// <summary>The first header field of the five staff lists' heads, in the header's order.</summary>
	private const int FirstHandymanField = 20;

	/// <summary>The staff models in the order the header holds their lists' heads: handyman, mechanic, entertainer, guard, researcher.</summary>
	private static readonly int[] StaffHeadModels = [5, 4, 6, 7, 8];

	/// <summary>Where each staff model's <c>mNext</c> sits, its record's last word.</summary>
	private static int StaffNextAt( int model ) => RecordSizes[model] - 2;

	private const int GuardModel = 7;

	/// <summary>The message sets the thing list decides: everybody's, the staff's and the guards'.</summary>
	private const int EverybodySet = 0xa;

	private const int StaffSet = 0xc;

	/// <summary>The day's change: every catalogue object and the model-19 thing.</summary>
	private const int ObjectSet = 0xb;

	private const int GuardSet = 0x1b;

	private static bool IsPerson( int model ) => Array.IndexOf( PersonModels, model ) >= 0;

	/// <summary>
	/// <paramref name="body"/> with the running park's people in place of the file's.
	///
	/// <para>
	/// <b>The thing list.</b> A person the file holds and the park still has keeps their record, written over field
	/// by field; one the park has made is written whole, ahead of the file's things and newest first, which is the
	/// order the park gives them their turns in; one who has gone is left out. Every other thing's record goes out
	/// as it lies.
	/// </para>
	/// <para>
	/// <b>The chains.</b> Each cell's <c>mWho</c> and each thing's <c>mMapChild</c> and <c>mMapParent</c> are one
	/// chain. A person is written on the cell their <c>mX</c> and <c>mY</c> name: every person of every park file to
	/// hand stands in their own cell's chain, before any object. Whoever has come onto a cell heads its chain, the
	/// newest first; whoever the file had there and is there still follows, in the file's order; then the file's
	/// things that are no person, in theirs.
	/// </para>
	/// <para>
	/// <b>The staff lists</b> (the header's five heads and each member's <c>mNext</c>) run in the thing list's order,
	/// as the files' do. <b>The sprite table</b> keeps a kept person's slot, gives a made one the lowest free and
	/// grows by fifty when it is full (<c>FUN_00475a10</c>), and empties a gone one's. <b>Message sets</b>
	/// <c>0xa</c>, <c>0xc</c> and <c>0x1b</c> hold every person, every member of staff and every guard beside the
	/// things of the file's that are no person, in rising id.
	/// </para>
	/// <para>
	/// <b>A balloon and a thought bubble</b> are each a sprite of the table, named by the slot in the person's
	/// record: one the file holds for that person and the park still shows keeps its slot and is written over, one
	/// made takes the lowest free after the people's own, and one gone is let go.
	/// </para>
	/// <para>
	/// A made record's bytes that nothing here holds are nought, as the original's own constructors leave them
	/// (<c>FUN_00518e00</c>, <c>FUN_0050ffe0</c>, <c>FUN_004758f0</c>).
	/// </para>
	/// </summary>
	/// <exception cref="InvalidOperationException">The file's list, sprites or sets were not read whole, or a person cannot be written.</exception>
	internal byte[] PutPeople( byte[] body, IReadOnlyList<WrittenPerson> people, out PeopleWritten report )
		=> PutPeople( body, people, null, out report );

	/// <summary>
	/// <see cref="PutPeople(byte[], IReadOnlyList{WrittenPerson}, out PeopleWritten)"/>, with the objects bought and
	/// sold since the load (<c>docs/exe/saves.md</c>, "OpenTPW's writer, a thing bought and a thing sold").
	///
	/// <para>
	/// <b>An object made</b> goes into the thing list among the made, newest first, and to the head of the object
	/// list (<c>mFirstObject</c>, <c>mNext</c>), as the original's constructor puts one; onto its anchor cell's
	/// chain behind whoever stands there; and into set <c>0xb</c>, the day's change. <b>An object gone</b> is left
	/// out of all four.
	/// </para>
	/// </summary>
	internal byte[] PutPeople( byte[] body, IReadOnlyList<WrittenPerson> people, ObjectEdits? objects, out PeopleWritten report )
		=> PutPeople( body, people, objects, null, out report );

	/// <summary>
	/// <see cref="PutPeople(byte[], IReadOnlyList{WrittenPerson}, ObjectEdits?, out PeopleWritten)"/>, with the
	/// balloons let go and still bursting. One is a sprite of the table that no record names (a guest's
	/// <c>mBalloonScript</c> is cleared as they let go, <c>0x004fe96b</c>): each is written on a record made anew, on the lowest free slot
	/// after the people's, the balloons' and the bubbles', and every balloon of the file's that nobody names now is
	/// let go. Null leaves the file's as they lie.
	/// </summary>
	internal byte[] PutPeople( byte[] body, IReadOnlyList<WrittenPerson> people, ObjectEdits? objects,
		IReadOnlyList<WrittenSprite>? letGo, out PeopleWritten report )
		=> PutPeople( body, people, objects, letGo, null, out report );

	/// <summary>
	/// <see cref="PutPeople(byte[], IReadOnlyList{WrittenPerson}, ObjectEdits?, IReadOnlyList{WrittenSprite}?, out PeopleWritten)"/>,
	/// with the riders' heads. Each is written on the slot it is given, before any slot is dealt to a person made:
	/// over the file's record where the slot holds a head already, its kind and bank alone, and on a record made
	/// anew where it does not (<see cref="HeadSprite"/>). A head gone gives its slot up.
	/// </summary>
	internal byte[] PutPeople( byte[] body, IReadOnlyList<WrittenPerson> people, ObjectEdits? objects,
		IReadOnlyList<WrittenSprite>? letGo, HeadEdits? heads, out PeopleWritten report )
	{
		var madeObjects = objects?.Made ?? [];
		var goneObjects = objects?.Gone ?? new HashSet<int>();

		if ( ThingHeadAt < 0 || WorldEndAt < 0 || MapAt < 0 || !ClosedOnTrailer )
			throw new InvalidOperationException( "the park file it was loaded from holds no thing list" );

		if ( !ClosedOnSpriteTrailer || SpritesAt < 0 )
			throw new InvalidOperationException( "the park file it was loaded from holds no sprite table" );

		var (setsAt, setsEndAt, sets) = ReadMessageSets( body, SpritesEndAt + SpriteTrailer.Length );
		var fileRecords = _records.ToDictionary( record => record.Id );
		var running = new Dictionary<int, WrittenPerson>();

		foreach ( var person in people )
		{
			var id = person.Person.ThingId;

			if ( id < 1 || id > HighestThingId )
				throw new InvalidOperationException( $"thing {id} is past the {HighestThingId} ids a park file can name" );

			if ( !IsPerson( person.Person.Model ) || (person.Person.Model == GuestModel) != (person.Person.Guest != null)
				|| (person.Person.Model != GuestModel) != (person.Person.Staff != null) )
				throw new InvalidOperationException( $"thing {id} is no guest and no member of staff" );

			if ( fileRecords.TryGetValue( id, out var held ) && held.Model != person.Person.Model )
				throw new InvalidOperationException( $"thing {id} is model {held.Model} in the file and {person.Person.Model} in the park" );

			if ( person.Person.RawX >> 8 >= MapSize || person.Person.RawY >> 8 >= MapSize || person.Person.RawX < 0 || person.Person.RawY < 0 )
				throw new InvalidOperationException( $"thing {id} stands off the map" );

			if ( !running.TryAdd( id, person ) )
				throw new InvalidOperationException( $"thing {id} is in the park twice" );
		}

		var boughtIds = new HashSet<int>();

		foreach ( var (id, record) in madeObjects )
		{
			if ( id < 1 || id > HighestThingId )
				throw new InvalidOperationException( $"thing {id} is past the {HighestThingId} ids a park file can name" );

			if ( fileRecords.ContainsKey( id ) || running.ContainsKey( id ) || !boughtIds.Add( id )
				|| record.Length != RecordSizes[CatalogueObjectModel] )
				throw new InvalidOperationException( $"thing {id}, an object bought, is in the file or the park already" );
		}

		foreach ( var id in goneObjects )
		{
			if ( !fileRecords.TryGetValue( id, out var held ) || held.Model != CatalogueObjectModel )
				throw new InvalidOperationException( $"thing {id}, an object sold, is no object of the file's" );
		}

		// The sprite table, slot by slot: the file's records, to be written over, let go and added to.
		var sprites = new SortedDictionary<int, byte[]>();

		foreach ( var (slot, at) in _spriteRecords )
			sprites[slot] = body.AsSpan( at, SpriteRecordSize ).ToArray();

		var slots = SpriteSlots;

		// The list: the made, newest first, then the file's without the gone.
		var list = new List<(int Id, int Model, byte[] Record, bool Made)>();

		foreach ( var person in people.Where( person => !fileRecords.ContainsKey( person.Person.ThingId ) ) )
			list.Add( (person.Person.ThingId, person.Person.Model, new byte[RecordSizes[person.Person.Model]], true) );

		var made = list.Count;

		foreach ( var (id, record) in madeObjects )
			list.Add( (id, CatalogueObjectModel, (byte[])record.Clone(), true) );

		list.Sort( ( a, b ) => b.Id.CompareTo( a.Id ) );
		var gone = 0;

		// The file's chains, before anything is moved: which cells held a person, and each cell's other things in order.
		var others = new Dictionary<int, List<int>>();
		var stood = new Dictionary<int, List<int>>();
		var touched = new HashSet<int>();

		for ( var cell = 0; cell < _cells.Length; ++cell )
		{
			var steps = 0;

			for ( var who = (int)_cells[cell].Occupant; who != 0; who = BinaryPrimitives.ReadUInt16LittleEndian( body.AsSpan( fileRecords[who].At + MapChildAt ) ) )
			{
				if ( !fileRecords.TryGetValue( who, out var linked ) || ++steps > _records.Count )
					throw new InvalidOperationException( $"cell {cell}'s chain names thing {who}, which the file does not hold, or runs round" );

				var kind = IsPerson( linked.Model ) ? stood : others;

				if ( !kind.TryGetValue( cell, out var chain ) )
					kind[cell] = chain = [];

				chain.Add( who );

				if ( IsPerson( linked.Model ) || goneObjects.Contains( who ) )
					touched.Add( cell );
			}
		}

		foreach ( var record in _records )
		{
			if ( goneObjects.Contains( record.Id ) )
				continue;

			if ( IsPerson( record.Model ) && !running.ContainsKey( record.Id ) )
			{
				// Gone: their record is left out, and their sprite, balloon and bubble let go.
				FreeSlots( body, record, sprites, own: true, balloon: true, bubble: true );
				++gone;
				continue;
			}

			list.Add( (record.Id, record.Model, body.AsSpan( record.At, record.Size ).ToArray(), false) );

			if ( IsPerson( record.Model ) )
			{
				var kept = running[record.Id];

				// A balloon's or a bubble's slot is let go only where the person shows none now: one they show is
				// written over where it lies, or left alone where the slot holds another kind of sprite.
				FreeSlots( body, record, sprites, own: kept.Sprite == null, balloon: kept.Balloon == null, bubble: kept.Bubble == null );
			}
		}

		// The heads, on the slots their lookup records name, before a slot is dealt to anybody made.
		foreach ( var slot in heads?.Gone ?? new HashSet<int>() )
			sprites.Remove( slot );

		foreach ( var rider in heads?.Hung ?? [] )
		{
			if ( rider.Slot < 1 )
				throw new InvalidOperationException( $"a rider's head is given sprite slot {rider.Slot}" );

			if ( sprites.TryGetValue( rider.Slot, out var held ) && IsHeadKind( BinaryPrimitives.ReadInt32LittleEndian( held.AsSpan( SpriteType ) ) ) )
			{
				Put32( held, SpriteType, rider.Kind );
				Put32( held, SpriteBank, rider.Bank );
			}
			else if ( held != null )
				throw new InvalidOperationException( $"a rider's head is given sprite slot {rider.Slot}, which holds another sprite" );
			else
				sprites[rider.Slot] = HeadSprite( rider );

			Room( rider.Slot );
		}

		// The sprites: a kept person's slot is theirs still; a made one's is the lowest free, oldest first.
		var slotOf = new Dictionary<int, int>();
		var unmatched = 0;

		foreach ( var (id, model, record, isMade) in list.Where( entry => IsPerson( entry.Model ) ).OrderBy( entry => entry.Id ) )
		{
			var person = running[id];

			if ( person.Sprite is not { } picture )
				continue;

			var slot = isMade ? 0 : BinaryPrimitives.ReadInt32LittleEndian( record.AsSpan( PersonSpriteSlotAt ) );

			if ( slot == 0 || !sprites.ContainsKey( slot ) )
			{
				slot = FreeSlot();
				sprites[slot] = MadeSprite( slot, picture, sprites.Values, person.StateSetByte ?? person.MadeSetByte, ref unmatched );
			}
			else if ( person.StateSetByte is { } setByte )
			{
				Put32( sprites[slot], SpriteSetByteAt, setByte );
			}

			PutSprite( sprites[slot], person, picture );
			slotOf[id] = slot;
		}

		// The balloons and the bubbles, after the people's own: one the file holds for its person keeps its slot.
		var balloonOf = new Dictionary<int, int>();
		var bubbleOf = new Dictionary<int, int>();

		foreach ( var (id, model, record, isMade) in list.Where( entry => IsPerson( entry.Model ) ).OrderBy( entry => entry.Id ) )
		{
			var person = running[id];

			if ( person.Balloon is { } balloon && model == GuestModel )
				balloonOf[id] = PutOther( isMade ? 0 : BinaryPrimitives.ReadInt32LittleEndian( record.AsSpan( BalloonScriptAt ) ), balloon );

			if ( person.Bubble is { } bubble )
				bubbleOf[id] = PutOther( isMade ? 0 : BinaryPrimitives.ReadInt32LittleEndian( record.AsSpan( ThoughtScriptAt ) ), bubble );
		}

		if ( letGo != null )
		{
			var held = balloonOf.Values.ToHashSet();

			foreach ( var slot in sprites.Where( entry => !held.Contains( entry.Key )
				&& BinaryPrimitives.ReadInt32LittleEndian( entry.Value.AsSpan( SpriteType ) ) == BalloonSpriteKind ).Select( entry => entry.Key ).ToList() )
				sprites.Remove( slot );

			foreach ( var bursting in letGo )
				PutOther( 0, bursting );
		}

		int FreeSlot()
		{
			var slot = 1;

			while ( sprites.ContainsKey( slot ) )
				++slot;

			Room( slot );

			return slot;
		}

		// The table grows by fifty until it has the slot.
		void Room( int slot )
		{
			while ( slot >= slots )
				slots += SpriteSlotsStep;
		}

		int PutOther( int slot, WrittenSprite other )
		{
			// The file's slot is kept only where it holds a sprite of this kind: a slot let go, or one that names
			// somebody's own picture, is not written over.
			if ( !sprites.TryGetValue( slot, out var held )
				|| BinaryPrimitives.ReadInt32LittleEndian( held.AsSpan( SpriteType ) ) != other.Picture.Type )
			{
				slot = FreeSlot();
				sprites[slot] = MadeSprite( slot, other.Picture, sprites.Values, other.MadeSetByte, ref unmatched );
			}

			PutOtherSprite( sprites[slot], other );

			return slot;
		}

		// The records.
		var peopleOn = new Dictionary<int, List<int>>();

		foreach ( var (id, model, record, isMade) in list )
		{
			if ( !IsPerson( model ) )
				continue;

			var person = running[id];

			PutPerson( record, person, slotOf.GetValueOrDefault( id ), isMade,
				balloonOf.GetValueOrDefault( id ), bubbleOf.GetValueOrDefault( id ) );

			var cell = ((person.Person.RawY >> 8) * MapSize) + (person.Person.RawX >> 8);

			if ( !peopleOn.TryGetValue( cell, out var standing ) )
				peopleOn[cell] = standing = [];

			standing.Add( id );
			touched.Add( cell );
		}

		// An object made stands on its anchor cell, behind whoever stands there.
		var objectsOn = new Dictionary<int, List<int>>();

		foreach ( var (id, record) in madeObjects )
		{
			var cell = (record[11] * MapSize) + record[9];

			if ( cell >= _cells.Length )
				throw new InvalidOperationException( $"thing {id}, an object bought, stands off the map" );

			if ( !objectsOn.TryGetValue( cell, out var standing ) )
				objectsOn[cell] = standing = [];

			standing.Add( id );
			touched.Add( cell );
		}

		// The list's own links, and each staff kind's.
		var byId = list.ToDictionary( entry => entry.Id, entry => entry.Record );

		for ( var i = 0; i < list.Count; ++i )
			Put32( list[i].Record, 0, i + 1 < list.Count ? list[i + 1].Id : 0 );

		var head = (byte[])body.AsSpan( 0, ThingHeadAt + 4 ).ToArray();

		Put32( head, ThingHeadAt, list.Count > 0 ? list[0].Id : 0 );

		for ( var kind = 0; kind < StaffHeadModels.Length; ++kind )
		{
			var model = StaffHeadModels[kind];
			var members = list.Where( entry => entry.Model == model ).ToList();

			Put16( head, HeaderAt + HeaderFieldAt( FirstHandymanField + kind ), members.Count > 0 ? members[0].Id : 0 );

			for ( var i = 0; i < members.Count; ++i )
				Put16( members[i].Record, StaffNextAt( model ), i + 1 < members.Count ? members[i + 1].Id : 0 );
		}

		// The object list: the made, newest first, then the file's own chain without the gone.
		if ( madeObjects.Count > 0 || goneObjects.Count > 0 )
		{
			var chain = madeObjects.Select( entry => entry.Id ).OrderByDescending( id => id ).ToList();
			var steps = 0;

			for ( var id = (int)BinaryPrimitives.ReadUInt16LittleEndian( body.AsSpan( HeaderAt + HeaderFieldAt( FirstObjectField ) ) ); id != 0; )
			{
				if ( !fileRecords.TryGetValue( id, out var linked ) || linked.Model != CatalogueObjectModel || ++steps > _records.Count )
					throw new InvalidOperationException( $"the object list names thing {id}, which is no object of the file's, or runs round" );

				if ( !goneObjects.Contains( id ) )
					chain.Add( id );

				id = BinaryPrimitives.ReadUInt16LittleEndian( body.AsSpan( linked.At + ObjectNextAt ) );
			}

			Put16( head, HeaderAt + HeaderFieldAt( FirstObjectField ), chain.Count > 0 ? chain[0] : 0 );

			for ( var i = 0; i < chain.Count; ++i )
				Put16( byId[chain[i]], ObjectNextAt, i + 1 < chain.Count ? chain[i + 1] : 0 );
		}

		// The chains of every cell a person stood on or stands on now.
		var cellAt = CellRecordsAt( body );
		var headed = 0;

		foreach ( var cell in touched )
		{
			// Whoever has come onto the cell heads it, the newest first; whoever the file had there and is there
			// still follows in the file's order; then the file's things that are no person.
			var here = peopleOn.GetValueOrDefault( cell ) ?? [];
			var before = stood.GetValueOrDefault( cell ) ?? [];
			var chain = here.Where( id => !before.Contains( id ) ).ToList();

			chain.AddRange( before.Where( here.Contains ) );
			chain.AddRange( (others.GetValueOrDefault( cell ) ?? []).Where( id => !goneObjects.Contains( id ) ) );
			chain.AddRange( objectsOn.GetValueOrDefault( cell ) ?? [] );

			for ( var i = 0; i < chain.Count; ++i )
			{
				Put16( byId[chain[i]], MapChildAt, i + 1 < chain.Count ? chain[i + 1] : 0 );
				Put16( byId[chain[i]], MapParentAt, i > 0 ? chain[i - 1] : 0 );
			}

			var who = chain.Count > 0 ? chain[0] : 0;

			if ( who == _cells[cell].Occupant )
				continue;

			if ( (body[cellAt[cell]] & MapRecord) == 0 )
				throw new InvalidOperationException( $"cell {cell} has no map record to write who stands on it" );

			Put16( head, cellAt[cell] + 1 + CellOccupant, who );
			++headed;
		}

		// The sets.
		var isStaff = list.Where( entry => IsPerson( entry.Model ) && entry.Model != GuestModel ).Select( entry => entry.Id ).ToList();

		sets[EverybodySet] = Members( sets[EverybodySet], fileRecords, list.Where( entry => IsPerson( entry.Model ) ).Select( entry => entry.Id ) );
		sets[StaffSet] = Members( sets[StaffSet], fileRecords, isStaff );
		sets[GuardSet] = Members( sets[GuardSet], fileRecords, list.Where( entry => entry.Model == GuardModel ).Select( entry => entry.Id ) );

		if ( madeObjects.Count > 0 || goneObjects.Count > 0 )
		{
			for ( var set = 0; set < sets.Count; ++set )
				sets[set] = [.. sets[set].Where( id => !goneObjects.Contains( id ) )];

			if ( sets.Count <= ObjectSet )
				throw new InvalidOperationException( "the park file it was loaded from holds no set for the day's change" );

			sets[ObjectSet] = [.. sets[ObjectSet].Concat( madeObjects.Select( entry => entry.Id ) ).Distinct().Order()];
		}

		// Put together: the world up to the list's head, the list, what lies between it and the sprite table's
		// handles, the table, what lies between it and the sets, the sets, the rest.
		using var stream = new MemoryStream( body.Length + (made * 1024) );
		using var writer = new BinaryWriter( stream );

		writer.Write( head );

		foreach ( var entry in list )
			writer.Write( entry.Record );

		writer.Write( body, WorldEndAt, SpritesAt - WorldEndAt );
		writer.Write( System.Text.Encoding.ASCII.GetBytes( SpriteTag ) );
		writer.Write( SpriteRecordSize );
		writer.Write( slots );

		// A live slot's handle is tested against nought on the way in and nothing more: the slot's own number is
		// written, where the original writes the address the record had.
		for ( var slot = 0; slot < slots; ++slot )
			writer.Write( sprites.ContainsKey( slot ) ? slot : 0 );

		foreach ( var record in sprites.Values )
			writer.Write( record );

		writer.Write( body, SpritesEndAt, setsAt - SpritesEndAt );
		writer.Write( sets.Count );

		foreach ( var set in sets )
		{
			writer.Write( set.Count );

			foreach ( var id in set )
				writer.Write( (ushort)id );
		}

		writer.Write( body, setsEndAt, body.Length - setsEndAt );
		writer.Flush();

		report = new PeopleWritten( Kept: running.Count - made, Made: made, Gone: gone, SpriteSlots: slots,
			LiveSprites: sprites.Count, CellsHeaded: headed, UnmatchedSpriteSets: unmatched,
			Balloons: balloonOf.Count, Bubbles: bubbleOf.Count, LetGo: letGo?.Count ?? 0, Heads: heads?.Hung.Count ?? 0 );

		return stream.ToArray();
	}

	/// <summary>Where <c>mMapChild</c> and <c>mMapParent</c> sit in every thing's record (<c>FUN_0050b090</c>).</summary>
	private const int MapChildAt = 12;

	private const int MapParentAt = 14;

	/// <summary>Where a person's <c>mSpriteScript</c> sits, with <c>mThoughtScript</c> and a guest's <c>mBalloonScript</c>.</summary>
	private const int PersonSpriteSlotAt = 16;

	private const int ThoughtScriptAt = 390;

	/// <summary>Where a member of staff's <c>mTimeHired</c> sits, eight bytes.</summary>
	private const int TimeHiredAt = 491;

	private const int BalloonScriptAt = 406;

	/// <summary>Lets go of the slots a file's person names, each where its flag says so: their own, the balloon's and the bubble's.</summary>
	private static void FreeSlots( byte[] body, ThingRecord record, SortedDictionary<int, byte[]> sprites, bool own,
		bool balloon, bool bubble )
	{
		if ( bubble )
			sprites.Remove( BinaryPrimitives.ReadInt32LittleEndian( body.AsSpan( record.At + ThoughtScriptAt ) ) );

		if ( balloon && record.Model == GuestModel )
			sprites.Remove( BinaryPrimitives.ReadInt32LittleEndian( body.AsSpan( record.At + BalloonScriptAt ) ) );

		if ( own )
			sprites.Remove( BinaryPrimitives.ReadInt32LittleEndian( body.AsSpan( record.At + PersonSpriteSlotAt ) ) );
	}

	/// <summary>A set's members: the file's that are no person, and <paramref name="people"/>, in rising id.</summary>
	private static List<int> Members( List<int> set, Dictionary<int, ThingRecord> fileRecords, IEnumerable<int> people ) =>
		[.. set.Where( id => !fileRecords.TryGetValue( id, out var record ) || !IsPerson( record.Model ) )
			.Concat( people ).Distinct().Order()];

	/// <summary>Where each cell's status byte lies in <paramref name="body"/>.</summary>
	private int[] CellRecordsAt( byte[] body )
	{
		var at = new int[MapCellCount];
		var next = MapAt;

		for ( var cell = 0; cell < MapCellCount; ++cell )
		{
			var status = body[next];

			at[cell] = next;
			next += 1
				+ ((status & MapRecord) != 0 ? MapCellSize : 0)
				+ ((status & TrackRecord) != 0 ? TrackCellSize : 0)
				+ ((status & EffectsRecord) != 0 ? EffectsCellSize : 0);
		}

		return at;
	}

	/// <summary>The particles module's magic, which opens the block after the sprite table's trailer, and its trailer.</summary>
	private const string ParticlesMagic = "LCTP";

	private const string ParticlesTrailer = "TRAP";

	private const string MessageTrailer = "SSEM";

	/// <summary>
	/// The message sets, found by walking the particles module from <paramref name="at"/>, the byte after the sprite
	/// table's trailer: its magic, the enabled dword, two sized images, the pool's size and its trailer (FileFormats
	/// <c>saves.md</c>, "The particles module"). Answers where the sets begin, where their trailer sits, and each
	/// set's ids.
	/// </summary>
	private static (int At, int EndAt, List<List<int>> Sets) ReadMessageSets( byte[] body, int at )
	{
		int Dword()
		{
			if ( at + 4 > body.Length )
				throw new InvalidOperationException( "the park file it was loaded from ends inside its particles or its message sets" );

			var value = BinaryPrimitives.ReadInt32LittleEndian( body.AsSpan( at ) );

			at += 4;
			return value;
		}

		bool Tag( string tag ) => at + 4 <= body.Length && System.Text.Encoding.ASCII.GetString( body, at, 4 ) == tag;

		if ( !Tag( ParticlesMagic ) )
			throw new InvalidOperationException( "the park file it was loaded from holds no particles module after its sprites" );

		at += 4;

		if ( Dword() != 1 )
			throw new InvalidOperationException( "the park file it was loaded from was saved with particles off, a form not decoded" );

		for ( var image = 0; image < 2; ++image )
		{
			var size = Dword();

			if ( size < 0 || at + size > body.Length )
				throw new InvalidOperationException( "the park file's particles module does not close" );

			at += size;
		}

		if ( Dword() != 0 || !Tag( ParticlesTrailer ) )
			throw new InvalidOperationException( "the park file's particles module does not close" );

		at += 4;

		var setsAt = at;
		var count = Dword();

		if ( count <= GuardSet || count > 64 )
			throw new InvalidOperationException( $"the park file holds {count} message sets" );

		var sets = new List<List<int>>();

		for ( var set = 0; set < count; ++set )
		{
			var members = Dword();

			if ( members < 0 || at + (members * 2) > body.Length )
				throw new InvalidOperationException( "the park file's message sets do not close" );

			var ids = new List<int>( members );

			for ( var i = 0; i < members; ++i, at += 2 )
				ids.Add( BinaryPrimitives.ReadUInt16LittleEndian( body.AsSpan( at ) ) );

			sets.Add( ids );
		}

		if ( !Tag( MessageTrailer ) )
			throw new InvalidOperationException( "the park file's message sets do not close" );

		return (setsAt, at, sets);
	}

	// The sprite record's fields the constructor writes that the reader does not hold (FUN_004758f0).
	private const int SpriteSlotAt = 0x04;

	/// <summary>
	/// How much room the sprite's loop stack has left, of <see cref="SpriteLoopDepth"/>: a loop's start is pushed
	/// by taking one off this and storing the program's word at <see cref="SpriteLoopsAt"/> plus four times what is
	/// left (<c>FUN_00475230</c>), so the first pushed lies last.
	/// </summary>
	private const int SpriteLoopRoomAt = 0x1c;

	private const int SpriteLoopsAt = 0x20;

	private const int SpriteLoopDepth = 20;

	/// <summary>How many loops the program is inside, counted up by each loop start (<c>0x004763c6</c>) and down as one ends.</summary>
	private const int SpriteLoopCountAt = 0x78;

	/// <summary>Whether the sprite is showing a frame (<c>FUN_00540b90</c>); the end word clears it.</summary>
	private const int SpriteShownAt = 0x114;

	/// <summary>The state of a sprite whose program has run a turn, and of one that has reached its end word (<c>0x004750a2</c>).</summary>
	private const int SpriteRunningState = 2;

	private const int SpriteEndedState = 4;

	/// <summary>The sprite kind of a balloon, the table's "balloons" (<c>0x00764090</c>).</summary>
	private const int BalloonSpriteKind = 10;

	/// <summary>
	/// The loop starts a live sprite's program has pushed and not popped, the oldest first, each a word of the
	/// programs' array; empty for an empty slot or a stack that does not read.
	/// </summary>
	public IReadOnlyList<int> SpriteLoopsOf( int slot )
	{
		if ( !_spriteRecords.TryGetValue( slot, out var at ) )
			return [];

		var room = ReadInt32At( at + SpriteLoopRoomAt );

		if ( room < 0 || room > SpriteLoopDepth )
			return [];

		var loops = new int[SpriteLoopDepth - room];

		for ( var i = 0; i < loops.Length; ++i )
			loops[i] = ReadInt32At( at + SpriteLoopsAt + ((SpriteLoopDepth - 1 - i) * 4) );

		return loops;
	}

	/// <summary>
	/// The three words of a sprite's record that a bank's state script reads as locals 14, 17 and 18: the frames
	/// a direction of its set (<c>+0xbc</c>) and the state group's third and fourth bytes (<c>+0xc8</c>,
	/// <c>+0xcc</c>), which only a sprite made on a state holds.
	/// </summary>
	public readonly record struct SpriteStateWords( int FramesPerDirection, int LeadIn, int Hold );

	private const int SpriteLeadInAt = 0xc8;

	private const int SpriteHoldAt = 0xcc;

	/// <summary>A live sprite's <see cref="SpriteStateWords"/>; noughts for an empty slot.</summary>
	public SpriteStateWords SpriteStateWordsOf( int slot )
		=> _spriteRecords.TryGetValue( slot, out var at )
			? new SpriteStateWords( ReadInt32At( at + SpriteSetByteAt ), ReadInt32At( at + SpriteLeadInAt ), ReadInt32At( at + SpriteHoldAt ) )
			: default;

	/// <summary>
	/// A live sprite's drawing flags, the word at <c>+0xc4</c> its program writes as local 16 and the draw reads
	/// (<c>0x00542075</c>): nought as the constructor leaves it (<c>FUN_004758f0</c>), <c>0x1200</c> once a
	/// person's program has run its first word. Nought for an empty slot.
	/// </summary>
	public int SpriteFlagsOf( int slot )
		=> _spriteRecords.TryGetValue( slot, out var at ) ? ReadInt32At( at + SpriteFlagsAt ) : 0;

	private const int SpriteDueAt = 0x7c;

	private const int SpriteIntervalAt = 0x80;

	private const int SpriteScaleAt = 0xa4;

	private const int SpriteSetByteAt = 0xbc;

	/// <summary>The state a sprite is constructed with (<c>FUN_004758f0</c>), its loop stack empty.</summary>
	private const int SpriteConstructedState = 1;


	/// <summary>
	/// A sprite record as the original's constructor leaves one (<c>FUN_004758f0</c>): its slot, state 1, an empty loop stack,
	/// alpha 255 and scale 1, and nought where the constructor writes nought or nothing. Its <c>+0x7c</c> is
	/// nought, a time already past, so it takes its first turn at once.
	///
	/// <para>
	/// Its <c>+0xbc</c> is <paramref name="setByte"/>, the frames a direction of the set it is made on
	/// (<c>docs/exe/saves.md</c>, "A sprite's `+0xbc`"). Handed none, it is copied from the nearest sprite the file
	/// holds and counted where that is no sprite of the same kind, bank and set
	/// (<see cref="PeopleWritten.UnmatchedSpriteSets"/>).
	/// </para>
	/// </summary>
	private static byte[] MadeSprite( int slot, Sprite picture, IEnumerable<byte[]> held, int? setByte, ref int unmatched )
	{
		var record = new byte[SpriteRecordSize];

		Put32( record, SpriteSlotAt, slot );
		Put32( record, SpriteState, SpriteConstructedState );
		Put32( record, SpriteLoopRoomAt, SpriteLoopDepth );
		PutSingle( record, SpriteScaleAt, 1f );
		PutSingle( record, SpriteScaleAt + 4, 1f );

		if ( setByte is { } known )
		{
			Put32( record, SpriteSetByteAt, known );
			return record;
		}

		int Field( byte[] other, int at ) => BinaryPrimitives.ReadInt32LittleEndian( other.AsSpan( at ) );

		var best = 0;
		var copied = 1;

		foreach ( var other in held )
		{
			if ( Field( other, SpriteType ) != picture.Type )
				continue;

			var score = 1
				+ ((Field( other, SpriteNumberAt ) & 0xf) == picture.Set ? 2 : 0)
				+ (Field( other, SpriteBank ) == picture.Bank && Field( other, SpriteNumberAt ) == picture.SpriteNumber ? 4 : 0);

			if ( score > best )
				(best, copied) = (score, Field( other, SpriteSetByteAt ));
		}

		if ( best < 7 )
			++unmatched;

		Put32( record, SpriteSetByteAt, copied );

		return record;
	}

	/// <summary>The sprite kinds of a head: a child's and a costume's.</summary>
	public const int ChildHeadKind = 1;

	public const int CostumeHeadKind = 3;

	private static bool IsHeadKind( int kind ) => kind is ChildHeadKind or CostumeHeadKind;

	/// <summary>The word of the programs' array a head's sprite is made on, <c>0x0074f558</c> (<c>FUN_0044b410</c>).</summary>
	private const int HeadProgram = 1704;

	/// <summary>Where a head's program rests once it has run its first turn, and the word at <c>+0x10</c> beside it.</summary>
	private const int HeadRestingWord = 1698;

	private const int HeadRestingBase = 1696;

	/// <summary>The one word a resting head's program has pushed, at the stack's first place.</summary>
	private const int HeadPushedWord = 1714;

	/// <summary>The record's <c>+0x74</c>, one on a resting head, and its drawing flags at <c>+0xc4</c>.</summary>
	private const int SpriteDepthAt = 0x74;

	private const int SpriteFlagsAt = 0xc4;

	private const int HeadFlags = 0x3000080;

	/// <summary>The frames a direction of a head's set, its <c>+0xbc</c>.</summary>
	private const int HeadSetByte = 8;

	/// <summary>
	/// A head's sprite record, as every head of the park files to hand rests (<c>docs/exe/saves.md</c>,
	/// "OpenTPW's writer, a rider's head"): made on <see cref="HeadProgram"/> with no place, set or frame
	/// (<c>FUN_00475a10</c> handed noughts), then run its first turn, so state 2 and shown, one word pushed, and
	/// the program's own drawing flags. What the record keeps of its last drawing is left nought, as on every
	/// sprite made here.
	/// </summary>
	private static byte[] HeadSprite( WrittenHead head )
	{
		var unmatched = 0;
		var record = MadeSprite( head.Slot, default, [], HeadSetByte, ref unmatched );

		Put32( record, SpritePcAt, HeadRestingWord );
		Put32( record, SpriteScriptAt, HeadProgram );
		Put32( record, SpriteScriptAt + 4, HeadRestingBase );
		Put32( record, SpriteState, SpriteRunningState );
		Put32( record, SpriteLoopRoomAt, SpriteLoopDepth - 1 );
		Put32( record, SpriteLoopsAt + ((SpriteLoopDepth - 1) * 4), HeadPushedWord );
		Put32( record, SpriteDepthAt, 1 );
		Put32( record, SpriteIntervalAt, 0x3e );
		Put32( record, SpriteAlpha, 0xff );
		Put32( record, SpriteType, head.Kind );
		Put32( record, SpriteBank, head.Bank );
		Put32( record, SpriteFlagsAt, HeadFlags );
		Put32( record, SpriteShownAt, 1 );

		return record;
	}

	/// <summary>Writes a person's picture and place over their sprite's record: the program and how far into it, the
	/// interval, the place (the navigator's, ten world units to a cell), alpha, kind, bank, set, frame and facing,
	/// and with <see cref="WrittenPerson.SpriteLoops"/> its loop stack: the room left, the words pushed and the
	/// count of loops it is inside (<c>0x004763c6</c>), the words below the room left as they lie, as a pop leaves them
	/// (<c>FUN_00475260</c>), and with <see cref="WrittenPerson.SpriteFlags"/> its drawing flags.</summary>
	private static void PutSprite( byte[] record, WrittenPerson person, Sprite picture )
	{
		Put32( record, SpritePcAt, picture.Pc );
		Put32( record, SpriteScriptAt, picture.Script );
		Put32( record, SpriteIntervalAt, person.SpriteInterval );
		PutSingle( record, SpriteX, person.Person.Navigator.X * 10f / NavigatorState.One );
		PutSingle( record, SpriteHeight, picture.Height );
		PutSingle( record, SpriteY, person.Person.Navigator.Y * 10f / NavigatorState.One );
		Put32( record, SpriteAlpha, picture.Alpha );
		Put32( record, SpriteType, picture.Type );
		Put32( record, SpriteBank, picture.Bank );
		Put32( record, SpriteNumberAt, picture.SpriteNumber );
		Put32( record, SpriteFrame, picture.Frame );
		Put32( record, SpriteFacing, picture.Facing );

		if ( person.SpriteFlags is { } flags )
			Put32( record, SpriteFlagsAt, flags );

		if ( person.SpriteLoops is not { } loops )
			return;

		if ( loops.Count > SpriteLoopDepth )
			throw new InvalidOperationException( $"a sprite inside {loops.Count} loops is past the {SpriteLoopDepth} a record holds" );

		for ( var i = 0; i < loops.Count; ++i )
			Put32( record, SpriteLoopsAt + ((SpriteLoopDepth - 1 - i) * 4), loops[i] );

		Put32( record, SpriteLoopRoomAt, SpriteLoopDepth - loops.Count );
		Put32( record, SpriteLoopCountAt, loops.Count );

		// A program inside a loop has run a turn and shown a frame.
		if ( loops.Count > 0 )
		{
			Put32( record, SpriteState, SpriteRunningState );
			Put32( record, SpriteShownAt, 1 );
		}
	}

	/// <summary>Writes a balloon or a bubble over its sprite's record: the program, the interval, the place, alpha, kind, bank, set and frame, and with <see cref="WrittenSprite.Loops"/> its loop stack and state.</summary>
	private static void PutOtherSprite( byte[] record, WrittenSprite other )
	{
		var picture = other.Picture;

		Put32( record, SpritePcAt, picture.Pc );
		Put32( record, SpriteScriptAt, picture.Script );
		Put32( record, SpriteIntervalAt, other.Interval );
		PutSingle( record, SpriteX, picture.X );
		PutSingle( record, SpriteHeight, picture.Height );
		PutSingle( record, SpriteY, picture.Y );
		Put32( record, SpriteAlpha, picture.Alpha );
		Put32( record, SpriteType, picture.Type );
		Put32( record, SpriteBank, picture.Bank );
		Put32( record, SpriteNumberAt, picture.SpriteNumber );
		Put32( record, SpriteFrame, picture.Frame );

		if ( other.Loops is not { } loops )
			return;

		if ( loops.Count > SpriteLoopDepth )
			throw new InvalidOperationException( $"a sprite inside {loops.Count} loops is past the {SpriteLoopDepth} a record holds" );

		for ( var i = 0; i < loops.Count; ++i )
			Put32( record, SpriteLoopsAt + ((SpriteLoopDepth - 1 - i) * 4), loops[i] );

		Put32( record, SpriteLoopRoomAt, SpriteLoopDepth - loops.Count );
		Put32( record, SpriteLoopCountAt, loops.Count );
		Put32( record, SpriteState, other.Ended ? SpriteEndedState : SpriteRunningState );
		Put32( record, SpriteShownAt, other.Ended ? 0 : 1 );
	}

	/// <summary>
	/// Writes a person over their record, at the offsets <see cref="ReadPerson"/>, <see cref="ReadNavigator"/>,
	/// <see cref="ReadGuest"/> and <see cref="ReadStaff"/> read them at (FileFormats <c>saves.md</c>, "The person
	/// base"). What is not named here is left as the record has it: the file's on a kept person, nought on a made
	/// one.
	/// </summary>
	private static void PutPerson( byte[] record, WrittenPerson written, int slot, bool made, int balloonSlot, int bubbleSlot )
	{
		var person = written.Person;
		var navigator = person.Navigator;

		Put32( record, 4, person.Model );
		Put16( record, 8, person.RawX );                         // mX
		Put16( record, 10, person.RawY );                        // mY
		Put32( record, PersonSpriteSlotAt, slot );               // mSpriteScript
		Put32( record, 20, written.NextAnim );                   // mNextAnim
		Put32( record, 24, written.NextServiceInterval );        // mNextServiceInterval
		Put16( record, 28, (navigator.TargetX >> 8) & 0xffff );  // mAccurateDestX: the target, in mX's units
		Put16( record, 30, (navigator.TargetY >> 8) & 0xffff );  // mAccurateDestY
		Put32( record, 37, person.SpriteKind );                  // mESPSprite

		if ( written.LastRecordedMapId is { } recorded )
			Put16( record, 41, recorded );                       // mLastRecordedMapId

		Put32( record, 224, written.PreviousX );                 // mPreviousX, the navigator's fixed point
		Put32( record, 228, written.PreviousY );                 // mPreviousY
		Put32( record, 232, (int)written.StrandedTime );         // mStrandedTime

		if ( written.SetDestSuccessfully is { } setDest )
			Put32( record, 238, setDest ? 1 : 0 );               // mSetDestSuccessfully

		Put32( record, 242, person.Angle & 0x7ff );              // mSpriteAngle
		Put32( record, 246, person.SpriteBank );                 // mSpriteID
		Put32( record, 386, written.LastThought );               // mLastThought
		Put32( record, ThoughtScriptAt, bubbleSlot );            // mThoughtScript
		Put32( record, 394, written.TimeBubbleShown );           // mTimeBubbleShown

		if ( person.Pace is { } pace )
		{
			Put16( record, 32, pace.AdjustorSpeed );             // mAdjustorSpeed
			Put16( record, 34, pace.BaseSpeed );                 // mBaseSpeed
			PutSingle( record, 220, pace.PreviousSpeed );        // mPreviousSpeed
			Put16( record, 236, pace.PurposeSpeed );             // mPurposeSpeed
		}

		// The navigator, at 43. Its force, formation, axes, mode, last progress and timestamp are the record's.
		if ( made )
		{
			Put32( record, 75, navigator.Mass );
			Put32( record, 148, navigator.Radius );
		}

		Put32( record, 79, navigator.MaxForce );
		Put32( record, 83, navigator.MaxSpeed );
		Put32( record, 87, navigator.CantReachDest );
		Put32( record, 95, navigator.PathBufferCount );
		Put32( record, 99, navigator.PathCount );
		record[103] = (byte)(navigator.PathFinished ? 1 : 0);
		Put32( record, 108, navigator.StuckBits );
		Put32( record, 112, navigator.BufferedDistance );
		Put32( record, 116, navigator.TailDistance );
		Put32( record, 120, navigator.TargetX );
		Put32( record, 124, navigator.TargetY );
		Put32( record, 132, navigator.PathTotalCount );
		Put32( record, 136, navigator.TotalDistance );
		Put32( record, 140, navigator.X );
		Put32( record, 144, navigator.Y );
		Put32( record, 212, navigator.VelocityX );
		Put32( record, 216, navigator.VelocityY );

		// subpath_buffer[i] and subpath_dist[i], twelve bytes a slot from 152: a leg's length sits with the waypoint
		// it starts from, and only the legs between waypoints are written, as the original's SetDest leaves them.
		if ( written.Waypoints is { } waypoints )
		{
			for ( var i = 0; i < waypoints.Count && i < NavigatorState.SubpathSlots; ++i )
			{
				Put32( record, SubpathAt + (12 * i), waypoints[i].X );
				Put32( record, SubpathAt + 4 + (12 * i), waypoints[i].Y );

				if ( written.LegLengths is { } legs && i < legs.Count )
					Put32( record, SubpathAt + 8 + (12 * i), legs[i] );
			}
		}

		if ( person.Guest is { } guest )
		{
			record[36] = (byte)guest.WalkingTurns;               // mCount
			Put32( record, 398, guest.ArrivalDate );
			Put32( record, 402, guest.ArrivalIndex );            // mArrivalIndex
			Put32( record, BalloonScriptAt, balloonSlot );       // mBalloonScript
			Put32( record, 410, guest.BeenAdmitted );
			Put32( record, 414, guest.Cash );
			Put32( record, 418, guest.ExitLevel );
			PutSingle( record, 422, guest.Happiness );
			PutSingle( record, 426, guest.Hunger );
			PutSingle( record, 430, guest.LastPosX );
			PutSingle( record, 434, guest.LastPosY );
			PutSingle( record, 438, guest.Litter );
			Put16( record, 442, guest.MajorDest );
			Put32( record, 444, guest.NumRides );
			Put32( record, 448, guest.NumShops );
			Put32( record, 452, guest.NumSideshows );
			Put32( record, 456, guest.NumSideshowsWon );
			Put32( record, 460, guest.PaidAdmission );
			Put32( record, 464, guest.ParkOpeningWait );
			record[468] = (byte)guest.PersonType;
			record[469] = (byte)guest.PrankeryIndex;

			for ( var i = 0; i < GuestState.Remembered; ++i )
			{
				Put16( record, 470 + (4 * i), guest.PreviousRides is { } rides && i < rides.Count ? rides[i] : 0 );
				Put16( record, 472 + (4 * i), guest.PreviousTemporaryRides is { } refusals && i < refusals.Count ? refusals[i] : 0 );
			}

			Put16( record, 486, guest.QNext );
			Put16( record, 488, guest.QPrev );
			Put32( record, 490, guest.QueueMoveDelay );
			record[494] = (byte)guest.QueuePos;
			Put32( record, 495, guest.RemainingBalloonLife );
			Put16( record, 499, guest.SavedMajorDest );
			Put32( record, 501, guest.SavedState );
			Put32( record, 505, guest.State );
			PutSingle( record, 509, guest.Thirst );
			Put32( record, 513, guest.TimeOfLastSpotAnim );
			Put32( record, 517, guest.TimeStartedIdling );
			PutSingle( record, 525, guest.Toilet );
			PutSingle( record, 529, guest.Vomit );
		}
		else if ( person.Staff is { } staff )
		{
			Put32( record, 398, staff.PayGrade );
			PutSingle( record, 402, staff.Happiness );
			Put32( record, 406, staff.JobsDone );

			for ( var i = 0; i < StaffNameLength; ++i )
				Put16( record, 410 + (2 * i), i < staff.Name.Length && i < StaffNameLength - 1 ? staff.Name[i] : 0 );

			Put16( record, 476, staff.PatrolBottomLeft );
			Put16( record, 478, staff.PatrolTopRight );
			record[480] = (byte)staff.PercentageThroughGrade;
			Put16( record, 481, staff.RestArea );
			Put32( record, 483, staff.State );
			Put32( record, 487, staff.TimeStartedIdling );
			BinaryPrimitives.WriteInt64LittleEndian( record.AsSpan( TimeHiredAt, 8 ), staff.TimeHired );
			PutSingle( record, 499, staff.Tiredness );

			switch ( person.Model )
			{
				case MechanicModel:
					Put32( record, 503, staff.DurationOfRepair );
					Put16( record, 507, staff.ObjectToRepair );
					break;
				case HandymanModel:
					Put32( record, 505, staff.TimeStartedCleaning );
					Put16( record, 509, staff.ToiletToClean );
					break;
				case EntertainerModel:
					Put32( record, 503, staff.TimeStartedEntertaining );
					break;
				case ResearcherModel:
					Put32( record, 503, staff.TimeStartedResearching );
					break;
			}
		}
	}

	private static void Put16( byte[] record, int at, int value ) =>
		BinaryPrimitives.WriteUInt16LittleEndian( record.AsSpan( at, 2 ), unchecked((ushort)value) );

	private static void Put32( byte[] record, int at, int value ) =>
		BinaryPrimitives.WriteInt32LittleEndian( record.AsSpan( at, 4 ), value );

	private static void PutSingle( byte[] record, int at, float value ) =>
		BinaryPrimitives.WriteSingleLittleEndian( record.AsSpan( at, 4 ), value );
}
