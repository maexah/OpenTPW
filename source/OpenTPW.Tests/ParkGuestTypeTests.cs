using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The eight kinds of guest: what each prefers, read from the park's balance where it chooses and where it
/// arrives, and the kind an arriving guest is drawn as. See <c>docs/exe/ride-operation.md</c>, "What a thing is
/// worth to a guest".
///
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkGuestTypeTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	/// <summary>Every <see cref="ParkPeople"/> a test made goes with it, out of <see cref="Entity.All"/> and <c>Current</c>.</summary>
	[TestCleanup]
	public void LetThePeopleGo() => TestRun.DeleteEvery<ParkPeople>();

	private const int BellyBounce = 13;

	private const int JungleSpray = 14;

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private static ParkBalance Balance() => new( "jungle", easyMode: true );

	/// <summary>
	/// <b>Two kinds of guest on one cell choose different rides, through the park's own wiring.</b> At (55,28) the
	/// Jungle Spray's back of queue is 98 for distance and the Belly Bounce's 84. A type 3 prefers 35: the Spray's
	/// computed 30 and the Belly Bounce's 40 both score 90 for excitement, so 18 against 17. A type 0 prefers 80: nought
	/// against 20, so 9, which is not enough, against 10. Both win outright, so neither turns on the tick's tie-break.
	///
	/// <para>
	/// With no balance behind the chooser every kind prefers 50 and scores every candidate alike, so no two kinds
	/// on one cell can choose differently.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TwoKindsOfGuestOnOneCellChooseByWhatEachPrefers()
	{
		var world = World();
		var people = new ParkPeople( world, Balance(), null, new ParkState( world ), new ParkItemCatalogue( "jungle", data ) );

		var likesItQuiet = people.Admit( 55, 28, personType: 3 );
		var likesItWild = people.Admit( 55, 28, personType: 0 );

		// Isolate kind preference from the constructor's independently drawn needs.
		foreach ( var id in new[] { likesItQuiet, likesItWild } )
		{
			var guest = people.Guests[id];
			guest.Thirst = guest.Hunger = guest.Toilet = 0f;
		}

		var why = people.WhyCensus().ToArray();

		StringAssert.Contains( Chose( why, likesItQuiet ), $"chose thing {JungleSpray} ",
			"a type 3, preferring 35, takes the Jungle Spray" );
		StringAssert.Contains( Chose( why, likesItWild ), $"chose thing {BellyBounce} ",
			"a type 0, preferring 80, takes the Belly Bounce" );
	}

	private static int Counted( string gap ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == gap ).Times;

	private static string Chose( string[] census, int thingId )
		=> census.Single( line => line.StartsWith( $"thing {thingId,3} " ) );

	/// <summary>
	/// <b>And the choice a guest acts on is the same</b>: deciding at (55,28), a type 3 sets off for the Jungle Spray
	/// and a type 0 for the Belly Bounce, the thing each is sent to written in <see cref="Peep.MajorDest"/>. The turn
	/// offers a ride on one roll in three (<c>FUN_004fec90</c>), so each is made afresh and stepped until it is offered.
	/// </summary>
	[TestMethod]
	public void AGuestDecidingSetsOffForWhatTheirKindPrefers()
	{
		var world = World();
		var state = new ParkState( world );
		var behaviour = new PeepBehaviour( world, new Random( 1 ),
			new ParkAdmission( Balance(), world.Economy!.Value.AdmissionFee ), () => ParkRides.GateIsOpen, state,
			new ParkItemCatalogue( "jungle", data ), balance: Balance() );
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		int SetsOffFor( int personType )
		{
			for ( var turn = 0; turn < 60; ++turn )
			{
				var peep = Standing( 30, personType, PeepState.Deciding, 55, 28, thing: 0 );
				var events = Counted( "DECIDE_CHOSEN_EVENT" );

				peep.TimeStartedIdling = 17;

				behaviour.Step( peep, new PeepWalk( peep.Navigator, blocked ), playing: null, 1000 );

				if ( peep.State == PeepState.GoingToRide )
				{
					// A choice that names a thing stamps nothing (0x004ff437..0x004ff457), and pushes event 2.
					Assert.AreEqual( 17, peep.TimeStartedIdling, "a guest who chose keeps their idle stamp" );
					Assert.AreEqual( events + 1, Counted( "DECIDE_CHOSEN_EVENT" ), "event 2, counted" );

					return peep.MajorDest;
				}
			}

			Assert.Fail( $"a type {personType} was never offered a ride in 60 turns" );
			return 0;
		}

		Assert.AreEqual( JungleSpray, SetsOffFor( 3 ), "a type 3, preferring 35, sets off for the Jungle Spray" );
		Assert.AreEqual( BellyBounce, SetsOffFor( 0 ), "a type 0, preferring 80, sets off for the Belly Bounce" );
	}

	/// <summary>
	/// <b>An arriving guest is drawn as one of the eight kinds</b> (<c>FUN_004faec0</c>, <c>0x004fb019</c>), and
	/// starts within fifteen percent of that kind's <c>PeepTypes[n].StartingCash</c>.
	/// </summary>
	[TestMethod]
	public void AnArrivingGuestIsDrawnAsOneOfTheEightKinds()
	{
		var world = World();
		var people = new ParkPeople( world, Balance(), null, new ParkState( world ), random: new Random( 1 ) );

		var arrivals = Enumerable.Range( 0, 64 )
			.Select( _ => people.Guests[people.Admit( 42, 5 )] )
			.ToArray();

		CollectionAssert.AreEquivalent( Enumerable.Range( 0, 8 ).ToArray(),
			arrivals.Select( guest => guest.PersonType ).Distinct().ToArray(), "all eight kinds, and no other" );

		int[] startingCash = [300, 500, 600, 700, 750, 600, 400, 500];

		foreach ( var guest in arrivals )
			Assert.IsTrue( guest.Cash >= startingCash[guest.PersonType] * 85 / 100
				&& guest.Cash <= startingCash[guest.PersonType] * 115 / 100, $"guest {guest.ThingId}, a type {guest.PersonType}" );
	}

	/// <summary>
	/// <b>A guest turns away from a ride too far from what their kind prefers</b> (<c>FUN_004fd4e0</c>, the byte at
	/// <c>0x004fd50a</c>), and remembers it (<c>FUN_004fdc60</c>, <c>0x004ffce6</c>). The Jungle Spray's computed
	/// excitement is 30: a type 0 prefers 80, 50 away, past the 44 allowed, gives up on it at its back cell and puts it
	/// in front of their refusals; a type 2 prefers 50, 20 away, and joins, remembering nothing. The refusal is the
	/// "not exciting enough" arm: event 5 and thought <c>0xc</c> counted, and the idle stamp zeroed
	/// (<c>0x004ffcf6</c>), where the joiner's is left alone.
	/// </summary>
	[TestMethod]
	public void AGuestTurnsAwayFromARideTooFarFromWhatTheirKindPrefers()
	{
		var world = World();
		var state = new ParkState( world );
		var behaviour = new PeepBehaviour( world, new Random( 1 ), null, () => ParkRides.GateIsOpen, state,
			new ParkItemCatalogue( "jungle", data ), balance: Balance() );
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		var wild = Standing( 30, personType: 0, PeepState.GoingToRide, 52, 29 );
		var middling = Standing( 31, personType: 2, PeepState.GoingToRide, 52, 29 );
		wild.TimeStartedIdling = middling.TimeStartedIdling = 17;

		string[] arms = ["ARRIVAL_NOT_EXCITING_EVENT", "ARRIVAL_TOO_EXCITING_EVENT"];
		var before = arms.Select( Counted ).ToArray();

		behaviour.Step( wild, new PeepWalk( wild.Navigator, blocked ), playing: null, 40 );
		behaviour.Step( middling, new PeepWalk( middling.Navigator, blocked ), playing: null, 40 );

		CollectionAssert.AreEqual( new[] { before[0] + 1, before[1] },
			arms.Select( Counted ).ToArray(), "event 5, once, and not the other arm's" );
		Assert.AreEqual( 0xc, wild.Thoughts.Last, "thought 0xc, bored" );
		Assert.AreEqual( 0, middling.Thoughts.Last, "and a joiner thinks nothing" );
		Assert.AreEqual( 0, wild.TimeStartedIdling, "the refusal zeroes the idle stamp" );
		Assert.AreEqual( 17, middling.TimeStartedIdling, "which a joiner keeps until the queue stamps it" );

		Assert.AreEqual( PeepState.Deciding, wild.State, "the type 0 thinks again" );
		Assert.AreEqual( 0, wild.MajorDest, "and names nothing" );
		Assert.AreEqual( -1, state.PositionInQueue( JungleSpray, wild.ThingId ), "and is not in the queue" );
		CollectionAssert.AreEqual( new[] { JungleSpray, 0, 0, 0 }, wild.PreviousTemporaryRides.ToArray(),
			"and remembers turning away from it" );
		CollectionAssert.AreEqual( new[] { 0, 0, 0, 0 }, middling.PreviousTemporaryRides.ToArray(),
			"where the type 2 has nothing to remember" );

		Assert.AreEqual( PeepState.SteppingUpQueue, middling.State, "the type 2 walks to their place" );
		Assert.AreEqual( 0, state.PositionInQueue( JungleSpray, middling.ThingId ), "at the head of the queue" );
	}

	/// <summary>
	/// <b>The turn-away reads the thing's computed excitement</b>: priced at its prize of 50, the Jungle Spray's
	/// excitement is 20, and a type 1, preferring 65, is 45 away and turns away from it - where its file's 35 would
	/// have let them join.
	/// </summary>
	[TestMethod]
	public void TheTurnAwayReadsTheComputedExcitement()
	{
		var world = World();
		var state = new ParkState( world );
		var behaviour = new PeepBehaviour( world, new Random( 1 ), null, () => ParkRides.GateIsOpen, state,
			new ParkItemCatalogue( "jungle", data ), balance: Balance() );
		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		state.ReplaceObject( state.Objects.Single( o => o.ThingId == JungleSpray ) with { PricePerUse = 50 } );

		var guest = Standing( 30, personType: 1, PeepState.GoingToRide, 52, 29 );

		behaviour.Step( guest, new PeepWalk( guest.Navigator, blocked ), playing: null, 40 );

		Assert.AreEqual( PeepState.Deciding, guest.State, "the type 1 thinks again" );
		Assert.AreEqual( JungleSpray, guest.PreviousTemporaryRides[0], "remembering the Spray" );
	}

	/// <summary>
	/// <b>A dirty toilet that declares an excitement turns every arrival away</b>: <c>FUN_004fd4e0</c> answers 100
	/// for one before it compares anything (<c>0x004fd4ea</c>). No shipped toilet declares one, so the Jungle Spray
	/// is given the toilet bit: at a State of repair of 25 the type 2 joins as before, and at 24 turns away, by the
	/// "too exciting" arm: event 4 and thought <c>0xf</c>.
	/// </summary>
	[TestMethod]
	public void ADirtyToiletWithAnExcitementTurnsEveryArrivalAway()
	{
		foreach ( var repair in new[] { 25f, 24f } )
		{
			var world = World();
			var state = new ParkState( world );
			var behaviour = new PeepBehaviour( world, new Random( 1 ), null, () => ParkRides.GateIsOpen, state,
				new ParkItemCatalogue( "jungle", data ), balance: Balance() );
			var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;
			var spray = state.Objects.Single( o => o.ThingId == JungleSpray );

			state.ReplaceObject( spray with
			{
				Flags = (ushort)(spray.Flags | ParkWorld.CatalogueObject.ToiletFlag),
				StateOfRepair = repair
			} );

			var middling = Standing( 31, personType: 2, PeepState.GoingToRide, 52, 29 );
			var (events, other) = (Counted( "ARRIVAL_TOO_EXCITING_EVENT" ), Counted( "ARRIVAL_NOT_EXCITING_EVENT" ));

			behaviour.Step( middling, new PeepWalk( middling.Navigator, blocked ), playing: null, 40 );

			var dirty = repair < ParkState.DirtyBelow ? 1 : 0;

			Assert.AreEqual( events + dirty, Counted( "ARRIVAL_TOO_EXCITING_EVENT" ), $"repair {repair}: event 4" );
			Assert.AreEqual( dirty == 1 ? 0xf : 0, middling.Thoughts.Last, $"repair {repair}: thought 0xf" );
			Assert.AreEqual( other, Counted( "ARRIVAL_NOT_EXCITING_EVENT" ), "never the other arm" );

			if ( repair < ParkState.DirtyBelow )
			{
				Assert.AreEqual( PeepState.Deciding, middling.State, "at 24 the type 2 thinks again" );
				Assert.AreEqual( JungleSpray, middling.PreviousTemporaryRides[0], "remembering it" );
			}
			else
			{
				Assert.AreEqual( PeepState.SteppingUpQueue, middling.State, "at 25 the type 2 joins" );
			}
		}
	}

	/// <summary>
	/// A guest of <paramref name="personType"/> in <paramref name="state"/> on the centre of a cell, naming
	/// <paramref name="thing"/> - the Jungle Spray, whose back cell is (52,29), unless told otherwise.
	/// </summary>
	private static Peep Standing( int thingId, int personType, PeepState state, int cellX, int cellY,
		int thing = JungleSpray )
	{
		var centre = new FixedVector( PeepNavigator.WaypointCentre( cellX ), PeepNavigator.WaypointCentre( cellY ) );

		return new Peep( thingId, new ParkWorld.GuestState(
			State: (int)state, SavedState: (int)PeepState.Deciding, PersonType: personType,
			Cash: 300, ExitLevel: 100, Happiness: 50f, Thirst: 10f, Hunger: 10f, Toilet: 10f,
			Vomit: 0f, Litter: 0f, MajorDest: thing, QueuePos: 0, PrankeryIndex: 0 ),
			new ParkWorld.NavigatorState(
				X: centre.X, Y: centre.Y, VelocityX: 0, VelocityY: 0, TargetX: centre.X, TargetY: centre.Y,
				Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
				MaxForce: 0, MaxSpeed: 0, NavMode: 0, CantReachDest: 0, PathFinished: false,
				PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
				BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 ) );
	}
}
