using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A guest's own turn standing in a queue - <c>FUN_004ffff0</c>: the toilet and the lost place, which put them
/// out; the invited guest who is not the nominee, whose turn is nothing; the broken ride, which re-takes no
/// place; and the unhappy arm, held until Q85. See <c>docs/exe/ride-operation.md</c>, "The <c>InQueue</c> turn".
///
/// <para>
/// Guests are queued for the Belly Bounce in Lost Kingdom's own save, and one of them takes one turn at thing
/// sweep 40: past the thirty after a spot animation that the mood waits for, since none of them has had one.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkQueueTurnTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const int BellyBounce = 13;

	private const float Before = 50f;

	/// <summary><c>MediumHappinessChange</c>, 15, in <c>FUN_005012f0</c>, and nothing else.</summary>
	private const float PutOut = 35f;

	/// <summary>A need nobody leaves for.</summary>
	private const float Content = 10f;

	private const int Sweep = 40;

	private sealed record Park( ParkWorld World, ParkState State, PeepBehaviour Behaviour, RideScript Script,
		Dictionary<int, Peep> Guests );

	/// <param name="stop">Whether the queue walk stops at a guest no longer queueing, as the park's own does.</param>
	private Park Open( bool stop = true )
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( world );
		var admission = new ParkAdmission( new ParkBalance( "jungle", easyMode: true ), world.Economy!.Value.AdmissionFee );

		using var rse = data.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );
		var script = new RideScript( new RideScriptFile( rse ) );

		var guests = new Dictionary<int, Peep>();

		var behaviour = new PeepBehaviour( world, new Random( 1 ), admission, () => ParkRides.GateIsOpen, state,
			new ParkItemCatalogue( "jungle", data ),
			leaveQueue: ( ride, id ) => ParkRideOperation.LeaveQueue( state, script, ride.ThingId, id ),
			stillQueueing: stop ? id => guests.TryGetValue( id, out var peep ) && ParkRideOperation.IsQueueing( peep ) : null );

		return new( world, state, behaviour, script, guests );
	}

	private static ParkWorld.NavigatorState StandingStill => new(
		X: 0, Y: 0, VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
		Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
		MaxForce: 0, MaxSpeed: 0, NavMode: 0, CantReachDest: 0, PathFinished: false,
		PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
		BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

	/// <summary>A guest standing in a queue for <paramref name="thing"/>, not yet in its links.</summary>
	private static Peep Guest( int thingId, float happiness = Before, float toilet = Content, int thing = BellyBounce )
		=> new( thingId, new ParkWorld.GuestState(
			State: (int)PeepState.InQueue, SavedState: (int)PeepState.Deciding, PersonType: 0,
			Cash: 300, ExitLevel: 100, Happiness: happiness, Thirst: Content, Hunger: Content, Toilet: toilet,
			Vomit: 0f, Litter: 0f, MajorDest: thing, QueuePos: 0, PrankeryIndex: 0 ), StandingStill );

	/// <summary>Puts each guest at the back of their thing's queue, at the place it gives them.</summary>
	private static void Join( Park park, params Peep[] peeps )
	{
		foreach ( var peep in peeps )
		{
			park.Guests[peep.ThingId] = peep;
			peep.QueuePos = park.State.JoinQueue( peep.MajorDest, peep.ThingId );
		}
	}

	/// <summary>One turn of one guest, at <paramref name="tick"/>.</summary>
	private static void Turn( Park park, Peep peep, int tick = Sweep )
	{
		var walk = new PeepWalk( peep.Navigator, CellEdge.For( park.World, ParkPeople.WalkingMode ).Blocked );

		park.Behaviour.Step( peep, walk, playing: null, tick );
	}

	private static void AssertPutOut( Park park, Peep peep, string why, int thing = BellyBounce )
	{
		Assert.AreEqual( PeepState.Deciding, peep.State, $"guest {peep.ThingId} thinks again: {why}" );
		Assert.AreEqual( 0, peep.MajorDest, $"guest {peep.ThingId} names nothing: {why}" );
		Assert.AreEqual( PutOut, peep.Happiness, 0.001f, $"guest {peep.ThingId} loses MediumHappinessChange: {why}" );
		Assert.AreEqual( 0, peep.QueuePos, $"guest {peep.ThingId} has no place: {why}" );
		Assert.AreEqual( -1, park.State.PositionInQueue( thing, peep.ThingId ), $"guest {peep.ThingId} is out: {why}" );
		Assert.AreEqual( 0, park.State.NextInQueue( peep.ThingId ), $"guest {peep.ThingId} links forward to nobody" );
		Assert.AreEqual( 0, park.State.PreviousInQueue( peep.ThingId ), $"guest {peep.ThingId} links back to nobody" );
	}

	private static void AssertQueueing( Peep peep, string why, int thing = BellyBounce, float happiness = Before )
	{
		Assert.AreEqual( PeepState.InQueue, peep.State, $"guest {peep.ThingId} still queues: {why}" );
		Assert.AreEqual( thing, peep.MajorDest, $"guest {peep.ThingId} still names it: {why}" );
		Assert.AreEqual( happiness, peep.Happiness, 0.001f, $"guest {peep.ThingId} loses nothing: {why}" );
	}

	/// <summary>
	/// <b>A queuer whose toilet need is above 80 leaves the queue</b> (<c>0x005003a2</c>): out of it, for
	/// <c>MediumHappinessChange</c> alone, the ride's admission slot emptied where it names them
	/// (<c>FUN_004ddd20</c>), and the queue joined up around them.
	/// </summary>
	[TestMethod]
	public void AQueuerWhoseToiletIsAboveEightyLeavesTheQueue()
	{
		var park = Open();
		var ahead = Guest( 30 );
		var leaving = Guest( 31, toilet: 81f );
		var behind = Guest( 32 );

		Join( park, ahead, leaving, behind );
		park.Script.Set( ParkRideOperation.AdmitVariable, leaving.ThingId );

		Turn( park, leaving );

		AssertPutOut( park, leaving, "a toilet need of 81" );
		Assert.AreEqual( 0, park.Script[ParkRideOperation.AdmitVariable], "the ride's slot no longer names them" );
		Assert.AreEqual( behind.ThingId, park.State.NextInQueue( ahead.ThingId ), "the queue closes up around them" );
		Assert.AreEqual( ahead.ThingId, park.State.PreviousInQueue( behind.ThingId ) );
		Assert.AreEqual( 1, park.State.PositionInQueue( BellyBounce, behind.ThingId ), "the guest behind is second now" );
	}

	/// <summary>
	/// <b>80 is not above 80, and the need is read as a truncated byte</b> (<c>__ftol</c>, then <c>AL</c>), so 80.9
	/// stays too.
	/// </summary>
	[TestMethod]
	public void AToiletNeedOfEightyIsNotAboveIt()
	{
		foreach ( var toilet in new[] { 80f, 80.9f } )
		{
			var park = Open();
			var peep = Guest( 30, toilet: toilet );

			Join( park, peep );
			Turn( park, peep );

			AssertQueueing( peep, $"a toilet need of {toilet}" );
		}
	}

	/// <summary>
	/// <b>The toilet is asked about only from happiness 20 to 80</b>: above it the guest plays a spot animation
	/// (<c>0x0050031e</c>), from 10 to 19 another (<c>0x00500336</c>), each counted, and neither leaves. The
	/// happiness is a truncated byte as well, so 80.9 is 80 and 19.9 is 19.
	/// </summary>
	[TestMethod]
	public void TheToiletIsAskedAboutOnlyFromTwentyToEighty()
	{
		foreach ( var (happiness, leaves) in new[] { (81f, false), (80.9f, true), (20f, true), (19.9f, false), (10f, false) } )
		{
			var park = Open();
			var peep = Guest( 30, happiness, toilet: 90f );

			Join( park, peep );
			Turn( park, peep );

			if ( leaves )
			{
				Assert.AreEqual( PeepState.Deciding, peep.State, $"happiness {happiness} leaves for the toilet" );
				Assert.AreEqual( happiness - 15f, peep.Happiness, 0.001f, $"happiness {happiness} loses 15" );
			}
			else
			{
				AssertQueueing( peep, $"happiness {happiness} is not asked", happiness: happiness );
			}
		}
	}

	/// <summary><b>A guest queueing for a toilet stays in its queue</b>, however badly they need it (<c>0x005003b6</c>).</summary>
	[TestMethod]
	public void AQueuerForAToiletStaysInItsQueue()
	{
		var park = Open();
		var toilet = park.State.Objects.First( thing => thing.IsToilet && thing.IsVisitable );
		var peep = Guest( 30, toilet: 95f, thing: toilet.ThingId );

		Join( park, peep );
		Turn( park, peep );

		AssertQueueing( peep, $"queueing for the toilet, thing {toilet.ThingId}", thing: toilet.ThingId );
		Assert.AreEqual( 0, park.State.PositionInQueue( toilet.ThingId, peep.ThingId ), "and at its front" );
	}

	/// <summary>
	/// <b>The mood is read only once thirty sweeps have passed since a spot animation</b> (<c>0x00500308</c>,
	/// unsigned): a guest who has never had one waits out the park's first thirty.
	/// </summary>
	[TestMethod]
	public void TheMoodWaitsThirtySweepsAfterASpotAnimation()
	{
		var park = Open();
		var peep = Guest( 30, toilet: 90f );

		Join( park, peep );

		Turn( park, peep, tick: 30 );
		AssertQueueing( peep, "at sweep 30 the mood is not read" );

		Turn( park, peep, tick: 31 );
		AssertPutOut( park, peep, "at sweep 31 it is" );
	}

	/// <summary>
	/// <b>An unhappy queuer stays</b>: the original puts out a guest below 10 (thought <c>0xb</c>), and that arm is
	/// held until Q85 gives an arriving guest the original's happiness of 50 rather than nought. Alexah's hold.
	/// </summary>
	[TestMethod]
	public void AnUnhappyQueuerStaysUntilArrivalsHaveTheOriginalsHappiness()
	{
		var park = Open();
		var peep = Guest( 30, happiness: 0f );

		Join( park, peep );
		Turn( park, peep );

		AssertQueueing( peep, "happiness nought", happiness: 0f );
	}

	/// <summary>
	/// <b>A queuer behind somebody who has stopped queueing is lost to the queue walk, and put out</b>
	/// (<c>FUN_004ddf50</c> answers -1 at the first guest it steps past who is not queueing; <c>0x00500270</c>).
	/// Leaving splices by their own links, so the guest behind them is joined to the one who stopped.
	/// </summary>
	[TestMethod]
	public void AQueuerTheWalkCannotReachIsPutOut()
	{
		var park = Open();
		var stopped = Guest( 30 );
		var lost = Guest( 31 );
		var behind = Guest( 32 );

		Join( park, stopped, lost, behind );
		stopped.SetState( PeepState.Deciding, Sweep, new Random( 1 ) );

		Turn( park, lost );

		AssertPutOut( park, lost, "the walk gave up at guest 30" );
		Assert.AreEqual( stopped.ThingId, park.State.FirstInQueue( BellyBounce ), "the head is left where it was" );
		Assert.AreEqual( behind.ThingId, park.State.NextInQueue( stopped.ThingId ), "and linked on to the guest behind" );

		Turn( park, behind );

		AssertPutOut( park, behind, "and so is everybody behind" );
		Assert.AreEqual( 0, park.State.NextInQueue( stopped.ThingId ), "until nobody is linked to the one who stopped" );
	}

	/// <summary>
	/// <b>A queuer who is in no queue's links is put out, and empties the head of the queue they name</b>: with
	/// nobody in front, <c>FUN_004ddd20</c> writes their own next - nobody - as the head (<c>0x004ddde9</c>). The
	/// guests who were in it are then lost to the walk in turn.
	/// </summary>
	[TestMethod]
	public void AQueuerInNoQueueEmptiesTheHeadOfTheQueueTheyName()
	{
		var park = Open();
		var head = Guest( 30 );
		var second = Guest( 31 );
		var stranger = Guest( 32 );

		Join( park, head, second );
		park.Guests[stranger.ThingId] = stranger;

		Turn( park, stranger );

		AssertPutOut( park, stranger, "in no queue at all" );
		Assert.AreEqual( 0, park.State.FirstInQueue( BellyBounce ), "the head is their own next, which is nobody" );
		Assert.AreEqual( second.ThingId, park.State.NextInQueue( head.ThingId ), "the two in it keep their links" );

		Turn( park, second );

		AssertPutOut( park, second, "the walk from an empty head finds nobody" );
	}

	/// <summary>
	/// <b>At the front and invited, but the ride's nominee is somebody else: the whole turn is nothing</b>
	/// (<c>0x005001d8</c>) - not even the toilet, which would otherwise put this guest out.
	/// </summary>
	[TestMethod]
	public void AnInvitedGuestWhoIsNotTheNomineeDoesNothing()
	{
		var park = Open();
		var peep = Guest( 30, toilet: 95f );

		Join( park, peep );
		peep.BeenAdmitted = true;
		park.State.NominateForLoading( BellyBounce, 99 );

		Turn( park, peep );

		AssertQueueing( peep, "invited, not nominated, and needing the toilet" );
		Assert.IsTrue( peep.BeenAdmitted, "the invitation is kept" );
	}

	/// <summary>
	/// <b>A ride broken down (state 1) re-takes nobody's place</b> (<c>FUN_004e0370</c>, <c>0x00500523</c>): a guest
	/// left behind their true place keeps the place they had, and stands. The control, the ride in state 0, re-takes
	/// it and walks there - from the back queue cell, where a route to it starts.
	/// </summary>
	[TestMethod]
	public void ABrokenRideLeavesAQueuersPlaceAsItWas()
	{
		foreach ( var (rideState, expected, walking) in new[] { (1, 1, PeepState.InQueue), (0, 0, PeepState.SteppingUpQueue) } )
		{
			var park = Open();
			var boarded = Guest( 30 );
			var peep = Guest( 31 );

			peep.Navigator.Position = new FixedVector( PeepNavigator.WaypointCentre( 49 ), PeepNavigator.WaypointCentre( 22 ) );

			Join( park, boarded, peep );
			park.State.LeaveQueue( BellyBounce, boarded.ThingId );
			peep.QueueMoveDelay = 0;

			var ride = park.State.Objects.Single( thing => thing.ThingId == BellyBounce );
			Assert.IsTrue( park.State.ReplaceObject( ride with { State = rideState } ) );

			Turn( park, peep );

			Assert.AreEqual( expected, peep.QueuePos, $"a ride in state {rideState}" );
			Assert.AreEqual( walking, peep.State, $"a ride in state {rideState}" );
			Assert.AreEqual( BellyBounce, peep.MajorDest, $"still queueing for it, a ride in state {rideState}" );
			Assert.AreEqual( Before, peep.Happiness, 0.001f, $"losing nothing, a ride in state {rideState}" );
		}
	}

	/// <summary>The jungle's one car track - see <c>ParkTrackTypeTests</c>.</summary>
	private const int DinoKarts = 1150;

	/// <summary>
	/// <b>A queue for a car track whose track is not valid puts its queuer out</b>: item track type 1 and
	/// <c>mIsTrackRideValid</c> nought (<c>0x00500631</c>..<c>0x00500643</c>), thought <c>0xd</c>, counted. The Belly
	/// Bounce stands in as Dino Karts; the control, a valid track, keeps them.
	/// </summary>
	[TestMethod]
	public void AQueueForACarTrackThatIsNotValidPutsItsQueuerOut()
	{
		Assert.IsTrue( new ParkItemCatalogue( "jungle", data ).TryGet( DinoKarts, out var karts ), "Dino Karts is catalogued" );
		Assert.AreEqual( ItemDescriptionFile.CarTrack, karts.TrackType, "and runs on a car track" );

		foreach ( var (valid, leaves) in new[] { (0, true), (1, false) } )
		{
			var park = Open();
			var ride = park.State.Objects.Single( thing => thing.ThingId == BellyBounce );
			Assert.IsTrue( park.State.ReplaceObject( ride with { CatalogueId = DinoKarts, IsTrackRideValid = valid } ) );

			var peep = Guest( 30 );

			Join( park, peep );
			Turn( park, peep );

			if ( leaves )
				AssertPutOut( park, peep, "a car track that is not valid" );
			else
				AssertQueueing( peep, "a valid car track" );
		}
	}

	/// <summary>
	/// <b>The same toilet arm through the park's own people</b>, so the ride's side is the one
	/// <see cref="ParkPeople"/> wires to its guests: two of the save's guests queue for the Belly Bounce, the second
	/// needing the toilet, and the park is ticked as a level ticks it until the second's turn comes. The Belly
	/// Bounce is put in state 3, whose turn does nothing (<c>FUN_004e0e00</c>), so only the guest's turn can move
	/// the queue.
	/// </summary>
	[TestMethod]
	public void ThePeoplesOwnQueueTurnLetsTheRideGo()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( world );

		using var rse = data.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );
		var script = new RideScript( new RideScriptFile( rse ) );

		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), gateStatus: null, state,
			new ParkItemCatalogue( "jungle", data ), id => id == BellyBounce ? script : null );

		try
		{
			var queued = people.Peeps.Where( guest => guest.ExitLevel > 0 ).Take( 2 ).ToArray();
			Assert.AreEqual( 2, queued.Length, "the save has two guests with a day left" );

			foreach ( var peep in queued )
			{
				peep.MajorDest = BellyBounce;
				peep.Happiness = Before;
				peep.Thirst = peep.Hunger = peep.Toilet = peep.Vomit = Content;
				peep.SetState( PeepState.InQueue, tick: 1, new Random( 1 ) );
				peep.QueuePos = state.JoinQueue( BellyBounce, peep.ThingId );
			}

			var (head, leaving) = (queued[0], queued[1]);
			leaving.Toilet = 90f;
			script.Set( ParkRideOperation.AdmitVariable, leaving.ThingId );

			var ride = state.Objects.Single( thing => thing.ThingId == BellyBounce );
			Assert.IsTrue( state.ReplaceObject( ride with { State = 3 } ), "the ride's own turn is taken out" );

			Time.Paused = false;
			Time.StepFrames = 0;
			GameClock.Rebase();
			Frame( 0f );

			for ( var frame = 0; frame < 2000 && leaving.State == PeepState.InQueue; ++frame )
			{
				Frame( 1f / 60f );
				people.Update();
			}

			Assert.AreEqual( PeepState.Deciding, leaving.State, "they left for the toilet" );
			Assert.AreEqual( 0, script[ParkRideOperation.AdmitVariable], "the ride's slot no longer names them" );
			Assert.AreEqual( -1, state.PositionInQueue( BellyBounce, leaving.ThingId ), "nor does its queue" );
			Assert.AreEqual( head.ThingId, state.FirstInQueue( BellyBounce ), "the guest in front keeps the head" );
			Assert.AreNotEqual( leaving.ThingId, state.NextInQueue( head.ThingId ), "and links on to them no more" );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>The park's own queue walk stops at a guest who has stopped queueing</b>, so the guest behind them is lost
	/// to it and put out: three of the save's guests queue for the Belly Bounce and the middle one goes back to
	/// deciding, still linked, and the park takes one sweep. The head is still queueing, so the ride's turn drops
	/// nobody.
	/// </summary>
	[TestMethod]
	public void ThePeoplesOwnQueueWalkStopsAtAGuestNoLongerQueueing()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( world );

		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), gateStatus: null, state,
			new ParkItemCatalogue( "jungle", data ), scriptFor: null );

		try
		{
			var queued = people.Peeps.Where( guest => guest.ExitLevel > 0 ).Take( 3 ).ToArray();
			Assert.AreEqual( 3, queued.Length, "the save has three guests with a day left" );

			foreach ( var peep in queued )
			{
				peep.MajorDest = BellyBounce;
				peep.Happiness = Before;
				peep.Thirst = peep.Hunger = peep.Toilet = peep.Vomit = Content;
				peep.SetState( PeepState.InQueue, tick: 1, new Random( 1 ) );
				peep.QueuePos = state.JoinQueue( BellyBounce, peep.ThingId );
			}

			var (head, stopped, lost) = (queued[0], queued[1], queued[2]);
			stopped.SetState( PeepState.Deciding, tick: 1, new Random( 1 ) );

			var ride = state.Objects.Single( thing => thing.ThingId == BellyBounce );
			Assert.IsTrue( state.ReplaceObject( ride with { State = 3 } ), "the ride's own turn is taken out" );

			Time.Paused = false;
			Time.StepFrames = 0;
			GameClock.Rebase();
			Frame( 0f );

			// One sweep and no more: the guest who stopped may choose the ride again and join behind, which would
			// cut the chain whether or not the walk stops.
			var first = GameClock.Ticks / ParkPeople.ThingTickEvery;

			for ( var frame = 0; frame < 60 && GameClock.Ticks / ParkPeople.ThingTickEvery == first; ++frame )
			{
				Frame( 1f / 60f );
				people.Update();
			}

			Assert.AreNotEqual( first, GameClock.Ticks / ParkPeople.ThingTickEvery, "a sweep ran" );
			Assert.AreEqual( PeepState.Deciding, lost.State, "the walk could not reach them" );
			Assert.AreEqual( PutOut, lost.Happiness, 0.001f, "and they lost MediumHappinessChange" );
			Assert.AreEqual( head.ThingId, state.FirstInQueue( BellyBounce ), "the head keeps its place" );
			Assert.AreEqual( PeepState.InQueue, head.State, "and still queues" );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// One frame, through both clocks, in the order a frame runs them: <c>Time.Update</c> in
	/// <c>Renderer.Update</c>, then <c>GameClock.Update</c> in <see cref="Level.Update"/>.
	/// </summary>
	private static void Frame( float seconds )
	{
		Time.Update( seconds );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );
	}
}
