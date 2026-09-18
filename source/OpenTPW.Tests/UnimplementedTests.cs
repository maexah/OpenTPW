using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The reporter that says so when the program reaches something it has not built.
///
/// <para>
/// <b>This is tested because a reporter that quietly stops reporting is the very fault it exists to
/// catch.</b> The tree was full of counters that recorded their own gaps and were never read - two of
/// them with doc comments saying the count existed so the gap would be "visible" - and the only place
/// any of them became text was a test. A de-duplicating reporter has an obvious way to fail the same
/// way: announce nothing, or announce once and then forget it ever happened.
/// </para>
/// </summary>
[TestClass]
public class UnimplementedTests
{
	[TestInitialize]
	public void Forget() => Unimplemented.Forget();

	[TestCleanup]
	public void ForgetAgain() => Unimplemented.Forget();

	/// <summary>What is reached is remembered, and named well enough to act on.</summary>
	[TestMethod]
	public void WhatWasReachedIsRemembered()
	{
		Assert.IsFalse( Unimplemented.Any, "nothing has been reported yet" );

		Unimplemented.Report( "SETOBJPARAM" );

		Assert.IsTrue( Unimplemented.Any );
		CollectionAssert.AreEqual( new[] { "SETOBJPARAM" },
			Unimplemented.Summary.Select( entry => entry.What ).ToArray() );
		Assert.AreEqual( 1, Unimplemented.Summary[0].Times );
	}

	/// <summary>
	/// <b>Repeats are counted rather than dropped, and that is the whole design.</b> A ride script takes
	/// a turn eight times a second, so the console gets one line; but "it happened 400 times" and "it
	/// happened once" want different reactions, and a reporter that forgot the difference would hide
	/// which gap actually matters.
	/// </summary>
	[TestMethod]
	public void RepeatsAreCountedNotDropped()
	{
		for ( var i = 0; i < 5; ++i )
			Unimplemented.Report( "COAST" );

		Assert.AreEqual( 1, Unimplemented.Summary.Count, "one distinct gap, however often it is reached" );
		Assert.AreEqual( 5, Unimplemented.Summary[0].Times, "and it is known to have happened five times" );
	}

	/// <summary>Different gaps stay apart, most-reached first, so the worst one leads.</summary>
	[TestMethod]
	public void DifferentGapsAreKeptApartAndOrderedByHowOftenTheyBite()
	{
		Unimplemented.Report( "DIPMUSIC" );

		for ( var i = 0; i < 3; ++i )
			Unimplemented.Report( "SETOBJPARAM" );

		var summary = Unimplemented.Summary;

		Assert.AreEqual( 2, summary.Count );
		Assert.AreEqual( "SETOBJPARAM", summary[0].What, "the one reached most often comes first" );
		Assert.AreEqual( 3, summary[0].Times );
		Assert.AreEqual( "DIPMUSIC", summary[1].What );
		Assert.AreEqual( 1, summary[1].Times );
	}

	/// <summary>
	/// Nothing is reported for a blank name. A report that says only "unimplemented" tells nobody what to
	/// build, and would collapse every distinct gap into one line.
	/// </summary>
	[TestMethod]
	public void ANamelessReportIsRefused()
	{
		Unimplemented.Report( "" );
		Unimplemented.Report( "   " );
		Unimplemented.Report( null! );

		Assert.IsFalse( Unimplemented.Any, "a gap with no name is not a report" );
	}
}
