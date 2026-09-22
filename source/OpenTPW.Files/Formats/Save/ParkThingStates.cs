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
public readonly record struct SavedChannel( int Role, int Entry, int Flags );

/// <summary>
/// One thing as a park save left its MODEL: the animation channels it was running.
/// </summary>
/// <param name="CatalogueId">The item this thing is, which is how a record is matched to a thing.</param>
/// <param name="Slot">Where it sat in the module, kept so a caller can say which record it took.</param>
/// <param name="Channels">Its channels, in order.</param>
public readonly record struct SavedThing( int CatalogueId, int Slot, SavedChannel[] Channels );

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
/// <b>Measured, not assumed.</b> Of the fourteen placed things in Lost Kingdom, eleven are saved
/// holding exactly the role the game settles them into when its scripts are run from the beginning -
/// the Fountain on role 5, the three Toilets on role 5, the Jungle Spray on role 2 across all three of
/// its lanes, the Traffic Lights looping role 5. The three that differ (the two Cameras, the Staff Room
/// and the Bus) are things whose scripts cycle roles, so a saved snapshot is simply a different moment.
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
/// <b>Records are matched to things by catalogue id and order, which is measured rather than decoded.</b>
/// The module's own records carry an item id, not a thing id, so three Small Toilets are three records
/// that read alike. In the shipped park the records of placed things appear in ascending script-handle
/// order, which pairs them off - and <b>within one catalogue id the pairing is unobservable</b>, because
/// those records' channels are identical. Anything relying on telling two Toilets apart would need the
/// thing handle decoded first; nothing here does.
/// </para>
/// </summary>
public sealed class ParkThingStates
{
	/// <summary>The engine's "no animation" sentinel - the same 12 <c>AnimTimeControl</c> uses.</summary>
	public const int NoRole = 12;

	/// <summary>
	/// The tag closing the module before this one, little-endian - so it reads <c>SYSG</c> in a dump.
	/// See <see cref="ParkScriptStates"/> for why the tags read backwards.
	/// </summary>
	private const string PrecedingTrailer = "SYSG";

	/// <summary>A channel record, in dwords: flags, role, entry, then eight this does not read.</summary>
	private const int ChannelDwords = 11;

	/// <summary>Where a present record's own fields begin - a one-byte tag leads, so everything is unaligned.</summary>
	private const int IdOffset = 0x01;

	/// <summary>The two node counts, as shorts: the first counts the flag words, the second a block before them.</summary>
	private const int CountsOffset = 0x2b;

	/// <summary>Where the variable-length tail begins.</summary>
	private const int TailOffset = 0x37;

	private readonly byte[] _data;
	private int _at;

	private readonly List<SavedThing> _things = [];

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

	private void Walk( Func<int, int> channelsFor )
	{
		var start = FindModule();

		_at = start;

		var bytes = ReadInt32();
		var body = _at;
		var end = body + bytes;

		if ( bytes < 12 || end > _data.Length )
			throw new InvalidDataException( $"the ride module says it is {bytes} bytes" );

		var placed = ReadInt32();
		var free = ReadInt32();

		Skip( 4 );                                  // the high-water mark, which nothing here needs

		Slots = placed + free;

		if ( placed < 0 || free < 0 || Slots > _data.Length )
			throw new InvalidDataException( $"the ride module says {placed} placed and {free} free" );

		for ( var slot = 0; slot < Slots && _at < end; ++slot )
		{
			// An ABSENT slot costs exactly one byte. The engine computes the next cursor before it tests
			// the tag, so a slot that holds nothing advances by the tag alone - which is what makes 161
			// present records fit in a module with far more slots than that.
			if ( _data[_at] != 1 )
			{
				++_at;
				continue;
			}

			ReadThing( slot, channelsFor );
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

		// The tail is a block keyed on the second count, then one flag word per node, then the channels.
		_at = record + TailOffset + (before * 2 * 4) + (flagWords * 4);

		var count = Math.Max( channelsFor( catalogueId ), 1 );
		var channels = new SavedChannel[count];

		for ( var index = 0; index < count; ++index )
		{
			var at = _at + (index * ChannelDwords * 4);

			channels[index] = new SavedChannel(
				Role: ReadInt32At( at + 4 ),
				Entry: ReadInt32At( at + 8 ),
				Flags: ReadInt32At( at ) );
		}

		_at += count * ChannelDwords * 4;

		if ( _at > _data.Length )
			throw new InvalidDataException( $"slot {slot}'s channels run past the end of the payload" );

		_things.Add( new SavedThing( catalogueId, slot, channels ) );
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
}
