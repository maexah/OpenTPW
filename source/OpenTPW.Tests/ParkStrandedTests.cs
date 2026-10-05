using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The stranded bookkeeping and the thought bubble - <c>docs/exe/ride-operation.md</c>, "The stranded
/// bookkeeping", "Thoughts and their pictures", "The red square under a stranded person".
///
/// <para>
/// The park is the shipped Lost Kingdom with the Belly Bounce's queue tail cut off as the game run cuts it:
/// (48,22), the path the queue's back cell (49,22) joins, made bare ground, so a guest on (49,22) has one
/// link, the one its own <c>mDirection</c> names, and every wander from it is a dead end.
/// </para>
/// </summary>
[TestClass]
public class ParkStrandedTests
{
	private const string Theme = "jungle";
	private const int East = 0x04;

	private sealed class Counted( int draw ) : Random
	{
		public int Taken { get; private set; }

		public override int Next()
		{
			++Taken;

			return draw;
		}
	}

	private static (ParkWorld World, ParkState State, ParkPeople People) Open()
	{
		var data = GameData.Required();
		FileSystem = data;

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( world );

		return (world, state, new ParkPeople( world, new ParkBalance( Theme, easyMode: true ), gateStatus: null, state,
			new ParkItemCatalogue( Theme, data ) ));
	}

	private static void Close( ParkPeople people )
	{
		people.Delete();
		Entity.ApplyDeletions();
	}

	/// <summary>(48,22) made bare ground, and (49,22) left with its link east alone.</summary>
	private static void CutTheTail( ParkState state )
	{
		state.SetRecord( 48, 22, state.Record( 48, 22 ) with { Type = CellEdge.Nothing, Neighbours = 0 } );
		state.SetRecord( 49, 22, state.Record( 49, 22 ) with { Neighbours = East } );

		Assert.AreEqual( ParkRideChoice.QueueCellType, state.Record( 49, 22 ).Type );
		Assert.AreEqual( East, state.Record( 49, 22 ).Direction, "the back cell's direction names its one link" );
	}

	private static Peep GuestAt( int cellX, int cellY, PeepState state = PeepState.Deciding, int thingId = 7 )
		=> new( thingId, new ParkWorld.GuestState(
			State: (int)state, SavedState: (int)PeepState.Wandering, PersonType: 0, Cash: 300,
			ExitLevel: 100, Happiness: 50f, Thirst: 10f, Hunger: 10f, Toilet: 10f, Vomit: 0f,
			Litter: 0f, MajorDest: 0, QueuePos: 0, PrankeryIndex: 0 ),
			new ParkWorld.NavigatorState(
				X: PeepNavigator.WaypointCentre( cellX ), Y: PeepNavigator.WaypointCentre( cellY ),
				VelocityX: 0, VelocityY: 0,
				TargetX: PeepNavigator.WaypointCentre( cellX ), TargetY: PeepNavigator.WaypointCentre( cellY ),
				Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
				MaxForce: 26214, MaxSpeed: 13107, NavMode: 0, CantReachDest: 0, PathFinished: false,
				PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
				BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 ) );

	private static PeepBehaviour Behaviour( ParkWorld world, ParkState state, Random random )
		=> new( world, random, new ParkAdmission( new ParkBalance( Theme, easyMode: true ),
			world.Economy!.Value.AdmissionFee ), () => ParkRides.GateIsOpen, state );

	private static PeepWalk WalkOn( ParkState state, Peep peep )
		=> new( peep.Navigator, new CellEdge( state.Record, ParkPeople.WalkingMode ).Blocked ) { Ground = state };

	/// <summary>
	/// A wander that meets a dead end thinks <c>0x11</c>, raises the blue question mark and stamps the guest with a
	/// fresh value of the counter (<c>0x004f9deb</c>, <c>0x004f9e09</c>).
	/// </summary>
	[TestMethod]
	public void ADeadEndStampsTheGuestAndThinksConfused()
	{
		var (world, state, people) = Open();

		try
		{
			CutTheTail( state );

			var peep = GuestAt( 49, 22 );
			var walk = WalkOn( state, peep );
			var before = ParkState.Counter;

			Assert.IsFalse( Behaviour( world, state, new Random( 1 ) ).SetRandomDest( peep, walk ) );

			Assert.IsTrue( peep.StrandedTime > before, "stamped with a value the counter gave after the call began" );
			Assert.AreEqual( Thoughts.Confused, peep.Thoughts.Last );
			Assert.AreEqual( (0, 15), peep.Thoughts.Bubble, "SPR_TB's set 15, the blue question mark" );
			Assert.IsFalse( walk.HasRoute, "and nothing is routed" );
		}
		finally
		{
			Close( people );
		}
	}

	/// <summary>
	/// A stranded guest is refused a wander before any draw, with thought <c>0x11</c> again (<c>0x004f94e2</c>),
	/// and refused a route to a cell with no destination written (<c>FUN_004fa530</c>), so the chooser can name
	/// them nothing.
	/// </summary>
	[TestMethod]
	public void AStrandedGuestIsRefusedAWanderARouteAndARide()
	{
		var (world, state, people) = Open();

		try
		{
			CutTheTail( state );

			var peep = GuestAt( 49, 22 );
			var walk = WalkOn( state, peep );
			var draws = new Counted( 1 );
			var behaviour = Behaviour( world, state, draws );

			Assert.IsFalse( behaviour.SetRandomDest( peep, walk ) );

			var (stamp, taken, target) = (peep.StrandedTime, draws.Taken, peep.Navigator.Target);

			Assert.AreNotEqual( 0u, stamp );
			peep.Thoughts.Restore( 0, 0 );

			Assert.IsFalse( behaviour.SetRandomDest( peep, walk ), "refused" );
			Assert.AreEqual( taken, draws.Taken, "before the pass count is drawn" );
			Assert.AreEqual( Thoughts.Confused, peep.Thoughts.Last, "with the thought again" );
			Assert.AreEqual( stamp, peep.StrandedTime, "and the stamp kept" );

			// (50,22) is the next queue cell, a step away and open to them.
			Assert.IsFalse( PeepBehaviour.SendTo( state, peep, walk, (50, 22) ), "a route is refused" );
			Assert.AreEqual( target, peep.Navigator.Target, "with no destination written" );
			Assert.AreEqual( stamp, peep.StrandedTime );

			Assert.IsFalse( behaviour.ChooseSomewhereToGo( peep, walk, tick: 500 ), "and the chooser names nothing" );
			Assert.AreEqual( 0, peep.MajorDest );

			// The same guest with no stamp is routed there, which is what makes the refusals above the stamp's.
			peep.StrandedTime = 0;
			Assert.IsTrue( PeepBehaviour.SendTo( state, peep, walk, (50, 22) ), "unstamped, the same route is found" );
		}
		finally
		{
			Close( people );
		}
	}

	/// <summary>
	/// The test of the nine cells (<c>FUN_004fa770</c>): a block stamp newer than the guest's frees them only in a
	/// block one of the 3 x 3 cells around the base lies in, and the base of a guest on a queue cell is the far end
	/// of the queue run, not their own cell.
	/// </summary>
	[TestMethod]
	public void AStampNearTheQueuesFarEndFreesAStrandedGuest()
	{
		var (world, state, people) = Open();

		try
		{
			CutTheTail( state );

			// The run carried on west over the block's edge at x 48: (48,22), (47,22) and (46,22) made queue cells
			// facing back east, so the far end from (49,22) is (46,22), in block (2,1), and its nine cells are
			// x 45 to 47: none in the guest's own block (3,1).
			foreach ( var x in new[] { 48, 47, 46 } )
				state.SetRecord( x, 22, state.Record( x, 22 ) with { Type = ParkRideChoice.QueueCellType, Direction = East, Neighbours = East } );

			Assert.AreEqual( MapStep.CellId( 46, 22 ), FarEnd( world, state, 49, 22 ) );

			var peep = GuestAt( 49, 22 );
			var walk = WalkOn( state, peep );

			peep.StrandedTime = ParkState.NextCounter();
			Assert.IsTrue( PeepBehaviour.StillStranded( state, peep, walk ), "no stamp is as new as theirs" );

			state.StampBlock( 49, 22 );
			Assert.IsTrue( PeepBehaviour.StillStranded( state, peep, walk ), "their own block is not one of the nine cells'" );

			state.StampBlock( 40, 40 );
			Assert.IsTrue( PeepBehaviour.StillStranded( state, peep, walk ), "nor is a far one" );

			state.StampBlock( 33, 30 );
			Assert.IsFalse( PeepBehaviour.StillStranded( state, peep, walk ), "block (2,1), the far end's, frees them" );

			Assert.IsFalse( PeepBehaviour.RefusedAsStranded( state, peep, walk ) );
			Assert.AreNotEqual( 0u, peep.StrandedTime, "the test alone zeroes nothing; the route or the wander after it does" );

			Assert.IsTrue( PeepBehaviour.SendTo( state, peep, walk, (50, 22) ), "freed, a route is found" );
			Assert.AreEqual( 0u, peep.StrandedTime, "and the route asked for zeroes the stamp (0x004fa5d0)" );

			// A guest on plain path is based on their own cell.
			var onPath = GuestAt( 47, 19, thingId: 8 );
			var pathWalk = WalkOn( state, onPath );

			onPath.StrandedTime = ParkState.NextCounter();
			Assert.IsTrue( PeepBehaviour.StillStranded( state, onPath, pathWalk ) );

			state.StampBlock( 50, 20 );
			Assert.IsFalse( PeepBehaviour.StillStranded( state, onPath, pathWalk ),
				"(48,19) is one of (47,19)'s nine cells, and in block (3,1)" );
		}
		finally
		{
			Close( people );
		}
	}

	private static int FarEnd( ParkWorld world, ParkState state, int x, int y )
	{
		var at = MapStep.CellId( x, y );

		while ( ParkRideChoice.StepToNextQueueCell( state.Park, at ) is var next and not 0 )
			at = next;

		return at;
	}

	/// <summary>
	/// A stamp above the counter is zeroed by the refusal's own test (a stamp from an earlier run of the counter),
	/// and every walk tick zeroes the stamp (<c>0x004fa30b</c>).
	/// </summary>
	[TestMethod]
	public void AStampAboveTheCounterAndAWalkTickBothZeroIt()
	{
		var (world, state, people) = Open();

		try
		{
			var peep = GuestAt( 47, 19 );
			var walk = WalkOn( state, peep );

			peep.StrandedTime = uint.MaxValue;
			Assert.IsFalse( PeepBehaviour.RefusedAsStranded( state, peep, walk ) );
			Assert.AreEqual( 0u, peep.StrandedTime, "the counter is below it" );

			var walking = GuestAt( 47, 19, PeepState.Wandering, thingId: 9 );
			var walkingWalk = WalkOn( state, walking );

			walking.Navigator.Target = new FixedVector( PeepNavigator.WaypointCentre( 47 ), PeepNavigator.WaypointCentre( 24 ) );
			Assert.IsTrue( walkingWalk.PlanRoute() );
			walking.StrandedTime = ParkState.NextCounter();

			Behaviour( world, state, new Random( 1 ) ).Step( walking, walkingWalk, playing: null, tick: 5 );

			Assert.AreEqual( 0u, walking.StrandedTime, "a walk tick zeroes it" );
		}
		finally
		{
			Close( people );
		}
	}

	/// <summary>
	/// The walker re-plans when its block is stamped after its route was found, and gives up when no route is left
	/// (<c>FUN_0050ed10</c>, <c>0x0050ed79</c>); a walk with no park to read never notices.
	/// </summary>
	[DataTestMethod]
	[DataRow( true )]
	[DataRow( false )]
	public void AWalkerRePlansWhenTheGroundChangesUnderIt( bool readsTheGround )
	{
		var (_, state, people) = Open();

		try
		{
			var peep = GuestAt( 47, 19, PeepState.Wandering );
			var walk = WalkOn( state, peep );

			if ( !readsTheGround )
				walk.Ground = null;

			peep.Navigator.Target = new FixedVector( PeepNavigator.WaypointCentre( 49 ), PeepNavigator.WaypointCentre( 22 ) );
			Assert.IsTrue( walk.PlanRoute(), "(47,19) to the Belly Bounce's back of queue, through (48,22)" );

			var planned = walk.RouteStamp;

			Assert.AreNotEqual( 0u, planned, "a route found takes a counter value" );
			Assert.AreEqual( WalkVerdict.Walking, walk.Step(), "nothing stamped: the walk goes on" );
			Assert.AreEqual( planned, walk.RouteStamp, "on the route it had" );

			// The one way in cut, as the game run cuts it; (48,22) is in block (3,1) and the walker in (2,1), so the
			// stamp they read is put in their own block too, as an edit there would.
			CutTheTail( state );
			state.StampBlock( 47, 19 );

			Assert.IsTrue( state.BlockStamp( 47, 19 ) > planned );

			var verdict = walk.Step();

			if ( readsTheGround )
				Assert.AreEqual( WalkVerdict.CannotReach, verdict, "re-planned at once, no route, given up" );
			else
				Assert.AreEqual( WalkVerdict.Walking, verdict, "with no stamps to read the old route is walked on" );
		}
		finally
		{
			Close( people );
		}
	}

	/// <summary>A re-plan that finds a way keeps walking, on a route stamped afresh.</summary>
	[TestMethod]
	public void AWalkerWithAWayLeftWalksOnWithAFreshRoute()
	{
		var (_, state, people) = Open();

		try
		{
			var peep = GuestAt( 47, 19, PeepState.Wandering );
			var walk = WalkOn( state, peep );

			peep.Navigator.Target = new FixedVector( PeepNavigator.WaypointCentre( 47 ), PeepNavigator.WaypointCentre( 25 ) );
			Assert.IsTrue( walk.PlanRoute() );

			var planned = walk.RouteStamp;

			state.StampBlock( 40, 20 );

			Assert.AreEqual( WalkVerdict.Walking, walk.Step() );
			Assert.IsTrue( walk.RouteStamp > state.BlockStamp( 47, 19 ), "the new route is newer than the stamp" );
			Assert.IsTrue( walk.RouteStamp > planned );

			var again = walk.RouteStamp;

			Assert.AreEqual( WalkVerdict.Walking, walk.Step() );
			Assert.AreEqual( again, walk.RouteStamp, "and one re-plan answers one stamp" );
		}
		finally
		{
			Close( people );
		}
	}

	/// <summary>
	/// A queue measured again stamps its new back cell's block (<c>FUN_004de1f0</c>, <c>0x004de266</c>) with the
	/// counter value taken before the measure.
	/// </summary>
	[TestMethod]
	public void AQueueMeasuredAgainStampsItsBackCellsBlock()
	{
		var (world, state, people) = Open();

		try
		{
			var bounce = world.Objects.First( thing => thing.ThingId == 13 );
			var (back, _) = ParkRideChoice.QueueCellsFor( world, bounce );

			Assert.AreEqual( MapStep.CellId( 49, 22 ), back, "the Belly Bounce's back of queue" );
			Assert.AreEqual( 0u, state.BlockStamp( 49, 22 ) );

			var before = ParkState.Counter;

			state.RemeasureQueue( bounce.ThingId );

			Assert.IsTrue( state.BlockStamp( 49, 22 ) > before );
			Assert.AreEqual( 0u, state.BlockStamp( 47, 22 ), "and no other block's" );
		}
		finally
		{
			Close( people );
		}
	}

	/// <summary>The shipped park's guests: nobody stranded, every thought one the switch knows or none.</summary>
	[TestMethod]
	public void TheShippedParksGuestsAreSavedWithNoStampAndAKnownThought()
	{
		var (world, _, people) = Open();

		try
		{
			var guests = world.People.Where( person => person.Guest != null ).Select( person => person.Guest!.Value ).ToArray();

			Assert.IsTrue( guests.Length > 0 );

			foreach ( var guest in guests )
			{
				Assert.AreEqual( 0u, guest.StrandedTime );
				Assert.IsTrue( guest.LastThought == 0 || Thoughts.Known( guest.LastThought ), $"thought {guest.LastThought}" );
				Assert.IsTrue( guest.TimeBubbleShown >= 0 && guest.TimeBubbleShown <= world.GameTick,
					$"bubble time {guest.TimeBubbleShown} against the park's clock {world.GameTick}" );
			}

		}
		finally
		{
			Close( people );
		}
	}
}

/// <summary>SetThought, the bubble's life, the needs' pick and the red square's blink; none needs the game.</summary>
[TestClass]
public class ThoughtsTests
{
	/// <summary>The switch's pictures: the bank in the high bits, the set in the low four (<c>FUN_0050be80</c>).</summary>
	[TestMethod]
	public void EachThoughtNamesItsBankSetAndClass()
	{
		Assert.AreEqual( (0, 15, 0), Thoughts.PictureOf( 0x11 ), "confused: SPR_TB's set 15" );
		Assert.AreEqual( (0, 8, 1), Thoughts.PictureOf( 1 ), "hungry" );
		Assert.AreEqual( (1, 5, 0), Thoughts.PictureOf( 5 ), "pleased: 21, SPR_TC's set 5" );
		Assert.AreEqual( (0, 9, 0), Thoughts.PictureOf( 6 ) );
		Assert.AreEqual( (0, 0, 3), Thoughts.PictureOf( 9 ) );
		Assert.AreEqual( (1, 0, 3), Thoughts.PictureOf( 0x12 ), "the staff's: 16, SPR_TC's set 0" );
		Assert.AreEqual( (1, 1, 2), Thoughts.PictureOf( 0x13 ) );
		Assert.AreEqual( (1, 4, 0), Thoughts.PictureOf( 0x16 ) );

		Assert.IsFalse( Thoughts.Known( 0 ) );
		Assert.IsFalse( Thoughts.Known( 0x17 ) );

		// Every thought has its own picture.
		Assert.AreEqual( 22, Enumerable.Range( 1, 22 ).Select( thought => Thoughts.PictureOf( thought ) )
			.Select( picture => (picture.Bank, picture.Set) ).Distinct().Count() );
	}

	/// <summary>A class holds off the next bubble 20 sweeps a class from the last one made; the thought is stored either way.</summary>
	[TestMethod]
	public void AThoughtsClassHoldsOffItsBubble()
	{
		var thoughts = new Thoughts();

		Assert.IsTrue( thoughts.Set( 0x11, gameTick: 800 ), "class 0 always shows" );
		Assert.AreEqual( 800, thoughts.TimeBubbleShown );

		Assert.IsFalse( thoughts.Set( 9, gameTick: 859 ), "class 3: 60 sweeps from the last bubble" );
		Assert.AreEqual( 9, thoughts.Last, "stored all the same" );
		Assert.IsNull( thoughts.Bubble, "and the old bubble is freed even so" );
		Assert.AreEqual( 800, thoughts.TimeBubbleShown );

		Assert.IsTrue( thoughts.Set( 9, gameTick: 860 ) );
		Assert.AreEqual( (0, 0), thoughts.Bubble );
		Assert.AreEqual( 860, thoughts.TimeBubbleShown );

		Assert.IsFalse( thoughts.Set( 1, gameTick: 879 ), "class 1: 20" );
		Assert.IsTrue( thoughts.Set( 1, gameTick: 880 ) );
		Assert.IsFalse( thoughts.Set( 0x13, gameTick: 919 ), "class 2: 40" );
		Assert.IsTrue( thoughts.Set( 0x13, gameTick: 920 ) );
		Assert.IsTrue( thoughts.Set( 6, gameTick: 920 ), "class 0 straight after" );
	}

	/// <summary>In a first-person view the thought is stored and the bubble left as it was; an unknown thought frees it.</summary>
	[TestMethod]
	public void FirstPersonStoresTheThoughtAndTouchesNoBubble()
	{
		var thoughts = new Thoughts();

		Assert.IsFalse( thoughts.Set( 0x11, gameTick: 800, firstPerson: true ) );
		Assert.AreEqual( 0x11, thoughts.Last );
		Assert.IsNull( thoughts.Bubble );

		thoughts.Set( 6, gameTick: 800 );
		thoughts.Set( 0x11, gameTick: 801, firstPerson: true );
		Assert.AreEqual( (0, 9), thoughts.Bubble, "the bubble showing stays" );
		Assert.AreEqual( 800, thoughts.TimeBubbleShown );

		Assert.IsFalse( thoughts.Set( 0x40, gameTick: 802 ) );
		Assert.AreEqual( 0x40, thoughts.Last );
		Assert.IsNull( thoughts.Bubble, "not a known thought: the old bubble freed, none made" );
	}

	/// <summary>A bubble is taken away by the first needs turn more than 12 sweeps after it was made (<c>FUN_0050be40</c>).</summary>
	[TestMethod]
	public void ABubbleGoesAfterTwelveSweeps()
	{
		var thoughts = new Thoughts();

		thoughts.Set( 0x11, gameTick: 800 );

		thoughts.Expire( 812 );
		Assert.IsNotNull( thoughts.Bubble, "12 sweeps on it stays" );

		thoughts.Expire( 813 );
		Assert.IsNull( thoughts.Bubble, "13 on it goes" );
		Assert.AreEqual( 0x11, thoughts.Last, "the thought is kept" );
	}

	/// <summary>The needs' pick, in the original's order (<c>FUN_004fc8a0</c>).</summary>
	[DataTestMethod]
	[DataRow( 91f, 91f, 100f, 100f, 100f, 3 )]
	[DataRow( 91f, 90.9f, 0f, 0f, 50f, 0 )]
	[DataRow( 100f, 90f, 100f, 100f, 100f, 1 )]
	[DataRow( 90.9f, 100f, 100f, 100f, 100f, 2 )]
	[DataRow( 0f, 99.9f, 100f, 100f, 100f, 4 )]
	[DataRow( 0f, 0f, 99.9f, 100f, 100f, 0xe )]
	[DataRow( 0f, 0f, 0f, 99.9f, 100f, 9 )]
	[DataRow( 0f, 0f, 0f, 0f, 99.9f, 0 )]
	[DataRow( 0f, 0f, 0f, 0f, 10f, 0 )]
	[DataRow( 0f, 0f, 0f, 0f, 9.9f, 0xb )]
	public void TheNeedsPickTheirThoughtInOrder( float hunger, float thirst, float toilet, float illness, float happiness, int thought )
	{
		var peep = new Peep( 5, new ParkWorld.GuestState(
			State: (int)PeepState.Deciding, SavedState: 0, PersonType: 0, Cash: 0, ExitLevel: 100,
			Happiness: happiness, Thirst: thirst, Hunger: hunger, Toilet: toilet, Vomit: illness, Litter: 0f,
			MajorDest: 0, QueuePos: 0, PrankeryIndex: 0 ),
			new ParkWorld.NavigatorState( 0, 0, 0, 0, 0, 0, ParkWorld.NavigatorState.DefaultMass,
				ParkWorld.NavigatorState.DefaultRadius, 0, 0, 0, 0, false, 0, 0, 0, 0, 0, 0, 0 ) );

		Assert.AreEqual( thought == 0 ? null : thought, Thoughts.PickFromNeeds( peep ) );
	}

	/// <summary>A guest takes the save's stranded stamp, last thought and bubble time (file 232, 386, 394).</summary>
	[TestMethod]
	public void AGuestTakesTheSavesStampAndThought()
	{
		var peep = new Peep( 5, new ParkWorld.GuestState(
			State: (int)PeepState.Deciding, SavedState: 0, PersonType: 0, Cash: 0, ExitLevel: 100,
			Happiness: 50f, Thirst: 0f, Hunger: 0f, Toilet: 0f, Vomit: 0f, Litter: 0f,
			MajorDest: 0, QueuePos: 0, PrankeryIndex: 0, StrandedTime: 5, LastThought: 9, TimeBubbleShown: 700 ),
			new ParkWorld.NavigatorState( 0, 0, 0, 0, 0, 0, ParkWorld.NavigatorState.DefaultMass,
				ParkWorld.NavigatorState.DefaultRadius, 0, 0, 0, 0, false, 0, 0, 0, 0, 0, 0, 0 ) );

		Assert.AreEqual( 5u, peep.StrandedTime );
		Assert.AreEqual( 9, peep.Thoughts.Last );
		Assert.AreEqual( 700, peep.Thoughts.TimeBubbleShown );
		Assert.IsNull( peep.Thoughts.Bubble, "the bubble's own sprite is not made again" );
	}

	/// <summary>Red squares show seven frames and are gone two (<c>0x0053c7b1</c>..<c>0x0053c7f6</c>).</summary>
	[TestMethod]
	public void RedSquaresBlinkSevenFramesOnAndTwoOff()
	{
		var (shown, frames) = (true, 0f);
		var seen = "";

		for ( var frame = 0; frame < 18; ++frame )
		{
			seen += shown ? "1" : "0";
			(shown, frames) = ParkBuildMarkers.AdvanceBlink( shown, frames, 1f );
		}

		Assert.AreEqual( "111111100111111100", seen );

		// And by time: a long frame crosses several changes at once and lands where single frames would.
		Assert.AreEqual( (false, 0.5f), ParkBuildMarkers.AdvanceBlink( true, 0f, 16.5f ) );
	}
}
