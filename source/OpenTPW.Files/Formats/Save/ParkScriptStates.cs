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
/// <param name="CallIndex">
/// The call index, <c>+0x40</c>: the slot the next <c>JSR</c> writes, counting down, so the live frames
/// are the slots above it. -1 for a script with no stack.
/// </param>
/// <param name="HeapIndex">The heap index, <c>+0x44</c>: how many values <c>HUSH</c> has pushed.</param>
/// <param name="Result">The result register, <c>+0x48</c>, which the conditional branches test.</param>
/// <param name="Stack">
/// The stack array, the record's first block after the body, one dword per slot. The slots above the call
/// index are open frames, return addresses tagged <c>0x20000000</c>; the rest hold whatever was last written
/// there, a returned call's address included, since nothing clears a slot.
/// </param>
/// <param name="WaitDeadline">
/// <c>+0xa0</c>: the deadline a <c>WAIT</c> or <c>WAITANIM</c> is sitting on, a reading of the saved clock
/// (<see cref="ParkClock"/>); nought for none.
/// </param>
/// <param name="AnimationDeadline">
/// <c>+0xa4</c>: the deadline the last trigger armed, which <c>WAIT4ANIM</c> waits on, a reading of the same clock;
/// nought for none. A save can hold one long past, since passing does not clear it: only a passed <c>WAIT4ANIM</c>,
/// a <c>WAITANIM</c>'s first visit, a <c>LOOPANIM</c> that starts a loop or a <c>LOOPANIM_CH</c> does.
/// </param>
/// <param name="LoopingKey">
/// <c>+0xa8</c>: <c>(entry &lt;&lt; 16) + role</c> of the last <c>LOOPANIM</c>, or <c>0xffff</c> after a one-shot.
/// </param>
/// <param name="AnimationMark"><c>+0xbc</c>: <c>TRIGWAITANIM</c>'s mark, the role it waits for plus one; nought for none.</param>
/// <param name="TimerDeadline"><c>+0xc4</c>: the deadline <c>SETTIMER</c> last set, a reading of the same clock; nought for none.</param>
/// <param name="Heads">
/// The head table, <c>+0x30</c>: the visitor <c>ADDHEAD</c> put on each head node, slot n on node n + 1, nought for a
/// free slot. Its length is the saved block's, which <c>FUN_005597a0</c> takes as the count <c>+0x4c</c>; empty for a
/// script with none. Null only where a record was made without a save.
/// </param>
public readonly record struct SavedScript( int Handle, int Position, int BodyWords, int[] Variables,
	int CallIndex, int HeapIndex, int Result, int[] Stack,
	uint WaitDeadline, uint AnimationDeadline, int LoopingKey, int AnimationMark, uint TimerDeadline,
	int[]? Heads = null, SavedLimboSlot[]? Limbo = null, int InLimbo = 0,
	SavedBounceSlot[]? Bounce = null, int Bouncing = 0, int BounceBase = 0, int BounceNode = 0,
	SavedWalkSlot[]? Walk = null, int Thing = 0, int ModelHandle = 0, SavedEffect[]? Effects = null );

/// <summary>
/// One record of a script's object list, an effect <c>ADDOBJ</c> started (<c>docs/exe/saves.md</c>, "OpenTPW's
/// writer, a made script's started effects"): the type (1 and 2 a particle, 3 to 10 a sound), the handle the spawn
/// answered, the node asked for, that node's lookup record in the model (-1 with no node) and the tag a
/// <c>KILLOBJ</c> matches. The file holds them newest first.
/// </summary>
public readonly record struct SavedEffect( int Type, int Handle, int Node, int Index, int Tag );

/// <summary>One limbo slot, eight bytes of block 4: who is held, nought for a free slot, and the clock reading they are due back at.</summary>
public readonly record struct SavedLimboSlot( int Handle, uint Due );

/// <summary>
/// One bounce slot, sixteen bytes of block 5: who is on it, nought for a free slot, the node they are on, and the clock
/// readings they are due off at and got on at. A slot let go keeps the last three.
/// </summary>
public readonly record struct SavedBounceSlot( int Handle, int Node, uint Due, uint Start );

/// <summary>
/// One walk slot, thirty-two bytes (FileFormats <c>saves.md</c>, "The walk slots"): its four node ids, the clock
/// readings its leg began and is due at, the walker, the action, the state (0 free, 1 walking on, 2 on the ride, 3
/// walking off, 4 off), the flags and the facing, one of eight ways. The dword at <c>+0x1c</c> is not taken.
/// </summary>
public readonly record struct SavedWalkSlot( short WalkNode, short HeadNode, short OffFrom, short OffTo,
	uint Start, uint Due, int Handle, short Action, short State, short Flags, short Facing = 0 );

/// <summary>
/// One running script as a park file's writer takes it (<see cref="ParkScriptStates.Put"/>): what
/// <see cref="SavedScript"/> reads, each deadline and stamp a reading of the clock the file is written under. A table
/// that is null, or not the length the file's record holds, leaves the file's.
/// </summary>
public readonly record struct WrittenScript( int Handle, int Position, int CallIndex, int HeapIndex, int Result,
	int[] Stack, int[] Variables, uint WaitDeadline, uint AnimationDeadline, int LoopingKey, int AnimationMark,
	uint TimerDeadline, SavedLimboSlot[]? Limbo, int InLimbo, SavedBounceSlot[]? Bounce, int Bouncing, int BounceBase,
	int BounceNode, SavedWalkSlot[]? Walk, int[]? Heads, SavedEffect[]? Effects = null );

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
/// <b>What is deliberately not read.</b> The original restores more per script - the string blob and the struct's
/// other fields. The counter, the body length, the variables, the stack with its two
/// indices, the result register, the five fields a clock or an animation keeps (the two wait deadlines, the looping
/// key, <c>TRIGWAITANIM</c>'s mark and the timer), the limbo, bounce and walk slots with their counts, the head
/// table and the object list (<see cref="SavedEffect"/>) are taken, because they are what this program models; the rest are stepped over by length so that the walk
/// still has to add up. A script's <i>name</i> is not in the struct at all
/// and is recovered a different way - see <see cref="RideScript.TakeDeclaredName"/>.
/// </para>
/// </summary>
public sealed partial class ParkScriptStates
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

	/// <summary>The tick counter's place in the header block.</summary>
	private const int TickDword = 1;

	/// <summary>The next handle's place in the header block.</summary>
	private const int NextHandleDword = 2;

	/// <summary>Where the program counter sits in the saved struct - <c>+0x3c</c>, so dword 15.</summary>
	private const int PositionDword = 15;

	/// <summary>The call index - <c>+0x40</c>, so dword 16.</summary>
	private const int CallIndexDword = 16;

	/// <summary>The heap index - <c>+0x44</c>, so dword 17.</summary>
	private const int HeapIndexDword = 17;

	/// <summary>The result register - <c>+0x48</c>, so dword 18.</summary>
	private const int ResultDword = 18;

	/// <summary>
	/// Where the handle sits. Shown to be <c>mRideScriptHandle</c> - see
	/// <c>ParkScriptStateTests.EverySavedScriptIsTheScriptOfSomePlacedThing</c>.
	/// </summary>
	private const int HandleDword = 2;

	/// <summary>Where the body length sits - <c>+0x50</c>, so dword 20.</summary>
	private const int LengthDword = 20;

	/// <summary>The <c>WAIT</c> and <c>WAITANIM</c> deadline - <c>+0xa0</c>, so dword 40.</summary>
	private const int WaitDeadlineDword = 40;

	/// <summary>The trigger's deadline <c>WAIT4ANIM</c> reads - <c>+0xa4</c>, so dword 41.</summary>
	private const int AnimationDeadlineDword = 41;

	/// <summary>The looping key - <c>+0xa8</c>, so dword 42.</summary>
	private const int LoopingKeyDword = 42;

	/// <summary><c>TRIGWAITANIM</c>'s mark - <c>+0xbc</c>, so dword 47.</summary>
	private const int AnimationMarkDword = 47;

	/// <summary><c>SETTIMER</c>'s deadline - <c>+0xc4</c>, so dword 49.</summary>
	private const int TimerDeadlineDword = 49;

	/// <summary>How many limbo slots are taken - <c>+0x60</c>, so dword 24.</summary>
	private const int InLimboDword = 24;

	/// <summary>Two words at <c>+0x6c</c>: how many are bouncing, and <c>BOUNCESETBASE</c>'s value above it.</summary>
	private const int BouncingAt = 0x6c;

	/// <summary>The base node numbers are counted from, which <c>BOUNCESETNODE</c> sets - <c>+0x70</c>, so dword 28.</summary>
	private const int BounceNodeDword = 28;

	/// <summary>The id of the script's thing, a word at <c>+0xac</c>.</summary>
	private const int ThingAt = 0xac;

	/// <summary>The model handle of the script's thing - <c>+0xc8</c>, so dword 50.</summary>
	private const int ModelHandleDword = 50;

	/// <summary>Which block holds limbo, eight bytes a slot, counting the body as nought.</summary>
	private const int LimboBlock = 4;

	/// <summary>Which block holds the bounce slots, sixteen bytes a slot.</summary>
	private const int BounceBlock = 5;

	private const int LimboSlotSize = 8;

	private const int BounceSlotSize = 16;

	/// <summary>
	/// Length-prefixed blocks between a script's body and its run of 32-byte records. The variables
	/// are the second of them.
	/// </summary>
	private const int BlocksAfterBody = 5;

	/// <summary>Which of those blocks holds the stack, counting the body as nought.</summary>
	private const int StackBlock = 1;

	/// <summary>Which of those blocks holds the variables, counting the body as nought.</summary>
	private const int VariableBlock = 2;

	/// <summary>One record of the 32-byte run each script carries.</summary>
	private const int SubRecordSize = 32;

	private readonly byte[] _data;
	private int _at;

	private readonly Dictionary<int, SavedScript> _byHandle = [];
	private readonly List<int> _order = [];

	/// <summary>Where each record's struct and tables lie in the payload, by handle - what <see cref="Put"/> writes over.</summary>
	private readonly Dictionary<int, Place> _places = [];

	/// <summary>A record's struct and the first byte of each table's data; -1 for a table the record lacks.</summary>
	/// <summary>Where each record begins and ends, by handle - what <see cref="Splice"/> takes out.</summary>
	private readonly Dictionary<int, (int Start, int End)> _extents = [];

	/// <summary>Where the count of records lies, where the first record begins, and the struct's size.</summary>
	private int _countAt = -1, _recordsAt = -1, _structSize;

	private readonly record struct Place( int Struct, int Stack, int Variables, int Limbo, int Bounce, int Walk, int Heads, int Effects );

	/// <summary>Where the header block's dwords begin, and how many bytes it holds; -1 where the module did not read.</summary>
	private int _headerAt = -1;

	private int _headerBytes;

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
	/// The handles in the order the module holds them. The engine's reader puts each at the head of its list as it
	/// reads it (<c>0x005599d3</c>), so a loaded park takes its turns within a tick in the reverse of this order.
	/// </summary>
	public IReadOnlyList<int> Order => _order;

	/// <summary>
	/// The scheduler's tick counter as the park was saved, <c>DAT_008791a4</c>: the header block's second dword, which
	/// <c>FUN_005597a0</c> reads straight over the scheduler's globals (<c>0x005598d7</c>). Nought where it did not read.
	/// </summary>
	public int Tick { get; private set; }

	/// <summary>
	/// The handle the next new script will be given, <c>DAT_008791a8</c>: the header block's third dword. Nought where
	/// it did not read.
	/// </summary>
	public int NextHandle { get; private set; }

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

		// The header block: the subsystem's own globals from 0x008791a0, read over them whole - the initialised
		// flag, the tick counter, the next handle, the script count and a stale list pointer.
		// A shorter block leaves the rest of the globals as the reset set them, so it is read as far as it goes.
		var header = ReadInt32();
		var headerAt = _at;

		_headerAt = headerAt;
		_headerBytes = header;

		Skip( header );

		var tick = header > TickDword * 4 ? ReadInt32At( headerAt + (TickDword * 4) ) : 0;
		var nextHandle = header > NextHandleDword * 4 ? ReadInt32At( headerAt + (NextHandleDword * 4) ) : 0;
		Skip( DiscardedDwords * 4 );

		_countAt = _at;
		Declared = ReadInt32();

		var structSize = ReadInt32();

		_structSize = structSize;
		_recordsAt = _at;

		if ( Declared < 0 || structSize < (ModelHandleDword + 1) * 4 )
			throw new InvalidDataException( $"the script module says {Declared} scripts of {structSize} bytes" );

		for ( var script = 0; script < Declared; ++script )
			ReadScript( structSize );

		ClosedOnGuard = true;

		// Only once every record has read, so a module that stops part-way leaves the scheduler starting afresh.
		Tick = tick;
		NextHandle = nextHandle;
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
		var callIndex = ReadInt32At( start + (CallIndexDword * 4) );
		var heapIndex = ReadInt32At( start + (HeapIndexDword * 4) );
		var result = ReadInt32At( start + (ResultDword * 4) );
		var waitDeadline = (uint)ReadInt32At( start + (WaitDeadlineDword * 4) );
		var animationDeadline = (uint)ReadInt32At( start + (AnimationDeadlineDword * 4) );
		var loopingKey = ReadInt32At( start + (LoopingKeyDword * 4) );
		var animationMark = ReadInt32At( start + (AnimationMarkDword * 4) );
		var timerDeadline = (uint)ReadInt32At( start + (TimerDeadlineDword * 4) );

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
		var stack = Array.Empty<int>();
		var limbo = Array.Empty<SavedLimboSlot>();
		var bounce = Array.Empty<SavedBounceSlot>();
		int stackAt = -1, variablesAt = -1, limboAt = -1, bounceAt = -1;

		for ( var block = 1; block <= BlocksAfterBody; ++block )
		{
			var bytes = ReadInt32();

			if ( block == VariableBlock )
			{
				variablesAt = _at;
				variables = ReadInts( bytes / 4 );
			}
			else if ( block == StackBlock )
			{
				stackAt = _at;
				stack = ReadInts( bytes / 4 );
			}
			else if ( block == LimboBlock )
			{
				limboAt = _at;
				limbo = new SavedLimboSlot[Slots( bytes, LimboSlotSize )];

				for ( var slot = 0; slot < limbo.Length; ++slot )
					limbo[slot] = new SavedLimboSlot( ReadInt32(), (uint)ReadInt32() );
			}
			else if ( block == BounceBlock )
			{
				bounceAt = _at;
				bounce = new SavedBounceSlot[Slots( bytes, BounceSlotSize )];

				for ( var slot = 0; slot < bounce.Length; ++slot )
					bounce[slot] = new SavedBounceSlot( ReadInt32(), ReadInt32(), (uint)ReadInt32(), (uint)ReadInt32() );
			}
			else
				Skip( bytes );
		}

		// The walk slots, by their count (FUN_005597a0 reads count << 5 bytes).
		var walk = new SavedWalkSlot[Slots( checked(ReadInt32() * (long)SubRecordSize), SubRecordSize )];
		var walkAt = _at;

		for ( var slot = 0; slot < walk.Length; ++slot )
		{
			var at = _at;

			Skip( SubRecordSize );

			walk[slot] = new SavedWalkSlot(
				WalkNode: ReadInt16At( at ), HeadNode: ReadInt16At( at + 0x02 ),
				OffFrom: ReadInt16At( at + 0x04 ), OffTo: ReadInt16At( at + 0x06 ),
				Start: (uint)ReadInt32At( at + 0x08 ), Due: (uint)ReadInt32At( at + 0x0c ),
				Handle: ReadInt32At( at + 0x10 ), Action: ReadInt16At( at + 0x16 ),
				State: ReadInt16At( at + 0x18 ), Flags: ReadInt16At( at + 0x1a ), Facing: ReadInt16At( at + 0x14 ) );
		}

		// The head table, by its length in bytes (0x00559d3d..0x00559da7), then the script's directory string (+0x38).
		var headBytes = ReadInt32();
		var headsAt = _at;
		var heads = ReadInts( headBytes / 4 );

		Skip( ReadInt32() );

		var guard = ReadString( ObjectGuard.Length );

		if ( guard != ObjectGuard )
			throw new InvalidDataException(
				$"script handle {handle} should have ended on '{ObjectGuard}' and ended on '{guard}'" );

		// The script's own object list (0x00559f8b): a count, a record's size, then the records, newest first.
		var objects = ReadInt32();
		var objectSize = ReadInt32();
		SavedEffect[]? effects = null;
		var effectsAt = -1;

		if ( objectSize == EffectRecordSize )
		{
			effects = new SavedEffect[Slots( checked(objects * (long)EffectRecordSize), EffectRecordSize )];
			effectsAt = _at;

			for ( var index = 0; index < effects.Length; ++index )
			{
				var at = _at;

				Skip( EffectRecordSize );

				effects[index] = new SavedEffect( ReadInt32At( at + 0x08 ), ReadInt32At( at + 0x0c ),
					ReadInt32At( at + 0x10 ), ReadInt32At( at + 0x14 ), ReadInt32At( at + 0x18 ) );
			}
		}
		else
			Skip( objects * objectSize );

		// A handle twice over would make For() answer whichever came first, so the second is refused
		// rather than quietly dropped.
		var saved = new SavedScript( handle, position, length, variables, callIndex, heapIndex, result, stack,
			waitDeadline, animationDeadline, loopingKey, animationMark, timerDeadline, heads,
			limbo, ReadInt32At( start + (InLimboDword * 4) ),
			bounce, ReadInt16At( start + BouncingAt ), ReadInt16At( start + BouncingAt + 2 ),
			ReadInt32At( start + (BounceNodeDword * 4) ), walk,
			(ushort)ReadInt16At( start + ThingAt ), ReadInt32At( start + (ModelHandleDword * 4) ), effects );

		if ( !_byHandle.TryAdd( handle, saved ) )
			throw new InvalidDataException( $"two saved scripts both call themselves handle {handle}" );

		_places[handle] = new Place( start, stackAt, variablesAt, limboAt, bounceAt, walkAt, headsAt, effectsAt );
		_extents[handle] = (start, _at);

		_order.Add( handle );
	}

	/// <summary>How many whole slots a block of <paramref name="bytes"/> holds, refused where it runs past the payload.</summary>
	private int Slots( long bytes, int size )
	{
		if ( bytes < 0 || bytes > _data.Length - _at )
			throw new InvalidDataException( $"a block of {bytes} bytes, with {_data.Length - _at} bytes of payload left" );

		return (int)(bytes / size);
	}

	/// <summary>What <see cref="Put"/> did: the records written over, and the tables left as the file's because the
	/// running script's was another length.</summary>
	public readonly record struct Written( int Scripts, int TablesLeft );

	/// <summary>
	/// Writes the scheduler's tick and next handle over the header's, and each running script over its record in
	/// <paramref name="body"/>, a copy of the payload this was read from: the struct's fields
	/// <see cref="SavedScript"/> reads, and the stack, the variables, limbo, the bounce slots, the walk slots and
	/// the head table where they lie. The record's other bytes stay the file's: the links to other scripts, the
	/// name's offset, the speed word, the play rate, the body and the strings. The object list, the effects the
	/// script has started, is written where it holds as many records as the file's; one of another length is
	/// <see cref="Splice"/>'s (<see cref="Relisted"/>).
	///
	/// <para>
	/// A walk slot's last dword is not written. A free slot of any of the three
	/// tables keeps all but its handle and its state, as the engine's does a slot let go. A script the file holds
	/// no record for is not written.
	/// </para>
	/// </summary>
	/// <exception cref="InvalidOperationException">The module was not read whole, so no record's place is known.</exception>
	public Written Put( byte[] body, int tick, int nextHandle, IEnumerable<WrittenScript> scripts )
	{
		ArgumentNullException.ThrowIfNull( body );
		ArgumentNullException.ThrowIfNull( scripts );

		if ( Problem != null || !ClosedOnGuard || _headerAt < 0 )
			throw new InvalidOperationException( $"the park file's scripts were not read whole: {Problem}" );

		if ( _headerBytes >= (TickDword + 1) * 4 )
			PutInt32( body, _headerAt + (TickDword * 4), tick );

		if ( _headerBytes >= (NextHandleDword + 1) * 4 )
			PutInt32( body, _headerAt + (NextHandleDword * 4), nextHandle );

		var written = 0;
		var left = 0;

		foreach ( var script in scripts )
		{
			if ( !_places.TryGetValue( script.Handle, out var place ) || !_byHandle.TryGetValue( script.Handle, out var saved ) )
				continue;

			var at = place.Struct;

			PutInt32( body, at + (PositionDword * 4), script.Position );
			PutInt32( body, at + (CallIndexDword * 4), script.CallIndex );
			PutInt32( body, at + (HeapIndexDword * 4), script.HeapIndex );
			PutInt32( body, at + (ResultDword * 4), script.Result );
			PutInt32( body, at + (WaitDeadlineDword * 4), (int)script.WaitDeadline );
			PutInt32( body, at + (AnimationDeadlineDword * 4), (int)script.AnimationDeadline );
			PutInt32( body, at + (LoopingKeyDword * 4), script.LoopingKey );
			PutInt32( body, at + (AnimationMarkDword * 4), script.AnimationMark );
			PutInt32( body, at + (TimerDeadlineDword * 4), (int)script.TimerDeadline );

			if ( script.Stack.Length == saved.Stack.Length )
				PutInts( body, place.Stack, script.Stack );
			else
				++left;

			if ( script.Variables.Length == saved.Variables.Length )
				PutInts( body, place.Variables, script.Variables );
			else
				++left;

			if ( script.Limbo is { } limbo && limbo.Length == saved.Limbo!.Length )
			{
				PutInt32( body, at + (InLimboDword * 4), script.InLimbo );

				for ( var slot = 0; slot < limbo.Length; ++slot )
				{
					PutInt32( body, place.Limbo + (slot * LimboSlotSize), limbo[slot].Handle );

					if ( limbo[slot].Handle != 0 )
						PutInt32( body, place.Limbo + (slot * LimboSlotSize) + 4, (int)limbo[slot].Due );
				}
			}
			else
				++left;

			if ( script.Bounce is { } bounce && bounce.Length == saved.Bounce!.Length )
			{
				PutInt16( body, at + BouncingAt, script.Bouncing );
				PutInt16( body, at + BouncingAt + 2, script.BounceBase );
				PutInt32( body, at + (BounceNodeDword * 4), script.BounceNode );

				for ( var slot = 0; slot < bounce.Length; ++slot )
				{
					var to = place.Bounce + (slot * BounceSlotSize);

					PutInt32( body, to, bounce[slot].Handle );

					if ( bounce[slot].Handle == 0 )
						continue;

					PutInt32( body, to + 4, bounce[slot].Node );
					PutInt32( body, to + 8, (int)bounce[slot].Due );
					PutInt32( body, to + 12, (int)bounce[slot].Start );
				}
			}
			else
				++left;

			if ( script.Walk is { } walk && walk.Length == saved.Walk!.Length )
			{
				for ( var slot = 0; slot < walk.Length; ++slot )
				{
					var to = place.Walk + (slot * SubRecordSize);

					PutInt16( body, to + 0x18, walk[slot].State );

					if ( walk[slot].State == 0 )
					{
						PutInt32( body, to + 0x10, 0 );
						continue;
					}

					PutInt16( body, to, walk[slot].WalkNode );
					PutInt16( body, to + 0x02, walk[slot].HeadNode );
					PutInt16( body, to + 0x04, walk[slot].OffFrom );
					PutInt16( body, to + 0x06, walk[slot].OffTo );
					PutInt32( body, to + 0x08, (int)walk[slot].Start );
					PutInt32( body, to + 0x0c, (int)walk[slot].Due );
					PutInt32( body, to + 0x10, walk[slot].Handle );
					PutInt16( body, to + 0x14, walk[slot].Facing );
					PutInt16( body, to + 0x16, walk[slot].Action );
					PutInt16( body, to + 0x1a, walk[slot].Flags );
				}
			}
			else
				++left;

			if ( script.Heads is { } heads && heads.Length == saved.Heads!.Length )
				PutInts( body, place.Heads, heads );
			else
				++left;

			// The object list, where it is as long as the file's: each record's five words, its two links the file's.
			// One of another length changes the record's own, and is Splice's.
			if ( script.Effects is { } effects && saved.Effects is { } held && effects.Length == held.Length && place.Effects >= 0 )
			{
				for ( var index = 0; index < effects.Length; ++index )
				{
					var to = place.Effects + (index * EffectRecordSize);

					PutInt32( body, to + 0x08, effects[index].Type );
					PutInt32( body, to + 0x0c, effects[index].Handle );
					PutInt32( body, to + 0x10, effects[index].Node );
					PutInt32( body, to + 0x14, effects[index].Index );
					PutInt32( body, to + 0x18, effects[index].Tag );
				}
			}

			++written;
		}

		return new Written( written, left );
	}

	private static void PutInts( byte[] body, int at, int[] values )
	{
		for ( var i = 0; i < values.Length; ++i )
			PutInt32( body, at + (i * 4), values[i] );
	}

	private static void PutInt32( byte[] body, int at, int value ) =>
		System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian( body.AsSpan( at, 4 ), value );

	private static void PutInt16( byte[] body, int at, int value ) =>
		System.Buffers.Binary.BinaryPrimitives.WriteInt16LittleEndian( body.AsSpan( at, 2 ), unchecked((short)value) );

	private short ReadInt16At( int offset )
	{
		if ( offset < 0 || offset + 2 > _data.Length )
			throw new InvalidDataException( $"a word at 0x{offset:x} runs past the end of the payload" );

		return BitConverter.ToInt16( _data, offset );
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
