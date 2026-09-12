using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenTPW.ModKit;

internal static class Utility
{
	/// <summary>
	/// Opens a file in ImHex, if this machine has it. Named without a path so it is taken from PATH wherever
	/// it was installed - it used to be a hard-coded C:\Program Files path, which named the wrong place on
	/// Windows as often as not and named nothing at all anywhere else.
	/// </summary>
	public static void LaunchImHex( string targetFile ) => Launch( "imhex", $"\"{targetFile}\"" );

	/// <summary>
	/// Shows a file in whatever this machine browses files with: Explorer on Windows, Finder on macOS, and on
	/// Linux whatever the desktop has registered for a directory. Only Windows and macOS can select the file
	/// itself; elsewhere the folder holding it is opened.
	/// </summary>
	public static void LaunchExplorer( string targetFile )
	{
		if ( RuntimeInformation.IsOSPlatform( OSPlatform.Windows ) )
			Launch( "explorer.exe", $"/select,\"{targetFile}\"" );
		else if ( RuntimeInformation.IsOSPlatform( OSPlatform.OSX ) )
			Launch( "open", $"-R \"{targetFile}\"" );
		else
			Launch( "xdg-open", $"\"{Path.GetDirectoryName( targetFile )}\"" );
	}

	public static void DumpFile( string targetFile )
	{
		var fileContent = FileSystem.ReadAllBytes( targetFile );

		var fileName = Path.GetFileName( targetFile );

		// Beside the editor rather than in whatever directory the game was started from.
		var directory = Path.Join( AppContext.BaseDirectory, "dumps" );
		var dest = Path.Join( directory, fileName + ".dump" );

		System.IO.Directory.CreateDirectory( directory );
		System.IO.File.WriteAllBytes( dest, fileContent );

		LaunchExplorer( dest );
	}

	/// <summary>
	/// Starts something that is not part of the game. Nothing here is worth taking the editor down for: the
	/// tool may simply not be installed, and on a machine without a desktop there is nothing to open at all.
	/// </summary>
	private static void Launch( string executable, string arguments )
	{
		try
		{
			using var process = new Process
			{
				StartInfo = new ProcessStartInfo( executable, arguments ) { UseShellExecute = false }
			};

			process.Start();
		}
		catch ( Exception e )
		{
			Log.Warning( $"ModKit: {executable} would not start - {e.Message}" );
		}
	}
}
