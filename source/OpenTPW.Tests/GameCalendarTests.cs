using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The park's calendar: the date of the park's own clock, <c>mGameTick</c>, and the day's, the month's and the year's
/// edges against the date it last compared, as <c>Calendar_Update</c> (<c>0x004f8260</c>) makes them at the tail of a
/// sweep. Driven as <see cref="ParkLoop"/> drives it, through <see cref="Level.TurnTheCalendar"/>, with the park's tick
/// after each sweep.
/// </summary>
[TestClass]
public class GameCalendarTests
{
	/// <summary>Lost Kingdom's Easymode.TPWI: tick 755, its clock block's month 1 (February, nought-based) and day 2.</summary>
	private static readonly ParkWorld.CalendarBlock LostKingdom = new( 125911584000000000, 0, 1, 2, 15000 );

	[TestInitialize]
	public void Setup() => GameCalendar.Rebase();

	[TestCleanup]
	public void TearDown() => GameCalendar.Rebase();

	/// <summary>Sweeps the park's clock on from where the calendar last read it, one tick a frame, until <paramref name="stop"/>.</summary>
	private static bool SweepUntil( Func<bool> stop, int mostSweeps = 20000 )
	{
		for ( var sweep = 0; sweep < mostSweeps; ++sweep )
		{
			GameCalendar.Update( GameCalendar.Counter + 1 );

			if ( stop() )
				return true;
		}

		return false;
	}

	/// <summary>
	/// <b>Entering Lost Kingdom puts the date on the park's tick 755</b>, 2 February 2000 at 18:27:30 (755 × 15,000 / 4
	/// seconds), not on 1 January. The file's day 2 matches, so the first sweep turns nothing; the day turns on tick 761,
	/// the first past 3 February (760.32), and the month on tick 1383, the first past 1 March (1382.4), as the original's
	/// do (<c>docs/exe/ride-operation.md</c>, "The month's change").
	/// </summary>
	/// <remarks><b>Mutations:</b> the calendar started from nought; the file's month or day not read; the edges compared
	/// against the date before the sweep.</remarks>
	[TestMethod]
	public void LostKingdomsDateIsTheDateOfItsTick755()
	{
		GameCalendar.Rebase( 755, LostKingdom );

		Assert.AreEqual( new DateTime( 2000, 2, 2, 18, 27, 30 ), GameCalendar.Now );
		Assert.AreEqual( 755, GameCalendar.Counter );

		GameCalendar.Update( 756 );
		Assert.AreEqual( (false, false, false), (GameCalendar.DayRolled, GameCalendar.MonthRolled, GameCalendar.YearRolled),
			"the file's own date turns nothing on the first sweep" );

		Assert.IsTrue( SweepUntil( () => GameCalendar.DayRolled ) );
		Assert.AreEqual( (761, 1), (GameCalendar.Counter, GameCalendar.Days) );

		Assert.IsTrue( SweepUntil( () => GameCalendar.MonthRolled ) );
		Assert.AreEqual( 1383, GameCalendar.Counter );
		Assert.AreEqual( new DateTime( 2000, 3, 1 ), GameCalendar.Now.Date );
		Assert.IsTrue( GameCalendar.DayRolled );
		Assert.IsFalse( GameCalendar.YearRolled );
	}

	/// <summary>
	/// <b>A park with no file turns a day on its first sweep</b> and nothing else: the clock's constructor leaves the day at
	/// -1 (<c>0x004f7ebd</c>), park init seeds the month and year from tick nought (<c>FUN_004f85a0</c>). The second day
	/// turns on the 24th tick, 86,400 × 4 / 15,000 being 23.04: the tick is fed every 248 ms while the conversion divides
	/// by four, so a day is a little under six seconds.
	/// </summary>
	/// <remarks><b>Mutations:</b> the day seeded 1 rather than -1; the month seeded off by one.</remarks>
	[TestMethod]
	public void AParkWithNoFileTurnsADayOnItsFirstSweep()
	{
		Assert.AreEqual( GameCalendar.Epoch, GameCalendar.Now );

		GameCalendar.Update( 1 );
		Assert.AreEqual( (true, false, false, 1), (GameCalendar.DayRolled, GameCalendar.MonthRolled, GameCalendar.YearRolled,
			GameCalendar.Days) );

		Assert.IsTrue( SweepUntil( () => GameCalendar.DayRolled ) );
		Assert.AreEqual( (24, 2), (GameCalendar.Counter, GameCalendar.Days) );
		Assert.AreEqual( new DateTime( 2000, 1, 2 ), GameCalendar.Now.Date );
	}

	/// <summary>Ten days on: tick 230 is still 10 January, and tick 231 is the first of 11 January, with the day rolled.</summary>
	[TestMethod]
	public void TickTwoHundredAndThirtyOneIsTheFirstOfElevenJanuary()
	{
		GameCalendar.Update( 230 );
		Assert.AreEqual( new DateTime( 2000, 1, 10 ), GameCalendar.Now.Date );

		GameCalendar.Update( 231 );
		Assert.AreEqual( new DateTime( 2000, 1, 11 ), GameCalendar.Now.Date );
		Assert.IsTrue( GameCalendar.DayRolled );
	}

	/// <summary>The day is an edge, not a count: true for the one sweep it turns on and not the twenty-odd either side.</summary>
	[TestMethod]
	public void TheDayRollsOnceAndOnlyOnce()
	{
		var rolls = 0;

		for ( var tick = 1; tick <= 1000; ++tick )
		{
			GameCalendar.Update( tick );
			rolls += GameCalendar.DayRolled ? 1 : 0;
		}

		Assert.AreEqual( GameCalendar.Days, rolls );
		Assert.AreEqual( 44, rolls, "tick 1000 is 13 February: 1 January's turn on the first sweep and 43 more" );
	}

	/// <summary>
	/// A frame with no sweep compares nothing and a held park sweeps nothing, so its date stands and no edge fires, the
	/// same tick read again.
	/// </summary>
	/// <remarks><b>Mutations:</b> the edges left standing from the last sweep.</remarks>
	[TestMethod]
	public void AFrameWithNoSweepKeepsTheDate()
	{
		GameCalendar.Rebase( 755, LostKingdom );
		Assert.IsTrue( SweepUntil( () => GameCalendar.DayRolled ) );

		var now = GameCalendar.Now;
		GameCalendar.Update( GameCalendar.Counter );

		Assert.AreEqual( now, GameCalendar.Now );
		Assert.IsFalse( GameCalendar.DayRolled, "an edge is for one update only" );
	}

	/// <summary>
	/// The season is always one of the four rows the balance file declares, and over a year it is all four of them.
	/// The original's is <c>wMonth / 3</c> on the raw 1-based month with no bounds check (0x004f87e7), so December gives
	/// 4, one past a four-entry table; clamping is a deliberate divergence, and this fails if the overrun comes back.
	/// </summary>
	[TestMethod]
	public void ASeasonIsAlwaysOneOfTheFourTheBalanceFileDeclares()
	{
		var seen = new HashSet<int>();
		var months = new HashSet<int>();

		for ( var tick = 1; tick < 9000 && months.Count < 12; tick += 7 )
		{
			GameCalendar.Update( tick );
			seen.Add( GameCalendar.Season );
			months.Add( GameCalendar.Now.Month );
		}

		Assert.AreEqual( 12, months.Count, "the run was too short to reach every month" );
		Assert.IsTrue( seen.SetEquals( new[] { 0, 1, 2, 3 } ),
			$"seasons outside the four declared rows, or not all of them: {string.Join( ",", seen.Order() )}" );
	}

	/// <summary>
	/// <b>The month and the year each turn on their own edge</b>, the calendar's second and third compares
	/// (messages <c>0xc</c> and <c>0xd</c>): the month on the sweep that reaches 1 February, with the day's, and the year
	/// on the one that reaches 2001, with both.
	/// </summary>
	[TestMethod]
	public void TheMonthAndTheYearTurnOnTheirOwnEdges()
	{
		var months = 0;

		Assert.IsTrue( SweepUntil( () => GameCalendar.MonthRolled ), "the month never turned" );
		Assert.AreEqual( (2000, 2, 1, true, false), (GameCalendar.Now.Year, GameCalendar.Now.Month, GameCalendar.Now.Day,
			GameCalendar.DayRolled, GameCalendar.YearRolled) );

		Assert.IsTrue( SweepUntil( () =>
		{
			months += GameCalendar.MonthRolled ? 1 : 0;
			return GameCalendar.YearRolled;
		} ), "the year never turned" );

		Assert.AreEqual( (2001, 1, 1, true, true), (GameCalendar.Now.Year, GameCalendar.Now.Month, GameCalendar.Now.Day,
			GameCalendar.DayRolled, GameCalendar.MonthRolled) );
		Assert.AreEqual( 11, months, "March to December, and January with the year" );
	}

	/// <summary>
	/// <b>A file saved in another year turns a year on its first sweep after entering</b>, its month and day matching:
	/// the load reads the month and the day but not the year, which keeps the entry's seed, 2000 (<c>Calendar_Serialise</c>).
	/// <b>Loaded over a running park in that year it turns nothing</b>, the original's load going into the running park,
	/// whose year it does not write.
	/// </summary>
	/// <remarks><b>Mutations:</b> the year seeded from the file's tick; the running year not carried.</remarks>
	[TestMethod]
	public void AFileFromAnotherYearTurnsTheYearOnlyWhenEnteredFromTheLobby()
	{
		// Tick 16972 is 6 January 2002.
		var saved = LostKingdom with { MonthAtLastUpdate = 0, DayAtLastUpdate = 6 };

		GameCalendar.Rebase( 16972, saved );
		GameCalendar.Update( 16973 );
		Assert.AreEqual( (false, false, true), (GameCalendar.DayRolled, GameCalendar.MonthRolled, GameCalendar.YearRolled) );

		GameCalendar.CarryTheYear();
		GameCalendar.Rebase( 16972, saved );
		GameCalendar.Update( 16973 );
		Assert.AreEqual( (false, false, false), (GameCalendar.DayRolled, GameCalendar.MonthRolled, GameCalendar.YearRolled) );

		// Spent: the next park entered seeds 2000 again.
		GameCalendar.Rebase( 16972, saved );
		GameCalendar.Update( 16973 );
		Assert.IsTrue( GameCalendar.YearRolled );
	}

	/// <summary>
	/// <b>The year is carried through the scene change only from a park</b> (<c>Game.ChangeSceneIfAsked</c>): a 2002 file
	/// loaded over a running 2002 park turns no year on its first sweep, and the same file entered from the lobby turns
	/// one. The lobby's calendar is left in 2002 here, which the real lobby's own <see cref="GameCalendar.Rebase"/> never
	/// leaves it, so that a carry from the lobby would show.
	/// </summary>
	/// <remarks><b>Mutations:</b> the carry deleted, or moved below the load, turns the year from a park; the guard on the
	/// scene dropped turns none from the lobby.</remarks>
	[TestMethod]
	public void TheSceneChangeCarriesTheYearOnlyFromAPark()
	{
		// Tick 16972 is 6 January 2002.
		var saved = LostKingdom with { MonthAtLastUpdate = 0, DayAtLastUpdate = 6 };
		var was = Level.Current;

		try
		{
			bool YearTurnedOnEntering( Level.Scene from )
			{
				// The running scene's calendar stands in 2002.
				GameCalendar.Rebase( 16972, saved );
				GameCalendar.Update( 16973 );

				var scene = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
				typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( scene, from );
				Level.Current = scene;

				var turned = (bool?)null;

				Game.RequestParkLoad( "jungle" );
				Game.ChangeSceneIfAsked( () => { }, ( _, _ ) =>
				{
					// What the park's own build does with the file's clock block (Level.StartTheCalendar).
					GameCalendar.Rebase( 16972, saved );
					GameCalendar.Update( 16973 );
					turned = GameCalendar.YearRolled;
				}, () => { } );

				return turned ?? throw new AssertFailedException( "the park was not loaded" );
			}

			Assert.IsFalse( YearTurnedOnEntering( Level.Scene.Park ), "from a park in 2002, the year is carried" );
			Assert.IsTrue( YearTurnedOnEntering( Level.Scene.Lobby ), "from the lobby, the entry's seed, 2000" );
		}
		finally
		{
			Level.Current = was;
		}
	}

	/// <summary>
	/// A file whose month and day are not its tick's turns both on its first sweep, as the original turns them on a file
	/// this game wrote before its writer carried the pair (<c>docs/exe/saves.md</c>, "OpenTPW's writer, the calendar's
	/// fields").
	/// </summary>
	[TestMethod]
	public void AFileWhosePairIsStaleTurnsTheDayAndTheMonthOnItsFirstSweep()
	{
		GameCalendar.Rebase( 755, LostKingdom with { MonthAtLastUpdate = 3, DayAtLastUpdate = 9 } );
		GameCalendar.Update( 756 );

		Assert.AreEqual( (true, true, false), (GameCalendar.DayRolled, GameCalendar.MonthRolled, GameCalendar.YearRolled) );
	}

	/// <summary>The rate is the file's <c>mFunnySecsPerRealSec</c>: at 30,000 tick 4 is 08:20, and a rate under one is the default's.</summary>
	/// <remarks><b>Mutations:</b> the file's rate not read.</remarks>
	[TestMethod]
	public void TheRateIsTheFilesOwn()
	{
		GameCalendar.Rebase( 4, LostKingdom with { FunnySecsPerRealSec = 30000 } );
		Assert.AreEqual( new DateTime( 2000, 1, 1, 8, 20, 0 ), GameCalendar.Now );

		GameCalendar.Rebase( 4, LostKingdom with { FunnySecsPerRealSec = 0 } );
		Assert.AreEqual( new DateTime( 2000, 1, 1, 4, 10, 0 ), GameCalendar.Now );
	}

	/// <summary>
	/// <b>A sweep's own tick is read ahead of its compare</b>, as the weather reads it in the sweep's walk (Q1165): compared on
	/// 1382, 29 February 2000 23:35, tick 1383 is 1 March 00:37, one day edge on and season 1 where the compared date's is 0; tick
	/// 1382 is the day as compared; and once 1383 is compared it reads as the compare set it.
	/// </summary>
	/// <remarks><b>Mutations:</b> <see cref="GameCalendar.DaysAt"/> without the edge to come; <see cref="GameCalendar.SeasonAt"/>
	/// from the compared date.</remarks>
	[TestMethod]
	public void ASweepsTickIsReadAheadOfItsCompare()
	{
		Assert.IsTrue( SweepUntil( () => GameCalendar.Counter == 1382 ) );
		Assert.AreEqual( new DateTime( 2000, 2, 29, 23, 35, 0 ), GameCalendar.Now );

		var days = GameCalendar.Days;

		Assert.AreEqual( (days + 1, 1, 0), (GameCalendar.DaysAt( 1383 ), GameCalendar.SeasonAt( 1383 ), GameCalendar.Season) );
		Assert.AreEqual( days, GameCalendar.DaysAt( 1382 ) );

		GameCalendar.Update( 1383 );

		Assert.AreEqual( (days + 1, 1), (GameCalendar.Days, GameCalendar.Season) );
		Assert.AreEqual( (days + 1, 1), (GameCalendar.DaysAt( 1383 ), GameCalendar.SeasonAt( 1383 )) );
	}
}
