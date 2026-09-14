using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The loading bar's learned step counts. These need no game files: what is being checked is that a
/// situation is told apart from its neighbours and that a measurement replaces the guess it started with.
/// </summary>
[TestClass]
public class LoadStepCountsTests
{
	[TestInitialize]
	public void Forget() => LoadStepCounts.Forget();

	[TestCleanup]
	public void ForgetAgain() => LoadStepCounts.Forget();

	/// <summary>
	/// The first build of a scene and a later one are different questions, because nothing releases what a
	/// scene loaded and the second build finds most of it already cached. This is the whole reason a single
	/// constant per scene could never be right.
	/// </summary>
	[TestMethod]
	public void BuildingASceneAgainAsksADifferentQuestionFromBuildingItFirst()
	{
		// The spaces go, because a key is one space-separated field of a line in opentpw.cfg.
		var first = LoadStepCounts.KeyFor( "the lobby" );
		Assert.AreEqual( "the-lobby.first", first );

		LoadStepCounts.Record( "the lobby", first, 829 );

		var again = LoadStepCounts.KeyFor( "the lobby" );
		Assert.AreEqual( "the-lobby.again", again, "the lobby has been built once, so the next one is a rebuild" );
		Assert.AreNotEqual( first, again );

		// And a scene nobody has built is still on its first.
		Assert.AreEqual( "jungle.first", LoadStepCounts.KeyFor( "jungle" ) );
	}

	/// <summary>
	/// A seed is only what a first-ever run uses. Once a situation has been measured the measurement wins,
	/// whatever the seed says - which is what stops the constants needing to be re-measured by hand.
	/// </summary>
	[TestMethod]
	public void AMeasurementReplacesTheSeedItStartedFrom()
	{
		var key = LoadStepCounts.KeyFor( "jungle" );

		Assert.AreEqual( 500, LoadStepCounts.Expect( key, seed: 500 ),
			"nothing has been measured yet, so the seed is all there is" );

		LoadStepCounts.Record( "jungle", key, 918 );

		Assert.AreEqual( 918, LoadStepCounts.Expect( key, seed: 500 ),
			"the seed is ignored once the real number is known" );

		// The four situations this park and the lobby make are each kept apart.
		LoadStepCounts.Record( "jungle", LoadStepCounts.KeyFor( "jungle" ), 758 );

		Assert.AreEqual( 918, LoadStepCounts.Expect( "jungle.first", seed: 0 ) );
		Assert.AreEqual( 758, LoadStepCounts.Expect( "jungle.again", seed: 0 ) );
	}

	/// <summary>
	/// The file is only rewritten when a count actually moved. These sit in the player's save folder, so a
	/// run that learns nothing new must leave it untouched.
	/// </summary>
	[TestMethod]
	public void OnlyALearnedCountAsksToBeWrittenDown()
	{
		Assert.IsFalse( LoadStepCounts.TakeChanged(), "nothing has happened yet" );

		var key = LoadStepCounts.KeyFor( "the lobby" );
		LoadStepCounts.Record( "the lobby", key, 829 );

		Assert.IsTrue( LoadStepCounts.TakeChanged(), "829 was not known before" );
		Assert.IsFalse( LoadStepCounts.TakeChanged(), "asking clears it" );

		// The same number again is not news.
		LoadStepCounts.Record( "the lobby", key, 829 );
		Assert.IsFalse( LoadStepCounts.TakeChanged() );

		// A different one is.
		LoadStepCounts.Record( "the lobby", key, 831 );
		Assert.IsTrue( LoadStepCounts.TakeChanged() );

		// What was read back from the file is not a change to write out again.
		LoadStepCounts.Restore( "jungle.first", 918 );
		Assert.IsFalse( LoadStepCounts.TakeChanged() );
		Assert.AreEqual( 918, LoadStepCounts.Expect( "jungle.first", seed: 0 ) );
	}
}
