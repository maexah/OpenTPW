using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What a guest does once they are inside - the decision hub and the wandering it leads to.
///
/// <para>
/// <b>These are driven against the real park, because that is the only thing that makes them mean
/// anything.</b> Lost Kingdom saves seven guests in the gateway, every one of them walking to the exact
/// centre of an entrance cell; before this they arrived there and stood still for ever. The assertion that
/// matters is not that a state changed but that those seven <b>end up somewhere else on the map</b>.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkDecidingTests
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

	/// <summary>The seven guests the save leaves standing in the archway.</summary>
	private static readonly int[] InTheGateway = [41, 40, 38, 37, 36, 34, 32];

	private static (PeepBehaviour Behaviour, System.Collections.Generic.Dictionary<int, Peep> Guests,
		System.Collections.Generic.Dictionary<int, PeepWalk> Walks) Run(
		ParkWorld world, ParkAdmission admission, int turns, bool parkIsClosed = false, int seed = 1234 )
	{
		var behaviour = new PeepBehaviour( parkIsClosed, world.NumberOfVisitorsToDate, new Random( seed ),
			admission, () => ParkRides.GateIsOpen );

		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		var guests = ParkPeople.PeepsIn( world ).ToDictionary( peep => peep.ThingId );
		var walks = guests.Values.ToDictionary( peep => peep.ThingId,
			peep => new PeepWalk( peep.Navigator, blocked ) );

		for ( var tick = 1; tick <= turns; ++tick )
		{
			foreach ( var peep in guests.Values )
				behaviour.Step( peep, walks[peep.ThingId], playing: null, tick );
		}

		return (behaviour, guests, walks);
	}

	/// <summary>
	/// <b>The seven guests in the gateway stop standing in it.</b>
	///
	/// <para>
	/// This is the whole point of the decision hub, and the assertion is deliberately about <i>position</i>
	/// rather than about state. A guest who reached <c>Deciding</c> and was given a destination but never
	/// moved would satisfy any state check; what cannot be faked is being somewhere the save did not put
	/// them.
	/// </para>
	/// <para>
	/// Their saved destination is the exact centre of an entrance cell on row 17 - pinned independently in
	/// <see cref="ParkGuestStateTests"/> - so leaving it is measurable without knowing where they went.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheGuestsStandingInTheGatewayGoSomewhereElse()
	{
		var world = Park();

		// Where the save left them, read before anything runs.
		var started = ParkPeople.PeepsIn( world )
			.ToDictionary( peep => peep.ThingId, peep => peep.Navigator.Position );

		var (_, guests, _) = Run( world, Admission( world ), turns: 400 );

		var moved = InTheGateway.Count( id => guests[id].Navigator.Position != started[id] );

		Assert.AreEqual( InTheGateway.Length, moved,
			"every guest saved in the gateway should have left the cell centre the file sent them to" );

		// And they are doing something rather than having stopped: the hub only ever leaves a guest
		// deciding, wandering, or on their way out.
		foreach ( var id in InTheGateway )
		{
			Assert.IsTrue(
				guests[id].State is PeepState.Deciding or PeepState.Wandering or PeepState.HeadingForExit,
				$"guest {id} ended in {guests[id].State}, which the hub cannot produce" );
		}
	}

	/// <summary>
	/// At least some of them are actually <em>wandering</em>, which is the arm that moves anybody.
	///
	/// <para>
	/// A third of decisions wander, a third offer a ride that cannot be taken yet, and a third do nothing -
	/// so over a long run a guest passes through <c>Wandering</c> repeatedly. Asserting only the end state
	/// would be a coin toss; this watches every turn and counts how many distinct guests were ever seen in
	/// it.
	/// </para>
	/// </summary>
	[TestMethod]
	public void GuestsReallyDoWanderRatherThanOnlyDecidingToo()
	{
		var world = Park();
		var admission = Admission( world );

		var behaviour = new PeepBehaviour( parkIsClosed: false, world.NumberOfVisitorsToDate,
			new Random( 99 ), admission, () => ParkRides.GateIsOpen );

		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;
		var guests = ParkPeople.PeepsIn( world ).ToDictionary( peep => peep.ThingId );
		var walks = guests.Values.ToDictionary( peep => peep.ThingId,
			peep => new PeepWalk( peep.Navigator, blocked ) );

		var everWandered = new System.Collections.Generic.HashSet<int>();

		for ( var tick = 1; tick <= 400; ++tick )
		{
			foreach ( var peep in guests.Values )
			{
				behaviour.Step( peep, walks[peep.ThingId], playing: null, tick );

				if ( peep.State == PeepState.Wandering )
					everWandered.Add( peep.ThingId );
			}
		}

		Assert.IsTrue( everWandered.Count >= 5,
			$"only {everWandered.Count} guests ever wandered, which is too few for a one-in-three roll "
			+ $"over 400 turns: {string.Join( ", ", everWandered.OrderBy( id => id ) )}" );
	}

	/// <summary>
	/// A wandering guest aims at a random point <b>inside</b> the target cell, not its centre - the
	/// original's own arithmetic, and the opposite of what the pathfinder does for every other destination.
	///
	/// <para>
	/// <b>This is the discriminating check on <c>SetRandomDest</c>.</b> Every destination set anywhere else
	/// in this file is a cell centre, so an implementation that reached for the tidy answer would be
	/// indistinguishable by state and position alone. The roll is masked to <c>0x7f</c> and clamped to
	/// 5..123 of 256 sub-cell units, so a wander target's offset within its cell must land in that band -
	/// and must not be the 128 that a centre would give.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AWanderTargetIsARandomPointInTheCellRatherThanItsCentre()
	{
		var world = Park();
		var one = ParkWorld.NavigatorState.One;
		var admission = Admission( world );

		var behaviour = new PeepBehaviour( parkIsClosed: false, world.NumberOfVisitorsToDate,
			new Random( 7 ), admission, () => ParkRides.GateIsOpen );

		var blocked = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;
		var guests = ParkPeople.PeepsIn( world ).ToDictionary( peep => peep.ThingId );
		var walks = guests.Values.ToDictionary( peep => peep.ThingId,
			peep => new PeepWalk( peep.Navigator, blocked ) );

		// Collected across the whole run rather than sampled at the end: whether any particular guest is
		// mid-wander on the final turn is a property of the seed, and a test that turns on that would fail
		// for reasons that have nothing to do with the arithmetic it is checking.
		var offsets = new System.Collections.Generic.List<(int X, int Y)>();

		for ( var tick = 1; tick <= 400; ++tick )
		{
			foreach ( var peep in guests.Values )
			{
				behaviour.Step( peep, walks[peep.ThingId], playing: null, tick );

				if ( peep.State == PeepState.Wandering )
					offsets.Add( (peep.Navigator.Target.X % one, peep.Navigator.Target.Y % one) );
			}
		}

		Assert.IsTrue( offsets.Count > 0, "no guest wandered anywhere across four hundred turns" );

		foreach ( var (offX, offY) in offsets )
		{
			foreach ( var offset in new[] { offX, offY } )
			{
				var sub = offset / (one / 256);

				Assert.IsTrue( sub is >= 5 and <= 0x7b,
					$"a wander target sits {sub}/256 into its cell, outside the 5..123 the roll can give" );
			}
		}

		// The centre is 128/256, which the clamp cannot produce - so this also says the tidy answer was
		// not taken.
		Assert.IsFalse( offsets.Any( pair => pair.Item1 == one / 2 && pair.Item2 == one / 2 ),
			"a wander target landed exactly on a cell centre, which the original's clamp cannot produce" );
	}

	/// <summary>
	/// A park that shuts while a guest is deciding sends them home, badly out of sorts.
	///
	/// <para>
	/// <b>This arm is unreachable from the shipped park</b>, which is saved open and cannot be closed while
	/// it runs - the two buttons that would do it are among the bindings nothing consumes. It is reachable
	/// here only because <see cref="PeepBehaviour"/> can be built from the two facts rather than from a
	/// park, which is the same reason the waiting-outside arm is testable.
	/// </para>
	/// <para>
	/// Losing <c>BigHappinessChange</c> rather than the medium one is what separates this from every other
	/// mood change in the admission states, so the amount is asserted and not just the direction.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AParkThatShutsSendsTheDecidingGuestsHomeUnhappy()
	{
		var world = Park();
		var admission = Admission( world );

		// Four hundred turns, as above: the seven have to finish walking to the archway before they can
		// decide anything at all, and the longest route in this park arrives at turn 108.
		var (_, guests, _) = Run( world, admission, turns: 400, parkIsClosed: true );

		var one = ParkWorld.NavigatorState.One;

		var stops = new[] { admission.BusStopA, admission.BusStopB }
			.Select( cell => ((cell.X * one) + (one / 2), (cell.Y * one) + (one / 2)) )
			.ToHashSet();

		foreach ( var id in InTheGateway )
		{
			Assert.AreEqual( PeepState.HeadingForExit, guests[id].State,
				$"guest {id} should have given up on a park that shut under them" );

			Assert.IsTrue( stops.Contains( (guests[id].Navigator.Target.X, guests[id].Navigator.Target.Y) ),
				$"guest {id} should be walking to a bus stop, not to {guests[id].Navigator.Target}" );

			// 50 as saved, less the big change, and nothing else in this path touches happiness.
			Assert.AreEqual( 50f - admission.BigHappinessChange, guests[id].Happiness, 0.01f,
				$"guest {id} should have lost exactly the big mood change" );
		}
	}

	/// <summary>
	/// The two mood constants come from the balance file rather than from literals in the code.
	/// </summary>
	[TestMethod]
	public void TheMoodChangesAreTheBalanceFilesOwnNumbers()
	{
		var admission = Admission( Park() );

		Assert.AreEqual( 15, admission.MediumHappinessChange, "PeepInfo.MediumHappinessChange" );
		Assert.AreEqual( 25, admission.BigHappinessChange, "PeepInfo.BigHappinessChange" );
	}
}
