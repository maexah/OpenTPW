using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// Where a guest with nothing better to do may wander - the candidate half of <c>FUN_004f9490</c>.
///
/// <para>
/// <b>Alexah found this by playing: guests walked out of the park and off down the road.</b> The original
/// builds its four candidates from the byte <c>FUN_00522770</c> hands back - the cell's own
/// <c>mNeighbours</c> at <c>+0xc</c> - and that byte is a statement about which ways a cell <i>connects</i>,
/// not which ways it is walled. This build asked only whether an edge could be crossed, and the road's
/// edges can be.
/// </para>
/// <para>
/// <b>The number that makes this park able to tell right from wrong: 91.</b> Of its 16,384 cells only 91
/// carry a non-zero mask, and they are exactly the cells of types 1, 3, 9 and 10 - the paths, the queues
/// and a few specials. Every cell of type 7, 0, 2, 30 and 4 reads nought, the road outside the gate
/// (type 30) included. So "never stands where the mask is nought" is a strong claim here rather than a
/// vacuous one: it excludes 16,293 of the park's cells.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkWanderingTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>A guest standing at the centre of a named cell, in a named state.</summary>
	private static Peep GuestAt( int thingId, PeepState state, int cellX, int cellY )
	{
		var peep = new Peep( thingId, new ParkWorld.GuestState(
			State: (int)state, SavedState: (int)PeepState.Deciding, PersonType: 0, Cash: 300,
			ExitLevel: 100, Happiness: 50f, Thirst: 10f, Hunger: 10f, Toilet: 10f, Vomit: 0f,
			Litter: 0f, MajorDest: 0, QueuePos: 0, PrankeryIndex: 0 ),
			new ParkWorld.NavigatorState(
				X: PeepNavigator.WaypointCentre( cellX ), Y: PeepNavigator.WaypointCentre( cellY ),
				VelocityX: 0, VelocityY: 0,
				TargetX: PeepNavigator.WaypointCentre( cellX ), TargetY: PeepNavigator.WaypointCentre( cellY ),
				Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,

				// <b>Measured off the shipped park rather than invented, and the nought this used to hold
				// is why the test failed twice.</b> A guest with no maximum force and no maximum speed
				// cannot accelerate, so they never leave the cell they start on and the anti-vacuity guard
				// below fires instead of the assertion the test is about. Every guest in Lost Kingdom
				// carries mass 65,536 and radius 13,107 with a force/speed pair near 2:1; these are
				// thing 41's own. The helper this was copied from can afford noughts because its guests are
				// TELEPORTED to the exit by Dismiss and never walk a step.
				MaxForce: 26214, MaxSpeed: 13107, NavMode: 0, CantReachDest: 0, PathFinished: false,
				PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
				BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 ) );

		return peep;
	}

	/// <summary>
	/// <b>The bug, stated as the map states it.</b> A guest left to wander from a path cell inside the park
	/// never ends a turn on a cell whose <c>mNeighbours</c> is nought - which is every cell the original
	/// could not have chosen, the road included.
	///
	/// <para>
	/// <b>What this catches, measured rather than hoped for.</b> With the mask test disabled the guest
	/// reaches <b>(47,16)</b> on tick 523 - the cell immediately outside the gate at (47,17), and the
	/// first of the road cells - and this was the only test in the suite that noticed when it was measured. So it
	/// discriminates the fix rather than decorating it.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AWanderingGuestNeverStandsOnACellTheMapSaysConnectsNowhere()
	{
		var world = World();
		var park = new ParkState( world );

		// (47,20) is a path cell inside the park, four cells south of the entrance at (47,17).
		var peep = GuestAt( 7, PeepState.Wandering, 47, 20 );
		var walk = new PeepWalk( peep.Navigator, CellEdge.For( world, ParkPeople.WalkingMode ).Blocked );

		var behaviour = new PeepBehaviour( world, new Random( 7 ), null, () => ParkRides.GateIsOpen, park );

		var visited = new HashSet<(int, int)>();
		var rolls = new Random( 11 );

		for ( var tick = 1; tick <= 600; ++tick )
		{
			// <b>Re-armed every turn, and the first draft of this test failed for want of it.</b> Wandering
			// keeps itself only on a one-in-KeepWanderingShare roll and otherwise drops the guest into
			// Deciding, so a guest left alone leaves the arm this test is about within a turn or two and
			// then stands still - which is exactly what happened: they saw ONE cell and the anti-vacuity
			// guard below caught it. Putting them back keeps the candidate pick being exercised.
			if ( peep.State != PeepState.Wandering )
				peep.SetState( PeepState.Wandering, tick, rolls );

			behaviour.Step( peep, walk, playing: null, tick );

			var (x, y) = walk.Position.Cell;
			visited.Add( (x, y) );

			Assert.AreNotEqual( 0, world.CellAt( x, y ).Neighbours,
				$"tick {tick}: the guest stood on ({x},{y}), whose mNeighbours is nought - " +
				"the original could never have sent them there" );
		}

		// <b>Anti-vacuity, and it is the half that gives the assertion above any force.</b> A guest who
		// never took a step would satisfy every line of it by standing still on the cell they started on.
		Assert.IsTrue( visited.Count > 1,
			$"the guest has to actually wander for this to test anything; they saw {visited.Count} cell(s)" );
	}

	/// <summary>
	/// The other side of the same coin, and what stops the rule above being "guests cannot move": every
	/// cell the guest DID stand on is one the map connects, and the park really does offer more than one.
	/// </summary>
	[TestMethod]
	public void TheCellsAGuestMayWanderOverAreTheOnesTheMapConnects()
	{
		var world = World();

		var connected = 0;

		for ( var y = 0; y < ParkWorld.MapSize; ++y )
		{
			for ( var x = 0; x < ParkWorld.MapSize; ++x )
			{
				if ( world.CellAt( x, y ).Neighbours != 0 )
					++connected;
			}
		}

		// Measured, not chosen: this is what makes the test above discriminating rather than vacuous.
		Assert.AreEqual( 91, connected,
			"the shipped park connects 91 of its 16,384 cells; if this moves, the claim above changes too" );

		// And the road outside the gate is emphatically not one of them.
		Assert.AreEqual( 0, world.CellAt( 47, 10 ).Neighbours, "the road at (47,10) connects nowhere" );
		Assert.AreEqual( 0, world.CellAt( 47, 16 ).Neighbours, "nor the cell just outside the entrance" );
		Assert.AreNotEqual( 0, world.CellAt( 47, 17 ).Neighbours, "but the entrance cell itself does" );
	}
}
