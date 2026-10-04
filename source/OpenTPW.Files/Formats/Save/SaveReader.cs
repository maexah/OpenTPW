using System.Text;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace OpenTPW;

public class SaveReader : BaseFormat
{
	private ExpandedMemoryStream memoryStream;
	public byte[] buffer;

	public SaveReader( string path )
	{
		using var fileStream = File.OpenRead( path );
		ReadFromStream( fileStream );
	}

	public SaveReader( Stream stream )
	{
		ReadFromStream( stream );
	}
	public void Dispose()
	{
		memoryStream.Dispose();
	}

	protected override void ReadFromStream( Stream stream )
	{
		// Set up read buffer
		var tempStreamReader = new StreamReader( stream );
		var fileLength = (int)tempStreamReader.BaseStream.Length;

		buffer = new byte[fileLength];
		tempStreamReader.BaseStream.Read( buffer, 0, fileLength );
		tempStreamReader.Close();
		memoryStream = new ExpandedMemoryStream( buffer );
	}

	/// <summary>
	/// Where the 'BILZ' block starts in a file with no author header: after a fixed 1549-byte preamble
	/// of version, a byte, legal text, a 0x100-byte block, magic and the online-header flag.
	/// </summary>
	private const int BlockStart = 0x60D;

	public byte[] ReadFile()
	{
		memoryStream.Seek( 0, SeekOrigin.Begin );

		/*
			
		Header, as the loader FUN_00416240 reads it (FileFormats saves.md, "Header")
			4 bytes: Version - 400 in the shipped park, 500 in a saved one (NOT a magic number)
			1 byte:  Compared with nothing; it picks the entry the legal text is checked against (0x004162c2); 0
			0x500 bytes: Legal text from 0x005, UTF-16 (824 bytes of notice), then zeros
			0x100 bytes: From 0x505, validated by FUN_0051ab60; zero
			4 bytes: Magic at 0x605, big-endian 0x01221985, stored 01 22 19 85
			4 bytes: Online-header flag at 0x609; an author header follows it when not 0
			
		Data	
			## ZLIB Header ##
			4 bytes: Tag - BILZ, at 0x060D
			4 bytes: Size the payload inflates to
			4 bytes: Size of this whole block, tag and header included
			16 bytes: Unknown - 15, 9, 0, 0 in the shipped park
			ZLIB stream begins at 0x0629 and continues to the end of the file
			(the 28-byte header counts the tag, so it is 4 + 24, not 4 + 28)
		*/

		// Not a magic number - it is a version (docs/exe/saves.md). data/levels/jungle/Easymode.TPWI
		// carries 400 and every park the original saved carries 500, so both are allowed and
		// anything else says what it actually found rather than printing bytes.
		var version = memoryStream.ReadUInt32();

		if ( version != 400 && version != 500 )
			throw new Exception( $"Save version {version} is not one this can read (400 or 500)" );

		// The byte, the legal text and the 0x100-byte block are stepped over, not checked. The reads
		// below cut the same bytes at other places than the loader does: 0x604 to 0x607 are the zero
		// before the magic and its first three bytes, the 133 is the magic's last byte, and the online
		// byte and its "padding" are the flag dword, which the loader reads whole.
		memoryStream.Seek( 0x0604, SeekOrigin.Begin );

		var fileType = memoryStream.ReadInt32();

		var fileVersion = memoryStream.ReadByte();
		if ( fileVersion != 133 )
			throw new Exception( $"File version is not 133." );

		// File flag
		bool isOnline = memoryStream.ReadByte() != 0 ? true : false;

		// Padding
		_ = memoryStream.ReadBytes( 2 );

		if ( isOnline )
			throw new Exception( "File is online, no compatibility for this yet." );

		// Padding
		_ = memoryStream.ReadByte();

		// Start of Data
		var dataMagicNumber = memoryStream.ReadString( 4 );

		if ( dataMagicNumber != "BILZ" )
			throw new Exception( $"Magic number did not match: {dataMagicNumber}" );

		// The two dwords after the tag are the size the payload INFLATES to and the size of this
		// whole block including its 28-byte header - neither of them a compressed length. On the shipped
		// jungle park they read 1608309 and
		// 36930, and 1549 + 36930 is exactly the file's length.
		var uncompressedSize = memoryStream.ReadInt32();
		var blockSize = memoryStream.ReadInt32();

		if ( BlockStart + blockSize != buffer.Length )
			throw new Exception(
				$"The compressed block says it is {blockSize} bytes, which does not reach the end of a " +
				$"{buffer.Length}-byte file from 0x{BlockStart:x}" );

		// Unknown, and zero except for the first two: 15, 9, 0, 0 in the shipped park. Stepping over
		// them lands on 0x629, where the zlib header actually is - the 28 bytes include the tag.
		_ = memoryStream.ReadBytes( 16 );

		using var uncompressedStream = new MemoryStream();

		using ( var compressed = new InflaterInputStream( memoryStream ) )
			compressed.CopyTo( uncompressedStream );

		var inflated = uncompressedStream.ToArray();

		if ( inflated.Length != uncompressedSize )
			throw new Exception(
				$"The payload inflated to {inflated.Length} bytes where the header said {uncompressedSize}" );

		return inflated;
	}

	/// <summary>
	/// Converts the save file from byte array to a "readable" string
	/// (removes all null entries after string conversion)
	/// </summary>
	/// <returns></returns>
	public string FileToString()
	{
		var output = ReadFile();
		return Encoding.ASCII.GetString( output ).Replace( "\0", string.Empty );
	}
}
