using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The game's clock: the 31ms beat both of the original's loops run on, what a pause does to it, and
/// the two catch-up caps. Every test drives it as a frame does - <see cref="Time.Update"/>
/// from the renderer, then <see cref="GameClock.Update"/> from <see cref="Level.Update"/>.
/// </summary>
[TestClass]
public class GameClockTests
{
	[TestInitialize]
	public void Setup()
	{
		// Static, as the original's own clock is - so each test starts from a scene change.
		Time.Paused = false;
		Time.StepFrames = 0;
		Start();
	}

	/// <summary>One frame of a given length, through both clocks, in the order a frame runs them.</summary>
	private static void Frame( float seconds, bool paused = false, float catchUp = GameClock.LobbyCatchUp )
	{
		Time.Update( seconds );
		GameClock.Update( paused, catchUp );
	}

	/// <summary>
	/// A scene change, and the one frame it throws away with it.
	///
	/// <para>
	/// <see cref="GameClock.Rebase"/> discards the frame that follows it, because that frame is the one
	/// spanning the load - see <see cref="ASceneOwesNothingForTheTimeItSpentLoading"/>, which is the test
	/// that pins it. Every other test here wants an ordinary running clock rather than a scene entry, so
	/// each starts by spending that frame.
	/// </para>
	/// </summary>
	private static void Start()
	{
		GameClock.Rebase();
		Frame( 0f );
	}

	[TestMethod]
	public void TheBeatIsThirtyOneMillisecondsAtAnyFrameRate()
	{
		foreach ( var rate in new[] { 30f, 60f, 144f } )
		{
			Start();

			var before = GameClock.Ticks;

			for ( int frame = 0; frame < (int)rate; ++frame )
				Frame( 1f / rate );

			// A second of game time is 1 / 0.031 = 32.25 ticks, so 32 of them, and the leftover is
			// carried rather than dropped - which is the whole reason the beat holds at any rate.
			Assert.AreEqual( 32, GameClock.Ticks - before, $"{rate}fps" );
		}
	}

	[TestMethod]
	public void APausedClockRunsNoTicksAndNoTimeAtAll()
	{
		Frame( 0.5f );

		var ticks = GameClock.Ticks;
		var now = GameClock.Now;

		for ( int frame = 0; frame < 60; ++frame )
			Frame( 1f / 60f, paused: true );

		Assert.IsTrue( GameClock.Paused );
		Assert.AreEqual( 0f, GameClock.Delta );
		Assert.AreEqual( 0, GameClock.TicksDue );
		Assert.AreEqual( ticks, GameClock.Ticks, "a held world ran a tick" );
		Assert.AreEqual( now, GameClock.Now, "a held world aged" );

		// And it picks up where it left off rather than owing a second of arrears.
		Frame( 1f / 60f );
		Assert.AreEqual( 0, GameClock.TicksDue );
	}

	[TestMethod]
	public void AParkWorksThroughFourTimesTheLobbysArrears()
	{
		// A frame of a second and a half - a stall, an alt-tab - against each scene's cap.
		Start();
		Frame( 1.5f, catchUp: GameClock.LobbyCatchUp );
		var lobby = GameClock.TicksDue;

		Start();
		Frame( 1.5f, catchUp: GameClock.ParkCatchUp );
		var park = GameClock.TicksDue;

		// 0.5 / 0.031 = 16.1, and the park is not capped at all here because 1.5 is inside its 2.
		Assert.AreEqual( 16, lobby );
		Assert.AreEqual( 48, park );
	}

	/// <summary>
	/// A scene is not billed for the time it spent loading. The frame that spans a load is the longest
	/// one the game ever sees - eight seconds of park - and without the re-base the first frame back
	/// would run a whole capful of ticks at once: 64 in a park, and 16 in a lobby.
	/// </summary>
	[TestMethod]
	public void ASceneOwesNothingForTheTimeItSpentLoading()
	{
		GameClock.Rebase();

		// The frame that spans the load.
		Frame( 8f, catchUp: GameClock.ParkCatchUp );
		Assert.AreEqual( 0, GameClock.TicksDue, "the new scene was billed for its own loading" );

		// And it is only that one frame - the next is an ordinary one again.
		Frame( 1f / 60f, catchUp: GameClock.ParkCatchUp );
		Assert.AreEqual( 0, GameClock.TicksDue );

		Frame( 0.5f, catchUp: GameClock.ParkCatchUp );
		Assert.AreEqual( 16, GameClock.TicksDue, "the clock did not start again after the re-base" );
	}

	/// <summary>
	/// The cap has to act on the <i>unclamped</i> frame or it can never be reached: <see cref="Time.Delta"/>
	/// is held to a tenth of a second, so a clock counting ticks from that would treat a two-second stall
	/// as 100ms and neither cap would ever mean anything.
	/// </summary>
	[TestMethod]
	public void ALongFrameIsBoundedByTheCapAndNotByTheFrameClamp()
	{
		Frame( 2.5f, catchUp: GameClock.ParkCatchUp );

		// Capped at two seconds: 2 / 0.031 = 64.5.
		Assert.AreEqual( 64, GameClock.TicksDue );

		// Where the frame clamp, had it been what counted, would have allowed three.
		Assert.AreEqual( 0.1f, Time.Delta, 0.0001f );
	}

	/// <summary>
	/// The sub-tick remainder, which is what a park's drawing interpolates with.
	///
	/// <para>
	/// <b>It is a fraction of ONE tick and not of anything else.</b> Composing it with the eight-tick
	/// thing beat belongs to <see cref="ParkPeople.ThingTickFraction"/>, because the original keeps a
	/// separate baseline and a separate reciprocal per rate - 1/31, 1/62 and 1/248 - rather than one
	/// fraction everything divides down.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ThePartialTickIsWhateverHasNotComeDueYet()
	{
		Start();

		// Half a tick: none comes due, and the whole of it is still owed.
		Frame( GameClock.TickSeconds / 2f );
		Assert.AreEqual( 0, GameClock.TicksDue );
		Assert.AreEqual( 0.5f, GameClock.PartialTick, 0.02f );

		// The other half: one comes due, and next to nothing is left owed.
		Frame( GameClock.TickSeconds / 2f );
		Assert.AreEqual( 1, GameClock.TicksDue );
		Assert.IsTrue( GameClock.PartialTick < 0.05f, $"{GameClock.PartialTick} was left owed" );

		// And it can never reach one, because a whole tick's worth comes due instead of being owed.
		for ( var frame = 0; frame < 200; ++frame )
		{
			Frame( 1f / 60f );

			Assert.IsTrue( GameClock.PartialTick is >= 0f and < 1f,
				$"the partial tick left its range at {GameClock.PartialTick}" );
		}
	}

	/// <summary>
	/// A held world holds its interpolation too - nothing is owed while paused, so a paused park stops
	/// mid-stride rather than snapping anybody to where the simulation last left them.
	/// </summary>
	[TestMethod]
	public void APausedClockDoesNotAdvanceThePartialTick()
	{
		Frame( GameClock.TickSeconds / 2f );

		var held = GameClock.PartialTick;

		for ( var frame = 0; frame < 30; ++frame )
			Frame( 1f / 60f, paused: true );

		Assert.AreEqual( held, GameClock.PartialTick, 0.0001f, "a held clock moved the interpolation" );
	}
}
