using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The scrap percentage (<c>FUN_004e2290</c>, <see cref="ParkScrap"/>) and what it does to a queue cell's refund
/// (<c>FUN_005367a0</c>'s queue arm, <see cref="ParkPathBuilding.QueueRefund"/>). The figures are the jungle
/// <c>Rides.sam</c>'s: tier nought 50, 30, 20, 10 and tier one 60, 40, 30, 15. In the original the stock Belly
/// Bounce's queue gives 37 a cell back (<c>docs/exe/park-engine.md</c>, "Editing a queue", Q1038).
/// </summary>
[TestClass]
public class ParkScrapTests
{
	private BaseFileSystem data = null!;
	private ParkItemCatalogue catalogue = null!;
	private ParkItemCatalogue.Item ride;
	private Level? previous;

	private static readonly DateTime Now = new( 1999, 6, 1, 12, 0, 0 );

	[TestInitialize]
	public void MountTheGame()
	{
		FileSystem = data = GameData.Required();
		catalogue = new ParkItemCatalogue( "jungle", data );
		ride = catalogue.All.Single( item => item.Name == "Belly Bounce" );
		previous = Level.Current;
	}

	[TestCleanup]
	public void PutTheLevelBack() => Level.Current = previous!;

	private int Percent( TimeSpan age, int customers = 0, int tier = 0 )
		=> ParkScrap.Percent( Now, ParkWorld.BuiltWhen.At( Now - age ), customers, tier, ride );

	/// <summary>The item's table is read from the sam files, tier by tier (<c>+0x188</c>.. plus tier x 0x40).</summary>
	/// <remarks><b>Mutation:</b> reading every tier from slot nought fails the 60/40/30/15 row.</remarks>
	[TestMethod]
	public void TheTableIsTheItemsOwnByTierAndYear()
	{
		CollectionAssert.AreEqual( new[] { 50, 30, 20, 10 }, Enumerable.Range( 1, 4 ).Select( year => ride.ScrapValueAt( 0, year ) ).ToArray() );
		CollectionAssert.AreEqual( new[] { 60, 40, 30, 15 }, Enumerable.Range( 1, 4 ).Select( year => ride.ScrapValueAt( 1, year ) ).ToArray() );
		Assert.AreEqual( 0, ride.ScrapValueAt( 3, 1 ), "past the third tier" );
		Assert.AreEqual( 0, ride.ScrapValueAt( 0, 5 ), "past the fourth year" );
	}

	/// <summary>
	/// <b>100 only while under thirty days old with no customer yet</b> (<c>0x004e2390</c>); the thirtieth day and the first customer each
	/// bring the first year's figure.
	/// </summary>
	/// <remarks><b>Mutations:</b> dropping the age test fails the 30 days row; dropping the customer test fails the customer row; a flat 100 fails the stock row.</remarks>
	[TestMethod]
	public void ANewRideWithNoCustomerIsWorthAll()
	{
		Assert.AreEqual( 100, Percent( TimeSpan.FromDays( 5 ) ) );
		Assert.AreEqual( 100, Percent( TimeSpan.FromDays( 30 ) - TimeSpan.FromSeconds( 1 ) ), "29 days 23:59:59" );
		Assert.AreEqual( 50, Percent( TimeSpan.FromDays( 30 ) ), "the thirtieth day" );
		Assert.AreEqual( 50, Percent( TimeSpan.FromDays( 32 ) ), "the stock ride" );
		Assert.AreEqual( 50, Percent( TimeSpan.FromDays( 5 ), customers: 1 ), "one customer" );
	}

	/// <summary>The year bucket picks the field: 365 days each, the fourth for everything older.</summary>
	/// <remarks><b>Mutation:</b> shifting the buckets by one fails every row.</remarks>
	[TestMethod]
	public void TheYearPicksTheFigure()
	{
		Assert.AreEqual( 50, Percent( TimeSpan.FromDays( 364 ), customers: 5 ) );
		Assert.AreEqual( 30, Percent( TimeSpan.FromDays( 365 ), customers: 5 ) );
		Assert.AreEqual( 30, Percent( TimeSpan.FromDays( 729 ), customers: 5 ) );
		Assert.AreEqual( 20, Percent( TimeSpan.FromDays( 730 ), customers: 5 ) );
		Assert.AreEqual( 10, Percent( TimeSpan.FromDays( 1095 ), customers: 5 ) );
		Assert.AreEqual( 10, Percent( TimeSpan.FromDays( 4000 ), customers: 5 ) );
		Assert.AreEqual( 40, Percent( TimeSpan.FromDays( 400 ), customers: 5, tier: 1 ), "tier one, second year" );
		Assert.AreEqual( 30, Percent( TimeSpan.FromDays( 800 ), customers: 5, tier: 1 ), "tier one, third year" );
		Assert.AreEqual( 60, Percent( TimeSpan.FromDays( 40 ), customers: 5, tier: 1 ), "tier one, first year" );
	}

	/// <summary>
	/// The age is a signed divide toward nought (<c>__alldiv</c>): a stamp less than a year ahead is year nought, and under thirty days
	/// ahead counts as new; further ahead than a year it is bucket -1 or lower, which reaches the fourth year's figure.
	/// </summary>
	[TestMethod]
	public void AStampAheadDividesTowardNought()
	{
		Assert.AreEqual( 100, Percent( -TimeSpan.FromDays( 10 ) ), "ten days ahead, no customer" );
		Assert.AreEqual( 50, Percent( -TimeSpan.FromDays( 200 ) ), "200 days ahead is year nought but not under thirty days" );
		Assert.AreEqual( 10, Percent( -TimeSpan.FromDays( 800 ) ), "a negative bucket falls to the fourth year" );
	}

	private (ParkWorld Park, ParkState State, ParkWorld.CatalogueObject Ride) Stocked( TimeSpan age, int customers, int tier = 0 )
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var park = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( park );
		var built = ParkWorld.BuiltWhen.At( state.CalendarNow - age );
		var thing = new ParkWorld.CatalogueObject( ThingId: 77, CatalogueId: ride.Id, RawX: 19 << 8, RawY: 11 << 8, Angle: 0,
			EntryPos: (ushort)MapStep.CellId( 20, 11 ), ExitPos: (ushort)MapStep.CellId( 20, 14 ), Built: built, UpgradeLevel: tier );

		state.AddObject( thing );

		for ( var visit = 0; visit < customers; ++visit )
			state.RingsFor( 77 ).CountCustomer();

		var level = (Level)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( Level ) );

		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, park );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, state );
		typeof( Level ).GetProperty( nameof( Level.Catalogue ) )!.SetValue( level, catalogue );
		Level.Current = level;

		return (park, state, thing);
	}

	private static ParkWorld.MapCell QueueCellOf( ParkWorld.CatalogueObject thing )
		=> new() { Type = ParkRideChoice.QueueCellType, ParentId = (ushort)MapStep.CellId( thing.RawX >> 8, thing.RawY >> 8 ) };

	/// <summary>
	/// <b>A cell of 75 gives back 37 at the stock ride's 50 per cent</b> (a truncating divide, as the original's), 75 for a ride
	/// bought just now, and 37 again once a customer has come - the three figures Q1038 read in the original.
	/// </summary>
	/// <remarks><b>Mutations:</b> a refund of the whole price fails the stock row; rounding up fails 37; the percentage read from the cell's own thing, not its owner's, finds none and refunds all.</remarks>
	[TestMethod]
	public void AQueueCellRefundsTheOwnersPercentOfItsPrice()
	{
		var (_, stock, stockRide) = Stocked( TimeSpan.FromDays( 32 ), customers: 40 );

		Assert.AreEqual( 37, ParkPathBuilding.QueueRefund( stock, QueueCellOf( stockRide ), 75 ), "the stock ride" );

		var (_, fresh, freshRide) = Stocked( TimeSpan.FromDays( 2 ), customers: 0 );

		Assert.AreEqual( 75, ParkPathBuilding.QueueRefund( fresh, QueueCellOf( freshRide ), 75 ), "bought just now" );

		var (_, used, usedRide) = Stocked( TimeSpan.FromDays( 2 ), customers: 1 );

		Assert.AreEqual( 37, ParkPathBuilding.QueueRefund( used, QueueCellOf( usedRide ), 75 ), "after its first customer" );

		var (_, upgraded, upgradedRide) = Stocked( TimeSpan.FromDays( 100 ), customers: 1, tier: 1 );

		Assert.AreEqual( 45, ParkPathBuilding.QueueRefund( upgraded, QueueCellOf( upgradedRide ), 75 ), "tier one, 60 per cent" );
	}

	/// <summary>
	/// A cell whose ride cannot be found keeps the old whole refund and counts the gap, rather than a made-up figure.
	/// </summary>
	[TestMethod]
	public void ACellWithNoRideRefundsAllAndIsCounted()
	{
		var (_, state, _) = Stocked( TimeSpan.FromDays( 32 ), customers: 1 );
		var before = Unimplemented.Summary.FirstOrDefault( entry => entry.What == "QUEUE_REFUND_OWNER_NOT_FOUND" ).Times;

		Assert.AreEqual( 75, ParkPathBuilding.QueueRefund( state, new ParkWorld.MapCell { Type = ParkRideChoice.QueueCellType, ParentId = (ushort)MapStep.CellId( 90, 90 ) }, 75 ) );
		Assert.AreEqual( before + 1, Unimplemented.Summary.FirstOrDefault( entry => entry.What == "QUEUE_REFUND_OWNER_NOT_FOUND" ).Times );
	}

	/// <summary>
	/// <b>A sale refunds the price at the thing's percentage</b> (<c>FUN_004dd0a0</c>): half the Belly Bounce's 500 once it
	/// has had customers, all of it bought just now with none, 60 per cent at tier one, and the bank's balance steps by the
	/// refund alone.
	/// </summary>
	/// <remarks><b>Mutations:</b> the whole price fails the stock and tier rows; the percentage read after the thing leaves the
	/// state (its rings gone, no customer) refunds the used fresh ride's all; the tier left out fails the tier row.</remarks>
	[TestMethod]
	public void ASaleRefundsThePercentOfThePrice()
	{
		Assert.AreEqual( 500, ride.BuildPrice, "the jungle Belly Bounce's +0x1b8" );

		foreach ( var (age, customers, tier, refund) in new[] { (32, 40, 0, 250), (2, 0, 0, 500), (2, 1, 0, 250), (100, 1, 1, 300), (400, 1, 0, 150) } )
		{
			var (park, state, thing) = Stocked( TimeSpan.FromDays( age ), customers, tier );
			var before = state.Balance;

			var said = ParkBuilding.Sell( state, park, catalogue, null, null, thing.ThingId );

			StringAssert.Contains( said, $"sold for {refund}," );
			Assert.AreEqual( before + refund, state.Balance, $"{age} days, {customers} customers, tier {tier}: the balance's step" );
		}
	}

	/// <summary>The stock park with a kart ride at (30,24) 32 days old and one customer, its bare station laid, on the level the tools read.</summary>
	private (ParkWorld Park, ParkState State, ParkWorld.CatalogueObject Ride, ParkItemCatalogue.Item Item) UsedKarts()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var park = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( park );
		var karts = catalogue.All.First( item => item.TrackType == ItemDescriptionFile.CarTrack && item.HasQueue );
		var thing = ParkBuilding.Constructed( karts, 9000, 30, 24, 0, 0, 0, ParkWorld.BuiltWhen.At( state.CalendarNow - TimeSpan.FromDays( 32 ) ) );

		ParkTrackTool.Forget();
		state.AddObject( thing );
		state.RingsFor( thing.ThingId ).CountCustomer();
		ParkTrackPieces.LayStation( state, karts.TrackType, 30, 24, 0 );

		var level = (Level)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject( typeof( Level ) );

		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, park );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, state );
		typeof( Level ).GetProperty( nameof( Level.Catalogue ) )!.SetValue( level, catalogue );
		typeof( Level ).GetProperty( nameof( Level.Balance ) )!.SetValue( level, new ParkBalance( "jungle", easyMode: true ) );
		Level.Current = level;

		return (park, state, thing, karts);
	}

	/// <summary>
	/// <b>A used kart ride's sale withdraws its two mouth pieces at the ride's percentage</b> (<c>0x00528179</c>), through
	/// <see cref="ParkBuilding.Sell"/> itself: the month's costs step by <c>2 x cost x % / 100</c>, not by the whole price.
	/// </summary>
	/// <remarks><b>Mutation:</b> the sale handing <c>SellBareStation</c> 100 in place of its percentage.</remarks>
	[TestMethod]
	public void AUsedKartRidesSaleTearsItsStationDownAtItsPercentage()
	{
		var (park, state, thing, karts) = UsedKarts();
		var percent = ParkScrap.Percent( state, thing, karts );
		var cost = ParkTrackPieces.PieceCost( Level.Current, karts.TrackType );
		var costs = state.MonthCosts.Total;

		Assert.IsTrue( percent is > 0 and < 100 && cost > 0, $"a used ride at {percent} per cent, a piece of {cost}" );

		var teardown = Unimplemented.Summary.FirstOrDefault( entry => entry.What == "SALE_TRACK_TEARDOWN" ).Times;
		var said = ParkBuilding.Sell( state, park, catalogue, null, null, thing.ThingId );

		Assert.AreEqual( teardown, Unimplemented.Summary.FirstOrDefault( entry => entry.What == "SALE_TRACK_TEARDOWN" ).Times, $"the bare station is torn down: {said}" );
		Assert.AreEqual( ParkScrap.Value( 2 * cost, percent ), state.MonthCosts.Total - costs, $"the two pieces withdrawn at {percent} per cent: {said}" );
	}

	/// <summary>
	/// <b>Backspace on a used kart ride refunds each piece at the ride's percentage</b> (<c>0x0053b603</c>), through
	/// <see cref="ParkTrackTool.Backtrack"/> and the level it reads, not only <see cref="ParkTrackTool.BacktrackOn"/>.
	/// </summary>
	/// <remarks><b>Mutation:</b> <c>Backtrack</c> handing <c>BacktrackOn</c> 100 in place of the ride's percentage.</remarks>
	[TestMethod]
	public void BackspaceOnAUsedKartRideRefundsItsPercentage()
	{
		var (_, state, thing, karts) = UsedKarts();
		var percent = ParkScrap.Percent( state, thing, karts );
		var cost = ParkTrackPieces.PieceCost( Level.Current, karts.TrackType );
		var owner = (ushort)MapStep.CellId( 30, 24 );

		try
		{
			ParkTrackTool.LayRun( state, 0xb, owner, (34, 25), (34, 29), cost );
			ParkBuildMode.ArmAt( ParkBuildMode.KartTrack, thing.ThingId, 34, 29 );

			foreach ( var at in new[] { (34, 25), (34, 25), (34, 29) } )
				ParkBuildMode.Push( at.Item1, at.Item2 );

			var before = state.Balance;
			var said = ParkTrackTool.Backtrack();
			var match = System.Text.RegularExpressions.Regex.Match( said, @"(\d+) piece\(s\) removed, (\d+) refunded" );

			Assert.IsTrue( match.Success, said );

			var removed = int.Parse( match.Groups[1].Value );

			Assert.IsTrue( removed > 0, said );
			Assert.AreEqual( removed * ParkScrap.Value( cost, percent ), int.Parse( match.Groups[2].Value ), said );
			Assert.AreEqual( before + (removed * ParkScrap.Value( cost, percent )), state.Balance, said );
		}
		finally
		{
			ParkBuildMode.Forget();
		}
	}
}
