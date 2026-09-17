using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// A guest's needs, one tick at a time - the body of the original's <c>FUN_00501650</c>.
///
/// <para>
/// These need no game files and no device: every constant the tick uses is hardcoded in the executable
/// rather than read from the balance file, so the whole thing is arithmetic and can be pinned exactly.
/// </para>
/// </summary>
[TestClass]
public class PeepNeedsTests
{
	/// <summary>
	/// A guest with nothing pressing, so that each test can move the one need it is about. Thing id 4
	/// puts them in tick slot 0, which is the only slot that ever sees the drift - see
	/// <see cref="OnlyAQuarterOfTheGuestsEverGetHungrierOnTheirOwn"/>.
	/// </summary>
	private static Peep Guest( int thingId = 4, float thirst = 10f, float hunger = 10f,
		float toilet = 10f, float illness = 0f, float happiness = 50f, int exitLevel = 100 )
		=> new( thingId, new ParkWorld.GuestState(
			State: 6, SavedState: 6, PersonType: 0, Cash: 300, ExitLevel: exitLevel,
			Happiness: happiness, Thirst: thirst, Hunger: hunger, Toilet: toilet, Illness: illness,
			Litter: 0f, MajorDest: 0, QueuePos: 0, PrankeryIndex: 0 ), StandingStill );

	/// <summary>
	/// A navigator for a guest who is going nowhere. Nothing in this file reads it - these tests are about
	/// needs - but a guest cannot be built without one, because the save never produces a person without
	/// one. Written out here rather than shared with the other peep tests: it is test data, and data is
	/// clearer at the point of use than behind a helper in another file.
	/// </summary>
	private static ParkWorld.NavigatorState StandingStill => new(
		X: 0, Y: 0, VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
		Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius,
		MaxForce: 0, MaxSpeed: 0, NavMode: 0, CantReachDest: 0, PathFinished: false,
		PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
		BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 );

	/// <summary>
	/// The save's numbers arrive intact. Worth its own test because everything below starts from them.
	/// </summary>
	[TestMethod]
	public void AGuestStartsFromTheStateTheSaveGaveThem()
	{
		var peep = Guest( thirst: 36f, hunger: 18f, toilet: 13f );

		Assert.AreEqual( PeepState.Deciding, peep.State );
		Assert.AreEqual( 36f, peep.Thirst );
		Assert.AreEqual( 18f, peep.Hunger );
		Assert.AreEqual( 13f, peep.Toilet );
		Assert.AreEqual( 50f, peep.Happiness );
	}

	/// <summary>
	/// Each guest takes one tick in four, chosen by their thing id, so the park's guests are spread
	/// across the four slots rather than all updating together.
	/// </summary>
	[TestMethod]
	public void AGuestTakesOneTickInFour()
	{
		for ( var id = 0; id < 8; ++id )
		{
			var peep = Guest( id );

			for ( var tick = 0; tick < 8; ++tick )
			{
				Assert.AreEqual( (id & 3) == (tick & 3), peep.DueOn( tick ),
					$"guest {id} on tick {tick}" );
			}
		}
	}

	/// <summary>
	/// The countdown to going home moves only on the guest's own tick, which is what makes it a count of
	/// their turns rather than of the park's.
	/// </summary>
	[TestMethod]
	public void TheCountdownHomeMovesOnlyOnTheGuestsOwnTick()
	{
		var peep = Guest( thingId: 5, exitLevel: 100 );

		for ( var tick = 1; tick <= 8; ++tick )
			peep.Tick( tick );

		// Ticks 1 and 5 are theirs out of the eight.
		Assert.AreEqual( 98, peep.ExitLevel );
	}

	/// <summary>
	/// The three needs that grow on their own, and by how much: the original subtracts -1 from toilet
	/// and -2 from hunger and thirst, which is growth.
	/// </summary>
	[TestMethod]
	public void ThreeNeedsGrowEverySixteenthTick()
	{
		var peep = Guest( thirst: 10f, hunger: 10f, toilet: 10f );

		peep.Tick( 16 );

		Assert.AreEqual( 11f, peep.Toilet, "toilet grows by one" );
		Assert.AreEqual( 12f, peep.Hunger, "hunger grows by two" );
		Assert.AreEqual( 12f, peep.Thirst, "thirst grows by two" );

		// A tick that is theirs but is not a sixteenth leaves them alone.
		peep.Tick( 20 );

		Assert.AreEqual( 11f, peep.Toilet, "toilet on a plain tick" );
		Assert.AreEqual( 12f, peep.Hunger, "hunger on a plain tick" );
		Assert.AreEqual( 12f, peep.Thirst, "thirst on a plain tick" );
	}

	/// <summary>
	/// <b>The quirk, reproduced rather than tidied away.</b> The drift is gated on the same counter as
	/// the tick itself, and sixteen is a multiple of four - so a sixteenth tick is always slot zero, and
	/// only guests whose id is a multiple of four are ever hungry on their own account. The other three
	/// quarters of the park change only through what happens to them.
	///
	/// <para>
	/// This is pinned because it looks exactly like a bug, and a later reader tempted to "fix" it would
	/// be changing the game rather than the port.
	/// </para>
	/// </summary>
	[TestMethod]
	public void OnlyAQuarterOfTheGuestsEverGetHungrierOnTheirOwn()
	{
		var moved = 0;

		for ( var id = 0; id < 8; ++id )
		{
			var peep = Guest( id );

			// A full sixteen ticks, so every guest gets four turns of their own.
			for ( var tick = 1; tick <= 16; ++tick )
				peep.Tick( tick );

			if ( peep.Hunger > 10f )
				++moved;
		}

		Assert.AreEqual( 2, moved, "of eight guests, only ids 0 and 4 should ever have grown hungry" );
	}

	/// <summary>
	/// A need at its maximum costs happiness every tick, and each of the four does it separately - so a
	/// thoroughly miserable guest loses four a tick rather than one.
	/// </summary>
	[TestMethod]
	public void EveryNeedAtItsMaximumTakesAnotherPointOfHappiness()
	{
		foreach ( var (name, peep) in new (string, Peep)[]
		{
			("illness", Guest( illness: 100f )),
			("hunger", Guest( hunger: 100f )),
			("thirst", Guest( thirst: 100f )),
			("toilet", Guest( toilet: 100f ))
		} )
		{
			peep.Tick( 4 );

			Assert.AreEqual( 49f, peep.Happiness, $"{name} at its maximum should cost one happiness" );
		}

		var wretched = Guest( thirst: 100f, hunger: 100f, toilet: 100f, illness: 100f );

		wretched.Tick( 4 );

		Assert.AreEqual( 46f, wretched.Happiness, "all four at once should cost four" );
	}

	/// <summary>
	/// A need short of its maximum costs nothing, which is what stops the test above from passing for
	/// the wrong reason: the original truncates to an integer before comparing with 100.
	/// </summary>
	[TestMethod]
	public void ANeedShortOfItsMaximumCostsNothing()
	{
		var peep = Guest( toilet: 99f );

		peep.Tick( 4 );

		Assert.AreEqual( 50f, peep.Happiness, "ninety-nine is not a hundred" );
	}

	/// <summary>
	/// Needs are held between nothing and a hundred however hard they are pushed - the clamp every
	/// change in the original funnels through.
	/// </summary>
	[TestMethod]
	public void ANeedIsHeldBetweenNothingAndAHundred()
	{
		Assert.AreEqual( 100f, Peep.Change( 99f, 5f ), "pushed over the top" );
		Assert.AreEqual( 0f, Peep.Change( 1f, -5f ), "pushed under the bottom" );
		Assert.AreEqual( 50f, Peep.Change( 48f, 2f ), "left alone in between" );

		// And through the tick: a guest already at the top does not go past it.
		var peep = Guest( toilet: 100f, hunger: 100f, thirst: 100f );

		peep.Tick( 16 );

		Assert.AreEqual( 100f, peep.Toilet );
		Assert.AreEqual( 100f, peep.Hunger );
		Assert.AreEqual( 100f, peep.Thirst );
	}

	/// <summary>
	/// Being desperate for the toilet is what makes a guest hurry, and eighty is the line. Nothing else
	/// sets it: a guest starving or ill walks at their normal pace.
	/// </summary>
	[TestMethod]
	public void BeingDesperateForTheToiletIsWhatMakesAGuestHurry()
	{
		var hurrying = Guest( toilet: 81f );

		hurrying.Tick( 4 );

		Assert.AreEqual( Peep.HurryingSpeed, hurrying.PurposeSpeed, "past eighty they hurry" );

		var strolling = Guest( toilet: 80f, hunger: 100f, illness: 100f );

		strolling.Tick( 4 );

		Assert.AreEqual( Peep.UnhurriedSpeed, strolling.PurposeSpeed,
			"eighty exactly is not past eighty, and nothing else makes a guest hurry" );
	}

	/// <summary>
	/// A tick that is not the guest's own changes nothing at all, which is the guard the rest of these
	/// rest on.
	/// </summary>
	[TestMethod]
	public void ATickThatIsNotTheirsChangesNothing()
	{
		var peep = Guest( thingId: 1, toilet: 100f, exitLevel: 50 );

		peep.Tick( 16 );

		Assert.AreEqual( 50, peep.ExitLevel );
		Assert.AreEqual( 100f, peep.Toilet );
		Assert.AreEqual( 50f, peep.Happiness );
	}
}
