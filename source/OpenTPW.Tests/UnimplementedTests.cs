using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Threading.Tasks;

namespace OpenTPW.Tests;

/// <summary>
/// The reporter that says so when the program reaches something it has not built.
///
/// <para>
/// <b>This is tested because a reporter that quietly stops reporting is the very fault it exists to
/// catch.</b> A counter that records its own gaps and is never read shows nobody anything, and a
/// de-duplicating reporter has an obvious way to fail the same
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
	/// a turn about four times a second, so the console gets one line; but "it happened 400 times" and "it
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

	/// <summary>
	/// <b>A repeat takes no lock, so the count must still be exact.</b> The draw reports from every person every frame,
	/// and the render and the simulation do not share a thread: a repeat that lost an increment would understate the
	/// gap that bites most, which is the one the census exists to find. Several threads report the same gaps at once,
	/// one of them for the first time, and every report is accounted for.
	/// </summary>
	[TestMethod]
	public void RepeatsFromManyThreadsAreAllCounted()
	{
		const int threads = 8;
		const int each = 50_000;

		Parallel.For( 0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ =>
		{
			for ( var i = 0; i < each; ++i )
			{
				Unimplemented.Report( "RIDER_BOUNCE" );
				Unimplemented.Report( i % 2 == 0 ? "EVEN" : "ODD" );
			}
		} );

		var summary = Unimplemented.Summary.ToDictionary( entry => entry.What, entry => entry.Times );

		Assert.AreEqual( 3, summary.Count, "three distinct gaps, however many threads reached them" );
		Assert.AreEqual( threads * each, summary["RIDER_BOUNCE"] );
		Assert.AreEqual( threads * each / 2, summary["EVEN"] );
		Assert.AreEqual( threads * each / 2, summary["ODD"] );
	}

	/// <summary>Forgetting empties the reporter, and a gap reached again afterwards counts from one.</summary>
	[TestMethod]
	public void ForgettingStartsTheCountAgain()
	{
		Unimplemented.Report( "COAST" );
		Unimplemented.Report( "COAST" );
		Unimplemented.Forget();

		Assert.IsFalse( Unimplemented.Any );

		Unimplemented.Report( "COAST" );

		Assert.AreEqual( 1, Unimplemented.Summary[0].Times );
	}
}
