using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// Where the tests that read real game files get them from.
///
/// <para>
/// The path is asked of the game itself, so there is one answer to "where is Theme Park World" and the tests
/// cannot drift away from it.
/// </para>
/// <para>
/// Where there is no installation the tests needing one are skipped rather than failed, so a contributor who
/// has never owned the game still gets a green run.
/// </para>
/// </summary>
internal static class GameData
{
	/// <summary>Mounted once and kept: every test using it would otherwise read the archives again each time.</summary>
	private static BaseFileSystem? _data;

	/// <summary>
	/// The game's data folder, mounted, or the end of the calling test: <see cref="Assert.Inconclusive(string)"/>
	/// throws, and MSTest reports what it throws from as skipped rather than failed.
	/// </summary>
	public static BaseFileSystem Required()
	{
		if ( _data != null )
			return _data;

		// Set before anything can stop: GameDir says where it looked through the log, and a test class that
		// happened to run first would otherwise find no logger at all.
		Log = new();

		if ( !GameDir.Find( [] ) )
			Assert.Inconclusive( "skipped: no Theme Park World installation found" );

		var data = new BaseFileSystem( GameDir.Data );

		// Exactly what Game.Run registers, so the tests see the game's own file system and not a near miss.
		data.RegisterArchiveHandler<WadArchive>( ".wad" );
		data.RegisterArchiveHandler<SdtArchive>( ".sdt" );

		return _data = data;
	}
}
