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
/// centre of an entrance cell. The assertion that
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
			// <b>Four states, and the reason it is only four is the construction rather than the hub.</b>
			// Run builds the behaviour from two facts, so its chooser has no park and the ride arm can never
			// return a candidate; given one it would also produce GoingToRide and SteppingUpQueue. Widening
			// this list would weaken it, so it stays narrow and says why.
			// The fourth is the empty hand's spot animation, which every failed choice here plays.
			Assert.IsTrue(
				guests[id].State is PeepState.Deciding or PeepState.Wandering or PeepState.HeadingForExit
					or PeepState.PlayingSpotAnimation,
				$"guest {id} ended in {guests[id].State}, which the hub cannot produce without a park" );
		}
	}

	/// <summary>
	/// At least some of them are actually <em>wandering</em>, which is the arm that moves anybody.
	///
	/// <para>
	/// A third of decisions wander, a third offer a ride, and a third do nothing - and in THIS file the ride
	/// arm is inert, because <see cref="Run"/> builds the behaviour from two facts rather than from a park,
	/// so its chooser has nothing to choose from. See <see cref="ParkRideJoinTests"/> for the park-ful
	/// version, which is where that arm is exercised -
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

	/// <summary>A seed whose first roll takes the given arm of the turn's split.</summary>
	private static int SeedFor( int arm )
		=> Enumerable.Range( 0, 100 ).First( candidate => new Random( candidate ).Next() % 3 == arm );

	private static int Counted( string gap ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == gap ).Times;

	/// <summary>One guest of the save made Deciding at happiness 50, with a walk nothing shuts or everything does.</summary>
	private (Peep Guest, PeepWalk Walk, PeepBehaviour Behaviour) Deciding( int seed, int stamp, bool walled = false )
	{
		var world = Park();
		var guest = ParkPeople.PeepsIn( world ).First();
		var open = CellEdge.For( world, ParkPeople.WalkingMode ).Blocked;

		guest.SetState( PeepState.Deciding, tick: 1, new Random( 1 ) );
		guest.Happiness = 50f;
		guest.TimeStartedIdling = stamp;

		return (guest, new PeepWalk( guest.Navigator, walled ? ( _, _, _ ) => true : open ),
			new PeepBehaviour( parkIsClosed: false, world.NumberOfVisitorsToDate, new Random( seed ), Admission( world ) ));
	}

	/// <summary>
	/// <b>The chooser's empty hand</b> (<c>0x004ff46f</c>..<c>0x004ff4a3</c>): event 1, spot animation 4 with
	/// Deciding saved, <c>SmallHappinessChange</c> off, and the idle stamp. This file's behaviour has no park, so
	/// its chooser never names a thing.
	/// </summary>
	[TestMethod]
	public void AGuestTheChooserGivesNothingIsBoredLosesTheSmallChangeAndIsStamped()
	{
		var (guest, walk, behaviour) = Deciding( SeedFor( 0 ), stamp: 0 );
		var small = Admission( Park() ).SmallHappinessChange;
		var events = Counted( "DECIDE_NOTHING_CHOSEN_EVENT" );

		Assert.AreEqual( 5, small, "the balance file's SmallHappinessChange" );

		behaviour.Step( guest, walk, playing: null, tick: 100 );

		Assert.AreEqual( PeepState.PlayingSpotAnimation, guest.State, "they stop to play it" );
		Assert.AreEqual( PeepState.Deciding, guest.SavedState, "and come back to deciding" );
		Assert.AreEqual( PeepBehaviour.SpotBored, guest.NextAnimation, "number 4, hands on hips" );
		Assert.AreEqual( 45f, guest.Happiness, "50 less the small change" );
		Assert.AreEqual( 100, guest.TimeStartedIdling, "stamped with the sweep it failed on" );
		Assert.AreEqual( events + 1, Counted( "DECIDE_NOTHING_CHOSEN_EVENT" ), "event 1, counted" );

		for ( var tick = 101; tick <= 110; ++tick )
		{
			behaviour.Step( guest, walk, playing: null, tick );
			Assert.AreEqual( PeepState.PlayingSpotAnimation, guest.State, $"still playing on sweep {tick}" );
		}

		behaviour.Step( guest, walk, playing: null, tick: 111 );
		Assert.AreEqual( PeepState.Deciding, guest.State, "back on the eleventh sweep" );
	}

	/// <summary>
	/// <b>Each failed choice takes the small change again, and no two are closer than 31 sweeps</b>: the gate is
	/// the sweep counter more than 30 past the stamp (<c>0x004ff42e</c>), and the empty hand is what stamps it.
	/// </summary>
	[TestMethod]
	public void EveryFailedChoiceTakesTheSmallChangeAndTheGapIsMeasuredFromTheLast()
	{
		var (guest, walk, behaviour) = Deciding( seed: 3, stamp: 0 );
		var failedOn = new System.Collections.Generic.List<int>();

		for ( var tick = 100; tick <= 500; ++tick )
		{
			var before = guest.Happiness;

			behaviour.Step( guest, walk, playing: null, tick );

			if ( guest.Happiness == before )
				continue;

			Assert.AreEqual( before - 5f, guest.Happiness, $"sweep {tick} took something other than the small change" );
			Assert.AreEqual( tick, guest.TimeStartedIdling, $"and sweep {tick} stamped" );
			failedOn.Add( tick );
		}

		Assert.IsTrue( failedOn.Count >= 4, $"only {failedOn.Count} failed choices in 400 sweeps" );
		Assert.AreEqual( 50f - (5f * failedOn.Count), guest.Happiness, "nothing else in this turn moves happiness" );

		for ( var n = 1; n < failedOn.Count; ++n )
			Assert.IsTrue( failedOn[n] - failedOn[n - 1] > PeepBehaviour.ThinkingGap,
				$"failed choices on sweeps {failedOn[n - 1]} and {failedOn[n]}, inside the thinking gap" );
	}

	/// <summary>The chooser is asked on the 31st sweep past the stamp and not on the 30th (<c>JBE</c>, <c>0x004ff42e</c>).</summary>
	[TestMethod]
	[DataRow( 70, 100, false )]
	[DataRow( 69, 100, true )]
	[DataRow( 0, 30, false )]
	[DataRow( 0, 31, true )]
	public void TheChooserWaitsThirtySweepsPastTheStamp( int stamp, int tick, bool asked )
	{
		var (guest, walk, behaviour) = Deciding( SeedFor( 0 ), stamp );

		behaviour.Step( guest, walk, playing: null, tick );

		Assert.AreEqual( asked ? PeepState.PlayingSpotAnimation : PeepState.Deciding, guest.State );
		Assert.AreEqual( asked ? 45f : 50f, guest.Happiness );
		Assert.AreEqual( asked ? tick : stamp, guest.TimeStartedIdling );
	}

	/// <summary>
	/// <b>A wander that routes leaves the idle stamp as it was</b> (<c>0x004ff3d6</c>, state 7 and return), and
	/// <b>one that finds nowhere stamps it</b> (<c>0x004ff3f4</c>).
	/// </summary>
	[TestMethod]
	public void ARoutedWanderKeepsTheIdleStampAndAFailedOneSetsIt()
	{
		var (routed, _, behaviour) = Deciding( SeedFor( 1 ), stamp: 7 );

		// With no park every cell is linked on all four sides, so the wander may be aimed anywhere within five
		// cells: a walk nothing shuts routes there.
		behaviour.Step( routed, new PeepWalk( routed.Navigator, MapStep.LeavesTheMap ), playing: null, tick: 100 );

		Assert.AreEqual( PeepState.Wandering, routed.State );
		Assert.AreEqual( 7, routed.TimeStartedIdling, "a routed wander does not stamp" );
		Assert.AreEqual( 50f, routed.Happiness );

		var (stuck, walled, second) = Deciding( SeedFor( 1 ), stamp: 7, walled: true );

		second.Step( stuck, walled, playing: null, tick: 100 );

		Assert.AreEqual( PeepState.Deciding, stuck.State, "nowhere to wander to" );
		Assert.AreEqual( 100, stuck.TimeStartedIdling, "a failed wander stamps" );
		Assert.AreEqual( 50f, stuck.Happiness, "and costs nothing" );
	}

	/// <summary>
	/// A guest wandering from a linked cell aims at a random point <b>inside</b> the target cell, not its
	/// centre - the original's own arithmetic. The no-links arm aims at a centre
	/// (<see cref="ParkNoLinksWanderTests"/>); this file's park-less behaviour takes every cell as linked.
	///
	/// <para>
	/// <b>This is the discriminating check on <c>SetRandomDest</c>'s linked arm.</b> Every destination set
	/// anywhere else in this file is a cell centre, so an implementation that reached for the tidy answer would
	/// be indistinguishable by state and position alone. The roll is masked to <c>0x7f</c> and clamped to
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
	/// <b>The shipped park is saved open</b>, and this arm is reached once the entry-price screen's door shuts
	/// it (<see cref="ParkState.SetParkClosed"/>). It is driven here by building <see cref="PeepBehaviour"/>
	/// from the two facts rather than from a park, which is the same reason the waiting-outside arm is testable.
	/// </para>
	/// <para>
	/// Losing <c>BigHappinessChange</c> rather than the medium one is what separates this from every other
	/// mood change in the admission states, so the amount is asserted and not just the direction.
	/// </para>
	/// <para>
	/// <b>They walk to the crossing and arrive</b>, so the arrival is asserted as well as the aim: a guest only
	/// aimed at it can still be standing where they decided.
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

		var stops = new[] { admission.CrossingParkSideA, admission.CrossingParkSideB }
			.Select( cell => ((cell.X * one) + (one / 2), (cell.Y * one) + (one / 2)) )
			.ToHashSet();

		var stopCells = new[] { admission.CrossingParkSideA, admission.CrossingParkSideB }.ToHashSet();

		foreach ( var id in InTheGateway )
		{
			Assert.AreEqual( PeepState.PickingACellOutside, guests[id].State,
				$"guest {id} should have given up on a park that shut under them, WALKED to the crossing, "
				+ "and gone on to pick a cell outside the park" );

			Assert.IsTrue( stops.Contains( (guests[id].Navigator.Target.X, guests[id].Navigator.Target.Y) ),
				$"guest {id} should be walking to the crossing's park side, not to {guests[id].Navigator.Target}" );

			Assert.IsTrue( stopCells.Contains( guests[id].Navigator.Position.Cell ),
				$"guest {id} should have REACHED the crossing rather than merely been aimed at it - they "
				+ $"are standing at {guests[id].Navigator.Position.Cell}" );

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
