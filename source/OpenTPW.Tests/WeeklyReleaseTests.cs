using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The weekly build's guard (<c>tools/weekly/release.py guard</c>, Q798): a zip must hold what the game and its
/// licences need, as well as nothing of the game's. It is run as the workflow runs it, over zips made here.
/// </summary>
[TestClass]
public class WeeklyReleaseTests
{
	private const string Top = "OpenTPW-weekly-test-1";

	/// <summary>The three spellings of a build's files: how the runtime and the natives are named on each system.</summary>
	public enum Os { Linux, Windows, MacOs }

	/// <summary>Each entry release.py's REQUIRED asks for apart from the shaders, with the label the guard gives it when it is missing.</summary>
	private static (string Entry, string Label)[] Required( Os system )
	{
		// Windows spells a library "name.dll"; Linux and macOS "libname.so" and "libname.dylib".
		string Native( string name ) => system switch
		{
			Os.Windows => name + ".dll",
			Os.MacOs => "lib" + name + ".dylib",
			_ => "lib" + name + ".so",
		};

		return
		[
			( system == Os.Windows ? "OpenTPW.exe" : "OpenTPW", "the program" ),
			( "OpenTPW.dll", "OpenTPW.dll" ), ( "OpenTPW.Common.dll", "OpenTPW.Common.dll" ), ( "OpenTPW.Files.dll", "OpenTPW.Files.dll" ),
			( "OpenTPW.deps.json", "the dependency list" ), ( "OpenTPW.runtimeconfig.json", "the runtime settings" ),
			( Native( "coreclr" ), "the .NET runtime's core" ),
			( Native( "hostfxr" ), "the .NET runtime's host" ),
			( Native( "hostpolicy" ), "the .NET runtime's host policy" ),
			( system == Os.Windows ? "SDL2.dll" : Native( "SDL2-2.0" ), "SDL" ),
			( Native( "shaderc_shared" ), "shaderc" ),
			( Native( "spirv-cross" ), "SPIRV-Cross" ),
			( "content/fonts/Roboto-Medium.ttf", "the loading screen's font" ),
			( "content/fonts/LICENSE.txt", "the font's licence" ),
			( "README.txt", "README.txt" ), ( "LICENSE.md", "LICENSE.md" ), ( "THIRD-PARTY-NOTICES.md", "the third-party notices" ),
		];
	}

	/// <summary>A build's entries, by the names the packager gives them; the shaders are the repository's own.</summary>
	private static List<string> Complete( string root, bool windows = false, Os? system = null )
	{
		system ??= windows ? Os.Windows : Os.Linux;

		var entries = new List<string>
		{
			"OpenTPW.pdb", "OpenTPW.dll.config", "System.Private.CoreLib.dll", "NeoVeldrid.dll",
		};

		entries.AddRange( Required( system.Value ).Select( r => r.Entry ) );

		var shaders = Path.Join( root, "content", "shaders" );

		entries.AddRange( Directory.GetFiles( shaders, "*", SearchOption.AllDirectories ).Select( f => "content/shaders/" + Path.GetRelativePath( shaders, f ).Replace( '\\', '/' ) ) );

		return entries;
	}

	private static string Zip( IEnumerable<string> entries, string rid = "linux-x64" )
	{
		var path = Path.Join( Directory.CreateTempSubdirectory( "tpw-weekly" ).FullName, $"{Top}-{rid}.zip" );

		using ( var zip = ZipFile.Open( path, ZipArchiveMode.Create ) )
		{
			foreach ( var entry in entries )
				zip.CreateEntry( $"{Top}/{entry}" ).Open().Dispose();
		}

		return path;
	}

	/// <summary>The guard's exit code and what it printed, over a zip of these entries; the end of the test where there is no Python.</summary>
	private static (int Exit, string Output) Guard( IEnumerable<string> entries, string rid = "linux-x64" )
	{
		var zip = Zip( entries, rid );

		try
		{
			var script = Path.Join( RepoFiles.Root(), "tools", "weekly", "release.py" );
			var result = RepoFiles.Python( [script, "guard", zip] );

			if ( result == null )
				Assert.Inconclusive( "skipped: no Python here" );

			return result!.Value;
		}
		finally { Directory.Delete( Path.GetDirectoryName( zip )!, true ); }
	}

	[TestMethod]
	public void AZipWithEverythingPassesOnBothSystems()
	{
		var root = RepoFiles.Root();

		Assert.AreEqual( 0, Guard( Complete( root ) ).Exit );
		Assert.AreEqual( 0, Guard( Complete( root, windows: true ) ).Exit );
	}

	/// <remarks><b>Mutation:</b> an empty REQUIRED in release.py passes this zip.</remarks>
	[TestMethod]
	public void AZipOfOnlyTheNotesIsRefused()
	{
		var (exit, output) = Guard( ["README.txt", "LICENSE.md", "THIRD-PARTY-NOTICES.md"] );

		Assert.AreEqual( 1, exit, output );
		StringAssert.Contains( output, "missing: the program" );
		StringAssert.Contains( output, "missing: the shader 3d.shader" );
		StringAssert.Contains( output, "missing: the loading screen's font" );
	}

	[TestMethod]
	public void AZipWithoutTheFontIsRefused()
	{
		var (exit, output) = Guard( Complete( RepoFiles.Root() ).Where( e => e != "content/fonts/Roboto-Medium.ttf" ) );

		Assert.AreEqual( 1, exit, output );
		StringAssert.Contains( output, "missing: the loading screen's font" );
		Assert.AreEqual( 1, output.Split( "missing:" ).Length - 1, "only the font is missing: " + output );
	}

	[TestMethod]
	public void AZipWithoutTheFontsLicenceIsRefused()
	{
		var (exit, output) = Guard( Complete( RepoFiles.Root() ).Where( e => e != "content/fonts/LICENSE.txt" ) );

		Assert.AreEqual( 1, exit, output );
		StringAssert.Contains( output, "missing: the font's licence" );
	}

	[TestMethod]
	public void AZipWithoutTheNoticesIsRefused()
	{
		var (exit, output) = Guard( Complete( RepoFiles.Root() ).Where( e => e != "THIRD-PARTY-NOTICES.md" ) );

		Assert.AreEqual( 1, exit, output );
		StringAssert.Contains( output, "missing: the third-party notices" );
		Assert.AreEqual( 1, output.Split( "missing:" ).Length - 1, "only the notices are missing: " + output );
	}

	/// <remarks>REQUIRED names the shaders one by one, so a shader added to the repository and left off it is found here.</remarks>
	[TestMethod]
	public void EveryShaderInTheRepositoryIsRequired()
	{
		var root = RepoFiles.Root();
		var shaders = Complete( root ).Where( e => e.StartsWith( "content/shaders/" ) ).ToArray();

		Assert.IsTrue( shaders.Length >= 12, $"only {shaders.Length} shaders found" );

		foreach ( var shader in shaders )
		{
			var (exit, output) = Guard( Complete( root ).Where( e => e != shader ) );

			Assert.AreEqual( 1, exit, $"{shader} is not required: {output}" );
			StringAssert.Contains( output, "missing: the shader " + shader["content/shaders/".Length..] );
		}
	}

	/// <remarks><b>Mutation:</b> deleting any entry from REQUIRED in release.py, or narrowing a pattern to the Linux and Windows spellings (<c>dll|so</c>), fails this.</remarks>
	[TestMethod]
	public void EveryRequiredEntryIsRefusedWhenMissingOnEverySystem()
	{
		var root = RepoFiles.Root();

		foreach ( var system in Enum.GetValues<Os>() )
		{
			Assert.AreEqual( 0, Guard( Complete( root, system: system ) ).Exit, $"a complete {system} build passes" );

			foreach ( var (entry, label) in Required( system ) )
			{
				var (exit, output) = Guard( Complete( root, system: system ).Where( e => e != entry ) );

				Assert.AreEqual( 1, exit, $"{system}: {entry} is not required: {output}" );
				StringAssert.Contains( output, "missing: " + label, $"{system}: {entry}" );
			}
		}
	}

	private static readonly string[] MoltenVk = ["libMoltenVK.dylib", "MoltenVK_icd.json"];

	/// <remarks><b>Mutation:</b> an empty REQUIRED_OSX in release.py passes the zips without the file.</remarks>
	[TestMethod]
	public void AMacZipWithoutMoltenVkOrItsManifestIsRefused()
	{
		var root = RepoFiles.Root();
		var full = Complete( root, system: Os.MacOs ).Concat( MoltenVk ).ToList();

		Assert.AreEqual( 0, Guard( full, "osx-arm64" ).Exit, "a complete macOS build passes" );

		foreach ( var (file, label) in new[] { ( MoltenVk[0], "MoltenVK (macOS)" ), ( MoltenVk[1], "the Vulkan driver's manifest (macOS)" ) } )
		{
			var (exit, output) = Guard( full.Where( e => e != file ), "osx-arm64" );

			Assert.AreEqual( 1, exit, $"{file} is required: {output}" );
			StringAssert.Contains( output, "missing: " + label );
			Assert.AreEqual( 1, output.Split( "missing:" ).Length - 1, output );
		}
	}

	/// <remarks><b>Mutation:</b> putting the MoltenVK files in REQUIRED itself refuses these.</remarks>
	[TestMethod]
	public void ALinuxOrWindowsZipNeedsNoMoltenVk()
	{
		var root = RepoFiles.Root();

		Assert.AreEqual( 0, Guard( Complete( root, system: Os.Linux ), "linux-x64" ).Exit );
		Assert.AreEqual( 0, Guard( Complete( root, system: Os.Windows ), "win-x64" ).Exit );
	}

	[TestMethod]
	public void ANativeLibraryOrTheRuntimesCoreMissingIsRefused()
	{
		var root = RepoFiles.Root();

		foreach ( var name in new[] { "libSDL2-2.0.so", "libshaderc_shared.so", "libspirv-cross.so", "libcoreclr.so", "libhostfxr.so", "OpenTPW.deps.json", "OpenTPW.runtimeconfig.json" } )
			Assert.AreEqual( 1, Guard( Complete( root ).Where( e => e != name ) ).Exit, name );
	}

	/// <remarks><b>Mutation:</b> a packager that leaves the notices, the font's licence, the README or LICENSE.md out of the zip: the guard inside <c>package</c> refuses it.</remarks>
	[TestMethod]
	public void ThePackagerAddsTheNotesAndTheNoticesFromTheRepository()
	{
		var root = RepoFiles.Root();
		var publish = Directory.CreateTempSubdirectory( "tpw-publish" ).FullName;
		var output = Directory.CreateTempSubdirectory( "tpw-zips" ).FullName;

		try
		{
			// A publish folder holds the build and the shaders and the one font; the packager brings the rest.
			var fromRepository = new[] { "README.txt", "LICENSE.md", "THIRD-PARTY-NOTICES.md", "content/fonts/LICENSE.txt" };

			foreach ( var entry in Complete( root ).Where( e => !fromRepository.Contains( e ) ) )
			{
				var path = Path.Join( publish, entry );

				Directory.CreateDirectory( Path.GetDirectoryName( path )! );
				File.WriteAllText( path, entry );
			}

			var script = Path.Join( root, "tools", "weekly", "release.py" );
			var result = RepoFiles.Python( [script, "package", publish, "weekly-test-1", "linux-x64", output] );

			if ( result == null )
				Assert.Inconclusive( "skipped: no Python here" );

			Assert.AreEqual( 0, result!.Value.Exit, result.Value.Output );

			using var zip = ZipFile.OpenRead( Path.Join( output, $"{Top}-linux-x64.zip" ) );
			var held = zip.Entries.Select( e => e.FullName ).ToArray();

			foreach ( var entry in fromRepository )
				CollectionAssert.Contains( held, $"{Top}/{entry}" );

			Assert.AreEqual( File.ReadAllText( Path.Join( root, "THIRD-PARTY-NOTICES.md" ) ), new StreamReader( zip.GetEntry( $"{Top}/THIRD-PARTY-NOTICES.md" )!.Open() ).ReadToEnd(), "the notices are the repository's own" );
		}
		finally
		{
			Directory.Delete( publish, true );
			Directory.Delete( output, true );
		}
	}

	[TestMethod]
	public void TheGuardsOtherRulesStillHold()
	{
		var root = RepoFiles.Root();

		Assert.AreEqual( 1, Guard( Complete( root ).Append( "data/levels/jungle/Easymode.TPWI" ) ).Exit, "an unlisted file is refused" );
		Assert.AreEqual( 1, Guard( Complete( root ).Append( "extra.wad" ) ).Exit, "one of the disc's file types is refused" );
		Assert.AreEqual( 1, Guard( Complete( root ).Append( "notes.txt" ) ).Exit, "a .txt other than the two ours is refused" );
	}
}
