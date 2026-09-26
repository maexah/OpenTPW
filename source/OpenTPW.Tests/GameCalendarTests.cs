using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The park's calendar: how long a day is, that only ticks move it, and that a season is always one of
/// the four rows the balance file declares. Driven as a park's frame drives it -
/// <see cref="Time.Update"/> from the renderer, then <see cref="GameClock.Update"/> and
/// <see cref="GameCalendar.Update"/> from <see cref="Level.Update"/>.
/// </summary>
[TestClass]
public class GameCalendarTests
{
	[TestInitialize]
	public void Setup()
	{
		Time.Paused = false;
		Time.StepFrames = 0;
		Start();
	}

	/// <summary>One frame, through all three clocks, in the order a park's update runs them.</summary>
	private static void Frame( float seconds, bool paused = false )
	{
		Time.Update( seconds );
		GameClock.Update( paused, GameClock.ParkCatchUp );
		GameCalendar.Update();
	}

	/// <summary>Entering a park, and the one frame <see cref="GameClock.Rebase"/> throws away with it.</summary>
	private static void Start()
	{
		GameClock.Rebase();
		GameCalendar.Rebase();
		Frame( 0f );
	}

	/// <summary>
	/// Runs frames until <paramref name="stop"/> is true, or gives up. A park frame at sixty is the
	/// ordinary case; nothing here wants the catch-up path.
	/// </summary>
	private static bool RunUntil( Func<bool> stop, int mostFrames = 200000 )
	{
		for ( int frame = 0; frame < mostFrames; ++frame )
		{
			if ( stop() )
				return true;

			Frame( 1f / 60f );
		}

		return stop();
	}

	/// <summary>
	/// A day is a little under six seconds, and the exact figure is worth pinning because it is not the
	/// one the arithmetic suggests.
	///
	/// <para>
	/// 86,400 funny seconds at 15,000 a second reads as 5.76s, but the counter only advances every eighth
	/// 31ms tick - 248ms - while the conversion divides by four as though it arrived every 250ms. So the
	/// first day turns on the 24th advance, which is 192 ticks, and days average 23.04 advances from there.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ADayTurnsOnTheTwentyFourthAdvance()
	{
		Assert.AreEqual( 0, GameCalendar.Days );

		// GameClock.Ticks counts from when the process started and a re-base deliberately leaves it
		// alone, so the only meaningful figure is the difference across this test.
		var before = GameClock.Ticks;

		Assert.IsTrue( RunUntil( () => GameCalendar.Days >= 1 ), "the day never turned" );

		Assert.AreEqual( 1, GameCalendar.Days );
		Assert.AreEqual( 24, GameCalendar.Counter, "the day turned on the wrong advance" );

		// 24 advances of 8 ticks each.
		Assert.AreEqual( 24 * 8, GameClock.Ticks - before, "a day took the wrong number of ticks" );
	}

	/// <summary>
	/// Ten days take 231 advances rather than ten of the first day's 24: a day is 23.04 advances on
	/// average, so the tenth turns on the first advance past 230.4.
	/// </summary>
	[TestMethod]
	public void TheDaysKeepTheOriginalsSlightlyFastPace()
	{
		Assert.IsTrue( RunUntil( () => GameCalendar.Days >= 10 ), "ten days never passed" );

		Assert.AreEqual( 231, GameCalendar.Counter );
	}

	/// <summary>
	/// The day is an edge, not a count - the original compares the derived day against the one it stored
	/// and fires on the difference (0x004f8321), so <see cref="GameCalendar.DayRolled"/> is true for one
	/// update and not for the hundreds either side of it.
	/// </summary>
	[TestMethod]
	public void TheDayRollsOnceAndOnlyOnce()
	{
		var rolls = 0;

		for ( int frame = 0; frame < 4000; ++frame )
		{
			Frame( 1f / 60f );

			if ( GameCalendar.DayRolled )
				rolls++;
		}

		Assert.AreEqual( GameCalendar.Days, rolls, "the edge fired a different number of times than days passed" );
		Assert.IsTrue( rolls > 1, "not enough time passed to be worth asserting" );
	}

	/// <summary>
	/// A held park holds its date. Nothing here tests a paused flag: no ticks come due, so no advance
	/// happens, which is the whole of how the original does it too.
	/// </summary>
	[TestMethod]
	public void AHeldParkKeepsItsDate()
	{
		RunUntil( () => GameCalendar.Days >= 1 );

		var counter = GameCalendar.Counter;
		var now = GameCalendar.Now;

		for ( int frame = 0; frame < 600; ++frame )
			Frame( 1f / 60f, paused: true );

		Assert.AreEqual( counter, GameCalendar.Counter, "a held park advanced its clock" );
		Assert.AreEqual( now, GameCalendar.Now, "a held park aged" );
		Assert.IsFalse( GameCalendar.DayRolled );
	}

	/// <summary>
	/// The season is always one of the four rows the balance file declares, and over a year it is all
	/// four of them.
	///
	/// <para>
	/// This is the test that pins the one place the original is deliberately not copied. Its season is
	/// <c>wMonth / 3</c> on the raw 1-based month with no bounds check (0x004f87e7), so <b>December
	/// gives 4</b> - one past a four-entry table - and it reads the array's own element count and two
	/// scheduling fields as if they were weather numbers. Clamping is a divergence, and an intentional
	/// one; this says so in a way that fails if anyone ever "restores" the overrun.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ASeasonIsAlwaysOneOfTheFourTheBalanceFileDeclares()
	{
		var seen = new HashSet<int>();
		var months = new HashSet<int>();

		// A year is about thirty-five minutes of game time, so this runs on well past December.
		for ( int frame = 0; frame < 150000; ++frame )
		{
			Frame( 1f / 60f );

			seen.Add( GameCalendar.Season );
			months.Add( GameCalendar.Now.Month );

			if ( months.Count == 12 )
				break;
		}

		Assert.AreEqual( 12, months.Count, "the run was too short to reach every month" );

		Assert.IsTrue( seen.SetEquals( new[] { 0, 1, 2, 3 } ),
			$"seasons outside the four declared rows, or not all of them: {string.Join( ",", seen.Order() )}" );
	}

	/// <summary>Entering a park starts the date again, as the original zeroes its counter at park init.</summary>
	[TestMethod]
	public void EnteringAParkStartsTheDateAgain()
	{
		RunUntil( () => GameCalendar.Days >= 2 );

		Assert.IsTrue( GameCalendar.Counter > 0 );

		Start();

		Assert.AreEqual( 0, GameCalendar.Counter );
		Assert.AreEqual( 0, GameCalendar.Days );
		Assert.AreEqual( GameCalendar.Epoch, GameCalendar.Now );
	}
}
