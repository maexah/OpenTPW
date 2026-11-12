using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// A test run never reaches a player's install (AGENTS.md rule 12; Q986): on a PC with Sim Theme Park registered, the
/// lookup that mounts the game for the tests once went through the registry and ran the suite against the player's copy.
/// </summary>
[TestClass]
public class GameDataTests
{
	/// <remarks><b>Mutation:</b> <c>Locate</c> asking <c>GameDir.Find</c> with no folder given (the working directory, the build's folder and the registry's places) calls <c>find</c> here.</remarks>
	[TestMethod]
	[DataRow( null )]
	[DataRow( "" )]
	[DataRow( "   " )]
	public void NoFolderNamedMeansNoGameAndNothingIsLookedIn( string? named )
	{
		var asked = new List<string[]>();

		Assert.IsFalse( GameData.Locate( named, args => { asked.Add( args ); return true; } ) );
		Assert.AreEqual( 0, asked.Count, "something was asked for a game although no folder was named: " + string.Join( " | ", asked.Select( a => string.Join( " ", a ) ) ) );
	}

	/// <remarks><b>Mutation:</b> a named folder given to <c>find</c> as nothing but the variable's presence (<c>[]</c>), or with another argument beside it, fails the argument check.</remarks>
	[TestMethod]
	public void ANamedFolderIsAskedAsTheOnlyFolderAndTheAnswerIsKept()
	{
		string[]? asked = null;

		Assert.IsTrue( GameData.Locate( "/some/where", args => { asked = args; return true; } ) );
		CollectionAssert.AreEqual( new[] { "--game", "/some/where" }, asked );

		Assert.IsFalse( GameData.Locate( "/some/where", _ => false ), "a folder the game does not accept is no game" );
	}

	/// <summary>
	/// <b>No variable is the end of the test, with nothing asked</b> (Q987): <c>Required</c> goes through <c>Mount</c>, and a
	/// variable that is not set (or blank) is Inconclusive, the skipped state, without <c>GameDir.Find</c> being called.
	/// </summary>
	/// <remarks><b>Mutation:</b> <c>Mount</c> calling <c>find( [] )</c> before or instead of <c>Locate</c> (the working directory, the build's folder and the registry's places) calls <c>find</c> here; a <c>Mount</c> that carries on when <c>Locate</c> is false does not throw Inconclusive.</remarks>
	[TestMethod]
	[DataRow( null )]
	[DataRow( "" )]
	[DataRow( "   " )]
	public void NoVariableIsInconclusiveAndFindIsNeverCalled( string? named )
	{
		var asked = 0;

		Assert.ThrowsException<AssertInconclusiveException>( () => GameData.Mount( named, _ => { asked++; return true; } ) );
		Assert.AreEqual( 0, asked, "GameDir.Find was called although no folder was named" );
	}

	/// <summary>
	/// <b><c>Required</c>'s own line</b> (Q987): with the variable unset it is Inconclusive and does not call <c>GameDir.Find</c> at all (any call clears and refills
	/// <c>Reasons</c>); the cache and the variable are put back as they were.
	/// </summary>
	/// <remarks><b>Mutation:</b> <c>GameDir.Find( [] )</c> put back into <c>Required</c> (the working directory, the build's folder and the registry's places) empties <c>Reasons</c>.</remarks>
	[TestMethod]
	public void RequiredWithNoVariableIsInconclusiveAndLeavesGameDirAlone()
	{
		var field = typeof( GameData ).GetField( "_data", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static )!;
		var cached = field.GetValue( null );
		var old = System.Environment.GetEnvironmentVariable( GameData.GamePathVariable );
		var folder = System.IO.Directory.CreateTempSubdirectory( "tpw-gamedata-req" ).FullName;

		try
		{
			// Plant a reason: a named folder that is no game. Only a later call of Find would clear them.
			Assert.IsFalse( GameDir.Find( ["--game", System.IO.Path.Join( folder, "not-the-game" )] ) );
			var planted = GameDir.Reasons.ToList();
			Assert.IsTrue( planted.Count > 0 );

			field.SetValue( null, null );
			System.Environment.SetEnvironmentVariable( GameData.GamePathVariable, null );

			Assert.ThrowsException<AssertInconclusiveException>( () => GameData.Required() );
			CollectionAssert.AreEqual( planted, GameDir.Reasons.ToList(), "GameDir.Find was called by Required" );
		}
		finally
		{
			System.Environment.SetEnvironmentVariable( GameData.GamePathVariable, old );
			field.SetValue( null, cached );
			System.IO.Directory.Delete( folder, true );
		}
	}

	/// <remarks>A folder named that the game does not accept is the same end, after one ask with that folder alone.</remarks>
	[TestMethod]
	public void ANamedFolderThatIsNotTheGameIsInconclusiveAfterOneAsk()
	{
		var asked = new List<string[]>();

		Assert.ThrowsException<AssertInconclusiveException>( () => GameData.Mount( "/some/where", args => { asked.Add( args ); return false; } ) );
		Assert.AreEqual( 1, asked.Count );
		CollectionAssert.AreEqual( new[] { "--game", "/some/where" }, asked[0] );
	}

	/// <remarks>The folder must be named as <c>--game</c>: that is the one source <c>GameDir.Find</c> never falls through to the places a player's install can be.</remarks>
	[TestMethod]
	public void ANamedFolderThatIsNotTheGameDoesNotFallThroughToAnotherPlace()
	{
		var folder = System.IO.Directory.CreateTempSubdirectory( "tpw-gamedata" ).FullName;

		try
		{
			// The registered folder and the box's default are a folder that does hold a game; the named one is not, and nothing falls through to them.
			var game = System.IO.Path.Join( folder, "installed" );
			System.IO.Directory.CreateDirectory( System.IO.Path.Join( game, "data", "levels" ) );

			var old = System.Environment.GetEnvironmentVariable( GameData.GamePathVariable );
			System.Environment.SetEnvironmentVariable( GameData.GamePathVariable, null );

			try
			{
				// A folder that is found sets GameDir.Root for every test after it, so only the refusal is asked here.
				Assert.IsFalse( GameData.Locate( System.IO.Path.Join( folder, "not-the-game" ), args => GameDir.Find( args, [game], game ) ) );
			}
			finally { System.Environment.SetEnvironmentVariable( GameData.GamePathVariable, old ); }
		}
		finally { System.IO.Directory.Delete( folder, true ); }
	}

	/// <summary>
	/// A bare <c>--game</c>, the last argument, names no folder: it is refused with its own reason for Q978's box, and
	/// nothing else is looked in (not the working directory, not the build's folder).
	/// </summary>
	[TestMethod]
	[DataRow( "--game" )]
	[DataRow( "--game=" )]
	public void ABareGameArgumentIsRefusedWithItsOwnReason( string bare )
	{
		var old = System.Environment.GetEnvironmentVariable( GameData.GamePathVariable );
		System.Environment.SetEnvironmentVariable( GameData.GamePathVariable, null );

		try
		{
			Assert.IsFalse( GameDir.Find( ["--fullscreen", bare], [], "/no/such/box/folder" ) );
			Assert.AreEqual( "Game files: --game was given with no folder after it.", GameDir.Reasons.FirstOrDefault() );
			Assert.AreEqual( 2, GameDir.Reasons.Count, string.Join( "\n", GameDir.Reasons ) );
		}
		finally { System.Environment.SetEnvironmentVariable( GameData.GamePathVariable, old ); }
	}
}
