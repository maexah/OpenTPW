namespace OpenTPW;

/// <summary>
/// The 32-byte header a park save opens each coaster with - the fields the offer gate's <c>FUN_00441970</c>
/// reads back through the node the loader rebuilds from it.
/// </summary>
/// <param name="Flags">
/// The header's first dword. Bit 0 is the node's circuit-closed bit and bit 1 its open-gap bit (<c>+0x3c</c>,
/// restored at <c>0x004380e7</c> and <c>0x004382b3</c>); the bits above go to the node's own flags.
/// </param>
/// <param name="MeshInstance">
/// The word at <c>+0x0a</c>: the model instance the node is found by, the number the coaster's object record
/// holds as <c>MeshInstanceID</c>.
/// </param>
/// <param name="Handle">The word at <c>+0x0c</c>: the coaster handle its script's <c>+0xe0</c> holds.</param>
/// <param name="Pieces">The word at <c>+0x16</c>: how many 0x22-byte piece records follow the header.</param>
/// <param name="Clashes">
/// The dword at <c>+0x1c</c>: the node's <c>+0x140</c>, how many pairs of its sections intersect, which the loader
/// restores last (<c>0x0043837d</c>). The gate refuses a coaster with any.
/// </param>
public readonly record struct SavedCoaster( int Flags, int MeshInstance, int Handle, int Pieces, int Clashes )
{
	/// <summary>Whether the gate's bits pass: the circuit closed (bit 0) and no gap open in it (bit 1).</summary>
	public bool CircuitClosed => (Flags & 1) != 0 && (Flags & 2) == 0;
}

/// <summary>
/// The coasters module of a park save (<c>SAOC</c>), as far as it can be read: how many coasters it holds, and the
/// first one's header. FileFormats <c>saves.md</c>, "The coasters module".
///
/// <para>
/// <b>Only the first coaster can be found.</b> The module follows the <c>CAME</c> tag (<c>EMAK</c> in a dump) and
/// opens with four dwords, the coaster count first, then each coaster's header, its pieces, and a run of track and
/// train data that is not decoded - 510 bytes of it after the one coaster in Alexah's played jungle park. So the
/// first header sits at a known place and every later one does not: <see cref="Unread"/> says how many were left.
/// A park with no coaster is walked whole, and must land on the <c>SAOC</c> tag after its four dwords.
/// </para>
/// </summary>
public sealed class ParkCoasters
{
	/// <summary>The tag closing the camera module before this one, little-endian - so it reads <c>EMAK</c> in a dump.</summary>
	private const string PrecedingTrailer = "EMAK";

	/// <summary>This module's own tag, little-endian - <c>COAS</c> read backwards.</summary>
	private const string Trailer = "SAOC";

	/// <summary>The four dwords the module opens with: the count, then three counts of the game's coaster handles.</summary>
	private const int CountsSize = 16;

	/// <summary>Each coaster's header.</summary>
	private const int HeaderSize = 32;

	/// <summary>Each piece record after a header.</summary>
	private const int PieceSize = 0x22;

	private readonly byte[] _data;

	/// <summary>Why the module was refused, or null where it was not. A refused module reads no coaster.</summary>
	public string? Problem { get; private set; }

	/// <summary>How many coasters the module says it holds.</summary>
	public int Count { get; private set; }

	/// <summary>The first coaster's header, or null where there is none or the module was refused.</summary>
	public SavedCoaster? First { get; private set; }

	/// <summary>How many coasters' headers could not be found - every one after the first.</summary>
	public int Unread => Math.Max( 0, Count - (First != null ? 1 : 0) );

	/// <summary>
	/// Reads the module out of the inflated payload - what <see cref="SaveReader.ReadFile"/> hands back, and the
	/// same array <see cref="ParkWorld"/> is given.
	/// </summary>
	public ParkCoasters( byte[] inflatedPayload )
	{
		_data = inflatedPayload ?? throw new ArgumentNullException( nameof( inflatedPayload ) );

		try
		{
			Read();
		}
		catch ( Exception e )
		{
			Count = 0;
			First = null;
			Problem ??= e.Message;
		}
	}

	/// <summary>The header of the coaster whose model instance this is, or null where none that was read has it.</summary>
	public SavedCoaster? For( int meshInstance )
		=> meshInstance != 0 && First is { } first && first.MeshInstance == meshInstance ? first : null;

	private void Read()
	{
		var start = FindModule();

		if ( start + CountsSize > _data.Length )
			throw new InvalidDataException( "the coasters module runs past the payload" );

		var count = BitConverter.ToInt32( _data, start );

		if ( count < 0 )
			throw new InvalidDataException( $"the coasters module says {count} coasters" );

		var at = start + CountsSize;

		if ( count == 0 )
		{
			if ( !TagAt( at ) )
				throw new InvalidDataException( $"the coasters module holds none and ends at {at} on no {Trailer} tag" );

			return;
		}

		if ( at + HeaderSize > _data.Length )
			throw new InvalidDataException( "the first coaster's header runs past the payload" );

		var first = new SavedCoaster(
			Flags: BitConverter.ToInt32( _data, at ),
			MeshInstance: BitConverter.ToUInt16( _data, at + 0x0a ),
			Handle: BitConverter.ToUInt16( _data, at + 0x0c ),
			Pieces: BitConverter.ToUInt16( _data, at + 0x16 ),
			Clashes: BitConverter.ToInt32( _data, at + 0x1c ) );

		// The header and its pieces have to end before the module's tag does.
		var pieces = at + HeaderSize + (first.Pieces * PieceSize);

		if ( !TagAtOrAfter( pieces ) )
			throw new InvalidDataException( $"no {Trailer} tag follows the first coaster's {first.Pieces} pieces" );

		Count = count;
		First = first;
	}

	/// <summary>The module's start: right after the first <c>EMAK</c> tag, which is the only one in all nine park files read.</summary>
	private int FindModule()
	{
		var tag = System.Text.Encoding.ASCII.GetBytes( PrecedingTrailer );

		for ( var at = 0; at + tag.Length <= _data.Length; ++at )
		{
			if ( _data.AsSpan( at, tag.Length ).SequenceEqual( tag ) )
				return at + tag.Length;
		}

		throw new InvalidDataException( $"no {PrecedingTrailer} tag anywhere in {_data.Length} bytes" );
	}

	private bool TagAt( int at )
		=> at + Trailer.Length <= _data.Length
			&& System.Text.Encoding.ASCII.GetString( _data, at, Trailer.Length ) == Trailer;

	private bool TagAtOrAfter( int from )
	{
		for ( var at = from; at + Trailer.Length <= _data.Length; ++at )
		{
			if ( TagAt( at ) )
				return true;
		}

		return false;
	}
}
