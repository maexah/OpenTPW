using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A ride picking the guest at the head of its queue and inviting them aboard -
/// <see cref="ParkRideOperation.Invite"/>, the original's <c>FUN_004e1220</c>.
///
/// <para>
/// <b>This is the head of the boarding chain, and the reason it matters is that without it the rest is
/// inert.</b> The guest's own <see cref="PeepState.InQueue"/> turn only sets off to board when it finds
/// both halves of what this does: their <c>mBeenAdmitted</c> flag AND the ride's nomination naming them.
/// Nothing else in the tree sets either, so a boarding arm without an invite is a feature that can never
/// fire - which is the trap this project keeps being caught by.
/// </para>
/// <para>
/// <b>The refusals carry these tests.</b> A ride that invited while full, mid-run, or with somebody
/// already called forward would board the wrong guest or two at once, and would look perfectly healthy
/// doing it. Every variable starts at nought in a freshly loaded script, so the capacity gate is set up
/// explicitly rather than relied on.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkRideInviteTests
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

	private ParkWorld.CatalogueObject TheRide() => Park().Objects.Single( o => o.ThingId == Ride );

	/// <summary>The Belly Bounce's own script, with room aboard and not yet running.</summary>
	private RideScript ReadyScript()
	{
		using var stream = data.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );
		var script = new RideScript( new RideScriptFile( stream ) );

		script.Set( ParkRideOperation.CapacityVariable, 5 );

		return script;
	}

	private static Peep Guest( int thingId, PeepState state, int queuePos = 0 )
		=> new( thingId, new ParkWorld.GuestState(
			State: (int)state, SavedState: (int)PeepState.Deciding, PersonType: 0, Cash: 300,
			ExitLevel: 100, Happiness: 50f, Thirst: 10f, Hunger: 10f, Toilet: 10f, Vomit: 0f,
			Litter: 0f, MajorDest: Ride, QueuePos: queuePos, PrankeryIndex: 0 ), StandingStill );

	private static ParkWorld.NavigatorState StandingStill => new(
		X: 0, Y: 0, VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
		Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
		MaxForce: 0, MaxSpeed: 0, NavMode: 0, CantReachDest: 0, PathFinished: false,
		PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
		BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

	/// <summary>A park with one guest queueing at the front, ready to be invited.</summary>
	private static (ParkState Park, Dictionary<int, Peep> Guests, Peep Head) Queued(
		PeepState state = PeepState.InQueue, int queuePos = 0 )
	{
		var park = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
		var head = Guest( 7, state, queuePos );

		park.JoinQueue( Ride, 7 );

		return (park, new Dictionary<int, Peep> { [7] = head }, head);
	}

	/// <summary>
	/// <b>Both halves together.</b> The guest is flagged AND nominated - either alone is useless.
	/// </summary>
	[TestMethod]
	public void TheHeadOfTheQueueIsFlaggedAndNominatedTogether()
	{
		var (park, guests, head) = Queued();

		Assert.AreEqual( 7, new ParkRideOperation( park, guests ).Invite( ReadyScript(), TheRide() ),
			"the guest at the front is invited" );

		Assert.IsTrue( head.BeenAdmitted, "OnAdmittance set their flag" );
		Assert.AreEqual( 7, park.PersonBeingLoaded( Ride ), "and the ride nominated them" );
	}

	/// <summary>Nobody queueing means nobody to invite, and no nomination is left behind.</summary>
	[TestMethod]
	public void AnEmptyQueueInvitesNobody()
	{
		var park = new ParkState( parkIsClosed: false, visitorsToDate: 0 );

		Assert.AreEqual( 0, new ParkRideOperation( park, new Dictionary<int, Peep>() )
			.Invite( ReadyScript(), TheRide() ) );

		Assert.AreEqual( 0, park.PersonBeingLoaded( Ride ) );
	}

	/// <summary>
	/// <b>The four refusals, one at a time.</b> Each leaves the guest un-flagged and the ride
	/// un-nominated, and each would still pass the happy path if it were dropped.
	/// </summary>
	[TestMethod]
	public void EachRefusalMattersOnItsOwn()
	{
		// The script still holds the last rider.
		var (full, fullGuests, fullHead) = Queued();
		var holding = ReadyScript();
		holding.Set( ParkRideOperation.AdmitVariable, 99 );

		Assert.AreEqual( 0, new ParkRideOperation( full, fullGuests ).Invite( holding, TheRide() ),
			"the admit slot is still full" );
		Assert.IsFalse( fullHead.BeenAdmitted, "so nobody was flagged" );

		// The ride is full: capacity is not above the number aboard.
		var (packed, packedGuests, packedHead) = Queued();
		var packedScript = ReadyScript();
		packedScript.Set( ParkRideOperation.OnRideVariable, 5 );

		Assert.AreEqual( 0, new ParkRideOperation( packed, packedGuests ).Invite( packedScript, TheRide() ),
			"five aboard, room for five" );
		Assert.IsFalse( packedHead.BeenAdmitted );

		// Mid-run.
		var (running, runningGuests, runningHead) = Queued();
		var runningScript = ReadyScript();
		runningScript.Set( ParkRideOperation.RunningVariable, 1 );

		Assert.AreEqual( 0, new ParkRideOperation( running, runningGuests ).Invite( runningScript, TheRide() ),
			"the ride is running" );
		Assert.IsFalse( runningHead.BeenAdmitted );

		// Somebody has already been called forward.
		var (busy, busyGuests, busyHead) = Queued();
		busy.NominateForLoading( Ride, 99 );

		Assert.AreEqual( 0, new ParkRideOperation( busy, busyGuests ).Invite( ReadyScript(), TheRide() ),
			"a nominee is already pending" );
		Assert.IsFalse( busyHead.BeenAdmitted );
		Assert.AreEqual( 99, busy.PersonBeingLoaded( Ride ), "and that nominee stands" );
	}

	/// <summary>
	/// A full ride still invites when it is a car or water track - those keep loading while they run, so
	/// capacity is not a gate for them.
	/// </summary>
	[TestMethod]
	public void ACarOrWaterTrackIsNotStoppedByBeingFull()
	{
		foreach ( var track in new[] { ItemDescriptionFile.CarTrack, ItemDescriptionFile.WaterTrack } )
		{
			var (park, guests, head) = Queued();
			var script = ReadyScript();
			script.Set( ParkRideOperation.OnRideVariable, 5 );

			Assert.AreEqual( 7, new ParkRideOperation( park, guests ).Invite( script, TheRide(), track ),
				$"track type {track} loads while full" );
			Assert.IsTrue( head.BeenAdmitted );
		}

		// Anti-vacuity: the same setup with an ordinary ride refuses, so the exemption is doing the work.
		var (ordinary, ordinaryGuests, _) = Queued();
		var ordinaryScript = ReadyScript();
		ordinaryScript.Set( ParkRideOperation.OnRideVariable, 5 );

		Assert.AreEqual( 0, new ParkRideOperation( ordinary, ordinaryGuests )
			.Invite( ordinaryScript, TheRide(), trackType: 0 ), "an ordinary ride is stopped by it" );
	}

	/// <summary>
	/// The guest must be queueing AND at the front - <c>FUN_00501290</c> tests both, and so does this.
	/// </summary>
	[TestMethod]
	public void OnlySomebodyQueueingAtTheFrontIsInvited()
	{
		// At the front by position, but not in the queueing state.
		var (wrongState, wrongStateGuests, wrongStateHead) = Queued( PeepState.Wandering );

		Assert.AreEqual( 0, new ParkRideOperation( wrongState, wrongStateGuests )
			.Invite( ReadyScript(), TheRide() ), "not queueing" );
		Assert.IsFalse( wrongStateHead.BeenAdmitted );

		// Queueing, but not at the front.
		var (back, backGuests, backHead) = Queued( PeepState.InQueue, queuePos: 2 );

		Assert.AreEqual( 0, new ParkRideOperation( back, backGuests ).Invite( ReadyScript(), TheRide() ),
			"third in the queue" );
		Assert.IsFalse( backHead.BeenAdmitted );
	}

	/// <summary>
	/// The watchdog: a nominee who never set off is dropped, so the ride is not stuck holding them.
	/// </summary>
	[TestMethod]
	public void ANomineeWhoNeverSetOffIsDropped()
	{
		var (park, guests, _) = Queued();
		park.NominateForLoading( Ride, 7 );

		Assert.IsTrue( new ParkRideOperation( park, guests ).DropUnreadyNominee( TheRide() ),
			"they are still queueing rather than coming to board" );

		Assert.AreEqual( 0, park.PersonBeingLoaded( Ride ) );
	}

	/// <summary>But a nominee who IS on their way is left alone, or nobody could ever board.</summary>
	[TestMethod]
	public void ANomineeOnTheirWayIsLeftAlone()
	{
		var park = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
		var guests = new Dictionary<int, Peep> { [7] = Guest( 7, PeepState.BeingAdmitted ) };

		park.NominateForLoading( Ride, 7 );

		Assert.IsFalse( new ParkRideOperation( park, guests ).DropUnreadyNominee( TheRide() ) );
		Assert.AreEqual( 7, park.PersonBeingLoaded( Ride ), "the nomination stands" );
	}

	/// <summary>A ride with nobody called forward has nothing to drop.</summary>
	[TestMethod]
	public void NoNomineeIsNothingToDrop()
		=> Assert.IsFalse( new ParkRideOperation(
			new ParkState( parkIsClosed: false, visitorsToDate: 0 ), new Dictionary<int, Peep>() )
			.DropUnreadyNominee( TheRide() ) );
}
