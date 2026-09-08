namespace OpenTPW;

/// <summary>
/// Performs operations that allow the quick, easy access of Theme Park World game files.
/// </summary>
public static class GameDir
{
	/// <summary>
	/// Joins <see cref="Settings.Default.GamePath"/> to <param name="path">path</param>.
	/// </summary>
	/// <param name="path"></param>
	/// <returns></returns>
	public static string GetPath( string path )
	{
		var normalizedPath = path.Replace( '\\', Path.DirectorySeparatorChar )
			.Replace( '/', Path.DirectorySeparatorChar );

		return Path.Join( Settings.Default.GamePath, normalizedPath );
	}
}
