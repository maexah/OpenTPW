using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
	public void MountTheGame() => data = GameData.Required();

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

			for ( var frame = 0; frame < frames; ++frame )
			{
				Frame( AFrame );
				ticked += GameClock.TicksDue;

				rides.Update();
				people.Update();

				foreach ( var peep in people.Peeps )
				{
					invited |= peep.BeenAdmitted;
					boarding |= peep.State is PeepState.BeingAdmitted or PeepState.EnteringRide;
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
				$"invited {string.Join( ",", nominators )} | queuedFor {string.Join( ",", queuedFor )}" );
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
			$"and that guest should have set off to board - {driven.Trace}" );

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
