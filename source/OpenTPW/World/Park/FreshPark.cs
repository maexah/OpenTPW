namespace OpenTPW;

/// <summary>
/// Full Simulation's initial world, built from the catalogue and terrain, with no save-file dependency.
/// FUN_005156a0 and FUN_0052f050; docs/exe/park-engine.md, "A fresh Full Simulation world".
/// Running changes belong to ParkState; this seed is read-only after construction.
/// </summary>
public sealed class FreshPark : IParkInitialState
{
	public ParkWorld? Save => null;
	public IReadOnlyList<ParkThingIdentity> Things { get; }
	public IReadOnlyList<ParkWorld.CatalogueObject> Objects { get; }
	public ParkWorld.EconomyState? Economy { get; }
	public ParkWorld.StaffHqState? StaffHq { get; } = new( new int[5] );
	public IReadOnlyList<ParkWorld.Person> People => [];
	public IReadOnlyList<ParkWorld.Sprite> Sprites => [];
	public IReadOnlyList<ParkWorld.MapCell> Cells => _cells;
	private readonly ParkWorld.MapCell[] _cells = new ParkWorld.MapCell[ParkWorld.MapSize * ParkWorld.MapSize];
	public int ParkGates => 11;
	public int TrafficLights => 12;
	public int FirstObject => TrafficLights;
	public int RandomSeed { get; } = unchecked((int)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
	public int Weather => 0;
	public int ParkClosed => 1;
	public int GameTick => 0;
	public int BankAccount => 8;
	public int NumberOfVisitorsToDate => 0;
	public int WorldState => 0;
	public int ThingCount => Things.Count;
	public int ArrivalVehicleForSmallCrowd => 0;
	public int ArrivalVehicleForMediumCrowd => 0;
	public int ArrivalVehicleForLargeCrowd => 0;
	public int CurrentArrivalVehicle => 0;
	public ParkWorld.ArrivalBlock Arrival => new( 0, 0, 5, 0, false, true );
	public IReadOnlyList<ParkWorld.ObjectControl> ObjectControlRecords { get; }

	public FreshPark( string theme, ParkBalance balance, ParkItemCatalogue catalogue, BaseFileSystem? files = null )
	{
		files ??= FileSystem;
		// Allocation order reserves the advisor (3) and challenge manager (10) even though neither draws.
		int[] models = [9, 10, 11, 12, 13, 14, 15, 16, 17, 19, 3, 3];
		Things = Array.AsReadOnly( models.Select( (model, at) => new ParkThingIdentity( at + 1, model ) ).ToArray() );
		Objects = Array.AsReadOnly( new[] { FixedObject( catalogue, "gates", ParkGates, 0 ),
			FixedObject( catalogue, "lights", TrafficLights, ParkGates ) } );
		ObjectControlRecords = Array.AsReadOnly( catalogue.All
			.Select( item => new ParkWorld.ObjectControl( item.Id, item.ResearchCost == 0, 0,
				// The two fixed objects are counted as the original's constructor counts them, made on tick nought.
				Objects.Count( placed => placed.CatalogueId == item.Id ) ) ).ToArray() );

		var loans = Enumerable.Range( 0, ParkWorld.EconomyState.LoanSlots ).Select( at => Loan( balance, at ) ).ToArray();
		Economy = new( balance.Int( "BankAccountInfo.InitialAdmissionFee" ), balance.Int( "BankAccountInfo.InitialCash" ),
			0, 1, 0, 0, 0, Array.AsReadOnly( loans ) );

		using var ground = files.OpenRead( $"levels/{theme}/terrain/base.MD2" );
		using var attributes = files.OpenRead( $"levels/{theme}/terrain/base.map" );
		InitializeCells( new HeightfieldFile( ground ), new AttributeMapFile( attributes ), balance );
		using var hoardings = files.OpenRead( $"levels/{theme}/Hoardings.sam" );
		FreshParkBoundary.Apply( _cells, balance, new SettingsFile( hoardings ) );
	}

	private static ParkWorld.CatalogueObject FixedObject( ParkItemCatalogue catalogue, string stem, int id, int next )
	{
		var item = catalogue.All.First( item => item.Stem.Equals( stem, StringComparison.OrdinalIgnoreCase ) );
		return new( id, item.Id, 128, 128, 0, EntryPos: 1, NextObject: (ushort)next, RideScript: id - 10,
			State: 3, TopLeft: 1, CanLoad: 1, ExitPos: 1, IsTrackRideValid: 1,
			OperatingCapacity: item.InitCapacity, OperatingDuration: item.InitDuration,
			OperatingSpeed: item.InitSpeed, PricePerUse: item.InitPricePerUse,
			// Object-owned settings seeded by FUN_004db090; docs/exe/ride-operation.md, Q97.
			CostOfGoods: item.CostOfGoods, ChanceOfWinning: item.ChanceOfWinning,
			StateOfRepair: 100, RemainingLife: 100, Built: ParkWorld.BuiltWhen.At( GameCalendar.Epoch ),
			MeshInstance: id - 10, Rings: EmptyRings(), QualityOfGoods: 50, AmountOfSpecialIngredient: 50 );
	}

	private static ParkWorld.ObjectRings EmptyRings()
	{
		ParkWorld.DayRing Ring() => new( -1, 30, false, 0, Array.AsReadOnly( new int[30] ) );
		return new( Ring(), Ring(), 0, Ring(), 0, Ring(), Ring(), Ring() );
	}

	private static ParkWorld.LoanState Loan( ParkBalance balance, int slot )
	{
		var key = $"LoanInfo[{slot}]";
		var amount = balance.Int( $"{key}.LoanAmount" );
		var apr = balance.Int( $"{key}.APRInPercent" );
		var months = balance.Int( $"{key}.RepaymentPeriodInMonths" );
		// Unsigned operands, double intermediates in instruction order, then __ftol truncation.
		var repayment = unchecked((int)((uint)amount * Math.Pow( (uint)apr * .01 + 1,
			(uint)months * (1.0 / 12) * .5 ) / (uint)months));
		return new( (uint)amount <= 10000 ? 1 : 0, amount, apr, months, repayment, 0, 0,
			balance.Int( $"{key}.Lendername" ) );
	}

	public ParkWorld.MapCell CellAt( int x, int y )
		=> ParkState.OnMap( x, y ) ? _cells[y * ParkWorld.MapSize + x] : default;

	private void SetCell( int x, int y, ParkWorld.MapCell cell ) => _cells[y * ParkWorld.MapSize + x] = cell;

	private void InitializeCells( HeightfieldFile ground, AttributeMapFile map, ParkBalance balance )
	{
		var offsetX = balance.Int( "MapInfo.HeightfieldXStart" );
		var offsetY = balance.Int( "MapInfo.HeightfieldYStart" );
		for ( var y = 0; y < ParkWorld.MapSize; ++y )
		{
			for ( var x = 0; x < ParkWorld.MapSize; ++x )
			{
				var attr = map.At( x + offsetX, y + offsetY );
				var buildable = x < ground.CellsX && y < ground.CellsY && (ground.FlagsAt( x, y ) & 1) == 0;
				var type = buildable ? ((attr & 8) != 0 ? 1 : 0) : (attr & 2) != 0 ? 2 : (attr & 0x80) != 0 ? 30 : 7;
				var flags = (ushort)((type == 1 ? 0x20 : 0x40) | ((attr & 0x10) != 0 ? 0x80 : 0)
					| ((attr & 4) != 0 ? 0x400 : 0));
				SetCell( x, y, new( type, flags, 0, 0, 0, 0, 0, 3, TrackType: buildable ? 0 : 7,
					StatusFlags: attr, Occupant: (ushort)(x == 0 && y == 0 ? TrafficLights : 0) ) );
				if ( type == 1 )
					ParkPathNeighbours.LinkPath( CellAt, SetCell, x, y );
			}
		}
		// The existing path builder uses the base art too: the original carries an RNG coin between
		// retile calls. Its startup RNG sequence is not reproduced; geometry and connectivity agree.
		Unimplemented.Report( "FRESH_PARK_PATH_ART_VARIANT" );
		for ( var i = 0; i < _cells.Length; ++i )
		{
			var cell = _cells[i];
			var tile = ParkPathTiles.TileFor( cell.Type, cell.Neighbours, cell.Direction );
			_cells[i] = cell with { TileSet = tile.Set, TileIndex = tile.Index, TileAngle = tile.Angle };
		}
	}

	public string Census() => $"fresh park: {ThingCount} things, {Objects.Count} objects, {People.Count} peeps, "
		+ $"{Cells.Count} cells; closed {ParkClosed}, fee {Economy!.Value.AdmissionFee}, cash {Economy.Value.Balance}; "
		+ $"gate {ParkGates}, lights {TrafficLights}; APR [{string.Join( ",", Economy.Value.Loans.Select( l => l.AprPercent ) )}]; "
		+ $"repayments [{string.Join( ",", Economy.Value.Loans.Select( l => l.MonthlyRepayment ) )}]";
}
