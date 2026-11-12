using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace OpenTPW.Tests;

/// <summary>
/// The balance reader against the original's parser (Q54; <c>docs/exe/park-engine.md</c>, "How a key finds its global").
/// An array's count is the highest subscript the files wrote, plus one, not its table's size, and an arriving guest's
/// kind is drawn over that count (<see cref="ParkBalance.RowsOf"/>, <see cref="ParkPeople.GuestTypesIn"/>). The
/// parser's other rule, that the first refused line ends the file, waits on the balance table's decode (Q1204); what
/// the shipped files show about its case-sensitive key search is measured here.
/// </summary>
[TestClass]
public class BalanceRowCountTests
{
	[TestCleanup]
	public void LetThePeopleGo()
	{
		TestRun.DeleteEvery<ParkPeople>();
		Unimplemented.Forget();
	}

	/// <summary>Every balance file the game ships, global and per theme, standard, easy and online.</summary>
	private static readonly string[] ShippedBalanceFiles =
	[
		"levels/Standard.sam", "levels/Online_Standard.sam",
		"levels/jungle/Standard.sam", "levels/jungle/Easy_Standard.sam", "levels/jungle/Online_Standard.sam",
		"levels/fantasy/Standard.sam", "levels/fantasy/Online_Standard.sam",
		"levels/hallow/Standard.sam", "levels/hallow/Online_Standard.sam",
		"levels/space/Standard.sam", "levels/space/Online_Standard.sam",
	];

	private static string Text( string path ) => Encoding.ASCII.GetString( FileSystem.ReadAllBytes( path ) );

	/// <summary>
	/// Two subscripts written, nought and five: the original's count is 6. A table-size rule says 8 and a rule
	/// counting the rows written says 2. A later layer writing a higher subscript raises it, as one object's merge does.
	/// Written falling (five, then nought) it is still 6, and a later layer's lower subscript leaves it: the original keeps
	/// the highest (<c>0x00401aa7</c>), where a rule taking the last subscript seen says 1 and 2.
	/// </summary>
	[TestMethod]
	public void AnArraysRowsAreTheHighestSubscriptWrittenPlusOne()
	{
		var two = ParkBalance.FromText( "PeepTypes[0].StartingCash 300\nPeepTypes[5].StartingCash 400\nPeepInfo.ExitLevel 120\n" );
		Assert.AreEqual( 6, two.RowsOf( "PeepTypes" ) );
		Assert.AreEqual( 0, two.RowsOf( "PeepInfo" ), "a group with no subscript has no rows" );
		Assert.AreEqual( 0, two.RowsOf( "Seasons" ), "a group no layer writes has no rows" );

		var layered = ParkBalance.FromText( "PeepTypes[0].StartingCash 300\n", "PeepTypes[6].PreferredExcitement.StartingCash 40 500\n" );
		Assert.AreEqual( 7, layered.RowsOf( "PeepTypes" ) );

		var falling = ParkBalance.FromText( "PeepTypes[5].StartingCash 400\nPeepTypes[0].StartingCash 300\n" );
		Assert.AreEqual( 6, falling.RowsOf( "PeepTypes" ), "subscripts written falling" );

		var lowerLater = ParkBalance.FromText( "PeepTypes[6].StartingCash 400\n", "PeepTypes[1].StartingCash 300\n" );
		Assert.AreEqual( 7, lowerLater.RowsOf( "PeepTypes" ), "a later layer writing a lower subscript" );
	}

	/// <summary>
	/// Every shipped stack comes to 8 kinds, and all 8 come from the global <c>Standard.sam</c>: the jungle's own file
	/// and its <c>Easy_Standard.sam</c> write no <c>PeepTypes</c> row, so the count is the global file's subscript 7, plus one.
	/// </summary>
	[TestMethod]
	public void EveryShippedStackHasEightGuestKindsFromTheGlobalFile()
	{
		FileSystem = GameData.Required();

		foreach ( var theme in new[] { "jungle", "fantasy", "hallow", "space" } )
			Assert.AreEqual( 8, new ParkBalance( theme ).RowsOf( "PeepTypes" ), theme );

		Assert.AreEqual( 8, new ParkBalance( "jungle", easyMode: true ).RowsOf( "PeepTypes" ), "jungle, Instant Action" );
		Assert.AreEqual( 8, ParkBalance.FromText( Text( "levels/Standard.sam" ) ).RowsOf( "PeepTypes" ), "the global file alone" );
		Assert.AreEqual( 0, ParkBalance.FromText( Text( "levels/jungle/Standard.sam" ), Text( "levels/jungle/Easy_Standard.sam" ) )
			.RowsOf( "PeepTypes" ), "the jungle's two files alone" );
	}

	private static ParkPeople People( ParkBalance balance )
	{
		FileSystem = GameData.Required();
		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		return new ParkPeople( world, balance, random: new Random( 1 ), banks: new ParkSpriteBanks( 6, 1, 4 ) );
	}

	private static SortedSet<int> KindsOfArrivals( ParkPeople people, int count )
	{
		var kinds = new SortedSet<int>();

		for ( var n = 0; n < count; ++n )
			kinds.Add( people.Guests[people.Admit( 55 + n % 8, 30 )].PersonType );

		return kinds;
	}

	/// <summary>The guest kinds the loaded Lost Kingdom park draws from: 0 to 7, every one of them, over 400 arrivals.</summary>
	[TestMethod]
	public void LostKingdomsArrivalsAreDrawnFromItsEightKinds()
	{
		var kinds = KindsOfArrivals( People( new ParkBalance( "jungle", easyMode: true ) ), 400 );

		CollectionAssert.AreEqual( new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, kinds.ToArray() );
	}

	/// <summary>
	/// A stack whose highest <c>PeepTypes</c> subscript is 2 draws arrivals over 3 kinds, 0 to 2, each seen; a draw over
	/// the save's 8 kinds would give kinds 3 to 7 too.
	/// </summary>
	[TestMethod]
	public void ArrivalsAreDrawnOverTheBalancesRows()
	{
		FileSystem = GameData.Required();
		var three = ParkBalance.FromText( Text( "levels/Standard.sam" ).Replace( "PeepTypes[3]", "#" ).Replace( "PeepTypes[4]", "#" )
			.Replace( "PeepTypes[5]", "#" ).Replace( "PeepTypes[6]", "#" ).Replace( "PeepTypes[7]", "#" ) );
		Assert.AreEqual( 3, three.RowsOf( "PeepTypes" ) );

		var kinds = KindsOfArrivals( People( three ), 200 );

		CollectionAssert.AreEqual( new[] { 0, 1, 2 }, kinds.ToArray() );
		Assert.IsFalse( Unimplemented.Summary.Any( one => one.What == "BALANCE_PEEP_TYPE_ROWS" ) );
	}

	/// <summary>A stack with no <c>PeepTypes</c> row, which no shipped one is, draws over the 8 kinds and is counted.</summary>
	[TestMethod]
	public void NoKindRowsDrawsOverEightAndIsCounted()
	{
		Unimplemented.Forget();

		Assert.AreEqual( 8, ParkPeople.GuestTypesIn( ParkBalance.FromText( "PeepInfo.ExitLevel 120\n" ) ) );
		Assert.AreEqual( 1, Unimplemented.Summary.Single( one => one.What == "BALANCE_PEEP_TYPE_ROWS" ).Times );
		Assert.AreEqual( 8, ParkPeople.GuestTypesIn( null ), "no balance at all, as a test's park has" );
	}

	/// <summary>The keys whose spelling differs from another's only in case, across the files given.</summary>
	private static List<string> SpellingsThatClash( IEnumerable<string> texts )
		=> texts.SelectMany( text => new SAMParser( text ).Parse() )
			.Select( pair => pair.Key ).Distinct( StringComparer.Ordinal )
			.GroupBy( key => key, StringComparer.OrdinalIgnoreCase )
			.Where( spellings => spellings.Count() > 1 )
			.Select( spellings => string.Join( " / ", spellings ) )
			.ToList();

	/// <summary>
	/// The original finds a key case-sensitively (<c>FUN_00401280</c>), this reader's stack ignores case. No key across
	/// the eleven shipped balance files is spelled two ways, so the two read the same keys from them. The check itself
	/// is shown to find a clash.
	/// </summary>
	[TestMethod]
	public void NoShippedBalanceKeyIsSpelledTwoWays()
	{
		FileSystem = GameData.Required();

		CollectionAssert.AreEqual( Array.Empty<string>(), SpellingsThatClash( ShippedBalanceFiles.Select( Text ) ) );
		Assert.AreEqual( 1, SpellingsThatClash( ["PeepInfo.ExitLevel 120\n", "peepinfo.ExitLevel 90\n"] ).Count );
	}
}
