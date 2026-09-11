using System.Buffers.Binary;
using System.Text;

namespace OpenTPW;

/// <summary>
/// Reads or writes one record of a save file, field by field, in whichever direction it was made for.
///
/// <para>
/// The original writes its options and players with one routine per structure, handed a flag that
/// says which way the bytes go - GameOptions_ReadWriteConfig (0x004242c0) for Config.tcf, 0x005aff50 for a
/// player's gms.dat - so the list of fields exists once and reading can never drift from writing. A
/// record here is the same: a method that takes one of these and passes each field to it by reference,
/// which fills the field when reading and takes it from the field when writing.
/// </para>
/// <para>
/// Every field is raw little-endian and follows the last with nothing in between - no names, lengths
/// or tags, and no checksum. The names the original's reads carry (0x004fae10 "VideoCard") are labels for
/// its debug log, not keys in the file. A read that runs out of bytes is an error, as it is to the
/// original ("File read failed").
/// </para>
/// </summary>
public sealed class RecordStream
{
	private readonly Stream _stream;
	private readonly byte[] _scratch = new byte[8];

	private RecordStream( Stream stream, bool writing )
	{
		_stream = stream;
		IsWriting = writing;
	}

	public static RecordStream ForReading( Stream stream ) => new( stream, false );

	public static RecordStream ForWriting( Stream stream ) => new( stream, true );

	public bool IsWriting { get; }

	public void Int32( ref int value )
	{
		if ( IsWriting )
		{
			BinaryPrimitives.WriteInt32LittleEndian( _scratch, value );
			_stream.Write( _scratch, 0, 4 );
		}
		else
		{
			Fill( 4 );
			value = BinaryPrimitives.ReadInt32LittleEndian( _scratch );
		}
	}

	public void Int16( ref short value )
	{
		if ( IsWriting )
		{
			BinaryPrimitives.WriteInt16LittleEndian( _scratch, value );
			_stream.Write( _scratch, 0, 2 );
		}
		else
		{
			Fill( 2 );
			value = BinaryPrimitives.ReadInt16LittleEndian( _scratch );
		}
	}

	public void UInt16( ref ushort value )
	{
		if ( IsWriting )
		{
			BinaryPrimitives.WriteUInt16LittleEndian( _scratch, value );
			_stream.Write( _scratch, 0, 2 );
		}
		else
		{
			Fill( 2 );
			value = BinaryPrimitives.ReadUInt16LittleEndian( _scratch );
		}
	}

	public void Byte( ref byte value )
	{
		if ( IsWriting )
		{
			_stream.WriteByte( value );
		}
		else
		{
			Fill( 1 );
			value = _scratch[0];
		}
	}

	/// <summary>A switch the original keeps in a single byte: anything but 0 is on, and on is written as 1.</summary>
	public void Bool( ref bool value )
	{
		var b = (byte)(value ? 1 : 0);
		Byte( ref b );
		value = b != 0;
	}

	/// <summary>
	/// Bytes the original writes without meaning anything by them - the rest of a word that holds a byte.
	/// They are whatever its memory held, which is zero in the files it makes, so zeros are written; on
	/// reading they are passed over.
	/// </summary>
	public void Padding( int count )
	{
		for ( int i = 0; i < count; ++i )
		{
			byte zero = 0;
			Byte( ref zero );
		}
	}

	/// <summary>A run of bytes of a known length, read into or written from <paramref name="bytes"/>.</summary>
	public void Bytes( byte[] bytes )
	{
		if ( IsWriting )
		{
			_stream.Write( bytes, 0, bytes.Length );
			return;
		}

		if ( _stream.ReadAtLeast( bytes, bytes.Length, throwOnEndOfStream: false ) < bytes.Length )
			throw new EndOfStreamException( "The file ended partway through a record" );
	}

	/// <summary>A dword length and then that many single-byte characters, with no terminator.</summary>
	public void String( ref string value )
	{
		var bytes = IsWriting ? Encoding.Latin1.GetBytes( value ) : [];
		var length = bytes.Length;
		Int32( ref length );

		if ( !IsWriting )
		{
			if ( length < 0 || length > 0x10000 )
				throw new InvalidDataException( $"A string {length} characters long is not a string" );

			bytes = new byte[length];
		}

		Bytes( bytes );
		value = Encoding.Latin1.GetString( bytes );
	}

	private void Fill( int count )
	{
		if ( _stream.ReadAtLeast( _scratch.AsSpan( 0, count ), count, throwOnEndOfStream: false ) < count )
			throw new EndOfStreamException( "The file ended partway through a record" );
	}
}
