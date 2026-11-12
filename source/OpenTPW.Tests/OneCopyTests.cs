using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// One copy at a time on one game folder (Q995), as the original's 0x0045f640 lets one copy run: a second take on a folder
/// is refused, a take on another folder is not. Each test names folders of its own, so a copy of the game running on this
/// machine, or another run of the suite, is never one of them. Two processes are shown in the running game (STATUS).
/// </summary>
[TestClass]
public class OneCopyTests
{
	private static string Folder( string what ) => Path.Join( Path.GetTempPath(), $"opentpw-onecopy-{Guid.NewGuid():N}", what );

	[TestMethod]
	public void ASecondCopyOnTheSameFolderIsRefused()
	{
		var folder = Folder( "game" );

		using var first = OneCopy.Open( folder, out var firstUnknown );
		using var second = OneCopy.Open( folder, out var secondUnknown );

		Assert.IsNotNull( first );
		Assert.IsNull( firstUnknown );
		Assert.IsNull( second, "the second copy on one folder must be refused" );
		Assert.IsNull( secondUnknown, "a refusal is a known answer, not a lock that cannot be told" );
	}

	[TestMethod]
	public void ACopyOnAnotherFolderRunsBesideIt()
	{
		var root = Folder( string.Empty );

		using var first = OneCopy.Open( Path.Join( root, "lane1" ), out _ );
		using var second = OneCopy.Open( Path.Join( root, "lane2" ), out var unknown );

		Assert.IsNotNull( first );
		Assert.IsNotNull( second, "a lock on one folder must not stop a copy on another (never machine-wide)" );
		Assert.IsNull( unknown );
	}

	[TestMethod]
	public void AnotherSpellingOfTheSameFolderIsTheSameLock()
	{
		var folder = Folder( "game" );

		using var first = OneCopy.Open( folder, out _ );
		using var trailing = OneCopy.Open( folder + Path.DirectorySeparatorChar, out _ );
		using var dotted = OneCopy.Open( Path.Join( folder, ".", "..", "game" ), out _ );

		Assert.IsNotNull( first );
		Assert.IsNull( trailing, "a closing separator is the same folder" );
		Assert.IsNull( dotted, "a path through . and .. is the same folder" );
	}

	[TestMethod]
	public void OnceTheFirstCopyEndsTheFolderIsFreeAgain()
	{
		var folder = Folder( "game" );

		OneCopy.Open( folder, out _ )!.Dispose();

		using var again = OneCopy.Open( folder, out _ );

		Assert.IsNotNull( again );
	}

	[TestMethod]
	public void CaseCountsInTheNameOnlyWhereTheSystemsNamesDo()
	{
		Assert.AreEqual( OneCopy.Name( "/Games/TPW", ignoreCase: true ), OneCopy.Name( "/games/tpw", ignoreCase: true ) );
		Assert.AreNotEqual( OneCopy.Name( "/Games/TPW", ignoreCase: false ), OneCopy.Name( "/games/tpw", ignoreCase: false ) );
	}

	[TestMethod]
	public void TheNameIsTheGlobalPrefixAndAHashWithNoSeparator()
	{
		var name = OneCopy.Name( Folder( "a/very/deep/game/folder" ) );

		StringAssert.StartsWith( name, @"Global\OpenTPW.OneCopy." );
		Assert.AreEqual( OneCopy.Prefix.Length + 32, name.Length );
		Assert.IsFalse( name[OneCopy.Prefix.Length..].Contains( '/' ) || name[OneCopy.Prefix.Length..].Contains( '\\' ) );
	}

	[TestMethod]
	public void TakeHoldsTheFolderForTheRestOfTheRun()
	{
		var folder = Folder( "game" );

		try
		{
			Assert.IsTrue( OneCopy.Take( folder ) );

			// Held by the run, not by a handle the caller could drop: the next take is refused.
			GC.Collect();
			GC.WaitForPendingFinalizers();

			Assert.IsFalse( OneCopy.Take( folder ) );
		}
		finally
		{
			OneCopy.ReleaseForTest();
		}

		using var after = OneCopy.Open( folder, out _ );
		Assert.IsNotNull( after );
	}

	[TestMethod]
	public void TheRefusalNamesTheFolderAndKeepsNoLog()
	{
		var text = StartFailure.Compose( StartFailure.Head, OneCopy.Refusal( "/games/tpw" ), string.Empty );

		StringAssert.Contains( text, "Another copy of OpenTPW is already running on the game in /games/tpw." );
		StringAssert.EndsWith( text, "Log: none" );
	}
}
