using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Closing and opening rides, and what a closed ride does to its queue - the park's door, the close and open and
/// their guard, the closed ride's completion on its own turn (<c>FUN_004e0450</c>), and the reopen at the end of a
/// queue measured again. See <c>docs/exe/ride-operation.md</c>, "The closed ride".
///
/// <para>
/// Every park here is Lost Kingdom's own, with a Belly Bounce script standing in for every visitable object's:
/// each declares <c>VAR_RIDECLOSED</c> and <c>VAR_LETMEON</c> by name. The scripts are not run.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkClosedRideTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const string Theme = "jungle";

	private const int BellyBounce = 13;

	private const float Before = 50f;

	/// <summary>A head put out by a closed ride: <c>MediumHappinessChange</c>, 15, in <c>FUN_005012f0</c>.</summary>
	private const float TurnedAway = 35f;

	/// <summary>The Belly Bounce's back of queue as shipped, (49,22): <c>mNeighbours</c> 0x44, <c>mDirection</c> 0x04.</summary>
	private const int BackX = 49, BackY = 22;

	private sealed record Park( ParkWorld World, ParkState State, ParkPeople People,
		Dictionary<int, RideScript> Scripts, ParkItemCatalogue Catalogue );

	private Park Open()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		var state = new ParkState( world );
		var catalogue = new ParkItemCatalogue( Theme, data );
		var scripts = state.Objects.Where( thing => thing.IsVisitable )
			.ToDictionary( thing => thing.ThingId, _ => Script() );

		var people = new ParkPeople( world, new ParkBalance( Theme, easyMode: true ), gateStatus: null, state,
			catalogue, id => scripts.GetValueOrDefault( id ) );

		return new( world, state, people, scripts, catalogue );
	}

	private static void Close( Park park )
	{
		park.People.Delete();
		Entity.ApplyDeletions();
	}

	private RideScript Script()
	{
		using var stream = data.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );

		return new RideScript( new RideScriptFile( stream ) );
	}

	private static ParkWorld.CatalogueObject Thing( Park park, int id )
		=> park.State.Objects.Single( thing => thing.ThingId == id );

	private static ParkWorld.CatalogueObject[] Visitable( Park park )
		=> park.State.Objects.Where( thing => thing.IsVisitable ).ToArray();

	private static void Change( Park park, int id, Func<ParkWorld.CatalogueObject, ParkWorld.CatalogueObject> how )
		=> Assert.IsTrue( park.State.ReplaceObject( how( Thing( park, id ) ) ), $"thing {id} is in the park" );

	/// <summary>Puts the park's first <paramref name="count"/> guests in the Belly Bounce's queue, in order.</summary>
	private static Peep[] Queue( Park park, int count )
	{
		var queued = park.People.Peeps.Take( count ).ToArray();

		Assert.AreEqual( count, queued.Length, "the save has enough guests" );

		foreach ( var peep in queued )
		{
			peep.MajorDest = BellyBounce;
			peep.Happiness = Before;
			peep.SetState( PeepState.InQueue, tick: 1, new Random( 1 ) );
			peep.QueuePos = park.State.JoinQueue( BellyBounce, peep.ThingId );
		}

		return queued;
	}

	private static void AssertTurnedAway( Park park, Peep peep, string why )
	{
		Assert.AreEqual( PeepState.Deciding, peep.State, $"guest {peep.ThingId} thinks again: {why}" );
		Assert.AreEqual( 0, peep.MajorDest, $"guest {peep.ThingId} names the ride no longer: {why}" );
		Assert.AreEqual( TurnedAway, peep.Happiness, 0.001f, $"guest {peep.ThingId} loses MediumHappinessChange: {why}" );
		Assert.AreEqual( -1, park.State.PositionInQueue( BellyBounce, peep.ThingId ), $"guest {peep.ThingId} is out: {why}" );
		Assert.AreEqual( 0, park.State.NextInQueue( peep.ThingId ), $"guest {peep.ThingId} links forward to nobody" );
		Assert.AreEqual( 0, park.State.PreviousInQueue( peep.ThingId ), $"guest {peep.ThingId} links back to nobody" );
	}

	private static void AssertStillQueueing( Park park, Peep peep, string why )
	{
		Assert.AreEqual( BellyBounce, peep.MajorDest, $"guest {peep.ThingId} still names the ride: {why}" );
		Assert.AreEqual( Before, peep.Happiness, 0.001f, $"guest {peep.ThingId} loses nothing: {why}" );
		Assert.IsTrue( park.State.PositionInQueue( BellyBounce, peep.ThingId ) >= 0, $"guest {peep.ThingId} still queues: {why}" );
	}

	/// <summary>One turn of the closed ride's completion, as the ride's own turn runs it.</summary>
	private static void OneTurn( Park park )
		=> park.People.CompleteOrTurnAway( new ParkRideOperation( park.State, park.People.Guests ),
			park.Scripts[BellyBounce], Thing( park, BellyBounce ), thingTick: 10 );

	/// <summary>
	/// The door's close arm closes every object a guest may be offered, and nothing else: <c>mCanLoad</c>
	/// nought, the nominee let go of, <c>VAR_RIDECLOSED</c> set (<c>FUN_004df300</c> at <c>0x0051a1ae</c>).
	/// </summary>
	[TestMethod]
	public void ClosingTheParkClosesEveryObjectAGuestMayBeOffered()
	{
		var park = Open();

		try
		{
			var visitable = Visitable( park );
			var others = park.State.Objects.Where( thing => !thing.IsVisitable ).ToArray();

			Assert.AreEqual( 6, visitable.Length, "three toilets, the Drinks Shop, the Jungle Spray and the Belly Bounce" );
			Assert.IsTrue( park.State.Objects.All( thing => thing.CanLoad == 1 ), "the shipped park is saved with every object open" );

			park.State.NominateForLoading( BellyBounce, park.People.Peeps[0].ThingId );

			park.State.SetParkClosed( true );

			Assert.IsTrue( park.State.ParkIsClosed, "the park is shut" );

			foreach ( var thing in visitable )
			{
				Assert.AreEqual( 0, Thing( park, thing.ThingId ).CanLoad, $"thing {thing.ThingId} is closed" );
				Assert.AreEqual( 1, park.Scripts[thing.ThingId][ParkRideOperation.ClosedVariable],
					$"thing {thing.ThingId}'s script is told" );
				Assert.AreEqual( thing.State, Thing( park, thing.ThingId ).State, $"thing {thing.ThingId}'s state is left alone" );
			}

			Assert.AreEqual( 0, park.State.PersonBeingLoaded( BellyBounce ), "the nominee is let go of" );

			foreach ( var thing in others )
				Assert.AreEqual( thing.CanLoad, Thing( park, thing.ThingId ).CanLoad, $"thing {thing.ThingId} is not a ride anybody visits" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// The door's open arm opens again every object its guard allows (<c>0x0051a013</c>..<c>0x0051a01e</c>),
	/// and in the shipped park that is all six: every back of queue and every entrance is joined to path.
	/// </summary>
	[TestMethod]
	public void OpeningTheParkOpensEveryObjectItClosed()
	{
		var park = Open();

		try
		{
			foreach ( var thing in Visitable( park ) )
				Assert.IsTrue( ParkRideOperation.MayOpen( park.World, thing, trackType: 0 ),
					$"thing {thing.ThingId} passes the guard as shipped" );

			park.State.SetParkClosed( true );
			park.State.SetParkClosed( false );

			Assert.IsFalse( park.State.ParkIsClosed, "the park is open" );

			foreach ( var thing in Visitable( park ) )
			{
				Assert.AreEqual( 1, thing.CanLoad, $"thing {thing.ThingId} is open again" );
				Assert.AreEqual( 0, thing.State, $"thing {thing.ThingId} is operating" );
				Assert.AreEqual( 0, park.Scripts[thing.ThingId][ParkRideOperation.ClosedVariable],
					$"thing {thing.ThingId}'s script is told" );
			}
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// Each arm acts only on a change (<c>0x00519f76</c>, <c>0x0051a09e</c>): shutting a shut park closes
	/// nothing, and opening an open one opens nothing.
	/// </summary>
	[TestMethod]
	public void TheDoorActsOnlyWhenItMoves()
	{
		var park = Open();

		try
		{
			var operation = new ParkRideOperation( park.State, park.People.Guests );

			operation.Close( park.Scripts[BellyBounce], BellyBounce );
			park.State.SetParkClosed( false );

			Assert.AreEqual( 0, Thing( park, BellyBounce ).CanLoad, "an open park's door opens nothing" );

			park.State.SetParkClosed( true );
			operation.Open( park.Scripts[BellyBounce], BellyBounce );
			park.State.SetParkClosed( true );

			Assert.AreEqual( 1, Thing( park, BellyBounce ).CanLoad, "a shut park's door closes nothing" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// The guard refuses a ride with a mechanic called (<c>mRequestedService</c>), out of service (1, 2 or 4),
	/// or whose queue joins nothing, and the door leaves each closed.
	/// </summary>
	[TestMethod]
	public void TheDoorLeavesClosedWhatItsGuardRefuses()
	{
		var refusals = new (string Why, Action<Park> Make)[]
		{
			("a mechanic called", park => Change( park, BellyBounce, thing => thing with { RequestedService = 1 } )),
			("broken down", park => Change( park, BellyBounce, thing => thing with { State = 1 } )),
			("waiting for an upgrade", park => Change( park, BellyBounce, thing => thing with { State = 2 } )),
			("condemned", park => Change( park, BellyBounce, thing => thing with { State = 4 } )),
			("its back of queue joined to nothing", park => park.State.SetRecord( BackX, BackY,
				park.State.Record( BackX, BackY ) with { Neighbours = 0x04 } )),
		};

		foreach ( var (why, make) in refusals )
		{
			var park = Open();

			try
			{
				park.State.SetParkClosed( true );
				make( park );
				park.State.SetParkClosed( false );

				Assert.AreEqual( 0, Thing( park, BellyBounce ).CanLoad, $"{why}: the Belly Bounce stays closed" );
				Assert.AreEqual( 1, park.Scripts[BellyBounce][ParkRideOperation.ClosedVariable], $"{why}: and its script is not told" );
				Assert.IsTrue( Visitable( park ).Where( thing => thing.ThingId != BellyBounce ).All( thing => thing.CanLoad == 1 ),
					$"{why}: the other five open" );
			}
			finally
			{
				Close( park );
			}
		}
	}

	/// <summary>
	/// <c>FUN_004de4a0</c>. A thing with a queue path asks its back cell, which as shipped links on to the path
	/// behind it (0x44 against 0x04); any other thing asks the cell one step out of its entrance, which must be
	/// path linked both ways.
	/// </summary>
	[TestMethod]
	public void TheBackOfAQueueIsConnectedWhenItJoinsSomething()
	{
		var park = Open();

		try
		{
			var bounce = Thing( park, BellyBounce );

			Assert.IsTrue( bounce.HasQueuePath, "the Belly Bounce's queue is laid on the ground" );
			Assert.IsTrue( ParkRideOperation.BackOfQueueConnected( park.World, bounce ), "joined as shipped" );

			park.State.SetRecord( BackX, BackY, park.State.Record( BackX, BackY ) with { Neighbours = 0x04 } );

			Assert.IsFalse( ParkRideOperation.BackOfQueueConnected( park.World, bounce ),
				"a back cell linked only to the cell ahead joins nothing" );

			var entered = Visitable( park ).Where( thing => !thing.HasQueuePath ).ToArray();

			Assert.IsTrue( entered.Length > 0, "some visitable thing has no queue path" );

			foreach ( var thing in entered )
			{
				Assert.IsTrue( ParkRideOperation.BackOfQueueConnected( park.World, thing ), $"thing {thing.ThingId} joined as shipped" );

				var facing = thing.Angle switch { 0 => 0x10, 90 => 0x04, 180 => 0x01, _ => 0x40 };
				var (x, y) = MapStep.CellAt( thing.EntryPos );
				var (bx, by) = ParkBuilding.Step( x, y, CellEdge.Opposite( facing ) );
				var beyond = park.State.Record( bx, by );

				park.State.SetRecord( bx, by, beyond with { Type = 0 } );

				Assert.IsFalse( ParkRideOperation.BackOfQueueConnected( park.World, thing ),
					$"thing {thing.ThingId}: with no path before its entrance it joins nothing" );

				park.State.SetRecord( bx, by, beyond );
			}
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// A closed ride's turn puts its head out of the queue, one a turn (<c>0x004e0554</c>): off the queue, back
	/// to deciding, <c>MediumHappinessChange</c> off. Whoever was second is the head for the next turn.
	/// </summary>
	[TestMethod]
	public void AClosedRidePutsOutItsHeadOneATurn()
	{
		var park = Open();

		try
		{
			var queued = Queue( park, 3 );

			park.State.SetParkClosed( true );

			OneTurn( park );

			AssertTurnedAway( park, queued[0], "the head, on the first turn" );
			AssertStillQueueing( park, queued[1], "second, on the first turn" );
			AssertStillQueueing( park, queued[2], "third, on the first turn" );
			Assert.AreEqual( queued[1].ThingId, park.State.FirstInQueue( BellyBounce ), "the second is the head now" );

			OneTurn( park );

			AssertTurnedAway( park, queued[1], "the head, on the second turn" );
			AssertStillQueueing( park, queued[2], "third, on the second turn" );

			OneTurn( park );
			OneTurn( park );

			AssertTurnedAway( park, queued[2], "the last" );
			Assert.AreEqual( 0, park.State.FirstInQueue( BellyBounce ), "the queue is empty" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// A head in <see cref="PeepState.EnteringRide"/> whom the admit slot no longer names is forced on instead
	/// (<c>FUN_00500870</c>): off the queue, riding, nothing lost. One the slot still names is put out, and the
	/// slot is emptied as they go (<c>FUN_004ddd20</c>).
	/// </summary>
	[TestMethod]
	public void AnEnteringHeadIsForcedOnUnlessTheSlotStillNamesThem()
	{
		var park = Open();

		try
		{
			var queued = Queue( park, 2 );
			var script = park.Scripts[BellyBounce];

			park.State.SetParkClosed( true );

			queued[0].SetState( PeepState.EnteringRide, tick: 1, new Random( 1 ) );
			OneTurn( park );

			Assert.AreEqual( PeepState.Riding, queued[0].State, "forced onto the ride" );
			Assert.AreEqual( Before, queued[0].Happiness, 0.001f, "losing nothing" );
			Assert.AreEqual( -1, park.State.PositionInQueue( BellyBounce, queued[0].ThingId ), "and out of the queue" );
			AssertStillQueueing( park, queued[1], "second, while the first was forced on" );

			queued[1].SetState( PeepState.EnteringRide, tick: 1, new Random( 1 ) );
			script.Set( ParkRideOperation.AdmitVariable, queued[1].ThingId );
			OneTurn( park );

			AssertTurnedAway( park, queued[1], "entering, but the slot still names them" );
			Assert.AreEqual( 0, script[ParkRideOperation.AdmitVariable], "the slot is emptied" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// Through the ride's own turn: a closed ride invites nobody and puts one head out on each thing sweep.
	/// </summary>
	[TestMethod]
	public void AClosedRideTurnsAwayOneHeadEverySweep()
	{
		var park = Open();

		try
		{
			var queued = Queue( park, 4 );

			park.State.SetParkClosed( true );

			Time.Paused = false;
			Time.StepFrames = 0;
			GameClock.Rebase();
			Time.Update( 0f );
			GameClock.Update( paused: false, GameClock.ParkCatchUp );

			var sweeps = 0;
			var seenOut = new HashSet<int>();

			for ( var frame = 0; frame < 2000 && sweeps < 6; ++frame )
			{
				Time.Update( 1f / 60f );
				GameClock.Update( paused: false, GameClock.ParkCatchUp );

				var ticks = GameClock.TicksDue;

				park.People.Update();

				var swept = Enumerable.Range( 0, ticks )
					.Count( i => ((GameClock.Ticks - ticks + 1 + i) & (ParkPeople.ThingTickEvery - 1)) == 0 );

				if ( swept == 0 )
					continue;

				sweeps += swept;

				var gone = queued.Where( peep => peep.MajorDest != BellyBounce ).ToArray();

				Assert.AreEqual( Math.Min( sweeps, queued.Length ), gone.Length, $"after {sweeps} sweeps, one head out for each" );
				Assert.AreEqual( 0, park.State.PersonBeingLoaded( BellyBounce ), "nobody is invited" );

				// The ride takes its turn after the people take theirs, so whoever it put out this sweep has
				// not yet decided anything - a guest in a shut park then loses heart and heads for the exit.
				foreach ( var peep in gone.Where( peep => seenOut.Add( peep.ThingId ) ) )
					AssertTurnedAway( park, peep, $"put out on sweep {sweeps}" );
			}

			Assert.IsTrue( sweeps >= 5, "the park swept" );
			Assert.AreEqual( queued.Length, seenOut.Count, "every one was seen as they were put out" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// The tail of <c>FUN_004de1f0</c>: a queue measured again opens its closed ride when the guard passes,
	/// whatever the park's door says, and the ride forgets its assigned member of staff either way.
	/// </summary>
	[TestMethod]
	public void AQueueMeasuredAgainOpensItsClosedRide()
	{
		var park = Open();

		try
		{
			// State 3, never offered, is one the guard does not refuse, and the open's SetState(0) moves it.
			park.State.SetParkClosed( true );
			Change( park, BellyBounce, thing => thing with { AssignedStaff = 5, State = 3 } );

			park.State.RemeasureQueue( BellyBounce );

			Assert.AreEqual( 1, Thing( park, BellyBounce ).CanLoad, "the Belly Bounce is open again" );
			Assert.AreEqual( 0, Thing( park, BellyBounce ).State, "and operating" );
			Assert.AreEqual( 0, park.Scripts[BellyBounce][ParkRideOperation.ClosedVariable], "and its script is told" );
			Assert.AreEqual( 0, Thing( park, BellyBounce ).AssignedStaff, "its member of staff is forgotten" );
			Assert.IsTrue( park.State.ParkIsClosed, "the park stays shut" );
			Assert.IsTrue( Visitable( park ).Where( thing => thing.ThingId != BellyBounce ).All( thing => thing.CanLoad == 0 ),
				"and nothing else opens" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>And not when the guard refuses: a mechanic called keeps it closed, and the staff is still forgotten.</summary>
	[TestMethod]
	public void AQueueMeasuredAgainLeavesClosedWhatTheGuardRefuses()
	{
		var park = Open();

		try
		{
			park.State.SetParkClosed( true );
			Change( park, BellyBounce, thing => thing with { RequestedService = 1, AssignedStaff = 5 } );

			park.State.RemeasureQueue( BellyBounce );

			Assert.AreEqual( 0, Thing( park, BellyBounce ).CanLoad, "the Belly Bounce stays closed" );
			Assert.AreEqual( 0, Thing( park, BellyBounce ).AssignedStaff, "its member of staff is forgotten all the same" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// The flags a bought thing is given (<see cref="ParkItemCatalogue.Item.ObjectFlags"/>) are the save's own, the
	/// whole word, for every placed object: the Belly Bounce has a queue on the ground and its item
	/// <c>Info.HasQueue</c>, the Jungle Spray neither.
	/// </summary>
	[TestMethod]
	public void ABoughtThingIsFlaggedAsTheSaveFlagsItsOwn()
	{
		var park = Open();

		try
		{
			var compared = 0;

			foreach ( var thing in park.World.Objects.Where( thing => thing.IsPlaced ) )
			{
				if ( !park.Catalogue.TryGet( thing.CatalogueId, out var item ) )
					continue;

				Assert.AreEqual( thing.Flags, item.ObjectFlags, $"thing {thing.ThingId} '{item.Name}'" );
				++compared;
			}

			Assert.AreEqual( 11, compared, "every placed object" );
			Assert.IsTrue( Thing( park, BellyBounce ).HasQueuePath, "one of them has a queue path" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// A Belly Bounce as a purchase or a move builds it - its flags from the item, no saved back of queue - opens
	/// again when the park does: it takes the back-of-queue arm of <c>FUN_004de4a0</c>, where the entrance arm
	/// would find its own queue cell and never open it.
	/// </summary>
	[TestMethod]
	public void ABoughtQueuedRideOpensAgainWithThePark()
	{
		var park = Open();

		try
		{
			Assert.IsTrue( park.Catalogue.TryGet( Thing( park, BellyBounce ).CatalogueId, out var item ), "the item" );
			Change( park, BellyBounce, thing => thing with { Flags = (ushort)item.ObjectFlags, BackOfQueue = 0 } );

			park.State.SetParkClosed( true );

			Assert.AreEqual( 0, Thing( park, BellyBounce ).CanLoad, "closed with the park" );

			park.State.SetParkClosed( false );

			Assert.AreEqual( 1, Thing( park, BellyBounce ).CanLoad, "and open with it again" );
		}
		finally
		{
			Close( park );
		}
	}

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	/// <summary>
	/// A ride whose script reads <c>VAR_BROKEN</c> set is broken down on its own turn (<c>FUN_004e14e0</c>,
	/// <c>0x004e1508</c>, and <c>FUN_004e0e60( 1 )</c>): the broken picture on its hoardings, one head put out,
	/// shut as every close shuts it, the event counted and <c>mState</c> 1. The turns after are state 1's, a head
	/// each.
	/// </summary>
	[TestMethod]
	public void ARideWhoseScriptSaysBrokenIsBrokenDownAndShutOnItsTurn()
	{
		var park = Open();

		try
		{
			var queued = Queue( park, 3 );
			var hoarding = park.State.BindHoarding( BellyBounce );
			var counted = Times( "RIDE_BROKEN_DOWN_EVENT" );

			park.State.NominateForLoading( BellyBounce, queued[0].ThingId );
			park.People.TakeTheRidesTurns( thingTick: 10 );

			Assert.AreEqual( (0, 1, 0u), (Thing( park, BellyBounce ).State, Thing( park, BellyBounce ).CanLoad, hoarding.Flags),
				"a ride whose script says nothing is left as it was" );

			Assert.IsTrue( park.Scripts[BellyBounce].Set( ParkRideOperation.BrokenVariable, 1 ) );
			park.People.TakeTheRidesTurns( thingTick: 11 );

			var broken = Thing( park, BellyBounce );

			Assert.AreEqual( (ParkRideChoice.StateRefusedOne, 0), (broken.State, broken.CanLoad), "broken down, and its door shut" );
			Assert.AreEqual( 1, park.Scripts[BellyBounce][ParkRideOperation.ClosedVariable], "the script is told it is closed" );
			Assert.AreEqual( 0, park.State.PersonBeingLoaded( BellyBounce ), "the nominee is let go of" );
			Assert.AreEqual( 0x13u, hoarding.Flags, "the hoardings go up with the broken picture, kind 2, kept by the close's kind 1" );
			Assert.AreEqual( 1, hoarding.Texture );
			Assert.AreEqual( counted + 1, Times( "RIDE_BROKEN_DOWN_EVENT" ), "the post to the event bus is counted, once" );
			AssertTurnedAway( park, queued[0], "the completion SetState runs puts the head out" );
			AssertStillQueueing( park, queued[1], "one head a call" );

			foreach ( var other in Visitable( park ).Where( thing => thing.ThingId != BellyBounce ) )
				Assert.AreEqual( (0, 1), (other.State, other.CanLoad), $"thing {other.ThingId}, whose script says nothing, is left open" );

			park.People.TakeTheRidesTurns( thingTick: 12 );

			AssertTurnedAway( park, queued[1], "a broken ride's own turn puts out the next head" );
			AssertStillQueueing( park, queued[2], "and one a turn" );
			Assert.AreEqual( counted + 1, Times( "RIDE_BROKEN_DOWN_EVENT" ), "a ride already broken down is not set so again" );
			Assert.AreEqual( 0x13u, hoarding.Flags );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// The remaining life (<c>+0x48</c>) cut to a byte decides between the two: at nought the ride is condemned,
	/// <c>mState</c> 4 and the hoardings' fourth-kind picture (<c>0x004e1556</c>); at one it is broken down.
	/// </summary>
	[TestMethod]
	[DataRow( 0.99f, ParkRideChoice.StateRefusedFour, 0x23u, "RIDE_CONDEMNED_EVENT" )]
	[DataRow( 0f, ParkRideChoice.StateRefusedFour, 0x23u, "RIDE_CONDEMNED_EVENT" )]
	[DataRow( 1f, ParkRideChoice.StateRefusedOne, 0x13u, "RIDE_BROKEN_DOWN_EVENT" )]
	[DataRow( 100f, ParkRideChoice.StateRefusedOne, 0x13u, "RIDE_BROKEN_DOWN_EVENT" )]
	public void ABrokenRideWithNoLifeLeftIsCondemned( float life, int state, uint flags, string gap )
	{
		var park = Open();

		try
		{
			var hoarding = park.State.BindHoarding( BellyBounce );
			var counted = Times( gap );

			Change( park, BellyBounce, thing => thing with { RemainingLife = life } );
			Assert.IsTrue( park.Scripts[BellyBounce].Set( ParkRideOperation.BrokenVariable, 1 ) );
			park.People.TakeTheRidesTurns( thingTick: 10 );

			Assert.AreEqual( (state, 0), (Thing( park, BellyBounce ).State, Thing( park, BellyBounce ).CanLoad) );
			Assert.AreEqual( flags, hoarding.Flags );
			Assert.AreEqual( counted + 1, Times( gap ) );
			Assert.AreEqual( 1, park.Scripts[BellyBounce][ParkRideOperation.ClosedVariable] );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// What the advisor's rows 28 to 32 read of the park (0x0059d620 to 0x0059d7a0): each kind of staff's members
	/// (<c>FUN_004c9960</c>), its monthly training budget, the staff HQ's <c>mBudget</c>, and its members' grades
	/// added up (<c>FUN_004c9c00</c>): the shipped park's one member a kind, no budget, grade 3 but for the
	/// researcher's 2.
	/// </summary>
	/// <remarks><b>Mutations:</b> a kind's members, budget or grades taken from another kind; the grades not added; a grade counted from one; the budget of the file, not the park's.</remarks>
	[TestMethod]
	public void TheAdvisorsParkFactsHoldEachKindOfStaffsMembersBudgetAndGrades()
	{
		var park = Open();

		try
		{
			Assert.AreEqual( StockStaffTraining, park.People.AdvisorFacts().Staff );
			CollectionAssert.AreEqual( new[] { 60, 60, 60, 60, 40 }, Enumerable.Range( 0, 5 ).Select( kind => park.People.AdvisorFacts().Staff.Of( kind ).GradeShare ).ToArray() );

			// Each kind's own budget and its own member's grade, by the member's model: 5, 4, 6, 7 and 8.
			int[] models = [5, 4, 6, 7, 8];
			for ( var kind = 0; kind < 5; ++kind )
			{
				park.People.TrainingBudgets[kind] = 100 + kind;
				park.People.Staff.Single( member => member.Model == models[kind] ).PayGrade = kind;
			}

			var staff = park.People.AdvisorFacts().Staff;
			for ( var kind = 0; kind < 5; ++kind )
				Assert.AreEqual( new ParkAdvisorMessages.KindTraining( 1, 100 + kind, kind ), staff.Of( kind ), $"kind {kind}" );
			Assert.AreEqual( default( ParkAdvisorMessages.KindTraining ), staff.Of( 5 ) );

			// A second mechanic's grade is added to the first's, 1 and 3 of ten: 40 per cent.
			Assert.AreNotEqual( 0, park.People.Hire( new ParkStaffPool.Candidate( Id: 998, Kind: 1, Name: "Ada Test", Grade: 3, Costume: 0, Wage: 69 ), 47, 21 ) );
			var two = park.People.AdvisorFacts().Staff;
			Assert.AreEqual( (new ParkAdvisorMessages.KindTraining( 2, 101, 4 ), 40), (two.Mechanics, two.Mechanics.GradeShare) );
			Assert.AreEqual( new ParkAdvisorMessages.KindTraining( 1, 100, 0 ), two.Handymen, "and nobody else's" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// What the advisor's row 19 reads of the park (0x0059d180): the mechanics whose area is not the whole map
	/// (<c>0x004c9b40</c>), the map's ride entrances, <c>mType</c> 9, and those inside such an area, both corners
	/// held (<c>0x004cd340</c>, <c>0x004cd500</c> mode 2). The shipped park's mechanic keeps to (39,21) to (56,28),
	/// which holds one of its eight entrances, (52,23); its handyman, entertainer and guard keep to areas too.
	/// </summary>
	/// <remarks><b>Mutations:</b> another kind's areas counted; the whole map counted as an area; a corner's row or column left out; the entrances of another <c>mType</c>; an entrance counted for each area that holds it; a mechanic with no area patrolling every entrance (<c>member.HasPatrolArea</c> left out of <c>MechanicPatrols</c>).</remarks>
	[TestMethod]
	public void TheAdvisorsParkFactsHoldTheEntrancesInsideTheMechanicsAreas()
	{
		var park = Open();

		try
		{
			var mechanic = park.People.Staff.Single( member => member.Model == ParkPeople.MechanicModel );
			(int, int, int) Facts() => park.People.AdvisorFacts() is var facts ? (facts.MechanicsWithAnArea, facts.Entrances, facts.EntrancesPatrolled) : default;

			Assert.AreEqual( (1, 8, 1), Facts(), "the shipped park" );
			mechanic.SetPatrolArea( MapStep.CellId( 52, 23 ), MapStep.CellId( 52, 23 ) );
			Assert.AreEqual( (1, 8, 1), Facts(), "the one cell, both corners" );
			mechanic.SetPatrolArea( MapStep.CellId( 53, 24 ), MapStep.CellId( 56, 28 ) );
			Assert.AreEqual( (1, 8, 0), Facts(), "a column and a row short" );
			mechanic.SetPatrolArea( MapStep.CellId( 43, 29 ), MapStep.CellId( 52, 30 ) );
			Assert.AreEqual( (1, 8, 3), Facts(), "(44,29), (43,30) and (52,30), on the corners' columns" );

			// A second mechanic over the same cells: two areas, the entrances counted once.
			Assert.AreNotEqual( 0, park.People.Hire( new ParkStaffPool.Candidate( Id: 998, Kind: 1, Name: "Ada Test", Grade: 3, Costume: 0, Wage: 69 ), 47, 21 ) );
			var second = park.People.Staff.Single( member => member.Model == ParkPeople.MechanicModel && member != mechanic );
			Assert.AreEqual( (1, 8, 3), Facts(), "a hire keeps to the whole map" );
			second.SetPatrolArea( MapStep.CellId( 43, 29 ), MapStep.CellId( 52, 30 ) );
			Assert.AreEqual( (2, 8, 3), Facts() );

			// A mechanic with no area at all, nought and nought, counts as one with an area and holds no cell.
			second.SetPatrolArea( 0, 0 );
			Assert.AreEqual( (2, 8, 3), Facts(), "no area counts and holds no cell" );

			second.ClearPatrolArea();
			mechanic.ClearPatrolArea();
			Assert.AreEqual( (0, 8, 0), Facts(), "the whole map is no area" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// What the advisor's row 76 reads of the park besides row 80's (0x0059dfd0): the lab's projects of a
	/// sideshow's kind that are not done (<c>0x004ca670</c>) and how much of the lab's work the sideshows are
	/// getting (<c>FUN_00503980</c>), made of every researcher's ability by their grade (<c>FUN_004c9e60</c>) and
	/// the most a park's could be (<c>FUN_004c9ed0</c>): the shipped park's two, 1, 12 and 120.
	/// </summary>
	/// <remarks><b>Mutations:</b> the features' rate or projects for the sideshows'; every researcher taken as one grade; the mechanics' grades added; the researchers counted for their ability; the most of another kind or grade.</remarks>
	[TestMethod]
	public void TheAdvisorsParkFactsHoldTheSideshowsResearchRateAndWhatItIsMadeOf()
	{
		var park = Open();

		try
		{
			(int Unfinished, int Rate, int Ability, int Most) Read()
			{
				var facts = park.People.AdvisorFacts();
				return (facts.SideshowProjectsUnfinished, facts.SideshowResearchRate, facts.ResearchAbility, facts.MostResearchAbility);
			}

			var researcher = park.People.Staff.Single( member => member.Model == 8 );
			Assert.AreEqual( 2, researcher.PayGrade );
			Assert.AreEqual( (2, StockSideshowResearchRate, StockResearchAbility, StockMostResearchAbility), Read() );

			// The grade is the ability: the sideshows' 14 is nought at 5 and 7 per cent, 1 at 10 and 13, 2 at 16,
			// where the features' 11 is still 1.
			int[] abilities = [6, 9, 12, 16, 20];
			int[] rates = [0, 0, 1, 1, 2];
			for ( var grade = 0; grade < 5; ++grade )
			{
				researcher.PayGrade = grade;
				Assert.AreEqual( (2, rates[grade], abilities[grade], 120), Read(), $"grade {grade}" );
			}

			// A second researcher's ability is added to the first's; a mechanic's grade is nobody's.
			researcher.PayGrade = 0;
			park.People.Staff.Single( member => member.ThingId == Mechanic ).PayGrade = 4;
			Assert.AreEqual( (2, 0, 6, 120), Read() );
			Assert.AreNotEqual( 0, park.People.Hire( new ParkStaffPool.Candidate( Id: 999, Kind: 4, Name: "Ada Test", Grade: 1, Costume: 0, Wage: 69 ), 47, 21 ) );
			Assert.AreEqual( (2, 1, 15, 120), Read(), "6 and 9 are 12 per cent" );
			Assert.IsTrue( park.People.Fire( researcher.ThingId ) );
			Assert.AreEqual( (2, 0, 9, 120), Read() );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// What the advisor's row 80 reads of the park (0x0059e260): the researchers (<c>FUN_004c9960</c> asked for
	/// type 8) and the lab's projects of a feature's kind that are not done (<c>0x004ca690</c>), the shipped
	/// park's one and eleven; and what rows 15 and 16 read besides, the lab's projects of a ride's kind that are not
	/// done (<c>0x004ca630</c>), its eight.
	/// </summary>
	/// <remarks><b>Mutations:</b> the mechanics, or all the staff, counted for the researchers; another kind's projects counted, or all of them; the count nought with a lab.</remarks>
	[TestMethod]
	public void TheAdvisorsParkFactsCountTheResearchersAndTheLabsUnfinishedFeatures()
	{
		var park = Open();

		try
		{
			(int Researchers, int Mechanics, int Unfinished) Read() => (park.People.AdvisorFacts().Researchers, park.People.AdvisorFacts().Mechanics, park.People.AdvisorFacts().FeatureProjectsUnfinished);
			Assert.AreEqual( (1, 1, StockFeatureProjectsUnfinished), Read() );
			Assert.AreEqual( 11, park.World.ResearchProjects.Count( project => project.Category == ItemDescriptionFile.Feature ) );
			Assert.AreEqual( 24, park.World.ResearchProjects.Count );
			Assert.IsTrue( park.People.Fire( Mechanic ) );
			Assert.AreEqual( (1, 0, StockFeatureProjectsUnfinished), Read(), "the mechanic is not a researcher" );
			var researcher = park.People.Staff.Single( member => member.Model == 8 ).ThingId;
			Assert.IsTrue( park.People.Fire( researcher ) );
			Assert.AreEqual( (0, 0, StockFeatureProjectsUnfinished), Read(), "the lab's projects are not the staff's" );
			Assert.AreEqual( 8, park.World.ResearchProjects.Count( project => project.Category == ItemDescriptionFile.Ride ) );
			Assert.AreEqual( StockRideProjectsUnfinished, park.People.AdvisorFacts().RideProjectsUnfinished, "rows 15 and 16 read the rides' (0x004ca630)" );
			Assert.IsTrue( park.People.Staff.Count >= 3, "the others are still on the staff" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// What the advisor's row 17 reads of the park besides (0x0059cfe0): the staff pool's researchers to hire
	/// (<c>FUN_00508380</c> asked for kind four, a slot whose <c>mValid</c> is set), the shipped park's three of sixteen
	/// candidates; and its researchers' ability added up and the most it could be, 12 and 120.
	/// </summary>
	/// <remarks><b>Mutations:</b> another kind's candidates counted, or every candidate; the count not dropping as one is taken.</remarks>
	[TestMethod]
	public void TheAdvisorsParkFactsCountTheResearchersToHire()
	{
		var park = Open();
		var pool = new ParkStaffPool( new ParkBalance( Theme, easyMode: true ), gameTick: park.World.GameTick, saved: park.World );

		try
		{
			Assert.AreEqual( 16, pool.Candidates.Count );
			Assert.AreEqual( (3, 1, 12, 120), (park.People.AdvisorFacts().ResearcherCandidates, park.People.AdvisorFacts().Researchers,
				park.People.AdvisorFacts().ResearchAbility, park.People.AdvisorFacts().MostResearchAbility) );
			Assert.IsTrue( pool.Take( pool.OfKind( 1 ).First().Id, out _ ) );
			Assert.AreEqual( 3, park.People.AdvisorFacts().ResearcherCandidates, "a mechanic taken is not a researcher" );
			Assert.IsTrue( pool.Take( pool.OfKind( 4 ).First().Id, out _ ) );
			Assert.AreEqual( 2, park.People.AdvisorFacts().ResearcherCandidates );
		}
		finally
		{
			ParkStaffPool.ForgetCurrent();
			Close( park );
		}
	}

	/// <summary>
	/// What the advisor's row 82 reads of the park (0x0059e380): the guests inside it (<c>FUN_004c9130</c>: those
	/// on a cell <c>FUN_004fa990</c> passes) and those of them whose toilet need, cut to a whole number and to its
	/// low byte, is over the advisor's FeaturesNoToilets.NeedingMoreThan, 60 (<c>0x004c9290</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> a guest outside counted inside, or as needing; 60 over 60; the need rounded, not cut; the low byte not taken; the level nought with no advisor; another need read for the toilet's.</remarks>
	[TestMethod]
	public void TheAdvisorsParkFactsCountTheGuestsInsideAndThoseNeedingAToilet()
	{
		var park = Open();

		try
		{
			(int Inside, int Needing) Read() => (park.People.AdvisorFacts().GuestsInside, park.People.AdvisorFacts().GuestsNeedingToilet);
			var guests = park.People.Peeps;
			Assert.AreEqual( 13, guests.Count );
			Assert.IsTrue( Peep.CountsOn( park.State.Record( 60, 60 ).Type ) );
			var (ox, oy) = (-1, -1);
			for ( var y = 0; y < ParkWorld.MapSize && ox < 0; ++y )
				for ( var x = 0; x < ParkWorld.MapSize && ox < 0; ++x )
					if ( !Peep.CountsOn( park.State.Record( x, y ).Type ) ) (ox, oy) = (x, y);
			Assert.IsTrue( ox >= 0, "a cell that is not the park's" );

			foreach ( var guest in guests )
			{
				guest.Navigator.Position = FixedVector.AtCell( 60, 60 );
				guest.Toilet = 99f;
				guest.Hunger = 0f;
				guest.Thirst = 0f;
			}

			Assert.AreEqual( (13, 0), Read(), "with no advisor there is no level to be over" );
			park.State.AdvisorMessages = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ) );
			Assert.AreEqual( (13, 13), Read(), "the level is the advisor's own" );
			foreach ( var guest in guests )
				guest.Toilet = 60f;
			Assert.AreEqual( (13, 0), Read(), "60 is not over 60" );
			guests[0].Toilet = 61f;
			Assert.AreEqual( (13, 1), Read() );
			guests[1].Toilet = 60.99f;
			Assert.AreEqual( (13, 1), Read(), "60.99 is cut to 60" );
			guests[2].Toilet = 317f;
			guests[3].Toilet = 256f;
			Assert.AreEqual( (13, 2), Read(), "the low byte: 317 is 61 and 256 nought" );
			guests[4].Hunger = 99f;
			guests[4].Thirst = 99f;
			Assert.AreEqual( (13, 2), Read(), "hunger and thirst are not asked" );
			guests[0].Navigator.Position = FixedVector.AtCell( ox, oy );
			Assert.AreEqual( (12, 1), Read(), "a guest outside the park is neither" );
			guests[5].Navigator.Position = FixedVector.AtCell( ox, oy );
			Assert.AreEqual( (11, 1), Read() );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// What the advisor's rows 3 and 4 read of the park (0x0059c9d0, 0x0059ca00): the guests inside it whose thirst
	/// (<c>0x004c93f0</c>) or hunger (<c>0x004c9340</c>), cut to a whole number and to its low byte, is over the advisor's
	/// VisitorsThirsty.ThirstierThan or VisitorsHungry.HungrierThan, 99 each in the shipped file.
	/// </summary>
	/// <remarks><b>Mutations:</b> a guest outside counted; 99 over 99; the need rounded, not cut; the low byte not taken; the
	/// two needs or their two levels each other's; the levels nought with no advisor; the toilet's need read for either.</remarks>
	[TestMethod]
	public void TheAdvisorsParkFactsCountTheGuestsInsideOverTheThirstAndTheHunger()
	{
		var park = Open();

		try
		{
			(int Thirsty, int Hungry) Read() => (park.People.AdvisorFacts().GuestsThirsty, park.People.AdvisorFacts().GuestsHungry);
			var guests = park.People.Peeps;
			Assert.AreEqual( 13, guests.Count );
			var (ox, oy) = (-1, -1);
			for ( var y = 0; y < ParkWorld.MapSize && ox < 0; ++y )
				for ( var x = 0; x < ParkWorld.MapSize && ox < 0; ++x )
					if ( !Peep.CountsOn( park.State.Record( x, y ).Type ) ) (ox, oy) = (x, y);
			Assert.IsTrue( ox >= 0, "a cell that is not the park's" );

			foreach ( var guest in guests )
			{
				guest.Navigator.Position = FixedVector.AtCell( 60, 60 );
				guest.Thirst = 100f;
				guest.Hunger = 100f;
				guest.Toilet = 100f;
			}

			Assert.AreEqual( (0, 0), Read(), "with no advisor there is no level to be over" );
			var settings = new SettingsFile( "Advisor/Advisor.sam" );
			park.State.AdvisorMessages = new ParkAdvisorMessages( settings );
			Assert.AreEqual( (99, 99), (park.State.AdvisorMessages.ThirstierThan, park.State.AdvisorMessages.HungrierThan) );
			Assert.AreEqual( (13, 13), Read(), "100 is over 99" );
			foreach ( var guest in guests )
			{
				guest.Thirst = 99.99f;
				guest.Hunger = 99f;
			}

			Assert.AreEqual( (0, 0), Read(), "99.99 is cut to 99, and 99 is not over 99" );
			guests[0].Thirst = 100f;
			guests[1].Thirst = 355f;
			guests[2].Thirst = 256f;
			Assert.AreEqual( (1, 0), Read(), "the low byte: 355 is 99, not over, and 256 nought; the hunger is not the thirst" );
			guests[3].Thirst = 356f;
			guests[4].Hunger = 100f;
			guests[5].Hunger = 100.5f;
			Assert.AreEqual( (2, 2), Read(), "356 is 100" );
			guests[1].Toilet = 0f;
			Assert.AreEqual( (2, 2), Read(), "the toilet need is not asked" );

			// Each level is its own key's: the thirst's at 98 counts a guest at 99 thirst, and the hunger's stays.
			var index = settings.Entries.FindIndex( pair => pair.Key == "VisitorsThirsty.ThirstierThan" );
			settings.Entries[index] = new SettingsPair( "VisitorsThirsty.ThirstierThan", "98" );
			Assert.AreEqual( (12, 2), Read(), "every guest at 99.99, 99 cut, is over 98 but the one at 256" );
			guests[0].Navigator.Position = FixedVector.AtCell( ox, oy );
			guests[4].Navigator.Position = FixedVector.AtCell( ox, oy );
			Assert.AreEqual( (10, 1), Read(), "a guest outside the park is neither, the second one thirsty too" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// What the advisor's row 14 reads of the park (0x0059ce30): the guests inside it under 30 happiness, each one's
	/// cut to a whole number and to its low byte (<c>FUN_004c9750</c> asked with 30), beside the guests inside.
	/// </summary>
	/// <remarks><b>Mutations:</b> a guest outside counted; 30 counted; the happiness rounded, not cut; the low byte not taken; another need read for the happiness.</remarks>
	[TestMethod]
	public void TheAdvisorsParkFactsCountTheUnhappyGuestsInside()
	{
		var park = Open();

		try
		{
			(int Inside, int Unhappy) Read() => (park.People.AdvisorFacts().GuestsInside, park.People.AdvisorFacts().GuestsUnhappy);
			var guests = park.People.Peeps;
			Assert.AreEqual( 13, guests.Count );
			var (ox, oy) = (-1, -1);
			for ( var y = 0; y < ParkWorld.MapSize && ox < 0; ++y )
				for ( var x = 0; x < ParkWorld.MapSize && ox < 0; ++x )
					if ( !Peep.CountsOn( park.State.Record( x, y ).Type ) ) (ox, oy) = (x, y);

			foreach ( var guest in guests )
			{
				guest.Navigator.Position = FixedVector.AtCell( 60, 60 );
				guest.Happiness = 30f;
				guest.Toilet = 0f;
			}

			Assert.AreEqual( (13, 0), Read(), "30 is not under 30" );
			guests[0].Happiness = 29.99f;
			Assert.AreEqual( (13, 1), Read(), "29.99 is cut to 29" );
			guests[1].Happiness = 285f;
			guests[2].Happiness = 256f;
			Assert.AreEqual( (13, 3), Read(), "the low byte: 285 is 29 and 256 nought" );
			guests[3].Happiness = -1f;
			Assert.AreEqual( (13, 3), Read(), "-1 is 255" );
			guests[4].Toilet = 99f;
			guests[4].Thirst = 0f;
			Assert.AreEqual( (13, 3), Read(), "the toilet need is not asked" );
			guests[0].Navigator.Position = FixedVector.AtCell( ox, oy );
			Assert.AreEqual( (12, 2), Read(), "a guest outside the park is neither" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// What row 5's score function reads of the stock park (<c>FUN_004c9960</c> asked for type 4,
	/// <c>FUN_004ca3a0</c> handed <c>StaffHireMechanics1.PoorerStateThan</c>, 25): its one mechanic, none once he is
	/// fired, and its rides worn: the Belly Bounce once its State of repair's whole number is under 25, or once it
	/// is broken down whatever its repair, and never the Jungle Spray, a sideshow, however worn. And what row 6's
	/// reads (<c>FUN_004ca310</c>): its rides broken down, the Belly Bounce in state 1 and in no other, however
	/// worn, and never the Jungle Spray.
	/// </summary>
	/// <remarks><b>Mutations:</b> a ride at the limit counted; the repair not cut to a whole number; a broken ride not counted, or counted twice; a condemned ride counted; a sideshow counted; the limit nought; every member of staff a mechanic; the fired mechanic still counted; a worn ride counted broken; a condemned ride or a broken sideshow counted broken; the broken ride not counted.</remarks>
	[TestMethod]
	public void TheAdvisorsParkFactsCountTheMechanicsAndTheWornRides()
	{
		var park = Open();

		try
		{
			(int Mechanics, int Worn) Read() => (park.People.AdvisorFacts().Mechanics, park.People.AdvisorFacts().RidesWorn);
			int Broken() => park.People.AdvisorFacts().RidesBroken;
			void Wear( int thing, float repair, int? state = null )
			{
				Assert.IsTrue( park.State.TryObject( thing, out var worn ) );
				park.State.ReplaceObject( worn with { StateOfRepair = repair, State = state ?? worn.State } );
			}

			Assert.AreEqual( (1, 0), Read(), "as shipped" );
			Wear( BellyBounce, 3f );
			Assert.AreEqual( (1, 0), Read(), "with no advisor there is no limit to be under" );
			park.State.AdvisorMessages = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ) );
			Assert.AreEqual( (1, 1), Read(), "the limit is the advisor's own" );
			Wear( BellyBounce, 25.0f );
			Assert.AreEqual( (1, 0), Read(), "25 is not under 25" );
			Wear( BellyBounce, 25.9f );
			Assert.AreEqual( (1, 0), Read(), "nor is 25.9, cut to 25" );
			Wear( BellyBounce, 24.99f );
			Assert.AreEqual( (1, 1), Read(), "24.99 is cut to 24" );
			Wear( 14, 3f );
			Assert.AreEqual( (1, 1), Read(), "the Jungle Spray is a sideshow" );
			Assert.AreEqual( 0, Broken(), "a worn ride is not a broken one" );
			Assert.IsTrue( park.State.TryObject( BellyBounce, out var bounce ) );
			Wear( BellyBounce, 24f, ParkRideChoice.StateRefusedOne );
			Assert.AreEqual( (1, 1), Read(), "worn and broken down is one ride" );
			Assert.AreEqual( 1, Broken() );
			Wear( BellyBounce, 100f );
			Assert.AreEqual( (1, 1), Read(), "broken down, whatever its repair" );
			Assert.AreEqual( 1, Broken() );
			Wear( 14, 3f, ParkRideChoice.StateRefusedOne );
			Assert.AreEqual( 1, Broken(), "a broken sideshow is no ride" );
			Wear( BellyBounce, 100f, ParkRideChoice.StateRefusedFour );
			Assert.AreEqual( (1, 0), Read(), "condemned is not broken down" );
			Assert.AreEqual( 0, Broken() );
			Wear( BellyBounce, 100f, bounce.State );
			Assert.AreEqual( (1, 0), Read() );

			Assert.IsTrue( park.People.Fire( Mechanic ) );
			Assert.AreEqual( (0, 0), Read(), "fired" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// What the advisor's rows 238 to 257 read of the park: each shop and sideshow in the chain's order, its outlet by its
	/// item, its age in whole days and its rings' last thirty days. The shipped park's are the drinks stall and the
	/// Jungle Spray, 32 days old on the file's day (the running game's `outlets` read the same), neither with a finished
	/// day of customers; a day's three customers and two walk-aways of the drinks stall make its share 66, and its satisfaction of 90 over its thirty days 53.
	/// </summary>
	/// <remarks><b>Mutations:</b> an outlet left out or a ride let in; the walk-aways read from the customers ring; today's
	/// counts added before the day is finished; the satisfaction another window's.</remarks>
	[TestMethod]
	public void TheAdvisorsParkFactsNameTheShopsAndSideshows()
	{
		var park = Open();

		try
		{
			var drinks = ParkAdvisorMessages.Outlet.Drinks;
			CollectionAssert.AreEqual( new ParkAdvisorMessages.OutletFigures[] { new( 16, drinks, 32, 0, 0, 50 ), new( 14, ParkAdvisorMessages.Outlet.Sideshows, 32, 0, 0, 50 ) },
				park.People.AdvisorFacts().Outlets!.All.ToArray() );

			var rings = park.State.RingsFor( 16 );
			for ( var i = 0; i < 3; ++i ) rings.CountCustomer();
			rings.CountWalkAway();
			rings.CountWalkAway();
			rings.Satisfy( 90 );
			Assert.AreEqual( new ParkAdvisorMessages.OutletFigures( 16, drinks, 32, 0, 0, 50 ), park.People.AdvisorFacts().Outlets!.All[0], "today's are not counted" );
			rings.Roll();
			var stall = park.People.AdvisorFacts().Outlets!.All[0];
			Assert.AreEqual( (new ParkAdvisorMessages.OutletFigures( 16, drinks, 32, 3, 2, 53 ), 66), (stall, stall.WalkAwayShare) );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// The advisor's shop and sideshow facts add the newest thirty finished days of customers and walk-aways, not one
	/// fewer, not one more (<c>0x004cabbc</c>..<c>0x004cabcf</c>, <c>ebx = 0x1e</c>): thirty-one days rolled with a different
	/// count each, so the oldest day is the one that is left out.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>LastDays( 1 )</c> or <c>LastDays( 29 )</c> for either count in <c>ParkPeople.AdvisorFacts</c>.</remarks>
	[TestMethod]
	public void TheAdvisorsOutletFactsAddTheNewestThirtyDays()
	{
		var park = Open();

		try
		{
			var rings = park.State.RingsFor( 16 );

			// Day d (1 oldest, 31 newest) has d customers and (d * 7) % 5 + 1 walk-aways.
			for ( var day = 1; day <= 31; ++day )
			{
				for ( var i = 0; i < day; ++i ) rings.CountCustomer();
				for ( var i = 0; i < (day * 7) % 5 + 1; ++i ) rings.CountWalkAway();
				rings.Roll();
			}

			var stall = park.People.AdvisorFacts().Outlets!.All[0];
			Assert.AreEqual( (495, 90), (stall.Customers, stall.WalkAways), "days 2 to 31: 495 customers, 90 walk-aways" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// What the advisor's row 1 reads of the shipped park (<c>FUN_004c9050</c>, <c>FUN_004ca8e0</c>,
	/// <c>FUN_004c91c0</c>): its door, its things of a ride's item and of a sideshow's, and the guests in state 3
	/// standing on either ticket booth's cell: not one beside a booth, and not one on a booth judging the fee.
	/// And what rows 260 to 262 read (<c>FUN_004ca9b0</c>, <c>FUN_004c9ee0</c>, <c>FUN_004c9f90</c>): its shops and
	/// toilets, and how many of the shops, sideshows and toilets are shut, none in the open park and all once the
	/// door has shut them. And what row 259 reads: how many of its rides are shut, the one ride once the door has
	/// shut it, and still that one with the sideshow and a toilet opened again. And what row 263 reads
	/// (<c>FUN_004ca030</c>, <c>FUN_004ca0c0</c>): its one staff room, which the door leaves open.
	/// </summary>
	/// <remarks><b>Mutations:</b> every waiting guest counted wherever they stand; every guest on a booth counted; one booth alone; the door's answer turned round; the shops counted as rides; the shut rides not counted, or counted with the sideshows'.</remarks>
	[TestMethod]
	public void TheAdvisorsParkFactsCountTheGuestsWaitingOnTheBoothCells()
	{
		var park = Open();

		try
		{
			var random = new Random( 1 );
			var guests = park.People.Peeps;
			foreach ( var guest in guests )
			{
				guest.Navigator.Position = FixedVector.AtCell( 60, 60 );
				guest.SetState( PeepState.Deciding, 800, random );
			}

			Assert.AreEqual( new ParkAdvisorMessages.ParkFacts( false, StockRides, StockSideshows, 0, StockShops, StockToilets, StaffRooms: StockStaffRooms, Mechanics: 1, DaysSinceStaffHired: StockDaysSinceStaffHired, RideKindsOnOffer: StockRideKindsOnOffer, RideKindsStanding: 1, DaysSinceObjectBuilt: StockDaysSinceObjectBuilt, ShopKindsOnOffer: StockShopKindsOnOffer, ShopKindsStanding: 1, Balance: StockBalance, SideshowKindsOnOffer: StockSideshowKindsOnOffer, SideshowKindsStanding: 1, Features: StockFeatures, FeatureKindsOnOffer: StockFeatureKindsOnOffer, FeatureKindsStanding: StockFeatureKindsStanding, GuestsInside: park.People.GateGuestCensus, Researchers: 1, FeatureProjectsUnfinished: StockFeatureProjectsUnfinished, SideshowProjectsUnfinished: 2, RideProjectsUnfinished: StockRideProjectsUnfinished, SideshowResearchRate: StockSideshowResearchRate, ResearchAbility: StockResearchAbility, MostResearchAbility: StockMostResearchAbility, Staff: StockStaffTraining, DevelopedCells: StockDevelopedCells, LifetimeVisitors: StockLifetimeVisitors, Happiness: 50f, LoadsCalled: StockLoadsCalled, MechanicsWithAnArea: 1, Entrances: 8, EntrancesPatrolled: 1 ), park.People.AdvisorFacts() with { Outlets = null } );

			void Put( int index, int x, int y, PeepState state )
			{
				guests[index].Navigator.Position = FixedVector.AtCell( x, y );
				guests[index].SetState( state, 800, random );
			}

			Put( 0, 47, 13, PeepState.WaitingForOpening );
			Put( 1, 48, 13, PeepState.WaitingForOpening );
			Put( 2, 48, 13, PeepState.WaitingForOpening );
			Put( 3, 48, 12, PeepState.WaitingForOpening );
			Put( 4, 47, 13, PeepState.JudgingTheFee );
			Assert.AreEqual( 3, park.People.WaitingToOpen );

			park.State.SetParkClosed( true );
			Assert.AreEqual( new ParkAdvisorMessages.ParkFacts( true, StockRides, StockSideshows, 3, StockShops, StockToilets,
				ShopsShut: StockShops, SideshowsShut: StockSideshows, ToiletsShut: StockToilets, RidesShut: StockRides, StaffRooms: StockStaffRooms, Mechanics: 1, DaysSinceStaffHired: StockDaysSinceStaffHired, RideKindsOnOffer: StockRideKindsOnOffer, RideKindsStanding: 1, DaysSinceObjectBuilt: StockDaysSinceObjectBuilt, ShopKindsOnOffer: StockShopKindsOnOffer, ShopKindsStanding: 1, Balance: StockBalance, SideshowKindsOnOffer: StockSideshowKindsOnOffer, SideshowKindsStanding: 1, Features: StockFeatures, FeatureKindsOnOffer: StockFeatureKindsOnOffer, FeatureKindsStanding: StockFeatureKindsStanding, GuestsInside: park.People.GateGuestCensus, Researchers: 1, FeatureProjectsUnfinished: StockFeatureProjectsUnfinished, SideshowProjectsUnfinished: 2, RideProjectsUnfinished: StockRideProjectsUnfinished, SideshowResearchRate: StockSideshowResearchRate, ResearchAbility: StockResearchAbility, MostResearchAbility: StockMostResearchAbility, Staff: StockStaffTraining, DevelopedCells: StockDevelopedCells, LifetimeVisitors: StockLifetimeVisitors, Happiness: 0f, LoadsCalled: StockLoadsCalled, MechanicsWithAnArea: 1, Entrances: 8, EntrancesPatrolled: 1 ), park.People.AdvisorFacts() with { Outlets = null } );

			// One toilet opened again, and the sideshow: the door's others stay shut.
			park.People.SetRideClosed( 22, closed: false );
			park.People.SetRideClosed( 14, closed: false );
			Assert.AreEqual( new ParkAdvisorMessages.ParkFacts( true, StockRides, StockSideshows, 3, StockShops, StockToilets,
				ShopsShut: StockShops, SideshowsShut: 0, ToiletsShut: StockToilets - 1, RidesShut: StockRides, StaffRooms: StockStaffRooms, Mechanics: 1, DaysSinceStaffHired: StockDaysSinceStaffHired, RideKindsOnOffer: StockRideKindsOnOffer, RideKindsStanding: 1, DaysSinceObjectBuilt: StockDaysSinceObjectBuilt, ShopKindsOnOffer: StockShopKindsOnOffer, ShopKindsStanding: 1, Balance: StockBalance, SideshowKindsOnOffer: StockSideshowKindsOnOffer, SideshowKindsStanding: 1, Features: StockFeatures, FeatureKindsOnOffer: StockFeatureKindsOnOffer, FeatureKindsStanding: StockFeatureKindsStanding, GuestsInside: park.People.GateGuestCensus, Researchers: 1, FeatureProjectsUnfinished: StockFeatureProjectsUnfinished, SideshowProjectsUnfinished: 2, RideProjectsUnfinished: StockRideProjectsUnfinished, SideshowResearchRate: StockSideshowResearchRate, ResearchAbility: StockResearchAbility, MostResearchAbility: StockMostResearchAbility, Staff: StockStaffTraining, DevelopedCells: StockDevelopedCells, LifetimeVisitors: StockLifetimeVisitors, Happiness: 0f, LoadsCalled: StockLoadsCalled, MechanicsWithAnArea: 1, Entrances: 8, EntrancesPatrolled: 1 ), park.People.AdvisorFacts() with { Outlets = null } );

			// The door left the staff room open; shut by itself, it alone is one more shut.
			var before = park.People.AdvisorFacts();
			park.People.SetRideClosed( StaffRoom, closed: true );
			Assert.AreEqual( before with { StaffRoomsShut = 1 }, park.People.AdvisorFacts() );
			StringAssert.EndsWith( park.People.GateDescription(), " waiting 3" );

			// The loads called: the shipped ring's one, then five once the ring has gone round, whatever its slot.
			for ( var load = 0; load < 6; ++load )
				park.State.AddArrivalLoad( 10 );
			Assert.AreEqual( (1, true, 5), (park.State.ArrivalLoads.Current, park.State.ArrivalLoads.Wrapped, park.People.AdvisorFacts().LoadsCalled) );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// What the advisor's row 113 reads of the park (<c>FUN_004ccb00</c> asked with nought): the first thing a
	/// guest may choose that has a queue whose back is not joined to a path, none in the shipped park, the Belly
	/// Bounce once its queue's back cell links only to the cell ahead, none while the queue tool is armed for it,
	/// and none for a thing with no queue whose entrance has no path before it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the connected queue answered; the tool's thing not passed over, or every thing passed over while a tool is armed; a thing with no queue answered; a thing no guest may choose answered; the last found answered, not the first.</remarks>
	[TestMethod]
	public void TheAdvisorsParkFactsNameARideWhoseQueueIsNotJoined()
	{
		var park = Open();

		try
		{
			Assert.AreEqual( 0, park.People.AdvisorFacts().UnjoinedQueue, "every queue joined as shipped" );

			// A thing with no queue and no path before its entrance is not asked.
			var spray = Thing( park, 14 );
			Assert.IsFalse( spray.HasQueuePath );
			var facing = spray.Angle switch { 0 => 0x10, 90 => 0x04, 180 => 0x01, _ => 0x40 };
			var (x, y) = MapStep.CellAt( spray.EntryPos );
			var (bx, by) = ParkBuilding.Step( x, y, CellEdge.Opposite( facing ) );
			var beyond = park.State.Record( bx, by );
			park.State.SetRecord( bx, by, beyond with { Type = 0 } );
			Assert.IsFalse( ParkRideOperation.BackOfQueueConnected( park.World, spray ) );
			Assert.AreEqual( 0, park.People.AdvisorFacts().UnjoinedQueue );
			park.State.SetRecord( bx, by, beyond );

			var back = park.State.Record( BackX, BackY );
			park.State.SetRecord( BackX, BackY, back with { Neighbours = 0x04 } );
			Assert.AreEqual( BellyBounce, park.People.AdvisorFacts().UnjoinedQueue );
			Assert.AreEqual( park.People.AdvisorFacts() with { UnjoinedQueue = 0, Outlets = null },
				new ParkAdvisorMessages.ParkFacts( false, StockRides, StockSideshows, park.People.WaitingToOpen, StockShops, StockToilets, StaffRooms: StockStaffRooms, Mechanics: 1, DaysSinceStaffHired: StockDaysSinceStaffHired, RideKindsOnOffer: StockRideKindsOnOffer, RideKindsStanding: 1, DaysSinceObjectBuilt: StockDaysSinceObjectBuilt, ShopKindsOnOffer: StockShopKindsOnOffer, ShopKindsStanding: 1, Balance: StockBalance, SideshowKindsOnOffer: StockSideshowKindsOnOffer, SideshowKindsStanding: 1, Features: StockFeatures, FeatureKindsOnOffer: StockFeatureKindsOnOffer, FeatureKindsStanding: StockFeatureKindsStanding, GuestsInside: park.People.GateGuestCensus, Researchers: 1, FeatureProjectsUnfinished: StockFeatureProjectsUnfinished, SideshowProjectsUnfinished: 2, RideProjectsUnfinished: StockRideProjectsUnfinished, SideshowResearchRate: StockSideshowResearchRate, ResearchAbility: StockResearchAbility, MostResearchAbility: StockMostResearchAbility, Staff: StockStaffTraining, DevelopedCells: StockDevelopedCells, LifetimeVisitors: StockLifetimeVisitors, Happiness: park.People.GuestsInsideHappiness, LoadsCalled: StockLoadsCalled, MechanicsWithAnArea: 1, Entrances: 8, EntrancesPatrolled: 1 ), "nothing else moved" );

			// The queue tool armed for it passes it over; armed for another thing, or put away, it does not.
			ParkBuildMode.Arm( ParkBuildMode.Queue, BellyBounce );
			Assert.AreEqual( 0, park.People.AdvisorFacts().UnjoinedQueue );
			ParkBuildMode.Arm( ParkBuildMode.Queue, 14 );
			Assert.AreEqual( BellyBounce, park.People.AdvisorFacts().UnjoinedQueue );
			ParkBuildMode.Arm( ParkBuildMode.Path );
			Assert.AreEqual( BellyBounce, park.People.AdvisorFacts().UnjoinedQueue );
			ParkBuildMode.Disarm();
			Assert.AreEqual( BellyBounce, park.People.AdvisorFacts().UnjoinedQueue );

			// With two unjoined, the first of the chain is the one named.
			var sprayFlags = Thing( park, 14 ).Flags;
			Change( park, 14, thing => thing with { Flags = (ushort)(thing.Flags | 0x08) } );
			Assert.IsTrue( Thing( park, 14 ).HasQueuePath );
			var (sprayBack, _) = ParkRideChoice.QueueCellsFor( park.World, Thing( park, 14 ) );
			Assert.AreNotEqual( 0, sprayBack );
			var (sx, sy) = MapStep.CellAt( sprayBack );
			var sprayCell = park.State.Record( sx, sy );
			park.State.SetRecord( sx, sy, sprayCell with { Neighbours = sprayCell.Direction } );
			Assert.IsFalse( ParkRideOperation.BackOfQueueConnected( park.World, Thing( park, 14 ) ), "the Spray, given a queue, joins nothing" );
			var chain = park.State.ObjectsInChainOrder().Where( thing => thing.ThingId is BellyBounce or 14 ).Select( thing => thing.ThingId ).ToArray();
			Assert.AreEqual( 2, chain.Length );
			Assert.AreEqual( chain[0], park.People.AdvisorFacts().UnjoinedQueue );
			ParkBuildMode.Arm( ParkBuildMode.Queue, chain[0] );
			Assert.AreEqual( chain[1], park.People.AdvisorFacts().UnjoinedQueue, "the tool's thing passed over, the next is named" );
			ParkBuildMode.Disarm();
			Change( park, 14, thing => thing with { Flags = sprayFlags } );
			park.State.SetRecord( sx, sy, sprayCell );

			// A thing no guest may choose is not asked.
			Change( park, BellyBounce, thing => thing with { Flags = (ushort)(thing.Flags & ~0x04) } );
			Assert.IsFalse( Thing( park, BellyBounce ).IsVisitable );
			Assert.AreEqual( 0, park.People.AdvisorFacts().UnjoinedQueue );

			park.State.SetRecord( BackX, BackY, back );
		}
		finally
		{
			ParkBuildMode.Disarm();
			Close( park );
		}
	}

	/// <summary>The shipped Lost Kingdom park's things of a ride's item and of a sideshow's: the Belly Bounce and the Jungle Spray.</summary>
	private const int StockRides = 1, StockSideshows = 1;
	/// <summary>Its things of a shop's item, the Drinks Shop, and its toilets, three Small Toilets; the door shuts each.</summary>
	private const int StockShops = 1, StockToilets = 3;
	/// <summary>Its staff rooms, the one Staff Room, thing 20, which the door does not shut.</summary>
	private const int StockStaffRooms = 1, StaffRoom = 20;
	/// <summary>The shipped park is entered on tick 755 and its newest member was made on tick 648: 401,250 calendar seconds, four whole days.</summary>
	private const int StockDaysSinceStaffHired = 4;
	/// <summary>The shipped park's kinds of ride on offer: the Belly Bounce, the Crazy Ape, the Rocky Racers and the Aztec Mayhem, the four researched that cost no golden ticket.</summary>
	private const int StockRideKindsOnOffer = 4;
	/// <summary>Its newest object was made on tick 604: 566,250 calendar seconds before tick 755, six whole days.</summary>
	private const int StockDaysSinceObjectBuilt = 6;
	/// <summary>The shipped park's kinds of shop on offer: the Drinks Shop, the Burger Shop and the Balloon Shop, the three researched.</summary>
	private const int StockShopKindsOnOffer = 3;
	/// <summary>The shipped park's bank balance, which row 74's score reads.</summary>
	private const int StockBalance = 87987;
	/// <summary>The shipped park's kinds of sideshow on offer: the Jungle Spray and the Strength Bird, the two researched of Lost Kingdom's five.</summary>
	private const int StockSideshowKindsOnOffer = 2;
	/// <summary>The shipped park's features: eight things of five kinds, and eight kinds researched (the toilet, 1402, stands three times and the item 1413 twice).</summary>
	private const int StockFeatures = 8;
	private const int StockFeatureKindsOnOffer = 8;
	private const int StockFeatureKindsStanding = 5;
	// The lab's projects of a feature's kind in the shipped park, none of them done.
	private const int StockFeatureProjectsUnfinished = 11;
	// The lab's projects of a ride's kind in the shipped park, the one put to work (30 points of 600) not done either.
	private const int StockRideProjectsUnfinished = 8;

	// Instant Action's Lost Kingdom: the one researcher is of grade 2, ability 12; six of the top grade's 20 are the
	// most; the lab's sideshow output of 14 at 10 per cent is 1.
	private const int StockResearchAbility = 12;
	private const int StockMostResearchAbility = 120;
	private const int StockSideshowResearchRate = 1;
	// The shipped park's staff: one member a kind, no training budget, grade 3 but for the researcher's 2.
	private static readonly ParkAdvisorMessages.StaffTraining StockStaffTraining = new( new( 1, 0, 3 ), new( 1, 0, 3 ), new( 1, 0, 3 ), new( 1, 0, 3 ), new( 1, 0, 2 ) );
	// The shipped park's developed cells, the advisor's row 10's (ParkDevelopedCellsTests).
	private const int StockDevelopedCells = 126;

	/// <summary>The shipped park's lifetime visitors, and the arrival loads its ring holds: one, the slot nought and not wrapped.</summary>
	private const int StockLifetimeVisitors = 8, StockLoadsCalled = 1;
	private const int JungleSpray = 1303, LaughingHyenas = 1304, StrengthBird = 1305, GiantPuzzle = 1307, Arcade = 1309;
	private const int DrinksShop = 1203, BurgerShop = 1207, BalloonShop = 1209, IceCreamShop = 1206, FriesShop = 1212;
	private const int Toilet = 1402;
	private const int CrazyApe = 1101, RockyRacers = 1102, AztecMayhem = 1104, Eruption = 1112, SunGod = 1106;

	/// <summary>
	/// What the advisor's row 60 reads of the park (0x004ca450, 0x004ca7b0, 0x004c9070): the kinds of ride on offer
	/// are the catalogue's items of a ride's kind that the park has researched and that cost no golden ticket, in
	/// the regular catalogue and Instant Action's alike; the kinds standing are those of them a thing stands of,
	/// however many (the park's own count of each item, the control record's); and the days are counted from the
	/// newest object's making.
	/// </summary>
	/// <remarks><b>Mutations:</b> the golden-ticket rides on offer, or counted standing; the unresearched on offer; every ride a kind; a kind sold still standing; the sideshows' kinds counted; the days the staff's; the research not kept, or every item's own file's.</remarks>
	private static int AztecMayhemResearchCost( Park park )
		=> park.Catalogue.TryGet( AztecMayhem, out var item ) ? item.ResearchCost : -1;

	[TestMethod]
	public void TheAdvisorCountsTheKindsOfRideOnOfferAndStanding()
	{
		var park = Open();

		try
		{
			var research = new ParkResearch( park.World.ObjectControlRecords, park.Catalogue );
			var rides = park.Catalogue.All.Where( item => item.UiType == ItemDescriptionFile.Ride ).ToArray();
			CollectionAssert.AreEquivalent( new[] { 1100, CrazyApe, RockyRacers, AztecMayhem },
				rides.Where( item => item.GoldenTicketCost == 0 && research.IsResearched( item.Id ) ).Select( item => item.Id ).ToArray() );
			Assert.IsTrue( park.Catalogue.TryGet( Eruption, out var eruption ) && eruption.GoldenTicketCost > 0 && research.IsResearched( Eruption ), "researched, and a golden ticket's" );
			Assert.IsTrue( park.Catalogue.TryGet( SunGod, out var sun ) && sun.GoldenTicketCost == 0 && !research.IsResearched( SunGod ), "no golden ticket, and not researched" );
			var instant = new ParkItemCatalogue( Theme, data, instantAction: true );
			Assert.AreEqual( 4, instant.All.Count( item => item.UiType == ItemDescriptionFile.Ride && item.GoldenTicketCost == 0
				&& new ParkResearch( park.World.ObjectControlRecords, instant ).IsResearched( item.Id ) ), "Instant Action's catalogue offers the same four" );

			(int OnOffer, int Standing, int Rides, int Days) Read()
			{
				var facts = park.People.AdvisorFacts();
				return (facts.RideKindsOnOffer, facts.RideKindsStanding, facts.Rides, facts.DaysSinceObjectBuilt);
			}

			Assert.AreEqual( (4, 1, 1, StockDaysSinceObjectBuilt), Read() );
			Assert.AreNotEqual( park.State.DaysSinceStaffHired, park.State.DaysSinceObjectBuilt );

			// A second Belly Bounce is a ride more and no kind more; an Ape is a kind; a golden-ticket ride is not.
			var bounce = Thing( park, BellyBounce );
			park.State.SetGameTick( 900 );
			park.State.AddObject( bounce with { ThingId = park.State.NextThingId() } );
			Assert.AreEqual( (4, 1, 2, 0), Read(), "the days are counted from the newest build" );
			park.State.AddObject( bounce with { ThingId = park.State.NextThingId(), CatalogueId = CrazyApe } );
			Assert.AreEqual( (4, 2, 3, 0), Read() );
			park.State.AddObject( bounce with { ThingId = park.State.NextThingId(), CatalogueId = Eruption } );
			Assert.AreEqual( (4, 2, 4, 0), Read() );
			park.State.AddObject( bounce with { ThingId = park.State.NextThingId(), CatalogueId = SunGod } );
			Assert.AreEqual( (4, 3, 5, 0), Read(), "a kind standing that is not on offer is still a kind standing" );
			park.State.SetGameTick( 900 + 692 );
			Assert.AreEqual( 30, Read().Days );
			park.State.SetGameTick( 900 + 691 );
			Assert.AreEqual( 29, Read().Days );

			// A sale takes a kind away with its last thing, and no sooner.
			var apes = park.State.Objects.Where( thing => thing.CatalogueId == CrazyApe ).Select( thing => thing.ThingId ).ToArray();
			park.State.AddObject( bounce with { ThingId = park.State.NextThingId(), CatalogueId = CrazyApe } );
			Assert.AreEqual( (4, 3, 6), (Read().OnOffer, Read().Standing, Read().Rides) );
			Assert.IsTrue( park.State.RemoveObject( apes[0] ) );
			Assert.AreEqual( (4, 3, 5), (Read().OnOffer, Read().Standing, Read().Rides) );
			Assert.IsTrue( park.State.RemoveObject( park.State.Objects.Single( thing => thing.CatalogueId == CrazyApe ).ThingId ) );
			Assert.AreEqual( (4, 2, 4), (Read().OnOffer, Read().Standing, Read().Rides), "a kind built once and sold stands no more" );

			var bare = new ParkPeople( park.World, new ParkBalance( Theme, easyMode: true ), gateStatus: null, new ParkState( park.World ) );
			Assert.AreEqual( (0, 0), (bare.AdvisorFacts().RideKindsOnOffer, bare.AdvisorFacts().RideKindsStanding), "no catalogue handed in" );
			bare.Delete();

			// The save's word is the flag: the Aztec Mayhem's file asks no research, and a file that holds it unresearched offers three.
			using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
			var bytes = new SaveReader( stream ).ReadFile();
			var ids = park.World.ObjectControlRecords.Select( record => record.ItemId ).ToArray();
			var table = Enumerable.Range( 0, bytes.Length - ids.Length * 32 )
				.Single( at => Enumerable.Range( 0, 10 ).All( i => BitConverter.ToInt32( bytes, at + 32 * i ) == ids[i] ) );
			Assert.AreEqual( 0, AztecMayhemResearchCost( park ) );
			bytes[table + 32 * Array.IndexOf( ids, AztecMayhem ) + 0x10] = 0;
			var held = new ParkWorld( bytes );
			var fewer = new ParkPeople( held, new ParkBalance( Theme, easyMode: true ), gateStatus: null, new ParkState( held ), park.Catalogue );
			Assert.AreEqual( (3, 1), (fewer.AdvisorFacts().RideKindsOnOffer, fewer.AdvisorFacts().RideKindsStanding) );
			fewer.Delete();
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// What the advisor's row 71 reads of the park (0x004ca510, 0x004ca7b0 asked for a shop's): the kinds of shop
	/// on offer are the catalogue's items of a shop's kind that the park has researched, a golden ticket's among
	/// them; the kinds standing are the shop items a thing stands of that cost no golden ticket, researched or not;
	/// the shops are the things of a shop's item, and the days are the newest build's.
	/// </summary>
	/// <remarks><b>Mutations:</b> the unresearched on offer; a golden-ticket shop not on offer, or counted standing; an unresearched kind not standing; every shop a kind; a kind sold still standing; the rides' kinds counted, or the rides' count handed over; the two counts changed over.</remarks>
	[TestMethod]
	public void TheAdvisorCountsTheKindsOfShopOnOfferAndStanding()
	{
		var park = Open();

		try
		{
			var research = new ParkResearch( park.World.ObjectControlRecords, park.Catalogue );
			var shops = park.Catalogue.All.Where( item => item.UiType == ParkRideOperation.ShopUiType ).ToArray();
			CollectionAssert.AreEquivalent( new[] { DrinksShop, BurgerShop, BalloonShop },
				shops.Where( item => research.IsResearched( item.Id ) ).Select( item => item.Id ).ToArray() );
			Assert.IsTrue( shops.All( item => item.GoldenTicketCost == 0 ), "no Lost Kingdom shop costs a golden ticket" );
			Assert.IsTrue( shops.Any( item => item.Id == IceCreamShop ) && shops.Any( item => item.Id == FriesShop ) );
			var instant = new ParkItemCatalogue( Theme, data, instantAction: true );
			Assert.AreEqual( 3, instant.All.Count( item => item.UiType == ParkRideOperation.ShopUiType
				&& new ParkResearch( park.World.ObjectControlRecords, instant ).IsResearched( item.Id ) ), "Instant Action's catalogue offers the same three" );

			(int OnOffer, int Standing, int Shops) Read()
			{
				var facts = park.People.AdvisorFacts();
				return (facts.ShopKindsOnOffer, facts.ShopKindsStanding, facts.Shops);
			}

			Assert.AreEqual( (3, 1, 1), Read() );
			Assert.AreNotEqual( (park.People.AdvisorFacts().RideKindsOnOffer, park.People.AdvisorFacts().Rides + 1), (Read().OnOffer, Read().Shops), "the rides' are other numbers" );

			// A second Drinks Shop is a shop more and no kind more; a Burger Shop is a kind; an unresearched kind standing is one too.
			var drinks = park.State.Objects.Single( thing => thing.CatalogueId == DrinksShop );
			park.State.AddObject( drinks with { ThingId = park.State.NextThingId() } );
			Assert.AreEqual( (3, 1, 2), Read() );
			park.State.AddObject( drinks with { ThingId = park.State.NextThingId(), CatalogueId = BurgerShop } );
			Assert.AreEqual( (3, 2, 3), Read() );
			park.State.AddObject( drinks with { ThingId = park.State.NextThingId(), CatalogueId = IceCreamShop } );
			Assert.AreEqual( (3, 3, 4), Read(), "a kind standing that is not on offer is still a kind standing" );
			park.State.AddObject( drinks with { ThingId = park.State.NextThingId(), CatalogueId = CrazyApe } );
			Assert.AreEqual( (3, 3, 4), Read(), "a ride is no shop and no kind of one" );

			// A sale takes a kind away with its last thing, and no sooner.
			park.State.AddObject( drinks with { ThingId = park.State.NextThingId(), CatalogueId = BurgerShop } );
			var burgers = park.State.Objects.Where( thing => thing.CatalogueId == BurgerShop ).Select( thing => thing.ThingId ).ToArray();
			Assert.AreEqual( (3, 3, 5), Read() );
			Assert.IsTrue( park.State.RemoveObject( burgers[0] ) );
			Assert.AreEqual( (3, 3, 4), Read() );
			Assert.IsTrue( park.State.RemoveObject( burgers[1] ) );
			Assert.AreEqual( (3, 2, 3), Read(), "a kind built and sold stands no more" );

			// The rule on items of its own: a golden-ticket shop is on offer once researched, and is never a kind standing.
			var golden = shops.Single( item => item.Id == BalloonShop ) with { GoldenTicketCost = 1 };
			var items = shops.Where( item => item.Id != BalloonShop ).Append( golden )
				.Append( park.Catalogue.All.First( item => item.UiType == ItemDescriptionFile.Ride ) ).ToArray();
			Assert.AreEqual( (3, 1), ParkPeople.Kinds( items, ParkRideOperation.ShopUiType, research.IsResearched, id => id == DrinksShop ? 2 : 0 ) );
			Assert.AreEqual( (3, 1), ParkPeople.Kinds( items, ParkRideOperation.ShopUiType, research.IsResearched, id => id is DrinksShop or BalloonShop ? 1 : 0 ), "the golden-ticket shop stands and is no kind" );
			Assert.AreEqual( (3, 2), ParkPeople.Kinds( items, ParkRideOperation.ShopUiType, research.IsResearched, id => id is DrinksShop or FriesShop ? 1 : 0 ), "the unresearched kind stands" );
			Assert.AreEqual( (1, 0), ParkPeople.Kinds( items, ParkRideOperation.ShopUiType, id => id is BalloonShop or 1100, _ => 0 ), "the golden-ticket shop alone on offer; the ride is none" );
			Assert.AreEqual( (0, 0), ParkPeople.Kinds( [], ParkRideOperation.ShopUiType, research.IsResearched, _ => 1 ) );

			var bare = new ParkPeople( park.World, new ParkBalance( Theme, easyMode: true ), gateStatus: null, new ParkState( park.World ) );
			Assert.AreEqual( (0, 0), (bare.AdvisorFacts().ShopKindsOnOffer, bare.AdvisorFacts().ShopKindsStanding), "no catalogue handed in" );
			bare.Delete();
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// What the advisor's row 75 reads of the park (0x004ca570, 0x004ca7b0 asked for a sideshow's): the kinds of
	/// sideshow on offer are the catalogue's items of a sideshow's kind that the park has researched, a golden
	/// ticket's among them; the kinds standing are the sideshow items a thing stands of that cost no golden
	/// ticket, researched or not; the sideshows are the things of a sideshow's item.
	/// </summary>
	/// <remarks><b>Mutations:</b> the shops' kinds counted for the sideshows', or the sideshows' for the shops'; the unresearched on offer; a golden-ticket sideshow not on offer, or counted standing; every sideshow a kind; a kind sold still standing; the two counts changed over.</remarks>
	[TestMethod]
	public void TheAdvisorCountsTheKindsOfSideshowOnOfferAndStanding()
	{
		var park = Open();

		try
		{
			var research = new ParkResearch( park.World.ObjectControlRecords, park.Catalogue );
			var sideshows = park.Catalogue.All.Where( item => item.UiType == ItemDescriptionFile.SideShow ).ToArray();
			CollectionAssert.AreEquivalent( new[] { JungleSpray, LaughingHyenas, StrengthBird, GiantPuzzle, Arcade }, sideshows.Select( item => item.Id ).ToArray(), "the regular catalogue's five" );
			Assert.IsTrue( sideshows.All( item => item.GoldenTicketCost == 0 ), "no Lost Kingdom sideshow costs a golden ticket" );
			CollectionAssert.AreEquivalent( new[] { JungleSpray, StrengthBird }, sideshows.Where( item => research.IsResearched( item.Id ) ).Select( item => item.Id ).ToArray() );
			var instant = new ParkItemCatalogue( Theme, data, instantAction: true );
			CollectionAssert.AreEquivalent( new[] { JungleSpray, LaughingHyenas, StrengthBird, GiantPuzzle },
				instant.All.Where( item => item.UiType == ItemDescriptionFile.SideShow ).Select( item => item.Id ).ToArray(), "Instant Action's catalogue leaves the Arcade out" );

			(int OnOffer, int Standing, int Sideshows) Read()
			{
				var facts = park.People.AdvisorFacts();
				return (facts.SideshowKindsOnOffer, facts.SideshowKindsStanding, facts.Sideshows);
			}

			(int OnOffer, int Standing, int Shops) Shops()
			{
				var facts = park.People.AdvisorFacts();
				return (facts.ShopKindsOnOffer, facts.ShopKindsStanding, facts.Shops);
			}

			Assert.AreEqual( (StockSideshowKindsOnOffer, 1, 1), Read() );
			Assert.AreEqual( (StockShopKindsOnOffer, 1, 1), Shops() );

			// A second Jungle Spray is a sideshow more and no kind more; another sideshow is a kind; a shop or a ride is neither.
			var spray = park.State.Objects.Single( thing => thing.CatalogueId == JungleSpray );
			park.State.AddObject( spray with { ThingId = park.State.NextThingId() } );
			Assert.AreEqual( (StockSideshowKindsOnOffer, 1, 2), Read() );
			park.State.AddObject( spray with { ThingId = park.State.NextThingId(), CatalogueId = LaughingHyenas } );
			park.State.AddObject( spray with { ThingId = park.State.NextThingId(), CatalogueId = Arcade } );
			Assert.AreEqual( (StockSideshowKindsOnOffer, 3, 4), Read(), "a kind standing counts, on offer or not" );
			Assert.AreEqual( (StockShopKindsOnOffer, 1, 1), Shops(), "a sideshow is no shop and no kind of one" );
			park.State.AddObject( spray with { ThingId = park.State.NextThingId(), CatalogueId = BurgerShop } );
			park.State.AddObject( spray with { ThingId = park.State.NextThingId(), CatalogueId = CrazyApe } );
			Assert.AreEqual( (StockSideshowKindsOnOffer, 3, 4), Read(), "a shop and a ride are no sideshow and no kind of one" );
			Assert.AreEqual( (StockShopKindsOnOffer, 2, 2), Shops() );

			// A sale takes a kind away with its last thing, and no sooner.
			park.State.AddObject( spray with { ThingId = park.State.NextThingId(), CatalogueId = LaughingHyenas } );
			var hyenas = park.State.Objects.Where( thing => thing.CatalogueId == LaughingHyenas ).Select( thing => thing.ThingId ).ToArray();
			Assert.AreEqual( (StockSideshowKindsOnOffer, 3, 5), Read() );
			Assert.IsTrue( park.State.RemoveObject( hyenas[0] ) );
			Assert.AreEqual( (StockSideshowKindsOnOffer, 3, 4), Read() );
			Assert.IsTrue( park.State.RemoveObject( hyenas[1] ) );
			Assert.AreEqual( (StockSideshowKindsOnOffer, 2, 3), Read(), "a kind built and sold stands no more" );

			// The rule on items of its own: a golden-ticket sideshow is on offer once researched, and is never a kind standing; a shop is asked for by its own kind.
			var golden = sideshows.Single( item => item.Id == GiantPuzzle ) with { GoldenTicketCost = 1 };
			var shop = park.Catalogue.All.Single( item => item.Id == DrinksShop );
			var items = sideshows.Where( item => item.Id != GiantPuzzle ).Append( golden ).Append( shop ).ToArray();
			bool Offered( int id ) => id is JungleSpray or GiantPuzzle or DrinksShop;
			Assert.AreEqual( (2, 1), ParkPeople.Kinds( items, ItemDescriptionFile.SideShow, Offered, id => id == JungleSpray ? 2 : 0 ) );
			Assert.AreEqual( (2, 1), ParkPeople.Kinds( items, ItemDescriptionFile.SideShow, Offered, id => id is JungleSpray or GiantPuzzle or DrinksShop ? 1 : 0 ), "the golden-ticket sideshow and the shop stand and are no kind" );
			Assert.AreEqual( (2, 2), ParkPeople.Kinds( items, ItemDescriptionFile.SideShow, Offered, id => id is JungleSpray or Arcade ? 1 : 0 ), "the unresearched kind stands" );
			Assert.AreEqual( (1, 0), ParkPeople.Kinds( items, ItemDescriptionFile.SideShow, id => id is GiantPuzzle or DrinksShop, _ => 0 ), "the golden-ticket sideshow alone on offer" );
			Assert.AreEqual( (1, 1), ParkPeople.Kinds( items, ParkRideOperation.ShopUiType, Offered, _ => 1 ), "the shops' count of the same items is the one shop" );
			Assert.AreEqual( (0, 0), ParkPeople.Kinds( [], ItemDescriptionFile.SideShow, Offered, _ => 1 ) );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// What the advisor's row 79 reads of the park (0x004ca5d0, 0x004ca7b0 and FUN_004ca8e0 asked for a
	/// feature's): the kinds of feature on offer are the catalogue's items of a feature's kind that the park has
	/// researched; the kinds standing are the feature items a thing stands of that cost no golden ticket,
	/// researched or not; the features are the things of a feature's item. The shipped park: eight on offer,
	/// five of them standing, eight things.
	/// </summary>
	/// <remarks><b>Mutations:</b> the sideshows' or the shops' kinds counted for the features', or the features' for theirs; the features not counted, or every thing one; the unresearched on offer; every feature a kind; a kind sold still standing; the counts changed over.</remarks>
	[TestMethod]
	public void TheAdvisorCountsTheFeaturesAndTheirKindsOnOfferAndStanding()
	{
		var park = Open();

		try
		{
			var research = new ParkResearch( park.World.ObjectControlRecords, park.Catalogue );
			var features = park.Catalogue.All.Where( item => item.UiType == ItemDescriptionFile.Feature ).ToArray();
			Assert.AreEqual( 28, features.Length, "the regular catalogue's" );
			Assert.IsTrue( features.All( item => item.GoldenTicketCost == 0 ), "no Lost Kingdom feature costs a golden ticket" );
			CollectionAssert.AreEquivalent( new[] { 1401, Toilet, 1403, 1406, 1411, 1413, 1425, 1428 }, features.Where( item => research.IsResearched( item.Id ) ).Select( item => item.Id ).ToArray() );
			CollectionAssert.AreEquivalent( new[] { Toilet, Toilet, Toilet, 1403, 1406, 1411, 1413, 1413 },
				park.State.Objects.Select( thing => thing.CatalogueId ).Where( id => features.Any( item => item.Id == id ) ).ToArray(), "the things 11, 12 and 15 are of no catalogue feature" );

			(int OnOffer, int Standing, int Features) Read()
			{
				var facts = park.People.AdvisorFacts();
				return (facts.FeatureKindsOnOffer, facts.FeatureKindsStanding, facts.Features);
			}

			(int, int, int, int, int, int) Others()
			{
				var facts = park.People.AdvisorFacts();
				return (facts.SideshowKindsOnOffer, facts.SideshowKindsStanding, facts.Sideshows, facts.ShopKindsOnOffer, facts.ShopKindsStanding, facts.Shops);
			}

			Assert.AreEqual( (StockFeatureKindsOnOffer, StockFeatureKindsStanding, StockFeatures), Read() );
			Assert.AreEqual( (StockSideshowKindsOnOffer, 1, 1, StockShopKindsOnOffer, 1, 1), Others() );

			// A fourth toilet is a feature more and no kind more; another feature is a kind, researched or not; a shop, a sideshow or a ride is neither.
			var toilet = park.State.Objects.First( thing => thing.CatalogueId == Toilet );
			park.State.AddObject( toilet with { ThingId = park.State.NextThingId() } );
			Assert.AreEqual( (StockFeatureKindsOnOffer, StockFeatureKindsStanding, StockFeatures + 1), Read() );
			park.State.AddObject( toilet with { ThingId = park.State.NextThingId(), CatalogueId = 1425 } );
			park.State.AddObject( toilet with { ThingId = park.State.NextThingId(), CatalogueId = 1400 } );
			Assert.AreEqual( (StockFeatureKindsOnOffer, StockFeatureKindsStanding + 2, StockFeatures + 3), Read(), "a kind standing counts, on offer or not" );
			Assert.AreEqual( (StockSideshowKindsOnOffer, 1, 1, StockShopKindsOnOffer, 1, 1), Others(), "a feature is no shop or sideshow and no kind of one" );
			park.State.AddObject( toilet with { ThingId = park.State.NextThingId(), CatalogueId = BurgerShop } );
			park.State.AddObject( toilet with { ThingId = park.State.NextThingId(), CatalogueId = LaughingHyenas } );
			park.State.AddObject( toilet with { ThingId = park.State.NextThingId(), CatalogueId = CrazyApe } );
			Assert.AreEqual( (StockFeatureKindsOnOffer, StockFeatureKindsStanding + 2, StockFeatures + 3), Read(), "a shop, a sideshow and a ride are no feature and no kind of one" );
			Assert.AreEqual( (StockSideshowKindsOnOffer, 2, 2, StockShopKindsOnOffer, 2, 2), Others() );

			// A sale takes a kind away with its last thing, and no sooner.
			park.State.AddObject( toilet with { ThingId = park.State.NextThingId(), CatalogueId = 1425 } );
			var bought = park.State.Objects.Where( thing => thing.CatalogueId == 1425 ).Select( thing => thing.ThingId ).ToArray();
			Assert.AreEqual( (StockFeatureKindsOnOffer, StockFeatureKindsStanding + 2, StockFeatures + 4), Read() );
			Assert.IsTrue( park.State.RemoveObject( bought[0] ) );
			Assert.AreEqual( (StockFeatureKindsOnOffer, StockFeatureKindsStanding + 2, StockFeatures + 3), Read() );
			Assert.IsTrue( park.State.RemoveObject( bought[1] ) );
			Assert.AreEqual( (StockFeatureKindsOnOffer, StockFeatureKindsStanding + 1, StockFeatures + 2), Read(), "a kind built and sold stands no more" );

			var instant = new ParkItemCatalogue( Theme, data, instantAction: true );
			Assert.AreEqual( 19, instant.All.Count( item => item.UiType == ItemDescriptionFile.Feature ), "Instant Action's catalogue" );
			Assert.AreEqual( 8, instant.All.Count( item => item.UiType == ItemDescriptionFile.Feature && research.IsResearched( item.Id ) ), "the same eight are on offer in it" );
		}
		finally
		{
			Close( park );
		}
	}

	private const int Mechanic = 26;


	private static Staff TheMechanic( Park park ) => park.People.Staff.Single( member => member.ThingId == Mechanic );

	/// <summary>Breaks the Belly Bounce on its own turn with the advisor listening; answers his messages.</summary>
	private static ParkAdvisorMessages BreakWithTheAdvisorListening( Park park, int gameTick = 2000, ParkAdvisorMessages? messages = null )
	{
		park.State.AdvisorMessages = messages ??= new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ) );
		park.State.SetGameTick( gameTick );
		Assert.IsTrue( park.Scripts[BellyBounce].Set( ParkRideOperation.BrokenVariable, 1 ) );
		park.People.TakeTheRidesTurns( thingTick: 10 );
		Assert.AreEqual( ParkRideChoice.StateRefusedOne, Thing( park, BellyBounce ).State, "broken down" );

		return messages;
	}

	/// <summary>
	/// The mechanics' base hears a break (<c>FUN_004dadc0</c>): the shipped park's one mechanic, walking with no
	/// job, is free, and the advisor is told nothing.
	/// </summary>
	[TestMethod]
	public void ABreakWithAFreeMechanicTellsTheAdvisorNothing()
	{
		var park = Open();

		try
		{
			Assert.AreEqual( (0, false), (TheMechanic( park ).ObjectToRepair, TheMechanic( park ).Activity is StaffActivity.Resting) );
			Assert.AreEqual( 0, BreakWithTheAdvisorListening( park ).PendingCount );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>With no mechanic in the park the advisor is posted message 0x44 with the ride, on the park's tick.</summary>
	[TestMethod]
	public void ABreakWithNoMechanicInTheParkPostsMessage68WithTheRide()
	{
		var park = Open();

		try
		{
			Assert.IsTrue( park.People.Fire( Mechanic ) );

			var messages = BreakWithTheAdvisorListening( park );

			Assert.IsTrue( messages.Census().Contains( "pending=1 attempts=0 threshold=25 slot=68/subject=13 " ), messages.Census() );

			var said = new List<(int, int)>();

			messages.Tick( 2001, 0, ( response, sample ) => { said.Add( (response, sample) ); return 0; } );
			CollectionAssert.AreEqual( new[] { (198, 196) }, said );

			park.People.TakeTheRidesTurns( thingTick: 11 );
			Assert.AreEqual( 0, messages.PendingCount, "a ride already broken down is not spoken of again" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>The post carries the park's tick: one inside 480 sweeps of the line last said is refused.</summary>
	[TestMethod]
	public void ABreakInsideTheMessagesOwnWaitIsNotPosted()
	{
		var park = Open();

		try
		{
			Assert.IsTrue( park.People.Fire( Mechanic ) );

			var messages = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ) );

			Assert.IsTrue( messages.Post( ParkAdvisorMessages.NoMechanics, 1600, 14 ) );
			messages.Tick( 1600, 0, ( _, _ ) => 0 );
			Assert.AreEqual( 0, BreakWithTheAdvisorListening( park, gameTick: 2079, messages ).PendingCount, "479 sweeps on" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// A mechanic with another job, or with none and resting, on his way to rest or striking, is busy: with
	/// nobody else, message 0x45. One with no job in any other state is free.
	/// </summary>
	[TestMethod]
	[DataRow( 14, StaffActivity.GoingToRide, 69 )]
	[DataRow( 14, StaffActivity.Idle, 69 )]
	[DataRow( 0, StaffActivity.GoingToRest, 69 )]
	[DataRow( 0, StaffActivity.Resting, 69 )]
	[DataRow( 0, StaffActivity.GoingOnStrike, 69 )]
	[DataRow( 0, StaffActivity.OnStrike, 69 )]
	[DataRow( 0, StaffActivity.Idle, 0 )]
	[DataRow( 0, StaffActivity.Walking, 0 )]
	[DataRow( 0, StaffActivity.Repairing, 0 )]
	[DataRow( 0, StaffActivity.Held, 0 )]
	public void ABreakWithEveryMechanicBusyPostsMessage69( int job, StaffActivity doing, int message )
	{
		var park = Open();

		try
		{
			TheMechanic( park ).ObjectToRepair = job;
			TheMechanic( park ).SetActivity( doing, 0 );
			Assert.AreEqual( message, ParkPeople.MessageForABreak( park.People.Staff, Thing( park, BellyBounce ) ) );

			var messages = BreakWithTheAdvisorListening( park );

			Assert.AreEqual( message == 0 ? 0 : 1, messages.PendingCount );
			Assert.AreEqual( message != 0, messages.Census().Contains( "slot=69/subject=13 " ), messages.Census() );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// The advisor's turn is the sweep's last (<c>FUN_0050b360</c>'s arm <c>0xb</c>, thing 3 behind every thing
	/// made after him), so a message a ride posts on its turn is said on that same tick: a Belly Bounce worn
	/// under 10 on tick 64.
	/// </summary>
	[TestMethod]
	public void AMessageARidePostsOnItsTurnIsSaidOnTheSameSweep()
	{
		var park = Open();

		try
		{
			var messages = park.State.AdvisorMessages = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ) );

			Assert.IsTrue( park.Scripts[BellyBounce].Set( ParkRideOperation.OnRideVariable, 5 ) );
			Change( park, BellyBounce, thing => thing with { StateOfRepair = 11.5f } );
			park.State.SetGameTick( 63 );

			Time.Paused = false;
			Time.StepFrames = 0;
			GameClock.Rebase();
			Time.Update( 0f );
			GameClock.Update( paused: false, GameClock.ParkCatchUp );

			for ( var frame = 0; frame < 2000 && park.State.GameTick < 64; ++frame )
			{
				Time.Update( 1f / 60f );
				GameClock.Update( paused: false, GameClock.ParkCatchUp );
				park.People.Update();
			}

			Assert.AreEqual( 64, park.State.GameTick, "one sweep" );
			Assert.IsTrue( messages.Census().Contains( "pending=0 attempts=1 " ), messages.Census() );
			Assert.IsTrue( messages.Census().Contains( "message=66/score=100/line=0/lastTick=64/said=1/" ), messages.Census() );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// The sweep hands the advisor the park: with the shipped park shut and three guests waiting on the booths'
	/// cells, the sweep whose poll stands on row 1 says message 1 at 60.
	/// </summary>
	[TestMethod]
	public void TheSweepHandsTheAdvisorTheParkSoAShutParksLineIsSaid()
	{
		var park = Open();

		try
		{
			var messages = park.State.AdvisorMessages = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ),
				cursor: new ParkAdvisorMessages.PollCursor { Row = 1 } );
			var random = new Random( 1 );
			park.State.SetParkClosed( true );
			foreach ( var (guest, x) in new[] { (0, 47), (1, 48), (2, 48) } )
			{
				park.People.Peeps[guest].Navigator.Position = FixedVector.AtCell( x, 13 );
				park.People.Peeps[guest].SetState( PeepState.WaitingForOpening, park.State.GameTick, random );
			}

			var before = park.People.WaitingToOpen;
			Time.Paused = false;
			Time.StepFrames = 0;
			GameClock.Rebase();
			Time.Update( 0f );
			GameClock.Update( paused: false, GameClock.ParkCatchUp );
			var first = park.State.GameTick;

			for ( var frame = 0; frame < 2000 && park.State.GameTick < first + 1; ++frame )
			{
				Time.Update( 1f / 60f );
				GameClock.Update( paused: false, GameClock.ParkCatchUp );
				park.People.Update();
			}

			var waiting = park.People.WaitingToOpen;
			Assert.IsTrue( before >= 3 && waiting >= 3, $"{before} then {waiting} waiting" );
			Assert.IsTrue( messages.Census().Contains( $"message=1/score={20 * waiting}/line=0/lastTick={first + 1}/said=1/" ), messages.Census() );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// And one a member of staff posts on theirs: the mechanic's decide that takes the broken Bounce posts
	/// message 0x46, which is never left waiting at a sweep's end.
	/// </summary>
	[TestMethod]
	public void AMessageAMechanicPostsOnHisTurnIsSaidOnTheSameSweep()
	{
		var park = Open();

		try
		{
			var messages = park.State.AdvisorMessages = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ) );

			Assert.IsTrue( park.Scripts[BellyBounce].Set( ParkRideOperation.BrokenVariable, 1 ) );

			Time.Paused = false;
			Time.StepFrames = 0;
			GameClock.Rebase();
			Time.Update( 0f );
			GameClock.Update( paused: false, GameClock.ParkCatchUp );

			var first = park.State.GameTick;

			for ( var frame = 0; frame < 6000 && messages.Attempts == 0; ++frame )
			{
				Time.Update( 1f / 60f );
				GameClock.Update( paused: false, GameClock.ParkCatchUp );
				park.People.Update();
				Assert.AreEqual( 0, messages.PendingCount, $"left waiting at the end of sweep {park.State.GameTick}: {messages.Census()}" );
			}

			Assert.AreEqual( BellyBounce, TheMechanic( park ).ObjectToRepair, "the mechanic took it" );
			Assert.IsTrue( park.State.GameTick > first + 1, "on a later sweep than the break's" );
			Assert.IsTrue( messages.Census().Contains( $"message=70/score=110/line=0/lastTick={park.State.GameTick}/said=1/" ), messages.Census() );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// A mechanic whose job already names the ride ends the look: message 0x46 where nobody called him to it
	/// (<c>+0x64</c>), nothing where somebody did. A condemned ride's event is another, and posts nothing.
	/// </summary>
	[TestMethod]
	[DataRow( 0, 100f, 70 )]
	[DataRow( 1, 100f, 0 )]
	[DataRow( 0, 0f, 0 )]
	public void ABreakOfTheRideAMechanicAlreadyNamesPostsMessage70UnlessHeWasCalled( int called, float life, int message )
	{
		var park = Open();

		try
		{
			TheMechanic( park ).ObjectToRepair = BellyBounce;
			Change( park, BellyBounce, thing => thing with { RequestedService = called, RemainingLife = life } );

			var messages = park.State.AdvisorMessages = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ) );

			Assert.IsTrue( park.Scripts[BellyBounce].Set( ParkRideOperation.BrokenVariable, 1 ) );
			park.People.TakeTheRidesTurns( thingTick: 10 );
			Assert.AreEqual( message == 0 ? 0 : 1, messages.PendingCount, messages.Census() );
			Assert.AreEqual( message != 0, messages.Census().Contains( $"slot={message}/subject=13 " ), messages.Census() );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>Two mechanics: a busy one before a free one says nothing, and two busy say message 0x45.</summary>
	[TestMethod]
	public void TheLookGoesOnPastABusyMechanicToAFreeOne()
	{
		var park = Open();

		try
		{
			var saved = park.World.People.Single( person => person.ThingId == Mechanic );
			var second = new Staff( 99, saved.Model, saved.Staff!.Value with { State = (int)StaffActivity.Walking }, saved.Navigator );
			var first = TheMechanic( park );

			first.ObjectToRepair = 14;
			Assert.AreEqual( 0, ParkPeople.MessageForABreak( new[] { first, second }, Thing( park, BellyBounce ) ) );
			Assert.AreEqual( 0, ParkPeople.MessageForABreak( new[] { second, first }, Thing( park, BellyBounce ) ) );
			second.ObjectToRepair = 15;
			Assert.AreEqual( 69, ParkPeople.MessageForABreak( new[] { first, second }, Thing( park, BellyBounce ) ) );
			Assert.AreEqual( 68, ParkPeople.MessageForABreak( park.People.Staff.Where( member => member.ThingId != Mechanic ), Thing( park, BellyBounce ) ),
				"the other four kinds are not mechanics" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// <c>mRequestedService</c> is read from file offset 1078, the dword after the three floats; the shipped
	/// park has no mechanic called on any of its fourteen objects. This pins the offset against the float
	/// before it only: the dword after it is nought here too, and the chain that closes on
	/// <c>mTotalTakings</c> at 1090 is what places it (<see cref="ParkWorld"/>).
	/// </summary>
	[TestMethod]
	public void NoObjectInTheShippedParkHasAMechanicCalled()
	{
		var park = Open();

		try
		{
			Assert.AreEqual( 14, park.World.Objects.Count, "fourteen objects" );
			Assert.IsTrue( park.World.Objects.All( thing => thing.RequestedService == 0 ), "none waits on a mechanic" );
			Assert.AreEqual( 100f, park.World.Objects.Single( thing => thing.ThingId == BellyBounce ).StateOfRepair,
				"the float before it reads as it should" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// The last step of the turn's first half, <c>FUN_004e0b90</c> (<c>docs/exe/ride-operation.md</c>, "The first
	/// half of the turn", step 5): a head who is not queueing is cleared and nothing else is tidied, in every state
	/// but 3 and 4. So the head is nobody afterwards, and the guest is left as they were: still naming the ride and
	/// with their happiness of 50 whole, because in states 1 and 2 the completion that follows finds no head to put
	/// out and takes no <c>MediumHappinessChange</c> off anybody.
	/// </summary>
	[TestMethod]
	[DataRow( 0 )]
	[DataRow( ParkRideChoice.StateRefusedOne )]
	[DataRow( 2 )]
	public void AStaleQueueHeadIsDroppedOnTheTurnOfAThingNotInStateThreeOrFour( int state )
	{
		var park = Open();

		try
		{
			var head = Queue( park, 1 )[0];

			Change( park, BellyBounce, thing => thing with { State = state } );
			head.SetState( PeepState.Deciding, tick: 1, new Random( 1 ) );

			Assert.AreEqual( head.ThingId, park.State.FirstInQueue( BellyBounce ), "the head names a guest who queues no longer" );

			park.People.TakeTheRidesTurns( thingTick: 10 );

			Assert.AreEqual( 0, park.State.FirstInQueue( BellyBounce ), $"state {state}: the stale head is cleared" );
			Assert.AreEqual( BellyBounce, head.MajorDest, $"state {state}: and nothing else is tidied" );
			Assert.AreEqual( Before, head.Happiness, 0.001f, $"state {state}: dropped, not put out, so nothing is lost" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// <c>FUN_004e0b90</c> returns for states 3 and 4 before its stale-head drop, and <c>FUN_004e0e00</c> sends
	/// state 3 straight to its return (<c>docs/exe/ride-operation.md</c>, "The first half of the turn", step 2:
	/// "a state-3 object does nothing at all on its turn"; "The second half", state 3: nothing). So a state-3
	/// thing whose head has stopped queueing still names that head after its turn.
	/// </summary>
	[TestMethod]
	public void AThingInStateThreeKeepsAStaleQueueHeadOnItsTurn()
	{
		var park = Open();

		try
		{
			var head = Queue( park, 1 )[0];

			Change( park, BellyBounce, thing => thing with { State = 3 } );
			head.SetState( PeepState.Deciding, tick: 1, new Random( 1 ) );

			park.People.TakeTheRidesTurns( thingTick: 10 );

			Assert.AreEqual( head.ThingId, park.State.FirstInQueue( BellyBounce ), "state 3 returns before the stale-head drop" );
			Assert.AreEqual( BellyBounce, head.MajorDest, "and the guest is not touched" );
			Assert.AreEqual( Before, head.Happiness, 0.001f, "nor put out" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// The watchdog at the tail of <c>FUN_004e1220</c> (<c>0x004e1382</c>), through the ride's own turn: on a turn
	/// that invites nobody an open ride lets go of a nominee who is not being admitted, and keeps one who is. A
	/// closed ride (<c>mCanLoad</c> nought) runs the completion and returns at <c>0x004e13fc</c>, so the watchdog is
	/// not reached and the nominee stands (<c>docs/exe/ride-operation.md</c>, "The closed ride", the completion).
	/// </summary>
	[TestMethod]
	public void AnOpenRidesTurnDropsANomineeWhoIsNotComingToBoard()
	{
		var park = Open();

		try
		{
			var guest = park.People.Peeps[0];

			guest.SetState( PeepState.BeingAdmitted, tick: 1, new Random( 1 ) );
			park.State.NominateForLoading( BellyBounce, guest.ThingId );
			park.People.TakeTheRidesTurns( thingTick: 10 );

			Assert.AreEqual( guest.ThingId, park.State.PersonBeingLoaded( BellyBounce ), "a nominee on their way is kept" );

			guest.SetState( PeepState.Deciding, tick: 1, new Random( 1 ) );
			Change( park, BellyBounce, thing => thing with { CanLoad = 0 } );
			park.People.TakeTheRidesTurns( thingTick: 11 );

			Assert.AreEqual( guest.ThingId, park.State.PersonBeingLoaded( BellyBounce ),
				"a closed ride's turn does not reach the watchdog" );

			Change( park, BellyBounce, thing => thing with { CanLoad = 1 } );
			park.People.TakeTheRidesTurns( thingTick: 12 );

			Assert.AreEqual( 0, park.State.PersonBeingLoaded( BellyBounce ),
				"an open ride's turn lets go of a nominee who is not being admitted" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// <c>FUN_004e0e00</c>'s arm for states 1, 2 and 4 is the completion <c>FUN_004e0450</c> and then the dismissal
	/// <c>FUN_004e1410</c> (<c>docs/exe/ride-operation.md</c>, "The second half"): a ride broken down, waiting for
	/// an upgrade or condemned still lets off the rider its script names in <c>VAR_LETMEOFF</c>, who is then
	/// leaving the ride, and the slot is emptied behind them.
	/// </summary>
	[TestMethod]
	[DataRow( ParkRideChoice.StateRefusedOne )]
	[DataRow( 2 )]
	[DataRow( ParkRideChoice.StateRefusedFour )]
	public void ARideOutOfServiceStillLetsOffWhoeverItsScriptReports( int state )
	{
		var park = Open();

		try
		{
			var rider = park.People.Peeps[0];
			var script = park.Scripts[BellyBounce];

			rider.MajorDest = BellyBounce;
			rider.QueuePos = 0;
			rider.SetState( PeepState.Riding, tick: 1, new Random( 1 ) );

			Change( park, BellyBounce, thing => thing with { State = state, CanLoad = 0 } );
			Assert.IsTrue( script.Set( ParkRideOperation.DismissVariable, rider.ThingId ), "the script reports its rider" );

			park.People.TakeTheRidesTurns( thingTick: 10 );

			Assert.AreEqual( PeepState.LeavingRide, rider.State, $"state {state}: the rider is let off" );
			Assert.AreEqual( 0, script[ParkRideOperation.DismissVariable], $"state {state}: and the slot is emptied behind them" );
		}
		finally
		{
			Close( park );
		}
	}
}
