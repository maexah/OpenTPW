using System.Runtime.Versioning;
using Microsoft.Win32;

namespace OpenTPW;

/// <summary>
/// Where Windows says Theme Park World was installed (Q981). The installer writes no folder into the registry's
/// vendor key; the only record of the folder is the uninstall entry, whose <c>UninstallString</c> carries it as
/// <c>-f"&lt;folder&gt;\Uninst.isu"</c>. The disc's name for the game is "Theme Park World" and the box's is
/// "SimTheme Park", and a PC has been seen with the key called the first and the display name the second, so either
/// name on either value counts. Read on a Windows 11 PC with Sim Theme Park in <c>C:\Program Files (x86)\SimTheme
/// Park</c>, which the original's default of <c>C:\Program Files (x86)\Bullfrog\Theme Park World</c> missed.
/// </summary>
public static class WindowsInstall
{
	/// <summary>The uninstall list, in the 32-bit view of HKLM (<c>WOW6432Node</c>), where a 32-bit installer writes.</summary>
	private const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

	private const string FolderSwitch = "-f";

	/// <summary>The folder name the box's installer uses, tried among the defaults when the registry names none.</summary>
	public const string BoxFolderName = "SimTheme Park";

	/// <summary>The program-files folder of 32-bit programs, for a machine that cannot say.</summary>
	private const string ProgramFilesX86Fallback = @"C:\Program Files (x86)";

	/// <summary>The folder Sim Theme Park's installer chooses, inside the 32-bit program-files folder.</summary>
	public static string DefaultFolder()
		=> DefaultFolder( Environment.GetFolderPath( Environment.SpecialFolder.ProgramFilesX86 ) );

	/// <summary><see cref="DefaultFolder()"/> for a given program-files folder; an empty one is the usual <c>C:\Program Files (x86)</c>.</summary>
	public static string DefaultFolder( string programFilesX86 )
		=> (string.IsNullOrWhiteSpace( programFilesX86 ) ? ProgramFilesX86Fallback : programFilesX86.TrimEnd( '\\', '/' )) + @"\" + BoxFolderName;

	/// <summary>
	/// The folder the uninstall entries name, in the order found. Nothing off Windows, and nothing when the
	/// registry will not open: a registry that cannot be read is no reason to stop, the defaults are tried next.
	/// </summary>
	public static IReadOnlyList<string> RegisteredFolders()
	{
		var folders = new List<string>();

		if ( !OperatingSystem.IsWindows() )
			return folders;

		try
		{
			foreach ( var view in new[] { RegistryView.Registry32, RegistryView.Registry64 } )
				ReadView( view, folders );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Game files: the Windows uninstall list could not be read, so it is passed over - {e.Message}" );
		}

		return folders;
	}

	[SupportedOSPlatform( "windows" )]
	private static void ReadView( RegistryView view, List<string> folders )
	{
		using var hklm = RegistryKey.OpenBaseKey( RegistryHive.LocalMachine, view );
		using var list = hklm.OpenSubKey( UninstallKey );

		if ( list == null )
			return;

		foreach ( var name in list.GetSubKeyNames() )
		{
			using var entry = list.OpenSubKey( name );

			if ( entry == null )
				continue;

			if ( !IsTheGame( name, entry.GetValue( "DisplayName" ) as string ) )
				continue;

			var folder = FolderOfUninstallString( entry.GetValue( "UninstallString" ) as string );

			Log.Info( $"Game files: the uninstall entry {name} ({view}) names {folder ?? "no folder"}" );

			if ( folder != null && !folders.Contains( folder, StringComparer.OrdinalIgnoreCase ) )
				folders.Add( folder );
		}
	}

	/// <summary>
	/// Whether an uninstall entry is Theme Park World's, by its key's name or its display name. Case, spaces and
	/// punctuation do not count: "SimTheme Park", "Sim Theme Park" and "SIMTHEMEPARK" are one name.
	/// </summary>
	public static bool IsTheGame( string keyName, string? displayName )
		=> IsTheGameName( keyName ) || IsTheGameName( displayName );

	private static bool IsTheGameName( string? name )
	{
		var plain = new string( (name ?? string.Empty).Where( char.IsLetter ).ToArray() ).ToLowerInvariant();

		return plain is "themeparkworld" or "simthemepark";
	}

	/// <summary>
	/// The install folder inside an InstallShield uninstall string, <c>C:\WINDOWS\IsUninst.exe -f"C:\Program Files
	/// (x86)\SimTheme Park\Uninst.isu"</c>: the folder of the file the <c>-f</c> switch names, quoted or not. Null
	/// when there is no such switch, or what follows it is no path.
	/// </summary>
	public static string? FolderOfUninstallString( string? uninstallString )
	{
		if ( string.IsNullOrWhiteSpace( uninstallString ) )
			return null;

		// The switch is the first "-f" that starts a word; the program's own path may hold the two letters.
		for ( int at = uninstallString.IndexOf( FolderSwitch, StringComparison.OrdinalIgnoreCase ); at >= 0;
			at = uninstallString.IndexOf( FolderSwitch, at + 1, StringComparison.OrdinalIgnoreCase ) )
		{
			if ( at > 0 && !char.IsWhiteSpace( uninstallString[at - 1] ) )
				continue;

			var file = uninstallString[(at + FolderSwitch.Length)..].Trim();

			if ( file.StartsWith( '"' ) )
			{
				var close = file.IndexOf( '"', 1 );
				file = close < 0 ? file[1..] : file[1..close];
			}

			var cut = file.LastIndexOfAny( ['\\', '/'] );

			if ( cut <= 0 || file.Length < 3 || !((char.IsLetter( file[0] ) && file[1] == ':') || file.StartsWith( @"\\" )) )
				return null;

			return file[..cut];
		}

		return null;
	}
}
