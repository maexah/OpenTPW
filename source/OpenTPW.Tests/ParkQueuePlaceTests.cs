using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The walk to a place in a queue - <c>FUN_00501160</c>, FindQueueDestination: the place turned into a point
/// (<see cref="ParkQueuePlace"/>), and its three callers - the join, the <c>InQueue</c> re-take and the refused
/// door - with the chooser's aim at the back of the queue and the arrival test on it. See
/// <c>docs/exe/ride-operation.md</c>, "Walking to a new place in the queue".
///
/// <para>
/// Measured against Lost Kingdom's own save. The Belly Bounce (thing 13) is its one thing with a queue path, four
/// cells from (52,22) back to (49,22); every other thing stands all its places in its back-of-queue cell.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkQueuePlaceTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const int BellyBounce = 13;

	private const int JungleSpray = 14;

	/// <summary>The least jitter, so every sub byte a test asks for is exact.</summary>
	private const int J = ParkQueuePlace.JitterFrom;

	private const float Before = 50f;

	/// <summary><c>MediumHappinessChange</c>, 15, in <c>FUN_005012f0</c>.</summary>
	private const float PutOut = 35f;

	private const int One = FixedVector.One;

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private static ParkWorld.CatalogueObject Thing( ParkWorld world, int thingId )
		=> world.Objects.Single( thing => thing.ThingId == thingId );

	private static (int X, int Y) CellOf( ParkQueuePlace.Point point ) => MapStep.CellAt( point.Cell );

	// ---- The place to a point: FUN_004de840 and FUN_004dec30 -------------------------------------------------

	/// <summary>
	/// <b>Along is four places a cell, and wraps</b>: <c>(u8)__ftol( n × 0.25f × 255.0f )</c>. And the jitter is
	/// <c>draw % 28 + 114</c>, unsigned.
	/// </summary>
	[TestMethod]
	public void AlongIsFourPlacesACellAndWraps()
	{
		CollectionAssert.AreEqual( new[] { 0, 63, 127, 191, 255, 62, 126 },
			Enumerable.Range( 0, 7 ).Select( ParkQueuePlace.Along ).ToArray() );

		Assert.AreEqual( 114, ParkQueuePlace.Jitter( 0 ) );
		Assert.AreEqual( 141, ParkQueuePlace.Jitter( 27 ) );
		Assert.AreEqual( 114, ParkQueuePlace.Jitter( 28 ) );
		Assert.AreEqual( (int)(int.MaxValue % 28u) + 114, ParkQueuePlace.Jitter( int.MaxValue ) );
		Assert.AreEqual( 130, ParkQueuePlace.Jitter( int.MinValue ),
			"the engine answers 0x80000000 unchanged, and the DIV is unsigned: 2^31 % 28 is 16" );
	}

	/// <summary>
	/// <b>The Belly Bounce's sixteen places stand along its four cells</b>, the decode's table exactly: from the
	/// front edge of (52,22), whose direction <c>0x10</c> points at the entrance, 63/256 of a cell further back each
	/// place; the fourth place of each cell turns to the next cell's direction (<c>0x04</c>, so 63 across, not 64),
	/// and the back cell's fourth turns away from the path it joins at (48,22). A seventeenth has no cell.
	/// </summary>
	[TestMethod]
	public void TheBellyBouncesSixteenPlacesStandAlongItsFourCells()
	{
		var world = World();
		_ = new ParkState( world );

		var ride = Thing( world, BellyBounce );

		Assert.IsTrue( ride.HasQueuePath, "the Belly Bounce walks its queue path" );

		// (cell x, sub x, sub y) for each place; every one on row 22.
		var expected = new (int X, int SubX, int SubY)[]
		{
			(52, J, 255), (52, J, 192), (52, J, 128), (52, 63, J),
			(51, 255, J), (51, 192, J), (51, 128, J), (51, 63, J),
			(50, 255, J), (50, 192, J), (50, 128, J), (50, 63, J),
			(49, 255, J), (49, 192, J), (49, 128, J), (49, 63, J)
		};

		for ( var place = 0; place < expected.Length; ++place )
		{
			var point = ParkQueuePlace.For( world, ride, place, J );

			Assert.AreEqual( (expected[place].X, 22), CellOf( point ), $"place {place}'s cell" );
			Assert.AreEqual( expected[place].SubX, point.SubX, $"place {place} across x" );
			Assert.AreEqual( expected[place].SubY, point.SubY, $"place {place} across y" );
			Assert.IsTrue( point.Written, $"place {place} has a direction" );
		}

		Assert.AreEqual( 0, ParkQueuePlace.For( world, ride, 16, J ).Cell, "a seventeenth place is past the cells" );
	}

	/// <summary>
	/// <b>The back cell's fourth place turns away from the FIRST of N, E, S and W that it links to plain path</b>
	/// (<c>0x004dea15</c>). The shipped (49,22) cannot tell that from its fallback - the side opposite its one path, W,
	/// is its own direction, <c>0x04</c> - so the map is edited: linked north to a path at (49,21), N is asked first
	/// and place 15 turns to <c>0x10</c>, (J, 64). Linked north to bare ground, N is not path and W answers again,
	/// (63, J); with no path linked at all, the cell's own direction answers, (63, J) too.
	/// </summary>
	[TestMethod]
	public void TheBackCellsFourthPlaceTurnsAwayFromTheFirstPathItJoins()
	{
		foreach ( var (north, west, expected) in new[]
		{
			(CellEdge.Path, CellEdge.Path, (J, 64)),
			(CellEdge.Nothing, CellEdge.Path, (63, J)),
			(CellEdge.Nothing, CellEdge.Nothing, (63, J))
		} )
		{
			var world = World();
			var state = new ParkState( world );
			var ride = Thing( world, BellyBounce );

			var back = state.Record( 49, 22 );
			state.SetRecord( 49, 22, back with { Neighbours = (byte)(back.Neighbours | 0x01) } );
			state.SetRecord( 49, 21, state.Record( 49, 21 ) with { Type = north } );
			state.SetRecord( 48, 22, state.Record( 48, 22 ) with { Type = west } );

			var point = ParkQueuePlace.For( world, ride, 15, J );

			Assert.AreEqual( (49, 22), CellOf( point ), $"north {north}, west {west}" );
			Assert.AreEqual( expected, (point.SubX, point.SubY), $"north {north}, west {west}" );
		}
	}

	/// <summary>
	/// <b>A thing without the queue-path bit stands every place in its back-of-queue cell</b>, facing the ENTRY
	/// cell's direction, with no bound: the Jungle Spray's entrance faces <c>0x10</c> and a toilet's <c>0x40</c>, so
	/// a fifth guest wraps to the far edge of the same cell.
	/// </summary>
	[TestMethod]
	public void EveryPlaceOfAVirtualQueueStandsInItsBackCell()
	{
		var world = World();
		_ = new ParkState( world );

		var spray = Thing( world, JungleSpray );
		var toilet = world.Objects.First( thing => thing.IsToilet );

		Assert.IsFalse( spray.HasQueuePath );
		Assert.IsFalse( toilet.HasQueuePath );

		var sprayBack = ParkRideChoice.QueueCellsFor( world, spray ).BackOfQueue;
		var toiletBack = ParkRideChoice.QueueCellsFor( world, toilet ).BackOfQueue;

		Assert.AreEqual( (52, 29), MapStep.CellAt( sprayBack ), "the Jungle Spray's back of queue" );
		Assert.AreEqual( (toilet.EntryCellX + 1, toilet.EntryCellY), MapStep.CellAt( toiletBack ),
			"a toilet's back of queue, east of its entrance" );

		var along = new[] { 0, 63, 127, 191, 255 };

		for ( var place = 0; place < along.Length; ++place )
		{
			var atTheSpray = ParkQueuePlace.For( world, spray, place, J );
			var atTheToilet = ParkQueuePlace.For( world, toilet, place, J );

			Assert.AreEqual( sprayBack, atTheSpray.Cell, $"the spray's place {place} is in its back cell" );
			Assert.AreEqual( (J, 255 - along[place]), (atTheSpray.SubX, atTheSpray.SubY), $"the spray's place {place}" );

			Assert.AreEqual( toiletBack, atTheToilet.Cell, $"the toilet's place {place} is in its back cell" );
			Assert.AreEqual( (along[place], J), (atTheToilet.SubX, atTheToilet.SubY), $"the toilet's place {place}" );
		}
	}

	/// <summary>
	/// <b>An entrance with no direction the switch knows writes neither sub byte</b> (<c>"Dodgy cell direction"</c>):
	/// the point is marked unwritten and stands at the cell's centre.
	/// </summary>
	[TestMethod]
	public void AnEntranceFacingNoSideWritesNeitherByte()
	{
		var world = World();
		var state = new ParkState( world );
		var spray = Thing( world, JungleSpray );

		var entry = state.Record( spray.EntryCellX, spray.EntryCellY );
		state.SetRecord( spray.EntryCellX, spray.EntryCellY, entry with { Direction = 0x05 } );

		var point = ParkQueuePlace.For( world, spray, 0, J );

		Assert.IsFalse( point.Written, "0x05 is two sides, which the switch does not know" );
		Assert.AreEqual( ParkRideChoice.QueueCellsFor( world, spray ).BackOfQueue, point.Cell, "still the back cell" );
		Assert.AreEqual( (ParkQueuePlace.Centre, ParkQueuePlace.Centre), (point.SubX, point.SubY) );
	}

	/// <summary>A sub byte s is s/256 of its cell in the simulation's 16.16 (<c>FUN_00510100( X &lt;&lt; 8 )</c>).</summary>
	[TestMethod]
	public void APointIsItsSubBytesInTwoHundredFiftySixthsOfItsCell()
	{
		var point = new ParkQueuePlace.Point( MapStep.CellId( 52, 22 ), 114, 255, Written: true );

		Assert.AreEqual( new FixedVector( 52 * One + 114 * 256, 22 * One + 255 * 256 ), point.Position );
	}

	// ---- The callers, through the guest's own turn -----------------------------------------------------------

	private sealed record Park( ParkWorld World, ParkState State, PeepBehaviour Behaviour, RideScript Script,
		Dictionary<int, Peep> Guests );

	private Park Open( Func<ParkWorld.CatalogueObject, int, bool>? admit = null )
	{
		var world = World();
		var state = new ParkState( world );
		var admission = new ParkAdmission( new ParkBalance( "jungle", easyMode: true ), world.Economy!.Value.AdmissionFee );

		using var rse = data.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );
		var script = new RideScript( new RideScriptFile( rse ) );

		var guests = new Dictionary<int, Peep>();

		var behaviour = new PeepBehaviour( world, new Random( 1 ), admission, () => ParkRides.GateIsOpen, state,
			new ParkItemCatalogue( "jungle", data ), admit: admit,
			leaveQueue: ( ride, id ) => ParkRideOperation.LeaveQueue( state, script, ride.ThingId, id ),
			stillQueueing: id => guests.TryGetValue( id, out var peep ) && ParkRideOperation.IsQueueing( peep ) );

		return new( world, state, behaviour, script, guests );
	}

	/// <summary>A guest in <paramref name="state"/> for <paramref name="thing"/>, standing still on the centre of a cell.</summary>
	private static Peep Guest( Park park, int thingId, PeepState state, int cellX, int cellY, float toilet = 10f,
		int thing = BellyBounce )
	{
		var centre = new FixedVector( PeepNavigator.WaypointCentre( cellX ), PeepNavigator.WaypointCentre( cellY ) );

		var peep = new Peep( thingId, new ParkWorld.GuestState(
			State: (int)state, SavedState: (int)PeepState.Deciding, PersonType: 0,
			Cash: 300, ExitLevel: 100, Happiness: Before, Thirst: 10f, Hunger: 10f, Toilet: toilet,
			Vomit: 0f, Litter: 0f, MajorDest: thing, QueuePos: 0, PrankeryIndex: 0 ),
			new ParkWorld.NavigatorState(
				X: centre.X, Y: centre.Y, VelocityX: 0, VelocityY: 0, TargetX: centre.X, TargetY: centre.Y,
				Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
				MaxForce: 0, MaxSpeed: 0, NavMode: 0, CantReachDest: 0, PathFinished: false,
				PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
				BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 ) );

		park.Guests[thingId] = peep;

		return peep;
	}

	private static void Turn( Park park, Peep peep, int tick = 40 )
	{
		var walk = new PeepWalk( peep.Navigator, CellEdge.For( park.World, ParkPeople.WalkingMode ).Blocked );

		park.Behaviour.Step( peep, walk, playing: null, tick );
	}

	/// <summary>Whether a guest is aimed <paramref name="subY"/>/256 down a cell, and 114..141 across it.</summary>
	private static void AssertAimedAcrossX( Peep peep, int cellX, int cellY, int subY, string why )
	{
		var target = peep.Navigator.Target;

		Assert.AreEqual( cellY * One + subY * 256, target.Y, $"guest {peep.ThingId} along: {why}" );
		Assert.IsTrue( target.X >= cellX * One + ParkQueuePlace.JitterFrom * 256
			&& target.X <= cellX * One + (ParkQueuePlace.JitterFrom + ParkQueuePlace.JitterSpread - 1) * 256,
			$"guest {peep.ThingId} across is jittered 114..141 into cell {cellX}, not {target.X / (float)One}: {why}" );
	}

	/// <summary>Put out by <c>FUN_005012f0</c>: thinking again, naming nothing, fifteen down and out of the links.</summary>
	private static void AssertPutOut( Park park, Peep peep, string why )
	{
		Assert.AreEqual( PeepState.Deciding, peep.State, $"guest {peep.ThingId} thinks again: {why}" );
		Assert.AreEqual( 0, peep.MajorDest, $"guest {peep.ThingId} names nothing: {why}" );
		Assert.AreEqual( PutOut, peep.Happiness, 0.001f, $"guest {peep.ThingId} loses MediumHappinessChange: {why}" );
		Assert.AreEqual( -1, park.State.PositionInQueue( BellyBounce, peep.ThingId ), $"guest {peep.ThingId} is out: {why}" );
	}

	/// <summary>
	/// <b>A guest arriving on the back-of-queue cell joins and walks to their own place</b> (<c>0x004ffdad</c>): the
	/// first to the front edge of the front cell, (52,22) at 255/256 down, the second 63/256 behind them.
	/// </summary>
	[TestMethod]
	public void AGuestArrivingOnTheBackOfTheQueueWalksToTheirOwnPlace()
	{
		var park = Open();
		var first = Guest( park, 30, PeepState.GoingToRide, 49, 22 );
		var second = Guest( park, 31, PeepState.GoingToRide, 49, 22 );

		Turn( park, first );
		Turn( park, second );

		Assert.AreEqual( PeepState.SteppingUpQueue, first.State, "the first walks to their place" );
		Assert.AreEqual( PeepState.SteppingUpQueue, second.State, "and so does the second" );
		Assert.AreEqual( 0, first.QueuePos, "the first's place is written as they set off" );
		Assert.AreEqual( 1, second.QueuePos, "and the second's" );
		Assert.AreEqual( 1, park.State.PositionInQueue( BellyBounce, second.ThingId ), "behind the first" );

		AssertAimedAcrossX( first, 52, 22, 255, "place 0, the front edge of the front cell" );
		AssertAimedAcrossX( second, 52, 22, 192, "place 1, 63/256 further back" );
	}

	/// <summary>
	/// <b>A guest arriving anywhere but the back-of-queue cell is aimed at it again and walks on</b>
	/// (<c>0x004ffc3d</c>): "The back of the queue has moved while I was walking here". They join nothing yet.
	/// </summary>
	[TestMethod]
	public void AGuestArrivingOffTheBackOfTheQueueIsAimedThereAgain()
	{
		var park = Open();
		var atTheEntrance = Guest( park, 30, PeepState.GoingToRide, 52, 23 );

		Turn( park, atTheEntrance );

		Assert.AreEqual( PeepState.GoingToRide, atTheEntrance.State, "still walking to the ride" );
		Assert.AreEqual( BellyBounce, atTheEntrance.MajorDest );
		Assert.AreEqual( -1, park.State.PositionInQueue( BellyBounce, atTheEntrance.ThingId ), "not in its queue yet" );
		Assert.AreEqual(
			new FixedVector( PeepNavigator.WaypointCentre( 49 ), PeepNavigator.WaypointCentre( 22 ) ),
			atTheEntrance.Navigator.Target, "aimed at the centre of the back cell, (49,22)" );
		Assert.IsTrue( atTheEntrance.Navigator.TotalWaypoints > 0, "with a route to it" );
	}

	/// <summary>
	/// <b>With no route to the back of the queue, a guest thinks again still naming the thing</b>
	/// (<c>0x004ffe16</c>, event <c>0x16</c>): <c>MajorDest</c> is kept, and nothing is docked.
	/// </summary>
	[TestMethod]
	public void AGuestWithNoRouteToTheBackOfTheQueueThinksAgainStillNamingIt()
	{
		var park = Open();
		var lost = Guest( park, 30, PeepState.GoingToRide, 0, 0 );

		Turn( park, lost );

		Assert.AreEqual( PeepState.Deciding, lost.State );
		Assert.AreEqual( BellyBounce, lost.MajorDest, "the thing is still named" );
		Assert.AreEqual( Before, lost.Happiness, 0.001f, "and nothing is taken off" );
		Assert.AreEqual( -1, park.State.PositionInQueue( BellyBounce, lost.ThingId ) );
	}

	/// <summary>
	/// <b>A joiner behind somebody who has stopped queueing cannot get to their place, and leaves</b>
	/// (<c>0x004ffdf4</c>): the queue walk answers -1 at the one who stopped, so FindQueueDestination answers 0 with
	/// nothing written, and the joiner is taken back out of the queue they have just joined and put out.
	/// </summary>
	[TestMethod]
	public void AJoinerBehindSomebodyWhoHasStoppedQueueingLeavesTheQueue()
	{
		var park = Open();
		var stopped = Guest( park, 30, PeepState.Deciding, 49, 22 );
		var joiner = Guest( park, 31, PeepState.GoingToRide, 49, 22 );

		park.State.JoinQueue( BellyBounce, stopped.ThingId );

		Turn( park, joiner );

		AssertPutOut( park, joiner, "they cannot reach their place" );
		Assert.AreEqual( 0, park.State.NextInQueue( stopped.ThingId ), "and the queue ends where it did" );
	}

	/// <summary>
	/// <b>A queuer whose place has moved walks to the new one</b> (<c>0x00500532</c>): the head gone, the second is
	/// head, and walks forward to the front edge in <see cref="PeepState.SteppingUpQueue"/>.
	/// </summary>
	[TestMethod]
	public void AQueuerWhosePlaceMovedWalksToTheNewOne()
	{
		var park = Open();
		var head = Guest( park, 30, PeepState.InQueue, 52, 22 );
		var second = Guest( park, 31, PeepState.InQueue, 52, 22 );

		park.State.JoinQueue( BellyBounce, head.ThingId );
		second.QueuePos = park.State.JoinQueue( BellyBounce, second.ThingId );
		park.State.LeaveQueue( BellyBounce, head.ThingId );
		park.Guests.Remove( head.ThingId );

		Turn( park, second );

		Assert.AreEqual( PeepState.SteppingUpQueue, second.State );
		Assert.AreEqual( 0, second.QueuePos );
		AssertAimedAcrossX( second, 52, 22, 255, "the new head walks to the front edge" );
	}

	/// <summary>
	/// <b>A queuer who cannot get to their new place is put out</b> (<c>0x005004b3</c>, "Couldn't get to my intended
	/// queue position"): standing where no route reaches the queue.
	/// </summary>
	[TestMethod]
	public void AQueuerWhoCannotGetToTheirNewPlaceIsPutOut()
	{
		var park = Open();
		var head = Guest( park, 30, PeepState.InQueue, 52, 22 );
		var stranded = Guest( park, 31, PeepState.InQueue, 0, 0 );

		park.State.JoinQueue( BellyBounce, head.ThingId );
		stranded.QueuePos = park.State.JoinQueue( BellyBounce, stranded.ThingId );
		park.State.LeaveQueue( BellyBounce, head.ThingId );
		park.Guests.Remove( head.ThingId );

		Turn( park, stranded );

		AssertPutOut( park, stranded, "no route to their new place" );
	}

	/// <summary>
	/// <b>A joiner whose entrance faces no side the switch knows is counted and walks to the back cell's centre</b>:
	/// the original routes with whatever its stack held (<c>"Dodgy cell direction"</c>), which nothing here can
	/// reproduce.
	/// </summary>
	[TestMethod]
	public void AnEntranceFacingNoSideIsCountedAndStoodAtTheCentre()
	{
		var park = Open();
		var spray = Thing( park.World, JungleSpray );

		var entry = park.State.Record( spray.EntryCellX, spray.EntryCellY );
		park.State.SetRecord( spray.EntryCellX, spray.EntryCellY, entry with { Direction = 0x05 } );

		var joiner = Guest( park, 30, PeepState.GoingToRide, 52, 29, thing: JungleSpray );
		var before = Counted( "QUEUE_PLACE_DODGY_DIRECTION" );

		Turn( park, joiner );

		Assert.AreEqual( before + 1, Counted( "QUEUE_PLACE_DODGY_DIRECTION" ), "counted once" );
		Assert.AreEqual( PeepState.SteppingUpQueue, joiner.State );
		Assert.AreEqual( new FixedVector( PeepNavigator.WaypointCentre( 52 ), PeepNavigator.WaypointCentre( 29 ) ),
			joiner.Navigator.Target, "the centre of the back cell, (52,29)" );
	}

	private static int Counted( string what )
		=> Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	/// <summary>
	/// <b>A place past the queue's cells cannot be walked to</b>: the seventeenth of the Belly Bounce's queue is on no
	/// cell, which the original packs as (127, 255), and a queuer re-taking it is put out.
	/// </summary>
	[TestMethod]
	public void APlacePastTheQueuesCellsCannotBeWalkedTo()
	{
		var park = Open();

		for ( var ahead = 0; ahead < 16; ++ahead )
			park.State.JoinQueue( BellyBounce, Guest( park, 100 + ahead, PeepState.InQueue, 49, 22 ).ThingId );

		var seventeenth = Guest( park, 30, PeepState.InQueue, 49, 22 );

		park.State.JoinQueue( BellyBounce, seventeenth.ThingId );
		seventeenth.QueueMoveDelay = 0;

		Assert.AreEqual( 16, park.State.PositionInQueue( BellyBounce, seventeenth.ThingId ) );

		Turn( park, seventeenth );

		AssertPutOut( park, seventeenth, "place 16 is on no cell" );
	}

	/// <summary>
	/// <b>After a re-take the mood still runs on the same turn</b> (<c>JNZ 0x005002f2</c>): a queuer walking to their
	/// new place whose toilet need is above 80 leaves the queue there and then. The control, a need of 10, walks on.
	/// </summary>
	[TestMethod]
	public void TheMoodStillRunsOnTheTurnOfARetake()
	{
		foreach ( var (toilet, leaves) in new[] { (81f, true), (10f, false) } )
		{
			var park = Open();
			var head = Guest( park, 30, PeepState.InQueue, 52, 22 );
			var second = Guest( park, 31, PeepState.InQueue, 52, 22, toilet );

			park.State.JoinQueue( BellyBounce, head.ThingId );
			second.QueuePos = park.State.JoinQueue( BellyBounce, second.ThingId );
			park.State.LeaveQueue( BellyBounce, head.ThingId );
			park.Guests.Remove( head.ThingId );

			Turn( park, second );

			if ( leaves )
				AssertPutOut( park, second, "the toilet, after walking to the new place" );
			else
				Assert.AreEqual( PeepState.SteppingUpQueue, second.State, "a need of 10 walks on" );
		}
	}

	/// <summary>
	/// <b>A guest the ride refuses at the door walks back to the front of the queue</b> (<c>0x00500826</c>): still
	/// linked at its head, their place is nought, and they walk to its point to wait for a new call forward.
	/// </summary>
	[TestMethod]
	public void ARefusedGuestWalksBackToTheFrontOfTheQueue()
	{
		var park = Open( admit: ( _, _ ) => false );
		var refused = Guest( park, 30, PeepState.BeingAdmitted, 52, 23 );

		park.State.JoinQueue( BellyBounce, refused.ThingId );

		Turn( park, refused );

		Assert.AreEqual( PeepState.SteppingUpQueue, refused.State, "back to their place" );
		Assert.AreEqual( 0, refused.QueuePos );
		Assert.AreEqual( BellyBounce, refused.MajorDest, "still queueing for it" );
		AssertAimedAcrossX( refused, 52, 22, 255, "place 0" );
	}

	/// <summary>
	/// <b>A refused guest the queue walk cannot find is put out</b> (<c>0x00500857</c>, "Couldn't rejoin FOQ
	/// even!"), losing only <c>MediumHappinessChange</c>.
	/// </summary>
	[TestMethod]
	public void ARefusedGuestWhoCannotRejoinTheQueueIsPutOut()
	{
		var park = Open( admit: ( _, _ ) => false );
		var refused = Guest( park, 30, PeepState.BeingAdmitted, 52, 23 );

		Turn( park, refused );

		AssertPutOut( park, refused, "not in the queue's links" );
	}

	/// <summary>
	/// <b>The park run whole: every guest walking to something is aimed at the centre of its back-of-queue cell</b>
	/// (<c>0x004fcbc4</c>), and every guest in a queue at the point for the place they last took - J anywhere in
	/// 114..141. The point is <see cref="ParkQueuePlace"/>'s, so this pins the wiring; the tests above pin the
	/// decode.
	/// </summary>
	[TestMethod]
	public void EveryGuestIsAimedWhereTheOriginalAimsThem()
	{
		var world = World();
		var state = new ParkState( world );
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var admission = new ParkAdmission( new ParkBalance( "jungle", easyMode: true ), world.Economy!.Value.AdmissionFee );

		var guests = ParkPeople.PeepsIn( world ).ToDictionary( peep => peep.ThingId );

		var behaviour = new PeepBehaviour( world, new Random( 1234 ), admission, () => ParkRides.GateIsOpen, state,
			catalogue, leaveQueue: ( ride, id ) => ParkRideOperation.LeaveQueue( state, null, ride.ThingId, id ),
			stillQueueing: id => guests.TryGetValue( id, out var peep ) && ParkRideOperation.IsQueueing( peep ) );

		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;
		var walks = guests.Values.ToDictionary( peep => peep.ThingId, peep => new PeepWalk( peep.Navigator, blocked ) );

		var going = 0;
		var queueing = 0;
		var bellyBouncePlaces = new HashSet<int>();

		for ( var tick = 1; tick <= 600; ++tick )
		{
			foreach ( var peep in guests.Values )
			{
				var was = peep.State;

				behaviour.Step( peep, walks[peep.ThingId], playing: null, tick );

				if ( peep.State == PeepState.GoingToRide && peep.MajorDest != 0 )
				{
					++going;

					var back = ParkRideChoice.QueueCellsFor( world, Thing( world, peep.MajorDest ) ).BackOfQueue;
					var (x, y) = MapStep.CellAt( back );

					Assert.AreEqual(
						new FixedVector( PeepNavigator.WaypointCentre( x ), PeepNavigator.WaypointCentre( y ) ),
						peep.Navigator.Target, $"guest {peep.ThingId} walking to thing {peep.MajorDest}, from {was}" );
				}

				if ( peep.State is PeepState.InQueue or PeepState.SteppingUpQueue )
				{
					++queueing;

					// The jitter is the one byte a test cannot know, so the axis it moves is found by moving it.
					var thing = Thing( world, peep.MajorDest );
					var least = ParkQueuePlace.For( world, thing, peep.QueuePos, J ).Position;
					var acrossX = ParkQueuePlace.For( world, thing, peep.QueuePos, J + 1 ).Position.X != least.X;
					var target = peep.Navigator.Target;

					var along = acrossX ? target.Y - least.Y : target.X - least.X;
					var across = acrossX ? target.X - least.X : target.Y - least.Y;
					var what = $"guest {peep.ThingId} at place {peep.QueuePos} of thing {thing.ThingId} aimed at "
						+ $"({target.X / (float)One:0.000},{target.Y / (float)One:0.000}), the decode's "
						+ $"({least.X / (float)One:0.000},{least.Y / (float)One:0.000}) with J from 114";

					Assert.AreEqual( 0, along, what );
					Assert.IsTrue( across >= 0 && across < ParkQueuePlace.JitterSpread * 256 && across % 256 == 0, what );

					if ( thing.ThingId == BellyBounce )
						bellyBouncePlaces.Add( peep.QueuePos );
				}
			}
		}

		Assert.IsTrue( going > 0, "somebody walked to something" );
		Assert.IsTrue( queueing > 0, "somebody queued" );
		// The save's thirteen guests fill no more than the front cell in this many turns; the sixteen places are
		// TheBellyBouncesSixteenPlacesStandAlongItsFourCells.
		Assert.IsTrue( bellyBouncePlaces.Count > 1,
			$"the Belly Bounce's queuers took places {string.Join( ",", bellyBouncePlaces.Order() )}, more than one" );
	}
}
