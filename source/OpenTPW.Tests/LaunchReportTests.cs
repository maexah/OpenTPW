using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

[TestClass]
public class LaunchReportTests
{
	[TestMethod]
	public void TheBuildLineNamesTheTagAndTheCommit()
	{
		Assert.AreEqual( "Build: weekly-2026-11-01, commit 52ac9c2c35e5", LaunchReport.Build( "weekly-2026-11-01+52ac9c2c35e5eed437f16fadd2a758853d9fbbb1" ) );
		Assert.AreEqual( "Build: a local build, commit 9d4484a11739", LaunchReport.Build( "local+9d4484a117393d1d50b3bd4689585ddc3656bdea" ) );
		Assert.AreEqual( "Build: weekly-2026-11-01, commit unknown", LaunchReport.Build( "weekly-2026-11-01" ) );
		Assert.AreEqual( "Build: unknown, commit unknown", LaunchReport.Build( null ) );
	}

	[TestMethod]
	public void ThisBuildIsStampedLocalWithItsCommitWhereThereIsGit()
	{
		var stamp = typeof( LaunchReport ).Assembly
			.GetCustomAttributes( typeof( System.Reflection.AssemblyInformationalVersionAttribute ), false )
			.Cast<System.Reflection.AssemblyInformationalVersionAttribute>().Single().InformationalVersion;

		StringAssert.Matches( stamp, new System.Text.RegularExpressions.Regex( "^local(\\+[0-9a-f]{40})?$" ) );
	}

	[TestMethod]
	public void TheMachineLineNamesTheReleaseTheProcessorAndItsThreads()
	{
		Assert.AreEqual( "System: Ubuntu 26.04 LTS (Linux 7.0.0-38-generic), x64, AMD Ryzen 7 5800X 8-Core Processor, 16 threads",
			LaunchReport.Machine( "Linux 7.0.0-38-generic", "Ubuntu 26.04 LTS", Architecture.X64, 16, " AMD Ryzen 7 5800X 8-Core Processor " ) );
		Assert.AreEqual( "System: Microsoft Windows 10.0.22631, arm64, processor not named, 8 threads",
			LaunchReport.Machine( "Microsoft Windows 10.0.22631", null, Architecture.Arm64, 8, null ) );
	}

	[TestMethod]
	public void TheRuntimeLineSaysWhetherItCameWithTheBuild()
	{
		Assert.AreEqual( ".NET: .NET 10.0.1, linux-x64, inside this build", LaunchReport.Runtime( ".NET 10.0.1", "linux-x64", true ) );
		Assert.AreEqual( ".NET: .NET 10.0.1, win-x64, installed on the system", LaunchReport.Runtime( ".NET 10.0.1", "win-x64", false ) );
	}

	[TestMethod]
	public void TheGpuLineNamesTheDriver()
	{
		Assert.AreEqual( "GPU: AMD Radeon RX 6700 XT (RADV NAVI22) (AMD), Vulkan 1.4.318, driver radv Mesa 25.2.3",
			LaunchReport.Gpu( "AMD Radeon RX 6700 XT (RADV NAVI22)", "AMD", "1.4.318", "radv", "Mesa 25.2.3" ) );
		Assert.AreEqual( "GPU: x (y), Vulkan 1.3, driver not named", LaunchReport.Gpu( "x", "y", "1.3", null, " " ) );
	}

	[TestMethod]
	public void TheDisplayLineNamesTheModeAndBothSizes()
	{
		Assert.AreEqual( "Display: windowed, the game 1280 x 720, the desktop 2560 x 1440 at 144 Hz",
			LaunchReport.Screen( DisplayMode.Windowed, new Point2( 1280, 720 ), new VideoMode( 2560, 1440, 144 ) ) );
		Assert.AreEqual( "Display: borderless full screen, the game 1920 x 1080, the desktop not known",
			LaunchReport.Screen( DisplayMode.BorderlessFullScreen, new Point2( 1920, 1080 ), null ) );
		Assert.AreEqual( "Display: full screen, the game 800 x 600, the desktop 800 x 600 at 60 Hz",
			LaunchReport.Screen( DisplayMode.FullScreen, new Point2( 800, 600 ), new VideoMode( 800, 600, 60 ) ) );
	}

	[TestMethod]
	public void TheDataLineCountsTheDiscsFilesAndNamesTheFirstMissing()
	{
		var disc = Enumerable.Range( 1, 9 ).Select( i => $"levels/f{i}.wad" ).ToList();
		var all = new HashSet<string>( disc.Select( f => f.ToUpperInvariant() ), StringComparer.OrdinalIgnoreCase ) { "extra/patch.wad" };

		Assert.AreEqual( "Game data: 9 of the disc's 9 data files found, so it looks complete", LaunchReport.Data( disc, all ) );

		var some = new HashSet<string>( disc.Skip( 7 ), StringComparer.OrdinalIgnoreCase );

		Assert.AreEqual( "Game data: 2 of the disc's 9 data files found, so it looks incomplete; missing levels/f1.wad, levels/f2.wad, levels/f3.wad, levels/f4.wad, levels/f5.wad and 2 more",
			LaunchReport.Data( disc, some ) );
		Assert.AreEqual( "Game data: 3 of the disc's 9 data files found, so it looks incomplete; missing levels/f1.wad, levels/f2.wad, levels/f3.wad, levels/f4.wad, levels/f5.wad and 1 more",
			LaunchReport.Data( disc, new HashSet<string>( disc.Skip( 6 ), StringComparer.OrdinalIgnoreCase ) ) );
		Assert.AreEqual( "Game data: 8 of the disc's 9 data files found, so it looks incomplete; missing levels/f9.wad",
			LaunchReport.Data( disc, new HashSet<string>( disc.Take( 8 ), StringComparer.OrdinalIgnoreCase ) ) );
	}

	[TestMethod]
	public void TheBuiltInDiscListHoldsTheDiscsDataFolder()
	{
		var disc = LaunchReport.DiscFiles();

		Assert.AreEqual( 789, disc.Count );
		Assert.AreEqual( disc.Count, disc.Distinct( StringComparer.OrdinalIgnoreCase ).Count() );
		CollectionAssert.Contains( disc.ToList(), "levels/jungle/Easymode.TPWI" );
		Assert.IsFalse( disc.Any( f => f.StartsWith( '#' ) || f.StartsWith( "Data/", StringComparison.OrdinalIgnoreCase ) ) );
	}

	[TestMethod]
	public void FilesUnderListsAFolderRelativeWithForwardSlashesInAnyCase()
	{
		var folder = Directory.CreateTempSubdirectory( "tpw-launch" ).FullName;

		try
		{
			Directory.CreateDirectory( Path.Join( folder, "Levels", "Jungle" ) );
			File.WriteAllText( Path.Join( folder, "Levels", "Jungle", "EASYMODE.tpwi" ), "" );
			File.WriteAllText( Path.Join( folder, "ui.wad" ), "" );

			var found = LaunchReport.FilesUnder( folder );

			Assert.AreEqual( 2, found.Count );
			Assert.IsTrue( found.Contains( "levels/jungle/Easymode.TPWI" ) );
			Assert.IsTrue( found.Contains( "ui.wad" ) );
		}
		finally { Directory.Delete( folder, true ); }
	}

	[TestMethod]
	public void TheShippedGameLooksComplete()
	{
		GameData.Required();

		StringAssert.StartsWith( LaunchReport.Data( LaunchReport.DiscFiles(), LaunchReport.FilesUnder( GameDir.Data ) ), "Game data: 789 of the disc's 789" );
	}
}
