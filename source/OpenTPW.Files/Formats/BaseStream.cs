using System.Text;

namespace OpenTPW;

/// <summary>
/// <b>Nothing constructs this, and this code does not run.</b> It is an internal copy of
/// <see cref="ExpandedMemoryStream"/>, the stream the readers use, and no other assembly can see it.
///
/// <para>
/// Its <c>ReadInt16</c> takes four bytes and returns the low sixteen bits, where
/// <see cref="ExpandedMemoryStream.ReadInt16"/> takes two, so a short read from it would land two bytes past
/// the next field. Kept rather than deleted, as dead code is here; read it as a note, not a second stream to
/// build on.
/// </para>
/// </summary>
internal class BaseStream : MemoryStream
{
	public BaseStream( byte[] buffer ) : base( buffer ) { }
	public byte[] ReadBytes( int length, bool bigEndian = false )
	{
		var bytes = new byte[length];
		Read( bytes, 0, length );

		if ( bigEndian )
			Array.Reverse( bytes );
		return bytes;
	}

	public char[] ReadChars( int length, bool bigEndian = false )
	{
		return Encoding.ASCII.GetChars( ReadBytes( length, bigEndian ) );
	}

	public string ReadString( int length, bool bigEndian = false )
	{
		return Encoding.ASCII.GetString( ReadBytes( length, bigEndian ) );
	}

	public string ReadHex( int length, bool bigEndian = false )
	{
		return Convert.ToHexString( ReadBytes( length, bigEndian ) );
	}

	public int ReadInt16( bool bigEndian = false )
	{
		return BitConverter.ToInt16( ReadBytes( 4, bigEndian ), 0 );
	}

	public int ReadInt32( bool bigEndian = false )
	{
		return BitConverter.ToInt32( ReadBytes( 4, bigEndian ), 0 );
	}

	public uint ReadUInt32( bool bigEndian = false )
	{
		return BitConverter.ToUInt32( ReadBytes( 4, bigEndian ), 0 );
	}

	public uint ReadUIntN( int n, bool bigEndian = false )
	{
		return BitConverter.ToUInt32( ReadBytes( n, bigEndian ), 0 );
	}
}
