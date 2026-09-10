namespace OpenTPW;

public class SdtArchive : IArchive
{
	private ExpandedMemoryStream memoryStream;
	private SoundFile mp2Reader;

	public byte[] buffer;
	public List<MP2File> soundFiles;

	public SdtArchive( string path )
	{
		soundFiles = new List<MP2File>();
		using var fileStream = File.OpenRead( path );
		ReadFromStream( fileStream );
	}

	public SdtArchive( Stream stream )
	{
		soundFiles = new List<MP2File>();
		ReadFromStream( stream );
	}

	public void Dispose()
	{
		memoryStream.Dispose();
	}

	public void ReadFromStream( Stream stream )
	{
		// Set up read buffer
		var tempStreamReader = new StreamReader( stream );
		var fileLength = (int)tempStreamReader.BaseStream.Length;

		buffer = new byte[fileLength];
		tempStreamReader.BaseStream.Read( buffer, 0, fileLength );
		tempStreamReader.Close();
		memoryStream = new ExpandedMemoryStream( buffer );
		mp2Reader = new SoundFile( new MemoryStream(buffer) );

		ReadArchive();
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
				4 bytes: Sample rate
				4 bytes: Resolution
				4 bytes: Sound type
					*See enum above (also: https://github.com/ufdada/dk2-tools/blob/6b4e49b607bbb7e0aa843856e584f6dd1365e7fc/Formats/Sound/sdt_struct.bt)
				4 bytes: Unknown
				4 bytes: Samples
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
