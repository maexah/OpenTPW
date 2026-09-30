namespace OpenTPW;

/// <summary>
/// One instruction of a world-sprite script.
///
/// <para>
/// The executable has <b>eighteen</b> of these, and the person scripts use exactly the first four - counted, not
/// assumed: a decode of all 1,857 words of the script array agrees with a tally taken independently by value
/// on every one of the eighteen. The let-go balloon adds three more and the end word. The other eleven (the rest
/// of the locals arithmetic, a gosub on the same twenty-deep stack and an eight-way comparison jump table at
/// <c>0x004762b0</c>) are reached only by scripts that belong to things this project does not draw yet, so they
/// are left out rather than written blind.
/// </para>
/// </summary>
internal enum SpriteOp
{
	/// <summary>
	/// Writes a value into one of the instance's own words - <c>LAB_004766d0</c>, two operands. Every person
	/// script opens with the same one, writing <c>0x1200</c> into <c>+0xc4</c>, the draw's flags word
	/// (<c>FUN_00542010</c> reads it at <c>0x00542075</c>); the let-go balloon sets its alpha with it.
	/// </summary>
	SetLocal,

	/// <summary>
	/// Chooses the picture set - <c>LAB_004768b0</c>, one operand, writing <c>+0xb4</c>. It does <b>not</b>
	/// yield, so the set and the first frame of a script are shown on the same turn.
	/// </summary>
	SetSet,

	/// <summary>
	/// Shows a frame and yields - <c>LAB_00476940</c>, one operand, writing <c>+0xb8</c>. 581 of the array's
	/// 863 instructions are this one, and it is the only thing in a person script that stops a turn.
	/// </summary>
	Frame,

	/// <summary>
	/// Moves the program counter - <c>LAB_00476020</c>, one operand. <b>It does not change which script is
	/// running</b>, only where in the array the next instruction is read from; see
	/// <see cref="SpriteScript.Script"/>.
	/// </summary>
	Jump,

	/// <summary>
	/// Takes a value off one of the instance's words - <c>0x004767e0</c>, two operands, an integer <c>SUB</c>
	/// for the words past the six float locals (<c>0x0047682f</c>). The let-go balloon fades its alpha with it.
	/// </summary>
	SubLocal,

	/// <summary>
	/// Marks where a loop starts - <c>0x004763b0</c>, no operand. It pushes the program counter, already past
	/// itself, onto the instance's twenty-deep stack (<c>FUN_00475230</c>) and counts it in <c>+0x78</c>.
	/// </summary>
	LoopStart,

	/// <summary>
	/// Loops while a word compares true - <c>0x004763d0</c>, three operands: the word, the comparison and the
	/// value. True jumps back to the pushed start and keeps it pushed; false pops it (<c>0x00476673</c>), and
	/// with nothing pushed it only logs. Comparison 8 is <c>word &gt;= value</c>, signed (the table at
	/// <c>0x00476678</c>, entry 7, <c>0x004764c2</c>), the only one in any script copied here.
	/// </summary>
	LoopWhile,

	/// <summary>
	/// The end of a script - the word <c>0x005da3c0</c>, a bare <c>RET</c> that the VM spots rather than calls
	/// (<c>0x0047509d</c>..<c>0x004750af</c>): the instance is hidden and stops, and <c>FUN_00475360</c> frees
	/// it on its next due turn.
	/// </summary>
	End
}

/// <summary>
/// The animation a person is drawn with, played the way the original plays it: a tiny program out of
/// <c>testme.exe</c> that chooses a set, then shows one frame per turn, then jumps.
///
/// <para>
/// <b>This is what moves a person's picture on.</b> <see cref="ParkWorld.Sprite.Frame"/> is only where the
/// save left it. The frame is not a function of anything - it is stepped by a script.
/// </para>
/// <para>
/// <b>The scripts are compiled into the executable, not stored in the game's data.</b> They are one flat array
/// of 1,857 words at <c>DAT_0074dab8</c> holding 83 scripts back to back, each word either the address of an
/// opcode handler or an operand of the one before it. <c>FUN_00475730</c> points every instance at that array
/// unconditionally and reads no bytecode from the save at all - the save's <c>SPSC</c> module is the instance
/// table, not the scripts. So they are copied out here, exactly as <see cref="PeepHeading"/>'s arctangent is,
/// and for the same reason: they are the original's data and there is nothing to compute them from.
/// </para>
/// <para>
/// <b>How the copy was checked.</b> Walking the array from end to end with the operand count of each opcode
/// either stays in step or lands on a word that is not a handler, and it stayed in step across all 1,857.
/// That is what makes the operand counts a measurement rather than an arrangement that happens to add up.
/// Every person script's
/// instruction addresses were then rebuilt from the compact form below and compared against the decode, and
/// all twenty-one matched.
/// </para>
/// <para>
/// <b>A person script is always: set a set, show some frames, jump.</b> No loops, no branches, no subroutine
/// calls - those exist in the opcode set and no person script uses one. The jump is usually back to the
/// script's own first instruction, which is what makes a walk cycle; the one-shot animations instead jump to
/// <b>90</b>, the standing script, so they play once and settle.
/// </para>
/// </summary>
public sealed class SpriteScript
{
	/// <summary>
	/// One script as the executable writes it, in the only shape a person script ever takes.
	///
	/// <para>
	/// <b><see cref="Entry"/> is the real address in the original's array, not an ordinal.</b> It has to be:
	/// the save records which script a sprite is on and how far into it by exactly these numbers, and
	/// <see cref="LoopTo"/> is one of them too.
	/// </para>
	/// </summary>
	private readonly record struct Animation(
		int Index, int Entry, bool Locals, int? Set, int[] Frames, int LoopTo );

	/// <summary>
	/// The twenty-one scripts the person animation table names, read out of <c>DAT_0074dab8</c>.
	///
	/// <para>
	/// Index 1 is the walk and index 3 the stand; index 25 is also the held balloon's script (<see cref="Balloon"/>). Index 2 is the
	/// hurried walk, index 9 the same eight pictures held twice each, which is a walk at half speed.
	/// </para>
	/// </summary>
	private static readonly Animation[] Animations =
	[
		new( Index:  1, Entry:   42, Locals: true,  Set: 1,    Frames: [0, 1, 2, 3, 4, 5, 6, 7], LoopTo:   42 ),
		new( Index:  2, Entry:   66, Locals: true,  Set: 2,    Frames: [0, 1, 2, 3, 4, 5, 6, 7], LoopTo:   66 ),
		new( Index:  3, Entry:   90, Locals: true,  Set: 0,    Frames: [0], LoopTo:   90 ),
		new( Index:  4, Entry:  100, Locals: true,  Set: 14,   Frames: [0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 1], LoopTo:   90 ),
		new( Index:  5, Entry:  140, Locals: true,  Set: 12,   Frames: [0, 1, 2, 3], LoopTo:   90 ),
		new( Index:  6, Entry:  156, Locals: true,  Set: 11,   Frames: [0, 1, 2, 3], LoopTo:   90 ),
		new( Index:  7, Entry:  188, Locals: true,  Set: 6,    Frames: [0, 1, 2, 3], LoopTo:   90 ),
		new( Index:  8, Entry:  172, Locals: true,  Set: 4,    Frames: [0, 1, 2, 3], LoopTo:   90 ),
		new( Index:  9, Entry:    2, Locals: true,  Set: 1,    Frames: [0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7], LoopTo:    2 ),
		new( Index: 10, Entry:  402, Locals: true,  Set: 4,    Frames: [0, 1, 2, 3, 0, 0, 0, 0], LoopTo:  402 ),
		new( Index: 11, Entry:  426, Locals: true,  Set: 4,    Frames: [0, 1, 2, 3, 4, 5, 6, 7, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0], LoopTo:  426 ),
		new( Index: 12, Entry:  498, Locals: true,  Set: 4,    Frames: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 0, 0, 0, 0, 0, 0, 0, 0], LoopTo:  498 ),
		new( Index: 17, Entry: 1224, Locals: true,  Set: 4,    Frames: [0, 0, 1, 1, 2, 2, 3, 3], LoopTo: 1224 ),
		new( Index: 18, Entry: 1248, Locals: true,  Set: 4,    Frames: [0, 1, 2, 3, 4, 5, 6, 7], LoopTo: 1248 ),
		new( Index: 19, Entry: 1282, Locals: true,  Set: 5,    Frames: [0, 1, 2, 3, 0, 0, 0, 0, 0, 0], LoopTo: 1282 ),
		new( Index: 20, Entry: 1272, Locals: true,  Set: 3,    Frames: [0], LoopTo: 1272 ),
		new( Index: 21, Entry: 1208, Locals: true,  Set: 6,    Frames: [0, 1, 2, 3], LoopTo: 1208 ),
		new( Index: 22, Entry: 1310, Locals: true,  Set: 7,    Frames: [0, 1, 2, 3, 4, 5, 6, 7], LoopTo: 1334 ),
		new( Index: 23, Entry: 1334, Locals: true,  Set: 8,    Frames: [0, 1, 2, 3, 4, 5, 6, 7], LoopTo: 1334 ),
		new( Index: 24, Entry: 1358, Locals: true,  Set: 9,    Frames: [0, 1, 2, 3, 4, 5, 6, 7], LoopTo:   90 ),

		// The one script with no preamble at all: it shows the frame it is given and keeps whatever set the
		// sprite already had.
		new( Index: 25, Entry: 1650, Locals: false, Set: null, Frames: [0], LoopTo: 1650 )
	];

	/// <summary>
	/// Which script each animation number runs, from the table at <c>DAT_0075a418</c>.
	///
	/// <para>
	/// <b>Four of its entries are not scripts and must not be read as addresses.</b> Entries 13 to 16 hold the
	/// literals 0, 1, 2 and 3, and <c>FUN_00475b80</c> tells them apart by range: an argument of 0 to 3 is a
	/// <i>state</i>, looked up through the sprite bank's own four groups of bytes at <c>0x14E</c>, and
	/// anything larger is a script address. Those groups are not parsed by this project, so those four
	/// animations are <see cref="None"/> here rather than guessed at.
	/// </para>
	/// </summary>
	private static readonly int[] Table =
	[
		None,  42,   66,   90,  100,  140,  156,  188,  172,    2,
		 402, 426,  498, None, None, None, None, 1224, 1248, 1282,
		1272, 1208, 1310, 1334, 1358, 1650
	];

	/// <summary>An animation number that names no script.</summary>
	public const int None = -1;

	/// <summary>Walking - set 1, eight pictures. What <c>FUN_004fa2a0</c> puts a walking person on.</summary>
	public const int Walking = 1;

	/// <summary>Walking in a hurry - set 2, eight pictures, chosen when the person's purpose speed is up.</summary>
	public const int Hurrying = 2;

	/// <summary>Standing - set 0, one picture. Every one-shot animation ends by jumping into it.</summary>
	public const int Standing = 3;

	// Where each of the instance's words sits. The locals block begins at +0x84, so what the drawing reads is locals -
	// among them 7, 12 and 13 - which is why there is an opcode that writes a frame FROM a local.
	private const int AlphaLocal = 7;           // +0xa0, the draw's alpha byte (FUN_00542010, 0x0054207d)

	private const int SpriteNumberLocal = 12;   // +0xb4

	private const int FrameLocal = 13;          // +0xb8

	private const int PaceLocal = 16;           // +0xc4, the draw's flags (0x00542075), which every person script sets

	private const int LocalCount = 17;

	/// <summary>
	/// The value every person script writes into <see cref="PaceLocal"/>. Its <c>0x200</c> bit is one the draw tests
	/// (<c>0x005422fb</c>); what that does on screen is not decoded, and the drawing here reads none of the word.
	/// </summary>
	private const int PaceValue = 0x1200;

	/// <summary>
	/// How long an instance waits between turns when nothing has said otherwise - <c>0x3e</c>, written by the
	/// constructor at <c>004759c4</c>.
	///
	/// <para>
	/// <b>It is exactly one turn of the sprite system</b>, which is the whole trick: the system runs every 62ms
	/// and the test for being due is a strict <c>&gt;</c>, so a sprite left on this default comes due on every
	/// <i>other</i> turn. Walking drives the interval down to one or two, and then it comes due on every turn.
	/// The animation rate is therefore a doubling, not a continuous function of speed.
	/// </para>
	/// </summary>
	public const int DefaultInterval = 0x3e;

	/// <summary>The most an interval may be set to - <c>FUN_004d41d0</c> clamps at <c>0xfa</c>.</summary>
	public const int LongestInterval = 0xfa;

	/// <summary>
	/// How many opcodes one turn may run before it is cut off - <c>FUN_00475290</c>.
	///
	/// <para>
	/// It never binds for a person: the most any person script runs before yielding is three instructions,
	/// and that only on the turn it starts. It is here because leaving it out would make a runaway script
	/// spin for ever rather than stop, which is the behaviour being reproduced.
	/// </para>
	/// </summary>
	public const int Budget = 0x32;

	/// <summary>One decoded instruction, with the operands it carries.</summary>
	private readonly record struct ScriptStep( SpriteOp Op, int A, int B, int C = 0 );

	/// <summary>How many words an instruction occupies: the opcode plus its operands.</summary>
	private static int LengthOf( SpriteOp op ) => op switch
	{
		SpriteOp.SetLocal or SpriteOp.SubLocal => 3,
		SpriteOp.LoopWhile => 4,
		SpriteOp.LoopStart or SpriteOp.End => 1,
		_ => 2
	};

	/// <summary><see cref="SpriteOp.LoopWhile"/>'s comparison 8, <c>word &gt;= value</c>.</summary>
	private const int AtLeast = 8;

	/// <summary>
	/// The let-go balloon's script, <c>0x0074f4c0</c> (word 1666), and the loop it jumps into at <c>0x0074f490</c>
	/// (word 1654), read out of <c>DAT_0074dab8</c> word for word: the alpha set to 250, then frame 1 shown and the
	/// alpha taken down by 20 while it is still nought or more, then the end. So the burst is shown thirteen turns,
	/// at 250 down to 10. Word 1665, between the end and the script, is nought.
	/// </summary>
	private static readonly (int At, ScriptStep Step)[] LetGoBalloon =
	[
		(1654, new( SpriteOp.LoopStart, 0, 0 )),
		(1655, new( SpriteOp.Frame, 1, 0 )),
		(1657, new( SpriteOp.SubLocal, AlphaLocal, 20 )),
		(1660, new( SpriteOp.LoopWhile, AlphaLocal, AtLeast, 0 )),
		(1664, new( SpriteOp.End, 0, 0 )),
		(1666, new( SpriteOp.SetLocal, AlphaLocal, 250 )),
		(1669, new( SpriteOp.Jump, 1654, 0 ))
	];

	/// <summary>Where the let-go balloon's script starts - <c>0x0074f4c0</c>, which <c>FUN_004fe950</c> hands the sprite.</summary>
	public const int LetGoBalloonEntry = 1666;

	/// <summary>
	/// Every person instruction, at the address the original keeps it at. A flat map rather than a script
	/// holding its own list, because a jump can land inside a different script and the original's program
	/// counter is simply an index into one array.
	/// </summary>
	private static readonly Dictionary<int, ScriptStep> Program = BuildProgram();

	private static Dictionary<int, ScriptStep> BuildProgram()
	{
		var program = new Dictionary<int, ScriptStep>();

		foreach ( var animation in Animations )
		{
			var at = animation.Entry;

			void Write( ScriptStep step )
			{
				program[at] = step;
				at += LengthOf( step.Op );
			}

			if ( animation.Locals )
				Write( new ScriptStep( SpriteOp.SetLocal, PaceLocal, PaceValue ) );

			if ( animation.Set is { } set )
				Write( new ScriptStep( SpriteOp.SetSet, set, 0 ) );

			foreach ( var frame in animation.Frames )
				Write( new ScriptStep( SpriteOp.Frame, frame, 0 ) );

			Write( new ScriptStep( SpriteOp.Jump, animation.LoopTo, 0 ) );
		}

		foreach ( var (at, step) in LetGoBalloon )
			program[at] = step;

		return program;
	}

	/// <summary>Where a given animation number starts, or <see cref="None"/> if it names no script.</summary>
	public static int EntryFor( int animation )
		=> animation >= 0 && animation < Table.Length ? Table[animation] : None;

	/// <summary>How many animation numbers the original's table has.</summary>
	public static int TableSize => Table.Length;

	/// <summary>
	/// Whether an instruction begins at this word, so a position read out of a save can be checked rather
	/// than trusted.
	///
	/// <para>
	/// Only the twenty-one scripts people use and the let-go balloon's are copied out of the executable, so this is
	/// false for a position inside any of the other sixty-one - which is the honest answer, not a denial that they
	/// exist.
	/// </para>
	/// </summary>
	public static bool HasInstructionAt( int pc ) => Program.ContainsKey( pc );

	private readonly int[] _locals = new int[LocalCount];

	/// <summary>The loop starts pushed and not yet popped - the instance's stack at <c>+0x20</c>, twenty deep.</summary>
	private readonly Stack<int> _loops = new();

	/// <summary>
	/// Which script this sprite is running - the instance's <c>+0x0c</c>, and the thing the original compares
	/// when it asks "is this person already walking?" at <c>FUN_00475c50</c>.
	///
	/// <para>
	/// <b>A jump does not change it.</b> <c>LAB_00476020</c> writes only the program counter, so animation 22
	/// runs off the end of its own frames and straight through animation 23's body while still answering that
	/// it is script 1310. That is the original's behaviour and not a slip.
	/// </para>
	/// </summary>
	public int Script { get; private set; } = None;

	/// <summary>Where in the array the next instruction is - the instance's <c>+0x08</c>.</summary>
	public int Pc { get; private set; } = None;

	/// <summary>How long between turns - the instance's <c>+0x80</c>. See <see cref="DefaultInterval"/>.</summary>
	public int Interval { get; set; } = DefaultInterval;

	/// <summary>When this sprite next comes due, in milliseconds - the instance's <c>+0x7c</c>.</summary>
	public int Due { get; private set; }

	/// <summary>
	/// Whether the sprite is showing a frame - the instance's <c>+0x114</c>, set by a shown frame
	/// (<c>FUN_00540b90</c>) and cleared by a frame of -1, by the end word and by running off the copied program.
	/// The draw reads it (<c>FUN_00542010</c>): a balloon is drawn only once its script has shown a frame
	/// (<see cref="ParkGuestSprites"/>); people are drawn from the save's picture whatever it says.
	/// </summary>
	public bool Shown { get; private set; }

	/// <summary>
	/// The packed set and bank offset the drawing reads - the instance's <c>+0xb4</c>, which
	/// <see cref="ParkWorld.Sprite.SpriteNumber"/> is the saved copy of.
	/// </summary>
	public int SpriteNumber => _locals[SpriteNumberLocal];

	/// <summary>Which set of the bank is being drawn, taken apart exactly as the save's copy is.</summary>
	public int Set => SpriteNumber & 0xf;

	/// <summary>How far past its kind's first bank this sprite's bank sits.</summary>
	public int BankOffset => SpriteNumber >> 4;

	/// <summary>Which picture of that set is being drawn - the instance's <c>+0xb8</c>.</summary>
	public int Frame => _locals[FrameLocal];

	/// <summary>How opaque it is drawn, nought to 255 - the instance's <c>+0xa0</c>, which the draw reads as a byte.</summary>
	public int Alpha => _locals[AlphaLocal] & 0xff;

	/// <summary>
	/// Whether the script has reached its end word: hidden and stopped, the instance's state 4. Nothing a person
	/// runs ends.
	/// </summary>
	public bool Ended { get; private set; }

	/// <summary>
	/// Whether an ended script has had the due turn that frees it (<c>FUN_00475360</c>): its owner lets it go.
	/// </summary>
	public bool Freed { get; private set; }

	/// <summary>
	/// A sprite picked up exactly where the save left it.
	///
	/// <para>
	/// The file records the script and the position within it, so a guest saved mid-stride carries on from the
	/// picture they were on rather than snapping back to the start of the cycle. That is why sixteen of the
	/// shipped park's eighteen people are on different frames of the same walk.
	/// </para>
	/// </summary>
	/// <param name="alpha">
	/// The instance's alpha, <c>+0xa0</c>: 255 as the constructor <c>FUN_004758f0</c> writes it, or what the save kept.
	/// </param>
	public SpriteScript( int script, int pc, int spriteNumber, int frame, int alpha = 0xff )
	{
		Script = script;
		Pc = pc;
		_locals[SpriteNumberLocal] = spriteNumber;
		_locals[FrameLocal] = frame;
		_locals[AlphaLocal] = alpha;
	}

	/// <summary>
	/// Puts this sprite on an animation, from the start - <c>FUN_00475b80</c>, which writes both the script
	/// and the program counter so a switch restarts the whole thing.
	/// </summary>
	/// <returns>Whether that animation names a script at all.</returns>
	public bool Start( int animation )
	{
		var entry = EntryFor( animation );

		if ( entry == None )
			return false;

		StartAt( entry );

		return true;
	}

	/// <summary>
	/// Puts this sprite on the script at a word of the array, from the start - <c>FUN_00475b80</c> handed an
	/// address rather than an animation number. It empties the loop stack and keeps the picture, the alpha and
	/// when the sprite next comes due (<c>0x00475c23</c>..<c>0x00475c3e</c>).
	/// </summary>
	public void StartAt( int entry )
	{
		Script = entry;
		Pc = entry;
		Ended = false;
		_loops.Clear();
	}

	/// <summary>Whether this sprite is already running the script a given animation names.</summary>
	public bool IsOn( int animation ) => Script != None && Script == EntryFor( animation );

	/// <summary>Sets when this sprite next comes due, which the original's constructor does as it is made.</summary>
	public void ScheduleFrom( int now ) => Due = now + Interval;

	/// <summary>
	/// One turn of the sprite system for this instance - <c>FUN_00475360</c>'s body, which runs the script
	/// only when it has come due and then sets the next deadline from <i>now</i> rather than from the
	/// deadline it just met.
	///
	/// <para>
	/// <b>The test is a strict comparison and that is load-bearing.</b> With the default interval of 62 and
	/// turns 62ms apart, <c>now</c> equals the deadline rather than passing it, so the sprite sits the turn
	/// out - which is exactly how a standing sprite ends up at half the rate of a walking one without anything
	/// having to say so.
	/// </para>
	/// </summary>
	/// <returns>Whether the script actually ran.</returns>
	public bool Step( int now )
	{
		if ( now <= Due )
			return false;

		if ( Ended )
		{
			Freed = true;
			return false;
		}

		Run();

		Due = now + Interval;

		return true;
	}

	/// <summary>
	/// Runs opcodes until one yields or the budget is spent - <c>FUN_00475010</c>. Only
	/// <see cref="SpriteOp.Frame"/> yields, and the end word stops, so a turn is "everything up to and including
	/// the next picture".
	/// </summary>
	private void Run()
	{
		for ( var run = 0; run < Budget; ++run )
		{
			if ( !Program.TryGetValue( Pc, out var step ) )
			{
				// Off the end of what was copied out. Every person script loops for ever, so this cannot
				// happen for one of them; a sprite pointed at a script that was not copied stops here rather
				// than reading whatever happens to be next.
				Script = None;
				Pc = None;
				Shown = false;
				return;
			}

			Pc += LengthOf( step.Op );

			if ( Execute( step ) )
				return;
		}
	}

	/// <summary>One instruction. Returns whether it ended the turn.</summary>
	private bool Execute( ScriptStep step )
	{
		switch ( step.Op )
		{
			case SpriteOp.SetLocal:
				_locals[step.A] = step.B;
				return false;

			case SpriteOp.SetSet:
				_locals[SpriteNumberLocal] = step.A;
				return false;

			case SpriteOp.Frame:
				_locals[FrameLocal] = step.A;

				// A frame of -1 hides the sprite (0x0047698c) and yields all the same. No copied script uses one.
				Shown = step.A != -1;
				return true;

			case SpriteOp.Jump:
				Pc = step.A;
				return false;

			case SpriteOp.SubLocal:
				_locals[step.A] -= step.B;
				return false;

			case SpriteOp.LoopStart:
				_loops.Push( Pc );
				return false;

			case SpriteOp.LoopWhile:
				if ( _loops.Count == 0 )
					return false;

				var start = _loops.Pop();

				if ( step.B == AtLeast && _locals[step.A] >= step.C )
				{
					_loops.Push( start );
					Pc = start;
				}

				return false;

			case SpriteOp.End:
				Ended = true;
				Shown = false;
				return true;

			default:
				return true;
		}
	}

	/// <summary>
	/// How long to wait between pictures for a person who has just moved - <c>FUN_004d41d0</c> fed by the
	/// tail of <c>FUN_004fa2a0</c>.
	///
	/// <para>
	/// <b>It is a doubling, not a rate, and the arithmetic is why.</b> The original squares the two components
	/// of the step, takes the square root, multiplies by <c>3.829657e-05</c> and truncates. A person walks at
	/// most about 15,728 of the simulation's units in a turn, so the product is about 0.6 - which truncates to
	/// nothing. Doubled, for somebody who is <i>not</i> hurrying, it truncates to one. So the whole chain has
	/// three outcomes: leave the interval alone, set it to one, or set it to two, and every one of those is
	/// shorter than a turn of the sprite system. What actually changes is whether the sprite comes due every
	/// turn or every other one.
	/// </para>
	/// <para>
	/// <b>Zero means "leave it alone", not "as fast as possible".</b> <c>FUN_004d4190</c> applies the interval
	/// only when it is not zero, so a hurrying person whose step truncates away keeps whatever interval they
	/// had. That reads like a bug and is what the original does.
	/// </para>
	/// </summary>
	/// <param name="acrossStep">How far the person moved across, in the simulation's 16.16 units.</param>
	/// <param name="downStep">How far they moved down, in the same units.</param>
	/// <param name="hurrying">Whether they are in a hurry, which is the half that does <b>not</b> get doubled.</param>
	public static int IntervalFor( int acrossStep, int downStep, bool hurrying )
	{
		// The original multiplies as 32-bit integers and lets them wrap, then loads the result as a signed
		// integer - so this is deliberately not widened to long.
		var square = (acrossStep * acrossStep) + (downStep * downStep);

		var value = MathF.Sqrt( square ) * DistanceToInterval;

		if ( !hurrying )
			value += value;

		// __ftol truncates towards zero, and a negative result is floored at nothing rather than clamped.
		var interval = (int)value;

		if ( interval < 0 )
			return 0;

		return interval > LongestInterval ? LongestInterval : interval;
	}

	/// <summary>
	/// The factor at <c>_DAT_007006e8</c>, verbatim. One over about 26,112 - small enough that it truncates
	/// almost everything away, which is the point made in <see cref="IntervalFor"/>.
	/// </summary>
	private const float DistanceToInterval = 3.829657e-05f;
}
