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
