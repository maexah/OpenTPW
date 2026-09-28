using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What a guest remembers and what the park's calendar says, as the ride score reads them - the two histories
/// (<see cref="Peep.PreviousRides"/>, <see cref="Peep.PreviousTemporaryRides"/>), a thing's age, the rain, the
/// queue count and its cells - each through the wiring that writes or hands it in. <c>docs/exe/ride-operation.md</c>, "What a
/// thing is worth to a guest".
///
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkVisitHistoryTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const int BellyBounce = 13;

	private const int JungleSpray = 14;

	private const int DrinksShop = 16;

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private static ParkBalance Balance() => new( "jungle", easyMode: true );

	private RideScript Script()
	{
		using var stream = data.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );
		return new RideScript( new RideScriptFile( stream ) );
	}

	private ParkPeople People( ParkWorld world, ParkState state )
		=> new( world, Balance(), null, state, new ParkItemCatalogue( "jungle", data ) );

	private static void Done( ParkPeople people )
	{
		people.Delete();
		Entity.ApplyDeletions();
	}

	/// <summary>The thing a guest's <c>why</c> line says they chose.</summary>
	private static int Chose( ParkPeople people, int guestId )
	{
		var line = people.WhyCensus().Single( line => line.StartsWith( $"thing {guestId,3} " ) );
		var at = line.IndexOf( "chose thing ", StringComparison.Ordinal );

		return at < 0 ? 0 : int.Parse( line[(at + 12)..].Split( ' ' )[0] );
	}

	/// <summary>
	/// <b>A thing a guest leaves goes in front of their visits</b>, whatever it is (<c>FUN_004fd970</c>,
	/// <c>0x004fd98b</c>): the Belly Bounce and then the Drinks Shop, each let off through a ride script's
	/// <c>VAR_LETMEOFF</c>, leave the shop first.
	/// </summary>
	[TestMethod]
	public void LeavingAThingPutsItInFrontOfTheVisits()
	{
		var world = World();
		var state = new ParkState( world );
		var peep = ParkPeople.PeepsIn( world ).First();
		var guests = new Dictionary<int, Peep> { [peep.ThingId] = peep };

		foreach ( var thingId in new[] { BellyBounce, DrinksShop } )
		{
			var script = Script();

			peep.SetState( PeepState.Riding, tick: 1, new Random( 1 ) );
			script.Set( ParkRideOperation.DismissVariable, peep.ThingId );

			Assert.IsTrue( new ParkRideOperation( state, guests )
				.Dismiss( script, world.Objects.Single( o => o.ThingId == thingId ), tick: 2, new Random( 1 ) ), $"let off {thingId}" );
		}

		CollectionAssert.AreEqual( new[] { DrinksShop, BellyBounce, 0, 0 }, peep.PreviousRides.ToArray() );
	}

	/// <summary>
	/// <b>A refusal is forgotten a nought at a time</b>: on each sweep whose <c>mGameTick</c> divides by twenty, every
	/// guest's refusals take a nought in front (<c>FUN_004fdc90</c>, the needs turn's last call). Lost Kingdom loads on
	/// sweep 755, so the fifth sweep, 760, is the first to push one.
	/// </summary>
	[TestMethod]
	public void ASweepWhoseTickDividesByTwentyPushesANoughtOntoTheRefusals()
	{
		var world = World();
		var state = new ParkState( world );
		var people = People( world, state );

		try
		{
			var guest = people.Peeps[0];

			guest.RememberRefusal( JungleSpray );

			Time.Paused = false;
			Time.StepFrames = 0;
			GameClock.Rebase();
			Time.Update( 0f );
			GameClock.Update( paused: false, GameClock.ParkCatchUp );

			for ( var frame = 0; frame < 2000 && state.GameTick < 760; ++frame )
			{
				Assert.AreEqual( JungleSpray, guest.PreviousTemporaryRides[0], $"still in front on sweep {state.GameTick}" );

				Time.Update( 1f / 60f );
				GameClock.Update( paused: false, GameClock.ParkCatchUp );
				people.Update();
			}

			Assert.AreEqual( 760, state.GameTick, "the park swept up to 760" );
			CollectionAssert.AreEqual( new[] { 0, JungleSpray, 0, 0 }, guest.PreviousTemporaryRides.ToArray(),
				"and on 760 a nought went in front" );
		}
		finally
		{
			Done( people );
		}
	}

	/// <summary>
	/// <b>A removed thing leaves no visit behind</b> (<c>FUN_004fb360</c>, <c>0x004fb4ba</c>), for every guest whatever
	/// they name: each visit naming it is emptied, and the refusal at the same place with it - whatever that holds. A
	/// refusal of the thing where no visit names it stays.
	/// </summary>
	[TestMethod]
	public void ARemovedThingIsForgottenAndTheRefusalBesideIt()
	{
		var world = World();
		var behaviour = new PeepBehaviour( parkIsClosed: false, world.NumberOfVisitorsToDate, new Random( 1 ) );
		var guest = ParkPeople.PeepsIn( world ).First();

		guest.MajorDest = DrinksShop;

		foreach ( var visit in new[] { 0, BellyBounce, DrinksShop, BellyBounce } )
			guest.RememberVisit( visit );

		foreach ( var refusal in new[] { BellyBounce, 7, BellyBounce, 5 } )
			guest.RememberRefusal( refusal );

		Assert.AreEqual( PeepBehaviour.PutOff.No, behaviour.ThingRemoved( guest, BellyBounce, tick: 2 ),
			"a guest heading elsewhere is not put off" );

		CollectionAssert.AreEqual( new[] { 0, DrinksShop, 0, 0 }, guest.PreviousRides.ToArray(), "both visits emptied" );
		CollectionAssert.AreEqual( new[] { 0, BellyBounce, 0, BellyBounce }, guest.PreviousTemporaryRides.ToArray(),
			"and the refusals beside them, while the two refusals of it elsewhere stay" );
	}

	/// <summary>
	/// <b>The same kind as the thing a guest left last is not chosen, whichever one of that kind it is.</b> At (55,30)
	/// a type 3 takes a Jungle Spray, 18 against the Belly Bounce's 17 (either of two: a second stands on the same
	/// record, and they tie). Having left the second, they take the Belly Bounce: the kind scores nought, where
	/// dividing by visit would leave the first alone. Having left the Drinks Shop since, the first is theirs again,
	/// the second a quarter of it for the visit behind.
	/// </summary>
	[TestMethod]
	public void TheSameKindAsTheThingLeftLastIsNotChosen()
	{
		var world = World();
		var state = new ParkState( world );
		var spray = state.Objects.Single( o => o.ThingId == JungleSpray );

		state.AddObject( spray with { ThingId = 98 } );

		var people = People( world, state );

		try
		{
			var guest = people.Admit( 55, 30, personType: 3 );

			CollectionAssert.Contains( new[] { JungleSpray, 98 }, Chose( people, guest ), "with nothing left, a Spray" );

			people.Guests[guest].RememberVisit( 98 );

			Assert.AreEqual( BellyBounce, Chose( people, guest ), "having left the other Spray, the Belly Bounce" );

			people.Guests[guest].RememberVisit( DrinksShop );

			Assert.AreEqual( JungleSpray, Chose( people, guest ),
				"and having left the shop since, the Spray again - the one not divided down for the visit behind" );
		}
		finally
		{
			Done( people );
		}
	}

	/// <summary>
	/// <b>A thing is new, and worth five times as much, for the 184 sweeps after it is stamped</b>: a second Belly
	/// Bounce stamped now beats the first outright, and 185 sweeps on the two tie, which an odd tick gives to the later
	/// in the walk - the first, as the second was linked in at the head.
	/// </summary>
	[TestMethod]
	public void ANewThingIsWorthFiveTimesForOneHundredAndEightyFourSweeps()
	{
		var world = World();
		var state = new ParkState( world );
		var chooser = new ParkRideChooser( world, new ParkItemCatalogue( "jungle", data ), new ParkRideScore( Balance() ), state );
		var bounce = state.Objects.Single( o => o.ThingId == BellyBounce );

		state.AddObject( bounce with { ThingId = 99, Built = ParkWorld.BuiltWhen.At( state.CalendarNow ) } );

		int Picks() => chooser.ChooseFor( new ParkRideScore.Wants( 0, 0f, 0f, 0f, 0f ), 52, 25, gameTick: 1,
			queueLength: _ => 0, now: state.CalendarNow )?.ThingId ?? 0;

		Assert.AreEqual( 99, Picks(), "stamped this sweep it is new" );

		for ( var sweep = 0; sweep < 184; ++sweep )
			state.AdvanceGameTick();

		Assert.AreEqual( 99, Picks(), "and 184 sweeps on it still is" );

		state.AdvanceGameTick();

		Assert.AreEqual( BellyBounce, Picks(), "at 185 it is not: a tie, which the odd tick gives to the later" );
		Assert.AreEqual( BellyBounce, chooser.ChooseFor( new ParkRideScore.Wants( 0, 0f, 0f, 0f, 0f ), 52, 25, gameTick: 1,
			queueLength: _ => 0 )?.ThingId, "as it is with no calendar handed in" );
	}

	/// <summary>
	/// <b>The park's guests see a thing stamped now as new</b>: at (55,30) a type 3 takes the Jungle Spray, 18 against
	/// the Belly Bounce's 17, until a second Belly Bounce is stamped on this sweep - five times 17 - and takes the
	/// Spray again 185 sweeps later.
	/// </summary>
	[TestMethod]
	public void TheParksGuestsSeeAThingStampedNowAsNew()
	{
		var world = World();
		var state = new ParkState( world );
		var people = People( world, state );

		try
		{
			var guest = people.Admit( 55, 30, personType: 3 );

			Assert.AreEqual( JungleSpray, Chose( people, guest ), "before, the Spray" );

			var bounce = state.Objects.Single( o => o.ThingId == BellyBounce );

			state.AddObject( bounce with { ThingId = 99, Built = ParkWorld.BuiltWhen.At( state.CalendarNow ) } );

			Assert.AreEqual( 99, Chose( people, guest ), "a Belly Bounce stamped now, five times over" );

			for ( var sweep = 0; sweep < 185; ++sweep )
				state.AdvanceGameTick();

			Assert.AreEqual( JungleSpray, Chose( people, guest ), "and eight days on, the Spray again" );
		}
		finally
		{
			Done( people );
		}
	}

	/// <summary>
	/// <b>The choice reads a thing's computed excitement, not its file's.</b> At (52,33) a type 0, preferring 80, is
	/// offered nothing: the Jungle Spray's 30 scores it 9, which is not enough. Priced at -10, which makes its computed
	/// excitement its file's 35, it scores 10 and is offered.
	/// </summary>
	[TestMethod]
	public void TheChoiceReadsTheComputedExcitement()
	{
		var world = World();
		var state = new ParkState( world );
		var people = People( world, state );

		try
		{
			var guest = people.Admit( 52, 33, personType: 0 );

			Assert.AreEqual( 0, Chose( people, guest ), "at 30, nothing" );

			state.ReplaceObject( state.Objects.Single( o => o.ThingId == JungleSpray ) with { PricePerUse = -10 } );

			Assert.AreEqual( JungleSpray, Chose( people, guest ), "at 35, the Spray" );
		}
		finally
		{
			Done( people );
		}
	}

	/// <summary>
	/// <b>While drops fall, shelter is worth five times as much</b>, for a rain handed to the park's people. At (55,30)
	/// a type 0 takes the Belly Bounce dry, 10 against the Jungle Spray's 9; wet, the Spray is 45.
	/// </summary>
	[TestMethod]
	public void ShelterIsChosenWhileItRains()
	{
		var world = World();
		var people = People( world, new ParkState( world ) );

		try
		{
			var guest = people.Admit( 55, 30, personType: 0 );

			Assert.AreEqual( BellyBounce, Chose( people, guest ), "dry, the ride" );

			people.Raining = static () => true;

			Assert.AreEqual( JungleSpray, Chose( people, guest ), "wet, the sideshow, which is indoors" );
		}
		finally
		{
			Done( people );
		}
	}

	/// <summary>
	/// <b>A queue is counted up to and including the first guest no longer in it</b> (<c>FUN_004ddf50( 0 )</c>), and
	/// the choice's room test reads that count: four linked in the Jungle Spray's one-cell queue with the second of them
	/// gone off to decide count two, so a type 3 at (55,30) is still offered the Spray. Counting every link, the queue
	/// is full and they take the Belly Bounce.
	/// </summary>
	[TestMethod]
	public void AQueueIsCountedToTheFirstGuestNoLongerInIt()
	{
		var world = World();
		var state = new ParkState( world );
		var people = People( world, state );

		try
		{
			var queued = people.Peeps.Take( 4 ).ToArray();

			foreach ( var peep in queued )
			{
				peep.MajorDest = JungleSpray;
				peep.SetState( PeepState.InQueue, tick: 1, new Random( 1 ) );
				state.JoinQueue( JungleSpray, peep.ThingId );
			}

			queued[1].SetState( PeepState.Deciding, tick: 1, new Random( 1 ) );

			Assert.AreEqual( 4, state.QueueLength( JungleSpray ), "four links" );
			Assert.AreEqual( 2, state.QueueCount( JungleSpray, id => ParkRideOperation.IsQueueing( people.Guests[id] ) ),
				"counted to the one who left, and including them" );
			Assert.AreEqual( 4, state.QueueCount( JungleSpray ), "and every link with no test" );

			var guest = people.Admit( 55, 30, personType: 3 );

			Assert.AreEqual( JungleSpray, Chose( people, guest ), "room for them by the original's count" );
		}
		finally
		{
			Done( people );
		}
	}

	/// <summary>
	/// <b>The queue term is over the queue's walked cells</b>, which the chooser hands in: beside the Belly Bounce's
	/// entry at (52,24), with four in its queue of four cells, a type 3 scores it 24 - queue 75 - against the Jungle
	/// Spray's 18. Read as one cell the queue term is nought, 17, and the Spray would win. The record's cached pair and
	/// its saved <c>mQueueSizeInCells</c> are both cleared, so only the walk off the map can answer four.
	/// </summary>
	[TestMethod]
	public void TheChooserHandsInTheWalkedQueueCells()
	{
		var world = World();
		var state = new ParkState( world );
		var bounce = state.Objects.Single( o => o.ThingId == BellyBounce ) with { BackOfQueue = 0, QueueSizeInCells = 0 };

		Assert.IsTrue( state.ReplaceObject( bounce ) );

		var chooser = new ParkRideChooser( world, new ParkItemCatalogue( "jungle", data ), new ParkRideScore( Balance() ), state );

		Assert.AreEqual( 4, ParkRideChoice.QueueCellsFor( world, bounce ).Cells, "the Belly Bounce's queue walks four cells" );
		Assert.AreEqual( BellyBounce, chooser.ChooseFor( new ParkRideScore.Wants( 3, 0f, 0f, 0f, 0f ), 52, 24, gameTick: 0,
			queueLength: thing => thing.ThingId == BellyBounce ? 4 : 0 )?.ThingId );
	}

	/// <summary>
	/// <b>A queue too long to join</b> (<c>FUN_004ddb60</c>): for a thing without the queue-path bit, a count of a
	/// hundred or more, unsigned; for one with it, the unread capacity is counted and the guest let through.
	/// </summary>
	[TestMethod]
	public void AQueueIsTooLongAtAHundredWithoutAQueuePath()
	{
		var world = World();
		var spray = world.Objects.Single( o => o.ThingId == JungleSpray );
		var bounce = world.Objects.Single( o => o.ThingId == BellyBounce );

		Assert.IsFalse( spray.HasQueuePath, "the Spray has no queue path" );
		Assert.IsFalse( PeepBehaviour.QueueTooLong( spray, 99 ) );
		Assert.IsTrue( PeepBehaviour.QueueTooLong( spray, 100 ) );
		Assert.IsTrue( PeepBehaviour.QueueTooLong( spray, -1 ), "unsigned" );

		int Counted() => Unimplemented.Summary.FirstOrDefault( gap => gap.What == "QUEUE_TOO_LONG_CAPACITY" ).Times;

		var before = Counted();

		Assert.IsTrue( bounce.HasQueuePath, "the Belly Bounce has one" );
		Assert.IsFalse( PeepBehaviour.QueueTooLong( bounce, 1000 ), "let through" );
		Assert.AreEqual( before + 1, Counted(), "and counted" );
	}

	/// <summary>
	/// <b>The arrival's gates count the queue the original's way.</b> Four linked in the Jungle Spray's one-cell queue,
	/// the second of them gone off to decide, count two: a type 2 arriving at the back cell is let in, and then cannot
	/// reach their place past the one who left, so is put out (<c>0x004ffdf4</c>), losing
	/// <c>MediumHappinessChange</c>. Counting every link, the room gate would have turned them away untouched.
	/// </summary>
	[TestMethod]
	public void TheArrivalsGatesCountTheQueueTheOriginalsWay()
	{
		var world = World();
		var state = new ParkState( world );
		var balance = Balance();
		const int Stale = 71;

		foreach ( var guest in new[] { 70, Stale, 72, 73 } )
			state.JoinQueue( JungleSpray, guest );

		var behaviour = new PeepBehaviour( world, new Random( 1 ),
			new ParkAdmission( balance, world.Economy!.Value.AdmissionFee ), () => ParkRides.GateIsOpen, state,
			new ParkItemCatalogue( "jungle", data ), stillQueueing: id => id != Stale, balance: balance );

		// A type 2, preferring 50: the Spray's 30 is 20 away, so the excitement gate lets them by.
		var saved = world.People.First( person => person.Guest is { } );
		var centre = new FixedVector( PeepNavigator.WaypointCentre( 52 ), PeepNavigator.WaypointCentre( 29 ) );
		var arriving = new Peep( 30, saved.Guest!.Value with {
			State = (int)PeepState.GoingToRide, PersonType = 2, Happiness = 50f, MajorDest = JungleSpray },
			saved.Navigator with { X = centre.X, Y = centre.Y, TargetX = centre.X, TargetY = centre.Y } );

		Assert.AreEqual( 2, state.QueueCount( JungleSpray, id => id != Stale ) );

		behaviour.Step( arriving, new PeepWalk( arriving.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked ),
			playing: null, 40 );

		Assert.AreEqual( PeepState.Deciding, arriving.State );
		Assert.AreEqual( 50f - new ParkAdmission( balance, world.Economy!.Value.AdmissionFee ).MediumHappinessChange,
			arriving.Happiness, "let in, then put out" );
	}

	/// <summary>
	/// <b>The two histories come from the save, and a guest keeps them.</b> Every guest in Lost Kingdom's save has
	/// both empty, which a reader at the wrong offset would read too; so one guest's record is given four handles of
	/// each, interleaved at 470..485, before it is read. A record carrying them hands them to the guest.
	/// </summary>
	[TestMethod]
	public void AGuestStartsWithTheHistoriesTheirRecordCarries()
	{
		var world = World();

		foreach ( var person in world.People.Where( person => person.Guest is { } ) )
		{
			CollectionAssert.AreEqual( new[] { 0, 0, 0, 0 }, person.Guest!.Value.PreviousRides!.ToArray(), $"{person.ThingId}'s visits" );
			CollectionAssert.AreEqual( new[] { 0, 0, 0, 0 }, person.Guest!.Value.PreviousTemporaryRides!.ToArray(), $"{person.ThingId}'s refusals" );
		}

		// Guest 42's record, found by its own cash at 414, mExitLevel at 418 and kind at 468.
		var guest42 = world.People.Single( person => person.ThingId == 42 ).Guest!.Value;
		var bytes = new SaveReader( new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) ) ).ReadFile();
		var record = Enumerable.Range( 0, bytes.Length - 533 ).Single( at =>
			BitConverter.ToInt32( bytes, at + 414 ) == guest42.Cash && BitConverter.ToInt32( bytes, at + 418 ) == guest42.ExitLevel
			&& bytes[at + 468] == guest42.PersonType );
		int[] visits = [16, 13, 22, 0], refusals = [14, 0, 21, 23];

		for ( var i = 0; i < 4; ++i )
		{
			BitConverter.GetBytes( (ushort)visits[i] ).CopyTo( bytes, record + 470 + (i * 4) );
			BitConverter.GetBytes( (ushort)refusals[i] ).CopyTo( bytes, record + 472 + (i * 4) );
		}

		var patched = new ParkWorld( bytes ).People.Single( person => person.ThingId == 42 ).Guest!.Value;

		CollectionAssert.AreEqual( visits, patched.PreviousRides!.ToArray(), "mPreviousRides at 470, 474, 478, 482" );
		CollectionAssert.AreEqual( refusals, patched.PreviousTemporaryRides!.ToArray(), "and its twin at 472, 476, 480, 484" );

		var saved = world.People.First( person => person.Guest is { } );
		var peep = new Peep( saved.ThingId, saved.Guest!.Value with { PreviousRides = [16, 13, 0, 0], PreviousTemporaryRides = [0, 14, 0, 0] },
			saved.Navigator );

		CollectionAssert.AreEqual( new[] { 16, 13, 0, 0 }, peep.PreviousRides.ToArray() );
		CollectionAssert.AreEqual( new[] { 0, 14, 0, 0 }, peep.PreviousTemporaryRides.ToArray() );
	}
}
