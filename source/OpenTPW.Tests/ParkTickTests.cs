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
/// <b>This file exists because the screen disagreed with the tests.</b> Every test written for the walk
/// drove <see cref="PeepWalk"/> directly - plan a route, call <c>Step</c>, watch the position change - and
/// all of them passed. Opening a real park showed nobody moving at all. So the fault was never in the walk;
/// it was somewhere in the wiring that no test touched, between <see cref="ParkPeople"/>, the clock and the
/// renderer. These tests drive that wiring instead of going round it.
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

	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>One frame, through both clocks, in the order <see cref="Level.Update"/> uses.</summary>
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

	/// <summary>A sixtieth of a second, which is about two frames to a 31ms tick.</summary>
	private const float AFrame = 1f / 60f;

	/// <summary>
	/// <b>Ticking a park moves its guests.</b> The test that was missing, and the one the screen was right
	/// about.
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
	/// The budget is in FRAMES and a thing tick is sixteen of them, so this is about two and a half
	/// thousand turns of the thing engine - comfortably past the six hundred
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

			// <b>EVERY state any guest was ever seen in, rather than a hand-picked pair.</b> Twice now a
			// disjunction has hidden a missing step - "BeingAdmitted or EnteringRide" could not see that
			// nothing set the second, and "EnteringRide or Riding" cannot see whether the chain stalls
			// before Riding. Collecting them all costs one set and cannot be wrong about which it omits.
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
					// <b>The far end of the chain, and NOT a disjunction - twice over, that is what hid a
					// missing step.</b> It read "BeingAdmitted or EnteringRide" while nothing set the
					// second, then "EnteringRide or Riding" while nothing reached the second; each passed
					// on its first half alone. A test that spans a step boundary cannot see the boundary.
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

				// <b>No early exit, and that is deliberate rather than an oversight.</b> This loop used to
				// stop as soon as invited, nominated and boarding were all true - which sounds harmless and
				// is not: the run ended after 430 of its ~2,688 thing ticks, because the ride reached those
				// three first, and the set of things that had called somebody forward was therefore [13]
				// alone. Read carelessly that says "only rides invite"; what it actually says is "I stopped
				// looking". An early exit inside a measuring loop truncates the very thing being measured.
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
	/// <b>Ticking a park takes a RIDE's turn, and until this nothing in the suite proved it.</b>
	///
	/// <para>
	/// The whole boarding chain was built and committed over several branches - invite, admit, complete,
	/// dismiss - and every one of its tests constructed <see cref="ParkRideOperation"/> by hand. <b>Not one
	/// of them ran it from a park</b>, and every <see cref="ParkPeople"/> in the suite was built without the
	/// script delegate, so <c>TakeTheRidesTurns</c> returned on its first line. The wiring landed and the
	/// test count did not move - which is exactly how <see cref="PeepState.GoingToRide"/> shipped with no
	/// case in the behaviour and six hundred and sixty-seven green tests failed to notice.
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
	/// <b>Every guest ages, not just the quarter whose id divides four.</b>
	///
	/// <para>
	/// <see cref="Peep.Tick"/> gives each guest one turn in four by <c>(id &amp; 3) == (tick &amp; 3)</c>,
	/// and the tick it counts must be the <b>thing</b> tick. Feed it the game tick and every value
	/// reaching it is a multiple of eight; eight divides four, so the test is true only for guests whose
	/// id divides four and the other three quarters never age again. There is a comment in
	/// <see cref="ParkPeople"/> warning about exactly that - and a control proved the comment was all
	/// there was: swapping the thing tick for the game tick broke <b>no test at all</b>.
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
	/// beat instead made every guest in the shipped park move <b>eight times too fast</b> - they crossed
	/// from the bus stop to the gate inside a second, which is what Alexah saw and no test did.
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
	/// <b>No script is bound here</b>, so the manager takes OpenTPW's no-script fallback and drops on the sweep that
	/// calls the load, where the original would summon the vehicle and wait for it to answer 2. The handshake with a
	/// bound bus is <see cref="TheBusIsHeldAtTheStopUntilTheSweepAfterItsLastGuest"/>.
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
			Assert.AreEqual( call, drop, "with no vehicle to wait for, its one guest comes on the sweep that calls it" );
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
	/// </remarks>
	[TestMethod]
	public void TheBusIsHeldAtTheStopUntilTheSweepAfterItsLastGuest()
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
			thingId => thingId == busThing ? bus : rides.Scheduler.Find( rides.ScriptFor( thingId ) ) );

		try
		{
			Assert.IsNotNull( bus, $"the bus, thing {busThing}, should run {ParkRides.ScriptPathFor( busItem )}" );

			EnterPark();

			Assert.IsTrue( bus.Set( "VAR_STATUS", 0 ) && bus.Set( "VAR_TRIGGER", 0 ),
				"bus.RSE declares both variables the handshake turns on" );

			var newest = people.Peeps.Max( peep => peep.ThingId );

			for ( var sweep = 0; sweep < 600 && !people.LoadHeld; ++sweep )
				Sweep( people );

			Assert.AreEqual( 1264, people.State.GameTick, "the load is called on mGameTick 1264" );
			Assert.AreEqual( 1, bus["VAR_TRIGGER"], "and the waiting bus summoned on the same sweep" );
			Assert.AreEqual( newest, people.Peeps.Max( peep => peep.ThingId ), "nobody is dropped before it answers 2" );

			bus.Set( "VAR_TRIGGER", 0 );
			bus.Set( "VAR_STATUS", 2 );
			Sweep( people );

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
	/// <see cref="ParkPeople.ThingTickFraction"/> to one fails this test and nothing else.
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
}
