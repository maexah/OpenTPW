using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The Windows release opens no terminal behind the game (Q979): the project's output is a windowed program in Release and a console
/// program otherwise, so a Debug build keeps the console the debug console reads from. The built exe's header is looked at in the
/// running program (the Windows publish's subsystem), not here.
/// </summary>
[TestClass]
public class WindowedReleaseTests
{
	private static XElement[] Groups()
		=> XDocument.Load( Path.Join( RepoFiles.Root(), "source", "OpenTPW", "OpenTPW.csproj" ) ).Root!.Elements( "PropertyGroup" ).ToArray();

	private static string? OutputTypeUnder( string? condition )
		=> Groups().Where( g => (string?)g.Attribute( "Condition" ) == condition ).Select( g => (string?)g.Element( "OutputType" ) ).FirstOrDefault( v => v != null );

	[TestMethod]
	public void ABuildWithNoConfigurationNamedIsAConsoleProgram()
		=> Assert.AreEqual( "Exe", OutputTypeUnder( null ) );

	[TestMethod]
	public void AReleaseBuildIsAWindowedProgram()
		=> Assert.AreEqual( "WinExe", OutputTypeUnder( "'$(Configuration)' == 'Release'" ) );

	// MSBuild's last write wins: the conditioned group above the plain one would leave every Release build an Exe.
	[TestMethod]
	public void TheReleaseGroupComesAfterTheGroupItOverrides()
	{
		var groups = Groups();
		var plain = Array.FindIndex( groups, g => (string?)g.Attribute( "Condition" ) == null && g.Element( "OutputType" ) != null );
		var release = Array.FindIndex( groups, g => (string?)g.Attribute( "Condition" ) == "'$(Configuration)' == 'Release'" && g.Element( "OutputType" ) != null );

		Assert.IsTrue( plain >= 0 && release > plain, $"plain at {plain}, Release at {release}" );
	}

	[TestMethod]
	public void OnlyReleaseIsWindowed()
	{
		var windowed = Groups().Where( g => (string?)g.Element( "OutputType" ) == "WinExe" ).Select( g => (string?)g.Attribute( "Condition" ) ).ToArray();

		CollectionAssert.AreEqual( new[] { "'$(Configuration)' == 'Release'" }, windowed );
	}

	[TestMethod]
	public void NoParentTerminalIsJoinedOffWindows()
	{
		if ( OperatingSystem.IsWindows() )
			Assert.Inconclusive( "Windows: joining a parent terminal depends on how the test host was started." );

		Assert.IsFalse( ParentConsole.Attach() );
	}
}
