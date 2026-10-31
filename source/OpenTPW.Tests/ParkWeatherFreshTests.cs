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
	public void StartTheCalendar() => GameCalendar.Rebase();

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
	/// <b>Its first forecast comes on sweep 1</b>, its nought stamp being long past (the original's Halloween file saved on
	/// sweep 4: forecast 1.00, due 93.16), <b>and the rolled quality stands until that lands</b>: unread and unchanged, the
	/// wind with it, to sweep 92, then the forecast's on the day edge of sweep 93, where the original's is 94.
	/// </summary>
	/// <remarks><b>Mutations:</b> the first forecast waiting its three days; the forecast delivered at once; the delivery
	/// leaving the stamp at nought.</remarks>
	[TestMethod]
	public void TheFirstForecastComesOnTheFirstSweepAndLandsFourDaysLater()
	{
		var weather = new ParkWeather( null, drawn: false );

		try
		{
			var rolled = weather.Quality;

			GameCalendar.Update( 1 );
			weather.Update();

			var forecast = weather.Forecast;
			var first = weather.Written( null );

			Assert.IsTrue( forecast >= 0, "a forecast on the first sweep" );
			Assert.IsTrue( first.ForecastValid );
			Assert.AreEqual( forecast, first.ForecastQuality );
			Assert.AreEqual( ParkWeather.ForecastDue( first.LastForecasted, 4 ), first.ForecastTime );
			Assert.AreEqual( 0L, first.LastChanged );

			for ( var sweep = 2; sweep <= 92; ++sweep )
			{
				GameCalendar.Update( sweep );
				weather.Update();
			}

			Assert.AreEqual( (rolled, forecast), (weather.Quality, weather.Forecast), "stood to sweep 92" );
			Assert.AreEqual( (90f, 0.5f), (weather.WindDirection, weather.WindForce) );
			Assert.AreEqual( 0L, weather.Written( null ).LastChanged );

			GameCalendar.Update( 93 );
			weather.Update();

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

	/// <summary>
	/// <b>A park with a file still applies its roll and takes the file's wind</b>, the deviation until Q529: stamped as it
	/// opens, so its first forecast waits three days, and its force the roll's.
	/// </summary>
	/// <remarks><b>Mutations:</b> a loaded park standing as a fresh one does.</remarks>
	[TestMethod]
	public void ALoadedParkAppliesItsRollAndWaitsItsThreeDays()
	{
		var weather = new ParkWeather( 139f, drawn: false );

		try
		{
			Assert.AreEqual( 139f, weather.WindDirection );
			Assert.AreEqual( (100 - weather.Quality) * 0.01f, weather.WindForce, 1e-5f );
			Assert.AreNotEqual( 0L, weather.Written( null ).LastChanged );

			GameCalendar.Update( 1 );
			weather.Update();

			Assert.AreEqual( -1, weather.Forecast );
		}
		finally
		{
			weather.Delete();
		}
	}
}
