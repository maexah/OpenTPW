namespace OpenTPW;

/// <summary>
/// A bumper ride's cars as a park file's writer takes them (<c>docs/exe/saves.md</c>, "OpenTPW's writer, a track
/// ride's cars"): each car's record, the record of its model and of its wake's, and each rider's head.
///
/// <para>
/// <b>A car's model is its ride's supplemental mesh, and the record names the mesh's own item</b>: the ride's item
/// plus one plus the mesh's index, so the Hot Pot's boat is 1142 and its wake, mesh nought, 1141
/// (<c>Bumper_LaunchCar</c> hands <c>FUN_00463060</c> the item's <c>+0x4bc</c> entry for the mesh). Measured on the
/// Hot Pot alone, the one bumper ride with a record here.
/// </para>
/// <para>
/// <b>The clip is the drawn boat's</b> where the boats are drawn (<see cref="ParkBumperBoats"/>), and otherwise one
/// started as the file is written, so its first frame; the height is the drawn boat's, and nought with none,
/// counted (<c>SAVE_PARK_CAR_NO_HEIGHT</c>).
/// </para>
/// <para>
/// <b>A smoking car's smoke</b> (<c>docs/exe/saves.md</c>, "OpenTPW's writer, a smoking car") is asked for where
/// the drawn boat's first emitter node stands. A deviation: the original's emitter is where that node stood on
/// the car's last tick, and this is where it is drawn now, within a tick's travel. With no boat drawn the car
/// keeps what its file held and none is started, counted (<c>SAVE_PARK_CAR_SMOKE</c>).
/// </para>
/// </summary>
internal static class ParkCarWriter
{
	/// <summary>What a rider's seat id is looked up under in a car's model (<c>FUN_00549c60</c>).</summary>
	private const uint SeatSpace = 0x80;

	/// <summary>What a car's two emitter nodes are looked up under, ids 2 and 1 (<c>0x0054a1b0</c>).</summary>
	private const uint EmitterSpace = 0x100;

	/// <summary>The car's flags that give it a wake's model (<c>FUN_00543560</c>, chunk 5).</summary>
	private const int WakeFlags = 0x2002000;

	/// <summary>A car's channel before any clip: the launch's stop at speed 1 (<c>FUN_004732a0( model, 12, 0, 2, 1.0 )</c>).</summary>
	private static readonly SavedChannel CarChannel = new( ParkThingStates.NoRole, 0, 0, 1f, 0, 0, 0, ParkThingStates.NoRole, 0, 0, 0f );

	/// <summary>A wake's channel, which nothing starts or stops.</summary>
	private static readonly SavedChannel WakeChannel = new( ParkThingStates.NoRole, 0, 0, 0f, 0, 0, 0, ParkThingStates.NoRole, 0, 0, 0f );

	/// <summary>
	/// The ride of <paramref name="record"/> whole: its cars in pool order and its two lists. Null where the handle
	/// names no ride of the family built here, or the park's clock does not read.
	/// </summary>
	internal static ParkFileWriter.WrittenTrack? Track( ParkBumperCars cars, SavedTrackRide record, ParkItemCatalogue.Item item,
		Func<float, uint>? reading, ParkBumperBoats? boats, BaseFileSystem files )
	{
		if ( cars.RideOf( record.Handle ) is not { } ride || reading is null )
			return null;

		var now = ParkRides.NowMilliseconds;
		var models = new Dictionary<int, ModelFile?>();

		ModelFile? Model( int mesh )
		{
			if ( !models.TryGetValue( mesh, out var model ) )
				models[mesh] = model = Read( item, mesh, files );

			return model;
		}

		var written = new List<ParkFileWriter.WrittenCar>();

		foreach ( var car in cars.CarsOf( record.Handle ) )
		{
			var model = Model( car.Mesh );
			var players = boats?.AnimationsOf( car.Index ) ?? Players( item, car, model, files, now );
			var channel = players?.Channel( 0 ) is { } running ? ParkRides.WrittenChannel( running, CarChannel, reading, now ) : CarChannel;

			ParkThingStates.ModelTables? tables = null;

			if ( model != null && players?.Nodes is { } nodes && nodes.Count == model.Nodes.Count )
			{
				var words = nodes.Words( players.NodeFrames() );

				if ( words.Length > 0 )
					words[0] |= ParkModelTables.FirstNodeMarked;

				tables = new ParkThingStates.ModelTables( words, ParkModelTables.Lookups( model, doHeadProcessing: false ), ParkModelTables.SharedFlags( model ) );
			}
			else
				Unimplemented.Report( "SAVE_PARK_MADE_MODEL_TABLES" );

			int Record( int id, uint space ) => model?.FindNode( id, space ) is { } node and >= 0 ? node - model.LookupFirst : -1;

			float height = 0f;

			if ( boats?.HeightOf( car.Index ) is { } drawn )
				height = drawn;
			else
				Unimplemented.Report( "SAVE_PARK_CAR_NO_HEIGHT" );

			ParkFileWriter.CarModel? wake = null;

			if ( ((int)car.Flags & WakeFlags) != 0 )
			{
				ParkThingStates.ModelTables? wakeTables = null;

				if ( Model( 0 ) is { } trail )
				{
					var words = ParkModelTables.NodeWordsAtRest( trail );

					if ( words.Length > 0 )
						words[0] |= ParkModelTables.FirstNodeMarked;

					wakeTables = new ParkThingStates.ModelTables( words, ParkModelTables.Lookups( trail, doHeadProcessing: false ), ParkModelTables.SharedFlags( trail ) );
				}
				else
					Unimplemented.Report( "SAVE_PARK_MADE_MODEL_TABLES" );

				wake = new ParkFileWriter.CarModel( record.ItemId + 1, WakeChannel, wakeTables );
			}

			// A smoking car's smoke rises from its first emitter node, where the boat is drawn: each of the three
			// times 1024 and cut to a whole number (0x00544daa). With no boat drawn there is no place to put it.
			ParkParticles.Spawn? smoke = null;

			if ( car.Smoking )
			{
				if ( model?.FindNode( 2, EmitterSpace ) is { } node and >= 0 && boats?.NodeAt( car.Index, node ) is { } at )
					smoke = new ParkParticles.Spawn( ParkBumperCars.SmokeEffect, (int)(at.X * 1024f), (int)(at.Z * 1024f), (int)(at.Y * 1024f) );
				else
					Unimplemented.Report( "SAVE_PARK_CAR_SMOKE" );
			}

			written.Add( new ParkFileWriter.WrittenCar(
				cars.Written( car, height, (Record( 2, EmitterSpace ), Record( 1, EmitterSpace )) ),
				new ParkFileWriter.CarModel( record.ItemId + 1 + car.Mesh, channel, tables ), wake,
				[.. car.Riders.Select( rider => (Record: Record( rider.Seat, SeatSpace ), Visitor: rider.Peep) ).Where( head => head.Record >= 0 )],
				smoke ) );
		}

		return new ParkFileWriter.WrittenTrack( record, written, [.. ride.Leaving], [.. ride.Boarding] );
	}

	/// <summary>A supplemental mesh's model as its file gives it, or null where the item names none or it will not read.</summary>
	private static ModelFile? Read( ParkItemCatalogue.Item item, int mesh, BaseFileSystem files )
	{
		if ( item.SupplementalMeshes is not { } meshes || mesh < 0 || mesh >= meshes.Count || meshes[mesh] is not { } file )
			return null;

		try
		{
			using var stream = files.OpenRead( $"{item.Directory}/{Path.GetFileNameWithoutExtension( file )}.MD2" );

			return stream is null ? null : new ModelFile( stream );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Save: {item.Directory}/{file} would not read, so its car's model declares no tables - {e.Message}" );
			return null;
		}
	}

	/// <summary>A car's clips with nothing drawn: its model's, the clip its car plays started as the file is written.</summary>
	private static RideAnimations? Players( ParkItemCatalogue.Item item, ParkBumperCars.Car car, ModelFile? model, BaseFileSystem files, int now )
	{
		if ( model is null || item.SupplementalMeshes?[car.Mesh] is not { } file )
			return null;

		try
		{
			var players = RideAnimations.Load( item.Directory, Path.GetFileNameWithoutExtension( file ), files );

			players.Nodes = new ParkModelTables.Running( model, 1 );

			if ( car.Animation != RideAnimations.NoRole )
				players.Trigger( car.Animation, 0, AnimTimeControl.LoopFlag | AnimTimeControl.StartAtOnceFlag, 1f, now );

			players.Advance( now );

			return players;
		}
		catch ( Exception e )
		{
			Log.Warning( $"Save: the clips of {item.Directory}/{file} would not read - {e.Message}" );
			return null;
		}
	}
}
