namespace OpenTPW;

public class SdtArchive : IArchive
{
	private readonly ExpandedMemoryStream memoryStream;
	private readonly SoundFile mp2Reader;

	public readonly byte[] buffer;
	public List<MP2File> soundFiles = new();

	/// <summary>Reads a bank from the disk, not the game's file system.</summary>
	public SdtArchive( string path ) : this( File.ReadAllBytes( path ) )
	{
	}

	public SdtArchive( Stream stream ) : this( BaseFormat.ReadAll( stream ) )
	{
	}

	private SdtArchive( byte[] bytes )
	{
		buffer = bytes;
		memoryStream = new ExpandedMemoryStream( buffer );
		mp2Reader = new SoundFile( new MemoryStream( buffer ) );

		ReadArchive();
	}

	public void Dispose()
	{
		memoryStream.Dispose();
	}

	public void ReadArchive(bool dataOnly = false)
	{
		memoryStream.Seek( 0, SeekOrigin.Begin );

		/*
			#File header
				4 bytes: File count
			#For each file
				4 bytes: File offset
			#For each file (at offset)
				4 bytes: Header size
				4 bytes: Data size
				16 bytes: File name (usually null terminated)
				2 bytes: Sample rate
				1 byte: Bits per sample
				1 byte: Sound type
					*See MP2File.SoundTypes (also: https://github.com/ufdada/dk2-tools/blob/6b4e49b607bbb7e0aa843856e584f6dd1365e7fc/Formats/Sound/sdt_struct.bt)
				4 bytes: Unknown
				4 bytes: Played length, in bytes of 16-bit PCM (a little less than the stream decodes to)
				4 bytes: Unknown
				n bytes: File data
		*/

		var fileCount = memoryStream.ReadInt32();

		for ( int i = 0; i < fileCount; i++ ) 
		{
			var file = mp2Reader.GetFile( memoryStream, memoryStream.ReadInt32() );
			soundFiles.Add( file );
		}
	}

	public string[] GetFiles( string internalPath )
	{
		return soundFiles.Select( x => x.Name ).ToArray();
	}

	public string[] GetDirectories( string internalPath )
	{
		// No directories in SDT files
		return Array.Empty<string>();
	}

	/// <summary>
	/// Finds an entry by name, or null.
	///
	/// StartsWith rather than an equality because the name field in a .sdt is a fixed sixteen
	/// bytes and a longer name is truncated to fit rather than shortened - "TP SCREECH 11.mp2"
	/// is stored as "TP SCREECH 11.m" - so a caller asking for the real name would never match.
	/// </summary>
	public ArchiveFile? GetFile( string name )
	{
		int index = soundFiles.FindIndex( x => x.Name.StartsWith( name, StringComparison.OrdinalIgnoreCase ) );
		return index < 0 ? null : soundFiles[index];
	}

	public byte[] GetData( int offset, int length )
	{
		memoryStream.Seek( offset, SeekOrigin.Begin );
		return memoryStream.ReadBytes( length );
	}

	public Stream? OpenFile( string path )
	{
		var file = GetFile( path );
		return file == null ? null : new MemoryStream( file.GetData() );
	}

	public long GetFileSize( string path )
	{
		return GetFile( path )?.GetData().Length ?? 0L;
	}

	public DateTime GetModifiedTime()
	{
		return DateTime.UnixEpoch;
	}
}
