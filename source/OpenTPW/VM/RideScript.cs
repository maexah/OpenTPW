namespace OpenTPW;

/// <summary>
/// Runs one compiled ride script, as the original's interpreter runs it.
///
/// <para>
/// This is deliberately not <see cref="RideVM"/>. That type came from upstream, has never run, and is
/// wrong about the things this one had to get right: it converts branch targets that need no
/// conversion, models Sign and Zero flags the engine does not have, and returns from subroutines
/// through a <c>Queue</c>, which comes back to the first caller rather than the most recent. The
/// opcode table it was built around is real work and is kept, in <see cref="Opcode"/>.
/// </para>
///
/// <para>
/// <b>The shape of the machine</b>, read out of <c>FUN_00551cb0</c> (one instruction),
/// <c>FUN_005516b0</c> (the loop around it) and the handlers each opcode dispatches to:
/// </para>
///
/// <list type="bullet">
/// <item>The body is an array of words and the program counter indexes it directly, so a branch
/// target is used as it stands.</item>
/// <item><b>There are no condition flags.</b> Every instruction that computes something leaves its
/// value in one result register, and the conditional branches compare that against zero.</item>
/// <item>A destination operand must be a variable. When it is not, the engine returns through NOP's
/// own handler, so the instruction does nothing at all rather than failing - see
/// <see cref="IgnoredWrites"/>, which counts them because seventeen shipped instructions do it.</item>
/// <item>A script runs a fixed number of instructions per turn, and yields early by zeroing that
/// budget. <c>WAIT</c> yields by rewinding the program counter onto itself, so it costs one
/// instruction per turn until its deadline passes rather than blocking anything.</item>
/// </list>
///
/// <para>
/// <b>Only the opcodes whose handlers were actually read are implemented.</b> Everything else is a
/// no-op that <see cref="NotImplemented"/> counts, because a guessed instruction is worse than an
/// absent one: it would run, produce a plausible number and take a branch nobody can account for.
/// The ones left out reach into a world that does not exist yet - guests, scenery, sound - so they
/// can be filled in beside whatever provides those. That list is shorter than it was: objects and
/// animation landed beside <see cref="RideEffects"/> and the deadline fields, limbo needed no world
/// at all because the engine keeps it in the script's own frame, and the instructions that reach
/// other scripts needed only the registry <see cref="RideScriptScheduler"/> already was.
/// </para>
/// </summary>
public sealed class RideScript
{
	/// <summary>
	/// The original gives each script a turn every eighth tick, staggered by script so the work
	/// spreads out; <c>TURBO</c> opts a script out of that. Nothing here schedules anything - the
	/// caller decides when to call <see cref="Turn"/> - but the number is the original's and belongs
	/// with the rest of the machine.
	/// </summary>
	public const int TicksBetweenTurns = 8;

	private readonly RideScriptFile _file;
	private readonly int[] _variables;
	private readonly int[] _stack;
	private readonly Dictionary<int, RideInstruction> _atAddress;

	/// <summary>Subroutine returns fill the stack from the top down; see <see cref="RideScriptFile.StackSize"/>.</summary>
	private int _calls;

	/// <summary>HUSH and HOP fill the same storage from the bottom up, and must not meet <see cref="_calls"/>.</summary>
	private int _values;

	private int _budget;
	private bool _critical;

	/// <summary>
	/// The deadline a <c>WAIT</c> or a <c>WAITANIM</c> is sitting on - the engine's field <c>+0xa0</c>,
	/// which both of them share.
	///
	/// <para>
	/// <b>Null is the engine's own nought: an empty slot, not a deadline of zero.</b> The distinction
	/// is load-bearing rather than tidy, because <c>WAITANIM</c> with no model sets a deadline in the
	/// <b>past</b> - see <see cref="WaitOutAnimation"/> - and a plain float testing <c>&lt;= 0</c> would
	/// read that back as an empty slot and arm it again every visit, so the script would never move.
	/// </para>
	/// </summary>
	private float? _waitUntil;

	/// <summary>
	/// The deadline <c>SETTIMER</c> last set, on the caller's clock - the engine's field <c>+0xc4</c>.
	/// One per script, and zero until a <c>SETTIMER</c> has run.
	/// </summary>
	private float _timerUntil;

	/// <summary>
	/// When the animation last triggered finishes - the engine's field <c>+0xa4</c>, and <b>not</b> the
	/// one <c>WAIT</c> uses. <c>TRIGANIM</c> arms it, <c>LOOPANIM</c> clears it, and <c>WAIT4ANIM</c> is
	/// the only instruction that reads it. Null when nothing has been triggered.
	/// </summary>
	private float? _animationUntil;

	/// <summary>
	/// Which animation is looping - the engine's field <c>+0xa8</c>, holding the key
	/// <c>(second &lt;&lt; 16) + first</c>, and <see cref="OneShot"/> after a one-shot trigger.
	/// </summary>
	private int _looping;

	/// <summary>
	/// What a one-shot <c>TRIGANIM</c> leaves in <see cref="_looping"/>. No <c>LOOPANIM</c> can name it:
	/// its key is built from two operands the engine sign-extends from sixteen bits, so a literal
	/// 65535 arrives as -1. That is why a trigger always leaves the next <c>LOOPANIM</c> looking like a
	/// change.
	/// </summary>
	private const int OneShot = 0xFFFF;

	/// <summary>
	/// The 300ms the engine adds to, or subtracts from, every animation length it is given - the
	/// constant behind both its <c>ADD EAX,-0x12c</c> and the floor it compares against.
	/// </summary>
	private const int AnimationSlack = 300;

	/// <summary>
	/// <c>RAND</c>'s generator state - see <see cref="NextRandom"/>, which carries why this is per
	/// script where the engine's is per game.
	/// </summary>
	private uint _random = DefaultSeed;

	/// <summary>
	/// What <see cref="_random"/> starts at. The engine's own starting value is not established - its
	/// generator is seeded by a plain setter (<c>FUN_00516370</c>) called from eight places, none of
	/// them the script system - so this is ours, chosen only so that a run is repeatable.
	/// </summary>
	private const uint DefaultSeed = 1;

	/// <summary>
	/// One place in limbo - eight bytes of the engine's array at <c>+0x24</c>: whoever is being held,
	/// and the clock reading they are due back at. <b>A slot is free when its handle is nought</b>,
	/// which is how the engine finds one: it scans for the first zero rather than keeping a cursor.
	/// </summary>
	private struct LimboSlot
	{
		public int Handle;
		public float Release;
	}

	/// <summary>
	/// Everyone this script is holding - the engine's <c>+0x24</c>, sized by its <c>+0x58</c>.
	///
	/// <para>
	/// <b>Limbo needs no world, which is what made this family implementable now.</b> The array is part
	/// of the script's own frame: the loader reads the count out of the file header and allocates
	/// <c>count * 8</c> bytes for it, and the teardown frees it beside the variables and the stack. So
	/// unlike everything else still outstanding, none of these five instructions is waiting on guests
	/// to exist before it can be honest - see <see cref="RideScriptFile.LimboCapacity"/>.
	/// </para>
	/// </summary>
	private readonly LimboSlot[] _limbo;

	/// <summary>
	/// How many slots are taken - the engine's <c>+0x60</c>, which it keeps rather than recounts, and
	/// which is why <see cref="SendToLimbo"/> can drive it out of step with the slots themselves.
	/// </summary>
	private int _inLimbo;

	/// <summary>
	/// What <c>LIMBO</c>'s second operand is measured in. The engine multiplies it by a thousand before
	/// adding it to the clock, so the operand is <b>seconds</b> - see <see cref="SendToLimbo"/>.
	/// </summary>
	private const int LimboSecond = 1000;

	/// <summary>
	/// One place on the ride itself - sixteen bytes of the engine's array at <c>+0x28</c>: who is on it,
	/// which node of the ride they were put on, when they are due off, and when they got on. <b>A slot is
	/// free when its handle is nought</b>, and the scan starts from the beginning every time, exactly as
	/// <see cref="LimboSlot"/>'s does.
	/// </summary>
	private struct BounceSlot
	{
		public int Handle;
		public int Node;
		public float Expiry;
		public float Start;
	}

	/// <summary>
	/// Everyone this script currently has bouncing - the engine's <c>+0x28</c>, sized by its <c>+0x64</c>,
	/// which the loader fills from the header (<see cref="RideScriptFile.BounceCapacity"/>).
	///
	/// <para>
	/// <b>Like limbo, this needs no world</b>: the array is part of the script's own frame, and every
	/// instruction in the family answers out of it. Only one Lost Kingdom script declares any slots -
	/// <c>Bouncy.RSE</c>, with ten - so on the other 21 every <c>BOUNCE</c> refuses and every
	/// <c>UNBOUNCE</c> answers nought, which is the engine's own behaviour and not a stand-in for it.
	/// </para>
	/// </summary>
	private readonly BounceSlot[] _bounce;

	/// <summary>
	/// How many are bouncing - the engine's <c>+0x6c</c>, and <b>sixteen bits</b>, which is why
	/// <c>BOUNCING</c> sign-extends it (<c>MOVSX</c>) rather than simply loading it.
	/// </summary>
	private short _bouncing;

	/// <summary>
	/// What <c>BOUNCESETBASE</c> sets - the engine's <c>+0x6e</c>, a second sixteen-bit field beside the
	/// tally.
	///
	/// <para>
	/// <b>Nothing in the bounce family reads it.</b> Its only two readers are in <c>FUN_00557ab0</c>,
	/// which takes it beside the thing's model handle and the slot table to place whoever is bouncing, so
	/// it is presentation rather than bookkeeping. It is kept because <c>Bouncy.RSE</c> sets it - it is
	/// the one member of the family that script uses outside the main loop - and a script writing into
	/// nothing would read as an oversight. Compare <see cref="RideState.SetWorn"/>.
	/// </para>
	/// </summary>
	private short _bounceBase;

	/// <summary>
	/// What <c>BOUNCESETNODE</c> sets - the engine's <c>+0x70</c>, added to a slot's index to give the
	/// node a rider is put on.
	///
	/// <para>
	/// <b>The two setters do the opposite of what their names suggest, and this is the trap in the
	/// family.</b> <c>BOUNCESETNODE</c> writes the base that node numbers are counted from, and
	/// <c>BOUNCESETBASE</c> writes the unrelated field above. Worse, this one takes its operand
	/// <b>raw</b>: its handler has no <c>0x40000000</c> tag test at all, so a variable operand would be
	/// stored as its tagged word rather than its value.
	/// </para>
	/// <para>
	/// <b>Exactly one script in the game uses it</b>, and not in Lost Kingdom: <c>Jelly.RSE</c>, which
	/// passes a literal 3. Since every use is a literal the raw store can never differ from a resolved
	/// one in the shipped corpus, which is what makes reproducing the missing test free rather than
	/// risky - and it is pinned by a test, because the day that stops being true this stops being safe.
	/// In Lost Kingdom nothing sets it at all, so the base stays nought and a rider's node there is
	/// simply their slot index.
	/// </para>
	/// </summary>
	private int _bounceNode;

	/// <summary>What <c>BOUNCE</c>'s duration operand is measured in - milliseconds per second, as limbo's is.</summary>
	private const int BounceSecond = 1000;

	/// <summary>
	/// The width of the window a rider may leave in. Both ways off divide the milliseconds a rider has
	/// been on, modulo a second, by <b>200</b> and act only when that is nought - so somebody may only
	/// come off during the first fifth of each second they have been aboard.
	///
	/// <para>
	/// <b>The divisor was read, not guessed.</b> The idiom is <c>0x51eb851f</c> (2^37/100) with
	/// <c>SAR EDX,6</c>, which is a total shift of 38 and therefore 200, not the 100 the canonical
	/// <c>SAR EDX,5</c> spelling gives. That exact byte pair occurs twice in the whole executable - these
	/// two handlers - and the decompiler renders both as <c>/ 200</c>.
	/// </para>
	/// <para>
	/// <b>It is a poll, which is what makes it sane.</b> Bouncy asks again every pass of its loop, so the
	/// window costs at most a second rather than turning anyone away; it is also why
	/// <c>FORCEUNBOUNCE</c> still has a condition despite being the forcible one.
	/// </para>
	/// </summary>
	private const int BounceWindow = 200;

	/// <summary>
	/// Where <c>NAME</c>'s string sits in the blob - the engine's field <c>+0x74</c>, and the only thing
	/// <c>NAME</c> writes anywhere.
	///
	/// <para>
	/// <b>The loader leaves this at -1 rather than at nought</b> (<c>puVar7[0x1d] = 0xffffffff</c>), and
	/// <c>FINDSCRIPTRAND</c>'s walk skips a script whose value is negative. That is what makes "has never
	/// named itself" different from "is called whatever sits at offset nought", and it is load-bearing:
	/// <b>31 of the 308 shipped scripts never run <c>NAME</c></b>, and not one of them can ever be found.
	/// </para>
	/// </summary>
	private int _nameOffset = -1;

	/// <summary>
	/// Whether this script has muted the music - the engine's frame byte <c>+0xb9</c>.
	///
	/// <para>
	/// <b>Exactly two instructions in the whole subsystem touch that byte</b>: <c>DIPMUSIC</c>'s handler
	/// writes it, and the teardown reads it. There is no opcode that clears it, and no shipped script
	/// ever passes nought - all eight uses are a literal 1 - so in practice <b>only a script's death
	/// ever un-mutes the music</b>, which is why this has to outlive the instruction that set it.
	/// </para>
	/// </summary>
	private bool _dipped;

	public RideScript( RideScriptFile file )
	{
		_file = file;

		_variables = new int[Math.Max( file.VariableCount, file.VariableNames.Count )];
		_stack = new int[Math.Max( file.StackSize, 0 )];
		_limbo = new LimboSlot[Math.Max( file.LimboCapacity, 0 )];
		_bounce = new BounceSlot[Math.Max( file.BounceCapacity, 0 )];
		_atAddress = file.Instructions.ToDictionary( instruction => instruction.Address );

		_calls = _stack.Length - 1;
		_values = 0;

		Running = file.IsValid && file.Instructions.Count > 0;
	}

	/// <summary>False once the script has stopped - it ran off its own end, or hit <c>END</c>.</summary>
	public bool Running { get; private set; }

	/// <summary>Whether <c>TURBO</c> has asked for a turn every tick rather than every eighth.</summary>
	public bool EveryTick { get; private set; }

	/// <summary>Whatever the last instruction computed. The conditional branches test this.</summary>
	public int Result { get; private set; }

	/// <summary>Where execution is, counted in words from the start of the body.</summary>
	public int Position { get; private set; }

	/// <summary>The name the script gave itself with <c>NAME</c>, if it has run that far.</summary>
	public string Name { get; private set; } = string.Empty;

	/// <summary>The script's own variables, in the order <see cref="RideScriptFile.VariableNames"/> names them.</summary>
	public IReadOnlyList<int> Variables => _variables;

	/// <summary>Instructions skipped because their destination was not a variable, as the engine skips them.</summary>
	public int IgnoredWrites { get; private set; }

	/// <summary>Instructions that reach into a world this does not have yet, counted rather than guessed.</summary>
	public int NotImplemented { get; private set; }

	/// <summary>True while the script is sitting on a <c>WAIT</c> or <c>WAITANIM</c> not yet come due.</summary>
	public bool Waiting => _waitUntil is not null;

	/// <summary>True while the script is sitting on a <c>WAIT4ANIM</c> that has not come due.</summary>
	public bool WaitingForAnimation => _animationUntil is not null;

	/// <summary>
	/// The ride this script drives, or null if it has none. The original finds it by walking a list
	/// for a node whose id matches the script's own (<c>FUN_0043b050</c>) and keeps the handle; there
	/// is no such registry here, so the caller supplies it. A script without one still runs - every
	/// <c>COAST</c> op becomes a no-op, which is what the engine does when the handle it kept is null.
	/// </summary>
	public RideState? Ride { get; set; }

	/// <summary>Whether the script has run <c>COAST_INITIALISE</c>, which is how it claims its ride.</summary>
	public bool Initialised { get; private set; }

	/// <summary>
	/// Where this script's particles and sounds go, or null if it has nowhere to put them - see
	/// <see cref="RideEffects"/>, which is the engine's own list at <c>+0xb0</c>. A script without one
	/// still runs, and <c>ADDOBJ</c>, <c>EVENT</c> and <c>KILLOBJ</c> are counted rather than guessed,
	/// which is the same answer <see cref="Ride"/> gives <c>COAST</c>.
	/// </summary>
	public RideEffects? Effects { get; set; }

	/// <summary>
	/// The thing this script was loaded for - the engine's field <c>+0xac</c>, a sixteen-bit id and not a
	/// pointer, and nought for a script belonging to nothing.
	///
	/// <para>
	/// The loader takes it as its <b>second argument</b> and stores it before the script runs an
	/// instruction (<c>0x00558d2e</c>). It is what separates a script that drives something standing in a
	/// park from one run in a harness.
	/// </para>
	/// </summary>
	public int ThingId { get; set; }

	/// <summary>
	/// The animations the thing's model can play, or null where it has no model - the engine's field
	/// <c>+0xc8</c>.
	///
	/// <para>
	/// <b>The world never writes this: the loader seeds it once</b>, from the thing it was handed, by
	/// looking that thing up and taking its model handle (<c>0x00558d5b</c>-<c>0x00558d68</c>). So a
	/// script either has its model from the moment it is loaded or never gets one, which is why this is
	/// set beside <see cref="ThingId"/> and not later.
	/// </para>
	/// <para>
	/// <b>Null is a path the engine defines rather than one left open.</b> Every animation handler tests
	/// this handle first and substitutes nought for the length it would have asked the model for - so the
	/// 300ms a model-less <c>TRIGANIM</c> answers is the engine's own arithmetic on nought, not a
	/// stand-in. Giving a script its animations is therefore purely additive: the model-less answer is
	/// the general case evaluated at zero.
	/// </para>
	/// </summary>
	public RideAnimations? Animations { get; set; }

	/// <summary>How many the script is holding in limbo - what <c>INLIMBO</c> answers.</summary>
	public int InLimbo => _inLimbo;

	/// <summary>
	/// How much room is left in limbo - what <c>LIMBOSPACE</c> answers, and nought for the 284 shipped
	/// scripts whose header declares no slots at all.
	/// </summary>
	public int LimboSpace => _limbo.Length - _inLimbo;

	/// <summary>
	/// This script's own id - the engine's field <c>+0x8</c>, handed out by its loader from a counter that
	/// is only ever incremented. It is what <c>FINDSCRIPTRAND</c> answers, and what every instruction
	/// reaching another script carries. <see cref="RideScriptScheduler.Add"/> sets it.
	/// </summary>
	public int Id { get; set; }

	/// <summary>
	/// The registry this script is registered in, or null where it is being run on its own. Every
	/// instruction that reaches another script goes through it, exactly as the engine's do: there is one
	/// global list and <b>no script ever holds a pointer to another</b>, only an id.
	/// </summary>
	public RideScriptScheduler? Host { get; set; }

	/// <summary>
	/// The directory this script was loaded from - the engine's field <c>+0x38</c>, which its loader fills
	/// in by stripping the last component off the path it was handed.
	///
	/// <para>
	/// <b>It exists because a spawned script is named relative to the script that spawns it.</b>
	/// <c>SPAWNCHILD</c> and <c>SPAWNSOUND</c> concatenate this prefix with their string operand before
	/// calling the loader, and that is not a detail: all 48 spawn sites in the shipped corpus name a file
	/// sitting in the asking script's own directory, and the names they ask for are far from unique - 28
	/// of them ask for <c>EventMap.rse</c>, of which the game ships one copy per item. So a loader given
	/// the bare name has no way to tell which of them is meant.
	/// </para>
	/// <para>
	/// Empty where nothing set it, which leaves the bare name and is what a script run on its own gets.
	/// </para>
	/// </summary>
	public string Directory { get; set; } = string.Empty;

	/// <summary>
	/// Who spawned this script - the engine's field <c>+0x10</c>, an id and not a pointer, and nought for
	/// nobody.
	/// </summary>
	public int ParentId { get; set; }

	/// <summary>
	/// The one child this script has spawned - the engine's field <c>+0x0c</c>. There is room for exactly
	/// one, and <c>SPAWNCHILD</c> overwrites it without taking down whatever was there.
	/// </summary>
	public int ChildId { get; set; }

	/// <summary>
	/// The script a <c>SPAWNSOUND</c> spawned - the engine's field <c>+0x14</c>, a second and quite
	/// separate slot. <b><c>SPAWNSOUND</c> is not a sound instruction</b>: it calls the script loader, and
	/// all 28 shipped uses ask it for the same file, <c>EventMap.rse</c>.
	///
	/// <para>
	/// <b>No instruction ever reads this back, and it is not dead storage.</b> The far side is
	/// <c>FUN_0055a3e0</c>, which takes a script id and a variable index, follows that script's
	/// <c>+0x14</c> to the sound script and answers one of <i>its</i> variables - and it has 29 callers,
	/// none of them in the interpreter. So the point of <c>SPAWNSOUND</c> is to publish a block of
	/// variables the ride and sound code reads by id, which is why every use loads the same file. Nothing
	/// here consumes <i>that block</i>; the slot is kept so that whatever does will find it already
	/// filled. <b>The slot itself is read</b> - <c>RideScriptScheduler</c>'s teardown follows it to take
	/// the spawned sound script down with its parent. This said nothing consumed it at all.
	/// </para>
	/// </summary>
	public int SoundChildId { get; set; }

	/// <summary>
	/// Whether the script has run <c>NAME</c>, which is what makes it findable at all - see
	/// <see cref="_nameOffset"/>.
	/// </summary>
	public bool IsNamed => _nameOffset >= 0;

	/// <summary>
	/// Whether this script is holding the music muted - see <see cref="_dipped"/>. The scheduler reads
	/// it when the script dies, because that is the only thing that ever lets the music back up.
	/// </summary>
	public bool DippedMusic => _dipped;

	/// <summary>
	/// Reads one of the common variables by name rather than by the enum's numeric value.
	///
	/// <para>
	/// <b>For a RIDE script the two agree, and that was measured rather than assumed.</b> Bouncy, Monkey,
	/// Mumbo, Wateride, Spider and Inca God all declare the same twelve common names first and in the
	/// enum's own order, then append their own (<c>VAR_SCREAMING</c>, <c>VAR_BOATCOUNT</c>,
	/// <c>VAR_SPACELEFT</c>, and so on). So <c>Read( (int)variable )</c> would have been right for every
	/// ride in the park.
	/// </para>
	/// <para>
	/// <b>It is the OTHER scripts that make a fixed index unsafe.</b> A ride's archive can hold more than
	/// one <c>.RSE</c>, and the companions declare none of the common set: <c>child.RSE</c> in
	/// <c>monkey.wad</c> declares only <c>VAR_TEMP</c>, <c>effects.RSE</c> in <c>mumbo.wad</c> declares
	/// <c>VAR_TEMP</c> and <c>VAR_RAND</c>, and <c>EventMap.RSE</c> in <c>wateride.wad</c> declares ten
	/// <c>VAR_EVT</c> slots and a <c>VAR_PAR0</c>. A script numbers its variables in the order it declares
	/// them, so the enum is a list of NAMES to look up and not a layout to index with.
	/// </para>
	/// <para>
	/// Resolving through <see cref="IndexOf"/> costs nothing here - nothing calls this - and matches what
	/// <see cref="ParkRides"/> has always done for the park gate, which reaches <c>VAR_COMMAND</c> and
	/// <c>VAR_STATUS</c> by name for exactly this reason.
	/// </para>
	/// </summary>
	public int this[RideVariables variable] => this[variable.ToString()];

	/// <summary>Reads a variable by the name the script declares it under, or 0 if it has none such.</summary>
	public int this[string name]
	{
		get
		{
			var index = IndexOf( name );

			return index < 0 ? 0 : Read( index );
		}
	}

	/// <summary>Where a named variable sits, or -1. Names are the script's own, not a fixed set.</summary>
	public int IndexOf( string name )
	{
		for ( int i = 0; i < _file.VariableNames.Count; ++i )
		{
			if ( string.Equals( _file.VariableNames[i], name, StringComparison.Ordinal ) )
				return i;
		}

		return -1;
	}

	/// <summary>
	/// Writes one of this script's variables from outside it, by the name the script itself declares it
	/// under - the engine's <c>FUN_0055a0b0( script, index, value )</c>.
	///
	/// <para>
	/// <b>This is how a park's gate is opened and shut</b>, and in the original it is very nearly the only
	/// thing that ever writes a script variable from outside: <c>FUN_00519ef0</c> looks the gate's script up
	/// by handle and writes its variable 0. Everything else that moves a variable is an instruction inside
	/// some script.
	/// </para>
	/// <para>
	/// <b>It answers whether the write landed, and callers are expected to care.</b> A name this script does
	/// not declare gives -1 from <see cref="IndexOf"/>, and writing nothing would look on screen exactly
	/// like a gate that was never commanded - which is the state this whole mechanism exists to leave. The
	/// bounds check is the engine's own, which refuses an index outside the slot count rather than growing
	/// the array.
	/// </para>
	/// </summary>
	/// <returns>Whether the script declares that name and the value was written.</returns>
	internal bool Set( string name, int value )
	{
		var index = IndexOf( name );

		if ( index < 0 || index >= _variables.Length )
			return false;

		_variables[index] = value;

		return true;
	}

	/// <summary>
	/// Gives the script one turn, running until it spends its instruction budget, yields, or stops.
	/// <paramref name="now"/> is whatever clock the caller keeps; <c>WAIT</c> durations are added to
	/// it unchanged - see <see cref="Wait"/>.
	/// </summary>
	public void Turn( float now )
	{
		if ( !Running )
			return;

		// The engine zeroes its critical flag at the top of every tick, before any script runs
		// (FUN_005516b0), so a CRIT_LOCK does not outlive the turn that took it. Without this a script
		// that locks and then yields - ENDSLICE, or a WAIT - would come back with instructions still
		// costing nothing, and the loop below would never end. No shipped script does that, so nothing
		// in the corpus can catch it; it is here because the engine's own reset says so.
		_critical = false;
		_budget = _file.TimeSlice > 0 ? _file.TimeSlice : 1;

		// Nothing brings a channel up to date here, and that is deliberate. The engine sweeps its animation
		// players once per FRAME, off a clock snapshot taken outside the fixed-step loop the scripts run in,
		// and never from the script system: FUN_004735d0, which advances and poses, has exactly ONE caller
		// (FUN_00473c70, at 00473d2e), and not one of that function's ten call sites is FUN_005516b0 or sits
		// inside the 31ms loop. The park's are FUN_0044e410(2), FUN_00429df0(0) and FUN_00429df0(1), all of
		// them past the loop's back edge in Game_StateMachine. ParkObjects.Sweep is where that lives here.
		//
		// This used to advance per tick, which promoted a queued clip mid-catch-up: with three ticks due, a
		// clip ending on the first had its successor running before the second tick's instructions could
		// look at it. The engine cannot do that, because it has not swept yet. End-of-frame state is the
		// same either way - ParkRides hands the sweep Ticks * 31, which is exactly the instant its last tick
		// ran at - so what this changes is only what a script can see PART WAY THROUGH a long frame.
		//
		// Scripts stay correct without it because every path that reads channel state to answer one goes
		// through RideAnimations.Trigger, and that calls MoveTo on the channel itself before deciding.

		while ( _budget > 0 && Running )
			Step( now );
	}

	private void Step( float now )
	{
		if ( !_atAddress.TryGetValue( Position, out var instruction ) )
		{
			Running = false;
			return;
		}

		Position += 1 + instruction.Operands.Count;

		// A critical section stops instructions costing anything, so everything up to CRIT_UNLOCK runs
		// in one go however long it is.
		if ( !_critical )
			--_budget;

		Execute( instruction, now );
	}

	private void Execute( RideInstruction instruction, float now )
	{
		var operands = instruction.Operands;

		switch ( instruction.Opcode )
		{
			case Opcode.NOP:
				break;

			case Opcode.END:
				Running = false;
				break;

			case Opcode.ENDSLICE:
				_budget = 0;
				break;

			case Opcode.CRIT_LOCK:
				_critical = true;
				break;

			case Opcode.CRIT_UNLOCK:
				// Leaving a critical section also gives up the rest of the turn, which is what stops a
				// script holding the interpreter once it has unlocked.
				_critical = false;
				_budget = 0;
				break;

			case Opcode.TURBO:
				// The engine stores the low byte of the operand word as written, without resolving a
				// variable. Every shipped use is a literal 0 or 1.
				EveryTick = (operands[0].Value & 0xFF) != 0;
				break;

			case Opcode.NAME:
				TakeName( operands[0] );
				break;

			case Opcode.COPY:
				Store( operands[0], Value( operands[1] ) );
				break;

			case Opcode.ADD:
				Store( operands[0], Value( operands[0] ) + Value( operands[1] ) );
				break;

			case Opcode.SUB:
				Store( operands[0], Value( operands[1] ) - Value( operands[2] ) );
				break;

			case Opcode.MULT:
				Store( operands[0], Value( operands[1] ) * Value( operands[2] ) );
				break;

			case Opcode.DIV:
				Store( operands[0], Divide( Value( operands[1] ), Value( operands[2] ), remainder: false ) );
				break;

			case Opcode.MOD:
				Store( operands[0], Divide( Value( operands[1] ), Value( operands[2] ), remainder: true ) );
				break;

			case Opcode.TEST:
				// Reads the variable into the result register and stores nothing.
				if ( operands[0].Kind == RideOperandKind.Variable )
					Result = Value( operands[0] );
				else
					++IgnoredWrites;
				break;

			case Opcode.CMP:
				if ( operands[0].Kind == RideOperandKind.Variable )
					Result = Value( operands[0] ) - Value( operands[1] );
				else
					++IgnoredWrites;
				break;

			case Opcode.BRANCH:
				Jump( operands[0] );
				break;

			case Opcode.BRANCH_Z:
				if ( Result == 0 )
					Jump( operands[0] );
				break;

			case Opcode.BRANCH_NZ:
				if ( Result != 0 )
					Jump( operands[0] );
				break;

			case Opcode.BRANCH_NV:
				if ( Result < 0 )
					Jump( operands[0] );
				break;

			case Opcode.BRANCH_PV:
				// Strictly greater than zero. Branching on zero as well is the mistake the inherited
				// handler makes, and the one the engine's own JLE rules out.
				if ( Result > 0 )
					Jump( operands[0] );
				break;

			case Opcode.JSR:
				Call( operands[0] );
				break;

			case Opcode.RETURN:
				Return();
				break;

			case Opcode.PUSH:
				PushCall( Value( operands[0] ) );
				break;

			case Opcode.POP:
				Store( operands[0], PopCall() );
				break;

			case Opcode.HUSH:
				PushValue( Value( operands[0] ) );
				break;

			case Opcode.HOP:
				Store( operands[0], PopValue() );
				break;

			case Opcode.WAIT:
				Wait( now, Value( operands[0] ), 1 + operands.Count );
				break;

			// The animation family. Every one of these handlers tests the model handle at +0xc8 before
			// it does anything, and takes a path the engine defines completely when that handle is
			// nought - which is every script run on its own, and no longer every script in a park: a
			// placed thing is handed its own model when it is bound. See RideScript.Animations.
			//
			// TRIGWAITANIM is NOT among them, and that is now settled rather than deferred. Its handler
			// (0x552c1a) triggers exactly as TRIGANIM does, marks +0xbc with the animation id PLUS ONE,
			// rewinds four words onto itself and returns without ending the slice; on re-entry it asks
			// the model for channel 0 and goes on only when that answer plus one equals the mark. With
			// no model the query is skipped and the comparison is made against the RAW THIRD OPERAND,
			// which nothing can ever change - so the instruction parks the script for ever unless
			// operand three happens to equal operand one. In the 133 shipped uses it never does: 132
			// differ outright and the last is a variable. Implementing it faithfully would hang 56
			// scripts rather than complete 11, and that "+11" came from a coverage measure that cannot
			// see blocking at all. It waits on models existing, not on anyone's effort.
			case Opcode.FLUSHANIM:
				// The handler's first act is to fetch the model and leave if there is none, so with no
				// model this really is a no-op. With one it empties the channel's QUEUE and nothing else
				// (FUN_00473270): the clip that is running plays out, and nothing is answered.
				Animations?.Flush();
				break;

			case Opcode.TRIGANIM:
				// Operand one is the role, operand two the entry within it, and operand three is where
				// the length goes. The first two are resolved like any other value - the handler gives
				// each the same tag test before it pushes them at 0x00552950 - and the channel it plays
				// on is a literal nought, which is what the _CH variants exist to vary.
				TriggerAnimation( now, Value( operands[0] ), Value( operands[1] ), operands[2] );
				break;

			case Opcode.WAITANIM:
				// The same two operands, and no destination: this one keeps the length to itself and
				// waits on it rather than answering it.
				WaitOutAnimation( now, Value( operands[0] ), Value( operands[1] ), 1 + operands.Count );
				break;

			case Opcode.LOOPANIM:
				// Operand one is the role and operand two the entry, the same order the other three take.
				Loop( now, Value( operands[0] ), Value( operands[1] ) );
				break;

			case Opcode.WAIT4ANIM:
				WaitForAnimation( now, 1 + operands.Count );
				break;

			case Opcode.GETTIME:
				// The clock as it stands, stored like any other result. The engine reads the game's
				// own clock object at 0x785970 and stores what it returns with no arithmetic at all -
				// it is NOT how long this ride has existed, which is what the published docs said.
				Store( operands[0], (int)now );
				break;

			case Opcode.SETTIMER:
				// clock + duration, into the script's single timer. Unlike WAIT, the engine does NOT
				// scale this by the speed word: SETTIMER resolves its operand and adds it to the
				// clock reading directly (0x0055641b).
				_timerUntil = now + Value( operands[0] );
				break;

			case Opcode.GETTIMER:
				// What is left of that timer, floored at zero - the engine's own JNS after the
				// subtraction. Every one of the 21 shipped uses names a literal destination, so the
				// answer lands in the result register and the store is skipped, exactly as COAST 2 0
				// does; the branch that follows is what reads it.
				Store( operands[0], TimerRemaining( now ) );
				break;

			case Opcode.RAND:
				// The bound is taken as a sign-extended short WITHOUT being resolved - the handler
				// does a bare MOVSX on it, with none of the tag test every value operand gets. A
				// variable bound would therefore be read as a literal; no shipped script writes one.
				Store( operands[0], NextRandom( (short)operands[1].Value ) );
				break;

			// What a ride starts playing, and what stops it again. ADDOBJ keeps the engine's own record so
			// that a later KILLOBJ can find it by tag; EVENT deliberately keeps nothing, because its
			// handler throws the handle away and so nothing it starts is ever killable. FADEOBJ is NOT
			// among them - 113 instructions that complete no further script, and a fade differs from a
			// kill only in stopping a sound gently, which nothing here can yet hear. (This named
			// SETOBJPARAM alongside it at 133; SETOBJPARAM is implemented, below.)
			case Opcode.ADDOBJ:
				AddObject( operands );
				break;

			case Opcode.EVENT:
				TriggerEvent( operands );
				break;

			case Opcode.KILLOBJ:
				KillObjects( operands[0] );
				break;

			// Limbo: where a shop or a toilet keeps a guest while they are inside it. All five handlers
			// work on the script's own frame - the slots at +0x24, how many there are at +0x58, how many
			// are taken at +0x60 - so none of them needs a world to be honest, which is what separates
			// this family from everything else still outstanding. Only the 24 scripts whose header
			// declares slots can hold anyone; on the other 284 a LIMBO answers nought, and that is the
			// engine's own JLE rather than a stand-in for it.
			case Opcode.LIMBO:
				// Both operands are read and NEITHER is written - the handler sets the result register
				// and returns without ever testing a destination tag. Every other instruction in the
				// family answers into its operand, so this asymmetry is easy to get wrong by analogy.
				Result = SendToLimbo( now, Value( operands[0] ), Value( operands[1] ) ) ? 1 : 0;
				break;

			case Opcode.UNLIMBO:
				Store( operands[0], TakeFromLimbo( now ) );
				break;

			case Opcode.FORCEUNLIMBO:
				ForceFromLimbo( operands[0] );
				break;

			case Opcode.INLIMBO:
				Store( operands[0], _inLimbo );
				break;

			case Opcode.LIMBOSPACE:
				// All 24 shipped uses write a literal 0 where a destination would go, so the answer
				// lands in the result register and the write is stepped over - the COAST 2 0 idiom.
				Store( operands[0], LimboSpace );
				break;

			// Reaching the scripts around it: this script's one child, whoever spawned it, and anything
			// else in the registry by id or by name. None of these needs a world - every one works on
			// another script's own frame - and the registry they go through is the scheduler that was
			// already here.
			//
			// NOT ONE OF THEM BLOCKS, which is what made this family safe to take on where TRIGWAITANIM
			// was not: no handler among them rewinds the program counter onto itself or zeroes the
			// instruction budget, and every store to +0x3c in their blocks belongs to the inlined operand
			// fetch. That was checked in the bytes before any of this was written, because the instrument
			// that ranks these opcodes measures coverage and is blind to blocking.
			case Opcode.SPAWNCHILD:
				SpawnChild( operands[0] );
				break;

			case Opcode.SPAWNSOUND:
				SpawnSound( operands[0] );
				break;

			case Opcode.REMOVECHILD:
				RemoveChild();
				break;

			case Opcode.SETVARINCHILD:
				SetVariableIn( ChildId, Value( operands[0] ), Value( operands[1] ) );
				break;

			case Opcode.SETVARINPARENT:
				// No shipped script uses this one at all. It is here because it is literally the same
				// block of engine code as SETVARINCHILD, reached with the other id.
				SetVariableIn( ParentId, Value( operands[0] ), Value( operands[1] ) );
				break;

			case Opcode.GETVARINCHILD:
				GetVariableIn( ChildId, operands[0], Value( operands[1] ) );
				break;

			case Opcode.GETVARINPARENT:
				GetVariableIn( ParentId, operands[0], Value( operands[1] ) );
				break;

			case Opcode.GETREMOTEVAR:
				GetRemoteVariable( operands[0], Value( operands[1] ), Value( operands[2] ) );
				break;

			case Opcode.SETREMOTEVAR:
				SetRemoteVariable( Value( operands[0] ), Value( operands[1] ), Value( operands[2] ) );
				break;

			case Opcode.FINDSCRIPTRAND:
				FindScriptAtRandom( operands[0], operands[1] );
				break;

			case Opcode.DIPMUSIC:
				DipMusic( Value( operands[0] ) );
				break;

			case Opcode.SETOBJPARAM:
				SetObjectParameter( operands );
				break;

			// Riding, for the rides that carry people on the ride itself rather than in cars. This is one
			// of SIX ways a script reports somebody off - the others are COAST 3, BUMP 2, WALKGET, HOP
			// with DELHEAD, and TOUR 4 - so implementing it frees exactly the scripts that use it, which
			// in Lost Kingdom is Bouncy.RSE alone.
			case Opcode.BOUNCESETNODE:
				// Raw, with no tag test: the handler stores the operand word itself. See _bounceNode.
				_bounceNode = operands[0].Value;
				break;

			case Opcode.BOUNCESETBASE:
				_bounceBase = (short)Value( operands[0] );
				break;

			case Opcode.BOUNCE:
				// Answers into the result register and writes no operand, the asymmetry LIMBO has too.
				Result = Bounce( now, Value( operands[0] ), Value( operands[1] ) ) ? 1 : 0;
				break;

			case Opcode.UNBOUNCE:
				Store( operands[0], Unbounce( now, whenDue: true ) );
				break;

			case Opcode.FORCEUNBOUNCE:
				Store( operands[0], Unbounce( now, whenDue: false ) );
				break;

			case Opcode.BOUNCING:
				Store( operands[0], _bouncing );
				break;

			case Opcode.COAST:
				Coast( operands );
				break;

			default:
				// Reaches into a world that does not exist yet. Counted, never guessed.
				++NotImplemented;
				break;
		}
	}

	/// <summary>
	/// <c>COAST</c>, the whole of a script's reach into its own ride - see <see cref="RideState"/>.
	///
	/// <para>
	/// The two halves of the instruction are treated differently, and the difference is load-bearing.
	/// The selector is fetched <b>without being resolved</b>, so a variable operand keeps its tag and
	/// falls straight out of the engine's <c>DEC</c> / <c>CMP 7</c> / <c>JA</c> range instead of
	/// naming an op; all 144 shipped uses are literals, so that never happens in practice. The
	/// argument is resolved for the ops that set something, and left alone for the two that read,
	/// because those write their answer to it.
	/// </para>
	///
	/// <para>
	/// Ops 2 and 3 go through <see cref="Store"/> so the answer lands in the result register whether or
	/// not the destination is a variable. That is what makes <c>COAST 2 0</c> work, and every one of
	/// the twelve shipped uses of op 2 is written that way: the literal zero is a deliberate dummy
	/// destination, the write is skipped, and the branch that follows reads the register.
	/// </para>
	/// </summary>
	private void Coast( IReadOnlyList<RideOperand> operands )
	{
		var selector = operands[0].Kind == RideOperandKind.Literal ? operands[0].Value : -1;
		var argument = operands[1];

		if ( Ride is null )
		{
			++NotImplemented;
			return;
		}

		switch ( selector )
		{
			case 1:
				Ride.AddRider( Value( argument ) );
				break;

			case 2:
				Store( argument, Ride.RoomRemaining() );
				break;

			case 3:
				Store( argument, Ride.TakeRider() );
				break;

			case 4:
				Ride.SetBroken( Value( argument ) );
				break;

			case 5:
				Ride.SetClosed( Value( argument ) );
				break;

			case 6:
				Ride.SetCapacity( Value( argument ) );
				break;

			case 7:
				// Reads its operand and throws it away, as the shipped handler does.
				Ride.SetWorn( Value( argument ) );
				break;

			case 8:
				// The original looks the ride up here and keeps the handle. The caller has already
				// supplied one, so all that is left is to record that the script asked.
				Initialised = true;
				break;

			default:
				// Outside 1..8 the engine logs and carries on, which is a no-op with a complaint.
				++NotImplemented;
				break;
		}
	}

	/// <summary>
	/// <c>ADDOBJ</c>: start a particle or a sound and keep the record a <c>KILLOBJ</c> will look for.
	///
	/// <para>
	/// All four operands are resolved like any other value - the handler gives each one the same tag test
	/// - and they are, in order, the type, the node, the effect id and the tag. <b>The fourth is a tag and
	/// not a duration</b>: it is the field <c>KILLOBJ</c> compares against, and the values the corpus
	/// kills are exactly the values its <c>ADDOBJ</c>s create.
	/// </para>
	/// </summary>
	private void AddObject( IReadOnlyList<RideOperand> operands )
	{
		if ( Effects is null )
		{
			++NotImplemented;
			return;
		}

		Effects.Add( Value( operands[0] ), Value( operands[1] ), Value( operands[2] ), Value( operands[3] ) );
	}

	/// <summary>
	/// <c>EVENT</c>: start one and keep nothing. The handler calls the same worker <c>ADDOBJ</c> does and
	/// then returns without storing what came back, so an event's effect can never be stopped again.
	/// </summary>
	private void TriggerEvent( IReadOnlyList<RideOperand> operands )
	{
		if ( Effects is null )
		{
			++NotImplemented;
			return;
		}

		Effects.Trigger( Value( operands[0] ), Value( operands[1] ), Value( operands[2] ) );
	}

	/// <summary>
	/// <c>KILLOBJ</c>: stop every record carrying the tag - see <see cref="RideEffects.Kill"/> for why
	/// every one rather than the first.
	/// </summary>
	private void KillObjects( RideOperand tag )
	{
		if ( Effects is null )
		{
			++NotImplemented;
			return;
		}

		Effects.Kill( Value( tag ) );
	}

	/// <summary>
	/// <c>LIMBO</c>: hold someone for a while, and answer whether there was room.
	///
	/// <para>
	/// <b>The duration is in seconds.</b> The engine multiplies the operand by a thousand before adding
	/// it to the clock - three chained <c>LEA</c>s coming to 125, then a scale of eight - so the twenty
	/// shipped <c>LIMBO $0 5</c> instructions hold someone for five seconds. The published docs call this
	/// operand "unknown, possibly related to LIMBOSPACE, but may also be duration".
	/// </para>
	///
	/// <para>
	/// A free slot is one with no handle in it, and the search starts from the beginning every time, so
	/// a slot that has been emptied is filled again before any later one. There is no separate test for
	/// being full: the walk simply finds nothing and falls out with nought.
	/// </para>
	///
	/// <para>
	/// <b>One engine defect is reproduced rather than quietly fixed:</b> a handle of nought is written
	/// into the slot and counted, but a slot holding nought is exactly what the engine calls free - so
	/// the tally and the slots disagree from then on, and whoever it was can never be found again. No
	/// shipped script can reach it, because all 24 guard the instruction with a test of the variable
	/// that would name the guest. Inventing a guard the engine does not have would be the bigger lie.
	/// </para>
	/// </summary>
	private bool SendToLimbo( float now, int handle, int seconds )
	{
		for ( int slot = 0; slot < _limbo.Length; ++slot )
		{
			if ( _limbo[slot].Handle != 0 )
				continue;

			_limbo[slot] = new LimboSlot { Handle = handle, Release = now + (seconds * LimboSecond) };
			++_inLimbo;

			return true;
		}

		return false;
	}

	/// <summary>
	/// <c>UNLIMBO</c>: whoever is due back, or nought when nobody is.
	///
	/// <para>
	/// The engine walks from the first slot and takes the first occupied one whose release has gone
	/// <b>strictly</b> past the clock - <c>CMP [slot+4],clock</c> and a signed <c>JL</c> - so it is "the
	/// first one due" rather than "the one who has waited longest". Nought is how it says nobody, and
	/// every shipped use branches on exactly that.
	/// </para>
	/// </summary>
	private int TakeFromLimbo( float now )
	{
		for ( int slot = 0; slot < _limbo.Length; ++slot )
		{
			if ( _limbo[slot].Handle == 0 || _limbo[slot].Release >= now )
				continue;

			return Release( slot );
		}

		return 0;
	}

	/// <summary>
	/// <c>FORCEUNLIMBO</c>: the same walk with the clock test taken out, which is how a shop empties
	/// itself when it shuts.
	///
	/// <para>
	/// <b>It refuses a destination that is not a variable before it does anything at all.</b> The tag
	/// test comes first and the handler leaves through <c>NOP</c>'s own exit, so it does not even reach
	/// the result register - unlike <see cref="Store"/>, which sets the register whatever happens. All 23
	/// shipped uses name a variable, so the refusal never fires in the corpus; it is here because it is
	/// the one place in the family where the order of the engine's own tests is visible.
	/// </para>
	/// </summary>
	private void ForceFromLimbo( RideOperand destination )
	{
		if ( destination.Kind != RideOperandKind.Variable )
		{
			++IgnoredWrites;
			return;
		}

		for ( int slot = 0; slot < _limbo.Length; ++slot )
		{
			if ( _limbo[slot].Handle == 0 )
				continue;

			Store( destination, Release( slot ) );
			return;
		}

		Store( destination, 0 );
	}

	/// <summary>Empties a slot and answers who was in it, as both ways out of limbo do.</summary>
	private int Release( int slot )
	{
		var handle = _limbo[slot].Handle;

		_limbo[slot] = default;
		--_inLimbo;

		return handle;
	}

	/// <summary>
	/// <c>BOUNCE</c>: put somebody on the ride for a number of seconds, and say whether there was room.
	///
	/// <para>
	/// The engine fills the first free slot with four words - the rider, the node they are on
	/// (<see cref="_bounceNode"/> plus the slot's own index), the clock reading they are due off at, and
	/// the clock reading they got on at - then adds one to the tally. With no free slot it answers nought
	/// and does nothing, which is the only refusal it has: <b>it never consults
	/// <c>VAR_CAPACITY</c></b>. Bouncy gates itself on that variable before it ever gets here
	/// (<c>BOUNCING VAR_TEMP</c> / <c>CMP VAR_CAPACITY, VAR_TEMP</c>), so the array size is a ceiling
	/// rather than the ride's capacity.
	/// </para>
	/// <para>
	/// The duration is in <b>seconds</b>: the engine multiplies by a thousand before adding it to the
	/// clock, through the same <c>LEA</c> chain (x5, x25, x125, then x8) it uses elsewhere.
	/// </para>
	/// </summary>
	private bool Bounce( float now, int handle, int seconds )
	{
		for ( int slot = 0; slot < _bounce.Length; ++slot )
		{
			if ( _bounce[slot].Handle != 0 )
				continue;

			_bounce[slot] = new BounceSlot
			{
				Handle = handle,
				Node = _bounceNode + slot,
				Expiry = now + (seconds * BounceSecond),
				Start = now,
			};

			++_bouncing;

			return true;
		}

		return false;
	}

	/// <summary>
	/// <c>UNBOUNCE</c> and <c>FORCEUNBOUNCE</c>: whoever is ready to come off, or nought when nobody is.
	///
	/// <para>
	/// <b>These WRITE their operand rather than reading it</b>, which is the whole reason
	/// <c>VAR_LETMEOFF</c> is an outbox: the script fills it with whoever came off and the engine clears
	/// it once they are walked to the exit. The inherited <c>RideVM</c> handler in <c>VM/Handlers</c> has
	/// this backwards, removing a visitor named by the operand, and is dead code.
	/// </para>
	/// <para>
	/// Both walk from the first slot and take the first occupied one that may leave. The only difference
	/// is the duration: <c>UNBOUNCE</c> also requires the rider to be past their expiry - strictly past,
	/// a signed compare - and <c>FORCEUNBOUNCE</c> drops that test alone. <b>Both still honour
	/// <see cref="BounceWindow"/></b>, so "forcible" means "whatever the duration said", not
	/// "unconditionally".
	/// </para>
	/// <para>
	/// One difference is deliberate and immaterial: the engine reads the clock once before the walk for
	/// the expiry test and again inside it for the elapsed time. There is one reading here, because the
	/// two differ by less than the millisecond either is measured in.
	/// </para>
	/// </summary>
	private int Unbounce( float now, bool whenDue )
	{
		for ( int slot = 0; slot < _bounce.Length; ++slot )
		{
			if ( _bounce[slot].Handle == 0 )
				continue;

			if ( whenDue && _bounce[slot].Expiry >= now )
				continue;

			if ( (int)(now - _bounce[slot].Start) % BounceSecond / BounceWindow != 0 )
				continue;

			var handle = _bounce[slot].Handle;

			_bounce[slot] = default;
			--_bouncing;

			return handle;
		}

		return 0;
	}

	/// <summary>
	/// <c>NAME</c>: the script says what it is called.
	///
	/// <para>
	/// The handler stores the operand's string <b>offset</b> at <c>+0x74</c> and does nothing else, and it
	/// requires the string tag - a literal where a string belongs leaves through <c>NOP</c>'s own exit.
	/// All 277 shipped uses carry the tag, so the check turns nothing away; it is here because that offset
	/// is what <c>FINDSCRIPTRAND</c> reads back, and an untagged one would make a script findable under a
	/// name it never took.
	/// </para>
	/// </summary>
	private void TakeName( RideOperand operand )
	{
		if ( operand.Kind != RideOperandKind.String )
			return;

		_nameOffset = operand.Value;
		Name = _file.StringAt( operand.Value ) ?? Name;
	}

	/// <summary>
	/// <c>SPAWNCHILD</c>: load another script and keep it as this one's child.
	///
	/// <para>
	/// The engine builds a path from its own directory at <c>+0x38</c> and the string operand, hands it to
	/// the loader, and keeps <b>the id</b> that comes back rather than a pointer. It then finds the new
	/// script in the registry and copies five things into it: this script's id, as the child's parent, and
	/// the fields at <c>+0xac</c>, <c>+0x9c</c>, <c>+0xb4</c> and <c>+0xc8</c>. The last of those is the
	/// model handle and <c>+0xac</c> is the thing, both of which are copied here; <c>+0x9c</c> and
	/// <c>+0xb4</c> remain unmodelled and are nought on both sides.
	/// </para>
	///
	/// <para>
	/// <b>That copy used to be nothing at all, and this file said so.</b> While no script had a thing or a
	/// model, every one of the five was nought on both sides and the parent id was the whole of it. Handing
	/// a placed thing its own model is what made the sentence false - a spawned script drives the same
	/// thing its parent does, and a child that lost the model would answer the engine's floor where its
	/// parent answers a real clip length.
	/// </para>
	///
	/// <para>
	/// <b>It does not take down the child it replaces, and that is reproduced rather than tidied.</b> There
	/// is one slot; a second <c>SPAWNCHILD</c> overwrites it and the first child runs on with nobody
	/// holding it. All four shipped scripts that spawn inside a loop run a <c>REMOVECHILD</c> first -
	/// <c>tvsim</c> at word 205 before 211, <c>arcade</c> at 93 before 127, <c>Volcano</c> at 284 before
	/// 290, <c>droid</c> at 43 before 69 - so it is the content that keeps the slot tidy, not the engine.
	/// </para>
	///
	/// <para>
	/// <b>It writes no result.</b> Unlike nearly everything else here the handler never touches the result
	/// register, so whatever the instruction before it left there survives.
	/// </para>
	/// </summary>
	private void SpawnChild( RideOperand name )
	{
		if ( !CanSpawn( name, out var path ) )
			return;

		var id = Host!.Spawn( path );

		// SPAWNCHILD guards the loader's answer where SPAWNSOUND does not - TEST EAX,EAX / JZ at 0x55512e.
		if ( id == 0 )
			return;

		ChildId = id;

		var child = Host.Find( id );

		if ( child is null )
			return;

		child.ParentId = Id;

		// The engine copies the thing and its model handle into the child too, so a spawned script drives
		// the same thing its parent does rather than nothing at all. This used to be nothing to copy -
		// both fields were nought on both sides - and it stopped being nothing the moment a placed thing
		// was handed its own model.
		child.ThingId = ThingId;
		child.Animations = Animations;
	}

	/// <summary>
	/// <c>SPAWNSOUND</c>: the same load into a different slot, and <b>no link of any kind</b>.
	///
	/// <para>
	/// The handler is <c>SPAWNCHILD</c>'s twin up to the loader call and then simply stops: it stores the
	/// id at <c>+0x14</c> and returns. So the script it spawns has no parent, is not this script's child,
	/// cannot be reached by <c>SETVARINCHILD</c> and cannot be removed by <c>REMOVECHILD</c> - it is taken
	/// down only when this script itself dies.
	/// </para>
	/// </summary>
	private void SpawnSound( RideOperand name )
	{
		if ( !CanSpawn( name, out var path ) )
			return;

		// No guard here, and the asymmetry is the engine's: SPAWNCHILD tests the loader's answer before it
		// stores, this one stores whatever came back. So a load that failed writes a nought into the slot
		// and empties it, where the same failure leaves SPAWNCHILD's slot untouched.
		SoundChildId = Host!.Spawn( path );
	}

	/// <summary>
	/// The half both spawning instructions share: everything that has to hold before the loader is called
	/// at all.
	///
	/// <para>
	/// A non-string operand is the engine's silent no-op - both handlers test the tag and leave through
	/// <c>NOP</c>'s exit without touching their slot - so it must be distinguishable from a load that was
	/// attempted and failed, which is the one case that writes a nought back.
	/// </para>
	/// </summary>
	private bool CanSpawn( RideOperand name, out string path )
	{
		path = string.Empty;

		if ( Host is null )
		{
			// Nowhere to put a script even if one could be read. Counted, as COAST is without a ride.
			++NotImplemented;
			return false;
		}

		if ( name.Kind != RideOperandKind.String )
			return false;

		var text = _file.StringAt( name.Value );

		if ( text is null )
			return false;

		// The engine builds the path the same way, from the prefix its loader left at +0x38 - see
		// Directory, and note that the prefix is what makes the answer unambiguous rather than merely
		// convenient.
		path = Directory.Length == 0 ? text : $"{Directory}/{text}";

		return true;
	}

	/// <summary>
	/// <c>REMOVECHILD</c>: kill the child, rather than merely forgetting it.
	///
	/// <para>
	/// The handler hands the child's id to the same teardown the tick loop runs on a script that has
	/// stopped, with a second argument of nought - which is what decides that the dying script's effects
	/// are not turned into particles on the way out - and then clears the slot. With no child it does
	/// nothing whatever, which is every script's state until a <c>SPAWNCHILD</c> has run.
	/// </para>
	/// </summary>
	private void RemoveChild()
	{
		if ( ChildId == 0 )
			return;

		Host?.Destroy( ChildId );
		ChildId = 0;
	}

	/// <summary>
	/// <c>SETVARINCHILD</c> and <c>SETVARINPARENT</c> - one block of engine code reached from two places:
	/// write a value into another script's variable.
	///
	/// <para>
	/// <b>The bound is checked from above only.</b> The engine compares the index against the target's own
	/// variable count at <c>+0x8c</c> and gives up if it is not less, and never asks whether it is
	/// negative - so a negative index writes behind the array. <b>That one is named rather than
	/// reproduced</b>: where the write would land is a fact about the original's allocator and not about
	/// the instruction, and every shipped use names a literal 0 or 1 anyway.
	/// </para>
	///
	/// <para>
	/// The result register is set only when the write actually happened, and the instruction is skipped
	/// entirely where no such script exists - which in the engine, having found nobody, is a read through
	/// a null pointer.
	/// </para>
	/// </summary>
	private void SetVariableIn( int id, int index, int value )
	{
		if ( id == 0 )
			return;

		var target = Host?.Find( id );

		if ( target is null || index < 0 || index >= target.Slots )
			return;

		target._variables[index] = value;
		Result = value;
	}

	/// <summary>
	/// <c>GETVARINCHILD</c> and <c>GETVARINPARENT</c>: read another script's variable into one of ours.
	///
	/// <para>
	/// <b>The destination is tested before anything else happens</b> - before the child or parent id is so
	/// much as looked at - and one that is not a variable leaves through <c>NOP</c>'s own exit without
	/// ever reaching the result register. That is <c>FORCEUNLIMBO</c>'s shape rather than
	/// <see cref="Store"/>'s, and all 19 shipped uses name a variable, so the refusal never fires in the
	/// corpus.
	/// </para>
	///
	/// <para>
	/// <b>The missing lower bound is this pair's too, not only the writing pair's.</b> These two share
	/// their own tail at <c>0x5555ad</c> and it compares the index against the target's count from above
	/// only, so a negative index is an out-of-bounds <i>read</i> whose value is then written into a
	/// variable. It is refused here for the same reason the write is - see <see cref="SetVariableIn"/>.
	/// </para>
	/// </summary>
	private void GetVariableIn( int id, RideOperand destination, int index )
	{
		if ( destination.Kind != RideOperandKind.Variable )
		{
			++IgnoredWrites;
			return;
		}

		if ( id == 0 )
			return;

		var target = Host?.Find( id );

		if ( target is null || index < 0 || index >= target.Slots )
			return;

		Store( destination, target._variables[index] );
	}

	/// <summary>
	/// <c>GETREMOTEVAR</c>: read a variable out of any script at all, by that script's id.
	///
	/// <para>
	/// <b>Its three operands are destination, script id, variable id, in that order</b> - the published
	/// docs call all three unknown. The handler fetches the first without resolving it, keeping it for the
	/// write; resolves the second and hands it to the registry lookup; and resolves the third as an index.
	/// </para>
	///
	/// <para>
	/// <b>Failing is not silence: it answers nought.</b> The handler zeroes the result register before it
	/// looks anything up, and every way of failing - no such script, an index below nought or past the
	/// target's count - jumps to a tail that still writes that nought into the destination. So a single
	/// <see cref="Store"/> covers both paths, and a script branching on the answer reads a real nought
	/// rather than whatever happened to be lying in the register.
	/// </para>
	/// </summary>
	private void GetRemoteVariable( RideOperand destination, int id, int index )
	{
		var target = Host?.Find( id );
		var found = target is not null && index >= 0 && index < target.Slots;

		Store( destination, found ? target!._variables[index] : 0 );
	}

	/// <summary>
	/// <c>SETREMOTEVAR</c>: write a variable in any script at all, by that script's id.
	///
	/// <para>
	/// <b>This one checks both ends of the index</b>, where its parent and child cousins check only the
	/// top, and it complains and returns rather than writing when it fails - leaving the result register
	/// alone. The asymmetry is the engine's own: there is a guard here and none fifty lines earlier.
	/// </para>
	/// </summary>
	private void SetRemoteVariable( int id, int index, int value )
	{
		var target = Host?.Find( id );

		if ( target is null || index < 0 || index >= target.Slots )
			return;

		target._variables[index] = value;
		Result = value;
	}

	/// <summary>
	/// <c>FINDSCRIPTRAND</c>: pick one of the scripts calling themselves a given name, at random, and
	/// answer its id.
	///
	/// <para>
	/// <b>It matches on what <c>NAME</c> stored</b>, comparing the text at the candidate's own <c>+0x34</c>
	/// plus its <c>+0x74</c> - so a script that has never run <c>NAME</c> can never be found, because the
	/// loader leaves that offset negative and the walk skips it. Both names the corpus looks for are real:
	/// <c>bus.RSE</c> asks four times for "Traffic Lights" and <c>zob.RSE</c> once for "Zob Upgrade", and
	/// a shipped script takes each of those names.
	/// </para>
	///
	/// <para>
	/// The engine walks the registry twice - once to count the matches, once to reach the chosen one - and
	/// the draw between them is <c>1 + (drawn mod count)</c> against the same generator <c>RAND</c> uses.
	/// <b>The caller is not excluded</b>, so a script looking for its own name can find itself.
	/// </para>
	///
	/// <para>
	/// <b>Which of them it picks cannot be reproduced, and that is settled rather than unfinished.</b> The
	/// engine's generator is one counter for the whole game, read from some 680 places of which only three
	/// are instructions, so what this draws in the original depends on how much unrelated game code drew
	/// before it. A per-script generator gets the distribution right and the particular answer wrong,
	/// which is the deviation <see cref="NextRandom"/> already carries and names.
	/// </para>
	///
	/// <para>
	/// <b>A name is not unique, and a machine loading one script per file would make this degenerate.</b>
	/// The engine loads a script for every placed thing, so a park with five traffic lights has five live
	/// scripts all answering to "Traffic Lights" and the draw is genuinely over five.
	/// </para>
	/// </summary>
	private void FindScriptAtRandom( RideOperand name, RideOperand destination )
	{
		// Zeroed before the operand is so much as tested for being a string, so a wrongly tagged one still
		// leaves a nought behind rather than the previous instruction's answer.
		Result = 0;

		if ( name.Kind != RideOperandKind.String || Host is null )
			return;

		var wanted = _file.StringAt( name.Value );

		if ( wanted is null )
			return;

		var matches = new List<RideScript>();

		foreach ( var candidate in Host.NewestFirst() )
		{
			if ( candidate.IsNamed && string.Equals( candidate.Name, wanted, StringComparison.Ordinal ) )
				matches.Add( candidate );
		}

		if ( matches.Count == 0 )
			return;

		Store( destination, matches[Math.Abs( NextDraw() % matches.Count )].Id );
	}

	/// <summary>
	/// <c>DIPMUSIC</c>: mute the music, and remember that this script is the one holding it down.
	///
	/// <para>
	/// <b>It is a mute rather than a partial duck</b>, which is worth stating because the opcode's name
	/// suggests otherwise. The value goes into one global, and when the mixer next re-applies its group
	/// volumes it tests that global against nought and, if it is set, drives the music group's volume to
	/// <b>0</b> outright - where the unset branch uses the configured volume. Speech ducking is a
	/// separate mechanism through a different global that scales by a percentage; this one does not.
	/// </para>
	///
	/// <para>
	/// <b>Any non-nought value mutes.</b> The test is against zero, not against one, so the operand is
	/// not the 0-or-1 switch it looks like - though every shipped use passes a literal 1.
	/// </para>
	///
	/// <para>
	/// <b>The frame byte and the global can disagree, and that is the engine's.</b> It stores the low
	/// <i>byte</i> as the marker and passes the low <i>word</i> on, so a value of 0x100 would mute while
	/// the marker read nought - and the music would then never come back up, because the teardown
	/// consults the marker. Nothing shipped can reach it.
	/// </para>
	/// </summary>
	private void DipMusic( int value )
	{
		if ( Host is null )
		{
			// The mute is one setting for the whole game, and there is nowhere to put it without the
			// registry - counted, as COAST is without a ride.
			++NotImplemented;
			return;
		}

		_dipped = (value & 0xFF) != 0;
		Host.MusicDip = value & 0xFFFF;
	}

	/// <summary>
	/// <c>SETOBJPARAM</c>: set a parameter on every effect carrying a tag - see
	/// <see cref="RideEffects.SetParameter"/>, which is where the engine's two-case type dispatch lives.
	/// </summary>
	private void SetObjectParameter( IReadOnlyList<RideOperand> operands )
	{
		if ( Effects is null )
		{
			++NotImplemented;
			return;
		}

		Effects.SetParameter( Value( operands[0] ), Value( operands[1] ), Value( operands[2] ) );
	}

	/// <summary>
	/// How many variables another script will let this one reach - its field <c>+0x8c</c>, which is the
	/// count out of the file header rather than the length of the array. The two differ only where a
	/// script names more variables than it declares.
	/// </summary>
	private int Slots => Math.Max( _file.VariableCount, 0 );

	/// <summary>
	/// One turn of the engine's generator (<c>FUN_00516330</c>), halved - what <c>RAND</c> and
	/// <c>FINDSCRIPTRAND</c> both draw before they take their different remainders of it.
	/// </summary>
	private int NextDraw()
	{
		_random = (_random * 0x19660Du) + 0x3C6EF35Fu;
		_random = (_random >> 13) | (_random << 19);

		return Math.Abs( (int)_random ) >> 1;
	}

	/// <summary>
	/// An operand read as a number: a variable's value, or the low 16 bits sign-extended. The engine
	/// resolves every value operand exactly these two ways and no other (FUN_005573a0).
	/// </summary>
	private int Value( RideOperand operand )
	{
		if ( operand.Kind != RideOperandKind.Variable )
			return (short)operand.Value;

		return Read( operand.Value );
	}

	private int Read( int index ) => index >= 0 && index < _variables.Length ? _variables[index] : 0;

	/// <summary>
	/// Writes a result. The result register is set whatever happens - the engine stores it before it
	/// looks at the destination - and the variable only when the destination is one.
	/// </summary>
	private void Store( RideOperand destination, int value )
	{
		Result = value;

		if ( destination.Kind != RideOperandKind.Variable )
		{
			++IgnoredWrites;
			return;
		}

		if ( destination.Value >= 0 && destination.Value < _variables.Length )
			_variables[destination.Value] = value;
		else
			++IgnoredWrites;
	}

	/// <summary>Signed division where a zero divisor yields zero rather than throwing, as the engine tests for it.</summary>
	private static int Divide( int left, int right, bool remainder )
	{
		if ( right == 0 )
			return 0;

		// int.MinValue / -1 overflows on the CLR where the original would simply wrap; neither case
		// occurs in any shipped script, and answering 0 keeps a script running rather than crashing it.
		if ( left == int.MinValue && right == -1 )
			return 0;

		return remainder ? left % right : left / right;
	}

	/// <summary>A branch operand has to be a location. When it is not, the engine parks the script.</summary>
	private void Jump( RideOperand target )
	{
		if ( target.Kind != RideOperandKind.Location )
		{
			Running = false;
			return;
		}

		Position = target.Value;
	}

	private void Call( RideOperand target )
	{
		if ( _stack.Length == 0 || _calls < 0 || _calls >= _stack.Length )
		{
			Running = false;
			return;
		}

		_stack[_calls--] = Position;
		Jump( target );
	}

	private void Return()
	{
		if ( _stack.Length == 0 || _calls + 1 >= _stack.Length )
			return;

		Position = _stack[++_calls];
	}

	private void PushCall( int value )
	{
		if ( _stack.Length == 0 || _calls < 0 )
			return;

		_stack[_calls--] = value;
	}

	private int PopCall()
	{
		if ( _stack.Length == 0 || _calls + 1 >= _stack.Length )
			return 0;

		return _stack[++_calls];
	}

	/// <summary>HUSH fills from the bottom, and must not run into the subroutine stack coming down.</summary>
	private void PushValue( int value )
	{
		if ( _values < 0 || _values >= _stack.Length || _values > _calls )
			return;

		_stack[_values++] = value;
	}

	private int PopValue()
	{
		if ( _values <= 0 || _values > _stack.Length )
			return 0;

		return _stack[--_values];
	}

	/// <summary>
	/// Sits on the <c>WAIT</c> rather than blocking: the program counter is put back onto it so the
	/// same instruction runs again next turn, and the turn ends.
	///
	/// <para>
	/// <b>The duration is added to the caller's clock unchanged, and that is now a finding rather than
	/// a shrug.</b> This comment used to say the unit was unestablished. It is milliseconds: the engine
	/// adds the duration to the clock object at <c>0x785970</c>, whose chain
	/// (<c>0x00402d70</c> -> <c>0x00402f10</c> -> <c>0x004030d0</c> -> <c>0x004033a0</c>) ends at a
	/// source that falls back to <c>timeGetTime()</c> and scales its <c>QueryPerformanceCounter</c>
	/// path to agree with it. <b>So a caller must keep its clock in milliseconds</b> - and note that
	/// <see cref="GameClock.Now"/> is in seconds, which would make every wait a thousand times too
	/// long.
	/// </para>
	///
	/// <para>
	/// <b>The engine's speed scaling is deliberately absent, because it cannot ever do anything.</b>
	/// The dispatcher divides the duration by <c>0.5 + 0.01 * speed</c>, worked out afresh for every
	/// instruction from the script's speed word at <c>+0xc0</c>. That word is written in exactly two
	/// places in the whole script system - the loader setting it to 50, and the scheduler copying it
	/// into a linked script - and <b>no opcode writes it</b>, so it is 50 for every script that ever
	/// runs and the divisor is exactly 1. Implementing the division would add a field that could never
	/// differ from one.
	/// </para>
	/// </summary>
	private void Wait( float now, int duration, int length )
	{
		if ( _waitUntil is null )
		{
			// The engine sets the deadline and leaves without looking at it, so even a wait that is
			// already over costs the rest of the turn. Nothing shipped asks for one - every WAIT in the
			// corpus names a positive duration - but WAITANIM, which shares this field, asks for
			// exactly that, and the old shape here would have let it through in the same turn.
			_waitUntil = now + duration;
			Position -= length;
			_budget = 0;
			return;
		}

		if ( now >= _waitUntil )
		{
			_waitUntil = null;
			return;
		}

		Position -= length;
		_budget = 0;
	}

	/// <summary>
	/// What is left of the <c>SETTIMER</c> deadline, never less than zero - the engine stores the
	/// subtraction and then replaces it with zero if it came out negative (<c>JNS</c> at
	/// <c>0x0055646d</c>), so a timer that has run out reads as nought rather than going negative.
	/// </summary>
	private int TimerRemaining( float now )
	{
		var left = (int)(_timerUntil - now);

		return left < 0 ? 0 : left;
	}

	/// <summary>
	/// <c>RAND</c>, whole: a value from 0 to <paramref name="bound"/> <b>inclusive</b>, which is what
	/// the engine's <c>% (bound + 1)</c> gives and what the published docs already said.
	///
	/// <para>
	/// The generator is the engine's own (<c>FUN_00516330</c>): multiply, add, rotate right thirteen,
	/// and take the absolute value. The opcode then halves that, takes it modulo the bound plus one,
	/// and takes the absolute value again.
	/// </para>
	///
	/// <para>
	/// <b>One deviation, named rather than hidden:</b> the engine keeps a single generator for the
	/// whole game, so its scripts draw from one shared sequence and interleave. This keeps one per
	/// script. The arithmetic is identical and a run is repeatable, but the numbers are not the ones
	/// the original would have produced - which they could not be in any case, since what the engine
	/// seeds its generator with is not established.
	/// </para>
	/// </summary>
	private int NextRandom( int bound )
	{
		var drawn = NextDraw();
		var span = bound + 1;

		// The engine would divide by zero here. No shipped script asks for it - every bound in the
		// corpus is between 1 and 5000 - so this answers nought rather than inventing a behaviour.
		if ( span <= 0 )
			return 0;

		return Math.Abs( drawn % span );
	}

	/// <summary>
	/// <c>TRIGANIM</c>: start a one-shot animation, answer how long it runs, and arm the deadline that
	/// <c>WAIT4ANIM</c> waits on.
	///
	/// <para>
	/// The engine asks the model how long the animation is, takes 300 off the answer, and floors the
	/// result at 300 with a <b>signed</b> comparison. With no model it never asks and substitutes
	/// nought, so the sum is <c>0 - 300</c> and the floor catches it: <b>300ms, and that is the
	/// engine's own answer rather than a number chosen here.</b>
	/// </para>
	///
	/// <para>
	/// The length goes through <see cref="Store"/>, so a literal destination - which 64 of the 74
	/// shipped uses write - leaves it in the result register instead, the same idiom as
	/// <c>COAST 2 0</c>.
	/// </para>
	/// </summary>
	private void TriggerAnimation( float now, int role, int entry, RideOperand destination )
	{
		var length = FloorAnimation( StartAnimation( now, role, entry, 0 ) - AnimationSlack );

		Store( destination, length );

		_animationUntil = now + length;
		_looping = OneShot;
	}

	/// <summary>
	/// How long one entry of one role runs, in milliseconds - the engine's <c>FUN_004732a0</c>, reduced
	/// to the part a machine with no animation playing can be faithful about.
	///
	/// <para>
	/// <b>Nought means "no model", and it is not the same answer as "no such animation".</b> With no
	/// model the engine never asks, and nought is what the arithmetic above is done on - which is why a
	/// model-less <c>TRIGANIM</c> answers the 300 floor rather than a number invented here. With a model
	/// but a role or entry it has nothing for, the engine substitutes <see cref="RideAnimations.UnknownLength"/>
	/// instead. <b>That second branch is shipped content, not a hypothetical:</b> eight of the role
	/// references in the game name a role whose file its own archive does not carry - <c>royaloo</c>,
	/// <c>fries</c>, <c>icecream</c> and <c>purse</c> in fantasy, <c>crys_b</c>, <c>scentro</c> and
	/// <c>spawheel</c> in space.
	/// </para>
	///
	/// <para>
	/// <b>The channel is modelled now, so this starts the clip rather than asking about it.</b> The engine
	/// takes the channel over only when it is idle, finished or frozen; otherwise the clip goes in a queue
	/// and the answer becomes <b>the time still to run plus the new clip's length</b>, the two truncated
	/// separately. So what a script is told stops equalling
	/// <see cref="RideAnimations.DurationMilliseconds"/> as soon as two triggers land inside one clip -
	/// which is the engine's own arithmetic, and is reachable in a running park rather than hypothetical.
	/// </para>
	/// </summary>
	private int StartAnimation( float now, int role, int entry, int flags )
	{
		// Null is "no model", which every one of these handlers tests for first. The arithmetic is then
		// done on nought and the floor catches it - so the 300 a model-less TRIGANIM answers is the
		// engine's own number, not a stand-in, and a role the model lacks is a different answer again.
		if ( Animations is null )
			return 0;

		// A literal 1.0, because every triggering handler pushes 0x3f800000. The channel divides by it, so
		// anything else here would change the length a script is told as well as the speed it plays at.
		return Animations.Trigger( role, entry, flags, 1f, (int)now );
	}

	/// <summary>
	/// <c>TRIGANIM</c>'s floor, which is <b>signed</b> - so a length that came out under the slack, which
	/// nought always does, reads as the slack itself rather than as a negative. Its sibling
	/// <c>WAITANIM</c> floors the same number unsigned and gets the opposite answer; see
	/// <see cref="WaitOutAnimation"/>.
	/// </summary>
	private static int FloorAnimation( int length ) => length < AnimationSlack ? AnimationSlack : length;

	/// <summary>
	/// <c>WAITANIM</c>: hold while the animation named runs.
	///
	/// <para>
	/// <b>With no model this costs exactly one turn, and the 300 it looks like it should wait is not
	/// what it waits.</b> Its arithmetic is its sibling's with two differences, and both of them
	/// matter. It stores the length-less-300 as the <b>low half of a qword whose high half is nought</b>
	/// and does a <c>FILD qword</c>, so -300 is read as 4,294,966,996; <c>__ftol</c> converts that
	/// exactly (it is a <c>FISTP qword</c> that hands back the low dword, so nothing overflows) and
	/// -300 comes back out. It then compares against the 300 floor <b>unsigned</b>, which a negative
	/// passes. So the deadline is <c>clock - 300</c> - already past - and the instruction rewinds and
	/// gives up the turn anyway, because the engine sets the deadline without looking at it. The next
	/// turn walks straight through.
	/// </para>
	///
	/// <para>
	/// <b>One deviation, named rather than hidden:</b> the engine holds that deadline as an unsigned
	/// dword, so a clock under 300ms would wrap it to something enormous and park the script for about
	/// 49 days. Nothing can reach that - a ride's scripts do not run in the first three tenths of a
	/// second of a game - and reproducing it would only turn an unreachable case into a hang.
	/// </para>
	/// </summary>
	private void WaitOutAnimation( float now, int role, int entry, int length )
	{
		// The handler's first test is whether its deadline field is empty, and only then does it trigger
		// (0x005529bc: CMP [EBP+0xa0],EDI / JZ). Every later visit takes the re-entry path and only rewinds.
		// Without this guard the clip would be started again on every turn the script sat here, which would
		// hold it at its first frame for as long as it waited.
		if ( _waitUntil is not null )
		{
			Wait( now, 0, length );
			return;
		}

		var duration = StartAnimation( now, role, entry, 0 ) - AnimationSlack;

		// The floor its sibling applies SIGNED, this one applies UNSIGNED - so a negative sails straight
		// past it where TRIGANIM's catches it, and only a genuinely short positive length is raised to the
		// slack. With no model the length is nought, the subtraction gives -300, and that is what the
		// deadline gets: already past, so the instruction costs exactly one turn.
		if ( (uint)duration < (uint)AnimationSlack )
			duration = AnimationSlack;

		Wait( now, duration, length );
	}

	/// <summary>
	/// <c>LOOPANIM</c>: set an animation looping, unless that same one already is.
	///
	/// <para>
	/// Asking again for the animation already running is the engine's early exit and does nothing at
	/// all - which matters because the other path <b>clears the <c>WAIT4ANIM</c> deadline</b>: a loop
	/// never finishes, so there is nothing left to wait for. <b>That guard is now load-bearing rather than
	/// ceremonial</b>: this really does start the clip, so without it a script sitting in a loop would
	/// restart its animation on every single turn and hold it at the first frame for ever. The Drinks Shop
	/// runs <c>LOOPANIM 5 0</c> twice and the Belly Bounce <c>LOOPANIM 2 0</c> twice, so it is reached by
	/// shipped content and not only in principle.
	/// </para>
	///
	/// <para>
	/// <b>The length is discarded.</b> The engine calls the same trigger the others do and throws the
	/// answer away without storing it anywhere, which is why <c>LOOPANIM</c> has no destination operand.
	/// </para>
	/// </summary>
	private void Loop( float now, int role, int entry )
	{
		// The key the engine compares is built by addition rather than by an or, which differs only if a
		// variable holds more than sixteen bits.
		var key = (entry << 16) + role;

		if ( _looping == key )
			return;

		StartAnimation( now, role, entry, AnimTimeControl.LoopFlag );

		_animationUntil = null;
		_looping = key;
	}

	/// <summary>
	/// <c>WAIT4ANIM</c>: hold until the animation last triggered has run its length.
	///
	/// <para>
	/// <b>With nothing triggered it does not wait at all.</b> The handler's first test is whether the
	/// deadline is nought and it leaves if it is, so a script reaching this without a trigger walks
	/// past - which is what makes the instruction honest before anything animates, and what stops the
	/// 74 scripts that use it being parked for ever.
	/// </para>
	/// </summary>
	private void WaitForAnimation( float now, int length )
	{
		if ( _animationUntil is null )
			return;

		if ( now >= _animationUntil )
		{
			_animationUntil = null;
			return;
		}

		Position -= length;
		_budget = 0;
	}
}
