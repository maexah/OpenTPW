namespace OpenTPW;

/// <summary>
/// One script as a park save left it: where its program counter stood, and what its variables held.
/// </summary>
/// <param name="Handle">
/// The script's own handle, which is what an object record's <c>mRideScriptHandle</c> names - see
/// <c>ParkScriptStateTests.EverySavedScriptIsTheScriptOfSomePlacedThing</c> for how the two were shown to be
/// the same number.
/// </param>
/// <param name="Position">
/// The saved program counter, counted in words from the start of the body exactly as
/// <see cref="RideInstruction.Address"/> is.
/// </param>
/// <param name="BodyWords">
/// How long the script's body is, as the saved struct itself declares it. Kept because it is the
/// bound the original checks the program counter against, so it is what says a saved counter is
/// sane without going back to the <c>.RSE</c> file.
/// </param>
/// <param name="Variables">
/// The script's variables, in the order <see cref="RideScriptFile.VariableNames"/> names them.
/// </param>
public readonly record struct SavedScript( int Handle, int Position, int BodyWords, int[] Variables );

/// <summary>
/// The <c>RSSE</c> module of a park save: every running script's program counter and variables.
///
/// <para>
/// <b>Why this exists.</b> A park loaded from a save must not replay what its things did when they
/// were built, and the original does not suppress that with a guard - it restores each script where
/// it left off. The Belly Bounce is the case that shows it: <c>Bouncy.RSE</c> body <b>word 4</b> is
/// <c>WAITANIM 0 0</c>, which starts role 0, the construction clip, and with every script started
/// from nought the ride hatches out of its egg again on every single load. Its saved counter is 46.
/// (Word 0 is <c>NAME</c> and word 2 <c>BOUNCESETBASE</c>, so word 4 is its third instruction.)
/// </para>
///
/// <para>
/// <b>Where it sits.</b> <see cref="ParkWorld"/> reads the first two of the seventeen modules; this reads
/// the eleventh. <c>FUN_00415270</c> is the restore chain that walks all of them in order, each
/// followed by a four-character tag it checks on the way back in, and <c>FUN_005597a0</c> is the arm
/// that reads this one - the one whose failure logs "RSSE scripts failed to load".
/// </para>
///
/// <para>
/// <b>This one is FOUND rather than walked, and that is a deviation worth naming.</b> Walking to it
/// honestly would mean reading the nine modules in between, only two of which are decoded. Instead it is
/// located by two anchors that have to agree: the <c>FLYR</c> tag closing the module before it -
/// stored little-endian, so it reads <c>RYLF</c> in a dump, the same way
/// <see cref="ParkWorld.Trailer"/> reads <c>DLRW</c> - immediately followed by this module's own
/// <c>RSSE</c> magic, which <c>FUN_005597a0</c> tests as a dword against <c>0x45535352</c> and so is
/// stored forwards. Everything after that IS walked, block by block, and the walk has to land on the
/// literal <c>"OBJ "</c> guard at the end of every record or the whole read is refused.
/// </para>
///
/// <para>
/// <b>Three things were measured before any of this was trusted</b>, against
/// <c>levels/jungle/Easymode.TPWI</c>:
/// </para>
/// <list type="bullet">
/// <item>
/// The struct's dword 20 is <c>+0x50</c>, the body length the original bounds-checks the counter
/// against. It equals the length of the body block that follows it for all fourteen scripts, which
/// is what pins the struct's alignment and therefore the counter at dword 15 (<c>+0x3c</c>).
/// </item>
/// <item>
/// Every one of the fourteen saved counters lands on an exact instruction boundary, and each on a
/// <c>BRANCH</c>, <c>BRANCH_Z</c>, <c>TEST</c> or <c>WAIT</c> - what a settled script waits on. That
/// matters because an unknown position stops a script dead rather than erring, so
/// <see cref="RideScript.ResumeAt"/> refuses one it cannot find.
/// </item>
/// <item>
/// The variable block is the second after the body, which is the one <c>FUN_005597a0</c> counts into
/// field <c>+0x23</c> - the loader's own variable count. Its slot 2 is <c>VAR_CAPACITY</c> and slot 3
/// <c>VAR_DURATION</c>, and they agree with the <i>object</i> records, a wholly separate part of the
/// file: 5 and 30 for the Belly Bounce, and 1, 1, 1 and 3 for the three toilets and the sideshow.
/// </item>
/// </list>
///
/// <para>
/// <b>What is deliberately not read.</b> The original restores a great deal more per script - the
/// wait deadlines, the call stack, the limbo, bounce and walk tables, the string blob and a run of
/// 32-byte records - and the struct's other fields with them. Only the counter, the body length and
/// the variables are taken, because they are the three this program models; the rest are stepped over
/// by length so that the walk still has to add up. A script's <i>name</i> is not in the struct at all
/// and is recovered a different way - see <see cref="RideScript.TakeDeclaredName"/>.
/// </para>
/// </summary>
public sealed class ParkScriptStates
{
	/// <summary>
	/// The tag closing the module before this one, little-endian - so it reads <c>RYLF</c> in a dump.
	/// </summary>
	private const string PrecedingTrailer = "RYLF";

	/// <summary>
	/// This module's own magic. <c>FUN_005597a0</c> compares a dword against <c>0x45535352</c>, which
	/// puts these four characters the right way round in the file, unlike the tags between modules.
	/// </summary>
	private const string Magic = "RSSE";

	/// <summary>
	/// The guard closing every script's record. <c>FUN_005597a0</c> refuses the load without it,
	/// logging "RSSE: Load Fail - Object list missing", and it is what makes this walk self-checking.
	/// </summary>
	private const string ObjectGuard = "OBJ ";

	/// <summary>
	/// Dwords the original reads and throws away between the module header and the script count.
	/// </summary>
	private const int DiscardedDwords = 5;

	/// <summary>Where the program counter sits in the saved struct - <c>+0x3c</c>, so dword 15.</summary>
	private const int PositionDword = 15;

	/// <summary>
	/// Where the handle sits. Shown to be <c>mRideScriptHandle</c> - see
	/// <c>ParkScriptStateTests.EverySavedScriptIsTheScriptOfSomePlacedThing</c>.
	/// </summary>
	private const int HandleDword = 2;

	/// <summary>Where the body length sits - <c>+0x50</c>, so dword 20.</summary>
	private const int LengthDword = 20;

	/// <summary>
	/// Length-prefixed blocks between a script's body and its run of 32-byte records. The variables
	/// are the second of them.
	/// </summary>
	private const int BlocksAfterBody = 5;

	/// <summary>Which of those blocks holds the variables, counting the body as nought.</summary>
	private const int VariableBlock = 2;

	/// <summary>One record of the 32-byte run each script carries.</summary>
	private const int SubRecordSize = 32;

	private readonly byte[] _data;
	private int _at;

	private readonly Dictionary<int, SavedScript> _byHandle = [];

	/// <summary>
	/// Why the read stopped, or null where it did not. A surprise here is recorded rather than thrown:
	/// a park whose scripts will not read should still be a park, with its things merely starting from
	/// the beginning.
	/// </summary>
	public string? Problem { get; private set; }

	/// <summary>Whether every record ended on its <see cref="ObjectGuard"/>.</summary>
	public bool ClosedOnGuard { get; private set; }

	/// <summary>How many scripts the module says it holds.</summary>
	public int Declared { get; private set; }

	/// <summary>The scripts that were read, by the handle an object record names them with.</summary>
	public IReadOnlyDictionary<int, SavedScript> ByHandle => _byHandle;

	/// <summary>
	/// Reads the module out of the inflated payload - what <see cref="SaveReader.ReadFile"/> hands
	/// back, and the same array <see cref="ParkWorld"/> is given.
	/// </summary>
	public ParkScriptStates( byte[] inflatedPayload )
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

	/// <summary>The saved state of the script an object record names, or null where there is none.</summary>
	public SavedScript? For( int handle )
		=> _byHandle.TryGetValue( handle, out var saved ) ? saved : null;

	private void Walk()
	{
		_at = FindModule();

		Skip( Magic.Length );

		// The header block, whose contents are the subsystem's own globals rather than anything about a
		// script, so it is stepped over by its own length.
		Skip( ReadInt32() );
		Skip( DiscardedDwords * 4 );

		Declared = ReadInt32();

		var structSize = ReadInt32();

		if ( Declared < 0 || structSize < (LengthDword + 1) * 4 )
			throw new InvalidDataException( $"the script module says {Declared} scripts of {structSize} bytes" );

		for ( var script = 0; script < Declared; ++script )
			ReadScript( structSize );

		ClosedOnGuard = true;
	}

	/// <summary>
	/// The module's start, agreed by two anchors rather than searched for with one - see the class
	/// remarks for why it is found at all.
	/// </summary>
	private int FindModule()
	{
		var tag = System.Text.Encoding.ASCII.GetBytes( PrecedingTrailer );

		for ( var at = 0; at + tag.Length + Magic.Length <= _data.Length; ++at )
		{
			if ( _data[at] != tag[0] )
				continue;

			if ( System.Text.Encoding.ASCII.GetString( _data, at, tag.Length ) != PrecedingTrailer )
				continue;

			if ( System.Text.Encoding.ASCII.GetString( _data, at + tag.Length, Magic.Length ) == Magic )
				return at + tag.Length;
		}

		throw new InvalidDataException(
			$"no {Magic} module sits after a {PrecedingTrailer} tag anywhere in {_data.Length} bytes" );
	}

	private void ReadScript( int structSize )
	{
		var start = _at;

		var handle = ReadInt32At( start + (HandleDword * 4) );
		var position = ReadInt32At( start + (PositionDword * 4) );
		var length = ReadInt32At( start + (LengthDword * 4) );

		Skip( structSize );

		// The body. Its length is read rather than trusted: where it disagrees with the struct's own
		// figure the record is not what this thinks it is, and going on would write nonsense into a
		// script that is running perfectly well from the beginning.
		var bodyBytes = ReadInt32();

		if ( bodyBytes / 4 != length )
			throw new InvalidDataException(
				$"script handle {handle} declares {length} words and carries {bodyBytes / 4}" );

		Skip( bodyBytes );

		var variables = Array.Empty<int>();

		for ( var block = 1; block <= BlocksAfterBody; ++block )
		{
			var bytes = ReadInt32();

			if ( block == VariableBlock )
				variables = ReadInts( bytes / 4 );
			else
				Skip( bytes );
		}

		Skip( ReadInt32() * SubRecordSize );

		Skip( ReadInt32() );
		Skip( ReadInt32() );

		var guard = ReadString( ObjectGuard.Length );

		if ( guard != ObjectGuard )
			throw new InvalidDataException(
				$"script handle {handle} should have ended on '{ObjectGuard}' and ended on '{guard}'" );

		// The script's own object list, which this does not read.
		var objects = ReadInt32();
		var objectSize = ReadInt32();

		Skip( objects * objectSize );

		// A handle twice over would make For() answer whichever came first, so the second is refused
		// rather than quietly dropped.
		if ( !_byHandle.TryAdd( handle, new SavedScript( handle, position, length, variables ) ) )
			throw new InvalidDataException( $"two saved scripts both call themselves handle {handle}" );
	}

	private int[] ReadInts( int count )
	{
		// Bounded BEFORE the allocation, not by the reads that follow it. Four bytes of the file decide
		// this size, and a corrupt length of 0x20000000 would commit half a gigabyte against a payload of
		// about one and a half megabytes before the first read discovered it was past the end. ParkWorld's own
		// sprite table guards its allocation the same way.
		if ( count < 0 || count > (_data.Length - _at) / 4 )
			throw new InvalidDataException(
				$"a block of {count} dwords, with {_data.Length - _at} bytes of payload left" );

		var values = new int[count];

		for ( var i = 0; i < count; ++i )
			values[i] = ReadInt32();

		return values;
	}

	private string ReadString( int length )
	{
		if ( _at + length > _data.Length )
			throw new InvalidDataException( $"a {length}-byte tag runs past the end of the payload" );

		var text = System.Text.Encoding.ASCII.GetString( _data, _at, length );

		_at += length;

		return text;
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
}
