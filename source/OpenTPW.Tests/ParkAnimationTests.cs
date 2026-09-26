using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The seam between the simulation and the picture: does ticking a real park actually advance what its
/// guests look like, and does the drawing read the advanced picture rather than the saved one.
///
/// <para>
/// <b>This file exists for the same reason <see cref="ParkTickTests"/> does.</b> A test that drives
/// <see cref="PeepWalk"/> directly passes while a real park sits motionless, when the fault is in the wiring
/// no test touches. The animation carries exactly the same risk:
/// <see cref="SpriteScriptTests"/> drives the player directly and would pass just as happily if nothing ever
/// called it. So these drive the wiring - a real park, a real clock, the pool the renderer asks.
/// </para>
/// <para>
/// <b>And two of them measure a PERIOD rather than a change</b>: asserting that a picture moved passes at
/// any speed whatever, a guest moving eight times too fast included.
/// </para>
/// </summary>
[TestClass]
public class ParkAnimationTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	/// <summary>
	/// Ticks spent letting the park settle before the sprite system's cadence is measured.
	///
	/// <para>
	/// <b>There is a real transient at the start and it is worth writing down.</b> Every sprite begins on the
	/// 62ms interval the original's constructor writes, and the due test is a strict comparison - so the
	/// first turn sets the next deadline exactly one turn away and nobody is due on it. A park's first two
	/// turns really are four game ticks apart, and only once the walk has driven somebody's interval down to
	/// one does the system settle to a turn every other tick. This test is named for the settled cadence, so
	/// it skips the transient deliberately rather than widening what it will accept.
	/// </para>
	/// </summary>
	private const int WarmUp = 16;

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
	/// Every guest and what they are playing, for a failure message.
	///
	/// <para>
	/// A bare count does not say which guest was measured - a guest whose walk never runs, or the one guest in
	/// the park who is standing still and therefore shows one picture for ever. What a failure here needs to say
	/// is who everybody is.
	/// </para>
	/// </summary>
	private static string Everyone( ParkPeople people )
		=> string.Join( "\n   ", people.Peeps.Select( peep =>
		{
			var playing = people.SpriteFor( peep.ThingId )!;
			var walk = people.WalkFor( peep.ThingId );

			return $"thing {peep.ThingId,2} {peep.State,-20} "
				+ $"{(Peep.IsAWalkingState( peep.State ) ? "walks" : "still")} "
				+ $"script {playing.Script,4} pc {playing.Pc,4} set {playing.Set} frame {playing.Frame} "
				+ $"every {playing.Interval,3}ms route {(walk?.HasRoute == true ? "yes" : "no ")}";
		} ) );

	/// <summary>
	/// <b>Ticking a park advances its guests' pictures.</b>
	///
	/// <para>
	/// <b>It watches every turn rather than comparing the two ends.</b> A before-and-after finds guests who
	/// seem not to have moved at all - sixty-four game ticks is thirty-two turns of the sprite system, the walk
	/// is eight pictures long, and thirty-two divides by eight exactly, so every guest animating steadily is
	/// back on the picture they started on. <b>Sampling at a multiple of the cycle you are measuring shows a
	/// still park however fast it is running.</b>
	/// </para>
	/// <para>
	/// So it counts the distinct pictures each guest is seen on and asks for <b>all eight</b> from anyone
	/// still walking at the end - an exact number rather than a threshold, because over thirty-two turns a
	/// walker comes round the whole cycle at least twice. A guest who is standing shows one picture for ever,
	/// which is correct rather than a fault, so they are not asked the same question.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TickingTheParkAdvancesTheGuestsPictures()
	{
		var world = World();
		var people = new ParkPeople( world );

		try
		{
			EnterPark();

			var seen = people.Peeps
				.Where( peep => people.SpriteFor( peep.ThingId ) != null )
				.ToDictionary( peep => peep.ThingId, _ => new HashSet<int>() );

			Assert.AreEqual( 13, seen.Count, "every guest in the shipped park has an animation" );

			for ( var tick = 0; tick < 64; ++tick )
			{
				Frame( GameClock.TickSeconds );
				people.Update();

				foreach ( var peep in people.Peeps )
					seen[peep.ThingId].Add( people.SpriteFor( peep.ThingId )!.Frame );
			}

			var walking = people.Peeps
				.Where( peep => people.SpriteFor( peep.ThingId )!.IsOn( (int)PeepAnimation.Walk )
					|| people.SpriteFor( peep.ThingId )!.IsOn( (int)PeepAnimation.HurriedWalk ) )
				.ToList();

			// Without this the loop below could assert nothing at all and still pass.
			Assert.IsTrue( walking.Count > 0,
				"somebody in the shipped park should still be walking after 64 ticks.\n   " + Everyone( people ) );

			// The interval the walk asks for really does arrive: somebody walking is off the 62 the sprite
			// constructor writes. Said of at least one rather than of all, because a guest whose step comes
			// out as no distance at all asks for no change, which is what the original's truncation does.
			Assert.IsTrue( walking.Any( peep => people.SpriteFor( peep.ThingId )!.Interval <= 2 ),
				"the walk's own arithmetic never reached any sprite.\n   " + Everyone( people ) );

			foreach ( var peep in walking )
			{
				Assert.AreEqual( 8, seen[peep.ThingId].Count,
					$"guest {peep.ThingId} was seen on {seen[peep.ThingId].Count} pictures: "
					+ string.Join( ", ", seen[peep.ThingId].OrderBy( frame => frame ) ) );
			}
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>The sprite system turns once every two game ticks, and this pins the two.</b>
	///
	/// <para>
	/// The park loop reaches it at <c>0054f5fb</c>, inside the same 31ms loop as everything else, but behind
	/// a test of its own at <c>0054f5d7</c> - <c>TEST AL,0x1</c> then <c>JNZ</c> straight past it.
	/// </para>
	/// <para>
	/// <b>It measures the gap, not only the parity - parity alone lets a gate four times too slow straight
	/// through.</b> "The picture moves only on even ticks" sounds like it pins the two and does not: a gate on
	/// every eighth tick fires on 8, 16, 24, every one of them even, and that assertion passes while the park
	/// animates at a quarter speed.
	/// </para>
	/// <para>
	/// <b>It watches the whole park rather than one guest.</b> Picking a
	/// guest gets you whichever one the file happens to list first - which here is walking but taking steps
	/// too short to ask for a rate, so it sits at the default interval and turns half as often. Picking the
	/// one guest nobody is driving gets you the one waiting for the gate, who is on the standing script and
	/// shows a single picture for ever. Neither is a fault; both make a liar of a test that names the system.
	/// So this asks when <i>anything</i> in the park moved, which is what the system's period actually means.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheSpriteSystemTurnsOnceEveryTwoGameTicks()
	{
		var world = World();
		var people = new ParkPeople( world );

		try
		{
			EnterPark();

			for ( var tick = 1; tick <= WarmUp; ++tick )
			{
				Frame( GameClock.TickSeconds );
				people.Update();
			}

			var frames = people.Peeps.ToDictionary(
				peep => peep.ThingId, peep => people.SpriteFor( peep.ThingId )!.Frame );

			var turnedOn = new List<int>();

			for ( var tick = WarmUp + 1; tick <= WarmUp + 80; ++tick )
			{
				Frame( GameClock.TickSeconds );
				people.Update();

				var moved = false;

				foreach ( var peep in people.Peeps )
				{
					var now = people.SpriteFor( peep.ThingId )!.Frame;

					if ( now == frames[peep.ThingId] )
						continue;

					frames[peep.ThingId] = now;
					moved = true;
				}

				if ( moved )
					turnedOn.Add( tick );
			}

			Assert.IsTrue( turnedOn.Count >= 8,
				$"the park's pictures moved on only {turnedOn.Count} of 80 ticks.\n   " + Everyone( people ) );

			// EVERY gap, rather than the smallest or the evenness: somebody in the park is always on an
			// interval of one, so a turn of the system always shows somewhere - and each turn must land
			// exactly one sprite tick after the last.
			for ( var i = 1; i < turnedOn.Count; ++i )
			{
				Assert.AreEqual( ParkPeople.SpriteTickEvery, turnedOn[i] - turnedOn[i - 1],
					$"turns {i} and {i + 1} are {turnedOn[i] - turnedOn[i - 1]} game ticks apart; the whole "
					+ "series was " + string.Join( ", ", turnedOn ) );
			}

			var odd = turnedOn.Where( tick => (tick & 1) != 0 ).ToList();

			Assert.AreEqual( 0, odd.Count,
				"the sprite system runs on even ticks only, but the park moved on "
				+ string.Join( ", ", odd ) );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>Every guest ends up on one of the three animations a guest can be on.</b> A guest who is walking is
	/// put on the walk; one who has arrived is put on the stand by the state they arrive in
	/// (<see cref="Peep.AnimationFor"/>). What must never happen is a guest left on some other script entirely.
	/// </summary>
	[TestMethod]
	public void WalkingGuestsAreOnAnAnimationAGuestCanBeOn()
	{
		var world = World();
		var people = new ParkPeople( world );

		try
		{
			EnterPark();

			for ( var tick = 0; tick < 64; ++tick )
			{
				Frame( GameClock.TickSeconds );
				people.Update();
			}

			var walking = people.Peeps
				.Where( peep => Peep.IsAWalkingState( peep.State ) )
				.Where( peep => people.WalkFor( peep.ThingId )?.HasRoute == true )
				.ToList();

			Assert.IsTrue( walking.Count > 0, "somebody in the shipped park should still be walking" );

			foreach ( var peep in walking )
			{
				var playing = people.SpriteFor( peep.ThingId )!;

				Assert.IsTrue( playing.IsOn( (int)PeepAnimation.Walk )
					|| playing.IsOn( (int)PeepAnimation.HurriedWalk )
					|| playing.IsOn( (int)PeepAnimation.Stand ),
					$"guest {peep.ThingId} is on script {playing.Script}, which is none of the three.\n   "
					+ Everyone( people ) );
			}
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>Nobody changes clothes.</b> The original packs the set and the bank offset into one word and its
	/// "choose a set" instruction writes the whole word, so running a script could in principle move a guest
	/// onto a different bank of art - which would look like every child in the park swapping outfits the
	/// instant they started walking, and would be invisible to every other test here.
	/// </summary>
	[TestMethod]
	public void WalkingNeverChangesWhichBankAGuestWears()
	{
		var world = World();
		var people = new ParkPeople( world );

		try
		{
			EnterPark();

			var slots = world.People.ToDictionary( person => person.ThingId, person => person.SpriteSlot );
			var art = world.Sprites.ToDictionary( sprite => sprite.Slot );

			var before = people.Peeps.ToDictionary(
				peep => peep.ThingId, peep => art[slots[peep.ThingId]].BankOffset );

			for ( var tick = 0; tick < 128; ++tick )
			{
				Frame( GameClock.TickSeconds );
				people.Update();
			}

			foreach ( var peep in people.Peeps )
			{
				var drawn = ParkGuestSprites.Showing(
					people.SpriteFor( peep.ThingId ), art[slots[peep.ThingId]] );

				Assert.AreEqual( before[peep.ThingId], drawn.BankOffset,
					$"guest {peep.ThingId} changed from bank offset {before[peep.ThingId]} to "
					+ $"{drawn.BankOffset} while walking" );
			}
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>And the drawing takes the picture the animation is on, not the one the file recorded.</b>
	///
	/// <para>
	/// <b>It watches throughout rather than at the end.</b> A single look after sixty-four ticks is aliasable:
	/// widening the sprite gate to every eighth tick gives a guest eight advances in that window, eight is the
	/// length of the walk, and every drawn picture lands back on the saved one. Counting every tick cannot be
	/// aliased by the period of anything.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheDrawingTakesTheLivePictureNotTheSavedOne()
	{
		var world = World();
		var people = new ParkPeople( world );

		try
		{
			EnterPark();

			var slots = world.People.ToDictionary( person => person.ThingId, person => person.SpriteSlot );
			var art = world.Sprites.ToDictionary( sprite => sprite.Slot );

			var differing = 0;

			for ( var tick = 0; tick < 64; ++tick )
			{
				Frame( GameClock.TickSeconds );
				people.Update();

				differing += people.Peeps.Count( peep =>
				{
					var saved = art[slots[peep.ThingId]];
					var drawn = ParkGuestSprites.Showing( people.SpriteFor( peep.ThingId ), saved );

					return drawn.Frame != saved.Frame || drawn.Set != saved.Set;
				} );
			}

			Assert.IsTrue( differing > 0,
				"at no point in two seconds did anybody's drawn picture differ from the saved one.\n   "
				+ Everyone( people ) );

			// And with no animation at all it is the saved one, so a park without a simulation still draws.
			var first = art[slots[people.Peeps[0].ThingId]];
			var still = ParkGuestSprites.Showing( null, first );

			Assert.AreEqual( first.Frame, still.Frame );
			Assert.AreEqual( first.Set, still.Set );
			Assert.AreEqual( first.BankOffset, still.BankOffset );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}

	/// <summary>
	/// <b>A walking guest's pictures go round in order, one at a time.</b>
	///
	/// <para>
	/// This is what pins the guard that stops the walk asking for an animation the guest is already playing.
	/// <c>FUN_004fa2a0</c> tests that with <c>FUN_00475c50</c> before it queues anything; without the test
	/// the walk asks on every tick, the queue applies it on the guest's next needs turn, and the script
	/// restarts from its first instruction - so the guest snaps back to the first picture from wherever they
	/// had got to.
	/// </para>
	/// <para>
	/// <b>Every other test in this file would still pass.</b> A guest who restarts mid-cycle still shows all
	/// eight pictures over sixty-four ticks, still advances on every turn of the system, and is still on the
	/// walking script. Only the ORDER gives it away - a loop steps from the last picture to the first, a
	/// restart steps to the first from anywhere. So this asserts the sequence rather than the set.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AWalkingGuestsPicturesGoRoundInOrder()
	{
		var world = World();
		var people = new ParkPeople( world );

		try
		{
			EnterPark();

			var walk = SpriteScript.EntryFor( SpriteScript.Walking );
			var last = new Dictionary<int, int>();
			var steps = 0;

			for ( var tick = 0; tick < 96; ++tick )
			{
				Frame( GameClock.TickSeconds );
				people.Update();

				foreach ( var peep in people.Peeps )
				{
					var playing = people.SpriteFor( peep.ThingId )!;

					// Only while they stay on the walking script. A guest who arrives is put on the standing
					// one, and that change of script is not a step round this cycle.
					if ( playing.Script != walk )
					{
						last.Remove( peep.ThingId );
						continue;
					}

					if ( last.TryGetValue( peep.ThingId, out var before ) && before != playing.Frame )
					{
						Assert.AreEqual( (before + 1) % 8, playing.Frame,
							$"guest {peep.ThingId} went from picture {before} to {playing.Frame} at tick "
							+ $"{tick}, which is not the next one round.\n   " + Everyone( people ) );

						++steps;
					}

					last[peep.ThingId] = playing.Frame;
				}
			}

			// Without this the loop above could have asserted nothing at all and still passed.
			Assert.IsTrue( steps >= 40,
				$"only {steps} steps round the cycle were seen, which is too few to mean anything" );
		}
		finally
		{
			people.Delete();
			Entity.ApplyDeletions();
		}
	}
}
