using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Moving a thing standing in a park: the pickup the object window's move runs, and the console's move, which
/// is that pickup and one click at a cell. These read the shipped Lost Kingdom and are skipped where there is
/// no installation - see <see cref="GameData"/>.
///
/// <para>
/// <b>Each calls <see cref="ParkBuilding.Move(ParkState, ParkWorld, ParkItemCatalogue, ParkObjects?, ParkRides?,
/// int, int, int, int?)"/></b>, the code its mutation changes. With no objects to stand a model in, a
/// put-down that passes every test answers that it would not load, so every answer here comes from the real
/// refusals. The hand is static, and each test empties it after itself.
/// </para>
/// </summary>
[TestClass]
public class ParkMoveTests
{
	private BaseFileSystem data = null!;

	private readonly List<Entity> made = [];

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	[TestCleanup]
	public void PutAwayWhatWasMade()
	{
		ParkBuilding.Drop();
		ParkBuildMode.Forget();

		foreach ( var entity in made )
			entity.Delete();

		made.Clear();
		Entity.ApplyDeletions();
	}

	private const string Theme = "jungle";
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	/// <summary>The shipped park's Belly Bounce, anchored (51,23), covering (51,23)..(53,26).</summary>
	private const int BellyBounceThing = 13;

	/// <summary>The shipped park's gates - UI type 4, standing on no cell of their own.</summary>
	private const int GatesThing = 11;

	/// <summary>The Staff Room's item. The shipped park has one, anchored (58,16) and turned 90.</summary>
	private const int StaffRoomItem = 1411;

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );

		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private ParkRides Bind( ParkWorld world, ParkItemCatalogue catalogue )
	{
		var rides = new ParkRides( Theme, world, catalogue, data );

		made.Add( rides );

		return rides;
	}

	/// <summary>
	/// <b>A cell that refuses leaves the thing in the hand</b>, sold where it stood, as a click on a red cell
	/// does in the original: the move tool stays installed and the next click tries again. The Staff Room's
	/// anchor is built on, so the Belly Bounce cannot go there. <b>Picking up puts away whatever tool was
	/// armed</b>, as installing the move tool does.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> selling and then buying at the cell, rather than selling and taking the item into the
	/// hand, leaves the hand empty; refunding nothing at the sale leaves the balance 500 short; not putting
	/// the tool away leaves the path tool armed.
	/// </remarks>
	[TestMethod]
	public void AMoveOntoACellThatRefusesLeavesTheThingInTheHand()
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );
		var state = new ParkState( world );
		var rides = Bind( world, catalogue );

		Assert.IsTrue( state.TryObject( BellyBounceThing, out var bounce ) );
		Assert.IsTrue( catalogue.TryGet( bounce.CatalogueId, out var item ) );
		Assert.AreEqual( 500, item.BuildPrice, "the Belly Bounce costs 500" );

		var before = state.Balance;
		ParkBuildMode.Arm( ParkBuildMode.Path );

		var reply = ParkBuilding.Move( state, world, catalogue, null, rides, BellyBounceThing, 58, 16 );

		Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current, "picking up put the path tool away" );
		StringAssert.Contains( reply, "will not fit at (58,16)" );
		Assert.AreEqual( bounce.CatalogueId, ParkBuilding.Carrying, $"the Belly Bounce is still in the hand: {reply}" );
		Assert.IsFalse( state.TryObject( BellyBounceThing, out _ ), "it was sold where it stood" );
		Assert.AreEqual( 0, rides.ScriptFor( BellyBounceThing ), "and its script went with it" );

		// The ride's 500 and its queue's 225 (four cells, less the one the placer laid for nothing), banked at
		// pickup - and nothing charged, which a sale alone in a second copy of the park agrees with.
		Assert.AreEqual( before + 500 + 225, state.Balance, "the refund is banked at pickup and nothing is charged" );

		var sold = new ParkState( world );
		ParkBuilding.Sell( sold, world, catalogue, null, null, BellyBounceThing );

		Assert.AreEqual( sold.Balance, state.Balance, "a move banks what a sale banks" );
	}

	/// <summary>
	/// <b>The hand holds a moved thing facing the way it stood</b> - the pickup sets the carried rotation from
	/// the thing's own (<c>FUN_0052f1b0( [thing + 0x10], 1 )</c>) - <b>and the put-down builds it that way.</b>
	/// The Staff Room stands turned 90. Put down on the Belly Bounce's anchor, turned 90 its two-by-two
	/// footprint starts on row 22, and the refusal names (51,22), one of the Belly Bounce's queue cells;
	/// unturned it would start on row 23 and name the anchor itself, which is built on.
	/// <para>
	/// <b>An angle given with the put-down wins over the hand's</b>: moved again from a fresh copy of the park
	/// and put down at nought, it names (51,23), and the hand still holds it turned 90.
	/// </para>
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> holding the item at nought fails the angle; putting it down at nought when no angle is
	/// given, rather than at the hand's, names (51,23) and fails the answer; putting it down at the hand's
	/// whatever angle is given names (51,22) in the second half.
	/// </remarks>
	[TestMethod]
	public void AMovedThingIsHeldAndPutDownFacingTheWayItStood()
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );
		var state = new ParkState( world );

		var staffRoom = state.Objects.Single( placed => placed.CatalogueId == StaffRoomItem );

		Assert.AreEqual( 90, staffRoom.Angle, "the shipped Staff Room stands turned 90" );

		var reply = ParkBuilding.Move( state, world, catalogue, null, null, staffRoom.ThingId, 51, 23 );

		Assert.AreEqual( StaffRoomItem, ParkBuilding.Carrying, $"the Staff Room is in the hand: {reply}" );
		Assert.AreEqual( 90, ParkBuilding.CarryingAngle, "held the way it stood" );
		StringAssert.Contains( reply, "will not fit at (51,23) - (51,22)" );

		var again = new ParkState( world );
		var unturned = ParkBuilding.Move( again, world, catalogue, null, null, staffRoom.ThingId, 51, 23, 0 );

		StringAssert.Contains( unturned, "will not fit at (51,23) - (51,23)" );
		Assert.AreEqual( 90, ParkBuilding.CarryingAngle, "the hand still holds it the way it stood" );
	}

	/// <summary>
	/// <b>What nothing can sell is never picked up.</b> The original carries the item whether or not its
	/// demolish found the thing; here the hand stays empty unless the sale went through, so the gates, which
	/// no demolish reaches, stay where they are and are not also in the hand.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> taking the item into the hand without asking whether the sale happened fills the hand.
	/// </remarks>
	[TestMethod]
	public void WhatNothingCanSellIsNeverPickedUp()
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );
		var state = new ParkState( world );

		var reply = ParkBuilding.Move( state, world, catalogue, null, null, GatesThing, 40, 40 );

		Assert.AreEqual( 0, ParkBuilding.Carrying, $"the hand stays empty: {reply}" );
		Assert.IsTrue( state.TryObject( GatesThing, out _ ), "and the gates still stand" );
		StringAssert.Contains( reply, "which nothing can demolish" );
	}

	/// <summary>
	/// <b>Picking up asks nothing of the balance.</b> The original's pickup has no affordability test; a
	/// put-down the park cannot afford is refused like a taken cell, and the hand keeps the thing.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> taking the item into the hand through <see cref="ParkBuilding.Carry"/>, which refuses
	/// what the park cannot afford, leaves the hand empty.
	/// </remarks>
	[TestMethod]
	public void PickingUpAsksNothingOfTheBalance()
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );
		var state = new ParkState( world );

		Assert.IsTrue( state.TryObject( BellyBounceThing, out var bounce ) );
		Assert.IsTrue( catalogue.TryGet( bounce.CatalogueId, out var item ) );

		// Far enough below nought that the refund does not bring it back up to the price.
		state.Spend( state.Balance + 10 * item.BuildPrice );

		// Its own spot, which the sale has just cleared, so only the money can refuse it.
		var reply = ParkBuilding.Move( state, world, catalogue, null, null, BellyBounceThing, bounce.CellX, bounce.CellY,
			bounce.Angle );

		Assert.AreEqual( bounce.CatalogueId, ParkBuilding.Carrying, $"the Belly Bounce is in the hand: {reply}" );
		StringAssert.Contains( reply, $"costs {item.BuildPrice} and the park has" );
	}
}
