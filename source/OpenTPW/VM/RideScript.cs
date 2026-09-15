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
/// The ones left out all reach into a world that does not exist yet - objects, animation, guests,
/// sound - so they can be filled in beside whatever provides those.
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

	public RideScript( RideScriptFile file )
	{
		_file = file;

		_variables = new int[Math.Max( file.VariableCount, file.VariableNames.Count )];
		_stack = new int[Math.Max( file.StackSize, 0 )];
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

	public int this[RideVariables variable] => Read( (int)variable );

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
				Name = _file.StringAt( operands[0].Value ) ?? Name;
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
			// nought - which is every script here. TRIGWAITANIM is deliberately NOT among them: it
			// rewinds itself and walks a channel cursor at +0xbc across turns, which has not been read.
			// It was left out when the rest of the family landed because it then completed no further
			// script at all; the effect opcodes below have since changed that, and it now completes 11
			// and leads every remaining candidate. It is the next thing to weigh, not a settled no.
			case Opcode.FLUSHANIM:
				// The handler's first act is to fetch the model and leave if there is none. With no
				// model this is the engine's behaviour rather than a stand-in for it.
				break;

			case Opcode.TRIGANIM:
				TriggerAnimation( now, operands[2] );
				break;

			case Opcode.WAITANIM:
				WaitOutAnimation( now, 1 + operands.Count );
				break;

			case Opcode.LOOPANIM:
				// The key is built from the two operands exactly as the engine builds it, by addition
				// rather than by an or - which differs only if a variable holds more than sixteen bits.
				Loop( (Value( operands[1] ) << 16) + Value( operands[0] ) );
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
			// handler throws the handle away and so nothing it starts is ever killable. FADEOBJ and
			// SETOBJPARAM are NOT among them: 133 instructions that complete no further script, and a fade
			// differs from a kill only in stopping a sound gently, which nothing here can yet hear.
			case Opcode.ADDOBJ:
				AddObject( operands );
				break;

			case Opcode.EVENT:
				TriggerEvent( operands );
				break;

			case Opcode.KILLOBJ:
				KillObjects( operands[0] );
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
		_random = (_random * 0x19660Du) + 0x3C6EF35Fu;
		_random = (_random >> 13) | (_random << 19);

		var drawn = Math.Abs( (int)_random ) >> 1;
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
	private void TriggerAnimation( float now, RideOperand destination )
	{
		Store( destination, AnimationSlack );

		_animationUntil = now + AnimationSlack;
		_looping = OneShot;
	}

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
	private void WaitOutAnimation( float now, int length ) => Wait( now, -AnimationSlack, length );

	/// <summary>
	/// <c>LOOPANIM</c>: set an animation looping, unless that same one already is.
	///
	/// <para>
	/// Asking again for the animation already running is the engine's early exit and does nothing at
	/// all - which matters because the other path <b>clears the <c>WAIT4ANIM</c> deadline</b>: a loop
	/// never finishes, so there is nothing left to wait for. That early exit has no effect anything
	/// here can see, since with no model the two paths differ only in the deadline and a trigger always
	/// leaves <see cref="_looping"/> at <see cref="OneShot"/>; it is here because the engine does it,
	/// and it stops an animation being restarted every turn the moment a model exists.
	/// </para>
	/// </summary>
	private void Loop( int animation )
	{
		if ( _looping == animation )
			return;

		_animationUntil = null;
		_looping = animation;
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
