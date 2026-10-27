using System.Reflection;
using System.Runtime.InteropServices;
using NeoVeldrid;

namespace OpenTPW;

/// <summary>
/// What a tester's <c>console.log</c> says at its head, so a report from another machine can be read without
/// asking: the build and its commit, the system and its processor, the .NET runtime, the graphics card and its
/// Vulkan driver, the window and the display, and whether the game's data is all there. OpenTPW's own: the
/// original logs none of it.
///
/// <para>
/// Nothing here names the person or the computer: no account name, no machine name, no home folder. The game's
/// folder is printed by <see cref="GameDir"/>, and the file cuts it (and every other path under the home folder)
/// to a safe form - see <see cref="SafePaths"/>.
/// </para>
/// <para>
/// Each line is built by a function of what was measured, so the wording is tested without the machine; the
/// probes beside them read this machine and never throw.
/// </para>
/// </summary>
internal static class LaunchReport
{
	/// <summary>The build's name and commit, from the entry assembly's informational version (<c>tag+commit</c>; see the project file).</summary>
	public static string Build( string? informational )
	{
		if ( string.IsNullOrWhiteSpace( informational ) )
			return "Build: unknown, commit unknown";

		var plus = informational.IndexOf( '+' );
		var tag = BuildName( informational );
		var commit = plus < 0 ? string.Empty : informational[(plus + 1)..];

		commit = commit.Length == 0 ? "unknown" : commit[..Math.Min( 12, commit.Length )];

		return tag == "local"
			? $"Build: a local build, commit {commit}"
			: $"Build: {tag}, commit {commit}";
	}

	/// <summary>
	/// The build's name alone, the informational version before its <c>+commit</c>: <c>weekly-YYYY-MM-DD</c> for a weekly
	/// build, <c>local</c> for any other, <c>unknown</c> where the version is missing. The lobby letters it bottom right
	/// (<see cref="UI.VersionLabel"/>).
	/// </summary>
	public static string BuildName( string? informational )
	{
		if ( string.IsNullOrWhiteSpace( informational ) )
			return "unknown";

		var plus = informational.IndexOf( '+' );
		return plus < 0 ? informational : informational[..plus];
	}

	/// <summary>The build's name from the running program's own version (<see cref="BuildName"/>).</summary>
	public static string RunningBuildName
		=> BuildName( Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion );

	/// <summary>The system, its release where it names one, the processor and how many threads it runs.</summary>
	public static string Machine( string os, string? release, Architecture architecture, int threads, string? processor )
	{
		var named = string.IsNullOrWhiteSpace( release ) ? os : $"{release} ({os})";
		var cpu = string.IsNullOrWhiteSpace( processor ) ? "processor not named" : processor.Trim();

		return $"System: {named}, {architecture.ToString().ToLowerInvariant()}, {cpu}, {threads} threads";
	}

	/// <summary>The runtime, the platform it was built for, and whether it came inside the build or was installed.</summary>
	public static string Runtime( string framework, string runtimeIdentifier, bool bundled )
		=> $".NET: {framework}, {runtimeIdentifier}, {(bundled ? "inside this build" : "installed on the system")}";

	/// <summary>The graphics card and the Vulkan driver it runs through.</summary>
	public static string Gpu( string device, string vendor, string api, string? driverName, string? driverInfo )
	{
		var driver = string.Join( " ", new[] { driverName, driverInfo }.Where( s => !string.IsNullOrWhiteSpace( s ) ) );

		return $"GPU: {device} ({vendor}), Vulkan {api}, driver {(driver.Length == 0 ? "not named" : driver)}";
	}

	/// <summary>How the game fills the screen, the window's size and the desktop's mode.</summary>
	public static string Screen( DisplayMode mode, Point2 window, VideoMode? desktop )
	{
		var how = mode switch
		{
			DisplayMode.FullScreen => "full screen",
			DisplayMode.BorderlessFullScreen => "borderless full screen",
			_ => "windowed"
		};

		var display = desktop is { } d ? $"{d.Width} x {d.Height} at {d.RefreshRate} Hz" : "not known";

		return $"Display: {how}, the game {window.X} x {window.Y}, the desktop {display}";
	}

	/// <summary>
	/// How many of the disc's data files the game folder holds, the first few missing, and whether it looks whole.
	/// Files the disc does not have (a patch's, a save) are not counted either way.
	/// </summary>
	public static string Data( IReadOnlyList<string> disc, IReadOnlySet<string> found )
	{
		var missing = disc.Where( f => !found.Contains( f ) ).ToList();
		var count = $"{disc.Count - missing.Count} of the disc's {disc.Count} data files found";

		if ( missing.Count == 0 )
			return $"Game data: {count}, so it looks complete";

		var named = string.Join( ", ", missing.Take( 5 ) );
		var more = missing.Count > 5 ? $" and {missing.Count - 5} more" : string.Empty;

		return $"Game data: {count}, so it looks incomplete; missing {named}{more}";
	}

	/// <summary>The disc's data files, relative to its data folder, from the list built in (<c>DiscData.txt</c>).</summary>
	public static IReadOnlyList<string> DiscFiles()
	{
		using var stream = typeof( LaunchReport ).Assembly.GetManifestResourceStream( "OpenTPW.DiscData.txt" );

		if ( stream == null )
			return [];

		using var reader = new StreamReader( stream );

		return reader.ReadToEnd()
			.Split( '\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries )
			.Where( line => !line.StartsWith( '#' ) )
			.ToList();
	}

	/// <summary>Every file under a data folder, relative to it with forward slashes, matched without regard to case as the original's file system does.</summary>
	public static IReadOnlySet<string> FilesUnder( string dataFolder )
	{
		var found = new HashSet<string>( StringComparer.OrdinalIgnoreCase );

		try
		{
			var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };

			foreach ( var file in Directory.EnumerateFiles( dataFolder, "*", options ) )
				found.Add( Path.GetRelativePath( dataFolder, file ).Replace( '\\', '/' ) );
		}
		catch ( Exception e ) when ( e is IOException or UnauthorizedAccessException or ArgumentException )
		{
			Log.Warning( $"Game data: the data folder could not be listed ({e.GetType().Name})" );
		}

		return found;
	}

	/// <summary>The lines that need no window: once the game's folder is found and the log is open.</summary>
	public static void LogMachine( string dataFolder )
	{
		Log.Info( Build( Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ) );
		Log.Info( Machine( Kernel() ?? RuntimeInformation.OSDescription, Release(), RuntimeInformation.OSArchitecture, Environment.ProcessorCount, Processor() ) );
		Log.Info( Runtime( RuntimeInformation.FrameworkDescription, RuntimeInformation.RuntimeIdentifier, Bundled() ) );
		Log.Info( Data( DiscFiles(), FilesUnder( dataFolder ) ) );
	}

	/// <summary>The lines that need the device and the window: once the renderer is made and the display mode applied.</summary>
	public static void LogGraphics( GraphicsDevice device, Window window, DisplayMode mode )
	{
		string? driverName = null, driverInfo = null;

		if ( device.GetVulkanInfo( out var vulkan ) )
		{
			driverName = vulkan.DriverName;
			driverInfo = vulkan.DriverInfo;
		}

		Log.Info( Gpu( device.DeviceName, device.VendorName, device.ApiVersion.ToString(), driverName, driverInfo ) );
		Log.Info( Screen( mode, window.Size, Display.Desktop( window ) ) );
	}

	/// <summary>Whether the runtime sits beside the game (a self-contained build) rather than in a system install.</summary>
	private static bool Bundled()
	{
		var runtime = Path.GetDirectoryName( typeof( object ).Assembly.Location );

		return !string.IsNullOrEmpty( runtime )
			&& Path.GetFullPath( runtime ).TrimEnd( Path.DirectorySeparatorChar ) == Path.GetFullPath( AppContext.BaseDirectory ).TrimEnd( Path.DirectorySeparatorChar );
	}

	/// <summary>Linux's kernel release (the runtime's description names the distribution there, which <see cref="Release"/> already says); none elsewhere.</summary>
	private static string? Kernel()
	{
		if ( !OperatingSystem.IsLinux() )
			return null;

		try
		{
			return "Linux " + File.ReadAllText( "/proc/sys/kernel/osrelease" ).Trim();
		}
		catch ( Exception e ) when ( e is IOException or UnauthorizedAccessException )
		{
			return null;
		}
	}

	/// <summary>The release's name where the system says one: Linux's os-release; elsewhere the description says it.</summary>
	private static string? Release()
	{
		if ( !OperatingSystem.IsLinux() )
			return null;

		try
		{
			return File.ReadLines( "/etc/os-release" )
				.Where( line => line.StartsWith( "PRETTY_NAME=", StringComparison.Ordinal ) )
				.Select( line => line["PRETTY_NAME=".Length..].Trim( '"' ) )
				.FirstOrDefault();
		}
		catch ( Exception e ) when ( e is IOException or UnauthorizedAccessException )
		{
			return null;
		}
	}

	/// <summary>The processor's own name: Linux's cpuinfo, Windows's registry, macOS's sysctl. None when the system will not say.</summary>
	private static string? Processor()
	{
		try
		{
			if ( OperatingSystem.IsLinux() )
			{
				return File.ReadLines( "/proc/cpuinfo" )
					.Where( line => line.StartsWith( "model name", StringComparison.Ordinal ) )
					.Select( line => line[(line.IndexOf( ':' ) + 1)..].Trim() )
					.FirstOrDefault();
			}

			if ( OperatingSystem.IsWindows() )
			{
				using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey( @"HARDWARE\DESCRIPTION\System\CentralProcessor\0" );
				return key?.GetValue( "ProcessorNameString" ) as string;
			}

			if ( OperatingSystem.IsMacOS() )
			{
				using var sysctl = System.Diagnostics.Process.Start( new System.Diagnostics.ProcessStartInfo( "sysctl", "-n machdep.cpu.brand_string" )
				{
					RedirectStandardOutput = true,
					UseShellExecute = false
				} );

				if ( sysctl == null || !sysctl.WaitForExit( 2000 ) )
					return null;

				return sysctl.StandardOutput.ReadToEnd().Trim();
			}
		}
		catch ( Exception e ) when ( e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException or System.Security.SecurityException )
		{
			return null;
		}

		return null;
	}
}
