using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The longest queue a guest joins or stays in - <c>FUN_004dda40</c>: a hundred for a thing without a queue path, and
/// for one with it <c>trunc( max( capacity × QueueWaitTimeConstant × R / duration, 4 ) )</c> at its tier, where R is
/// its speed over the tier's <c>InitSpeed</c> stored as a float. The arrival's gate (<c>FUN_004ddb60</c>) turns away a
/// guest when the queue's count is at or past it, and the <c>InQueue</c> turn puts out a queuer whose place is past it.
/// See <c>docs/exe/ride-operation.md</c>, "The <c>InQueue</c> turn".
///
/// <para>
/// Lost Kingdom's Belly Bounce is saved at capacity 5, duration 30 and speed 60 on tier nought, and <c>Bouncy.sam</c>
/// gives its three tiers the constants 130, 135 and 145, so its longest queue is 21. Most of these read real game
/// files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkLongestQueueTests
{
	private const int BellyBounce = 13;

	private const int JungleSpray = 14;

	private const int Sweep = 40;

	private const float Before = 50f;

	private const float Content = 10f;

	private BaseFileSystem? data;

	private BaseFileSystem Data() => data ??= FileSystem = GameData.Required();

	private ParkWorld World()
	{
		using var stream = new MemoryStream( Data().ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private ParkItemCatalogue Catalogue() => new( "jungle", Data() );

	private static int Counted( string gap ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == gap ).Times;

	/// <summary>
	/// <b>Each tier's constant is a float, and the category's shows through</b> where the item states none, as every
	/// other <c>Upgrades</c> key does; with neither it is nought.
	/// </summary>
	[TestMethod]
	public void ATiersQueueConstantIsReadAsAFloatOverTheCategorys()
	{
		var category = new ItemDescriptionFile( "Upgrades[0].QueueWaitTimeConstant\t\t30.5\n"
			+ "Upgrades[1].QueueWaitTimeConstant\t\t35\nUpgrades[2].QueueWaitTimeConstant\t\t40.25\n" );
		var item = new ItemDescriptionFile( "Info.Id 1\nUpgrades[1].QueueWaitTimeConstant\t12.5\n", category );

		Assert.AreEqual( 30.5f, item.QueueWaitTimeConstantAt( 0 ), "the category's, not rounded" );
		Assert.AreEqual( 12.5f, item.QueueWaitTimeConstantAt( 1 ), "the item's own, not rounded" );
		Assert.AreEqual( 40.25f, item.QueueWaitTimeConstantAt( 2 ), "the category's, not rounded" );
		Assert.AreEqual( 0f, new ItemDescriptionFile( "Info.Id 1\n" ).QueueWaitTimeConstantAt( 0 ), "neither states it" );
	}

	/// <summary>
	/// <b>Every Lost Kingdom ride with a queue states its own constants</b>, and the catalogue carries all three tiers: the Belly
	/// Bounce 130, 135, 145, where <c>Rides.sam</c> gives 30, 35, 40; the Hot Pot 100, 120, 140; the Sun God 3, 4, 4.
	/// </summary>
	[TestMethod]
	public void TheCatalogueCarriesEachRidesOwnConstants()
	{
		var catalogue = Catalogue();

		(float, float, float) Constants( string name )
		{
			var item = catalogue.All.Single( item => item.Name == name );

			return (item.QueueWaitTimeConstantAt( 0 ), item.QueueWaitTimeConstantAt( 1 ), item.QueueWaitTimeConstantAt( 2 ));
		}

		Assert.AreEqual( (130f, 135f, 145f), Constants( "Belly Bounce" ) );
		Assert.AreEqual( (100f, 120f, 140f), Constants( "The Hot Pot" ) );
		Assert.AreEqual( (3f, 4f, 4f), Constants( "Sun God" ) );
	}

	/// <summary>
	/// <b>The Belly Bounce as saved holds 21</b>: 5 × 130 / 30 is 21.67, truncated. The Jungle Spray, with no queue
	/// path, holds a hundred whatever its item says.
	/// </summary>
	[TestMethod]
	public void TheSavedBellyBounceHoldsTwentyOne()
	{
		var world = World();
		var catalogue = Catalogue();
		var bounce = world.Objects.Single( o => o.ThingId == BellyBounce );
		var spray = world.Objects.Single( o => o.ThingId == JungleSpray );

		Assert.AreEqual( (5, 30, 60, 0), (bounce.OperatingCapacity, bounce.OperatingDuration, bounce.OperatingSpeed,
			bounce.UpgradeLevel), "as saved" );
		Assert.AreEqual( 21u, PeepBehaviour.LongestQueue( bounce, ItemFor( catalogue, bounce ) ) );
		Assert.AreEqual( 100u, PeepBehaviour.LongestQueue( spray, ItemFor( catalogue, spray ) ) );
	}

	/// <summary>
	/// <b>The formula, at the Belly Bounce's settings.</b> Capacity 2 holds 8 (8.67) and 1 holds 4 (4.33, truncated);
	/// capacity nought, at nought, takes the floor, 4. A speed of nought counts as the tier's own, 21. Tier 1 at 75 reads its own
	/// constant and speed, 22 (22.5), where tier nought's would make it 27; tier 2 at 90 is 24 (24.17).
	/// </summary>
	[TestMethod]
	public void TheLongestQueueScalesByCapacityTierAndSpeed()
	{
		var catalogue = Catalogue();
		var bounce = World().Objects.Single( o => o.ThingId == BellyBounce );
		var item = ItemFor( catalogue, bounce );

		uint? Longest( ParkWorld.CatalogueObject thing ) => PeepBehaviour.LongestQueue( thing, item );

		Assert.AreEqual( 8u, Longest( bounce with { OperatingCapacity = 2 } ) );
		Assert.AreEqual( 4u, Longest( bounce with { OperatingCapacity = 1 } ) );
		Assert.AreEqual( 4u, Longest( bounce with { OperatingCapacity = 0 } ), "the floor" );
		Assert.AreEqual( 21u, Longest( bounce with { OperatingSpeed = 0 } ), "R is 1 at a speed of nought" );
		Assert.AreEqual( 22u, Longest( bounce with { UpgradeLevel = 1, OperatingSpeed = 75 } ) );
		Assert.AreEqual( 24u, Longest( bounce with { UpgradeLevel = 2, OperatingSpeed = 90 } ) );
	}

	/// <summary>
	/// <b>R is stored as a float before it is used.</b> At capacity 1, duration 13 and speed 42, R is 0.7 rounded to
	/// single precision, 0.69999999, and the value 6.9999999 truncates to 6; kept at double it would come to 7.0.
	/// <b>The speed is unsigned</b>: a saved speed of -1 is 4294967295, R 71582792 as a float, and the value
	/// 1550960493; read signed, R would be negative and the answer the floor. At duration 1 the value is 46528814800,
	/// past 2^32, and <c>__ftol</c> keeps its low 32 bits, 3579141840.
	/// </summary>
	[TestMethod]
	public void TheSpeedRatioIsAFloatOfAnUnsignedSpeed()
	{
		var catalogue = Catalogue();
		var bounce = World().Objects.Single( o => o.ThingId == BellyBounce );
		var item = ItemFor( catalogue, bounce );

		Assert.AreEqual( 6u, PeepBehaviour.LongestQueue(
			bounce with { OperatingCapacity = 1, OperatingDuration = 13, OperatingSpeed = 42 }, item ) );
		Assert.AreEqual( 1550960493u, PeepBehaviour.LongestQueue( bounce with { OperatingSpeed = -1 }, item ) );
		Assert.AreEqual( 3579141840u, PeepBehaviour.LongestQueue( bounce with { OperatingSpeed = -1, OperatingDuration = 1 }, item ),
			"the low 32 bits" );
	}

	/// <summary>
	/// <b>An infinite value truncates to nought</b> (<c>__ftol</c>'s integer indefinite): a duration of nought, or a tier's
	/// <c>InitSpeed</c> of nought under a speed, and then even an empty queue is too long. Nought over nought is not a
	/// number and takes the floor, and a tier stating no constant holds the floor. A tier past the third is counted and
	/// lets everyone in, as does a thing the catalogue does not know.
	/// </summary>
	[TestMethod]
	public void TheLongestQueuesEdges()
	{
		var catalogue = Catalogue();
		var bounce = World().Objects.Single( o => o.ThingId == BellyBounce );
		var item = ItemFor( catalogue, bounce );

		Assert.AreEqual( 0u, PeepBehaviour.LongestQueue( bounce with { OperatingDuration = 0 }, item ), "a duration of nought" );
		Assert.IsTrue( PeepBehaviour.QueueTooLong( bounce with { OperatingDuration = 0 }, item, 0 ), "nobody is let in" );
		Assert.AreEqual( 0u, PeepBehaviour.LongestQueue( bounce, item with { InitSpeed = 0 } ), "an InitSpeed of nought" );
		Assert.AreEqual( 4u, PeepBehaviour.LongestQueue( bounce with { OperatingDuration = 0, OperatingCapacity = 0 }, item ),
			"nought over nought takes the floor" );
		Assert.AreEqual( 4u, PeepBehaviour.LongestQueue( bounce, item with { QueueWaitTimeConstant = 0f } ), "no constant" );

		var before = Counted( "QUEUE_CAPACITY_UPGRADE_TIER" );

		Assert.IsNull( PeepBehaviour.LongestQueue( bounce with { UpgradeLevel = 3 }, item ) );
		Assert.IsFalse( PeepBehaviour.QueueTooLong( bounce with { UpgradeLevel = 3 }, item, 1000 ), "let through" );
		Assert.AreEqual( before + 2, Counted( "QUEUE_CAPACITY_UPGRADE_TIER" ), "and counted" );

		var unknown = Counted( "QUEUE_CAPACITY_UNKNOWN_ITEM" );

		Assert.IsNull( PeepBehaviour.LongestQueue( bounce, null ), "a thing the catalogue does not know" );
		Assert.AreEqual( unknown + 1, Counted( "QUEUE_CAPACITY_UNKNOWN_ITEM" ), "counted" );
		Assert.AreEqual( 100u, PeepBehaviour.LongestQueue( World().Objects.Single( o => o.ThingId == JungleSpray ), null ) );
	}

	/// <summary>
	/// <b>A queue is too long at its longest or past it</b> (<c>FUN_004ddb60</c>): the Belly Bounce takes a twenty-first
	/// guest at 20 and not at 21, and a count is read unsigned, so -1 is past any longest.
	/// </summary>
	[TestMethod]
	public void AQueueIsTooLongAtItsLongest()
	{
		var catalogue = Catalogue();
		var bounce = World().Objects.Single( o => o.ThingId == BellyBounce );
		var item = ItemFor( catalogue, bounce );

		Assert.IsFalse( PeepBehaviour.QueueTooLong( bounce, item, 20 ) );
		Assert.IsTrue( PeepBehaviour.QueueTooLong( bounce, item, 21 ) );
		Assert.IsTrue( PeepBehaviour.QueueTooLong( bounce, item, -1 ), "unsigned" );
	}

	/// <summary>
	/// <b>A guest arriving at a full queue is turned away and remembers it</b>: the Belly Bounce at capacity 2 holds 8,
	/// inside its sixteen places of room, so an arrival finding 8 queueing is refused ("queue is too long!"), keeps their
	/// happiness, lets go of it and pushes it onto their refusals; one finding 7 joins, eighth in line.
	/// </summary>
	[TestMethod]
	public void AnArrivalAtAFullQueueIsTurnedAway()
	{
		foreach ( var (queued, refused) in new[] { (8, true), (7, false) } )
		{
			var (events, thoughts) = (Counted( "ARRIVAL_TOO_LONG_EVENT" ), Counted( "ARRIVAL_TOO_LONG_THOUGHT_0x10" ));
			var (state, arriving) = ArrivesAtTheBellyBounce( capacity: 2, queued );

			if ( refused )
			{
				Assert.AreEqual( PeepState.Deciding, arriving.State, $"{queued} queueing: turned away" );
				Assert.AreEqual( 0, arriving.MajorDest, "lets go of it" );
				Assert.AreEqual( Before, arriving.Happiness, 0.001f, "and loses nothing" );
				Assert.AreEqual( BellyBounce, arriving.PreviousTemporaryRides[0], "but remembers it" );
				Assert.AreEqual( -1, state.PositionInQueue( BellyBounce, arriving.ThingId ), "not in it" );
				Assert.AreEqual( IdleStamp, arriving.TimeStartedIdling, "the idle stamp is left alone" );
				Assert.AreEqual( events + 1, Counted( "ARRIVAL_TOO_LONG_EVENT" ), "event 0x15 is counted" );
				Assert.AreEqual( thoughts + 1, Counted( "ARRIVAL_TOO_LONG_THOUGHT_0x10" ), "and thought 0x10" );
			}
			else
			{
				Assert.AreEqual( BellyBounce, arriving.MajorDest, $"{queued} queueing: joins" );
				Assert.AreEqual( queued, state.PositionInQueue( BellyBounce, arriving.ThingId ), "at the back" );
				Assert.AreEqual( events, Counted( "ARRIVAL_TOO_LONG_EVENT" ), "no event" );
			}
		}
	}

	/// <summary>
	/// <b>A guest arriving at a queue with no room is turned away still naming the thing</b> (<c>FUN_004dda20</c>,
	/// <c>0x004ffe0a</c>): at its saved capacity of 5 the Belly Bounce's longest is 21, past its sixteen places of room,
	/// so an arrival finding 16 queueing fails the room gate first. They think again, keep <c>MajorDest</c>, lose
	/// nothing, remember no refusal and keep their idle stamp; event <c>0x15</c> is counted, and no thought. One
	/// finding 15 joins, sixteenth in line.
	/// </summary>
	[TestMethod]
	public void AnArrivalAtAQueueWithNoRoomStillNamesTheThing()
	{
		foreach ( var (queued, refused) in new[] { (16, true), (15, false) } )
		{
			var (events, tooLong) = (Counted( "ARRIVAL_NO_ROOM_EVENT" ), Counted( "ARRIVAL_TOO_LONG_EVENT" ));
			var (state, arriving) = ArrivesAtTheBellyBounce( capacity: 5, queued );

			Assert.AreEqual( BellyBounce, arriving.MajorDest, $"{queued} queueing: the thing is named either way" );
			Assert.AreEqual( Before, arriving.Happiness, 0.001f, "and nothing is lost" );
			Assert.AreEqual( 0, arriving.PreviousTemporaryRides[0], "nor a refusal remembered" );
			Assert.AreEqual( tooLong, Counted( "ARRIVAL_TOO_LONG_EVENT" ), "the too-long gate is not reached" );

			if ( refused )
			{
				Assert.AreEqual( PeepState.Deciding, arriving.State, "16 queueing: turned away" );
				Assert.AreEqual( -1, state.PositionInQueue( BellyBounce, arriving.ThingId ), "not in it" );
				Assert.AreEqual( IdleStamp, arriving.TimeStartedIdling, "the idle stamp is left alone" );
				Assert.AreEqual( events + 1, Counted( "ARRIVAL_NO_ROOM_EVENT" ), "event 0x15 is counted once" );
			}
			else
			{
				Assert.AreEqual( PeepState.SteppingUpQueue, arriving.State, "15 queueing: joins" );
				Assert.AreEqual( queued, state.PositionInQueue( BellyBounce, arriving.ThingId ), "at the back" );
				Assert.AreEqual( events, Counted( "ARRIVAL_NO_ROOM_EVENT" ), "no event" );
			}
		}
	}

	private const int IdleStamp = 17;

	/// <summary>
	/// A type 2 guest (preferring 50 against the Belly Bounce's 40, so past the excitement gate) taking their arrival
	/// turn on the back cell of the Belly Bounce's queue, set to <paramref name="capacity"/> with
	/// <paramref name="queued"/> already in it.
	/// </summary>
	private (ParkState State, Peep Arriving) ArrivesAtTheBellyBounce( int capacity, int queued )
	{
		var world = World();
		var state = new ParkState( world );
		var bounce = world.Objects.Single( o => o.ThingId == BellyBounce );
		state.ReplaceObject( bounce with { OperatingCapacity = capacity } );

		for ( var guest = 70; state.QueueCount( BellyBounce, _ => true ) < queued; ++guest )
			state.JoinQueue( BellyBounce, guest );

		var balance = new ParkBalance( "jungle", easyMode: true );
		var behaviour = new PeepBehaviour( world, new Random( 1 ),
			new ParkAdmission( balance, world.Economy!.Value.AdmissionFee ), () => ParkRides.GateIsOpen, state,
			Catalogue(), stillQueueing: _ => true, balance: balance );

		var (x, y) = MapStep.CellAt( ParkRideChoice.QueueCellsFor( world, bounce ).BackOfQueue );
		var centre = new FixedVector( PeepNavigator.WaypointCentre( x ), PeepNavigator.WaypointCentre( y ) );
		var saved = world.People.First( person => person.Guest is { } );
		var arriving = new Peep( 30, saved.Guest!.Value with {
			State = (int)PeepState.GoingToRide, PersonType = 2, Happiness = Before, MajorDest = BellyBounce },
			saved.Navigator with { X = centre.X, Y = centre.Y, TargetX = centre.X, TargetY = centre.Y } )
		{
			TimeStartedIdling = IdleStamp
		};

		Assert.AreEqual( 16, ParkRideChoice.QueueCellsFor( world, bounce ).Cells * ParkRideChoice.QueueRoomPerCell,
			"room for sixteen" );

		behaviour.Step( arriving, new PeepWalk( arriving.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked ),
			playing: null, Sweep );

		return (state, arriving);
	}

	/// <summary>
	/// <b>A queuer past the longest is put out on their own turn</b> (arm 5a, <c>0x0050059d</c>), losing
	/// <c>MediumHappinessChange</c>; one at it stays. At capacity 5 the Belly Bounce holds 21, so place 22 goes and 21
	/// stays; turned down to capacity 2, it holds 8, so place 9 goes and 8 stays.
	/// </summary>
	[TestMethod]
	public void AQueuerPastTheLongestIsPutOut()
	{
		foreach ( var (capacity, stays, goes) in new[] { (5, 21, 22), (2, 8, 9) } )
		{
			var world = World();
			var state = new ParkState( world );
			var bounce = world.Objects.Single( o => o.ThingId == BellyBounce );
			state.ReplaceObject( bounce with { OperatingCapacity = capacity } );

			using var rse = Data().OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );
			var script = new RideScript( new RideScriptFile( rse ) );
			var guests = new Dictionary<int, Peep>();
			var balance = new ParkBalance( "jungle", easyMode: true );
			var admission = new ParkAdmission( balance, world.Economy!.Value.AdmissionFee );

			var behaviour = new PeepBehaviour( world, new Random( 1 ), admission, () => ParkRides.GateIsOpen, state,
				Catalogue(),
				leaveQueue: ( ride, id ) => ParkRideOperation.LeaveQueue( state, script, ride.ThingId, id ),
				stillQueueing: id => guests.TryGetValue( id, out var peep ) && ParkRideOperation.IsQueueing( peep ) );

			// Every queuer must be one of these guests, for the queue walk to count them as still queueing.
			Assert.AreEqual( 0, state.QueueCount( BellyBounce, _ => true ), "the save queues nobody for the Belly Bounce" );

			for ( var id = 100; guests.Count <= goes; ++id )
			{
				var peep = Guest( id );
				guests[id] = peep;
				peep.QueuePos = state.JoinQueue( BellyBounce, id );
			}

			var staying = guests.Values.Single( peep => peep.QueuePos == stays );
			var going = guests.Values.Single( peep => peep.QueuePos == goes );

			Turn( world, behaviour, going );
			Turn( world, behaviour, staying );

			Assert.AreEqual( PeepState.Deciding, going.State, $"capacity {capacity}: place {goes} is put out" );
			Assert.AreEqual( 0, going.MajorDest );
			Assert.AreEqual( Before - admission.MediumHappinessChange, going.Happiness, 0.001f, "for MediumHappinessChange" );
			Assert.AreEqual( -1, state.PositionInQueue( BellyBounce, going.ThingId ), "out of the queue" );

			Assert.AreEqual( PeepState.InQueue, staying.State, $"capacity {capacity}: place {stays} stays" );
			Assert.AreEqual( BellyBounce, staying.MajorDest );
			Assert.AreEqual( Before, staying.Happiness, 0.001f );
		}
	}

	/// <summary><b>The <c>spend</c> census prints each thing's longest queue</b>: 21 for the Belly Bounce, 100 for the Spray.</summary>
	[TestMethod]
	public void TheSpendCensusPrintsTheLongestQueue()
	{
		var world = World();
		var people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), null, new ParkState( world ), Catalogue() );

		try
		{
			var lines = people.SpendCensus().ToArray();

			StringAssert.Contains( lines.Single( line => line.StartsWith( $"thing {BellyBounce,2} " ) ), " longest 21 " );
			StringAssert.Contains( lines.Single( line => line.StartsWith( $"thing {JungleSpray,2} " ) ), " longest 100 " );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	private static ParkItemCatalogue.Item ItemFor( ParkItemCatalogue catalogue, ParkWorld.CatalogueObject thing )
		=> catalogue.TryGet( thing.CatalogueId, out var item ) ? item : throw new AssertFailedException( "no item" );

	private static ParkWorld.NavigatorState StandingStill => new(
		X: 0, Y: 0, VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
		Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
		MaxForce: 0, MaxSpeed: 0, NavMode: 0, CantReachDest: 0, PathFinished: false,
		PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
		BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

	/// <summary>A guest standing in the Belly Bounce's queue, content, not yet in its links.</summary>
	private static Peep Guest( int thingId )
		=> new( thingId, new ParkWorld.GuestState(
			State: (int)PeepState.InQueue, SavedState: (int)PeepState.Deciding, PersonType: 0,
			Cash: 300, ExitLevel: 100, Happiness: Before, Thirst: Content, Hunger: Content, Toilet: Content,
			Vomit: 0f, Litter: 0f, MajorDest: BellyBounce, QueuePos: 0, PrankeryIndex: 0 ), StandingStill );

	private static void Turn( ParkWorld world, PeepBehaviour behaviour, Peep peep )
		=> behaviour.Step( peep, new PeepWalk( peep.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked ),
			playing: null, Sweep );
}
