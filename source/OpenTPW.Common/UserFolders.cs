namespace OpenTPW;

/// <summary>
/// The player's own folders, kept apart from the game's: <c>OpenTPW</c> in the data folder .NET names for the
/// system. Never verified: asked to verify, the runtime answers an empty string for a folder that is not there
/// yet (a new Linux account has no <c>~/.local/share</c>), and the path would then be relative to whatever folder
/// the game was started from.
/// </summary>
public static class UserFolders
{
	/// <summary>
	/// The player's data folder: <c>%AppData%\OpenTPW</c> on Windows, <c>$XDG_DATA_HOME/OpenTPW</c> (otherwise
	/// <c>~/.local/share/OpenTPW</c>) on Linux, <c>~/Library/Application Support/OpenTPW</c> on macOS, where
	/// .NET asks the system for its Application Support folder and reads no XDG variable (its
	/// <c>Environment.GetFolderPathCore.Unix.cs</c>, <c>TARGET_OSX</c>).
	/// </summary>
	public static string Data()
	{
		var root = OperatingSystem.IsWindows() ? Environment.SpecialFolder.ApplicationData : Environment.SpecialFolder.LocalApplicationData;

		return System.IO.Path.Join( Environment.GetFolderPath( root, Environment.SpecialFolderOption.DoNotVerify ), "OpenTPW" );
	}

	/// <summary>
	/// The cache folder: <c>%LocalAppData%\OpenTPW\cache</c> on Windows, where a cache is not roamed, and
	/// <c>cache</c> in <see cref="Data"/> elsewhere, where the two roots are one.
	/// </summary>
	public static string Cache()
		=> System.IO.Path.Join( Environment.GetFolderPath( Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify ), "OpenTPW", "cache" );
}
