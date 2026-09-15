namespace OpenTPW;

/// <summary>
/// The ride script instruction set: all 106 of it, in the order the original's own table gives.
///
/// <para>
/// This is not a guess or a list from a website. <c>FUN_00551cb0</c> reads an instruction word,
/// requires its top byte to be <c>0x80</c>, and dispatches only while
/// <c>(word ^ 0x80000000) &lt; 0x6a</c> - 0x6a being 106 - through the jump table at 0x5567d8.
/// Anything above that prints "RSSE: Unknown instruction". The names come from the table at
/// 0x765280, which is <c>{name*, operandCount*}</c> eight bytes a record, and reading past record
/// 105 gives pointers like 0x02020202 rather than another opcode, which is what confirms where it
/// ends.
/// </para>
///
/// <para>
/// <b>The count in that table is an ASCII digit string, not an integer</b> - "2" rather than 2 -
/// which is why it reads as a second pointer at a glance. <see cref="Opcodes.OperandCount"/> holds
/// what it decodes to.
/// </para>
///
/// <para>
/// This lives beside <see cref="RideScriptFile"/> rather than with the VM because the operand counts
/// are part of the file format: without them a script body is an undifferentiated run of dwords and
/// cannot be split into instructions at all.
/// </para>
/// </summary>
public enum Opcode
{
	NOP = 0,
	CRIT_LOCK = 1,
	CRIT_UNLOCK = 2,
	COPY = 3,
	SETLV = 4,
	SUB = 5,
	ENDSLICE = 6,
	GETTIME = 7,
	ADDOBJ = 8,
	ADDOBJ_EXT = 9,
	KILLOBJ = 10,
	FADEOBJ = 11,
	SETOBJPARAM = 12,
	EVENT = 13,
	EVENT_EXT = 14,
	FLUSHANIM = 15,
	TRIGANIM = 16,
	WAITANIM = 17,
	LOOPANIM = 18,
	TRIGWAITANIM = 19,
	GETANIM = 20,
	TRIGANIMSPEED = 21,
	FLUSHANIM_CH = 22,
	TRIGANIM_CH = 23,
	WAITANIM_CH = 24,
	LOOPANIM_CH = 25,
	TRIGWAITANIM_CH = 26,
	GETANIM_CH = 27,
	RAND = 28,
	JSR = 29,
	RETURN = 30,
	BRANCH = 31,
	BRANCH_Z = 32,
	BRANCH_NZ = 33,
	BRANCH_NV = 34,
	BRANCH_PV = 35,
	DBGMSG = 36,
	NAME = 37,
	TEST = 38,
	CMP = 39,
	PUSH = 40,
	POP = 41,
	HUSH = 42,
	HOP = 43,
	WAIT = 44,
	WAITABS = 45,
	WAIT4ANIM = 46,
	ADD = 47,
	MULT = 48,
	DIV = 49,
	MOD = 50,
	TURBO = 51,
	END = 52,
	TOUR = 53,
	BUMP = 54,
	COAST = 55,
	ADDHEAD = 56,
	DELHEAD = 57,
	LIMBO = 58,
	UNLIMBO = 59,
	FORCEUNLIMBO = 60,
	INLIMBO = 61,
	LIMBOSPACE = 62,
	SPAWNCHILD = 63,
	SPAWNSOUND = 64,
	REMOVECHILD = 65,
	SETVARINCHILD = 66,
	GETVARINCHILD = 67,
	SETVARINPARENT = 68,
	GETVARINPARENT = 69,
	BOUNCESETNODE = 70,
	BOUNCESETBASE = 71,
	BOUNCE = 72,
	UNBOUNCE = 73,
	FORCEUNBOUNCE = 74,
	BOUNCING = 75,
	WALKON = 76,
	WALKOFF = 77,
	WALKGET = 78,
	WALKST_FLOAT = 79,
	WALKFLOATSTAT = 80,
	WALKFLOATSTOP = 81,
	ENABLELIGHT = 82,
	DISABLELIGHT = 83,
	SETLIGHT = 84,
	COLOURLIGHT = 85,
	STARTSCREAM = 86,
	STOPSCREAM = 87,
	SINGLESCREAM = 88,
	SCREAMLEVEL = 89,
	FINDSCRIPTRAND = 90,
	GETREMOTEVAR = 91,
	SETREMOTEVAR = 92,
	REPAIREFFECT = 93,
	GETCUSTPTCLCODE = 94,
	SETTIMER = 95,
	GETTIMER = 96,
	YEAR = 97,
	MONTH = 98,
	DAY = 99,
	HOUR = 100,
	MIN = 101,
	SEC = 102,
	SETREVERB = 103,
	DIPMUSIC = 104,
	SPARK = 105
}

/// <summary>
/// How many operand words each opcode takes, decoded from the original's own table at 0x765280.
/// </summary>
public static class Opcodes
{
	/// <summary>The number the dispatcher's bound works out to: <c>(word ^ 0x80000000) &lt; 0x6a</c>.</summary>
	public const int Count = 106;

	/// <summary>
	/// Indexed by <see cref="Opcode"/>. Reading these off the binary settled several the published
	/// notes still call unknown - SETLV takes one operand, and WALKON takes seven, the most of any.
	/// </summary>
	private static readonly int[] Counts =
	[
		0, 0, 0, 2, 1, 3, 0, 1, 4, 5,           // NOP .. ADDOBJ_EXT
		1, 1, 3, 3, 4, 0, 3, 2, 2, 3,           // KILLOBJ .. TRIGWAITANIM
		1, 4, 1, 4, 3, 3, 4, 2, 2, 1,           // GETANIM .. JSR
		0, 1, 1, 1, 1, 1, 1, 1, 1, 2,           // RETURN .. CMP
		1, 1, 1, 1, 1, 1, 0, 2, 3, 3,           // PUSH .. DIV
		3, 1, 0, 2, 2, 2, 1, 1, 2, 1,           // MOD .. UNLIMBO
		1, 1, 1, 1, 1, 0, 2, 2, 2, 2,           // FORCEUNLIMBO .. GETVARINPARENT
		1, 1, 2, 1, 1, 1, 7, 1, 1, 3,           // BOUNCESETNODE .. WALKST_FLOAT
		1, 0, 1, 1, 2, 4, 2, 0, 2, 1,           // WALKFLOATSTAT .. SCREAMLEVEL
		2, 3, 3, 1, 2, 1, 1, 1, 1, 1,           // FINDSCRIPTRAND .. DAY
		1, 1, 1, 1, 1, 4                        // HOUR .. SPARK
	];

	/// <summary>
	/// How many words follow this opcode, or -1 for a value the original would call unknown.
	/// </summary>
	public static int OperandCount( this Opcode opcode )
	{
		var index = (int)opcode;

		return index >= 0 && index < Counts.Length ? Counts[index] : -1;
	}

	/// <summary>Whether the dispatcher would accept this opcode rather than refusing it.</summary>
	public static bool IsKnown( this Opcode opcode ) => (int)opcode >= 0 && (int)opcode < Count;
}
