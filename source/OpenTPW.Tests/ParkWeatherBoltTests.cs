using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// How long each scene's bolt is up: the lifetime the original's bolt draw takes as its argument 7, in milliseconds
/// (FUN_00580320; <c>docs/exe/weather.md</c>, "The bolt"). A lobby bolt lasts 500 and a park's lasts the balance file's
/// <c>LightningTime</c>.
/// </summary>
[TestClass]
public class ParkWeatherBoltTests
{
	[TestInitialize]
	public void StartTheCalendar() => GameCalendar.Rebase();

	[TestCleanup]
	public void LeaveTheCalendar() => GameCalendar.Rebase();

	/// <summary>Strikes once and answers the lifetime the bolt was handed, the weather drawing nothing.</summary>
	private static float LifetimeOfAStrike( ParkWeather weather )
	{
		var handed = new List<float>();
		weather.StrikeBolt = ( _, _, lifetime ) => handed.Add( lifetime );

		weather.DebugStrike( new Vector3( 500f, 500f, 0f ) );

		Assert.AreEqual( 1, handed.Count, "one bolt, one draw" );
		return handed[0];
	}

	/// <summary>
	/// <b>A park's bolt is up for <c>LightningTime</c>, 2000 milliseconds as shipped</b>, on the fallback a missing balance file
	/// leaves, so this runs without the game.
	/// </summary>
	/// <remarks><b>Mutations:</b> the bolt handed 0.42 (the old constant), 0.5 (the lobby's) or <c>_lightningTime</c> unconverted.</remarks>
	[TestMethod]
	public void AParksBoltIsUpForTheShippedTwoSeconds()
	{
		var weather = new ParkWeather( null, drawn: false );

		try
		{
			Assert.AreEqual( 2f, weather.BoltLifetime, 1e-6f );
			Assert.AreEqual( 2f, LifetimeOfAStrike( weather ), 1e-6f );
		}
		finally
		{
			weather.Delete();
		}
	}

	/// <summary>
	/// <b>A park's bolt follows the balance file's <c>LightningTime</c></b>: a value no wrong rule lands on (3000 is neither the
	/// shipped 2000, the lobby's 500 nor the old 0.42 seconds) comes out as 3 seconds.
	/// </summary>
	/// <remarks><b>Mutations:</b> the key read as <c>LightningCountdownFrom</c> or not read (the fallback 2000); the bolt handed the constant.</remarks>
	[TestMethod]
	public void AParksBoltFollowsTheBalanceFilesLightningTime()
	{
		FileSystem = GameData.Required();

		var balance = new ParkBalance( "jungle" );
		balance.Set( "WeatherEffects.LightningTime", "3000" );

		var weather = new ParkWeather( null, drawn: false, balance );

		try
		{
			Assert.AreEqual( 3f, LifetimeOfAStrike( weather ), 1e-6f );
		}
		finally
		{
			weather.Delete();
		}
	}

	/// <summary>
	/// <b>The shipped balance files all say 2000</b>, so a park of any theme holds its bolt two seconds (the key is read, not
	/// defaulted: the game's own file is the one the first test's fallback restates).
	/// </summary>
	[TestMethod]
	public void EveryShippedBalanceFileHoldsTheBoltTwoSeconds()
	{
		FileSystem = GameData.Required();

		foreach ( var theme in new[] { "jungle", "fantasy", "hallow", "space" } )
		{
			var weather = new ParkWeather( null, drawn: false, new ParkBalance( theme ) );

			try
			{
				Assert.AreEqual( 2f, weather.BoltLifetime, 1e-6f, theme );
			}
			finally
			{
				weather.Delete();
			}
		}
	}

	/// <summary>
	/// <b>The lobby's bolt is up for 500 milliseconds</b>, the lobby tick's argument 7 (its other 500 is the bolt's height).
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>BoltLifetime</c> 0.42 or 2.</remarks>
	[TestMethod]
	public void TheLobbysBoltIsUpForHalfASecond()
	{
		Assert.AreEqual( 0.5f, LobbyWeather.BoltLifetime, 1e-6f );
	}
}
