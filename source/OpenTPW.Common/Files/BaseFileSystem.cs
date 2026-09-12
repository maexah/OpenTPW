using System.Collections.Concurrent;
using System.Text;

namespace OpenTPW;

public class BaseFileSystem
{
	private readonly string basePath;

	/// <summary>The base directory without a trailing separator, so one directory has one name to cache under.</summary>
	private readonly string baseDirectory;

	/// <summary>
	/// Extensions are matched without regard to case, because the game's own are not consistent: every archive
	/// it ships is lower case except data\global\Speech\lips.WAD, the advisor's lip sync.
	/// </summary>
	private readonly Dictionary<string, Type> archiveHandlers = new( StringComparer.OrdinalIgnoreCase );

	private readonly ConcurrentDictionary<string, IArchive> archiveCache = new();

	/// <summary>
	/// What each directory really holds, keyed by the directory's absolute path: a name as it might be asked
	/// for, mapped to the name the directory really uses. See <see cref="Resolve"/> for why.
	///
	/// <para>
	/// Where a directory holds two names differing only by case there is no one answer, so the entry holds null
	/// and the name is used exactly as it was asked for - on a filesystem where case matters that is the only
	/// spelling that can be meant.
	/// </para>
	/// <para>
	/// A listing is dropped when this class writes into the directory (<see cref="Forget"/>), which is the only
	/// thing it does that changes what one holds. It is a ConcurrentDictionary because the ModKit reads through
	/// this object from a background task; each listing is built once and never changed, so two threads
	/// building the same one race only to store the same answer.
	/// </para>
	/// </summary>
	private readonly ConcurrentDictionary<string, Dictionary<string, string?>> listings = new();

	/// <summary>
	/// Maps a directory that is already there.
	///
	/// <para>
	/// A missing one is not made. This maps the game's own files, and answering "there is nothing there" by
	/// making an empty directory of that name turns one clear failure into every read failing with nothing
	/// said about why - on Linux it made a directory named, in full, "C:\Program Files (x86)\Bullfrog\Theme
	/// Park World\Data" beside the tests, and six of them then read it and failed. Whatever owns a directory
	/// - the save folder, the cache - makes it before mapping it.
	/// </para>
	/// </summary>
	public BaseFileSystem( string relativePath )
	{
		basePath = Path.GetFullPath( relativePath, Directory.GetCurrentDirectory() );
		baseDirectory = Path.TrimEndingDirectorySeparator( basePath );

		if ( !Directory.Exists( basePath ) )
			throw new DirectoryNotFoundException( $"There is no directory at {basePath}" );
	}

	public void RegisterArchiveHandler<T>( string extension ) where T : IArchive
	{
		archiveHandlers[extension] = typeof( T );
	}

	public string ReadAllText( string relativePath )
	{
		using var stream = OpenRead( relativePath );
		using var reader = new StreamReader( stream, Encoding.ASCII );
		return reader.ReadToEnd();
	}

	public byte[] ReadAllBytes( string relativePath )
	{
		using var stream = OpenRead( relativePath );
		using var ms = new MemoryStream();
		stream.CopyTo( ms );
		return ms.ToArray();
	}

	public bool FileExists( string relativePath )
	{
		var absolutePath = GetAbsolutePath( relativePath );
		return File.Exists( absolutePath );
	}

	public bool DirectoryExists( string relativePath )
	{
		var absolutePath = GetAbsolutePath( relativePath );
		return Directory.Exists( absolutePath );
	}

	public Stream OpenWrite( string relativePath )
	{
		var absolutePath = GetAbsolutePath( relativePath );
		var (archivePath, _) = FindArchivePath( absolutePath );

		if ( !string.IsNullOrEmpty( archivePath ) )
		{
			throw new NotImplementedException( "Can't write to archives" );
		}

		string? directoryName = Path.GetDirectoryName( absolutePath );
		if ( !Directory.Exists( directoryName ) )
			Directory.CreateDirectory( directoryName );

		// What these directories hold is about to change - see Listing.
		Forget( absolutePath );

		// Create rather than File.OpenWrite, which keeps a file's old length: writing a shorter file over
		// a longer one would leave the longer one's tail on the end.
		return File.Open( absolutePath, FileMode.Create, FileAccess.Write );
	}

	/// <summary>
	/// Writes a whole file, replacing whatever was there. The bytes go to a file beside it first, which then
	/// takes its place, so a game that stops partway through a save leaves the old file whole rather than
	/// half of the new one.
	/// </summary>
	public void WriteAllBytes( string relativePath, byte[] bytes )
	{
		var absolutePath = GetAbsolutePath( relativePath );
		var (archivePath, _) = FindArchivePath( absolutePath );

		if ( !string.IsNullOrEmpty( archivePath ) )
			throw new NotImplementedException( "Can't write to archives" );

		var directoryName = Path.GetDirectoryName( absolutePath );

		if ( !string.IsNullOrEmpty( directoryName ) )
			Directory.CreateDirectory( directoryName );

		var temporary = absolutePath + ".tmp";
		File.WriteAllBytes( temporary, bytes );
		File.Move( temporary, absolutePath, overwrite: true );

		// What these directories hold has just changed - see Listing.
		Forget( absolutePath );
	}

	public Stream OpenRead( string relativePath )
	{
		var absolutePath = GetAbsolutePath( relativePath );
		var (archivePath, internalPath) = FindArchivePath( absolutePath );

		if ( !string.IsNullOrEmpty( archivePath ) )
		{
			var archive = GetArchive( archivePath );
			return archive?.OpenFile( internalPath );
		}

		return File.Open( absolutePath, FileMode.Open, FileAccess.Read, FileShare.Read );
	}

	public long GetSize( string relativePath )
	{
		var absolutePath = GetAbsolutePath( relativePath );
		var (archivePath, internalPath) = FindArchivePath( absolutePath );

		if ( !string.IsNullOrEmpty( archivePath ) )
		{
			var archive = GetArchive( archivePath );
			return archive?.GetFileSize( internalPath ) ?? 0L;
		}

		return new FileInfo( absolutePath ).Length;
	}

	public DateTime GetModifiedTime( string relativePath )
	{
		var absolutePath = GetAbsolutePath( relativePath );
		var (archivePath, internalPath) = FindArchivePath( absolutePath );

		if ( !string.IsNullOrEmpty( archivePath ) )
		{
			var archive = GetArchive( archivePath );
			return archive?.GetModifiedTime() ?? DateTime.UnixEpoch;
		}

		return new FileInfo( absolutePath ).LastWriteTime;
	}

	public string[] GetFiles( string relativePath )
	{
		return GetFileSystemEntries( relativePath, false );
	}

	public string[] GetDirectories( string relativePath )
	{
		return GetFileSystemEntries( relativePath, true );
	}

	private string[] GetFileSystemEntries( string relativePath, bool directories )
	{
		var absolutePath = GetAbsolutePath( relativePath );
		var (archivePath, internalPath) = FindArchivePath( absolutePath );

		if ( !string.IsNullOrEmpty( archivePath ) )
		{
			var archive = GetArchive( archivePath );
			var entries = directories ? archive.GetDirectories( internalPath ) : archive.GetFiles( internalPath );
			return entries.Select( entry => Path.Combine( relativePath, entry ) ).ToArray();
		}

		if ( directories )
		{
			var fileSystemDirectories = Directory.GetDirectories( absolutePath );

			// An archive stands in for a directory of the same name, so it is listed as one, without its
			// extension. Path.ChangeExtension says that in one word and takes it off the file's own name,
			// where LastIndexOf( "." ) is the culture-sensitive overload and searches the whole path.
			var fileSystemArchives = Directory.GetFiles( absolutePath )
				.Where( x => archiveHandlers.ContainsKey( Path.GetExtension( x ) ) )
				.Select( x => Path.ChangeExtension( x, null ) );

			return fileSystemDirectories.Concat( fileSystemArchives ).ToArray();
		}
		else
		{
			return Directory.GetFiles( absolutePath ).Where( x => !archiveHandlers.ContainsKey( Path.GetExtension( x ) ) ).ToArray();
		}
	}

	private IArchive GetArchive( string archivePath )
	{
		if ( archiveCache.TryGetValue( archivePath, out var archive ) )
		{
			return archive;
		}

		var extension = Path.GetExtension( archivePath );
		if ( archiveHandlers.TryGetValue( extension, out var handlerType ) )
		{
			archive = (IArchive)Activator.CreateInstance( handlerType, new[] { archivePath } );
			archiveCache[archivePath] = archive;
			return archive;
		}

		return null;
	}

	private (string ArchivePath, string InternalPath) FindArchivePath( string path )
	{
		// Only inside our own tree. Nothing above the base directory is ours to look in, and the parts of an
		// absolute path above it can never name one of the game's archives.
		if ( !Inside( path ) )
			return (string.Empty, path);

		var parts = path[baseDirectory.Length..]
			.Split( Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries );

		var directory = baseDirectory;

		for ( int i = 0; i < parts.Length; ++i )
		{
			// An archive standing in for this part of the path, which is how a .wad is a directory as far as
			// everything above here is concerned.
			if ( ArchiveName( directory, parts[i] ) is { } archive )
				return (Path.Join( directory, archive ), string.Join( Path.DirectorySeparatorChar, parts[(i + 1)..] ));

			directory = Path.Join( directory, parts[i] );
		}

		return (string.Empty, path);
	}

	public string GetAbsolutePath( string relativePath )
	{
		var normalizedPath = relativePath.Replace( '\\', Path.DirectorySeparatorChar )
			.Replace( '/', Path.DirectorySeparatorChar );

		// Already an absolute path within our base directory (e.g. returned by
		// Directory.GetFiles/GetDirectories) - use as-is rather than combining again, and without matching it
		// against the disk, because a name the disk itself gave us is already spelled the way the disk spells
		// it. The test is ordinal: where case matters, a path differing from the base only by case is not
		// inside the base at all, and saying that it is hands back a path that cannot be opened.
		if ( Path.IsPathRooted( normalizedPath ) && Inside( normalizedPath ) )
			return normalizedPath;

		return Resolve( Path.Combine( basePath, normalizedPath.TrimStart( Path.DirectorySeparatorChar ) ) );
	}

	/// <summary>
	/// Whether a path names something inside the mapped directory. The whole name has to match, not just the
	/// front of it: without that test a sibling called "dataX" counts as being inside "data".
	/// </summary>
	private bool Inside( string path )
		=> path.StartsWith( baseDirectory, StringComparison.Ordinal )
			&& (path.Length == baseDirectory.Length || path[baseDirectory.Length] == Path.DirectorySeparatorChar);

	/// <summary>
	/// The path as the disk really spells it.
	///
	/// <para>
	/// The game's data was laid out where case never mattered and it shows: the advisor's lip sync is
	/// data\global\Speech\lips.WAD where every other archive is lower case, and the sound maps ask for
	/// "Sound\Sfx" where the folder is "sound". Windows answered all of it. Rather than lower-casing - which
	/// would break the names that <i>are</i> spelled right - each part of the path is matched against what its
	/// parent directory really holds. A part matching nothing is kept exactly as it was asked for, so the rest
	/// of the path is left alone: it is either inside an archive or about to be written, and a file being
	/// written must keep the name its caller chose.
	/// </para>
	/// </summary>
	private string Resolve( string absolutePath )
	{
		if ( !Inside( absolutePath ) )
			return absolutePath;

		var rest = absolutePath[baseDirectory.Length..].TrimStart( Path.DirectorySeparatorChar );

		if ( rest.Length == 0 )
			return absolutePath;

		var resolved = baseDirectory;

		foreach ( var part in rest.Split( Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries ) )
			resolved = Path.Join( resolved, RealName( resolved, part ) );

		return resolved;
	}

	/// <summary>
	/// The name <paramref name="directory"/> really holds for <paramref name="name"/>, or the name as it was
	/// asked for when the directory holds nothing like it, or holds more than one thing like it.
	/// </summary>
	private string RealName( string directory, string name )
		=> Listing( directory ).TryGetValue( name, out var real ) ? real ?? name : name;

	/// <summary>
	/// The name of the archive standing in for <paramref name="stem"/> in <paramref name="directory"/>, or null
	/// if there is none.
	///
	/// The extension is matched against what the directory really holds rather than tested with File.Exists, so
	/// lips.WAD is found by the same lookup as every lower-case archive and nothing has to be registered twice;
	/// and a directory holding no archive at all answers without a syscall.
	/// </summary>
	private string? ArchiveName( string directory, string stem )
	{
		var listing = Listing( directory );

		foreach ( var extension in archiveHandlers.Keys )
		{
			if ( listing.TryGetValue( stem + extension, out var real ) )
				return real ?? stem + extension;
		}

		return null;
	}

	/// <summary>The names <paramref name="directory"/> really holds - see <see cref="listings"/>.</summary>
	private Dictionary<string, string?> Listing( string directory )
	{
		return listings.GetOrAdd( directory, static path =>
		{
			var names = new Dictionary<string, string?>( StringComparer.OrdinalIgnoreCase );

			if ( !Directory.Exists( path ) )
				return names;

			foreach ( var entry in Directory.EnumerateFileSystemEntries( path ) )
			{
				var name = Path.GetFileName( entry );

				if ( !names.TryAdd( name, name ) )
					names[name] = null;
			}

			return names;
		} );
	}

	/// <summary>
	/// Drops what was remembered about the directories a written file sits in, up to the base directory, so a
	/// name written since is matched against what is really there. Writing is the only thing this class does
	/// that changes what a directory holds.
	/// </summary>
	private void Forget( string absolutePath )
	{
		for ( var directory = Path.GetDirectoryName( absolutePath );
			!string.IsNullOrEmpty( directory ) && directory.Length >= baseDirectory.Length;
			directory = Path.GetDirectoryName( directory ) )
		{
			listings.TryRemove( directory, out _ );
		}
	}

	public string GetRelativePath( string absolutePath )
	{
		var path = Path.GetRelativePath( basePath, absolutePath ).Replace( "\\", "/" );
		return $"/{path}";
	}

	public bool IsArchive( string path )
	{
		var (archivePath, internalPath) = FindArchivePath( path );

		return !string.IsNullOrEmpty( archivePath );
	}

	public FileSystemWatcher CreateWatcher( string relativeDir, string filter )
	{
		var directoryName = GetAbsolutePath( relativeDir );
		var watcher = new FileSystemWatcher( directoryName, filter );

		watcher.NotifyFilter = NotifyFilters.Attributes
							 | NotifyFilters.CreationTime
							 | NotifyFilters.DirectoryName
							 | NotifyFilters.FileName
							 | NotifyFilters.LastAccess
							 | NotifyFilters.LastWrite
							 | NotifyFilters.Security
							 | NotifyFilters.Size;

		watcher.EnableRaisingEvents = true;

		return watcher;
	}
}
