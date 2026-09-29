namespace OpenTPW;

/// <summary>
/// The clock module of a park save (<c>KOLC</c>, "Clock"): the reading of the game's millisecond clock when the park
/// was saved. FileFormats <c>saves.md</c>, "The clock module (<c>KOLC</c>)".
///
/// <para>
/// <b>It is what every other reading in the save is measured against.</b> A saved script's deadlines and a saved
/// animation channel's time stamps are readings of this same clock, and the game makes its clock read this again on
/// loading (<c>FUN_004031f0</c>, <c>0x004031fd</c>, reached from <c>FUN_00415140</c> at <c>0x00415193</c>), so a
/// reading less this one is how far ahead of, or behind, the save's own moment it lies - which is what
/// <see cref="Since"/> answers.
/// </para>
///
/// <para>
/// <b>Found by two anchors that have to agree.</b> The module is two dwords between the message centre's closing tag
/// (<c>SSEM</c>, little-endian) and its own (<c>KOLC</c>): so the second tag has to sit exactly twelve bytes after
/// the first, as it does in all nine park files read. Only the first dword is taken; the second, a second stopwatch's
/// reading, is what nothing here reads.
/// </para>
/// </summary>
public sealed class ParkClock
{
	/// <summary>The tag closing the message centre's module, little-endian - <c>MESS</c> read backwards.</summary>
	private const string PrecedingTrailer = "SSEM";

	/// <summary>This module's own tag, little-endian - <c>CLOK</c> read backwards.</summary>
	private const string Trailer = "KOLC";

	/// <summary>The module's size: two dwords.</summary>
	private const int ModuleSize = 8;

	private readonly byte[] _data;

	/// <summary>Why the module was refused, or null where it was not. A refused module has no reading.</summary>
	public string? Problem { get; private set; }

	/// <summary>
	/// The clock's reading at the save, in milliseconds - the first dword, the game clock's <c>+0x4c</c>
	/// (<c>FUN_00402e60</c>, <c>0x00402e6b</c>). Null where the module was refused.
	/// </summary>
	public uint? Reading { get; private set; }

	/// <summary>
	/// Reads the module out of the inflated payload - what <see cref="SaveReader.ReadFile"/> hands back, and the same
	/// array <see cref="ParkWorld"/> is given.
	/// </summary>
	public ParkClock( byte[] inflatedPayload )
	{
		_data = inflatedPayload ?? throw new ArgumentNullException( nameof( inflatedPayload ) );

		try
		{
			Reading = BitConverter.ToUInt32( _data, FindModule() );
		}
		catch ( Exception e )
		{
			Reading = null;
			Problem ??= e.Message;
		}
	}

	/// <summary>
	/// How many milliseconds a reading of the same clock lies after the save's moment, negative for one before it -
	/// the difference taken in 32 bits, as the clock itself counts. Null where the module was refused.
	/// </summary>
	public int? Since( uint reading ) => Reading is { } saved ? unchecked((int)(reading - saved)) : null;

	/// <summary>The module's start: right after an <c>SSEM</c> tag with a <c>KOLC</c> tag the module's size after it.</summary>
	private int FindModule()
	{
		var before = System.Text.Encoding.ASCII.GetBytes( PrecedingTrailer );
		var after = System.Text.Encoding.ASCII.GetBytes( Trailer );

		for ( var at = 0; at + before.Length + ModuleSize + after.Length <= _data.Length; ++at )
		{
			if ( !_data.AsSpan( at, before.Length ).SequenceEqual( before ) )
				continue;

			var module = at + before.Length;

			if ( _data.AsSpan( module + ModuleSize, after.Length ).SequenceEqual( after ) )
				return module;
		}

		throw new InvalidDataException(
			$"no {Trailer} tag sits {ModuleSize} bytes after a {PrecedingTrailer} tag anywhere in {_data.Length} bytes" );
	}
}
