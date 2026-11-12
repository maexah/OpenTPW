using System.Buffers.Binary;

namespace OpenTPW;

/// <summary>
/// The sound module's voice list (<c>SOUN</c>): the sound manager's live voices as the park was saved, each under its
/// handle, with the category slots their records name. FileFormats <c>saves.md</c>, "The voice list"; the writer is
/// <c>0x006b6c30</c> and the reader <c>0x006b6da0</c> (<c>docs/exe/audio.md</c>, "A park file's voices").
///
/// <para>
/// <b>Found by anchors that have to agree</b>: the module ends at its tag (<c>NUOS</c>), two bytes before the cheats'
/// (<c>STHC</c>), and its length word <c>L</c>, which counts itself, ends the list right there.
/// </para>
/// </summary>
public sealed class ParkVoices
{
	/// <summary>The class numbers a voice's type word holds (the reader's switch, <c>0x006b6f13</c>).</summary>
	public const int MusicType = 1;

	/// <summary>The class of flag <c>0x10</c> (<c>0x0070a440</c>).</summary>
	public const int FlagTenType = 2;

	public const int OneShotType = 3;

	/// <summary>The chain of any variation (<c>0x0070a390</c>).</summary>
	public const int ChainType = 4;

	public const int PlainType = 5;

	/// <summary>The held chain (<c>0x0070a2e0</c>).</summary>
	public const int HeldChainType = 6;

	/// <summary>A child of a chain of any variation (<c>AnyChainChild_Construct</c>).</summary>
	public const int ChainChildType = 7;

	/// <summary>A child of a held chain (<c>0x006c3de0</c>).</summary>
	public const int HeldChildType = 9;

	/// <summary>The class of flag <c>0x100</c>, which the reader has no case for.</summary>
	public const int FlagHundredType = 10;

	/// <summary>The voices a pool block holds (<c>VoicePool_AddBlock</c>, <c>0x006b69b0</c>).</summary>
	public const int SlotsABlock = 100;

	/// <summary>The mark a record opens with and holds twice more (<c>Voice_ReadRecord</c> checks each).</summary>
	private const int Mark = 0x0012d591;

	private const string Tag = "NUOS";

	private const string Opening = "SVDA";

	private const string CheatsTag = "STHC";

	private const string AfterCheatsTag = "CSDA";

	/// <summary>The cheats' two bytes between the sound's tag and their own.</summary>
	private const int CheatsSize = 2;

	/// <summary>
	/// A voice's record: its place (x, height, z), its own range (nought for none), its category's slot, its effect,
	/// variation and sample as places from nought, its controllers' four keys and four values (null for a voice with
	/// none), whether it follows the listener in a rectangle (voice flag <c>0x1000</c>), and its first child.
	/// </summary>
	public sealed record Record( float X, float Height, float Z, float Range, int Category, int Effect, int Variation,
		int Sample, byte[]? Keys, byte[]? Values, bool Rectangle, Child? Child );

	/// <summary>A voice's first child: its class (<see cref="ChainChildType"/> or <see cref="HeldChildType"/>) and its record.</summary>
	public sealed record Child( int Type, Record Record );

	/// <summary>One saved voice: its handle (its pool slot in the low 16 bits, a serial in the high), its class and its record.</summary>
	public sealed record Voice( int Handle, int Type, Record Record )
	{
		/// <summary>The pool slot the reader puts it back in (<c>VoicePool_FindFreeByNumber</c>, <c>0x006b6970</c>).</summary>
		public int Slot => Handle & 0xffff;
	}

	/// <summary>Why the module was refused, or null. A refused module has no voices.</summary>
	public string? Problem { get; }

	/// <summary>The category slots' file names, in slot order; null for an empty slot.</summary>
	public IReadOnlyList<string?> Categories { get; } = [];

	/// <summary>The pool's blocks of <see cref="SlotsABlock"/> as saved.</summary>
	public int Blocks { get; }

	/// <summary>How many voices the list says it holds.</summary>
	public int Count { get; }

	/// <summary>The voices read, oldest first, as the writer walks the live list from its tail (<c>0x006b6cd6</c>).</summary>
	public IReadOnlyList<Voice> Saved { get; } = [];

	/// <summary>
	/// Why the list stopped short of its count, or null where every voice was read. The original's reader reads such a
	/// voice's handle and type and not its record, so every voice after it is read from the wrong bytes: a type it has no
	/// case for (<see cref="FlagHundredType"/>), a handle whose slot is not free (<c>0x006b6efd</c>), or a record without
	/// its mark (a voice whose effect is in no loaded category, which the writer leaves bare, <c>0x006bcd6d</c>).
	/// </summary>
	public string? Stopped { get; }

	/// <summary>The handle and type of the voice the list stopped at, where it stopped.</summary>
	public (int Handle, int Type)? StoppedAtVoice { get; }

	/// <summary>Where the list's length word stands in the body, and each voice read's bytes, its handle first; -1 and none where the module was refused.</summary>
	private readonly int _listAt = -1;

	private readonly List<(int At, int End)> _spans = [];

	/// <summary>Reads the module out of the inflated body, the same array <see cref="ParkWorld"/> is given.</summary>
	public ParkVoices( byte[] body )
	{
		ArgumentNullException.ThrowIfNull( body );

		var end = FindTag( body );

		if ( end < 0 )
		{
			Problem = $"no {Tag} tag two bytes before a {CheatsTag} tag, a dword and a {AfterCheatsTag} tag";
			return;
		}

		var list = FindList( body, end );

		if ( list < 0 )
		{
			Problem = $"no {Opening} tag whose module's length ends at the {Tag} tag, or the park was saved with sound off";
			return;
		}

		try
		{
			_listAt = list;

			var at = list + 4;
			Count = Int32( body, at );
			at += 4;

			var libraryLength = Int32( body, at );
			var slots = Int32( body, at + 4 );
			var library = at + 4;
			at += 8 + (4 * slots);

			var names = new List<string?>();

			for ( var slot = 0; slot < slots; ++slot )
			{
				var length = body[at];
				names.Add( length == 0 ? null : System.Text.Encoding.Latin1.GetString( body, at + 1, length ).TrimEnd( '\0' ) );
				at += 1 + length;
			}

			if ( at != library + 4 + libraryLength )
			{
				Problem = "the category slots do not end where their length says";
				return;
			}

			Categories = names;
			Blocks = Int32( body, at );
			at += 4;

			var voices = new List<Voice>();
			var taken = new HashSet<int>();

			for ( var index = 0; index < Count; ++index )
			{
				var opens = at;
				var handle = Int32( body, at );
				var type = Int32( body, at + 4 );
				at += 8;

				var slot = handle & 0xffff;

				if ( slot >= Blocks * SlotsABlock || !taken.Add( slot ) )
				{
					(Stopped, StoppedAtVoice) = ($"voice {index}'s handle 0x{handle:x8} names a slot that is not free", (handle, type));
					break;
				}

				if ( type is < MusicType or > HeldChainType )
				{
					(Stopped, StoppedAtVoice) = ($"voice {index} is of type {type}, which the reader has no case for", (handle, type));
					break;
				}

				if ( ReadRecord( body, ref at ) is not { } record )
				{
					(Stopped, StoppedAtVoice) = ($"voice {index}'s record has no mark", (handle, type));
					break;
				}

				voices.Add( new Voice( handle, type, record ) );
				_spans.Add( (opens, at) );
			}

			Saved = voices;

			if ( Stopped is null && at != list + Int32( body, list ) )
				Problem = "the list does not end where its length says";
		}
		catch ( Exception e ) when ( e is ArgumentOutOfRangeException or IndexOutOfRangeException )
		{
			Problem = "the list runs past the module";
			Saved = [];
		}
	}

	/// <summary>
	/// <paramref name="body"/> with the voices under <paramref name="handles"/> taken out of its list, the list's length
	/// and count made less by them; the body as it is where the list was refused or stopped short, or names none of them.
	/// </summary>
	public static byte[] Without( byte[] body, IReadOnlySet<int> handles )
	{
		ArgumentNullException.ThrowIfNull( body );
		ArgumentNullException.ThrowIfNull( handles );

		var voices = new ParkVoices( body );

		if ( voices.Problem is not null || voices.Stopped is not null )
			return body;

		var gone = Enumerable.Range( 0, voices.Saved.Count ).Where( index => handles.Contains( voices.Saved[index].Handle ) ).ToList();

		if ( gone.Count == 0 )
			return body;

		var output = new List<byte>( body.Length );
		var from = 0;

		foreach ( var index in gone )
		{
			var (at, end) = voices._spans[index];
			output.AddRange( body.AsSpan( from, at - from ).ToArray() );
			from = end;
		}

		output.AddRange( body.AsSpan( from ).ToArray() );

		var written = output.ToArray();
		var cut = gone.Sum( index => voices._spans[index].End - voices._spans[index].At );

		BinaryPrimitives.WriteInt32LittleEndian( written.AsSpan( voices._listAt, 4 ), Int32( body, voices._listAt ) - cut );
		BinaryPrimitives.WriteInt32LittleEndian( written.AsSpan( voices._listAt + 4, 4 ), voices.Count - gone.Count );

		return written;
	}

	/// <summary>The handles of the held chains (<see cref="HeldChainType"/>) the list holds that <paramref name="named"/> does not.</summary>
	public IReadOnlySet<int> HeldChainsNotIn( IReadOnlySet<int> named )
	{
		ArgumentNullException.ThrowIfNull( named );

		return Saved.Where( voice => voice.Type == HeldChainType && !named.Contains( voice.Handle ) ).Select( voice => voice.Handle ).ToHashSet();
	}

	/// <summary>The file name of the slot <paramref name="record"/> names, or null for none.</summary>
	public string? CategoryOf( Record record )
		=> record.Category > 0 && record.Category < Categories.Count ? Categories[record.Category] : null;

	/// <summary>A record and its child's (<c>Voice_ReadRecord</c>, <c>0x006bd0a0</c>), or null where a mark is missing.</summary>
	private static Record? ReadRecord( byte[] body, ref int at )
	{
		if ( Int32( body, at ) != Mark )
			return null;

		var x = Single( body, at + 0x04 );
		var height = Single( body, at + 0x08 );
		var z = Single( body, at + 0x0c );
		var range = Single( body, at + 0x14 );
		var category = Int32( body, at + 0x18 );
		var effect = Int32( body, at + 0x1c );
		var variation = Int32( body, at + 0x20 );
		var sample = Int32( body, at + 0x24 );
		at += 0x28;

		if ( Int32( body, at ) != Mark )
			return null;

		byte[]? keys = null, values = null;

		if ( Int32( body, at + 4 ) != 0 )
		{
			keys = body.AsSpan( at + 8, 4 ).ToArray();
			values = body.AsSpan( at + 12, 4 ).ToArray();
			at += 24;
		}

		at += 8;

		if ( Int32( body, at ) != Mark )
			return null;

		var rectangle = body[at + 4] != 0;
		var childType = Int32( body, at + 9 );
		at += 13;

		Child? child = null;

		if ( childType != 0 )
		{
			// The type again, then the child's own record.
			at += 4;

			if ( ReadRecord( body, ref at ) is not { } inner )
				return null;

			child = new Child( childType, inner );
		}

		return new Record( x, height, z, range, category, effect, variation, sample, keys, values, rectangle, child );
	}

	/// <summary>Where the module's tag stands: two bytes before a <c>STHC</c> tag that a dword and a <c>CSDA</c> tag follow, the last such; -1 for none.</summary>
	private static int FindTag( byte[] body )
	{
		for ( var at = body.Length - 12; at >= Tag.Length + CheatsSize; --at )
		{
			if ( Is( body, at, CheatsTag ) && Is( body, at + 8, AfterCheatsTag ) && Is( body, at - CheatsSize - Tag.Length, Tag ) )
				return at - CheatsSize - Tag.Length;
		}

		return -1;
	}

	/// <summary>
	/// Where the list's length word stands: after a <c>SVDA</c> tag, the two dwords and the block's length, the block and
	/// the three floats, where that length ends the list at <paramref name="end"/>; the nearest such, and -1 for none or
	/// for a module saved with sound off (its first dword nought).
	/// </summary>
	private static int FindList( byte[] body, int end )
	{
		for ( var at = end - 32; at >= 0; --at )
		{
			if ( !Is( body, at, Opening ) )
				continue;

			var block = Int32( body, at + 12 );
			var list = at + 16 + block + 12;

			if ( block < 0 || list + 4 > end || list + Int32( body, list ) != end )
				continue;

			return Int32( body, at + 4 ) != 0 ? list : -1;
		}

		return -1;
	}

	private static bool Is( byte[] body, int at, string tag )
		=> at >= 0 && at + tag.Length <= body.Length && body.AsSpan( at, tag.Length ).SequenceEqual( System.Text.Encoding.ASCII.GetBytes( tag ) );

	private static int Int32( byte[] body, int at ) => BinaryPrimitives.ReadInt32LittleEndian( body.AsSpan( at, 4 ) );

	private static float Single( byte[] body, int at ) => BinaryPrimitives.ReadSingleLittleEndian( body.AsSpan( at, 4 ) );
}
