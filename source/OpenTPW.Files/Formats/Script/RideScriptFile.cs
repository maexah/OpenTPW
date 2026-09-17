using System.Text;

namespace OpenTPW;

/// <summary>
/// What an operand word carries, taken from its top byte. The original stores the kind in the word
/// itself rather than in the instruction, so the same opcode can take a variable in one script and a
/// literal in the next - <c>COPY</c>'s second operand does exactly that.
/// </summary>
public enum RideOperandKind
{
	/// <summary>A number written into the script.</summary>
	Literal = 0x00,

	/// <summary>An offset into the script's string blob - see <see cref="RideScriptFile.StringAt"/>.</summary>
	String = 0x10,

	/// <summary>A branch target, as a word index into the script body.</summary>
	Location = 0x20,

	/// <summary>An index into the script's own variables, the ones <see cref="RideScriptFile.VariableNames"/> names.</summary>
	Variable = 0x40
}

/// <param name="Value">
/// The word with its kind byte stripped off. For a <see cref="RideOperandKind.Location"/> this is a
/// word index into the body and can be compared against <see cref="RideInstruction.Address"/>
/// directly; for a <see cref="RideOperandKind.Variable"/> it indexes
/// <see cref="RideScriptFile.VariableNames"/>; for a <see cref="RideOperandKind.String"/> it is a byte
/// offset into the blob.
/// </param>
public readonly record struct RideOperand( RideOperandKind Kind, int Value );

/// <param name="Address">
/// Where this instruction starts, counted in words from the beginning of the body - which is what a
/// branch target names, and what the original's program counter holds.
/// </param>
public sealed record RideInstruction( int Address, Opcode Opcode, IReadOnlyList<RideOperand> Operands );

/// <summary>
/// A .RSE ride script: the compiled program that drives one ride, shop, sideshow or feature.
///
/// <para>
/// None of the 308 the game ships are loose on disk - every one lives inside
/// <c>levels/&lt;theme&gt;/{rides,shops,sideshow,features,upgrades}/*.wad</c>, and the case of the
/// extension varies between them (<c>Coaster1.RSE</c>, <c>Monkey.rse</c>), so any lookup has to be
/// case-insensitive.
/// </para>
///
/// <para>
/// The layout below is read out of the loader, <c>FUN_005587f0</c>:
/// </para>
///
/// <code>
///   0x00  char[4]   "RSSE"
///   0x04  int32     version; the engine warns on a mismatch and carries on
///   0x08  int32     how many variables the script declares
///   0x0c  int32     a count the loader allocates for and never fills
///   0x10  int32     50 in every shipped file
///   0x14  int32     a count of 8-byte records, likewise allocated and never filled
///   0x18  int32     a count of 16-byte records
///   0x1c  int32     a count of 32-byte records
///   0x20  byte[16]  four dwords the loader reads and discards - always "Pad Pad Pad Pad "
///   0x30  int32     the length of the script body, in words
///   0x34  int32[]   the body
///         int32     the length of the string blob, in bytes
///         byte[]    the blob: NUL-terminated strings, referred to by byte offset
///         ...       one length-prefixed name per declared variable
/// </code>
///
/// <para>
/// <b>The variable names at the end look like trailing junk and are not.</b> The loader allocates
/// room for the variables' values and never reads their names, because the running game has no use
/// for them - so a reading that stops at the string blob leaves a tail it cannot explain, which is
/// how this was first mis-parsed: 98 of the 308 files appeared to be well-formed and 210 appeared
/// to be broken, and the 98 were exactly those declaring no variables. Parsing the names accounts
/// for the last byte of all 308.
/// </para>
///
/// <para>
/// <b>The body is a flat array of words, not a byte stream.</b> The program counter indexes it
/// directly (<c>FUN_00551cb0</c>, field +0x3c against the length at +0x50), so a branch target is a
/// word index and needs no conversion. Every word carries its kind in its top byte: 0x80 an opcode,
/// 0x40 a variable, 0x20 a branch target, 0x10 a string offset and 0x00 a literal. An instruction is
/// followed by exactly <see cref="Opcodes.OperandCount"/> operand words, which is the only thing that
/// makes the body divisible into instructions at all.
/// </para>
///
/// <para>
/// One detail worth knowing before writing an interpreter: <c>COPY</c>'s literal path sign-extends
/// the low 16 bits (<c>MOVSX EAX,AX</c> at 0x00551df5) rather than taking the whole field. No shipped
/// script has a literal that reaches beyond 16 bits, so the two readings agree on all of them, but
/// they would not agree in general. <see cref="RideOperand.Value"/> keeps the field as written and
/// leaves that choice to whatever executes it.
/// </para>
///
/// <para>
/// This reads a script; it does not run one. <b>Something else does</b> - <c>RideScript</c>, in the game
/// assembly, which carries 57 of the 106 instructions the opcode table declares and counts what it does
/// not carry rather than guessing at it. Every ride script the game ships both reads here and runs there.
/// </para>
///
/// <para>
/// <b>Not to be confused with <c>Ride</c> and <c>RideVM</c>, which are a different pair and are dead.</b>
/// <c>RideVM</c> is constructed in exactly one place - <c>Ride</c>'s own constructor - and <c>Ride</c> is
/// constructed nowhere at all, so neither runs. This paragraph used to say "there is no ride runtime in
/// the tree" and cite those two, which was right about them and wrong about the tree.
/// </para>
/// </summary>
public sealed class RideScriptFile : BaseFormat
{
	/// <summary>
	/// "RSSE", little-endian. The loader refuses anything else outright with "Not a RSSE script file".
	/// </summary>
	private const int Magic = 0x45535352;

	/// <summary>The version every shipped script carries, compared by the loader against DAT_00879190.</summary>
	public const int ExpectedVersion = 0x00010F51;

	private const int VersionOffset = 0x04;
	private const int VariableCountOffset = 0x08;
	private const int StackSizeOffset = 0x0C;
	private const int TimeSliceOffset = 0x10;
	private const int LimboRecordsOffset = 0x14;
	private const int PadOffset = 0x20;
	private const int PadLength = 16;
	private const int LengthOffset = 0x30;
	private const int BodyOffset = 0x34;

	private const uint KindMask = 0xFF000000;
	private const uint ValueMask = 0x00FFFFFF;
	private const uint OpcodeKind = 0x80000000;

	/// <summary>False when the file was missing, or did not read as a script - never throws.</summary>
	public bool IsValid { get; private set; }

	/// <summary>What the file states, which may not be <see cref="ExpectedVersion"/>.</summary>
	public int Version { get; private set; }

	/// <summary>
	/// How many variables the script declares, which is also how many names follow the string blob.
	/// Taken from the header rather than from <see cref="VariableNames"/>, because a truncated tail
	/// leaves fewer names than the script actually uses.
	/// </summary>
	public int VariableCount { get; private set; }

	/// <summary>
	/// How much stack the script asked for with <c>#setstack</c>. Two stacks share it: subroutine
	/// returns fill it from the top down, and <c>HUSH</c>/<c>HOP</c> from the bottom up.
	/// </summary>
	public int StackSize { get; private set; }

	/// <summary>
	/// How many instructions the script may run before it is made to give way, which is 50 in every
	/// shipped file. It is a count of instructions and not a length of time.
	/// </summary>
	public int TimeSlice { get; private set; }

	/// <summary>
	/// How many people the script can hold in limbo at once - the header's "limbo records", which the
	/// loader turns into an array of that many eight-byte slots (<c>FUN_005587f0</c> reads it into the
	/// frame's <c>+0x58</c> and allocates <c>count * 8</c> bytes for <c>+0x24</c>).
	///
	/// <para>
	/// Exactly 24 of the 308 shipped scripts declare any, every one of them 10, and those 24 are exactly
	/// the scripts that use a limbo instruction - no script declares slots it never uses, and none uses
	/// limbo without declaring them. They are shops and toilets rather than rides.
	/// </para>
	/// </summary>
	public int LimboCapacity { get; private set; }

	/// <summary>The body, split into instructions. Empty unless <see cref="IsValid"/>.</summary>
	public IReadOnlyList<RideInstruction> Instructions { get; private set; } = [];

	/// <summary>
	/// The script's own variables, in the order it declares them, which is the order a
	/// <see cref="RideOperandKind.Variable"/> operand indexes.
	/// </summary>
	public IReadOnlyList<string> VariableNames { get; private set; } = [];

	/// <summary>Every string in the blob, by the byte offset that refers to it.</summary>
	public IReadOnlyDictionary<int, string> Strings { get; private set; }
		= new Dictionary<int, string>();

	public RideScriptFile( string path )
	{
		ReadFromFile( path );
	}

	public RideScriptFile( Stream stream )
	{
		ReadFromStream( stream );
	}

	/// <summary>
	/// The string a <see cref="RideOperandKind.String"/> operand names, or null where it points
	/// somewhere the blob holds no string - which no shipped script does.
	/// </summary>
	public string? StringAt( int offset ) => Strings.TryGetValue( offset, out var text ) ? text : null;

	protected override void ReadFromFile( string path )
	{
		// OpenRead answers null for a member that is not there, and a ride without a script is a
		// normal thing to ask about - so this is left quiet, as SignFile is, for callers that try
		// more than one candidate name.
		using var stream = FileSystem.OpenRead( path );

		if ( stream == null )
			return;

		ReadFromStream( stream );
	}

	protected override void ReadFromStream( Stream stream )
	{
		using var memoryStream = new MemoryStream();
		stream.CopyTo( memoryStream );
		var data = memoryStream.ToArray();

		if ( data.Length < BodyOffset )
		{
			Log.Warning( $"Ride script is {data.Length} bytes, too short to hold a header" );
			return;
		}

		if ( BitConverter.ToInt32( data, 0 ) != Magic )
		{
			Log.Warning( "Ride script does not begin with RSSE" );
			return;
		}

		Version = BitConverter.ToInt32( data, VersionOffset );
		VariableCount = BitConverter.ToInt32( data, VariableCountOffset );
		StackSize = BitConverter.ToInt32( data, StackSizeOffset );
		TimeSlice = BitConverter.ToInt32( data, TimeSliceOffset );
		LimboCapacity = BitConverter.ToInt32( data, LimboRecordsOffset );

		// The loader says so and then reads the file anyway (0x005587f0), so refusing here would turn
		// a warning the original lives with into a ride that does not load.
		if ( Version != ExpectedVersion )
			Log.Warning( $"Ride script is version {Version:X8}, not the {ExpectedVersion:X8} the engine was built for" );

		var length = BitConverter.ToInt32( data, LengthOffset );

		if ( length <= 0 )
		{
			Log.Warning( "Ride script declares no instructions at all" );
			return;
		}

		var blobLengthAt = BodyOffset + (length * 4L);

		if ( blobLengthAt + 4 > data.Length )
		{
			Log.Warning( $"Ride script declares {length} words, which do not fit in {data.Length} bytes" );
			return;
		}

		if ( !TryReadInstructions( data, length, out var instructions ) )
			return;

		var blobLength = BitConverter.ToInt32( data, (int)blobLengthAt );
		var blobAt = (int)blobLengthAt + 4;

		if ( blobLength < 0 || blobAt + (long)blobLength > data.Length )
		{
			Log.Warning( $"Ride script's string blob claims {blobLength} bytes, which do not fit" );
			return;
		}

		Instructions = instructions;
		Strings = ReadStrings( data, blobAt, blobLength );
		VariableNames = ReadVariableNames( data, blobAt + blobLength );
		IsValid = true;
	}

	/// <summary>
	/// Splits the body into instructions, using each opcode's operand count to find the next one.
	///
	/// <para>
	/// This is stricter than the original, which discovers a bad word only when the program counter
	/// reaches it and then prints "Unknown instruction" and stops that script. Refusing the file up
	/// front is the more useful contract for a reader: half a decoded script looks like a whole one
	/// and would be acted on as if it were.
	/// </para>
	/// </summary>
	private static bool TryReadInstructions( byte[] data, int length, out RideInstruction[] instructions )
	{
		instructions = [];

		var body = new uint[length];

		for ( int i = 0; i < length; ++i )
			body[i] = BitConverter.ToUInt32( data, BodyOffset + (i * 4) );

		var read = new List<RideInstruction>();

		for ( int at = 0; at < length; )
		{
			var word = body[at];

			if ( (word & KindMask) != OpcodeKind )
			{
				Log.Warning( $"Ride script word {at} is {word:X8}, where an instruction was due" );
				return false;
			}

			var opcode = (Opcode)(word ^ OpcodeKind);
			var operandCount = opcode.OperandCount();

			if ( operandCount < 0 )
			{
				Log.Warning( $"Ride script word {at} is opcode {(int)opcode}, past the {Opcodes.Count} the engine knows" );
				return false;
			}

			if ( at + 1 + operandCount > length )
			{
				Log.Warning( $"Ride script's {opcode} at {at} wants {operandCount} operands with {length - at - 1} words left" );
				return false;
			}

			var operands = new RideOperand[operandCount];

			for ( int i = 0; i < operandCount; ++i )
			{
				var operand = body[at + 1 + i];

				operands[i] = new RideOperand( (RideOperandKind)((operand & KindMask) >> 24),
					(int)(operand & ValueMask) );
			}

			read.Add( new RideInstruction( at, opcode, operands ) );
			at += 1 + operandCount;
		}

		instructions = [.. read];

		return true;
	}

	/// <summary>
	/// The blob, which is NUL-terminated strings back to back. They are kept by the offset each one
	/// starts at because that is what an operand carries - not by position, which nothing refers to.
	/// </summary>
	private static Dictionary<int, string> ReadStrings( byte[] data, int at, int length )
	{
		var strings = new Dictionary<int, string>();
		var start = 0;

		for ( int i = 0; i < length; ++i )
		{
			if ( data[at + i] != 0 )
				continue;

			strings[start] = Encoding.ASCII.GetString( data, at + start, i - start );
			start = i + 1;
		}

		return strings;
	}

	/// <summary>
	/// The names at the end of the file, one length-prefixed run each, the length counting the NUL.
	///
	/// <para>
	/// A file that runs out here is not refused. The original never reads this far - it allocates the
	/// variables' storage from the count and leaves it at that - so a script whose tail is short or
	/// missing is one the game would still run, and saying otherwise would refuse a working ride over
	/// a part of the file nothing executes.
	/// </para>
	/// </summary>
	private string[] ReadVariableNames( byte[] data, int at )
	{
		var count = BitConverter.ToInt32( data, VariableCountOffset );

		if ( count <= 0 )
			return [];

		var names = new List<string>();

		for ( int i = 0; i < count; ++i )
		{
			if ( at + 4 > data.Length )
				break;

			var length = BitConverter.ToInt32( data, at );
			at += 4;

			if ( length <= 0 || at + length > data.Length )
				break;

			names.Add( Encoding.ASCII.GetString( data, at, length ).TrimEnd( '\0' ) );
			at += length;
		}

		if ( names.Count != count )
			Log.Warning( $"Ride script declares {count} variables but names {names.Count} of them" );

		return [.. names];
	}
}
