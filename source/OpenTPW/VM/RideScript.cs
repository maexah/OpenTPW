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
	private float _waitUntil;

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

	/// <summary>True while the script is sitting on a <c>WAIT</c> that has not come due.</summary>
	public bool Waiting => _waitUntil > 0f;

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

			case Opcode.WAIT4ANIM:
				// Nothing animates here yet, so there is never anything to wait for. When animation
				// arrives this becomes the same shape as WAIT on its own deadline.
				++NotImplemented;
				break;

			default:
				// Reaches into a world that does not exist yet. Counted, never guessed.
				++NotImplemented;
				break;
		}
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
	/// The duration is added to the caller's clock unchanged. The original divides it by a factor it
	/// works out from the script's own speed word before adding it to a clock of its own, and the unit
	/// of that clock has not been established - so inventing a conversion here would be a guess
	/// dressed as a measurement. A caller that keeps its clock in the script's units gets the right
	/// behaviour; one that does not gets waits of the wrong length, which is visible rather than
	/// silent.
	/// </para>
	/// </summary>
	private void Wait( float now, int duration, int length )
	{
		if ( _waitUntil <= 0f )
			_waitUntil = now + duration;

		if ( now >= _waitUntil )
		{
			_waitUntil = 0f;
			return;
		}

		Position -= length;
		_budget = 0;
	}
}
