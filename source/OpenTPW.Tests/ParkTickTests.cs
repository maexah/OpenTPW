using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The seam between the simulation and the frame: does ticking a park the way <see cref="Level.Update"/>
/// ticks one actually move anybody, and would the drawing then read the moved position.
///
/// <para>
/// <b>A test of the walk alone cannot see the wiring.</b> Driving <see cref="PeepWalk"/> directly - plan a
/// route, call <c>Step</c>, watch the position change - proves the walk and nothing about what lies between
/// <see cref="ParkPeople"/>, the clock and the renderer, where a fault leaves a real park with nobody
/// moving at all. These tests drive that wiring instead of going round it.
/// </para>
/// <para>
/// <b>The clock is driven exactly as a level drives it</b>, and a scene entry is reproduced first:
/// <see cref="GameClock.Rebase"/> deliberately throws away the frame that follows it - the one that spans
/// the load - so a test that does not spend that frame sees no ticks at all and would "reproduce" the
/// fault for entirely the wrong reason.
/// </para>
/// </summary>
[TestClass]
public class ParkTickTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	/// <summary>Every <see cref="ParkPeople"/> a test made goes with it, out of <see cref="Entity.All"/> and <c>Current</c>.</summary>
	[TestCleanup]
	public void LetThePeopleGo() => TestRun.DeleteEvery<ParkPeople>();

	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>
	/// One frame, through both clocks, in the order a frame runs them: <c>Time.Update</c> in
	/// <c>Renderer.Update</c>, then <c>GameClock.Update</c> in <see cref="Level.Update"/>.
	/// </summary>
	private static void Frame( float seconds )
	{
		Time.Update( seconds );
		GameClock.Update( paused: false, GameClock.ParkCatchUp );
	}

	/// <summary>Entering a park: the clock re-bases, and the frame spanning the load is spent.</summary>
	private static void EnterPark()
	{
		Time.Paused = false;
		Time.StepFrames = 0;
		GameClock.Rebase();
		Frame( 0f );
	}

	/// <summary>
	/// The frame clock's count and its seconds set to nought, as a fresh process has them. Guests take their turns
	/// on <c>GameClock.Ticks / 8</c>, which no scene entry resets, so without this a seeded run still plays out by
	/// however many ticks the tests before it ran.
	/// </summary>
	private static void PinTheClock()
	{
		typeof( GameClock ).GetProperty( nameof( GameClock.Ticks ) )!.SetValue( null, 0 );
		typeof( GameClock ).GetProperty( nameof( GameClock.Now ) )!.SetValue( null, 0f );
	}

	/// <summary>A sixtieth of a second, which is about two frames to a 31ms tick.</summary>
	private const float AFrame = 1f / 60f;

	/// <summary>
	/// <b>Ticking a park moves its guests.</b>
	/// </summary>
	[TestMethod]
	public void TickingTheParkMovesItsGuests()
	{
		var world = World();
		var people = new ParkPeople( world );

		try
		{
			EnterPark();

			var before = people.Peeps.ToDictionary( peep => peep.ThingId, peep => peep.Navigator.Position );

			var ticked = 0;

			for ( var frame = 0; frame < 120; ++frame )
			{
				Frame( AFrame );
				ticked += GameClock.TicksDue;
				people.Update();
			}

			// If this is zero the clock never ran and nothing below means anything - so it is asserted
			// first and separately, because "nobody moved" has two very different causes.
			Assert.IsTrue( ticked > 0, "two seconds of frames should have come due as ticks" );

			var moved = people.Peeps
				.Where( peep => peep.Navigator.Position != before[peep.ThingId] )
				.ToList();

			Assert.AreEqual( 12, moved.Count,
				"the twelve guests in a walking state should all have moved; the thirteenth is waiting "
				+ $"for the park to open. Ticks that came due: {ticked}." );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>The theme these run against - the one park in scope.</summary>
	private const string Theme = "jungle";

	/// <summary>
	/// What a run of the park saw, gathered across every frame rather than read off the last.
	///
	/// <para>
	/// <b>It carries the stages BEFORE the one under test on purpose.</b> An invite needs a guest to choose
	/// a ride, walk to it, join its queue and reach the front of it; if any of those never happens the
	/// invite cannot either, and "not invited" alone would send a reader to the wrong place entirely. So
	/// each stage is reported, and a failure says which one the park stopped at.
	/// </para>
	/// </summary>
	private sealed record Ridden( bool Invited, bool Nominated, bool Boarding,
		bool Queued, bool AtFront, int LongestQueue, int Ticked, int ThingTicks, string Variables,
		string Nominators )
	{
		/// <summary>The stages in order, for a failure message that points at the first missing one.</summary>
		public string Trace =>
			$"{ThingTicks} thing ticks ({Ticked} game ticks); queued={Queued}, "
			+ $"longest={LongestQueue}, atFront={AtFront}, invited={Invited}, "
			+ $"nominated={Nominated}, boarding={Boarding}; nominators=[{Nominators}]; "
			+ $"script[{Variables}]";
	}

	/// <summary>
	/// Runs the shipped park the way <see cref="Level"/> runs one, with or without the delegate that lets a
	/// ride reach its own script - which is the single thing under test.
	/// </summary>
	/// <remarks>
	/// The budget is in FRAMES and a thing tick is about fifteen of them, so this is about 2,700 turns
	/// of the thing engine - comfortably past the six hundred
	/// <see cref="ParkRideJoinTests"/> needs to see a queue form at all, because reaching the FRONT of one
	/// is further along than joining it.
	/// </remarks>
	private Ridden RunPark( bool wireScripts, int frames = 40000 )
	{
		var world = World();
		var state = new ParkState( world );
		var catalogue = new ParkItemCatalogue( Theme, data );
		var rides = new ParkRides( Theme, world, catalogue, data );

		var people = new ParkPeople( world, new ParkBalance( Theme, easyMode: true ),
			() => ParkRides.GateIsOpen, state, catalogue,
			wireScripts ? thingId => rides.Scheduler.Find( rides.ScriptFor( thingId ) ) : null );

		try
		{
			EnterPark();

			bool invited = false, nominated = false, boarding = false, queued = false, atFront = false;
			int ticked = 0, longest = 0;

			// Sorted so the reported sets read the same way every run - they go into a message a human
			// compares by eye against the park's own thing ids.
			var nominators = new SortedSet<int>();

			// And which things were ever QUEUED for, which is a different question from which ever
			// invited. "Never queued for" and "queued for but never invites" want opposite fixes, and a
			// single boolean cannot tell them apart.
			var queuedFor = new SortedSet<int>();

			// <b>EVERY state any guest was ever seen in, rather than a hand-picked pair.</b> A disjunction
			// hides a missing step: "EnteringRide or Riding" cannot see whether the chain stalls before
			// Riding. Collecting them all costs one set and cannot be wrong about which it omits.
			var statesSeen = new SortedSet<string>();

			for ( var frame = 0; frame < frames; ++frame )
			{
				Frame( AFrame );
				ticked += GameClock.TicksDue;

				rides.Update();
				people.Update();

				foreach ( var peep in people.Peeps )
				{
					statesSeen.Add( peep.State.ToString() );

					invited |= peep.BeenAdmitted;
					// <b>The far end of the chain, and NOT a disjunction</b>, which passes on its first half
					// alone: a test that spans a step boundary cannot see the boundary.
					// Riding is only reached by completing an admission, so this asserts that and nothing
					// weaker. Every state actually seen is reported in the trace regardless.
					boarding |= peep.State == PeepState.Riding;
					atFront |= peep.State == PeepState.InQueue && peep.QueuePos == 0;
				}

				foreach ( var thing in world.Objects )
				{
					if ( state.PersonBeingLoaded( thing.ThingId ) != 0 )
					{
						nominated = true;

						// WHICH things call somebody forward, not merely that one did. Shops and sideshows
						// share the ride handshake - all thirteen of their scripts declare the same common
						// twelve - so this is where that stops being an inference and becomes an observation.
						nominators.Add( thing.ThingId );
					}

					var length = state.QueueLength( thing.ThingId );

					if ( length <= 0 )
						continue;

					queued = true;
					queuedFor.Add( thing.ThingId );
					longest = System.Math.Max( longest, length );
				}

				// <b>No early exit, and that is deliberate rather than an oversight.</b> Stopping as soon as
				// invited, nominated and boarding are all true can end the run once the ride has reached
				// those three, while the set of things that called somebody forward is still the ride alone -
				// which reads as "only rides invite" and means "stopped looking". An early exit inside a
				// measuring loop truncates the very thing being measured.
				// The whole budget costs a few hundred milliseconds, which is not worth a wrong answer.
			}

			// What the ride's own script holds when the run ends. Which of Invite's gates refuses is not
			// visible from the outside, and "not invited" has five quite different causes.
			var bounce = wireScripts ? rides.Scheduler.Find( rides.ScriptFor( 13 ) ) : null;

			// Whether the script is ALIVE, which "every variable is nought" cannot distinguish from
			// "the engine never wrote capacity". NAME is Bouncy.RSE's first instruction, so an empty name
			// means not one instruction has run.
			var vars = bounce == null
				? "no script for thing 13"
				: $"name='{bounce.Name}', running={bounce.Running}, pc={bounce.Position}, "
					+ $"notImpl={bounce.NotImplemented}, ignored={bounce.IgnoredWrites}, "
					+ $"letMeOn={bounce[ParkRideOperation.AdmitVariable]}, "
					+ $"capacity={bounce[ParkRideOperation.CapacityVariable]}, "
					+ $"onRide={bounce[ParkRideOperation.OnRideVariable]}, "
					+ $"running={bounce[ParkRideOperation.RunningVariable]}, "
					+ $"broken={bounce[ParkRideOperation.BrokenVariable]}, "
					+ $"letMeOff={bounce[ParkRideOperation.DismissVariable]}";

			return new Ridden( invited, nominated, boarding, queued, atFront, longest,
				ticked, ticked / ParkPeople.ThingTickEvery, vars,
				$"invited {string.Join( ",", nominators )} | queuedFor {string.Join( ",", queuedFor )}"
					+ $" | states {string.Join( ",", statesSeen )}" );
		}
		finally
		{
			people.Delete();
			rides.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>Ticking a park takes a RIDE's turn.</b>
	///
	/// <para>
	/// The boarding chain's own tests - invite, admit, complete, dismiss - construct
	/// <see cref="ParkRideOperation"/> by hand, and a <see cref="ParkPeople"/> built without the script
	/// delegate returns from <c>TakeTheRidesTurns</c> on its first line. So the wiring is proved only by a
	/// park built with the delegate, as this one is.
	/// </para>
	/// <para>
	/// <b>The control is the half that matters.</b> The same park, the same frames, with the delegate left
	/// null: nobody may ever be invited. Without it this test would pass on any park where a guest merely
	/// reached a queue, and would keep passing if the ride's turn were deleted.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TickingTheParkLetsARideCallSomebodyAboard()
	{
		var driven = RunPark( wireScripts: true );

		// Printed rather than only asserted on: which THINGS call somebody forward is the evidence that
		// shops and sideshows share the ride handshake, and a passing test would otherwise say nothing.
		System.Console.WriteLine( $"ride turn: {driven.Trace}" );

		// Asserted first and separately: if the clock never ran, everything below is vacuously true.
		Assert.IsTrue( driven.Ticked > 0, "no tick ever came due, so this test proves nothing" );

		// In stage order, so the FIRST failure names where the park actually stopped. An invite needs a
		// guest to have chosen a ride, walked to it, queued, and reached the front; asserting the invite
		// alone would blame the last link for a break three steps earlier.
		Assert.IsTrue( driven.Queued, $"nobody ever joined a queue, so no ride could invite - {driven.Trace}" );
		Assert.IsTrue( driven.AtFront, $"nobody ever reached the FRONT of a queue - {driven.Trace}" );

		Assert.IsTrue( driven.Invited,
			$"a ride's turn should have called the head of its queue aboard - {driven.Trace}" );
		Assert.IsTrue( driven.Nominated,
			$"and should have nominated them - the flag alone is half the handshake - {driven.Trace}" );
		Assert.IsTrue( driven.Boarding,
			"and should have ended up RIDING - which needs the guest's own BeingAdmitted arm to admit them "
			+ $"and their EnteringRide arm to complete it, neither of which this tree had - {driven.Trace}" );

		// The control. Same park, same frames, no way for a ride to reach its script.
		var inert = RunPark( wireScripts: false );

		Assert.IsTrue( inert.Ticked > 0, "the control must really have ticked, or it proves nothing either" );
		Assert.IsFalse( inert.Invited, "with no script delegate no ride can invite anybody" );
		Assert.IsFalse( inert.Nominated, "and none can nominate anybody" );
	}

	/// <summary>
	/// <b>Every guest ages, not just the quarter whose id is a multiple of four.</b>
	///
	/// <para>
	/// <see cref="Peep.Tick"/> gives each guest one turn in four by <c>(id &amp; 3) == (tick &amp; 3)</c>,
	/// and the tick it counts must be the <b>thing</b> tick. Feed it the game tick and every value
	/// reaching it is a multiple of eight, so its low two bits are nought: the test is true only for
	/// guests whose id is a multiple of four, and the other three quarters never age again. Swapping the
	/// thing tick for the game tick fails here.
	/// </para>
	/// <para>
	/// <b>The first assertion is a guard, not a formality.</b> If the shipped park happened to put all
	/// its guests in one slot, the rest of this would pass whichever tick was fed in. So the four slots
	/// are asserted to be represented before anything is concluded from them being served.
	/// </para>
	/// </summary>
	[TestMethod]
	public void GuestsInEveryTickSlotGetTheirNeedsTicked()
	{
		var world = World();
		var people = new ParkPeople( world );

		try
		{
			EnterPark();

			var slots = people.Peeps
				.Select( peep => peep.ThingId & (Peep.TickShare - 1) )
				.Distinct()
				.ToList();

			CollectionAssert.AreEquivalent( new[] { 0, 1, 2, 3 }, slots,
				"the shipped park must have guests in all four slots or this proves nothing" );

			var before = people.Peeps.ToDictionary( peep => peep.ThingId, peep => peep.ExitLevel );

			// Sixteen thing ticks, so every slot comes round four times over.
			for ( var tick = 0; tick < 16 * ParkPeople.ThingTickEvery; ++tick )
			{
				Frame( GameClock.TickSeconds );
				people.Update();
			}

			foreach ( var peep in people.Peeps )
			{
				Assert.IsTrue( peep.ExitLevel < before[peep.ThingId],
					$"guest {peep.ThingId} is in slot {peep.ThingId & (Peep.TickShare - 1)} and did not "
					+ $"age at all: {before[peep.ThingId]} -> {peep.ExitLevel}" );
			}
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>The thing engine turns once every eight game ticks, and this pins the eight.</b>
	///
	/// <para>
	/// The park loop gates it at <c>0054f668</c> on <c>(counter &amp; 7) == 0</c>. Running it on the 31ms
	/// beat instead moves every guest in the shipped park <b>eight times too fast</b>.
	/// </para>
	/// <para>
	/// <b>It measures the period rather than a distance</b>, because a distance test passes whatever the
	/// rate is: guests that move eight times too far still moved. This records which game ticks the
	/// position actually changes on and asserts every gap between them is
	/// <see cref="ParkPeople.ThingTickEvery"/>, so removing the gate turns every gap into one and fails
	/// here at once.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheThingEngineTurnsOnceEveryEightGameTicks()
	{
		var world = World();
		var people = new ParkPeople( world );

		try
		{
			EnterPark();

			var walk = people.WalkFor( 42 );

			Assert.IsNotNull( walk, "thing 42 is one of the twelve guests that walk" );

			var turnedOn = new List<int>();
			var last = walk.Position;

			for ( var tick = 1; tick <= 64; ++tick )
			{
				Frame( GameClock.TickSeconds );
				people.Update();

				if ( walk.Position == last )
					continue;

				turnedOn.Add( tick );
				last = walk.Position;
			}

			Assert.IsTrue( turnedOn.Count >= 4,
				$"the walk should have turned several times in 64 game ticks, not {turnedOn.Count}" );

			for ( var i = 1; i < turnedOn.Count; ++i )
			{
				Assert.AreEqual( ParkPeople.ThingTickEvery, turnedOn[i] - turnedOn[i - 1],
					$"game ticks between turn {i} and {i + 1}; the whole series was "
					+ string.Join( ", ", turnedOn ) );
			}
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>Guests arrive on the park's own clock, carrying on the wait the save was left in</b> (<c>docs/exe/park.md</c>,
	/// "Arrivals"). The clock is the save's <c>mGameTick</c>, 755 in Lost Kingdom, one up a sweep, and the wait counts
	/// from the save's <c>mTimeSig</c>, 661. So the first load is called on <c>mGameTick</c> 1264, the 509th sweep, 126.2 s
	/// in; it is let go on the sweep after its last guest is dropped; and the next is called on the first tick whose
	/// fours are 151 past the let-go's, 602 to 605 sweeps after that drop.
	///
	/// <para>
	/// <b>No script is bound here</b>, so the manager takes OpenTPW's no-script fallback: the sweep that calls the
	/// load is the summons, and with no script to wait for its guest is dropped on the next, where the original would
	/// wait for the vehicle to answer 2. The handshake with a bound bus is
	/// <see cref="TheBusIsHeldAtTheStopUntilTheSweepAfterItsLastGuest"/>.
	/// </para>
	/// </summary>
	/// <remarks>
	/// <b>Mutations</b>: the mark taken from the frame clock rather than the save; the compare made <c>&gt;=</c>; the
	/// load let go on the drop's own sweep; the mark stamped with the drop's tick though the load is let go a sweep
	/// later (1264 and 1265 share a four, so only the mark itself tells them apart); the call returning before it asks
	/// the vehicle; the clock not advanced by the sweep.
	/// </remarks>
	[TestMethod]
	public void GuestsArriveOnTheParkClockFromTheWaitTheSaveLeft()
	{
		var world = World();
		var people = new ParkPeople( world );

		try
		{
			EnterPark();

			Assert.AreEqual( 755, people.State.GameTick, "the park's clock starts from the save's" );
			Assert.AreEqual( 661, people.ArrivalMark, "and its wait from the save's mark" );

			var newest = people.Peeps.Max( peep => peep.ThingId );
			int? call = null, drop = null, letGo = null, next = null;

			for ( var frame = 0; frame < 4000 && next == null; ++frame )
			{
				var tickBefore = people.State.GameTick;
				var heldBefore = people.LoadHeld;

				// A tenth of a second is three or four 31 ms ticks, so no frame runs more than one sweep, and each
				// change below is stamped with the sweep that made it.
				Frame( 0.1f );
				people.Update();

				var tick = people.State.GameTick;

				Assert.IsTrue( tick - tickBefore <= 1, $"one sweep a frame at most, not {tick - tickBefore}" );

				if ( !heldBefore && people.LoadHeld )
				{
					if ( call == null )
						call = tick;
					else
						next = tick;
				}

				if ( drop == null && people.Peeps.Max( peep => peep.ThingId ) > newest )
				{
					drop = tick;

					Assert.IsTrue( people.LoadHeld && people.StillToDrop == 0,
						"the last guest's sweep leaves the load held with nobody left: the flag is apart from the count" );
				}

				if ( heldBefore && !people.LoadHeld && letGo == null )
				{
					letGo = tick;

					Assert.AreEqual( tick, people.ArrivalMark, "the next wait counts from the let-go's own tick" );
				}
			}

			Assert.AreEqual( 1264, call, "the first load is called on the 509th sweep after 755" );
			Assert.AreEqual( call + 1, drop, "the call's own sweep is the summons; with no script to wait for, its one guest comes on the next" );
			Assert.AreEqual( drop + 1, letGo, "the load is let go on the sweep after the last drop, not on it" );
			Assert.AreEqual( ParkPeople.FirstDueTick( letGo!.Value, 150 ), next,
				"and the next is called on the first tick whose fours are 151 past the let-go's" );
			Assert.IsTrue( next - drop is >= 602 and <= 605, $"{next - drop} sweeps from the drop to the next call" );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>The bus is held at the stop until the sweep after its last guest, and only then is the load let go</b>
	/// (<c>FUN_004cf3e0</c>; <c>docs/exe/park.md</c>, "Arrivals"). The save's own bus runs <c>bus.RSE</c>, and the
	/// test plays that script's part by hand, by name, rather than stepping it: waiting between runs at 0, then at
	/// the stop at 2 and spinning on <c>VAR_TRIGGER</c> (<c>bus.RSE</c> 39-45). A waiting bus is summoned on the
	/// sweep that calls the load, its guest is dropped only once it answers 2, it is not let go on that drop's
	/// sweep, and the load is let go, the wait restarted and the bus sent away on the first sweep after that finds
	/// it still at 2.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> <c>StepVehicle</c> passing <c>loadHeld: false</c>, which lets the bus go on the drop's sweep;
	/// the let-go not waiting for 2.
	///
	/// <para>
	/// <b>The generators are seeded, the frame clock is pinned, and the stop is asserted empty.</b> The load is
	/// called 509 sweeps in, and by then the draw can have sent a saved guest home: one standing at the stop has a
	/// vehicle summoned for them and a bus at 2 with its load off sent on (<see cref="ParkPeople.LeaverAtTheStop"/>),
	/// which is the original's and not this test's subject. Of seeds 0 to 599 given to all four generators, seven
	/// put a guest there (<see cref="SeedWithALeaverAtTheStop"/> is one), the same seven on a second pass; without
	/// the clock pinned (<see cref="PinTheClock"/>) a seed's outcome changes with the tests run before it.
	/// </para>
	/// </remarks>
	[TestMethod]
	public void TheBusIsHeldAtTheStopUntilTheSweepAfterItsLastGuest() => TheBusIsHeld( seed: 1 );

	/// <summary>A seed whose draws stand a saved guest at the stop before the first load is called.</summary>
	private const int SeedWithALeaverAtTheStop = 164;

	/// <summary>
	/// <b>The handshake above says why it stops when a guest going home stands at the stop</b>, where its own
	/// assertions would name the bus's trigger or pass.
	/// </summary>
	[TestMethod]
	public void TheBusHandshakeNamesALeaverAtTheStop()
	{
		var failure = Assert.ThrowsException<AssertFailedException>( () => TheBusIsHeld( SeedWithALeaverAtTheStop ) );

		StringAssert.Contains( failure.Message, NobodyIsGoingHome );
	}

	private const string NobodyIsGoingHome = "no guest stands at the stop to go home";

	private void TheBusIsHeld( int seed )
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );
		var rides = new ParkRides( Theme, world, catalogue, data );
		var busThing = world.ArrivalVehicleForSmallCrowd;

		// The fixed items' scripts are bound in ParkRides' second pass, which needs the stood models, so the bus's is
		// spawned here from its own catalogue item (1600), as that pass spawns it.
		Assert.IsTrue( catalogue.TryGet( 1600, out var busItem ), "the jungle's catalogue has the bus" );
		var busScript = rides.Scheduler.Spawn( ParkRides.ScriptPathFor( busItem ) );
		var bus = rides.Scheduler.Find( busScript );
		var standingBefore = ParkFixedItems.Current;

		StandVehicles( ("bus", busThing) );

		var people = new ParkPeople( world, new ParkBalance( Theme, easyMode: true ),
			() => ParkRides.GateIsOpen, new ParkState( world ), catalogue,
			thingId => thingId == busThing ? bus : rides.Scheduler.Find( rides.ScriptFor( thingId ) ),
			random: new Random( seed ), behaviourRandom: new Random( seed ), rideRandom: new Random( seed ),
			staffRandom: new Random( seed ) );

		try
		{
			Assert.IsNotNull( bus, $"the bus, thing {busThing}, should run {ParkRides.ScriptPathFor( busItem )}" );

			EnterPark();
			PinTheClock();

			Assert.IsTrue( bus.Set( "VAR_STATUS", 0 ) && bus.Set( "VAR_TRIGGER", 0 ),
				"bus.RSE declares both variables the handshake turns on" );

			var newest = people.Peeps.Max( peep => peep.ThingId );

			for ( var sweep = 0; sweep < 600 && !people.LoadHeld; ++sweep )
				Sweep( people );

			Assert.IsFalse( people.LeaverAtTheStop(), $"{NobodyIsGoingHome} with seed {seed}, or the bus answers them too" );
			Assert.AreEqual( 1264, people.State.GameTick, "the load is called on mGameTick 1264" );
			Assert.AreEqual( 1, bus["VAR_TRIGGER"], "and the waiting bus summoned on the same sweep" );
			Assert.AreEqual( newest, people.Peeps.Max( peep => peep.ThingId ), "nobody is dropped before it answers 2" );

			// Current and reporting nought, as it does driving off (bus.RSE 92-103), or 1 driving in: still nobody.
			foreach ( var status in new[] { 0, 1 } )
			{
				bus.Set( "VAR_STATUS", status );
				Sweep( people );
				Assert.AreEqual( newest, people.Peeps.Max( peep => peep.ThingId ), $"nobody is dropped while it answers {status}" );
				Assert.IsTrue( people.LoadHeld && people.StillToDrop == 1, "and the load is held whole" );
			}

			bus.Set( "VAR_TRIGGER", 0 );
			bus.Set( "VAR_STATUS", 2 );
			Sweep( people );

			Assert.IsFalse( people.LeaverAtTheStop(), $"{NobodyIsGoingHome} with seed {seed} as the load drops" );
			Assert.IsTrue( people.Peeps.Max( peep => peep.ThingId ) > newest, "at the stop, its one guest is dropped" );
			Assert.IsTrue( people.LoadHeld && people.StillToDrop == 0, "and the load is held with nobody left" );
			Assert.AreEqual( 0, bus["VAR_TRIGGER"], "so the bus is NOT let go on the last drop's sweep" );

			bus.Set( "VAR_STATUS", 3 );
			Sweep( people );
			Sweep( people );

			Assert.IsTrue( people.LoadHeld, "a vehicle not answering 2 does not let the load go" );
			Assert.AreEqual( 661, people.ArrivalMark, "nor start the next wait" );

			bus.Set( "VAR_STATUS", 2 );
			Sweep( people );

			Assert.IsFalse( people.LoadHeld, "found at 2 with nobody left, the load is let go" );
			Assert.AreEqual( people.State.GameTick, people.ArrivalMark, "and the next wait counts from this sweep" );
			Assert.AreEqual( 1, bus["VAR_TRIGGER"], "and the bus is sent away" );
		}
		finally
		{
			people.Delete();
			rides.Delete();
			Entity.ApplyDeletions();
			typeof( ParkFixedItems ).GetProperty( nameof( ParkFixedItems.Current ) )!.SetValue( null, standingBefore );
		}
	}

	/// <summary>
	/// <b>A guest going home waits at the crossing while the bus loads, walks to the stop, has a vehicle summoned for
	/// them, and goes when it stands</b> (<c>FUN_00500ad0</c>, <c>FUN_00500bd0</c>, <c>FUN_0051a760</c>, the manager's
	/// tail; <c>docs/exe/ride-operation.md</c>, "Q128"). The test plays the vehicle's script by hand, all three
	/// vehicles answering through the one script, so whichever the summons draws can be played.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a leaver taken out at the crossing; not held while the bus drives in, unloads or moves on;
	/// sent to stop B's cells; no vehicle summoned for one standing at the stop; a vehicle at 2 with its load off not
	/// sent on for them; gone at a status other than 4; left standing at 4; the wrong pair of cells for the vehicle that came; a guest on
	/// the stop who is not going home counted as waiting; a vehicle stood at its first spin triggered when summoned; the
	/// more-than-nine arm of the wait dropped or moved; a guest who goes left on their cell until the sweep's end; one who is not
	/// the head of their cell going; the vehicle sent off from 4 while one still stands there; the stay's sample not counted.
	/// </remarks>
	[TestMethod]
	public void ALeaverWaitsAtTheCrossingWalksToTheStopAndGoesWhenTheVehicleStands()
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );
		var rides = new ParkRides( Theme, world, catalogue, data );
		var busThing = world.ArrivalVehicleForSmallCrowd;

		Assert.IsTrue( catalogue.TryGet( 1600, out var busItem ), "the jungle's catalogue has the bus" );
		var script = rides.Scheduler.Find( rides.Scheduler.Spawn( ParkRides.ScriptPathFor( busItem ) ) );
		var standingBefore = ParkFixedItems.Current;

		const int seaplaneThing = 60000, ferryThing = 60001;

		StandVehicles( ("bus", busThing), ("seaplane", seaplaneThing), ("ferry", ferryThing) );

		// Seeded, so the vehicle the summons draws and the cells the walks end on are the same every run.
		var people = new ParkPeople( world, new ParkBalance( Theme, easyMode: true ),
			() => ParkRides.GateIsOpen, new ParkState( world ), catalogue,
			thingId => thingId is var id && (id == busThing || id == seaplaneThing || id == ferryThing)
				? script : rides.Scheduler.Find( rides.ScriptFor( thingId ) ),
			random: new Random( 3 ), behaviourRandom: new Random( 3 ) );

		void Plays( int status )
		{
			script!.Set( "VAR_TRIGGER", 0 );
			script.Set( "VAR_STATUS", status );
		}

		int Triggered() => script!["VAR_TRIGGER"];

		try
		{
			Assert.IsNotNull( script );
			EnterPark();
			Plays( 0 );

			// A guest standing on the stop who is not going home is nobody waiting.
			var stop = people.Admission!.BusStopA;
			var cells = PeepBehaviour.StopCells( stop );

			Assert.AreNotEqual( 0, people.Admit( stop.X, stop.Y ) );
			Assert.IsFalse( people.LeaverAtTheStop(), "a guest on the stop's cell who is not in state 0x15 is not waiting" );

			// The bus current and driving in, and a guest at the crossing who is going home.
			people.ForceArrival( 1 );
			Sweep( people );
			Plays( 1 );

			var id = people.Admit( 47, 9 );
			var guest = people.Guests[id];
			guest.PaidAdmission = true;
			guest.SetState( PeepState.PickingACellOutside, people.State.GameTick, new Random( 1 ) );

			static int StaysCounted() => Unimplemented.Summary.FirstOrDefault( gap => gap.What == "LEAVER_STAY_SAMPLE" ).Times;
			var staysBefore = StaysCounted();

			foreach ( var status in new[] { 1, 2, 3 } )
			{
				Plays( status );
				people.HoldTheLoad();
				Sweep( people );
				Sweep( people );
				Assert.AreEqual( PeepState.PickingACellOutside, guest.State, $"the bus at {status}: they wait at the crossing" );
				Assert.IsTrue( people.Guests.ContainsKey( id ), "and are not taken out there" );
			}

			// More than nine of the load still to drop: only the bus moving on holds them (FUN_0051a760).
			foreach ( var (status, withTen, withNine) in new[] { (0, true, true), (1, true, false), (2, true, false), (3, false, false), (4, true, true), (5, true, true) } )
			{
				Plays( status );
				people.HoldTheLoad( 10 );
				Assert.AreEqual( withTen, people.MayCrossTheRoad(), $"ten still to drop, the bus at {status}" );
				people.HoldTheLoad( 9 );
				Assert.AreEqual( withNine, people.MayCrossTheRoad(), $"nine still to drop, the bus at {status}" );
			}

			people.HoldTheLoad( 0 );
			Plays( 5 );
			Sweep( people );
			Assert.AreEqual( PeepState.WalkingOutside, guest.State, "the bus gone on: they set off" );

			for ( var sweep = 0; sweep < 300 && guest.State != PeepState.AtTheBusStop; ++sweep )
				Sweep( people );

			Assert.AreEqual( PeepState.AtTheBusStop, guest.State, "they reach the stop" );
			Assert.IsTrue( StaysCounted() > staysBefore, "and the stay they would hand the park analyser there, having paid at the gate, is counted, not built" );
			Assert.IsTrue( cells.Any( cell => Near( cell, guest.Navigator.Position.Cell ) ),
				$"on one of stop A's four cells, not at {guest.Navigator.Position.Cell}" );
			Assert.IsTrue( people.LeaverAtTheStop() );
			Assert.AreEqual( PeepBehaviour.ArrivalHeading, people.WalkFor( id )!.Heading, "facing as for the bus, the one current" );

			// The bus spent and forgotten: with a leaver standing there one is summoned, at random of the three.
			Plays( 6 );
			Sweep( people );
			Plays( 0 );
			Sweep( people );
			Assert.AreNotEqual( ParkPeople.NoVehicle, people.VehicleStatus(), "a vehicle is summoned for them" );
			Assert.AreEqual( 1, Triggered(), "and triggered" );

			var larger = people.LargerVehicleIsCurrent;
			var pair = larger ? cells[2..] : cells[..2];

			// Driving in, they walk to that vehicle's pair if they are not on it already.
			Plays( 1 );

			for ( var sweep = 0; sweep < 300; ++sweep )
			{
				Sweep( people );

				if ( guest.State == PeepState.AtTheBusStop && pair.Any( cell => Near( cell, guest.Navigator.Position.Cell ) ) )
					break;
			}

			Assert.AreEqual( PeepState.AtTheBusStop, guest.State );
			Assert.IsTrue( pair.Any( cell => Near( cell, guest.Navigator.Position.Cell ) ),
				$"on the {(larger ? "larger vehicle's" : "bus's")} pair, not at {guest.Navigator.Position.Cell}" );

			// Standing at the arrivals' stop with no load to drop and somebody waiting: sent on.
			Plays( 2 );
			Sweep( people );
			Assert.AreEqual( 1, Triggered(), "at 2 with its load off, it is sent on for them" );
			Assert.IsTrue( people.Guests.ContainsKey( id ), "they do not go at 2" );

			// A second guest on the same cell, newer and so visited before them in the sweep, with the first stood
			// there again as the head of its list: at 4 only the head goes, and the vehicle is not sent off while one
			// still stands there.
			var (cellX, cellY) = guest.Navigator.Position.Cell;
			var second = people.Admit( cellX, cellY );
			var staysSoFar = StaysCounted();

			people.Guests[second].SetState( PeepState.AtTheBusStop, people.State.GameTick, new Random( 1 ) );
			Assert.AreEqual( staysSoFar, StaysCounted(), "one who never paid at the gate hands the analyser no stay" );
			Assert.AreEqual( second, (int)people.State.CellAt( cellX, cellY ).Occupant, "the newcomer heads the cell" );
			Assert.AreEqual( second, people.Peeps[0].ThingId, "and the sweep" );
			people.State.Forget( id );
			people.State.StandOn( id, cellX, cellY );
			Assert.AreEqual( id, (int)people.State.CellAt( cellX, cellY ).Occupant, "the first guest heads the cell again" );

			// They hold a balloon, which the original deletes with them (FUN_004fb330): it is not let go to burst.
			people.Guests[second].Balloon = Balloon.Make( second, sets: 4, now: 0 );
			people.Guests[id].Balloon = Balloon.Make( id, sets: 4, now: 0 );
			Assert.AreEqual( 0, people.Bursting.Count );

			Plays( 4 );
			Sweep( people );
			Assert.IsFalse( people.Guests.ContainsKey( id ), "at 4 the head of the cell is gone" );
			Assert.AreEqual( 0, people.Bursting.Count, "and the balloon they held is gone with them, not let go" );
			Assert.IsTrue( people.Guests.ContainsKey( second ), "and the one behind, whose turn came first, is not" );
			Assert.IsNotNull( people.Guests[second].Balloon, "and keeps theirs" );
			Assert.AreEqual( 0, Triggered(), "with one still at the stop the vehicle is not sent off" );

			// A third, the newest, who heads the cell and is visited first: they go inside their own turn, so the
			// one behind is the head later in the same sweep and goes too; and with nobody left at the stop the
			// vehicle is sent off on that sweep, the manager's turn coming after every guest's.
			var third = people.Admit( cellX, cellY );

			people.Guests[third].SetState( PeepState.AtTheBusStop, people.State.GameTick, new Random( 1 ) );
			Assert.AreEqual( third, (int)people.State.CellAt( cellX, cellY ).Occupant, "the newest heads the cell" );

			Sweep( people );
			Assert.IsFalse( people.Guests.ContainsKey( third ), "they are gone" );
			Assert.IsFalse( people.Guests.ContainsKey( second ), "and so is the one who stood behind them, on the same sweep" );
			Assert.AreEqual( 1, Triggered(), "and with nobody left at the stop it is sent off" );

			// A vehicle found standing at its first spin, unloading, when it is first summoned is made current and
			// not triggered: its drive in is done, and a trigger would send it on empty.
			Plays( 6 );
			Sweep( people );
			Plays( 2 );
			people.ForceArrival( 1 );
			Sweep( people );
			Assert.AreEqual( 0, Triggered(), "summoned while it stands unloading: not triggered" );
			Assert.AreNotEqual( ParkPeople.NoVehicle, people.VehicleStatus(), "and current" );
		}
		finally
		{
			people.Delete();
			rides.Delete();
			Entity.ApplyDeletions();
			typeof( ParkFixedItems ).GetProperty( nameof( ParkFixedItems.Current ) )!.SetValue( null, standingBefore );
		}
	}

	/// <summary>
	/// <b>What a larger vehicle does differently, and whose summons is whose</b> (<c>docs/exe/park.md</c>, "Arrivals"
	/// and "The spent vehicle"; <c>docs/exe/ride-operation.md</c>, "Q128"). The third vehicle's load is made two rows
	/// out from stop B (<c>FUN_004cf720</c>); it holds nobody at the road and is nobody's bus to run for
	/// (<c>0x0051a774</c>, <c>FUN_0051aad0</c>); a guest at the stop walks to its pair of cells while it moves on as
	/// while it drives in (<c>FUN_00500bd0</c>). A leaver does not send a vehicle on while its load still drops
	/// (<c>0x004cf4de</c>). A vehicle spent while its load is held is forgotten all the same and the load's own is
	/// summoned by its size, before the tail can draw one at random for a leaver (<c>0x004cf489</c> before
	/// <c>0x004cf4b6</c>); with no load, the leavers' summons draws each of the three. All three vehicles answer
	/// through one script, played by hand.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a scripted vehicle's load made at stop B; leavers held at the road by a ferry; guests running
	/// for the ferry at 3; no walk to the pair while the vehicle moves on; a vehicle at 2 sent on with its load still
	/// dropping; a vehicle spent with its load held never forgotten; the tail run before the manager; the leavers'
	/// summons always the bus.
	/// </remarks>
	[TestMethod]
	public void ALargerVehicleIsNobodysBusAndEachSummonsDrawsItsOwn()
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );
		var rides = new ParkRides( Theme, world, catalogue, data );
		var busThing = world.ArrivalVehicleForSmallCrowd;

		Assert.IsTrue( catalogue.TryGet( 1600, out var busItem ), "the jungle's catalogue has the bus" );
		var script = rides.Scheduler.Find( rides.Scheduler.Spawn( ParkRides.ScriptPathFor( busItem ) ) );
		var standingBefore = ParkFixedItems.Current;

		const int seaplaneThing = 60000, ferryThing = 60001;

		StandVehicles( ("bus", busThing), ("seaplane", seaplaneThing), ("ferry", ferryThing) );

		var people = new ParkPeople( world, new ParkBalance( Theme, easyMode: true ),
			() => ParkRides.GateIsOpen, new ParkState( world ), catalogue,
			thingId => thingId is var id && (id == busThing || id == seaplaneThing || id == ferryThing)
				? script : rides.Scheduler.Find( rides.ScriptFor( thingId ) ),
			random: new Random( 5 ), behaviourRandom: new Random( 5 ), rideRandom: new Random( 5 ),
			staffRandom: new Random( 5 ) );

		void Plays( int status )
		{
			script!.Set( "VAR_TRIGGER", 0 );
			script.Set( "VAR_STATUS", status );
		}

		int Triggered() => script!["VAR_TRIGGER"];

		try
		{
			Assert.IsNotNull( script );
			EnterPark();
			Plays( 0 );

			var admission = people.Admission!;
			var cells = PeepBehaviour.StopCells( admission.BusStopA );

			// Sixty-one come by the third vehicle, summoned on the sweep that finds none.
			Assert.AreEqual( 3, people.ForceArrival( 61 ), "sixty-one come by the third vehicle" );
			Sweep( people );
			Assert.IsTrue( people.LargerVehicleIsCurrent, "the load's own vehicle is current" );
			Assert.AreEqual( 1, Triggered(), "and triggered" );

			// Whatever it reports, a vehicle that is not the bus holds nobody at the road.
			foreach ( var status in new[] { 1, 2, 3 } )
			{
				Plays( status );
				Assert.IsTrue( people.MayCrossTheRoad(), $"the third vehicle at {status} holds nobody at the road" );
			}

			// A guest standing at the stop on the bus's pair: while the larger vehicle moves on they walk to its pair.
			Plays( 3 );

			var leaver = people.Guests[people.Admit( cells[0].X, cells[0].Y )];

			leaver.SetState( PeepState.AtTheBusStop, people.State.GameTick, new Random( 1 ) );
			Sweep( people );
			Assert.AreEqual( PeepState.WalkingOutside, leaver.State, "at 3 they set off for the vehicle's pair" );
			Assert.IsTrue( cells[2..].Contains( leaver.Navigator.Target.Cell ), $"one of the larger pair, not {leaver.Navigator.Target.Cell}" );

			// And nobody heading for the gate runs for it.
			var heading = people.Peeps.Where( peep => peep.State == PeepState.HeadingForGate ).ToArray();

			Assert.IsTrue( heading.Length > 0, "some of Lost Kingdom's guests head for the gate" );
			Assert.IsTrue( heading.All( peep => peep.PurposeSpeed != Peep.RunningForTheBusSpeed ), "the third vehicle at 3 is not run for" );

			for ( var sweep = 0; sweep < 300 && leaver.State != PeepState.AtTheBusStop; ++sweep )
				Sweep( people );

			Assert.AreEqual( PeepState.AtTheBusStop, leaver.State, "they stand at the stop again" );
			Assert.IsTrue( people.LeaverAtTheStop() );

			// Unloading, its guests are made two rows out from stop B, one a sweep; and with a leaver standing at the
			// stop it is still not sent on while any are left to drop.
			var newest = people.Peeps.Max( peep => peep.ThingId );

			Plays( 2 );
			Sweep( people );

			var made = people.Peeps.Single( peep => peep.ThingId > newest );
			var stopB = admission.BusStopB;

			Assert.AreEqual( (stopB.X, stopB.Y - 2), people.WalkFor( made.ThingId )!.Position.Cell, "made two rows out from stop B" );
			Assert.AreEqual( 60, people.StillToDrop );
			Assert.AreEqual( 0, Triggered(), "a leaver at the stop does not send it on with sixty still to drop" );

			// Spent with its load still held: forgotten by the asking all the same, and the load's own vehicle summoned
			// by its size on the same sweep, before the tail can draw one at random for the leaver. Two to drop is the
			// bus's load, every time.
			for ( var round = 0; round < 8; ++round )
			{
				people.HoldTheLoad( 2 );
				Plays( 6 );
				Sweep( people );
				Assert.AreEqual( 0, script["VAR_STATUS"], $"round {round}: the spent vehicle's status is written nought" );
				Assert.AreNotEqual( ParkPeople.NoVehicle, people.VehicleStatus(), "one is current again" );
				Assert.IsFalse( people.LargerVehicleIsCurrent, "and it is the bus, the vehicle for a load of two" );
				Assert.AreEqual( 1, Triggered(), "triggered" );
			}

			// No load: the summons is the leaver's, one of the three at random.
			var drawn = new List<bool>();

			for ( var round = 0; round < 24; ++round )
			{
				people.HoldTheLoad( 0 );
				Plays( 6 );
				Sweep( people );
				Assert.AreNotEqual( ParkPeople.NoVehicle, people.VehicleStatus(), "a vehicle is summoned for the leaver" );
				drawn.Add( people.LargerVehicleIsCurrent );
			}

			Assert.IsTrue( drawn.Contains( true ) && drawn.Contains( false ),
				$"the leavers' summons draws the bus and the larger vehicles alike ({drawn.Count( larger => larger )} of 24 larger)" );
		}
		finally
		{
			people.Delete();
			rides.Delete();
			Entity.ApplyDeletions();
			typeof( ParkFixedItems ).GetProperty( nameof( ParkFixedItems.Current ) )!.SetValue( null, standingBefore );
		}
	}

	/// <summary>A cell, or the one beyond it across: a walk's last step can carry a guest just over the far edge.</summary>
	private static bool Near( (int X, int Y) cell, (int X, int Y) at ) => at.Y == cell.Y && at.X - cell.X is 0 or 1;

	/// <summary>
	/// <b>The stop's four cells</b> (<c>FUN_00500ad0</c>'s table): the stop given and the cell across, and the same two
	/// rows out.
	/// </summary>
	/// <remarks><b>Mutations:</b> the rows added; the cell across taken back, not on.</remarks>
	[TestMethod]
	public void TheStopsFourCellsAreStopAs()
	{
		CollectionAssert.AreEqual( new[] { (42, 5), (43, 5), (42, 3), (43, 3) }, PeepBehaviour.StopCells( (42, 5) ) );
	}

	/// <summary>
	/// <b>The summons for a leaver draws one of the three vehicles, each as likely</b> (<c>% 3</c>, in <c>FUN_0051a2f0</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> always the bus; a fourth vehicle; nought.</remarks>
	[TestMethod]
	public void TheSummonsAtRandomDrawsEachOfTheThreeVehicles()
	{
		var random = new Random( 7 );
		var drawn = Enumerable.Range( 0, 600 ).Select( _ => ParkPeople.VehicleAtRandom( random ) ).ToArray();

		CollectionAssert.AreEquivalent( new[] { 1, 2, 3 }, drawn.Distinct().ToArray() );
		Assert.IsTrue( drawn.GroupBy( vehicle => vehicle ).All( group => group.Count() is > 150 and < 250 ), "each about a third" );
	}

	/// <summary>
	/// <b>The bus is triggered by a summons and by nothing else, and a spent bus stays away</b> (<c>FUN_004cf3e0</c>,
	/// <c>FUN_0051a2f0</c>, <c>FUN_0051a690</c>; <c>docs/exe/park.md</c>, "The spent vehicle"). The test plays
	/// <c>bus.RSE</c>'s part by hand: idle with no load, nothing triggers it; a load summons it; it is not triggered
	/// driving in, nor while it unloads; the load all off, it is sent on; standing for leavers at 4 with nobody there,
	/// it is sent off; spent, its status is written nought and it is forgotten with no trigger, until the next load.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> an idle bus triggered; a load that summons nothing; a bus triggered while it drives in or
	/// unloads; not sent on when the load is off; not sent off at 4; a spent bus triggered, or left current, or left
	/// at 6.
	/// </remarks>
	[TestMethod]
	public void TheBusIsTriggeredOnlyByASummonsAndASpentBusStaysAway()
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );
		var rides = new ParkRides( Theme, world, catalogue, data );
		var busThing = world.ArrivalVehicleForSmallCrowd;

		Assert.IsTrue( catalogue.TryGet( 1600, out var busItem ), "the jungle's catalogue has the bus" );
		var bus = rides.Scheduler.Find( rides.Scheduler.Spawn( ParkRides.ScriptPathFor( busItem ) ) );
		var standingBefore = ParkFixedItems.Current;

		StandVehicles( ("bus", busThing) );

		var people = new ParkPeople( world, new ParkBalance( Theme, easyMode: true ),
			() => ParkRides.GateIsOpen, new ParkState( world ), catalogue,
			thingId => thingId == busThing ? bus : rides.Scheduler.Find( rides.ScriptFor( thingId ) ) );

		void Plays( int status )
		{
			bus!.Set( "VAR_TRIGGER", 0 );
			bus.Set( "VAR_STATUS", status );
		}

		int Triggered() => bus!["VAR_TRIGGER"];

		try
		{
			Assert.IsNotNull( bus );
			EnterPark();

			Plays( 0 );

			for ( var sweep = 0; sweep < 30; ++sweep )
				Sweep( people );

			Assert.AreEqual( 0, Triggered(), "idle, with no load: nothing triggers it" );
			Assert.AreEqual( ParkPeople.NoVehicle, people.VehicleStatus(), "and no vehicle is current" );

			var before = people.Peeps.Count;

			Assert.AreEqual( 1, people.ForceArrival( 1 ) );
			Sweep( people );
			Assert.AreEqual( 1, Triggered(), "a load summons it" );

			Plays( 1 );
			Sweep( people );
			Sweep( people );
			Assert.AreEqual( (0, before), (Triggered(), people.Peeps.Count), "driving in: not triggered, and nobody made" );

			Plays( 2 );
			Sweep( people );
			Assert.AreEqual( (0, before + 1), (Triggered(), people.Peeps.Count), "unloading: the guest made, the bus held" );

			Sweep( people );
			Assert.AreEqual( 1, Triggered(), "the load all off: sent on" );
			Assert.IsFalse( people.LoadHeld );

			Plays( 3 );
			Sweep( people );
			Assert.AreEqual( 0, Triggered(), "moving on: not triggered" );

			Plays( 4 );
			Sweep( people );
			Assert.AreEqual( 1, Triggered(), "standing for leavers with nobody there: sent off" );

			Plays( 5 );
			Sweep( people );
			Assert.AreEqual( 0, Triggered(), "driving off: not triggered" );

			Plays( 6 );
			Sweep( people );
			Assert.AreEqual( (0, 0), (Triggered(), bus["VAR_STATUS"]), "spent: its status written nought, and no trigger" );
			Assert.AreEqual( ParkPeople.NoVehicle, people.VehicleStatus(), "and forgotten" );

			for ( var sweep = 0; sweep < 30; ++sweep )
				Sweep( people );

			Assert.AreEqual( 0, Triggered(), "it stays away" );

			people.ForceArrival( 1 );
			Sweep( people );
			Assert.AreEqual( 1, Triggered(), "until the next load summons it" );
			Assert.AreEqual( 1, people.ForceArrival( 40 ), "a load called while a vehicle is current names that vehicle, whatever its size" );
		}
		finally
		{
			people.Delete();
			rides.Delete();
			Entity.ApplyDeletions();
			typeof( ParkFixedItems ).GetProperty( nameof( ParkFixedItems.Current ) )!.SetValue( null, standingBefore );
		}
	}

	/// <summary>
	/// <b>A guest heading for the gate runs for the bus while it pulls away</b> (<c>FUN_004ff730</c>; Q199): the hurry
	/// is 50 on the sweeps the park's own bus answers 3, and the guest's 25 or nought again once it answers 4. The
	/// test plays <c>bus.RSE</c>'s part by hand, as the test above does.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> <c>ParkPeople</c> not handing <c>BusStatus</c> in; <c>BusStatus</c> asking with no vehicle
	/// current, or any vehicle but the bus; a spent bus left current.
	/// </remarks>
	[TestMethod]
	public void AGuestHeadingForTheGateRunsWhileTheBusPullsAway()
	{
		var world = World();
		var catalogue = new ParkItemCatalogue( Theme, data );
		var rides = new ParkRides( Theme, world, catalogue, data );
		var busThing = world.ArrivalVehicleForSmallCrowd;

		Assert.IsTrue( catalogue.TryGet( 1600, out var busItem ), "the jungle's catalogue has the bus" );
		var bus = rides.Scheduler.Find( rides.Scheduler.Spawn( ParkRides.ScriptPathFor( busItem ) ) );
		var standingBefore = ParkFixedItems.Current;

		// A seaplane stood beside it, answering through the bus's script, so a load big enough for it can report 3
		// without the bus: nobody runs for any vehicle but the small crowd's (FUN_0051aad0).
		const int seaplaneThing = 60000;

		StandVehicles( ("bus", busThing), ("seaplane", seaplaneThing) );

		// Every generator seeded: left to the clock's seed, one of the saved guests now and then gave up and reached
		// the stop while this ran, and the vehicle summoned for them is current where the test asks for none.
		var people = new ParkPeople( world, new ParkBalance( Theme, easyMode: true ),
			() => ParkRides.GateIsOpen, new ParkState( world ), catalogue,
			thingId => thingId is var id && (id == busThing || id == seaplaneThing)
				? bus : rides.Scheduler.Find( rides.ScriptFor( thingId ) ),
			random: new Random( 1 ), behaviourRandom: new Random( 1 ), rideRandom: new Random( 1 ),
			staffRandom: new Random( 1 ) );

		try
		{
			Assert.IsNotNull( bus );

			EnterPark();

			// No vehicle is current before the first load (the save's mCurrentArrivalVehicle is nought), so a bus
			// answering 3 then is not asked, and the five saved guests heading for the gate keep their own hurry.
			bus.Set( "VAR_STATUS", PeepBehaviour.BusIsLeaving );
			Sweep( people );

			var heading = people.Peeps.Where( peep => peep.State == PeepState.HeadingForGate ).ToArray();

			Assert.AreEqual( 5, heading.Length, "five of Lost Kingdom's guests head for the gate" );
			Assert.IsTrue( heading.All( peep => peep.PurposeSpeed != Peep.RunningForTheBusSpeed ),
				"with no vehicle current, nobody runs for the bus" );

			bus.Set( "VAR_STATUS", 0 );

			var newest = people.Peeps.Max( peep => peep.ThingId );

			for ( var sweep = 0; sweep < 600 && !people.LoadHeld; ++sweep )
				Sweep( people );

			// Dropped at the stop, the guest walks to the roadside and stands there for as long as the bus unloads or
			// moves on (FUN_004ff520 on FUN_0051a760). The original's thirteen all stood until the tick its bus read 4.
			bus.Set( "VAR_STATUS", 2 );
			Sweep( people );

			var guest = people.Peeps.Single( peep => peep.ThingId > newest );
			var ownHurry = PeepBehaviour.HurriesToTheGate( guest ) ? Peep.HurryingSpeed : Peep.UnhurriedSpeed;
			var admission = people.Admission!;
			var roadside = PeepBehaviour.WalkInDraws( guest.ThingId ).SideB
				? admission.CrossingBusStopSideB : admission.CrossingBusStopSideA;

			Assert.AreEqual( PeepState.Walking, guest.State, "the dropped guest walks to the roadside first" );

			for ( var sweep = 0; sweep < 200 && guest.State == PeepState.Walking; ++sweep )
				Sweep( people );

			Assert.AreEqual( PeepState.AtGate, guest.State, "and stands there" );

			// A walk stops up to a sixth of a cell short of its aim, so one aimed high across the cell, coming from
			// the stop, stands just over its far edge, as the original's guest 53 does (docs/exe/ride-operation.md,
			// "Where a walk ends, measured").
			var stoodOn = people.WalkFor( guest.ThingId )!.Position.Cell;

			Assert.IsTrue( stoodOn == roadside || stoodOn == (roadside.X + 1, roadside.Y),
				$"on the cell their id picked, {roadside}, or just over its far edge, not on {stoodOn}" );

			foreach ( var status in new[] { 2, 3, 1 } )
			{
				bus.Set( "VAR_STATUS", status );
				Sweep( people );
				Sweep( people );

				Assert.AreEqual( PeepState.AtGate, guest.State, $"the road is not clear with the bus at {status}" );
			}

			bus.Set( "VAR_STATUS", 4 );
			Sweep( people );

			Assert.AreEqual( PeepState.HeadingForGate, guest.State, "the bus standing for leavers, they cross" );
			Assert.IsTrue( new[] { admission.TicketBoothA, admission.TicketBoothB }.Contains( guest.Navigator.Target.Cell ),
				$"aimed at a ticket booth's cell, not at {guest.Navigator.Target.Cell}" );

			Sweep( people );

			Assert.AreEqual( ownHurry, guest.PurposeSpeed, "with the bus standing, it is not run for" );

			bus.Set( "VAR_STATUS", PeepBehaviour.BusIsLeaving );
			Sweep( people );

			Assert.AreEqual( PeepState.HeadingForGate, guest.State );
			Assert.AreEqual( Peep.RunningForTheBusSpeed, guest.PurposeSpeed, "pulling away, it is run for at 50" );

			bus.Set( "VAR_STATUS", 4 );
			Sweep( people );

			Assert.AreEqual( ownHurry, guest.PurposeSpeed, "and gone, the guest's own hurry again" );

			// Spent, the bus is let go of by the asking (FUN_0051a690): its status written nought, no vehicle current,
			// and not run for.
			bus.Set( "VAR_STATUS", 6 );
			Sweep( people );

			Assert.AreEqual( 0, bus["VAR_STATUS"], "a spent bus has its status written nought" );
			Assert.AreEqual( ParkPeople.NoVehicle, people.VehicleStatus(), "and is no longer current" );
			Assert.AreEqual( ownHurry, guest.PurposeSpeed );

			Assert.AreEqual( 2, people.ForceArrival( 40 ), "forty come by seaplane" );
			Sweep( people );
			bus.Set( "VAR_STATUS", PeepBehaviour.BusIsLeaving );
			Sweep( people );

			Assert.AreEqual( PeepState.HeadingForGate, guest.State );
			Assert.AreEqual( ownHurry, guest.PurposeSpeed, "the seaplane at 3 is not run for" );
		}
		finally
		{
			people.Delete();
			rides.Delete();
			Entity.ApplyDeletions();
			typeof( ParkFixedItems ).GetProperty( nameof( ParkFixedItems.Current ) )!.SetValue( null, standingBefore );
		}
	}

	/// <summary>One thing sweep: frames of a tenth of a second until the park's clock has gone one up.</summary>
	private static void Sweep( ParkPeople people )
	{
		var from = people.State.GameTick;

		for ( var frame = 0; frame < 10 && people.State.GameTick == from; ++frame )
		{
			Frame( 0.1f );
			people.Update();
		}

		Assert.AreEqual( from + 1, people.State.GameTick, "one sweep" );
	}

	/// <summary>
	/// A <see cref="ParkFixedItems"/> that answers only which thing each vehicle was stood as, made without its
	/// constructor: <see cref="ParkPeople"/> finds a vehicle's script through <see cref="ParkFixedItems.Current"/>,
	/// and the real one builds every fixed item's model, which no test can.
	/// </summary>
	private static void StandVehicles( params (string Name, int Thing)[] vehicles )
	{
		var items = (ParkFixedItems)RuntimeHelpers.GetUninitializedObject( typeof( ParkFixedItems ) );

		typeof( ParkFixedItems ).GetField( "_thingOf", BindingFlags.NonPublic | BindingFlags.Instance )!
			.SetValue( items, vehicles.ToDictionary( vehicle => vehicle.Name, vehicle => vehicle.Thing ) );

		typeof( ParkFixedItems ).GetProperty( nameof( ParkFixedItems.Current ) )!.SetValue( null, items );
	}

	/// <summary>
	/// <b>The drawing moves between thing ticks; the simulation does not.</b> That pair is the whole of
	/// it: the walk turns once every eight game ticks, about four times a second, and the
	/// original slides the picture between the two positions over the frames in between rather than
	/// holding it still and jumping (<c>FUN_004f9f00</c>, per frame from <c>FUN_00518f90</c>).
	///
	/// <para>
	/// <b>It is asserted as a two-sided control, and that is what makes it a test rather than a
	/// tautology.</b> The navigator is held to ONE value across the window while the drawn position is
	/// required to take several. Interpolating in the simulation instead - which would be the wrong fix -
	/// fails the first half; not interpolating at all fails the second.
	/// </para>
	/// <para>
	/// <b>What this does NOT pin, said here rather than assumed away</b> (<c>docs/VERIFYING.md</c> rule
	/// 48). Making <c>ParkGuestSprites.OnRenderTranslucent</c> pass a literal <c>1f</c> instead of the
	/// live fraction <b>passed the whole suite when it was measured</b>: this test reaches <c>Standing</c> directly, and the
	/// render path it would break needs a graphics device a test run has none of. So the drawing's own
	/// wiring rests on the capture and not on the suite. What IS pinned here is the fraction: hard-wiring
	/// <see cref="ParkPeople.ThingTickFraction"/> to one fails this test and
	/// <see cref="EverySweepStampsEverybodyWhereTheyStoodAsItBegan"/>.
	/// </para>
	/// </summary>
	[TestMethod]
	public void BetweenThingTicksTheDrawingMovesAndTheSimulationDoesNot()
	{
		var world = World();
		var people = new ParkPeople( world );

		try
		{
			EnterPark();

			var person = world.People.Single( p => p.ThingId == 42 );
			var sprite = world.Sprites.Single( s => s.Slot == person.SpriteSlot );
			var walk = people.WalkFor( person.ThingId );

			Assert.IsNotNull( walk, "thing 42 is one of the twelve guests that walk" );

			// Get them walking, and then to the far side of a turn so the window below starts fresh.
			var start = walk.Position;

			for ( var frame = 0; frame < 600 && walk.Position == start; ++frame )
			{
				Frame( AFrame );
				people.Update();
			}

			Assert.AreNotEqual( start, walk.Position, "the guest never moved at all" );

			// Now across ONE thing tick: sample what the drawing would put on screen each frame while
			// the walk holds the position it just reached.
			var held = walk.Position;
			var drawn = new HashSet<(float, float)>();
			var navigator = new HashSet<FixedVector>();

			for ( var frame = 0; frame < 600 && walk.Position == held; ++frame )
			{
				var (x, y, _) = ParkGuestSprites.Standing( walk, 10f, 10f, person, sprite,
					ParkPeople.ThingTickFraction );

				drawn.Add( (x, y) );
				navigator.Add( walk.Position );

				Frame( AFrame );
				people.Update();
			}

			Assert.AreEqual( 1, navigator.Count,
				"the simulation moved inside the window, so this measures nothing" );

			Assert.IsTrue( drawn.Count > 1,
				$"the drawing should slide across a thing tick; it took {drawn.Count} position(s)" );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>An arrival is made at one of the five base speeds, drawn</b>, hurrying at 25 from a standstill (<c>FUN_004f8940</c>,
	/// <c>0x004fb1c9</c>): sixty-four arrivals reach all five, and no other.
	/// </summary>
	[TestMethod]
	public void ArrivalsAreMadeAtAllFiveBaseSpeeds()
	{
		var world = World();
		var people = new ParkPeople( world, new ParkBalance( Theme, easyMode: true ), null, new ParkState( world ),
			random: new System.Random( 1 ) );

		var arrivals = Enumerable.Range( 0, 64 ).Select( _ => people.Guests[people.Admit( 55, 30 )] ).ToArray();

		CollectionAssert.AreEquivalent( Peep.BaseSpeeds, arrivals.Select( guest => guest.BaseSpeed ).Distinct().ToArray() );
		Assert.IsTrue( arrivals.All( guest => guest.PurposeSpeed == Peep.HurryingSpeed && guest.PreviousSpeed == 0f
			&& guest.AdjustorSpeed == 0 && guest.Paced ) );
	}

	/// <summary>
	/// <b>Every sweep eases every guest's walking speed</b>, the arrival's from a standstill: on the first sweep after
	/// arriving the mover's speed is a quarter of the base and the hurry of 25 (<see cref="Peep.Pace"/>, the first half of
	/// <c>FUN_004fa870</c>), not the speed of one the mover was made with; and a guest loaded settled keeps theirs.
	/// </summary>
	[TestMethod]
	public void EverySweepEasesEveryGuestsWalkingSpeed()
	{
		var world = World();
		var people = new ParkPeople( world, new ParkBalance( Theme, easyMode: true ), null, new ParkState( world ),
			random: new System.Random( 1 ) );

		EnterPark();

		var arrival = people.Guests[people.Admit( 55, 30 )];
		var settled = people.Peeps.First( peep => peep.ThingId == 42 );
		var saved = settled.Navigator.MaxSpeed;

		Assert.AreEqual( 13107, arrival.Navigator.MaxSpeed, "made with the mover's speed of one" );

		Sweep( people );

		int[] first = [2785, 3440, 4096, 4751, 5406];

		Assert.AreEqual( first[System.Array.IndexOf( Peep.BaseSpeeds, arrival.BaseSpeed )], arrival.Navigator.MaxSpeed,
			$"a quarter of ({arrival.BaseSpeed} + 25) / 100" );
		Assert.AreEqual( saved, settled.Navigator.MaxSpeed, "thing 42 is saved settled at 1.2" );
	}

	/// <summary>
	/// <b>Every sweep stamps everybody where they stood as it began</b>, guests and staff, walking or not, and
	/// nothing stamps anybody between sweeps. So somebody who stops is drawn in one place, and a guest put down
	/// at a ride's exit is drawn there rather than slid across from where they boarded.
	///
	/// <para>
	/// The original stamps previous := current in <c>FUN_004fa870</c>, the first call of every person kind's
	/// tick handler (<c>0x00501658</c> for a guest, <c>0x00505495</c> for staff), whatever state they are in,
	/// and a put-down stamps again at <c>0x004fa95d</c>. <c>ParkGuestPlacementTests.AGuestWhoHasStoppedIsDrawnInOnePlace</c>
	/// pins what the drawing makes of a stamp; this pins that the park makes it.
	/// </para>
	/// <para>
	/// The run is the shipped park with its rides' scripts wired and its balance files read. Every 2,000th frame
	/// is a hitch that carries two or three sweeps, and once, straight after a sweep that moved them, one guest
	/// and one member of staff are left with no route, as a plan that fails leaves somebody. The behaviours
	/// draw from unseeded generators, so the counts differ run to run: each guard asks only that its case
	/// happened. Stops happen hundreds of times a run, stamps inside a hitch nearly two hundred, and put-downs
	/// about twenty; the test prints them. <b>Mutations:</b> deleting the
	/// stamp from either loop of <see cref="ParkPeople"/>'s update; moving either into <see cref="PeepWalk.Step"/>,
	/// after the turn, to every game tick, to every frame, or to the first sweep of a frame; stamping only
	/// those with a route; deleting the put-down's stamp; and moving <see cref="ParkPeople.ThingTickFraction"/>
	/// off the sweep, each fails an assertion here.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EverySweepStampsEverybodyWhereTheyStoodAsItBegan()
	{
		var world = World();
		var state = new ParkState( world );
		var catalogue = new ParkItemCatalogue( Theme, data );
		var rides = new ParkRides( Theme, world, catalogue, data );

		var people = new ParkPeople( world, new ParkBalance( Theme, easyMode: true ),
			() => ParkRides.GateIsOpen, state, catalogue,
			thingId => rides.Scheduler.Find( rides.ScriptFor( thingId ) ) );

		// Long enough for 17 or 18 ticks, so the frame carries two or three sweeps.
		const float Hitch = 0.55f;

		// The middle of the map's corner cell, which no path reaches.
		var nowhere = new FixedVector( PeepNavigator.WaypointCentre( 0 ), PeepNavigator.WaypointCentre( 0 ) );

		try
		{
			EnterPark();

			int sweeps = 0, moved = 0, guestsStopped = 0, staffStopped = 0, putDown = 0, restampedInAHitch = 0;
			string? guestLost = null, staffLost = null;
			var movedLastSweep = new HashSet<PeepNavigator>();

			// What each person's previous position must now be: set by each sweep, held between them.
			var stamped = new Dictionary<PeepNavigator, FixedVector>();

			for ( var frame = 0; frame < 40000; ++frame )
			{
				Frame( frame % 2000 == 1999 ? Hitch : AFrame );
				rides.Update();

				// Where everybody stands as this frame's first sweep, if it has one, begins.
				var stood = people.Peeps
					.Select( peep => (peep.Navigator, At: peep.Navigator.Position, Riding: peep.State == PeepState.Riding,
						Guest: (Peep?)peep, Id: peep.ThingId, Who: $"guest {peep.ThingId}") )
					.Concat( people.Staff.Select( member => (member.Navigator, At: member.Navigator.Position,
						Riding: false, Guest: (Peep?)null, Id: member.ThingId, Who: $"staff {member.ThingId}") ) )
					.ToList();

				var ticks = GameClock.TicksDue;

				people.Update();

				var sweptTimes = Enumerable.Range( 0, ticks )
					.Count( i => ((GameClock.Ticks - ticks + 1 + i) & (ParkPeople.ThingTickEvery - 1)) == 0 );

				if ( sweptTimes > 0 )
					++sweeps;

				// The drawing's fraction starts again on the tick that stamps, so a frame whose one tick swept is
				// less than a tick into it.
				if ( ticks == 1 && sweptTimes == 1 )
				{
					Assert.IsTrue( ParkPeople.ThingTickFraction < 1f / ParkPeople.ThingTickEvery,
						$"frame {frame} swept, and the drawing is {ParkPeople.ThingTickFraction:0.000} of the way through" );
				}

				var here = people.Peeps.Select( peep => peep.Navigator )
					.Concat( people.Staff.Select( member => member.Navigator ) )
					.ToHashSet();

				foreach ( var (navigator, at, riding, guest, id, who) in stood )
				{
					if ( !here.Contains( navigator ) )
						continue;

					if ( !stamped.TryGetValue( navigator, out var previous ) )
					{
						// First seen: arrived since the last frame, stamped however they arrived.
						stamped[navigator] = navigator.Previous;
						continue;
					}

					var walked = navigator.Position != at;

					if ( sweptTimes == 0 )
					{
						Assert.AreEqual( previous, navigator.Previous, $"{who} on frame {frame}, between sweeps, was stamped" );
						continue;
					}

					if ( sweptTimes > 1 )
					{
						// Stamped where the frame's last sweep found them, which is not seen from here; somebody who
						// moved in an earlier sweep of the frame is stamped somewhere between the two.
						if ( navigator.Previous != at && navigator.Previous != navigator.Position )
							++restampedInAHitch;

						stamped[navigator] = navigator.Previous;
						movedLastSweep.Remove( navigator );
						continue;
					}

					if ( riding && walked && guest!.State == PeepState.LeavingRide )
					{
						Assert.AreEqual( navigator.Position, navigator.Previous,
							$"{who} was put down at the exit on frame {frame}, so is drawn from where they were put" );

						++putDown;
					}
					else
					{
						Assert.AreEqual( at, navigator.Previous,
							$"{who} on frame {frame}: the stamp is where they stood as the sweep began" );
					}

					stamped[navigator] = navigator.Previous;

					if ( walked )
					{
						++moved;
						movedLastSweep.Add( navigator );

						// Once each, after the first second: a person who moved this sweep loses their route, so the next
						// sweep stamps somebody who has none.
						if ( frame > 60 && guest is { } lost && guestLost == null && Peep.IsAWalkingState( lost.State ) )
							guestLost = LoseTheRoute( people.WalkFor( id ), navigator, who );
						else if ( frame > 60 && guest == null && staffLost == null )
							staffLost = LoseTheRoute( people.StaffWalkFor( id ), navigator, who );
					}
					else if ( movedLastSweep.Remove( navigator ) )
					{
						if ( guest != null )
							++guestsStopped;
						else
							++staffStopped;
					}
				}
			}

			var counts = $"{sweeps} sweeps, {moved} moves, {guestsStopped} guest stops, {staffStopped} staff stops, "
				+ $"{putDown} put-downs, {restampedInAHitch} stamped mid-hitch, lost routes: {guestLost}, {staffLost}";

			System.Console.WriteLine( $"stamps: {counts}" );

			Assert.IsTrue( moved > 0, $"nobody moved, so no stamp was asked for: {counts}" );
			Assert.IsTrue( guestsStopped > 0, $"no guest stopped after moving: {counts}" );
			Assert.IsTrue( staffStopped > 0, $"no member of staff stopped after moving: {counts}" );
			Assert.IsTrue( putDown > 0, $"nobody was put down at a ride's exit: {counts}" );
			Assert.IsTrue( restampedInAHitch > 0, $"no hitch stamped anybody between its sweeps: {counts}" );
			Assert.IsNotNull( guestLost, $"no guest was left without a route: {counts}" );
			Assert.IsNotNull( staffLost, $"no member of staff was left without a route: {counts}" );
		}
		finally
		{
			people.Delete();
			rides.Delete();
			Entity.ApplyDeletions();
			ParkState.ForgetCurrent();
		}

		// Sends them somewhere no path reaches: the plan fails and leaves them standing with no route.
		string LoseTheRoute( PeepWalk? walk, PeepNavigator navigator, string who )
		{
			Assert.IsNotNull( walk, $"{who} walks" );

			navigator.Target = nowhere;

			Assert.IsFalse( walk!.PlanRoute(), $"{who} found a route to the map's corner" );
			Assert.IsFalse( walk.HasRoute, $"{who} kept a route after a failed plan" );

			return who;
		}
	}

	/// <summary>
	/// And the drawing reads that moved position rather than the saved one - the same lookup
	/// <c>ParkGuestSprites.OnRenderTranslucent</c> does, by thing id, through the live pool.
	/// </summary>
	[TestMethod]
	public void AfterTickingThePoolHandsTheDrawingALiveWalk()
	{
		var world = World();
		var people = new ParkPeople( world );

		try
		{
			EnterPark();

			for ( var frame = 0; frame < 120; ++frame )
			{
				Frame( AFrame );
				people.Update();
			}

			var person = world.People.Single( p => p.ThingId == 42 );
			var sprite = world.Sprites.Single( s => s.Slot == person.SpriteSlot );
			var walk = people.WalkFor( person.ThingId );

			Assert.IsNotNull( walk, "the pool knows this guest by the id the renderer looks them up by" );

			var (x, y, _) = ParkGuestSprites.Standing( walk, 10f, 10f, person, sprite );

			Assert.AreNotEqual( sprite.X, x, "the drawing takes the walked position, not the saved one" );
			Assert.AreNotEqual( sprite.Y, y );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>Every walk onto a ride in the shipped park is noticed within one tick of coming due</b> (Q183).
	///
	/// <para>
	/// At a sixtieth of a second a frame runs at most one tick, so the tick a slot changes in is known to the
	/// millisecond: its instant is <see cref="GameClock.Ticks"/> × 31. A slot first seen walking was started on
	/// that instant, so it is due that plus its leg, and the tick it is first seen arrived must be no earlier than
	/// that and less than a tick later. Three guests are sent to the Jungle Spray, whose legs (1100, 700, 1100 ms) come
	/// due 16, 13 and 16 ms before a tick. They never walk off here: the lane's clip has no player without the
	/// drawn park, so <c>GETANIM_CH</c> never lets them go, and a walk off is checked in
	/// <see cref="RideScriptWalkTests.AFinishedWalkOffKeepsTheLegItWalked"/>. <b>Mutations:</b> stepping the walks
	/// in each script's own turn instead (up to eight ticks late) and not stepping them from the park's tick at all
	/// (nobody arrives) each fail an assertion here.
	/// </para>
	/// </summary>
	[TestMethod]
	public void EveryWalkIsNoticedWithinOneTickOfComingDue()
	{
		var world = World();
		var state = new ParkState( world );
		var catalogue = new ParkItemCatalogue( Theme, data );
		var rides = new ParkRides( Theme, world, catalogue, data );

		var people = new ParkPeople( world, new ParkBalance( Theme, easyMode: true ),
			() => ParkRides.GateIsOpen, state, catalogue,
			thingId => rides.Scheduler.Find( rides.ScriptFor( thingId ) ) );

		const float Tick = GameClock.TickSeconds * 1000f;

		// Per script and slot: the handle walking, the state last seen, and when its walk comes due.
		var seen = new Dictionary<(int Script, int Slot), (int Handle, RideScript.WalkState State, float Due, int? Leg)>();
		int arrivedOn = 0, arrivedOff = 0;
		var sent = new HashSet<int>();
		var sentArrived = new HashSet<int>();
		float latest = float.MinValue;

		try
		{
			EnterPark();

			// Three guests on the stock Jungle Spray (thing 14), its three lanes' legs 1100, 700 and 1100 each way,
			// as the console's admit and send make them (Q184): nobody chooses a walk-on ride in this run on their own.
			const int JungleSpray = 14;

			// Kind 3, as Q184's run: a kind 0 sent there goes on to another ride.
			for ( var i = 0; i < 3; ++i )
			{
				var guest = people.AdmitInside( 55, 30, 3 );
				Assert.AreNotEqual( 0, guest, "the park would not take a guest at (55,30)" );
				Assert.IsNull( people.SendAsChosen( guest, JungleSpray ), "the guest would not go to the Jungle Spray" );
				sent.Add( guest );
			}

			for ( var frame = 0; frame < 40000; ++frame )
			{
				Frame( AFrame );
				rides.Update();
				people.Update();

				Assert.IsTrue( GameClock.TicksDue <= 1, $"frame {frame} ran {GameClock.TicksDue} ticks" );

				if ( GameClock.TicksDue == 0 )
					continue;

				var instant = GameClock.Ticks * Tick;

				foreach ( var script in rides.Scheduler.Scripts )
				{
					foreach ( var walking in script.Walking() )
					{
						var key = (script.Id, walking.Slot);
						seen.TryGetValue( key, out var before );
						var same = before.Handle == walking.Handle && before.State != RideScript.WalkState.Free;

						if ( walking.State is RideScript.WalkState.WalkingOn or RideScript.WalkState.WalkingOff )
						{
							if ( !same || before.State != walking.State )
								seen[key] = (walking.Handle, walking.State, instant + walking.Leg!.Value, walking.Leg);

							continue;
						}

						if ( same && before.State is RideScript.WalkState.WalkingOn or RideScript.WalkState.WalkingOff
							&& before.State != walking.State )
						{
							var late = instant - before.Due;
							latest = Math.Max( latest, late );

							Assert.IsTrue( late >= 0f && late < Tick,
								$"script {script.Id} slot {walking.Slot}: {before.State} due {before.Due} noticed at {instant}, {late} ms after" );

							if ( walking.State == RideScript.WalkState.Done )
							{
								++arrivedOff;
								Assert.AreEqual( before.Leg, walking.Leg, "a finished walk off keeps the leg it walked" );
							}
							else
							{
								++arrivedOn;

								if ( sent.Contains( walking.Handle ) )
									sentArrived.Add( walking.Handle );
							}
						}

						seen[key] = (walking.Handle, walking.State, before.Due, walking.Leg);
					}
				}
			}

			Console.WriteLine( $"{arrivedOn} arrived on, {arrivedOff} arrived off, latest {latest} ms after due" );

			CollectionAssert.AreEquivalent( sent.ToList(), sentArrived.ToList(),
				"each of the three guests sent should have arrived on a lane" );
		}
		finally
		{
			people.Delete();
			rides.Delete();
			Entity.ApplyDeletions();
		}
	}
}
