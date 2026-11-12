namespace OpenTPW.UI;

/// <summary>
/// The sideshow's window, its first piece: what the window loads from a sideshow, the ranges its three controls hold,
/// what its apply writes back, what apply to all and the arrows reach, and the figures its refresh reads
/// (<c>docs/exe/hud.md</c>, "The shop, sideshow and ride windows"). The debug console's <c>sideshowwindow</c> reads and
/// applies them.
///
/// <para>
/// <b>The window itself is not drawn yet</b> (Q1520): its layout is the stream at <c>0x00755e80</c>, built by
/// <c>FUN_004b2430</c> with the handler <c>FUN_004b2150</c> (<c>docs/exe/park-engine.md</c>, "The per-object management screen
/// is nine screens"), which no page has walked, and which control letters which figure is read only for the four
/// below. A sideshow's click still counts <c>SIDESHOW_WINDOW</c>.
/// </para>
///
/// <para>
/// <b>The settings are the window's buffered values</b> (the window object <c>0x007cc298</c>'s <c>+0x24</c> chance,
/// <c>+0x28</c> prize, <c>+0x2c</c> price), written to the sideshow by the apply, vtable <c>+0x3c</c> (<c>0x004b20b0</c>).
/// </para>
/// </summary>
internal static class ParkSideshowSettings
{
	/// <summary>The sideshows' UI type (<c>+0x4ac</c>), the arrows' and apply to all's class.</summary>
	internal const int UiType = 2;

	/// <summary>The chance of winning's slider <c>0xa08e</c>, 0 to 100.</summary>
	internal const int MostChance = 100;

	/// <summary>The prize's box <c>0xa090</c>, 0 to 10000.</summary>
	internal const int MostPrize = 10000;

	/// <summary>The price's spinner <c>0xa08f</c>, 0 to 500.</summary>
	internal const int HighestPrice = 500;

	/// <summary>How many finished days the month's figures add up, as the shop window's (<c>0x1e</c>).</summary>
	private const int MonthDays = 30;

	/// <summary>The window's three buffered values.</summary>
	internal readonly record struct Settings( int Chance, int Prize, int Price )
	{
		/// <summary>Each held to its control's range, as the slider, the box and the spinner hold what they are set to.</summary>
		internal Settings Held()
			=> new( Math.Clamp( Chance, 0, MostChance ), Math.Clamp( Prize, 0, MostPrize ), Math.Clamp( Price, 0, HighestPrice ) );
	}

	/// <summary>
	/// The load, vtable <c>+4</c> (<c>0x004b1ee0</c>): <c>0xa08e</c> the chance of winning (<c>+0x190</c>, the byte the
	/// win roll reads), <c>0xa090</c> the prize, the cost of goods (<c>+0x188</c>), and <c>0xa08f</c> the price (<c>+0x194</c>).
	/// </summary>
	internal static Settings Loaded( ParkWorld.CatalogueObject sideshow )
		=> new( sideshow.ChanceOfWinning & 0xff, sideshow.CostOfGoods, sideshow.PricePerUse );

	/// <summary>
	/// The apply, vtable <c>+0x3c</c> (<c>0x004b20b0</c>): the price to <c>+0x194</c> (<c>0x004e2770</c>), the chance cut to
	/// its low byte into <c>+0x190</c> (<c>0x004e21c0</c>) and the prize to <c>+0x188</c> (<c>0x004e1a20</c>).
	/// </summary>
	/// <remarks>
	/// <b>A deviation no shipped item reaches:</b> the original writes the chance and the prize only where the item's
	/// <c>UsageInfo.NonWinnableSideshow</c> (<c>+0x10c</c>) is nought, and hides their controls otherwise. No shipped
	/// <c>.sam</c> sets it (<c>hud.md</c>), so that branch is dead by content and the key is not read here.
	/// </remarks>
	internal static ParkWorld.CatalogueObject Applied( ParkWorld.CatalogueObject sideshow, Settings settings )
		=> sideshow with
		{
			PricePerUse = settings.Price,
			ChanceOfWinning = settings.Chance & 0xff,
			CostOfGoods = settings.Prize
		};

	/// <summary>The apply on one thing: false where the park has no such thing, or it already holds the settings.</summary>
	internal static bool ApplyTo( ParkState state, int thingId, Settings settings )
	{
		if ( !state.TryObject( thingId, out var sideshow ) )
			return false;

		var applied = Applied( sideshow, settings );

		if ( applied == sideshow )
			return false;

		state.ReplaceObject( applied );

		Log.Info( $"Sideshow window: thing {thingId} set to chance {applied.ChanceOfWinning}, prize {applied.CostOfGoods}, price {applied.PricePerUse}" );

		return true;
	}

	/// <summary>
	/// Apply to all's reach, <c>0xa0a9</c> (<c>0x004b23b3</c>: <c>0x00482040</c> on mask <c>0x100</c>, then <c>0x004b20f0</c>):
	/// the placed sideshows of the shown one's item, itself among them.
	/// </summary>
	internal static int[] OfItem( IEnumerable<ParkWorld.CatalogueObject> objects, ParkItemCatalogue catalogue, int catalogueId )
		=> ParkObjectWindow.OfItem( objects, catalogue, catalogueId, UiType );

	/// <summary>
	/// What the arrows walk, newest first (<see cref="ParkObjectWindow.Siblings"/> on UI type 2); both arrows,
	/// <c>0xa08c</c> and <c>0xa08d</c>, are hidden where it holds fewer than two.
	/// </summary>
	internal static List<int> Siblings( ParkState state, ParkItemCatalogue catalogue )
		=> ParkObjectWindow.Siblings( state, catalogue, UiType );

	/// <summary>
	/// The figures the refresh (<c>FUN_004b16c0</c>) reads, in their own units, not yet as its controls letter them:
	/// the customers of the last thirty finished days "of" those plus the walk-aways (C of M); the thirty days' takings
	/// (<c>0xa093</c>) and profit (<c>FUN_004e1c70</c>, <c>0xa092</c>); the satisfaction (<c>FUN_004e1e30</c>, the gauge
	/// <c>0xa094</c>); the winners last month as a percentage (<c>0xa096</c>), null with no customer; the excitement at
	/// the window's buffered settings (<c>FUN_004e0560</c>, <c>0xa099</c>); and the local happiness
	/// (<c>FUN_004cd880( cell, 1 )</c>).
	/// </summary>
	internal readonly record struct Figures( int Customers, int Asked, int Takings, int Profit, int Satisfaction,
		int? WinnersPercent, int Excitement, int LocalHappiness );

	/// <summary>
	/// <see cref="Figures"/> for <paramref name="sideshow"/> with the window's buffered <paramref name="pending"/>.
	/// </summary>
	/// <remarks>
	/// <b>Winners last month</b> is <c>FUN_004e1f20</c>: the served ring's thirty days times a hundred over the customers
	/// ring's (<c>ride-operation.md</c>, the settle-up's step 7). What it answers over no customer is not read, so that
	/// case is counted (<c>SIDESHOW_WINNERS_NO_CUSTOMERS</c>) and given as null.
	/// </remarks>
	internal static Figures FiguresOf( ParkState state, ParkWorld.CatalogueObject sideshow, ParkItemCatalogue.Item item,
		Settings pending, Func<int, float?> happinessOf )
	{
		var rings = state.RingsFor( sideshow.ThingId );
		var customers = rings.Customers.LastDays( MonthDays );
		var asked = unchecked(customers + rings.WalkAways.LastDays( MonthDays ));

		int? winners = null;

		if ( customers != 0 )
			winners = (int)(rings.Served.SumOfLastDays( MonthDays ) * 100 / customers);
		else
			Unimplemented.Report( "SIDESHOW_WINNERS_NO_CUSTOMERS" );

		return new Figures( customers, asked, rings.Takings.LastDays( MonthDays ), rings.ProfitOfLastDays( MonthDays ),
			rings.SatisfactionOfLastDays( MonthDays ), winners,
			ParkRideScore.ExcitementOf( Applied( sideshow, pending ), item ),
			ParkShopWindow.LocalHappiness( state, sideshow.CellX, sideshow.CellY, happinessOf ) );
	}

	/// <summary>The debug console's line for one sideshow with the window's buffered settings.</summary>
	internal static string Census( ParkState state, ParkItemCatalogue catalogue, ParkWorld.CatalogueObject sideshow,
		ParkItemCatalogue.Item item, Settings pending, Func<int, float?> happinessOf )
	{
		var figures = FiguresOf( state, sideshow, item, pending, happinessOf );
		var arrows = Siblings( state, catalogue ).Count >= 2 ? 1 : 0;

		return $"thing {sideshow.ThingId} '{item.Name}' item {item.Id} chance {pending.Chance} prize {pending.Prize} price {pending.Price}"
			+ $" | customers {figures.Customers} of {figures.Asked} takings {figures.Takings} profit {figures.Profit}"
			+ $" satisfaction {figures.Satisfaction} winners {(figures.WinnersPercent is { } won ? $"{won}%" : "none")}"
			+ $" excitement {figures.Excitement} local {figures.LocalHappiness}"
			+ $" | arrows {arrows} same item [{string.Join( ", ", OfItem( state.Objects, catalogue, sideshow.CatalogueId ) )}] (not drawn: Q1520)";
	}
}
