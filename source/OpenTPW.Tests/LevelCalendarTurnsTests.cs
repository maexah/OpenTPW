using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// What <see cref="Level.TurnTheCalendar"/> sends on each of the calendar's three turns (<c>0x004f8260</c>, <c>docs/exe/ride-operation.md</c>):
/// the day's change to every object's rings, the month's to the bank's analyser, the year's to the bank's profit. Each is driven
/// tick by tick from the epoch with the calendar's own flags counted, so a turn sent on the wrong flag, sent twice or not sent
/// shows at the tick it happens. None needs the game.
/// </summary>
[TestClass]
public class LevelCalendarTurnsTests
{
	[TestInitialize]
	public void HaveALog() => Log ??= new();

	/// <summary>
	/// <b>The day's change rolls every object's rings once on the tick the day turns, and on no other</b>: a takings figure of
	/// 4 set today is yesterday's after the first turn and the ring's entry has moved by one for each day that turned.
	/// </summary>
	/// <remarks><b>Mutations:</b> the rings not rolled at the day's change; rolled on every tick; rolled at the month's change.</remarks>
	[TestMethod]
	public void TheDaysChangeRollsTheRingsOnceForEachDayThatTurned()
	{
		GameCalendar.Rebase();

		try
		{
			var state = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
			var rings = state.RingsFor( 1 );
			var days = 0;

			rings.Takings.Today = 4;

			for ( var tick = 1; GameCalendar.Now < new System.DateTime( 2000, 1, 20 ); ++tick )
			{
				state.SetGameTick( tick );
				Level.TurnTheCalendar( state, null, null, _ => { } );

				if ( GameCalendar.DayRolled )
					++days;

				Assert.AreEqual( days - 1, rings.Takings.CurrentEntry, $"tick {tick}: one roll for each day turned, {days} so far" );

				if ( days >= 1 )
				{
					Assert.AreEqual( 0, rings.Takings.Today, $"tick {tick}: today starts at nought" );
					Assert.AreEqual( 4, rings.Takings.DaysAgo( days - 1 ), $"tick {tick}: the first day's 4 stays in its slot" );

					if ( days > 1 )
						Assert.AreEqual( 0, rings.Takings.DaysAgo( 0 ), $"tick {tick}: yesterday is a day that took nothing" );
				}
			}

			Assert.IsTrue( days >= 19, $"the 1st of January to the 19th turned its days, got {days}" );
		}
		finally
		{
			GameCalendar.Rebase();
		}
	}

	/// <summary>
	/// <b>The month closes the analyser once on the tick the month turns, and not on the day's</b>: 700 of costs put in January
	/// is the newest month's costs from the turn that ends January, and the balance ring has taken one sample for each month that
	/// turned, no more.
	/// </summary>
	/// <remarks><b>Mutations:</b> the month closed at the day's change; closed at the year's; not closed.</remarks>
	[TestMethod]
	public void TheMonthsChangeClosesTheAnalyserOnceForEachMonthThatTurned()
	{
		GameCalendar.Rebase();

		try
		{
			var state = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
			var months = 0;
			var seen = new System.Collections.Generic.List<string>();

			for ( var tick = 1; GameCalendar.Now < new System.DateTime( 2000, 4, 3 ); ++tick )
			{
				if ( GameCalendar.Now.Date == new System.DateTime( 2000, 1, 20 ) && state.MonthCosts.Total == 0 )
					state.MonthCosts.Add( 700 );

				state.SetGameTick( tick );
				Level.TurnTheCalendar( state, null, null, _ => { } );

				if ( GameCalendar.MonthRolled )
				{
					++months;
					seen.Add( $"{GameCalendar.Now:MM-dd}: costs {state.MonthCosts.Newest}" );
				}

				Assert.AreEqual( months - 1, state.MonthBalance.Current, $"tick {tick}: one sample for each month turned" );
			}

			Assert.AreEqual( 3, months, "January, February and March turned" );
			CollectionAssert.AreEqual( new[] { "02-01: costs 700", "03-01: costs 0", "04-01: costs 0" }, seen );
		}
		finally
		{
			GameCalendar.Rebase();
		}
	}

	/// <summary>
	/// <b>The year's change zeroes the year's profit on the tick the year turns, and not before</b>: 500 taken on the first tick
	/// stands through every day and month of 2000 and is gone at the turn into 2001.
	/// </summary>
	/// <remarks><b>Mutations:</b> the profit zeroed at the month's change; zeroed at the day's; never zeroed.</remarks>
	[TestMethod]
	public void TheYearsChangeZeroesTheYearsProfitAndOnlyThen()
	{
		GameCalendar.Rebase();

		try
		{
			var state = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
			var turned = false;
			var years = 0;

			state.Take( 500 );

			for ( var tick = 1; GameCalendar.Now < new System.DateTime( 2001, 1, 3 ); ++tick )
			{
				state.SetGameTick( tick );
				Level.TurnTheCalendar( state, null, null, _ => { } );

				if ( GameCalendar.YearRolled )
				{
					turned = true;
					++years;
					Assert.AreEqual( new System.DateTime( 2001, 1, 1 ), GameCalendar.Now.Date, "the year turns on the 1st of January" );
				}

				Assert.AreEqual( turned ? 0 : 500, state.ProfitThisYear, $"tick {tick}, {GameCalendar.Now:yyyy-MM-dd}" );
			}

			Assert.AreEqual( 1, years, "one year turned" );
		}
		finally
		{
			GameCalendar.Rebase();
		}
	}

	/// <summary>
	/// <b>The day's change rolls on a month's first day and a year's first day too</b>: the 1st of February 2000 and the 1st of
	/// January 2001 each roll the rings once, with the month's (and the year's) change on the same tick.
	/// </summary>
	/// <remarks><b>Mutations:</b> the day's change skipped on the tick the month turns; skipped on the tick the year turns.</remarks>
	[TestMethod]
	public void TheDaysChangeRollsOnAMonthsAndAYearsFirstDay()
	{
		GameCalendar.Rebase();

		try
		{
			var state = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
			var rings = state.RingsFor( 1 );
			var days = 0;
			var firsts = new System.Collections.Generic.List<string>();

			for ( var tick = 1; GameCalendar.Now < new System.DateTime( 2001, 1, 3 ); ++tick )
			{
				state.SetGameTick( tick );
				Level.TurnTheCalendar( state, null, null, _ => { } );

				if ( GameCalendar.DayRolled )
					++days;

				if ( GameCalendar.MonthRolled )
					firsts.Add( $"{GameCalendar.Now:yyyy-MM-dd} day {GameCalendar.DayRolled}" );

				Assert.AreEqual( (days - 1) % 30, rings.Takings.CurrentEntry, $"tick {tick}, {GameCalendar.Now:yyyy-MM-dd}: one roll for each day turned, {days} so far" );
			}

			CollectionAssert.Contains( firsts, "2000-02-01 day True" );
			CollectionAssert.Contains( firsts, "2001-01-01 day True" );
			Assert.IsFalse( System.Linq.Enumerable.Any( firsts, f => f.EndsWith( "False" ) ), "every month's first day rolled the day" );
		}
		finally
		{
			GameCalendar.Rebase();
		}
	}

	/// <summary>
	/// <b>On the tick the day, the month and the year all turn, they are sent in the original's order</b>: the day's change, then
	/// the gadget's trend read, then the month's, then the year's, then the golden tickets' test on the new year's profit
	/// (<c>0x004f8260</c>, <c>0x004f83ac</c>, <c>0x004d7b54</c>). The trend is read from the last closed month, so 700 of costs put in
	/// December do not show as a loss on the 1st of January's tick, which the month's change closes after the read. The trend read
	/// after the day's change, as against before it, is not told apart here: the day's change moves nothing the trend reads.
	/// </summary>
	/// <remarks><b>Mutations:</b> the month before the day; the year before the month; the trend read after the month's change;
	/// the tickets before the year's change.</remarks>
	[TestMethod]
	public void TheDayThenTheTrendThenTheMonthThenTheYearThenTheTickets()
	{
		GameCalendar.Rebase();

		var events = new System.Collections.Generic.List<string>();
		Logger.LogDelegate listen = ( _, text ) =>
		{
			if ( text.StartsWith( "The day's change:" ) ) events.Add( "day" );
			else if ( text.StartsWith( "Park: analyser month closed" ) ) events.Add( "month" );
			else if ( text.StartsWith( "Bank: the year's change" ) ) events.Add( "year" );
		};

		Logger.OnLog += listen;

		try
		{
			var state = new ParkState( parkIsClosed: false, visitorsToDate: 0 );
			var yearTurns = 0;
			var profitAtTickets = -1;

			state.Take( 500 );
			ParkGoldenTickets.TakeOwed();

			for ( var tick = 1; GameCalendar.Now < new System.DateTime( 2001, 1, 3 ); ++tick )
			{
				if ( GameCalendar.Now.Date == new System.DateTime( 2000, 12, 31 ) && state.MonthCosts.Total == 0 )
					state.MonthCosts.Add( 700 );

				events.Clear();
				ParkGoldenTickets.Owe( ParkGoldenTickets.EverySweeps );
				state.SetGameTick( tick );
				Level.TurnTheCalendar( state, null, null, _ =>
				{
					events.Add( "tickets" );
					profitAtTickets = state.ProfitThisYear;
				} );

				if ( !GameCalendar.YearRolled )
					continue;

				++yearTurns;
				CollectionAssert.AreEqual( new[] { "day", "month", "year", "tickets" }, events, "the order of the 1st of January" );
				Assert.AreEqual( 0, profitAtTickets, "the tickets are tested on the new year's profit" );
				Assert.AreEqual( 700, state.MonthCosts.Newest, "December's costs closed" );
				Assert.IsFalse( state.MonthInLoss, "the trend was read before the month closed" );
			}

			Assert.AreEqual( 1, yearTurns, "one year turned" );
		}
		finally
		{
			Logger.OnLog -= listen;
			GameCalendar.Rebase();
		}
	}
}
