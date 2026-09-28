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

	private const int BellyBounce = 13;

	private const int JungleSpray = 14;

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private static ParkBalance Balance() => new( "jungle", easyMode: true );

	/// <summary>
	/// <b>Two kinds of guest on one cell choose different rides, through the park's own wiring.</b> At (48,25) the
	/// Belly Bounce's entry is 96 for distance and the Jungle Spray's 91. A type 3 prefers 35: the Spray's 35 scores
	/// 100 for excitement and the Belly Bounce's 40 scores 90, so 19 against 18. A type 0 prefers 80: 10 against 20,
	/// so 10 against 11. Both win outright, so neither turns on the tick's tie-break.
	///
	/// <para>
	/// With no balance behind the chooser every kind prefers 50 and scores every candidate alike, so no two kinds
	/// on one cell can choose differently: the type 3 takes the Belly Bounce, 17 against 16.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TwoKindsOfGuestOnOneCellChooseByWhatEachPrefers()
	{
		var world = World();
		var people = new ParkPeople( world, Balance(), null, new ParkState( world ), new ParkItemCatalogue( "jungle", data ) );

		var likesItQuiet = people.Admit( 48, 25, personType: 3 );
		var likesItWild = people.Admit( 48, 25, personType: 0 );

		var why = people.WhyCensus().ToArray();

		StringAssert.Contains( Chose( why, likesItQuiet ), $"chose thing {JungleSpray} ",
			"a type 3, preferring 35, takes the Jungle Spray" );
		StringAssert.Contains( Chose( why, likesItWild ), $"chose thing {BellyBounce} ",
			"a type 0, preferring 80, takes the Belly Bounce" );
	}

	private static string Chose( string[] census, int thingId )
		=> census.Single( line => line.StartsWith( $"thing {thingId,3} " ) );

	/// <summary>
	/// <b>And the choice a guest acts on is the same</b>: deciding at (48,25), a type 3 sets off for the Jungle Spray
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
				var peep = Standing( 30, personType, PeepState.Deciding, 48, 25, thing: 0 );

				behaviour.Step( peep, new PeepWalk( peep.Navigator, blocked ), playing: null, 1000 );

				if ( peep.State == PeepState.GoingToRide )
					return peep.MajorDest;
			}

			Assert.Fail( $"a type {personType} was never offered a ride in 60 turns" );
			return 0;
		}

		Assert.AreEqual( JungleSpray, SetsOffFor( 3 ), "a type 3, preferring 35, sets off for the Jungle Spray" );
		Assert.AreEqual( BellyBounce, SetsOffFor( 0 ), "a type 0, preferring 80, sets off for the Belly Bounce" );
	}

	/// <summary>
	/// <b>An arriving guest is drawn as one of the eight kinds</b> (<c>FUN_004faec0</c>, <c>0x004fb019</c>), and
	/// starts with that kind's <c>PeepTypes[n].StartingCash</c>: 300, 500, 600, 700, 750, 600, 400 and 500.
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
			Assert.AreEqual( startingCash[guest.PersonType], guest.Cash, $"guest {guest.ThingId}, a type {guest.PersonType}" );
	}

	/// <summary>
	/// <b>A guest turns away from a ride too far from what their kind prefers</b> (<c>FUN_004fd4e0</c>, the byte at
	/// <c>0x004fd50a</c>). OpenTPW scores the Jungle Spray 35: a type 0 prefers 80, 45 away, past the 44 allowed, and
	/// gives up on it at its back cell; a type 2 prefers 50, 15 away, and joins. With no balance both prefer 50 and both
	/// join. The original computes the Spray's 30, which turns the type 0 away as well.
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

		behaviour.Step( wild, new PeepWalk( wild.Navigator, blocked ), playing: null, 40 );
		behaviour.Step( middling, new PeepWalk( middling.Navigator, blocked ), playing: null, 40 );

		Assert.AreEqual( PeepState.Deciding, wild.State, "the type 0 thinks again" );
		Assert.AreEqual( 0, wild.MajorDest, "and names nothing" );
		Assert.AreEqual( -1, state.PositionInQueue( JungleSpray, wild.ThingId ), "and is not in the queue" );

		Assert.AreEqual( PeepState.SteppingUpQueue, middling.State, "the type 2 walks to their place" );
		Assert.AreEqual( 0, state.PositionInQueue( JungleSpray, middling.ThingId ), "at the head of the queue" );
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
