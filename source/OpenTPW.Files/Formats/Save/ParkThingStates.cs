namespace OpenTPW;

/// <summary>
/// One animation channel as a park save left it: which role it was running, and how.
/// </summary>
/// <param name="Role">
/// The animation role, or <see cref="ParkThingStates.NoRole"/> where the channel was running nothing.
/// </param>
/// <param name="Entry">Which clip of that role.</param>
/// <param name="Flags">
/// The channel's own flag word. <b>It is not decoration.</b> Bit <c>0x1</c> is loop, and bit
/// <c>0x4</c> is the one that makes a channel hold its last frame rather than count as busy - which is
/// what most of the shipped park's things are saved doing.
/// </param>
/// <param name="Speed">
/// How fast the clip was playing. <b>Saved state, and not always 1.</b> The engine restores it -
/// <c>FUN_004647a0</c> copies this dword onto the channel's <c>+0xc</c>, the field its own debug
/// dumper pointedly cannot name - and in the shipped park it is nought on every idle channel, 1 on
/// fourteen of the fifteen running ones, and <b>1.1 on the Belly Bounce</b>, which is the park's only
/// ride. What a restored speed is worth is bounded and was measured: it survives loop wraps and holds,
/// and the script's next trigger on that channel replaces it with 1 - see <c>ParkRides.Restore</c>.
/// </param>
/// <param name="StartTime">
/// The channel's start stamp <c>+0x10</c>, a reading of the saved clock (<see cref="ParkClock"/>): where the clip
/// began, moved back by any carry into it.
/// </param>
/// <param name="Time">The clip time <c>+0x14</c>, the same clock's reading at the channel's last advance.</param>
/// <param name="NoPauseTime">The stamp <c>+0x18</c>, on the same clock; the save holds the clip time's reading or the clock's own.</param>
/// <param name="QueuedRole">The role queued to play next, <c>+0x24</c>, or <see cref="ParkThingStates.NoRole"/> for none.</param>
/// <param name="QueuedEntry">Its clip, <c>+0x28</c>.</param>
/// <param name="QueuedFlags">
/// The flags it was queued with, <c>+0x2c</c>: a caller's flags, not the channel's own word. The engine leaves them
/// behind when the queue empties, so a channel with nothing queued can still carry some.
/// </param>
/// <param name="QueuedSpeed">The speed it was queued at, <c>+0x30</c>.</param>
public readonly record struct SavedChannel( int Role, int Entry, int Flags, float Speed,
	uint StartTime, uint Time, uint NoPauseTime, int QueuedRole, int QueuedEntry, int QueuedFlags, float QueuedSpeed );

/// <summary>
/// One thing as a park save left its MODEL: the animation channels it was running.
/// </summary>
/// <param name="CatalogueId">The item definition this model uses; Slot identifies the saved model instance.</param>
/// <param name="Slot">Where it sat in the module, kept so a caller can say which record it took.</param>
/// <param name="Channels">Its channels, in order.</param>
/// <param name="ScriptHandle">
/// The record's <c>0x19</c>: the script handle of the thing it is the model of, an object's
/// <c>mRideScriptHandle</c>; nought for a piece of queue and other scenery.
/// </param>
/// <param name="NodeWords">A flag word a node, as the record holds them (FileFormats <c>saves.md</c>, "The node flag words").</param>
public readonly record struct SavedThing( int CatalogueId, int Slot, SavedChannel[] Channels,
	uint HoardingFlags = 0, float HoardingProgress = 0f, int ScriptHandle = 0, uint[]? NodeWords = null );

/// <summary>
/// One model as a park file's writer takes it (<see cref="ParkThingStates.Put"/>): the slot its record lies in, its
/// channels as they run, each stamp a reading of the clock the file is written under, and the hoarding's seven
/// bits and progress; null leaves the file's hoarding.
/// </summary>
/// <param name="NodeWords">
/// A flag word a node, written over the record's own where the record holds as many; null leaves the file's.
/// </param>
/// <param name="Heads">The riders' heads hung on its nodes; null leaves the record's lookup records the file's.</param>
public readonly record struct WrittenModel( int Slot, SavedChannel[] Channels, uint? HoardingFlags = null,
	float HoardingProgress = 0f, IReadOnlyList<uint>? NodeWords = null, WrittenHeads? Heads = null );

/// <summary>
/// The riders' heads on a model's nodes, as a park file's writer takes them (<c>docs/exe/saves.md</c>,
/// "OpenTPW's writer, a rider's head"). A head is a sprite of its own, and its node's lookup record holds the
/// sprite's slot (<c>FUN_0044b410</c>).
/// </summary>
/// <param name="Records">Every lookup record a head of the script's head table can hang on, by its place in the model's lookup table.</param>
/// <param name="Hung">Each head hung: its lookup record, and the visitor whose head it is.</param>
public readonly record struct WrittenHeads( IReadOnlyList<int> Records, IReadOnlyList<(int Record, int Visitor)> Hung );

/// <summary>
/// The <c>RSYS</c> module of a park save: what every thing's model was doing when it was saved.
///
/// <para>
/// <b>Why this exists, and why it is not optional.</b> <see cref="ParkScriptStates"/> restores where a
/// script had got to, which is what stops a loaded park replaying the clip that built everything in it.
/// On its own that is a net loss: a thing whose steady-state loop contains no animation instruction
/// never reaches the <c>LOOPANIM</c> in its prologue again, so it stands frozen for the whole session.
/// The Fountain Feature is the case that shows it - its script is
/// <c>NAME / TRIGANIM / WAIT / ADDOBJ / WAIT4ANIM / LOOPANIM 5 0 / ENDSLICE / BRANCH</c>, saved at the
/// <c>ENDSLICE</c>, and the two-instruction loop it resumes into can never get back to that
/// <c>LOOPANIM</c>. The original loses nothing because it restores BOTH halves: <c>FUN_005597a0</c>
/// puts the script back, and <c>FUN_004647a0</c> - this module - puts the channels back.
/// </para>
///
/// <para>
/// <b>Measured, not assumed.</b> Of the fourteen placed things in Lost Kingdom, <b>ten</b> are saved
/// holding exactly the role the game settles them into when its scripts are run from the beginning -
/// the Fountain on role 5, the three Toilets on role 5, the Jungle Spray on role 2 across all three of
/// its lanes, the Traffic Lights looping role 5. <b>The four that differ</b> - the two Cameras, the
/// Staff Room and the Bus - are things whose scripts cycle roles, so a saved snapshot is simply a
/// different moment.
/// </para>
///
/// <para>
/// <b>The per-node flag words are not laid over a loaded model, and that is a deviation.</b> The engine
/// restores them here too - bit <c>0x10</c> is the one that hides a node - so the original gets a
/// built item's visibility from the save. This does not: <c>ParkObjects.PoseAsBuilt</c> works it out
/// from the last frame of the construction clip instead, which is a stand-in for the same answer and
/// is where that decision is written down. The writer keeps them as clips start and writes them
/// (<see cref="SavedThing.NodeWords"/>, <see cref="ParkModelTables.Running"/>).
/// </para>
///
/// <para>
/// <b>The walk needs one thing the module does not carry: how many channels each thing has.</b> That is
/// the item's own <c>NumSimultAnims</c> - the Jungle Spray runs three lanes and everything else one -
/// so a lookup is passed in. It is load-bearing rather than cosmetic: walked with a single channel for
/// everything the cursor lands 5,786 bytes short of the end, and with the real counts it lands exactly
/// on it, across 161 records.
/// </para>
///
/// <para>
/// <b>A record names its thing by the thing's script.</b> The module's own records carry an item id, not a thing
/// id, so three Small Toilets are three records of one item; what tells them apart is the script handle at
/// <c>0x19</c>, the object's <c>mRideScriptHandle</c> (<see cref="ForScript"/>; <c>docs/exe/saves.md</c>, "The
/// objects, their scripts and their models").
/// </para>
/// </summary>
public sealed partial class ParkThingStates
{
	/// <summary>The engine's "no animation" sentinel - the same 12 <c>AnimTimeControl</c> uses.</summary>
	public const int NoRole = 12;

	/// <summary>
	/// The tag closing the module before this one, little-endian - so it reads <c>SYSG</c> in a dump.
	/// See <see cref="ParkScriptStates"/> for why the tags read backwards.
	/// </summary>
	private const string PrecedingTrailer = "SYSG";

	/// <summary>
	/// A channel record, in dwords: flags, role, entry, the three time stamps, speed, and the queue's role, entry,
	/// flags and speed - see <see cref="SavedChannel"/>.
	/// </summary>
	private const int ChannelDwords = 11;

	/// <summary>Where a present record's own fields begin - a one-byte tag leads, so everything is unaligned.</summary>
	private const int IdOffset = 0x01;

	/// <summary>Two counts, as shorts: the model's nodes (one flag word each) and its node-lookup records (eight bytes each,
	/// ahead of the flag words) - FileFormats saves.md, the ride system's record.</summary>
	private const int CountsOffset = 0x2b;

	/// <summary>The lookup records' shared flags, and behind them the count of things attached.</summary>
	private const int SharedOffset = 0x2f;

	/// <summary>A lookup record's runtime flag for something attached to it.</summary>
	public const int LookupAttached = 0x2;

	/// <summary>Where the variable-length tail begins.</summary>
	private const int TailOffset = 0x37;

	private readonly byte[] _data;
	private int _at;

	private readonly List<SavedThing> _things = [];

	/// <summary>Where each present record begins and where its channels do, by slot - what <see cref="Put"/> writes over.</summary>
	private readonly Dictionary<int, (int Record, int Channels)> _places = [];

	/// <summary>Where the module's length lies, and each slot's bytes in order, an empty slot's one byte among them - what <see cref="Splice"/> rebuilds.</summary>
	private int _moduleAt = -1;

	private readonly List<(int At, int End, bool Present)> _extents = [];

	/// <summary>The module's three counts: the present slots, the empty ones, and the cursor, the slot the next model made takes.</summary>
	public (int Present, int Empty, int Cursor) Header { get; private set; }

	/// <summary>The record's script handle.</summary>
	private const int ScriptHandleOffset = 0x19;

	/// <summary>The packed model flags, whose low seven bits are the hoarding's.</summary>
	private const int FlagsOffset = 0x1d;

	/// <summary>The hoarding's progress, a float.</summary>
	private const int ProgressOffset = 0x23;

	private const uint HoardingBits = 0x7f;

	/// <summary>Why the read stopped, or null. Recorded rather than thrown - see <see cref="ParkScriptStates"/>.</summary>
	public string? Problem { get; private set; }

	/// <summary>Whether the walk ended exactly on the module's end, which is its own check.</summary>
	public bool ClosedOnEnd { get; private set; }

	/// <summary>How many slots the module says it holds, present and free together.</summary>
	public int Slots { get; private set; }

	/// <summary>Every present record, in the order the module holds them.</summary>
	public IReadOnlyList<SavedThing> Things => _things;

	/// <summary>
	/// Reads the module out of the inflated payload.
	/// </summary>
	/// <param name="channelsFor">
	/// How many animation channels a given catalogue id's item runs at once - its
	/// <c>NumSimultAnims</c>. Answer 1 for anything unknown, which is what the engine floors it to.
	/// </param>
	public ParkThingStates( byte[] inflatedPayload, Func<int, int> channelsFor )
	{
		_data = inflatedPayload ?? throw new ArgumentNullException( nameof( inflatedPayload ) );

		ArgumentNullException.ThrowIfNull( channelsFor );

		try
		{
			Walk( channelsFor );
		}
		catch ( Exception e )
		{
			Problem ??= e.Message;
		}
	}

	/// <summary>
	/// The saved record for the <paramref name="ordinal"/>-th thing of this catalogue id, or null where
	/// the module holds no such record - see the class remarks for why the ordinal is how they are told
	/// apart, and what that does and does not prove.
	/// </summary>
	public SavedThing? For( int catalogueId, int ordinal )
	{
		var seen = 0;

		foreach ( var thing in _things )
		{
			if ( thing.CatalogueId != catalogueId )
				continue;

			if ( seen++ == ordinal )
				return thing;
		}

		return null;
	}

	/// <summary>
	/// The saved record of the model whose thing runs the script with this handle, or null where no record names
	/// it. Nought names no thing: a piece of queue and other scenery hold it.
	/// </summary>
	public SavedThing? ForScript( int handle )
	{
		if ( handle == 0 )
			return null;

		foreach ( var thing in _things )
		{
			if ( thing.ScriptHandle == handle )
				return thing;
		}

		return null;
	}

	/// <summary>
	/// Writes each model's channels, and its hoarding where one is given, over the record in its slot of
	/// <paramref name="body"/>, a copy of the payload this was read from. A channel is the record's eleven dwords in
	/// the file's order (<see cref="ReadThing"/>). A model whose slot holds no record, or one of another count of
	/// channels, is left as the file's. The hoarding's bits go into the low seven of the packed flags, the rest kept.
	/// </summary>
	/// <returns>How many records were written over.</returns>
	/// <exception cref="InvalidOperationException">The module was not read whole, so no record's place is known.</exception>
	/// <param name="headSprites">
	/// The sprite slot of each head hung, by its model's slot and its lookup record (<see cref="WrittenModel.Heads"/>):
	/// each such record is written <c>0x2</c> and the slot, every other record of <see cref="WrittenHeads.Records"/>
	/// as <c>FUN_0044b4c0</c> leaves one whose head is taken off, and the shared <c>0x4</c> and the count of things
	/// attached follow. Null leaves every record's lookup records the file's.
	/// </param>
	public int Put( byte[] body, IEnumerable<WrittenModel> models,
		IReadOnlyDictionary<(int Slot, int Record), int>? headSprites = null )
	{
		ArgumentNullException.ThrowIfNull( body );
		ArgumentNullException.ThrowIfNull( models );

		if ( Problem != null || !ClosedOnEnd )
			throw new InvalidOperationException( $"the park file's models were not read whole: {Problem}" );

		var written = 0;

		foreach ( var model in models )
		{
			if ( !_places.TryGetValue( model.Slot, out var place ) )
				continue;

			var saved = _things.Find( thing => thing.Slot == model.Slot );

			if ( saved.Channels.Length != model.Channels.Length )
				continue;

			for ( var index = 0; index < model.Channels.Length; ++index )
			{
				var channel = model.Channels[index];
				var at = place.Channels + (index * ChannelDwords * 4);

				PutInt32( body, at, channel.Flags );
				PutInt32( body, at + 4, channel.Role );
				PutInt32( body, at + 8, channel.Entry );
				PutInt32( body, at + 12, (int)channel.StartTime );
				PutInt32( body, at + 16, (int)channel.Time );
				PutInt32( body, at + 20, (int)channel.NoPauseTime );
				PutInt32( body, at + 24, BitConverter.SingleToInt32Bits( channel.Speed ) );
				PutInt32( body, at + 28, channel.QueuedRole );
				PutInt32( body, at + 32, channel.QueuedEntry );
				PutInt32( body, at + 36, channel.QueuedFlags );
				PutInt32( body, at + 40, BitConverter.SingleToInt32Bits( channel.QueuedSpeed ) );
			}

			if ( model.NodeWords is { } words && words.Count == ReadInt16At( place.Record + CountsOffset ) )
			{
				var at = place.Record + TailOffset + (ReadInt16At( place.Record + CountsOffset + 2 ) * 8);

				for ( var index = 0; index < words.Count; ++index )
					PutInt32( body, at + (index * 4), (int)words[index] );
			}

			if ( model.Heads is { } heads && headSprites != null )
			{
				var count = ReadInt16At( place.Record + CountsOffset + 2 );
				var at = place.Record + TailOffset;

				foreach ( var record in heads.Records.Where( record => record >= 0 && record < count ) )
				{
					PutInt32( body, at + (record * 8), ReadInt32At( at + (record * 8) ) & ~LookupAttached );
					PutInt32( body, at + (record * 8) + 4, NothingAttached );
				}

				foreach ( var (record, _) in heads.Hung.Where( head => head.Record >= 0 && head.Record < count ) )
				{
					if ( !headSprites.TryGetValue( (model.Slot, record), out var sprite ) )
						continue;

					PutInt32( body, at + (record * 8), ReadInt32At( at + (record * 8) ) | LookupAttached );
					PutInt32( body, at + (record * 8) + 4, sprite );
				}

				// Written back through the body: the record's own bytes are what count now.
				var attached = 0;

				for ( var record = 0; record < count; ++record )
				{
					if ( (System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian( body.AsSpan( at + (record * 8), 4 ) ) & LookupAttached) != 0 )
						++attached;
				}

				var shared = ReadInt32At( place.Record + SharedOffset ) & ~SharedAttached;

				PutInt32( body, place.Record + SharedOffset, shared | (attached > 0 ? SharedAttached : 0) );
				PutInt32( body, place.Record + SharedOffset + 4, attached );
			}

			if ( model.HoardingFlags is { } hoarding )
			{
				var packed = (uint)ReadInt32At( place.Record + FlagsOffset );

				PutInt32( body, place.Record + FlagsOffset, (int)((packed & ~HoardingBits) | (hoarding & HoardingBits)) );
				PutInt32( body, place.Record + ProgressOffset, BitConverter.SingleToInt32Bits( model.HoardingProgress ) );
			}

			++written;
		}

		return written;
	}

	/// <summary>The lookup records' shared flag for a model with something attached (<c>FUN_0044b410</c>).</summary>
	public const int SharedAttached = 0x4;

	/// <summary>The handle <c>FUN_0044b4c0</c> stores on a lookup record as what it held is taken off.</summary>
	public const int NothingAttached = -1;

	/// <summary>
	/// The sprite slots the file's record in <paramref name="slot"/> names on <paramref name="records"/>, the
	/// lookup records carrying <c>0x2</c> among them, by record; empty where the slot holds none.
	/// </summary>
	public IReadOnlyDictionary<int, int> AttachedOn( int slot, IEnumerable<int>? records = null )
	{
		var held = new Dictionary<int, int>();

		if ( LookupsOf( slot ) is not { } lookups )
			return held;

		foreach ( var record in records ?? Enumerable.Range( 0, lookups.Records.Length ) )
		{
			if ( record >= 0 && record < lookups.Records.Length && (lookups.Records[record].Flags & LookupAttached) != 0 )
				held[record] = lookups.Records[record].Handle;
		}

		return held;
	}

	/// <summary>
	/// The lookup records of the record in <paramref name="slot"/> as the file holds them, each its runtime flags
	/// and the handle of what is attached, with their shared flags and the count of things attached; null where
	/// the slot holds no record.
	/// </summary>
	public ((int Flags, int Handle)[] Records, int Shared, int Attached)? LookupsOf( int slot )
	{
		if ( !_places.TryGetValue( slot, out var place ) )
			return null;

		var records = new (int Flags, int Handle)[ReadInt16At( place.Record + CountsOffset + 2 )];

		for ( var index = 0; index < records.Length; ++index )
			records[index] = (ReadInt32At( place.Record + TailOffset + (index * 8) ), ReadInt32At( place.Record + TailOffset + (index * 8) + 4 ));

		return (records, ReadInt32At( place.Record + SharedOffset ), ReadInt32At( place.Record + SharedOffset + 4 ));
	}

	private static void PutInt32( byte[] body, int at, int value ) =>
		System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian( body.AsSpan( at, 4 ), value );

	private void Walk( Func<int, int> channelsFor )
	{
		var start = FindModule();

		_at = start;

		_moduleAt = start;

		var bytes = ReadInt32();
		var body = _at;
		var end = body + bytes;

		if ( bytes < 12 || end > _data.Length )
			throw new InvalidDataException( $"the ride module says it is {bytes} bytes" );

		var placed = ReadInt32();
		var free = ReadInt32();

		Header = (placed, free, ReadInt32());

		Slots = placed + free;

		if ( placed < 0 || free < 0 || Slots > _data.Length )
			throw new InvalidDataException( $"the ride module says {placed} placed and {free} free" );

		for ( var slot = 0; slot < Slots && _at < end; ++slot )
		{
			// An ABSENT slot costs exactly one byte. The engine computes the next cursor before it tests
			// the tag, so a slot that holds nothing advances by the tag alone - which is what makes 161
			// present records fit in a module with far more slots than that.
			var at = _at;

			if ( _data[_at] != 1 )
			{
				++_at;
				_extents.Add( (at, _at, false) );
				continue;
			}

			ReadThing( slot, channelsFor );
			_extents.Add( (at, _at, true) );
		}

		// The one check that says the whole walk was right: the engine writes this module's length and
		// then a tag, so a walk that stops anywhere else has misread a record somewhere behind it.
		ClosedOnEnd = _at == end;

		if ( !ClosedOnEnd )
			throw new InvalidDataException( $"the walk ended at {_at} and the module ends at {end}" );
	}

	private void ReadThing( int slot, Func<int, int> channelsFor )
	{
		var record = _at;
		var catalogueId = ReadInt32At( record + IdOffset );

		var flagWords = ReadInt16At( record + CountsOffset );
		var before = ReadInt16At( record + CountsOffset + 2 );

		if ( flagWords < 0 || before < 0 )
			throw new InvalidDataException( $"slot {slot} declares {flagWords} and {before} nodes" );

		// The tail is each lookup record's runtime flags and attached handle, then one flag word per node, then the channels.
		var words = new uint[flagWords];

		for ( var index = 0; index < flagWords; ++index )
			words[index] = (uint)ReadInt32At( record + TailOffset + (before * 2 * 4) + (index * 4) );

		_at = record + TailOffset + (before * 2 * 4) + (flagWords * 4);

		var count = Math.Max( channelsFor( catalogueId ), 1 );
		var channels = new SavedChannel[count];

		_places[slot] = (record, _at);

		for ( var index = 0; index < count; ++index )
		{
			var at = _at + (index * ChannelDwords * 4);

			// All eleven, in the record's order, which is not the channel's own field order: FUN_004647a0
			// copies them onto the fourteen-dword channel's +0x00, +0x04, +0x08, +0x10, +0x14, +0x18, +0x0c
			// and +0x24 to +0x30 (0x00464bcb..0x00464c17), so the seventh is the speed.
			channels[index] = new SavedChannel(
				Role: ReadInt32At( at + 4 ),
				Entry: ReadInt32At( at + 8 ),
				Flags: ReadInt32At( at ),
				Speed: ReadSingleAt( at + 24 ),
				StartTime: (uint)ReadInt32At( at + 12 ),
				Time: (uint)ReadInt32At( at + 16 ),
				NoPauseTime: (uint)ReadInt32At( at + 20 ),
				QueuedRole: ReadInt32At( at + 28 ),
				QueuedEntry: ReadInt32At( at + 32 ),
				QueuedFlags: ReadInt32At( at + 36 ),
				QueuedSpeed: ReadSingleAt( at + 40 ) );
		}

		_at += count * ChannelDwords * 4;

		if ( _at > _data.Length )
			throw new InvalidDataException( $"slot {slot}'s channels run past the end of the payload" );

		// FUN_004647a0 maps the packed RSYS word to model bits 0x20..0x800 and restores +0xb8.
		_things.Add( new SavedThing( catalogueId, slot, channels,
			(uint)ReadInt32At( record + FlagsOffset ) & HoardingBits, ReadSingleAt( record + ProgressOffset ),
			ReadInt32At( record + ScriptHandleOffset ), words ) );
	}

	/// <summary>The module's start, found the same way and for the same reason as the script module's.</summary>
	private int FindModule()
	{
		var tag = System.Text.Encoding.ASCII.GetBytes( PrecedingTrailer );

		for ( var at = 0; at + tag.Length + 4 <= _data.Length; ++at )
		{
			if ( _data[at] != tag[0] )
				continue;

			if ( System.Text.Encoding.ASCII.GetString( _data, at, tag.Length ) == PrecedingTrailer )
				return at + tag.Length;
		}

		throw new InvalidDataException( $"no {PrecedingTrailer} tag anywhere in {_data.Length} bytes" );
	}

	private void Skip( int count )
	{
		if ( count < 0 || _at + count > _data.Length )
			throw new InvalidDataException( $"a {count}-byte field runs past the end of the payload" );

		_at += count;
	}

	private int ReadInt32()
	{
		var value = ReadInt32At( _at );

		_at += 4;

		return value;
	}

	private int ReadInt32At( int offset )
	{
		if ( offset < 0 || offset + 4 > _data.Length )
			throw new InvalidDataException( $"a dword at 0x{offset:x} runs past the end of the payload" );

		return BitConverter.ToInt32( _data, offset );
	}

	private short ReadInt16At( int offset )
	{
		if ( offset < 0 || offset + 2 > _data.Length )
			throw new InvalidDataException( $"a word at 0x{offset:x} runs past the end of the payload" );

		return BitConverter.ToInt16( _data, offset );
	}

	private float ReadSingleAt( int offset )
	{
		if ( offset < 0 || offset + 4 > _data.Length )
			throw new InvalidDataException( $"a float at 0x{offset:x} runs past the end of the payload" );

		return BitConverter.ToSingle( _data, offset );
	}
}
