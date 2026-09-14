using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The numbers a park's weather runs on, read out of the real shipped balance files.
///
/// <para>
/// <b>Every lookup here passes a fallback that is deliberately not the answer.</b> That is the whole
/// point of the class: <see cref="ParkBalance.Int"/> returns its fallback for a key it cannot find, so
/// a test written the obvious way - asking for <c>Weather.DaysOfWarning</c> with a fallback of 4 and
/// asserting 4 - would pass just as happily if the parser dropped the key entirely. With a sentinel it
/// can only pass by actually reading the file.
/// </para>
/// <para>
/// There are two real hazards it is guarding against, and neither is hypothetical. The weather keys are
/// the first in the game to be <b>subscripted</b> - <c>Seasons[0].AvgWeatherQuality</c> - and the first
/// to carry a <b>negative</b> value, <c>-1</c>. If <c>SAMParser</c> split on the bracket, or
/// <c>NumberStyles.Integer</c> turned away the sign, the park would still run, nothing would look
/// broken, and the weather would quietly be whatever constants happened to be typed into
/// <c>ParkWeather</c>'s constructor instead of the game's own.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkWeatherTests
{
	[TestInitialize]
	public void MountTheGame()
	{
		FileSystem = GameData.Required();
	}

	/// <summary>Nothing in the files is this, so reading it back means the key was not found.</summary>
	private const int NotFound = -999;

	private static ParkBalance Jungle() => new( "jungle" );

	/// <summary>
	/// The four seasons, subscripted. Jungle's own Standard.sam does not mention weather at all, so
	/// these come from the global layer underneath it - which makes this a test of
	/// <see cref="ParkBalance"/>'s stacking as much as of the parser.
	/// </summary>
	[TestMethod]
	public void TheFourSeasonsReadThroughTheirSubscripts()
	{
		var balance = Jungle();

		int[] average = [75, 80, 90, 50];
		int[] tolerance = [25, 20, 10, 15];
		int[] exceptional = [10, 10, 5, 10];

		for ( int season = 0; season < 4; ++season )
		{
			Assert.AreEqual( average[season], balance.Int( $"Seasons[{season}].AvgWeatherQuality", NotFound ),
				$"Seasons[{season}].AvgWeatherQuality" );

			Assert.AreEqual( tolerance[season], balance.Int( $"Seasons[{season}].NormalTolerance", NotFound ),
				$"Seasons[{season}].NormalTolerance" );

			Assert.AreEqual( exceptional[season], balance.Int( $"Seasons[{season}].ChanceForExceptionalWeather", NotFound ),
				$"Seasons[{season}].ChanceForExceptionalWeather" );
		}
	}

	/// <summary>
	/// How often the weather turns, and how far ahead it is decided. These two are what make a day
	/// length matter: seven days at about five and three quarter seconds each is a change of weather
	/// roughly every forty seconds - see <see cref="GameCalendar"/>.
	/// </summary>
	[TestMethod]
	public void TheWeatherTurnsEverySevenDaysAndIsDecidedThreeDaysBefore()
	{
		var balance = Jungle();

		Assert.AreEqual( 4, balance.Int( "Weather.DaysOfWarning", NotFound ) );
		Assert.AreEqual( 7, balance.Int( "Weather.DaysBetweenChanges", NotFound ) );
		Assert.AreEqual( 20, balance.Int( "Weather.SpeedOfChange", NotFound ) );
	}

	/// <summary>
	/// The bands a quality has to fall inside, <b>in jungle</b> - which is the only theme with a saved
	/// park, and so the only one that can be played today.
	///
	/// <para>
	/// <b>Snow is switched off by data here, not missing from the game.</b> The original has a whole
	/// snow branch, its own particle mode and a dedicated renderer, and both snow.tga and its
	/// low-detail twin ship. Jungle cannot snow because the quality is clamped to at least 1 and its
	/// band is -1..-1 - and -1 is not a hack, it is what the schema compiled into the executable
	/// declares as the field's lower bound. <b>Hallow is a different matter entirely</b>; see
	/// <see cref="OnlyHallowCanSnowAndItsBandsOverlapWithItsRain"/>.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ANegativeBandSurvivesTheParser()
	{
		var balance = Jungle();

		Assert.AreEqual( -1, balance.Int( "WeatherEffects.QualityForSnowLow", NotFound ),
			"a negative value did not survive the parser" );
		Assert.AreEqual( -1, balance.Int( "WeatherEffects.QualityForSnowHigh", NotFound ) );

		Assert.AreEqual( 0, balance.Int( "WeatherEffects.QualityForRainLow", NotFound ) );
		Assert.AreEqual( 40, balance.Int( "WeatherEffects.QualityForRainHigh", NotFound ) );

		Assert.AreEqual( 0, balance.Int( "WeatherEffects.QualityForLightningLow", NotFound ) );
		Assert.AreEqual( 15, balance.Int( "WeatherEffects.QualityForLightningHigh", NotFound ) );

		// A quality is clamped to 1..100, so nothing can ever land inside JUNGLE's snow band.
		Assert.IsTrue( balance.Int( "WeatherEffects.QualityForSnowHigh", NotFound ) < 1,
			"jungle could snow, which its own balance file does not allow" );
	}

	/// <summary>What a storm is made of: how often it flashes, how long a bolt lasts, and how far off it sounds.</summary>
	[TestMethod]
	public void TheStormsOwnNumbersRead()
	{
		var balance = Jungle();

		Assert.AreEqual( 10, balance.Int( "WeatherEffects.LightningCountdownSpeed", NotFound ) );
		Assert.AreEqual( 500, balance.Int( "WeatherEffects.LightningCountdownFrom", NotFound ) );
		Assert.AreEqual( 2000, balance.Int( "WeatherEffects.LightningTime", NotFound ) );
		Assert.AreEqual( 10, balance.Int( "WeatherEffects.ThunderStartsThisFarAway", NotFound ) );
		Assert.AreEqual( 2, balance.Int( "WeatherEffects.ThunderRandomiser", NotFound ) );
		Assert.AreEqual( 600, balance.Int( "WeatherEffects.MaxRaindrops", NotFound ) );
	}

	/// <summary>
	/// <b>The weather IS per-theme, and this test exists because I assumed it was not.</b>
	///
	/// <para>
	/// It was written the other way round - asserting all four themes read the same numbers - and it
	/// failed on hallow's rain band of 45 against the global 40. Every theme restates 26 of the 27
	/// weather keys in its own Standard.sam, and five of them genuinely differ. Nothing in the code had
	/// to change, because <see cref="ParkBalance"/> layers a theme's file over the global one, but the
	/// assumption was in the documentation as fact.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheBandsArePerThemeAndHallowIsTheStormyOne()
	{
		// theme, snowLow/High, rainLow/High, lightningHigh
		(string Theme, int SnowLow, int SnowHigh, int RainLow, int RainHigh, int LightningHigh)[] themes =
		[
			("jungle", -1, -1, 0, 40, 15),
			("hallow", 0, 25, 15, 45, 25),
			("space", -1, -1, -1, -1, 15),
			("fantasy", -1, -1, 0, 40, 15)
		];

		foreach ( var (theme, snowLow, snowHigh, rainLow, rainHigh, lightningHigh) in themes )
		{
			var balance = new ParkBalance( theme );

			Assert.AreEqual( snowLow, balance.Int( "WeatherEffects.QualityForSnowLow", NotFound ), $"{theme} snow low" );
			Assert.AreEqual( snowHigh, balance.Int( "WeatherEffects.QualityForSnowHigh", NotFound ), $"{theme} snow high" );
			Assert.AreEqual( rainLow, balance.Int( "WeatherEffects.QualityForRainLow", NotFound ), $"{theme} rain low" );
			Assert.AreEqual( rainHigh, balance.Int( "WeatherEffects.QualityForRainHigh", NotFound ), $"{theme} rain high" );
			Assert.AreEqual( lightningHigh, balance.Int( "WeatherEffects.QualityForLightningHigh", NotFound ), $"{theme} lightning high" );
		}
	}

	/// <summary>
	/// Hallow can snow, and the other three cannot. A quality is clamped to 1..100, so a band whose top
	/// is below 1 can never be entered - which is the whole of why snow looked like dead code.
	///
	/// <para>
	/// <b>And hallow's bands overlap, which is why the original tosses a coin.</b> Its snow is 0..25 and
	/// its rain 15..45, so a quality of 15 to 25 qualifies as both - exactly the case FUN_00512f00
	/// resolves with a coin flip. That branch is not dead code; it is there for this one theme.
	/// </para>
	/// </summary>
	[TestMethod]
	public void OnlyHallowCanSnowAndItsBandsOverlapWithItsRain()
	{
		foreach ( var theme in new[] { "jungle", "space", "fantasy" } )
		{
			var balance = new ParkBalance( theme );

			Assert.IsTrue( balance.Int( "WeatherEffects.QualityForSnowHigh", NotFound ) < 1,
				$"{theme} could snow, which the shipped data does not allow" );
		}

		var hallow = new ParkBalance( "hallow" );

		var snowHigh = hallow.Int( "WeatherEffects.QualityForSnowHigh", NotFound );
		var rainLow = hallow.Int( "WeatherEffects.QualityForRainLow", NotFound );

		Assert.IsTrue( snowHigh >= 1, "hallow should be able to snow" );
		Assert.IsTrue( rainLow <= snowHigh, "hallow's snow and rain bands should overlap" );
	}

	/// <summary>
	/// The half of the old assumption that survives: the seasons, the pacing and every storm-timing
	/// number are the same in all four themes. Only the five band keys differ, and MaxRaindrops is the
	/// one key no theme declares at all.
	/// </summary>
	[TestMethod]
	public void TheSeasonsAndThePacingAreTheSameEverywhere()
	{
		foreach ( var theme in new[] { "jungle", "hallow", "space", "fantasy" } )
		{
			var balance = new ParkBalance( theme );

			Assert.AreEqual( 75, balance.Int( "Seasons[0].AvgWeatherQuality", NotFound ), theme );
			Assert.AreEqual( 50, balance.Int( "Seasons[3].AvgWeatherQuality", NotFound ), theme );
			Assert.AreEqual( 7, balance.Int( "Weather.DaysBetweenChanges", NotFound ), theme );
			Assert.AreEqual( 4, balance.Int( "Weather.DaysOfWarning", NotFound ), theme );
			Assert.AreEqual( 20, balance.Int( "Weather.SpeedOfChange", NotFound ), theme );
			Assert.AreEqual( 500, balance.Int( "WeatherEffects.LightningCountdownFrom", NotFound ), theme );
			Assert.AreEqual( 2000, balance.Int( "WeatherEffects.LightningTime", NotFound ), theme );

			// Declared only in the global layer, so this also proves the stack is reaching underneath.
			Assert.AreEqual( 600, balance.Int( "WeatherEffects.MaxRaindrops", NotFound ), theme );
		}
	}
}
