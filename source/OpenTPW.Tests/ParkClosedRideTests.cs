using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Closing and opening rides, and what a closed ride does to its queue - the park's door
/// (<c>FUN_00519ef0</c>), the close and open (<c>FUN_004df300</c>, <c>FUN_004df390</c>) and their guard
/// (<c>FUN_004df290</c>), the closed ride's completion on its own turn (<c>FUN_004e0450</c>), and the reopen
/// at the end of a queue measured again (<c>FUN_004de1f0</c>). See <c>docs/exe/ride-operation.md</c>, "The
/// closed ride".
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
	/// The flags a bought thing is given (<see cref="ParkBuilding.FlagsFor"/>) agree with the save's own on the
	/// visitable, toilet and queue-path bits for every placed object: the Belly Bounce has a queue on the ground
	/// and its item <c>Info.HasQueue</c>, the Jungle Spray neither.
	/// </summary>
	[TestMethod]
	public void ABoughtThingIsFlaggedAsTheSaveFlagsItsOwn()
	{
		var park = Open();

		try
		{
			const int Bits = ParkWorld.CatalogueObject.VisitableFlag | ParkWorld.CatalogueObject.ToiletFlag
				| ParkWorld.CatalogueObject.QueuePathFlag;

			var compared = 0;

			foreach ( var thing in park.World.Objects.Where( thing => thing.IsPlaced ) )
			{
				if ( !park.Catalogue.TryGet( thing.CatalogueId, out var item ) )
					continue;

				Assert.AreEqual( thing.Flags & Bits, ParkBuilding.FlagsFor( item ) & Bits,
					$"thing {thing.ThingId} '{item.Name}'" );
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
			Change( park, BellyBounce, thing => thing with { Flags = (ushort)ParkBuilding.FlagsFor( item ), BackOfQueue = 0 } );

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
}
