namespace OpenTPW;

/// <summary>
/// Buying something and putting it up, and selling it again - the first thing in this project that
/// changes what a park is made of.
///
/// <para>
/// <b>Everything a park contained used to come from the file and could never be anything else.</b>
/// <see cref="ParkWorld"/> describes a save and is immutable by rule, so the list of placed objects
/// and the type of every cell were fixed the moment a park loaded. <see cref="ParkState"/> now carries
/// both as running state, and this is what moves them.
/// </para>
///
/// <para>
/// <b>The order is the original's.</b> <c>FUN_00528a70</c> validates every cell of the footprint and
/// only then applies, and the money is taken at PLACE time inside the object constructor
/// <c>FUN_004db090</c> from the item's <c>+0x1b8</c> - which is <c>Upgrades[0].CostOfUpgrade</c>, the
/// same number <see cref="ParkItemCatalogue.Item.BuildPrice"/> reads. Nothing is charged for a
/// placement that is refused, which is why the original needs no refund on a cancelled one.
/// </para>
///
/// <para>
/// <b>What is deliberately not built yet, and is counted rather than guessed:</b> the per-cell
/// placement markers (the <c>m_*</c> textures), rotation while carrying, and the age-based scrap
/// curve - see <see cref="Sell"/>.
/// </para>
/// </summary>
public static class ParkBuilding
{
	/// <summary>
	/// The cell type a built thing's footprint stamps. <see cref="CellEdge.Footprint"/> is the same
	/// number read from the other side - the pathing rules - and there is one statement of it.
	/// </summary>
	private const int FootprintType = CellEdge.Footprint;

	/// <summary>
	/// Buys one item and stands it with its anchor on a cell. Answers a line saying what happened,
	/// because every caller so far is something a person is reading.
	/// </summary>
	public static string Buy( int catalogueId, int cellX, int cellY, int angle = 0 )
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state
			|| level.Catalogue is not { } catalogue || ParkObjects.Current is not { } objects )
			return "buy: a park has to be loaded";

		if ( !catalogue.TryGet( catalogueId, out var item ) )
			return $"buy: this theme has no item {catalogueId}";

		// The four the buy list itself refuses - see docs/exe/hud.md. WhichUIType 4 is the game's own
		// "not shown in the UI", which is what the vehicles, the gates and the land tools carry.
		if ( item.UiType is < 0 or > ItemDescriptionFile.Feature )
			return $"buy: '{item.Name}' is UI type {item.UiType}, which no buy tab lists";

		var footprint = ParkObjects.FootprintAt( item, cellX, cellY, angle );

		if ( Refusal( state, footprint ) is { } why )
			return $"buy: '{item.Name}' will not fit at ({cellX},{cellY}) - {why}";

		// The affordability test the original makes when a buy row is clicked, before it ever builds
		// the placement mode.
		if ( state.Balance < item.BuildPrice )
			return $"buy: '{item.Name}' costs {item.BuildPrice} and the park has {state.Balance}";

		var thingId = state.NextThingId();

		var placed = new ParkWorld.CatalogueObject(
			ThingId: thingId, CatalogueId: catalogueId,
			RawX: cellX << 8, RawY: cellY << 8, Angle: angle );

		if ( !objects.PlaceNow( placed, catalogue ) )
			return $"buy: '{item.Name}' would not load, so nothing was built and nothing was charged";

		state.AddObject( placed );
		Stamp( state, footprint, thingId );
		state.Spend( item.BuildPrice );

		// The ground draws every cell nothing else owns, so it has to be told that these are owned now
		// or it keeps drawing grass through the new floor.
		ParkGround.Current?.Rebuild();

		ParkRides.Current?.BindNew( placed, item );

		Log.Info( $"Building: bought '{item.Name}' for {item.BuildPrice} as thing {thingId} at " +
			$"({cellX},{cellY}) turned {angle}, covering ({footprint.Left},{footprint.Top}).." +
			$"({footprint.Right},{footprint.Bottom}) - the park has {state.Balance} left" );

		return $"buy: '{item.Name}' built as thing {thingId} at ({cellX},{cellY}) for {item.BuildPrice}, " +
			$"balance {state.Balance}";
	}

	/// <summary>
	/// Sells something standing in the park and gives the money back.
	///
	/// <para>
	/// <b>The refund is the full price, and that is the original's answer for a NEW object rather than
	/// a simplification.</b> <c>FUN_004dd0a0</c> refunds <c>price * FUN_004e2290( thing ) / 100</c>,
	/// and that percentage is picked from a four-year scrap table by the object's age bucket -
	/// returning a literal <b>100</b> for one that has just been built. The park's own screens call it
	/// "Scrap value".
	/// </para>
	/// <para>
	/// <b>The depreciation itself is NOT built</b>, and is counted rather than guessed: nothing here
	/// keeps a thing's age in the units that table is indexed by. So anything sold refunds in full,
	/// which is right the moment it is built and increasingly wrong afterwards.
	/// </para>
	/// </summary>
	public static string Sell( int thingId )
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state
			|| level.Catalogue is not { } catalogue || ParkObjects.Current is not { } objects )
			return "sell: a park has to be loaded";

		if ( !state.TryObject( thingId, out var placed ) )
			return $"sell: nothing in the park is thing {thingId}";

		if ( !catalogue.TryGet( placed.CatalogueId, out var item ) )
			return $"sell: thing {thingId} is catalogue item {placed.CatalogueId}, which this theme has none of";

		// The age-based scrap percentage, which nothing here can compute - see the remarks. Reported
		// once and counted, rather than a number invented to fill the gap.
		Unimplemented.Report( "SCRAP_VALUE_DEPRECIATION" );

		var refund = item.BuildPrice;
		var footprint = ParkObjects.FootprintAt( item, placed.CellX, placed.CellY, placed.Angle );

		objects.Remove( thingId );
		state.RemoveObject( thingId );

		for ( var y = footprint.Top; y <= footprint.Bottom; ++y )
		{
			for ( var x = footprint.Left; x <= footprint.Right; ++x )
			{
				state.ClearRecord( x, y );

				if ( ParkState.OnMap( x, y ) )
					state.CellAt( x, y ).Occupant = 0;
			}
		}

		state.Refund( refund );

		ParkGround.Current?.Rebuild();

		Log.Info( $"Building: sold '{item.Name}' (thing {thingId}) for {refund} - the park has {state.Balance}" );

		return $"sell: '{item.Name}' thing {thingId} sold for {refund}, balance {state.Balance}";
	}

	/// <summary>
	/// Moves something already standing to another cell. <b>The original does this as demolish then
	/// buy again</b> - the object is genuinely destroyed at pickup and the full price re-charged at
	/// put-down, with the footprint released in between - so this is those two in order rather than a
	/// third mechanism.
	/// </summary>
	public static string Move( int thingId, int cellX, int cellY, int angle = 0 )
	{
		if ( Level.Current?.ParkState is not { } state || !state.TryObject( thingId, out var placed ) )
			return $"move: nothing in the park is thing {thingId}";

		var was = $"({placed.CellX},{placed.CellY})";
		var sold = Sell( thingId );

		if ( !sold.StartsWith( "sell: '" ) )
			return $"move: {sold}";

		var bought = Buy( placed.CatalogueId, cellX, cellY, angle );

		return $"move: from {was} - {bought}";
	}

	/// <summary>
	/// Why a footprint may not go here, or null when it may. Every cell is tested before any is
	/// changed, which is the original's order and the reason a refused placement leaves nothing behind.
	///
	/// <para>
	/// <b>It refuses only what is demonstrably owned by something else</b> - off the map, already built
	/// on, a path, a queue. <b>The terrain rule itself is NOT reproduced</b>, and the first version of
	/// this got it badly wrong by reaching for one that was to hand: <see cref="CellEdge.IsSolid"/>
	/// answers "a guest may not step here", which is a different question, and type 7 alone is
	/// <b>9,077 of Lost Kingdom's 16,384 cells</b> - so using it made more than half the park
	/// unbuildable. Ground a guest cannot walk across is still ground a thing can stand on.
	/// </para>
	/// <para>
	/// The original's per-cell verdict is <c>FUN_00535670</c>, which compares the cell type against
	/// 0, 1, 3, 4, 9, 10, 0x15 and 0x19 - <b>and the adversarial check on that decode recorded that
	/// those labels are not what the disassembly shows</b>. So the rule is counted as unbuilt rather
	/// than guessed at: being more permissive than the original is visible and correctable, where a
	/// wrong rule refuses good ground and looks like the park's own geography.
	/// </para>
	/// </summary>
	private static string? Refusal( ParkState state, (int Left, int Top, int Right, int Bottom) footprint )
	{
		Unimplemented.Report( "PLACEMENT_TERRAIN_RULE" );

		for ( var y = footprint.Top; y <= footprint.Bottom; ++y )
		{
			for ( var x = footprint.Left; x <= footprint.Right; ++x )
			{
				if ( !ParkState.OnMap( x, y ) )
					return $"({x},{y}) is off the map";

				var cell = state.Record( x, y );

				if ( ParkObjects.CoversGround( cell ) )
					return $"({x},{y}) already has something built on it";

				if ( ParkPaths.IsPath( cell ) )
					return $"({x},{y}) is a path";

				if ( ParkQueues.IsQueue( cell ) )
					return $"({x},{y}) is a queue";
			}
		}

		return null;
	}

	/// <summary>Marks every cell of a footprint as built on, and names the thing standing there.</summary>
	private static void Stamp( ParkState state, (int Left, int Top, int Right, int Bottom) footprint, int thingId )
	{
		for ( var y = footprint.Top; y <= footprint.Bottom; ++y )
		{
			for ( var x = footprint.Left; x <= footprint.Right; ++x )
			{
				if ( !ParkState.OnMap( x, y ) )
					continue;

				var cell = state.Record( x, y );

				state.SetRecord( x, y, cell with { Type = FootprintType } );

				// The save's own arrangement: a placed object names itself on its cells, which is what
				// the eleven objects of the shipped park do - see ParkWorld.MapCell.Occupant.
				state.CellAt( x, y ).Occupant = (ushort)thingId;
			}
		}
	}

	/// <summary>Every object standing in the park now, for the debug console.</summary>
	public static IEnumerable<string> Census()
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state )
			yield break;

		foreach ( var placed in state.Objects )
		{
			var name = level.Catalogue is { } catalogue && catalogue.TryGet( placed.CatalogueId, out var item )
				? item.Name
				: $"item {placed.CatalogueId}";

			yield return $"thing {placed.ThingId,3} '{name}' at ({placed.CellX},{placed.CellY}) " +
				$"turned {placed.Angle}{(placed.IsPlaced ? "" : " (not placed)")}";
		}
	}
}
