using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// A balance key that names more than one field, which only the peep constants use.
///
/// <para>
/// A key is a group, an optional subscript, and then <b>one or more field names separated by dots</b> -
/// and the line supplies one value per name, in order. The original's parser says so outright: it
/// collects the dotted names into a table, stops at sixteen (it carries the string <c>"Too many fields
/// have been specified"</c> for the seventeenth), and then runs its value loop exactly as many times as
/// there are names, printing the offending field by name if one will not resolve.
/// </para>
/// <para>
/// This matters because it is silent when it is wrong. A parser that reads the line as one key and one
/// value keeps the first number, drops the rest, and reports no error at all - so
/// <c>PeepTypes[3].StartingCash</c> simply appears not to exist, and every caller gets its fallback.
/// </para>
/// <para>
/// Every lookup here passes a fallback that is deliberately not the answer, the rule
/// <see cref="ParkWeatherTests"/> sets out: <see cref="ParkBalance.Int"/> returns its fallback for a key
/// it cannot find, so a test that asserted the real value against a matching fallback would pass just as
/// happily if the key were dropped entirely.
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class BalanceFieldTests
{
	[TestInitialize]
	public void MountTheGame() => FileSystem = GameData.Required();

	/// <summary>Nothing in the files is this, so reading it back means the key was not found.</summary>
	private const int NotFound = -999;

	private static ParkBalance Jungle() => new( "jungle" );

	/// <summary>
	/// The eight kinds of guest, three columns apiece.
	///
	/// <para>
	/// Jungle's own Standard.sam does not mention peeps at all, so all of this comes from the global
	/// layer underneath it - which makes this a test of <see cref="ParkBalance"/>'s stacking as much as
	/// of the parser, the same way the weather keys are.
	/// </para>
	/// </summary>
	[TestMethod]
	public void AKeyThatNamesThreeFieldsDefinesThreeSettings()
	{
		var balance = Jungle();

		int[] excitement = [80, 65, 50, 35, 65, 80, 45, 80];
		int[] cash = [300, 500, 600, 700, 750, 600, 400, 500];
		int[] boredom = [40, 30, 40, 35, 30, 40, 30, 40];

		for ( var type = 0; type < ParkWorld.GuestState.PersonTypes; ++type )
		{
			Assert.AreEqual( excitement[type], balance.Int( $"PeepTypes[{type}].PreferredExcitement", NotFound ),
				$"PeepTypes[{type}].PreferredExcitement" );

			Assert.AreEqual( cash[type], balance.Int( $"PeepTypes[{type}].StartingCash", NotFound ),
				$"PeepTypes[{type}].StartingCash" );

			Assert.AreEqual( boredom[type], balance.Int( $"PeepTypes[{type}].BoredomThreshold", NotFound ),
				$"PeepTypes[{type}].BoredomThreshold" );
		}
	}

	/// <summary>
	/// The whole dotted key names nothing of its own, which is what says the line was actually taken
	/// apart rather than read twice. Without this, a parser that kept the whole key as an entry <i>and</i>
	/// added the three split ones would pass every assertion above.
	/// </summary>
	[TestMethod]
	public void TheJoinedUpKeyIsNotASettingOfItsOwn()
	{
		Assert.AreEqual( NotFound,
			Jungle().Int( "PeepTypes[0].PreferredExcitement.StartingCash.BoredomThreshold", NotFound ),
			"the three names are three settings, not one" );
	}

	/// <summary>
	/// The control, and the more important half of this file: a key naming a single field has to come
	/// back as that one setting, subscript and all. Sixteen lines in the whole game name more than
	/// one field, so a change here that broke the other few hundred keys would be a poor trade.
	/// </summary>
	[TestMethod]
	public void AKeyThatNamesOneFieldIsReadExactlyAsBefore()
	{
		var balance = Jungle();

		Assert.AreEqual( 120, balance.Int( "PeepInfo.ExitLevel", NotFound ), "PeepInfo.ExitLevel" );
		Assert.AreEqual( 60, balance.Int( "PeepInfo.ExitLevelVar", NotFound ), "PeepInfo.ExitLevelVar" );
		Assert.AreEqual( 15, balance.Int( "PeepInfo.StartingCashVarPc", NotFound ), "PeepInfo.StartingCashVarPc" );
		Assert.AreEqual( 100, balance.Int( "PeepInfo.ToiletDesparate", NotFound ), "PeepInfo.ToiletDesparate" );

		// Jungle leaves this at the global default; fantasy and space are the only themes that raise it.
		Assert.AreEqual( 4, balance.Int( "PeepInfo.ExcitementToCostDivisor", NotFound ),
			"PeepInfo.ExcitementToCostDivisor" );

		// A subscripted single-field key, which is the shape the weather keys use.
		Assert.AreEqual( 75, balance.Int( "Seasons[0].AvgWeatherQuality", NotFound ),
			"Seasons[0].AvgWeatherQuality" );
	}
}
