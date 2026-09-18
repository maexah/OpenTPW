using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Putting a guest on a ride and taking them off again - the handshake between the engine and a ride's
/// own script.
///
/// <para>
/// <b>The two slots run in OPPOSITE directions, and that is what these tests are shaped around.</b>
/// <c>VAR_LETMEON</c> is an inbox: the engine writes a guest into it and the script zeroes it once it has
/// taken them, so an EMPTY slot is how the script says it is ready for another. <c>VAR_LETMEOFF</c> is an
/// outbox: the script's <c>UNBOUNCE</c> writes whoever came off into it, and the engine clears it once
/// they are on their way.
/// </para>
/// <para>
/// So the assertions that carry this file are about refusals and clearing, not about the happy path: a
/// build that wrote into a full inbox would drop whoever was already there, and one that never cleared
/// the outbox would let the same guest off for ever.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkRideAdmitTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const int Ride = 13;

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>The Belly Bounce's own script, which declares both slots by name.</summary>
	private RideScript Script()
	{
		using var stream = data.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );

		return new RideScript( new RideScriptFile( stream ) );
	}

	private static Peep Guest( int thingId, PeepState state )
		=> new( thingId, new ParkWorld.GuestState(
			State: (int)state, SavedState: (int)PeepState.Deciding, PersonType: 0, Cash: 300,
			ExitLevel: 100, Happiness: 50f, Thirst: 10f, Hunger: 10f, Toilet: 10f, Vomit: 0f,
			Litter: 0f, MajorDest: 0, QueuePos: 0, PrankeryIndex: 0 ), StandingStill );

	private static ParkWorld.NavigatorState StandingStill => new(
		X: 0, Y: 0, VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
		Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
		MaxForce: 0, MaxSpeed: 0, NavMode: 0, CantReachDest: 0, PathFinished: false,
		PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
		BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

	private ParkWorld.CatalogueObject TheRide() => Park().Objects.Single( o => o.ThingId == Ride );

	/// <summary>The script really does declare both slots, or none of this means anything.</summary>
	[TestMethod]
	public void TheRideScriptDeclaresBothSlots()
	{
		var script = Script();

		Assert.IsTrue( script.IndexOf( ParkRideOperation.AdmitVariable ) >= 0, "VAR_LETMEON" );
		Assert.IsTrue( script.IndexOf( ParkRideOperation.DismissVariable ) >= 0, "VAR_LETMEOFF" );
	}

	/// <summary>A nominated guest is handed to the script, and the nomination is let go of.</summary>
	[TestMethod]
	public void ANominatedGuestIsHandedToTheScript()
	{
		var park = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
		var script = Script();
		var ride = TheRide();

		park.NominateForLoading( Ride, 7 );

		var operation = new ParkRideOperation( park, new Dictionary<int, Peep>() );

		Assert.IsTrue( operation.AdmitPerson( script, ride, 7 ), "handed over" );
		Assert.AreEqual( 7, script[ParkRideOperation.AdmitVariable], "the script has them" );
		Assert.AreEqual( 0, park.PersonBeingLoaded( Ride ), "and the nomination is let go of" );
	}

	/// <summary>
	/// <b>The load-bearing refusal.</b> A full slot is not written over - doing so would drop whoever the
	/// script had not taken yet.
	/// </summary>
	[TestMethod]
	public void AFullSlotIsNotWrittenOverAndTheNominationSurvives()
	{
		var park = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
		var script = Script();
		var ride = TheRide();

		park.NominateForLoading( Ride, 7 );
		Assert.IsTrue( new ParkRideOperation( park, new Dictionary<int, Peep>() )
			.AdmitPerson( script, ride, 7 ) );

		// A second guest, while the script still holds the first.
		park.NominateForLoading( Ride, 8 );

		Assert.IsFalse( new ParkRideOperation( park, new Dictionary<int, Peep>() )
			.AdmitPerson( script, ride, 8 ), "refused while the slot is full" );

		Assert.AreEqual( 7, script[ParkRideOperation.AdmitVariable], "the first guest is still there" );
		Assert.AreEqual( 8, park.PersonBeingLoaded( Ride ),
			"and the ride still holds its nominee rather than losing them" );

		// Once the script consumes and clears it - COPY VAR_LETMEON, 0 - the second may go in.
		script.Set( ParkRideOperation.AdmitVariable, 0 );

		Assert.IsTrue( new ParkRideOperation( park, new Dictionary<int, Peep>() )
			.AdmitPerson( script, ride, 8 ), "an empty slot takes the next one" );
	}

	/// <summary>Somebody who is not the nominee is refused - the original's "admitting wrong person".</summary>
	[TestMethod]
	public void TheWrongPersonIsRefused()
	{
		var park = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
		var script = Script();

		park.NominateForLoading( Ride, 7 );

		Assert.IsFalse( new ParkRideOperation( park, new Dictionary<int, Peep>() )
			.AdmitPerson( script, TheRide(), 9 ), "9 was never nominated" );

		Assert.AreEqual( 0, script[ParkRideOperation.AdmitVariable], "nothing was handed over" );
		Assert.AreEqual( 7, park.PersonBeingLoaded( Ride ), "and the real nominee stands" );
	}

	/// <summary>A ride out of service admits nobody, on either of the two states that take it out.</summary>
	[TestMethod]
	public void ARideOutOfServiceAdmitsNobody()
	{
		var ride = TheRide();

		foreach ( var state in new[] { ParkRideChoice.StateRefusedOne, ParkRideChoice.StateRefusedFour } )
		{
			var park = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
			var script = Script();

			park.NominateForLoading( Ride, 7 );

			Assert.IsFalse( new ParkRideOperation( park, new Dictionary<int, Peep>() )
				.AdmitPerson( script, ride with { State = state }, 7 ), $"state {state}" );

			Assert.AreEqual( 0, script[ParkRideOperation.AdmitVariable], $"state {state}: nothing handed" );
		}

		// Anti-vacuity: the state this park's ride actually carries is NOT a refusal, so the test above
		// is not just restating "any state refuses".
		var open = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
		open.NominateForLoading( Ride, 7 );

		Assert.IsTrue( new ParkRideOperation( open, new Dictionary<int, Peep>() )
			.AdmitPerson( Script(), ride, 7 ), $"the shipped ride's own state {ride.State} admits" );
	}

	/// <summary>
	/// The admission is finished once the script has taken the rider - the head leaves the queue, keeps
	/// no links, and is riding.
	/// </summary>
	[TestMethod]
	public void TheHeadLeavesTheQueueOnceTheScriptHasTakenThem()
	{
		var park = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
		var script = Script();

		park.JoinQueue( Ride, 7 );
		park.JoinQueue( Ride, 8 );

		var guests = new Dictionary<int, Peep>
		{
			[7] = Guest( 7, PeepState.EnteringRide ),
			[8] = Guest( 8, PeepState.InQueue )
		};

		var operation = new ParkRideOperation( park, guests );

		Assert.IsTrue( operation.CompleteAdmission( script, Ride, tick: 5, new Random( 1 ) ), "admitted" );

		Assert.AreEqual( PeepState.Riding, guests[7].State, "they are on the ride" );
		Assert.AreEqual( 8, park.FirstInQueue( Ride ), "the next one is at the front" );
		Assert.AreEqual( 1, park.QueueLength( Ride ) );

		// The original asserts exactly this afterwards - "Person not correctly removed from queue".
		Assert.AreEqual( 0, park.NextInQueue( 7 ), "no links left" );
		Assert.AreEqual( 0, park.PreviousInQueue( 7 ) );
	}

	/// <summary>
	/// <b>The discriminating pair.</b> A head who is not ready, or a slot the script has not yet cleared,
	/// both leave the queue alone.
	/// </summary>
	[TestMethod]
	public void NobodyIsAdmittedBeforeTheyAreReadyOrWhileTheSlotIsFull()
	{
		var park = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
		var script = Script();

		park.JoinQueue( Ride, 7 );

		// Queueing, but not yet at the point of entering.
		var waiting = new Dictionary<int, Peep> { [7] = Guest( 7, PeepState.InQueue ) };

		Assert.IsFalse( new ParkRideOperation( park, waiting )
			.CompleteAdmission( script, Ride, 5, new Random( 1 ) ), "not in EnteringRide" );

		Assert.AreEqual( 1, park.QueueLength( Ride ), "so the queue is untouched" );

		// Ready - but the script still holds them, so it has not taken them up yet.
		var ready = new Dictionary<int, Peep> { [7] = Guest( 7, PeepState.EnteringRide ) };
		script.Set( ParkRideOperation.AdmitVariable, 7 );

		Assert.IsFalse( new ParkRideOperation( park, ready )
			.CompleteAdmission( script, Ride, 5, new Random( 1 ) ), "the slot still names them" );

		Assert.AreEqual( 1, park.QueueLength( Ride ), "still untouched" );

		// And once the script clears it, the same call succeeds - which is what says the slot is the gate.
		script.Set( ParkRideOperation.AdmitVariable, 0 );

		Assert.IsTrue( new ParkRideOperation( park, ready )
			.CompleteAdmission( script, Ride, 5, new Random( 1 ) ), "cleared, so now they go" );
	}

	/// <summary>Whoever the script reports is let off, and the slot is cleared behind them.</summary>
	[TestMethod]
	public void WhoeverTheScriptReportsIsLetOffAndTheSlotIsCleared()
	{
		var park = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
		var script = Script();

		var guests = new Dictionary<int, Peep> { [7] = Guest( 7, PeepState.Riding ) };
		script.Set( ParkRideOperation.DismissVariable, 7 );

		Assert.IsTrue( new ParkRideOperation( park, guests )
			.Dismiss( script, TheRide(), tick: 9, new Random( 1 ) ), "let off" );

		Assert.AreEqual( PeepState.OnRide, guests[7].State, "they are leaving the ride" );
		Assert.AreEqual( 0, script[ParkRideOperation.DismissVariable],
			"and the slot is cleared, or the same guest would be let off for ever" );
	}

	/// <summary>An empty outbox, or somebody who is not riding, dismisses nobody.</summary>
	[TestMethod]
	public void AnEmptyOutboxOrSomebodyNotRidingDismissesNobody()
	{
		var park = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
		var script = Script();
		var guests = new Dictionary<int, Peep> { [7] = Guest( 7, PeepState.InQueue ) };

		Assert.IsFalse( new ParkRideOperation( park, guests )
			.Dismiss( script, TheRide(), 9, new Random( 1 ) ), "nobody reported" );

		script.Set( ParkRideOperation.DismissVariable, 7 );

		Assert.IsFalse( new ParkRideOperation( park, guests )
			.Dismiss( script, TheRide(), 9, new Random( 1 ) ), "reported, but they are not on the ride" );

		Assert.AreEqual( 7, script[ParkRideOperation.DismissVariable],
			"and the slot is left alone, so the fault stays visible" );
	}
}
