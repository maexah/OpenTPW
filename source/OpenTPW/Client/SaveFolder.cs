namespace OpenTPW;

/// <summary>
/// The game's save folder - save\ beside its data, which <see cref="SaveFileSystem"/> maps - laid out as the
/// original lays it out.
///
/// <para>
/// The original keeps the options that belong to the machine in save\Config.tcf (<see cref="ConfigFile"/>),
/// each player in a folder of their own under save\users\ named for their slot and their name, with their
/// progress and their own options in gms.dat, and each player's parks under that in a folder per theme.
/// </para>
/// <para>
/// File names are matched without regard to case when reading. The original ran where case never
/// mattered, and names its own files inconsistently - it writes "Config.tcf", and the safe-mode batch file
/// shipped beside it copies to "config.tcf" - so a file is written back under whatever name it was found by.
/// </para>
/// <para>
/// A file that will not read is reported and left alone, and whatever it held keeps its default. Every
/// file is written whole to one beside it and then moved into place, so a save cut short never leaves half
/// a file behind.
/// </para>
/// </summary>
internal static class SaveFolder
{
	private const string ConfigName = "Config.tcf";

	/// <summary>Reads save\Config.tcf into the options, if there is one - as the original does before anything else starts (0x00424930).</summary>
	public static void LoadConfig()
	{
		if ( Find( ConfigName ) is not { } path )
			return;

		try
		{
			using var stream = SaveFileSystem.OpenRead( path );

			if ( ConfigFile.Read( stream ) is { } file )
				GameOptions.Current.Apply( file );

			Log.Info( $"Saves: read the options from {path}" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Saves: {path} would not read, so the options keep their defaults - {e.Message}" );
		}
	}

	/// <summary>Writes the machine's options to save\Config.tcf - the options screen's tick (0x004237f0), and a player being saved (0x00424820).</summary>
	public static void SaveConfig()
	{
		var path = Find( ConfigName ) ?? ConfigName;

		try
		{
			using var memory = new MemoryStream();
			GameOptions.Current.ToConfigFile().Write( memory );

			SaveFileSystem.WriteAllBytes( path, memory.ToArray() );
			Log.Info( $"Saves: wrote the options to {path}" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Saves: the options could not be written to {path} - {e.Message}" );
		}
	}

	/// <summary>The name a file in the save folder goes by, whatever its case, or null if there is none.</summary>
	private static string? Find( string name, string directory = "" )
	{
		if ( !SaveFileSystem.DirectoryExists( directory ) )
			return null;

		var match = SaveFileSystem.GetFiles( directory )
			.Select( Path.GetFileName )
			.FirstOrDefault( file => string.Equals( file, name, StringComparison.OrdinalIgnoreCase ) );

		return match == null ? null : Path.Join( directory, match );
	}
}
