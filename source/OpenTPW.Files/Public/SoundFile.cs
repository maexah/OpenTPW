namespace OpenTPW;

// BIG SHOUT TO Toksisitee - https://github.com/Toksisitee/PopSoundEditor

/// <summary>
/// Reads the entries out of a .sdt sound bank - see <see cref="SdtArchive"/> for the container
/// itself.
/// </summary>
public class SoundFile : BaseFormat
{
	private ExpandedMemoryStream memoryStream;
	public byte[] buffer;

	public SoundFile( string path )
	{
		ReadFromFile( path );
	}

	public SoundFile( Stream stream )
	{
		ReadFromStream( stream );
	}

	public void Dispose()
	{
		memoryStream.Dispose();
	}

	protected void ReadFromStream( Stream stream )
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
	/// Reads one entry, starting at <paramref name="offset"/>.
	///
	/// The header is forty bytes and lays out as:
	///
	///     0x00  int32   header size - 40 in every entry in the game
	///     0x04  int32   size of the audio that follows the header
	///     0x08  char16  name, NUL-padded and truncated to fit rather than shortened
	///     0x18  uint16  sample rate
	///     0x1a  uint8   bits per sample
	///     0x1b  uint8   sound type - see <see cref="MP2File.SoundTypes"/>
	///     0x1c  int32   unknown, zero in all 3,739 entries the game ships
	///     0x20  int32   size the audio decodes to, in bytes
	///     0x24  int32   unknown, zero in all 3,739
	///
	/// Those middle four fields are the ones worth being careful about: read as an int16 and
	/// three int32s - which is how the size of the header appears to divide - the rate comes out
	/// right and everything after it is nonsense, because the bit depth and the type are a byte
	/// each rather than a word each. Across the whole game they only ever read 22,050 or 44,100
	/// at 16 bits, and 36 or 37 for the type, which is what fixes the split.
	///
	/// The fields are pulled out with <see cref="BitConverter"/> rather than off the stream
	/// because <see cref="ExpandedMemoryStream.ReadInt16"/> advances four bytes, not two.
	/// </summary>
	public MP2File GetFile( MemoryStream stream, int offset = 0, bool dataOnly = false )
	{
		var headerSize = BitConverter.ToInt32( buffer, offset );
		var soundDataSize = BitConverter.ToInt32( buffer, offset + 4 );

		var fileName = System.Text.Encoding.ASCII.GetString( buffer, offset + 8, 16 ).TrimEnd( '\0' );

		var sampleRate = BitConverter.ToUInt16( buffer, offset + 0x18 );
		var bitsPerSample = buffer[offset + 0x1a];
		var soundType = buffer[offset + 0x1b];
		var decodedSize = BitConverter.ToInt32( buffer, offset + 0x20 );

		// The audio starts where the header ends. Both sizes come out of the file, so they are
		// clamped rather than trusted - a truncated bank should give up an entry, not throw.
		var dataStart = offset + headerSize;
		var available = Math.Max( 0, Math.Min( soundDataSize, buffer.Length - dataStart ) );

		var soundData = new byte[available];
		Array.Copy( buffer, dataStart, soundData, 0, available );

		// The whole record, header included, which is what writing the entry back out needs.
		var wholeRecord = new byte[Math.Max( 0, Math.Min( headerSize + soundDataSize, buffer.Length - offset ) )];
		Array.Copy( buffer, offset, wholeRecord, 0, wholeRecord.Length );

		return new MP2File( headerSize, fileName, soundData, sampleRate, bitsPerSample, soundType,
			decodedSize, wholeRecord );
	}
}
