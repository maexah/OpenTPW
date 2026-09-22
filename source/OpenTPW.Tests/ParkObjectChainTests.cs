using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The park's object chain - <see cref="ParkState.ObjectsInChainOrder"/>, which is the original's
/// <c>mFirstObject</c> walk and decides what a guest may be offered and in what order.
///
/// <para>
/// <b>These need no game files.</b> The chain the shipped park carries is the one the FILE holds, and the
/// thing under test is what happens to it when something is bought or sold - which no saved park can
/// exercise, because nothing has ever been built in one. A park built here from the two-fact constructor
/// starts with an empty list and an empty chain, which is the only state that can reach these branches.
/// </para>
/// <para>
/// <b>What these pin, and what they deliberately do not.</b> They pin the chain: head insertion, the three
/// unlink cases, and that a walk reaches everything exactly once and stops. They do <i>not</i> pin the
/// wiring - that the ride sweep, the seat lookup and the two censuses read <see cref="ParkState"/> rather
/// than the save's list. That wiring was predicted to survive the whole suite before it was checked, and
/// it does: nothing here drives a bought object through a running park, so it rests on the game run and on
/// the census taken beside the screenshot, exactly as the audio and texture items before it recorded of
/// theirs (<c>docs/VERIFYING.md</c> rule 48).
/// </para>
/// </summary>
[TestClass]
public class ParkObjectChainTests
{
	/// <summary>A park with nothing in it - the seam that needs no save behind it.</summary>
	private static ParkState Park() => new( parkIsClosed: false, visitorsToDate: 0 );

	/// <summary>An object built from nothing, placed so that it does not read as the unplaced sentinel.</summary>
	private static ParkWorld.CatalogueObject Thing( int thingId )
		=> new( ThingId: thingId, CatalogueId: 1100, RawX: 10 << 8, RawY: 10 << 8, Angle: 0 );

	private static int[] Walk( ParkState park )
		=> park.ObjectsInChainOrder().Select( placed => placed.ThingId ).ToArray();

	/// <summary>
	/// The whole of the bug this file was written for: a bought thing has to be in the list the simulation
	/// sweeps <b>and</b> in the chain a guest's choice walks. Being in one and not the other is what makes
	/// a ride that stands, animates, and is never used.
	/// </summary>
	[TestMethod]
	public void SomethingBoughtJoinsBothTheListAndTheChain()
	{
		var park = Park();

		park.AddObject( Thing( 40 ) );

		CollectionAssert.AreEqual( new[] { 40 }, park.Objects.Select( o => o.ThingId ).ToArray(),
			"it should stand in the park" );
		CollectionAssert.AreEqual( new[] { 40 }, Walk( park ),
			"and it should be reachable from the chain, or nobody can ever be offered it" );
	}

	/// <summary>
	/// The newest thing is considered FIRST. <c>FUN_00519d80</c> links at the head, and that is observable
	/// rather than cosmetic: a tie between two equally good candidates goes to whichever the walk reaches
	/// later, so walk order decides which ride a guest sets off for.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> appending instead of prepending reverses this and fails here.
	/// </remarks>
	[TestMethod]
	public void TheNewestThingIsWalkedFirst()
	{
		var park = Park();

		park.AddObject( Thing( 40 ) );
		park.AddObject( Thing( 41 ) );
		park.AddObject( Thing( 42 ) );

		CollectionAssert.AreEqual( new[] { 42, 41, 40 }, Walk( park ),
			"newest first, which is the order the original's head insertion produces" );
	}

	/// <summary>
	/// Selling the head moves the head on, which is <c>FUN_00519dc0</c>'s first arm.
	/// </summary>
	[TestMethod]
	public void SellingTheHeadLeavesTheRestReachable()
	{
		var park = Park();

		park.AddObject( Thing( 40 ) );
		park.AddObject( Thing( 41 ) );
		park.AddObject( Thing( 42 ) );

		Assert.IsTrue( park.RemoveObject( 42 ), "the head was there to sell" );
		CollectionAssert.AreEqual( new[] { 41, 40 }, Walk( park ), "and the rest still walk" );
	}

	/// <summary>
	/// Selling from the middle patches the predecessor's link, which is <c>FUN_00519dc0</c>'s second arm
	/// and the one that silently loses the tail if it is missed.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> dropping the unlink entirely leaves 41 still linked, so the walk keeps returning it
	/// after it has been sold, and this fails.
	/// </remarks>
	[TestMethod]
	public void SellingFromTheMiddleLeavesTheRestReachable()
	{
		var park = Park();

		park.AddObject( Thing( 40 ) );
		park.AddObject( Thing( 41 ) );
		park.AddObject( Thing( 42 ) );

		Assert.IsTrue( park.RemoveObject( 41 ), "the middle one was there to sell" );
		CollectionAssert.AreEqual( new[] { 42, 40 }, Walk( park ),
			"the one behind it must still be reached, not stranded with it" );
	}

	/// <summary>Selling the last one in the walk ends the chain where the one before it now sits.</summary>
	[TestMethod]
	public void SellingTheTailLeavesTheRestReachable()
	{
		var park = Park();

		park.AddObject( Thing( 40 ) );
		park.AddObject( Thing( 41 ) );
		park.AddObject( Thing( 42 ) );

		Assert.IsTrue( park.RemoveObject( 40 ), "the tail was there to sell" );
		CollectionAssert.AreEqual( new[] { 42, 41 }, Walk( park ), "and the chain ends cleanly" );
	}

	/// <summary>
	/// The same invariant <c>ParkDecodedRecordTests</c> asserts of the file's own chain, asserted here of a
	/// chain that has been built and sold from: it reaches every object exactly once and stops.
	/// </summary>
	[TestMethod]
	public void TheChainReachesEveryObjectExactlyOnceAndStops()
	{
		var park = Park();

		foreach ( var id in new[] { 40, 41, 42, 43, 44 } )
			park.AddObject( Thing( id ) );

		park.RemoveObject( 42 );
		park.AddObject( Thing( 45 ) );
		park.RemoveObject( 45 );
		park.RemoveObject( 40 );

		var walked = Walk( park );

		CollectionAssert.AreEquivalent( park.Objects.Select( o => o.ThingId ).ToArray(), walked,
			"the chain should reach exactly what stands in the park" );
		Assert.AreEqual( walked.Distinct().Count(), walked.Length, "and reach none of them twice" );
	}

	/// <summary>
	/// Selling something that was never there changes nothing - the guard that stops a miss from walking
	/// the whole chain looking for a link that does not exist.
	/// </summary>
	[TestMethod]
	public void SellingSomethingThatWasNeverThereLeavesTheChainAlone()
	{
		var park = Park();

		park.AddObject( Thing( 40 ) );
		park.AddObject( Thing( 41 ) );

		Assert.IsFalse( park.RemoveObject( 99 ), "nothing in the park is thing 99" );
		CollectionAssert.AreEqual( new[] { 41, 40 }, Walk( park ), "and the chain is untouched" );
	}
}
