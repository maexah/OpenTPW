using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// SetRandomDest's arm for a cell with no links - <c>0x004f9a05</c>..<c>0x004f9d5f</c>, decoded in
/// <c>docs/exe/ride-operation.md</c>, "SetRandomDest", the no-links arm. A guest standing where the map
/// connects nowhere looks for path on seven rays up to three cells away, then tries five random cells.
///
/// <para>
/// The probe order is tested without the game; everything else reads Lost Kingdom and is skipped where
/// there is no installation - see <see cref="GameData"/>. The cells a sale clears come from a real sale of
/// the Belly Bounce (<see cref="ParkBuilding.Sell"/>): its queue at (49..52, 22), its entrance at (52,23),
/// and the path along row 21 above them.
/// </para>
/// </summary>
[TestClass]
public class ParkNoLinksWanderTests
{
	private const string Theme = "jungle";

	private const int BellyBounce = 13;

	/// <summary>A random source that answers only what the test gives it, and counts what was asked for.</summary>
	private sealed class Scripted( params int[] draws ) : Random
	{
		private readonly Queue<int> _draws = new( draws );

		public int Taken { get; private set; }

		public override int Next()
		{
			++Taken;

			return _draws.Dequeue();
		}
	}

	private sealed record Park( ParkWorld World, ParkState State, ParkItemCatalogue Catalogue, ParkPeople People );

	private static Park Open()
	{
		var data = GameData.Required();
		FileSystem = data;

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		var state = new ParkState( world );
		var catalogue = new ParkItemCatalogue( Theme, data );
		var people = new ParkPeople( world, new ParkBalance( Theme, easyMode: true ), gateStatus: null, state,
			catalogue );

		return new( world, state, catalogue, people );
	}

	/// <summary>Opens the park and sells the Belly Bounce, which leaves its queue and footprint bare.</summary>
	private static Park OpenAndSell()
	{
		var park = Open();

		StringAssert.Contains(
			ParkBuilding.Sell( park.State, park.World, park.Catalogue, null, null, BellyBounce, park.People ), "sold" );

		return park;
	}

	private static void Close( Park park )
	{
		park.People.Delete();
		Entity.ApplyDeletions();
	}

	/// <summary>A guest standing at the centre of a named cell - <see cref="ParkWanderingTests"/>' guest.</summary>
	private static Peep GuestAt( int cellX, int cellY )
		=> new( 7, new ParkWorld.GuestState(
			State: (int)PeepState.Deciding, SavedState: (int)PeepState.Deciding, PersonType: 0, Cash: 300,
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

	private static PeepBehaviour Behaviour( Park park, Random random )
		=> new( park.World, random, Admission( park.World ), () => ParkRides.GateIsOpen, park.State );

	private static ParkAdmission Admission( ParkWorld world )
		=> new( new ParkBalance( Theme, easyMode: true ), world.Economy!.Value.AdmissionFee );

	private static PeepWalk WalkFor( Peep peep, ParkWorld world )
		=> new( peep.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

	/// <summary>A cell's centre as the navigator holds a target.</summary>
	private static FixedVector Centre( int x, int y )
		=> new( PeepNavigator.WaypointCentre( x ), PeepNavigator.WaypointCentre( y ) );

	/// <summary>
	/// The 32 probes from a cell in the middle of the map, written out from the decode's table rather than
	/// computed: direction outside, distance inside, the guest's own cell at every k 0 and all of direction 0.
	/// </summary>
	[TestMethod]
	public void TheProbesRunTheTableDirectionOutsideDistanceInside()
	{
		(int, int)[] expected =
		[
			(60, 60), (60, 60), (60, 60), (60, 60),
			(60, 60), (61, 61), (62, 62), (63, 63),
			(60, 60), (61, 60), (62, 60), (63, 60),
			(60, 60), (61, 59), (62, 58), (63, 57),
			(60, 60), (60, 59), (60, 58), (60, 57),
			(60, 60), (59, 59), (58, 58), (57, 57),
			(60, 60), (59, 60), (58, 60), (57, 60),
			(60, 60), (59, 61), (58, 62), (57, 63)
		];

		CollectionAssert.AreEqual( expected, PeepBehaviour.NoLinksProbes( 60, 60 ).ToArray() );
	}

	/// <summary>No ray runs to (0, +k): the table has no such direction, so a path there is never probed.</summary>
	[TestMethod]
	public void NothingIsProbedAtZeroPlusK()
	{
		var probes = PeepBehaviour.NoLinksProbes( 60, 60 ).ToHashSet();

		foreach ( var k in new[] { 1, 2, 3 } )
			Assert.IsFalse( probes.Contains( (60, 60 + k) ), $"(0, +{k}) was probed" );
	}

	/// <summary>
	/// The probe is the cell id plus dy × 128 + dx, so a ray off column 0 comes back in at column 127 a row up,
	/// and one off column 127 at column 0 a row down.
	/// </summary>
	[TestMethod]
	public void AnXPastTheEdgeWrapsIntoTheNextRow()
	{
		var west = PeepBehaviour.NoLinksProbes( 0, 50 ).ToArray();

		// Direction 6, (−k, 0), at k 1 to 3.
		CollectionAssert.AreEqual( new[] { (127, 49), (126, 49), (125, 49) }, west[25..28] );

		var east = PeepBehaviour.NoLinksProbes( 127, 50 ).ToArray();

		// Direction 2, (k, 0), at k 1 to 3.
		CollectionAssert.AreEqual( new[] { (0, 51), (1, 51), (2, 51) }, east[9..12] );
	}

	/// <summary>
	/// A probe off the top or bottom of the map is left out: from row 0 the three rays with −k lose all nine.
	/// </summary>
	[TestMethod]
	public void AProbeOffTheMapIsLeftOut()
	{
		var top = PeepBehaviour.NoLinksProbes( 5, 0 ).ToArray();

		Assert.AreEqual( 32 - 9, top.Length );
		Assert.IsTrue( top.All( cell => ParkState.OnMap( cell.X, cell.Y ) ) );

		var bottom = PeepBehaviour.NoLinksProbes( 5, 127 ).ToArray();

		Assert.AreEqual( 32 - 6, bottom.Length, "directions 1 and 7 run to +k" );
	}

	/// <summary>
	/// Every queue cell the sale cleared has no links, and the first path on the probes is (x + 1, 21),
	/// direction 3 at k 1 - aimed at its centre, with a route planned to it.
	/// </summary>
	[TestMethod]
	public void AGuestOnAClearedQueueCellWandersToThePathUpAndToTheRight()
	{
		var park = OpenAndSell();

		try
		{
			foreach ( var x in new[] { 49, 50, 51, 52 } )
			{
				Assert.AreEqual( 0, CellEdge.Links( ParkState.CellFor( park.World, x, 22 ).Neighbours ),
					$"the sale should have left ({x},22) with no links" );

				var peep = GuestAt( x, 22 );
				var walk = WalkFor( peep, park.World );

				Assert.IsTrue( Behaviour( park, new Random( 1 ) ).SetRandomDest( peep, walk ),
					$"a guest on ({x},22) should find the path" );

				Assert.AreEqual( Centre( x + 1, 21 ), peep.Navigator.Target,
					$"from ({x},22) the first path probed is ({x + 1},21), aimed at its centre" );

				Assert.IsTrue( walk.HasRoute, $"and a route to it from ({x},22) is planned" );
			}
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// From the cleared entrance (52,23) the first hit is direction 3 at k 2, (54,21): (53,22) above-right is
	/// bare. Direction 4 would have found (52,21) first, so this pins direction 3 before 4.
	/// </summary>
	[TestMethod]
	public void FromTheClearedEntranceTheFirstPathIsTwoUpTheDiagonal()
	{
		var park = OpenAndSell();

		try
		{
			var peep = GuestAt( 52, 23 );
			var walk = WalkFor( peep, park.World );

			Assert.IsTrue( Behaviour( park, new Random( 1 ) ).SetRandomDest( peep, walk ) );
			Assert.AreEqual( Centre( 54, 21 ), peep.Navigator.Target );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// A path hit whose route fails moves on to the next probe (<c>0x004f9b98</c>). With every side into (51,21)
	/// shut, a guest on (50,22) skips it, misses (52,20) and (53,19), and takes direction 4's (50,21).
	/// </summary>
	[TestMethod]
	public void AFailedRouteMovesOnToTheNextProbe()
	{
		var park = OpenAndSell();

		try
		{
			var edges = CellEdge.For( park.World, ParkPeople.WalkingMode ).Blocked;

			var peep = GuestAt( 50, 22 );
			var walk = new PeepWalk( peep.Navigator,
				( x, y, direction ) => MapStep.Beyond( x, y, direction ) == (51, 21) || edges( x, y, direction ) );

			Assert.IsTrue( Behaviour( park, new Random( 1 ) ).SetRandomDest( peep, walk ) );
			Assert.AreEqual( Centre( 50, 21 ), peep.Navigator.Target );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// The arm is chosen by the own cell's link count, not by the neighbour pick coming up empty. With
	/// (48,22) given the west bit, the pick from the cleared (49,22) would take it; the count is still nought,
	/// so the guest is sent to (50,21)'s centre.
	/// </summary>
	[TestMethod]
	public void TheOwnCellsCountChoosesTheArmNotAnEmptyPick()
	{
		var park = OpenAndSell();

		try
		{
			var west = ParkState.CellFor( park.World, 48, 22 );
			park.State.SetRecord( 48, 22, west with { Neighbours = (byte)(west.Neighbours | 0x04) } );

			var peep = GuestAt( 49, 22 );

			Assert.IsTrue( Behaviour( park, new Random( 1 ) ).SetRandomDest( peep, WalkFor( peep, park.World ) ) );
			Assert.AreEqual( Centre( 50, 21 ), peep.Navigator.Target );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// And a linked cell whose pick finds nothing does not probe: (48,22) is path with links, every side shut,
	/// and the answer is false with the target untouched.
	/// </summary>
	[TestMethod]
	public void ALinkedCellWithEverySideShutDoesNotProbe()
	{
		var park = Open();

		try
		{
			var peep = GuestAt( 48, 22 );
			peep.Navigator.Target = Centre( 30, 30 );

			var walk = new PeepWalk( peep.Navigator, ( _, _, _ ) => true );

			Assert.AreNotEqual( 0, CellEdge.Links( ParkState.CellFor( park.World, 48, 22 ).Neighbours ) );
			Assert.IsFalse( Behaviour( park, new Random( 1 ) ).SetRandomDest( peep, walk ) );
			Assert.AreEqual( Centre( 30, 30 ), peep.Navigator.Target );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// A probe hits only on path (<c>FUN_00536310</c>, type 1). From the bare (49,23) beside the unsold Belly
	/// Bounce the probes cross its footprint (51,25), its exit (52,26), its entrance (52,23) and its queue (50,22)
	/// before the path (51,21), which is where the guest is sent.
	/// </summary>
	[TestMethod]
	public void OnlyPathIsAHit()
	{
		var park = Open();

		try
		{
			Assert.AreEqual( 3, ParkState.CellFor( park.World, 50, 22 ).Type, "(50,22) is the unsold queue" );

			var peep = GuestAt( 49, 23 );

			Assert.IsTrue( Behaviour( park, new Random( 1 ) ).SetRandomDest( peep, WalkFor( peep, park.World ) ) );
			Assert.AreEqual( Centre( 51, 21 ), peep.Navigator.Target );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// A failed route moves on to the next probe on the same ray, not the next ray: with (51,21) shut and a path
	/// cell laid at (52,20), a guest on (50,22) is sent to (52,20), direction 3 at k 2, not to direction 4's (50,21).
	/// </summary>
	[TestMethod]
	public void AFailedRouteMovesOnAlongItsRay()
	{
		var park = OpenAndSell();

		try
		{
			park.State.SetRecord( 52, 20, ParkState.CellFor( park.World, 52, 20 ) with { Type = CellEdge.Path } );

			var edges = CellEdge.For( park.World, ParkPeople.WalkingMode ).Blocked;

			var peep = GuestAt( 50, 22 );
			var walk = new PeepWalk( peep.Navigator,
				( x, y, direction ) => MapStep.Beyond( x, y, direction ) == (51, 21) || edges( x, y, direction ) );

			Assert.IsTrue( Behaviour( park, new Random( 1 ) ).SetRandomDest( peep, walk ) );
			Assert.AreEqual( Centre( 52, 20 ), peep.Navigator.Target );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// A lone path cell, laid with no links at (20,23), is its own first probe: direction 0 at k 0 hits it and the guest
	/// is sent to its centre, the table's quirk kept.
	/// </summary>
	[TestMethod]
	public void ALonePathCellIsItsOwnFirstHit()
	{
		var park = Open();

		try
		{
			park.State.SetRecord( 20, 23, ParkState.CellFor( park.World, 20, 23 ) with
			{
				Type = CellEdge.Path, Neighbours = 0
			} );

			var peep = GuestAt( 20, 23 );
			peep.Navigator.Position = new FixedVector(
				PeepNavigator.WaypointCentre( 20 ) + 0x2000, PeepNavigator.WaypointCentre( 23 ) - 0x3000 );
			peep.Navigator.Target = Centre( 30, 30 );

			var random = new Scripted( 0 );
			var walk = WalkFor( peep, park.World );

			Assert.IsTrue( Behaviour( park, random ).SetRandomDest( peep, walk ) );
			Assert.AreEqual( Centre( 20, 23 ), peep.Navigator.Target );
			Assert.IsTrue( walk.HasRoute );
			Assert.AreEqual( 1, random.Taken, "the call's one draw, and a probe hit draws no more" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// With no path on any probe, a try is own + (r % 11 − 5, r % 11 − 5), x drawn first after the call's own
	/// draw (<c>0x004f9534</c>), aimed at the cell's
	/// centre whatever its type. (20,23) is bare ground with nothing but bare ground for five cells round, and
	/// none of the map's track frames, whose records shut a cell's sides.
	/// </summary>
	[TestMethod]
	public void WithNoPathOnTheProbesATryTakesACellOfAnyType()
	{
		var park = Open();

		try
		{
			var peep = GuestAt( 20, 23 );

			// The call's draw, then dx 8 % 11 − 5 = 3 and dy 2 % 11 − 5 = −3.
			var random = new Scripted( 0, 8, 2 );

			Assert.IsTrue( Behaviour( park, random ).SetRandomDest( peep, WalkFor( peep, park.World ) ) );
			Assert.AreEqual( Centre( 23, 20 ), peep.Navigator.Target );
			Assert.AreEqual( 3, random.Taken );
			Assert.AreEqual( CellEdge.Nothing, ParkState.CellFor( park.World, 23, 20 ).Type,
				"the try took bare ground, which no probe would" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// A try drawn off the map uses one of the five: from (0,50), five draws of dx −5 answer false after the
	/// call's draw and ten more, and four of them then dx +5 land on (5,50). (0,50) and its tries are bare ground
	/// clear of track frames.
	/// </summary>
	[TestMethod]
	public void ADrawOffTheMapUsesATry()
	{
		var park = Open();

		try
		{
			var peep = GuestAt( 0, 50 );
			var none = new Scripted( 0, 0, 5, 0, 5, 0, 5, 0, 5, 0, 5, 10, 5 );

			Assert.IsFalse( Behaviour( park, none ).SetRandomDest( peep, WalkFor( peep, park.World ) ),
				"five draws off the map are five tries" );
			Assert.AreEqual( 11, none.Taken );

			var fifth = new Scripted( 0, 0, 5, 0, 5, 0, 5, 0, 5, 10, 5 );

			Assert.IsTrue( Behaviour( park, fifth ).SetRandomDest( peep, WalkFor( peep, park.World ) ) );
			Assert.AreEqual( Centre( 5, 50 ), peep.Navigator.Target );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// The whole turn: a put-off guest Deciding on (50,22) is reached by the one-in-three wander roll, leaves as
	/// Wandering toward (51,21)'s centre, and never goes home first. The chooser is held off by a fresh idle stamp.
	/// </summary>
	[TestMethod]
	public void APutOffGuestLeavesDecidingAsAWandererTowardThePath()
	{
		var park = OpenAndSell();

		try
		{
			var peep = GuestAt( 50, 22 );
			var walk = WalkFor( peep, park.World );
			var random = new Random( 5 );
			var behaviour = Behaviour( park, random );

			peep.SetState( PeepState.Deciding, 1, random );
			peep.TimeStartedIdling = 1;

			for ( var tick = 2; tick <= 1 + PeepBehaviour.ThinkingGap && peep.State == PeepState.Deciding; ++tick )
				behaviour.Step( peep, walk, playing: null, tick );

			Assert.AreEqual( PeepState.Wandering, peep.State );
			Assert.AreEqual( Centre( 51, 21 ), peep.Navigator.Target );
		}
		finally
		{
			Close( park );
		}
	}
}
