namespace OpenTPW;

/*
 * Special thanks to Fatbag: http://wiki.niotso.org/RefPack, 
 * WATTO: http://wiki.xentax.com/index.php/WAD_DWFB, 
 * and Rhys: https://github.com/riperiperi/FreeSO/blob/master/TSOClient/tso.files/FAR3/Decompresser.cs.
 */

public sealed class WadArchiveFile : ArchiveFile
{
	internal bool Compressed { get; set; }
	internal int SizeInArchive { get; set; }
	internal int OffsetInArchive { get; set; } 

	internal byte[]? CachedData { get; set; }

	public override byte[] GetData()
	{
		if ( CachedData != null )
			return CachedData;

		var data = Archive.GetData( OffsetInArchive, SizeInArchive );

		// Decompress file if necessary
		if ( Compressed )
		{
			var refpack = new Refpack( data );
			data = refpack.Decompress().ToArray();
		}

		// Save off data so we can use it again quickly if we need to
		CachedData = data;

		return data;
	}

	public void Free()
	{
		CachedData = null;
	}
}

public sealed class WadArchive : IArchive
{
	private ExpandedMemoryStream memoryStream;
	public byte[] Buffer { get; internal set; }

	/// <summary>
	/// The root directory ("/") for this archive.
	/// This may contain a collection of <see cref="ArchiveItem"/>s as children.
	/// </summary>
	public ArchiveDirectory Root { get; internal set; }

	public WadArchive( string path )
	{
		using var fileStream = File.OpenRead( path );
		ReadFromStream( fileStream );
	}

	public WadArchive( Stream stream )
	{
		ReadFromStream( stream );
	}

	public void Dispose()
	{
		memoryStream.Dispose();
	}

	private void ReadArchive()
	{
		memoryStream.Seek( 0, SeekOrigin.Begin );

		/* 
		 * Header:
		 * 4 - magic number ('DWFB')
		 * 4 - version
		 * 64 - padding??
		 * 4 - file count
		 * 4 - file list offset
		 * 4 - file list length
		 * 4 - null
		 */

		var magicNumber = memoryStream.ReadString( 4 );

		// Magic number
		if ( magicNumber != "DWFB" )
			throw new Exception( $"Magic number did not match: {magicNumber}" );

		// Version
		_ = memoryStream.ReadInt32();

		// Padding
		memoryStream.Seek( 64, SeekOrigin.Current ); // Skip padding

		// File count
		var fileCount = memoryStream.ReadInt32();

		// File list offset
		_ = memoryStream.ReadInt32();

		// File list length
		_ = memoryStream.ReadInt32();

		// Unused / unknown
		_ = memoryStream.ReadInt32();

		//
		// Whenever a filename contains a directory name (e.g. "textures/hi.wct"),
		// every file that comes after it will omit that directory name - but should
		// be considered part of the subdirectory.
		//
		var currentSubdirectory = "";

		// Details directory
		for ( var i = 0; i < fileCount; ++i )
		{
			/*
			 * File
			 * 4 - unused
			 * 4 - filename offset
			 * 4 - filename length
			 * 4 - data offset
			 * 4 - data length
			 * 4 - compression type ('4' for refpack)
			 * 4 - decompressed size
			 * 12 - null
			 */

			// Save the current position so that we can go back to it later
			var initialPos = memoryStream.Position;

			// Unused / unknown
			memoryStream.Seek( 4, SeekOrigin.Current );

			var newFile = new WadArchiveFile
			{
				Archive = this
			};

			// Filename offset
			var filenameOffset = memoryStream.ReadUInt32();

			// Filename length
			var filenameLength = memoryStream.ReadUInt32();

			// Data offset
			newFile.OffsetInArchive = (int)memoryStream.ReadUInt32();

			// Data length
			newFile.SizeInArchive = (int)memoryStream.ReadUInt32();

			// Compression type
			newFile.Compressed = memoryStream.ReadUInt32() == 4;

			// Decompressed size
			var decompressedSize = memoryStream.ReadUInt32();

			// Set file's name name
			memoryStream.Seek( filenameOffset, SeekOrigin.Begin );
			newFile.Name = memoryStream.ReadString( (int)filenameLength - 1 ); // String is null terminated

			//
			// Retrieve the target subdirectory within the archive's
			// file tree (and add to the tree if necessary)
			//
			ArchiveDirectory? subDirectory = Root;

			if ( newFile.Name.Contains( '\\' ) )
			{
				currentSubdirectory = newFile.Name[..(newFile.Name.LastIndexOf( "\\" ))];
				newFile.Name = newFile.Name[(newFile.Name.LastIndexOf( "\\" ) + 1)..];
			}

			// Are we currently in the root directory?
			if ( !string.IsNullOrEmpty( currentSubdirectory ) )
			{
				// Find a subdirectory object..
				var splitPath = SplitPath( currentSubdirectory );

				foreach ( string dir in splitPath )
				{
					ArchiveDirectory newSubDir;

					// The same comparison the tree is read back with. It used to be built with an ordinal,
					// case-sensitive one and read with a case-insensitive one, so "Generic" and "generic"
					// became two directories going in and one coming out.
					newSubDir = subDirectory.Children.OfType<ArchiveDirectory>().FirstOrDefault( x => SameName( x.Name, dir ) );

					if ( newSubDir == null )
					{
						// ..or create one if it doesn't exist
						newSubDir = new ArchiveDirectory( dir );
						subDirectory.Children.Add( newSubDir );
						subDirectory = newSubDir;
					}

					subDirectory = newSubDir;
				}
			}

			// Add to selected subdirectory
			subDirectory.Children.Add( newFile );

			// Return to initial position, skip to the next file's data
			memoryStream.Seek( initialPos + 40, SeekOrigin.Begin );
		}
	}

	public void ReadFromStream( Stream stream )
	{
		// Set up read buffer
		var tempStreamReader = new StreamReader( stream );
		var fileLength = (int)tempStreamReader.BaseStream.Length;

		Buffer = new byte[fileLength];
		tempStreamReader.BaseStream.Read( Buffer, 0, fileLength );
		tempStreamReader.Close();

		memoryStream = new ExpandedMemoryStream( Buffer );
		Root = new ArchiveDirectory( "/" );

		ReadArchive();
	}

	/// <summary>
	/// Whether two names in the archive are the same name.
	///
	/// Without regard to case, because the tree is built from names written where case never mattered:
	/// esprites.wad holds "Generic\Particles\SPR_PA.TPC" while the caller that wants it asks for a ".tpc".
	/// Ordinal, so the answer is the same wherever the game is run - a culture-sensitive compare makes "LIPS"
	/// and "lips" different strings under a Turkish locale, where "I" lower-cases to a dotless "ı".
	/// </summary>
	private static bool SameName( string? a, string? b ) => string.Equals( a, b, StringComparison.OrdinalIgnoreCase );

	/// <summary>
	/// The parts of a path inside the archive. Both separators are taken: the archive's own names use "\",
	/// paths arriving through <see cref="BaseFileSystem"/> use whichever the host uses, and a caller spelling
	/// one out by hand may use either.
	/// </summary>
	private static string[] SplitPath( string path ) => path.Split( ['\\', '/'], StringSplitOptions.RemoveEmptyEntries );

	private T GetItem<T>( string internalPath ) where T : ArchiveItem
	{
		var internalDirectory = Root;

		if ( internalPath == "" )
			throw new Exception( $"Path was empty" );

		var splitPath = SplitPath( internalPath );

		for ( int i = 0; i < splitPath.Length; i++ )
		{
			string dir = splitPath[i];

			if ( i == splitPath.Length - 1 )
			{
				return internalDirectory.Children.OfType<T>().FirstOrDefault( x => SameName( x.Name, dir ) );
			}

			internalDirectory = internalDirectory.Children.OfType<ArchiveDirectory>().First( x => SameName( x.Name, dir ) );
		}

		throw new FileNotFoundException( $"File not found: {internalPath}" );
	}

	private List<T> EnumerateItems<T>( string internalPath ) where T : ArchiveItem
	{
		var internalDirectory = Root;

		if ( internalPath != "" )
		{
			foreach ( string dir in SplitPath( internalPath ) )
			{
				internalDirectory = internalDirectory?.Children.OfType<ArchiveDirectory>().FirstOrDefault( x => SameName( x.Name, dir ) );
			}
		}

		return internalDirectory?.Children.OfType<T>().ToList() ?? null;
	}

	public ArchiveFile GetFile( string internalPath )
	{
		return GetItem<ArchiveFile>( internalPath );
	}

	public ArchiveDirectory GetDirectory( string internalPath )
	{
		return GetItem<ArchiveDirectory>( internalPath );
	}

	public string[] GetFiles( string internalPath )
	{
		var files = EnumerateItems<ArchiveFile>( internalPath );
		return (files?.Select( x => x.Name ).ToArray() ?? Array.Empty<string>())!;
	}

	public string[] GetDirectories( string internalPath )
	{
		var files = EnumerateItems<ArchiveDirectory>( internalPath );
		return (files?.Select( x => x.Name ).ToArray() ?? Array.Empty<string>())!;
	}

	public byte[] GetData( int offset, int length )
	{
		memoryStream.Seek( offset, SeekOrigin.Begin );
		return memoryStream.ReadBytes( length );
	}

	public Stream OpenFile( string path )
	{
		var file = GetFile( path );

		if ( file == null )
			return null;

		return new MemoryStream( file.GetData() );
	}

	public long GetFileSize( string path )
	{
		return GetFile( path ).GetData().Length;
	}

	public DateTime GetModifiedTime()
	{
		return DateTime.UnixEpoch;
	}
}
