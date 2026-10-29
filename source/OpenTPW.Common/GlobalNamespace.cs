global using static OpenTPW.Common.GlobalNamespace;

namespace OpenTPW.Common;
public static class GlobalNamespace
{
	/// <summary>The log. It holds nothing of its own, so one exists from the start and a line logged early is not lost.</summary>
	public static Logger Log { get; set; } = new();

	/// <summary>
	/// The game's data folder, mounted at startup. Read before then it says so by name, rather than failing later
	/// on a null; <see cref="FileSystemOrNull"/> reads it without throwing.
	/// </summary>
	public static BaseFileSystem FileSystem
	{
		get => FileSystemOrNull ?? throw NotMounted( nameof( FileSystem ) );
		set => FileSystemOrNull = value;
	}

	/// <summary><see cref="FileSystem"/>, or null before it is mounted: for a caller that puts back what it found.</summary>
	public static BaseFileSystem? FileSystemOrNull { get; set; }

	/// <summary>The save folder, mounted at startup; read as <see cref="FileSystem"/> is.</summary>
	public static BaseFileSystem SaveFileSystem
	{
		get => SaveFileSystemOrNull ?? throw NotMounted( nameof( SaveFileSystem ) );
		set => SaveFileSystemOrNull = value;
	}

	/// <summary><see cref="SaveFileSystem"/>, or null before it is mounted.</summary>
	public static BaseFileSystem? SaveFileSystemOrNull { get; set; }

	/// <summary>The cache folder, or null where it could not be made: the game carries on without one.</summary>
	public static BaseFileSystem? CacheFileSystem { get; set; }

	private static InvalidOperationException NotMounted( string name )
		=> new( $"{name} was read before it was mounted" );
}
