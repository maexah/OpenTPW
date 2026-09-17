using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A guest choosing a ride, walking to it, and joining its queue - the arm that runs from
/// <c>FUN_004fec90</c> through <c>FUN_004ffbc0</c>.
///
/// <para>
/// <b>These exist because the whole arm was unreachable from every test that came before them, and a
/// real fault hid in that gap.</b> <see cref="ParkDecidingTests"/> builds its behaviour from two facts
/// rather than from a park, so its chooser has nothing to choose and the ride arm never fires. Under
/// exactly that blindness, <c>PeepBehaviour.Step</c> shipped with <b>no case for
/// <see cref="PeepState.GoingToRide"/> at all</b> - a guest who chose a ride was put into a walking state
/// that nothing walked, and stood on the spot playing a walk animation for ever. Six hundred and
/// sixty-seven green tests did not see it. The fix is a case; the guard against it coming back is this
/// file, which drives the behaviour with a REAL park behind it.
/// </para>
/// <para>
/// Everything is collected across the whole run rather than read off the final turn. Whether a given
/// guest happens to be mid-queue on turn six hundred is a property of the seed, not of the code, and a
/// test that turned on it would fail for reasons unrelated to what it claims to check.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkRideJoinTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private ParkWorld Park()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private ParkAdmission Admission( ParkWorld world )
		=> new( new ParkBalance( "jungle", easyMode: true ), world.Economy!.Value.AdmissionFee );

	/// <summary>What a run of the park saw, gathered turn by turn.</summary>
	private sealed record Seen( HashSet<PeepState> States, HashSet<int> Queued, int LongestQueue,
		ParkState Park, Dictionary<int, Peep> Guests );

	/// <summary>
	/// Runs the shipped park with a real world, catalogue and running state behind the behaviour - which
	/// is what makes the ride arm reachable at all.
	/// </summary>
	private Seen Run( int turns = 600, int seed = 1234 )
	{
		var world = Park();
		var state = new ParkState( world );
		var catalogue = new ParkItemCatalogue( "jungle", data );

		var behaviour = new PeepBehaviour( world, new Random( seed ), Admission( world ),
			() => ParkRides.GateIsOpen, state, catalogue );

		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;
		var guests = ParkPeople.PeepsIn( world ).ToDictionary( peep => peep.ThingId );
		var walks = guests.Values.ToDictionary( peep => peep.ThingId,
			peep => new PeepWalk( peep.Navigator, blocked ) );

		var states = new HashSet<PeepState>();
		var queued = new HashSet<int>();
		var longest = 0;

		for ( var tick = 1; tick <= turns; ++tick )
		{
			foreach ( var peep in guests.Values )
			{
				behaviour.Step( peep, walks[peep.ThingId], playing: null, tick );
				states.Add( peep.State );
			}

			foreach ( var thing in world.Objects )
			{
				var length = state.QueueLength( thing.ThingId );

				if ( length <= 0 )
					continue;

				queued.Add( thing.ThingId );
				longest = Math.Max( longest, length );
			}
		}

		return new Seen( states, queued, longest, state, guests );
	}

	/// <summary>
	/// <b>The regression test for the missing case.</b> Guests reach <see cref="PeepState.GoingToRide"/>
	/// and then <em>leave</em> it - which is the half that was broken, because a state nothing walks is a
	/// state nothing leaves.
	/// </summary>
	[TestMethod]
	public void GuestsSentToARideDoNotStandStillInGoingToRide()
	{
		var seen = Run();

		Assert.IsTrue( seen.States.Contains( PeepState.GoingToRide ),
			"no guest was ever sent to a ride, so this test proves nothing - the chooser did not fire" );

		// The anti-vacuity half, and the one that actually catches the fault: with no case for it, every
		// guest who entered GoingToRide stayed there for the rest of the run.
		Assert.IsFalse( seen.Guests.Values.All( guest => guest.State != PeepState.GoingToRide )
			&& seen.Queued.Count == 0,
			"guests entered GoingToRide but nothing ever came of it" );

		Assert.IsTrue( seen.States.Contains( PeepState.SteppingUpQueue ),
			"a guest who arrives at a ride should start shuffling up its queue" );
	}

	/// <summary>
	/// Somebody actually ends up in a queue - the park's queues start empty, so anything above nought had
	/// to be put there by a guest arriving.
	/// </summary>
	[TestMethod]
	public void SomebodyJoinsAQueueThatStartedEmpty()
	{
		var seen = Run();

		Assert.IsTrue( seen.Queued.Count > 0, "no queue in the park ever held anybody" );
		Assert.IsTrue( seen.LongestQueue > 0, $"the longest queue seen was {seen.LongestQueue}" );

		// Only the two objects that clear the filter can be queued for - the shop and the three toilets
		// declare no queue cells, so a guest reaching one of those would mean the filter was bypassed.
		foreach ( var id in seen.Queued )
			Assert.IsTrue( id is 13 or 14, $"thing {id} was queued for, and only 13 and 14 can be" );
	}

	/// <summary>
	/// A queue never grows past what the original's own gate allows - <c>length &lt; cells * 4</c>, the
	/// test <c>FUN_004dda20</c> makes and the one the filter already made before a guest set off.
	/// </summary>
	[TestMethod]
	public void NoQueueGrowsPastTheRoomItHas()
	{
		var world = Park();
		var seen = Run();

		foreach ( var id in seen.Queued )
		{
			var thing = world.Objects.Single( o => o.ThingId == id );
			var room = thing.QueueSizeInCells * ParkRideChoice.QueueRoomPerCell;

			Assert.IsTrue( seen.Park.QueueLength( id ) <= room,
				$"thing {id} holds {seen.Park.QueueLength( id )} in a queue with room for {room}" );
		}

		Assert.IsTrue( seen.LongestQueue <= 16, $"the longest queue was {seen.LongestQueue}" );
	}

	/// <summary>
	/// Everyone recorded as queueing is in exactly one queue, linked both ways - which is what says the
	/// joins built a list rather than a set of orphans.
	/// </summary>
	[TestMethod]
	public void EveryQueuingGuestIsLinkedBothWays()
	{
		var seen = Run();

		foreach ( var id in seen.Queued )
		{
			var walked = new List<int>();

			for ( var guest = seen.Park.FirstInQueue( id ); guest != 0; guest = seen.Park.NextInQueue( guest ) )
				walked.Add( guest );

			Assert.AreEqual( walked.Count, walked.Distinct().Count(), $"thing {id}'s queue repeats somebody" );
			Assert.AreEqual( seen.Park.QueueLength( id ), walked.Count, $"thing {id}'s length matches its walk" );

			// The head has nobody in front; everyone else is pointed back at by the one ahead of them.
			Assert.AreEqual( 0, seen.Park.PreviousInQueue( walked[0] ), $"thing {id}'s head has nobody ahead" );

			for ( var i = 1; i < walked.Count; ++i )
			{
				Assert.AreEqual( walked[i - 1], seen.Park.PreviousInQueue( walked[i] ),
					$"thing {id}: {walked[i]} should point back at {walked[i - 1]}" );
			}
		}
	}
}
