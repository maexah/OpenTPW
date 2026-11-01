using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// What <see cref="Level.TurnTheCalendar"/> sends on each of the calendar's three turns (<c>0x004f8260</c>, <c>docs/exe/park-engine.md</c>):
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
}
