using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// A park's first days of weather, on the shipped numbers <see cref="ParkWeather"/> falls back to, so these run without
/// the game: <c>docs/exe/weather.md</c>, "A park's first days".
/// </summary>
[TestClass]
public class ParkWeatherFreshTests
{
	[TestInitialize]
	public void StartTheCalendar()
	{
		// The weather's stamps are the park state's calendar where there is one; these run on the calendar's own.
		ParkState.ForgetCurrent();
		GameCalendar.Rebase();
	}

	[TestCleanup]
	public void LeaveTheCalendar() => GameCalendar.Rebase();

	/// <summary>
	/// <b>A fresh park stands as <c>Weather_Construct</c> leaves it, whatever it rolled</b>: wind 90 at 0.5, the stamps nought,
	/// nothing falling, however low the roll. One in ten rolls is the season's exceptional one, so two hundred reach the
	/// rain's band.
	/// </summary>
	/// <remarks><b>Mutations:</b> the roll applied at once; the wind or its force left at nought; a quality not rolled.</remarks>
	[TestMethod]
	public void AFreshParkStandsAsTheConstructorLeavesIt()
	{
		var rolled = new HashSet<int>();

		for ( var run = 0; run < 200; ++run )
		{
			var weather = new ParkWeather( null, drawn: false );

			try
			{
				rolled.Add( weather.Quality );

				Assert.AreEqual( new ParkWorld.WeatherState( weather.Quality, 0, 0, 0, 0, false, 0, 0, false, 0, 0, 0, null, 90f, 0.5f ),
					weather.Written( null ) );
				Assert.AreEqual( -1, weather.Forecast );
				Assert.IsFalse( weather.Lightning );
			}
			finally
			{
				weather.Delete();
			}
		}

		Assert.IsTrue( rolled.Count > 1, "a roll, not a constant" );
	}

	/// <summary>
	/// <b>The stored roll is the roll, unclamped</b> (<c>Weather_Construct</c> stores <c>FUN_00512e10</c>'s answer at <c>+0x0c</c>; only
	/// <c>Weather_ApplyQuality</c> clamps): the season's exceptional roll is wholly random, 0 to 99, so a fresh park is found holding 0.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>Quality = Math.Clamp( Roll(), 1, 100 )</c> in the constructor.</remarks>
	[TestMethod]
	public void AFreshParksStoredQualityIsTheUnclampedRoll()
	{
		for ( var run = 0; run < 30000; ++run )
		{
			var weather = new ParkWeather( null, drawn: false );

			try
			{
				if ( weather.Quality == 0 )
					return;
			}
			finally
			{
				weather.Delete();
			}
		}

		Assert.Fail( "no fresh park held a stored quality of 0 in 30000 (about one in a thousand each)" );
	}

	/// <summary>
	/// <b>Its first forecast comes on sweep 1</b>, its nought stamp being long past (the original's Halloween file saved on
	/// sweep 4: forecast 1.00, due 93.16), <b>and the rolled quality stands until that lands</b>: unread and unchanged, the
	/// wind with it, to sweep 93, then the forecast's on the first tick past its due time, sweep 94, as the original's.
	/// </summary>
	/// <remarks><b>Mutations:</b> the first forecast waiting its three days; the forecast delivered at once; the delivery
	/// leaving the stamp at nought; the schedule on day edges (delivered on 93).</remarks>
	[TestMethod]
	public void TheFirstForecastComesOnTheFirstSweepAndLandsFourDaysLater()
	{
		var weather = new ParkWeather( null, drawn: false );

		try
		{
			var rolled = weather.Quality;

			GameCalendar.Update( 1 );
			weather.Turn();

			var forecast = weather.Forecast;
			var first = weather.Written( null );

			Assert.IsTrue( forecast >= 0, "a forecast on the first sweep" );
			Assert.IsTrue( first.ForecastValid );
			Assert.AreEqual( forecast, first.ForecastQuality );
			Assert.AreEqual( ParkWeather.ForecastDue( first.LastForecasted, 4 ), first.ForecastTime );
			Assert.AreEqual( 0L, first.LastChanged );

			for ( var sweep = 2; sweep <= 93; ++sweep )
			{
				GameCalendar.Update( sweep );
				weather.Turn();
			}

			Assert.AreEqual( (rolled, forecast), (weather.Quality, weather.Forecast), "stood to sweep 93" );
			Assert.AreEqual( (90f, 0.5f), (weather.WindDirection, weather.WindForce) );
			Assert.AreEqual( 0L, weather.Written( null ).LastChanged );

			GameCalendar.Update( 94 );
			weather.Turn();

			var delivered = Math.Clamp( forecast, 1, 100 );
			var turned = weather.Written( null );

			Assert.AreEqual( (delivered, -1), (weather.Quality, weather.Forecast) );
			Assert.AreNotEqual( 0L, turned.LastChanged );
			Assert.IsFalse( turned.ForecastValid );
			Assert.AreEqual( (100 - delivered) * 0.01f, weather.WindForce, 1e-5f );
			Assert.IsTrue( Math.Abs( weather.WindDirection - 90f ) <= 45f, $"wind {weather.WindDirection}" );
		}
		finally
		{
			weather.Delete();
		}
	}

	/// <summary>The park calendar on <paramref name="tick"/> at the shipped rate, a <c>FILETIME</c>: 3750 seconds a tick from 2000.</summary>
	private static long On( int tick ) => ParkWorld.StaffState.FileTimeOf( GameCalendar.Epoch.AddSeconds( tick * 3750L ) );

	/// <summary>
	/// A file shaped as q528's second (<c>docs/exe/saves.md</c>, "Both games"): quality 10 applied on 16970, this game's
	/// forecast of 83 made on 17004 and pending, the drops at 440 of 450, the storm armed, its thunder at (275.4, 300,
	/// 269.2) in the original's axes, the wind 142 at 0.9. Saved on 17010.
	/// </summary>
	private static ParkWorld.WeatherState Stormy => new( 10, On( 16970 ), On( 17004 ), ParkWeather.ForecastDue( On( 17004 ), 4 ), 83,
		true, 440, 450, true, 120, 2, -20, new System.Numerics.Vector3( 275.4f, 300f, 269.2f ), 142f, 0.9f );

	/// <summary>
	/// <b>A loaded park runs on from its file</b>, as the original's load does: the record is taken whole and nothing is
	/// applied, so it writes back as it was read; the drops stand at 440, within a step of their 450, so the ramp does not
	/// move them; the file's forecast of 83 falls due on the first tick past 17004 + 92.16, 17097, which the original given
	/// that file delivered on; the drops then walk down twenty a tick and are gone on 17118, the original's last tick of
	/// rain; and the next forecast comes on the first tick past 17097 + 69.12, 17167, the original's.
	/// </summary>
	/// <remarks><b>Mutations:</b> the roll applied at the load; the file's forecast not taken; the drops not taken; the
	/// delivery at the forecast's stamp; the next lead the warning's four days; the schedule on day edges.</remarks>
	[TestMethod]
	public void ALoadedParkRunsOnFromItsFile()
	{
		GameCalendar.Rebase( 17010 );
		var weather = new ParkWeather( Stormy, drawn: false );

		try
		{
			Assert.AreEqual( Stormy, weather.Written( null ), "the record as it was read" );
			Assert.AreEqual( (10, 440, 83, true), (weather.Quality, weather.Drops, weather.Forecast, weather.Lightning) );
			Assert.AreEqual( (142f, 0.9f), (weather.WindDirection, weather.WindForce) );

			int delivered = 0, dry = 0, next = 0;

			for ( var tick = 17011; tick < 17300 && next == 0; ++tick )
			{
				GameCalendar.Update( tick );
				weather.Turn();

				if ( delivered == 0 && weather.Forecast < 0 )
					delivered = tick;
				else if ( delivered == 0 )
					Assert.AreEqual( (10, 440), (weather.Quality, weather.Drops), $"stood on {tick}" );

				if ( dry == 0 && delivered != 0 && weather.Drops == 0 )
					dry = tick;

				if ( delivered != 0 && tick > delivered && weather.Forecast >= 0 )
					next = tick;
			}

			Assert.AreEqual( (17097, 17118, 17167), (delivered, dry, next) );
			Assert.AreEqual( On( 17097 ), weather.Written( null ).LastChanged );
			Assert.AreEqual( On( 17167 ), weather.Written( null ).LastForecasted );
		}
		finally
		{
			weather.Delete();
		}
	}

	/// <summary>
	/// <b>A file with a nought stamp forecasts on its first sweep</b>, as Restart Park's at tick nought does in the original
	/// (<c>docs/exe/weather.md</c>, "A park's first days"), its quality standing unapplied.
	/// </summary>
	/// <remarks><b>Mutations:</b> the roll applied at the load (stamped, so it waits its three days).</remarks>
	[TestMethod]
	public void AFileWithANoughtStampForecastsOnItsFirstSweep()
	{
		var restart = new ParkWorld.WeatherState( 77, 0, 0, 0, 0, false, 0, 0, false, 0, 0, 0, null, 90f, 0.5f );
		var weather = new ParkWeather( restart, drawn: false );

		try
		{
			GameCalendar.Update( 1 );
			weather.Turn();

			Assert.IsTrue( weather.Forecast >= 0, "a forecast on sweep 1" );
			Assert.AreEqual( (77, On( 1 )), (weather.Quality, weather.Written( null ).LastForecasted) );
			Assert.AreEqual( 0L, weather.Written( null ).LastChanged );
		}
		finally
		{
			weather.Delete();
		}
	}
}
