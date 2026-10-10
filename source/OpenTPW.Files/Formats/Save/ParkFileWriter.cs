using System.Buffers.Binary;
using System.IO.Compression;

namespace OpenTPW;

/// <summary>
/// Writes a park file from the one a park was loaded from (<c>docs/exe/saves.md</c>, "The writer" and "Module by
/// module"): the original's is <c>FUN_00414920</c>, the container, over <c>FUN_004164c0</c>, the body.
///
/// <para>
/// <b>Every module is carried</b>: the body goes out as the file's own, with the fields under <see cref="Running"/>
/// written over it, the park's people in place of the file's, its staff pool and arrival timer, and each object,
/// script and model the file holds as it runs, under the clock moved on (<see cref="RunningThings"/>). An object
/// bought since the load is written whole, its three records made, and one sold is taken out
/// (<see cref="MadeThing"/>); a track ride's record goes into the track-rides module or out of it with them
/// (<see cref="ParkTrackRides.Splice"/>); a queue cell laid or tiled again names a model made for it, and one
/// cleared names none (<see cref="QueuePiece"/>).
/// </para>
/// <para>
/// <b>The container</b> is the version, 500 (<c>0x006fd928</c>), whatever the file loaded carried; the rest of that
/// file's preamble, where the original writes its running language's legal text; a fresh <c>BILZ</c> header; and the
/// body deflated.
/// </para>
/// <para>
/// <b>A deviation: the stream is not the original's, byte for byte.</b> The original deflates at memory level 9
/// (<c>deflateInit2_( strm, -1, 8, 15, 9, 0, ... )</c>), which <see cref="ZLibStream"/> cannot be asked for. The
/// header still reads 15 and 9, and the original loads the file: its loader needs a stream that inflates to the
/// length the header gives.
/// </para>
/// </summary>
public static class ParkFileWriter
{
	/// <summary>The version every park file the original writes opens with (<c>0x006fd928</c>).</summary>
	private const int Version = 500;

	/// <summary>The <c>BILZ</c> header's size, its tag included, which the block's length counts.</summary>
	private const int BlockHeaderSize = 28;

	/// <summary>The header's two dwords after the lengths: the window bits and the memory level the original deflates with.</summary>
	private const int WindowBits = 15;

	private const int MemoryLevel = 9;

	/// <summary>
	/// What of the running park is written over the carried body: <c>mGameTick</c>, <c>mParkClosed</c>,
	/// <c>mNumberOfVisitorsToDate</c>, the economy thing's <c>mBalance</c>, the camera, and the cells to write over
	/// the file's, by their place in <see cref="ParkWorld.Cells"/> (<see cref="ParkWorld.PutCells"/> says which of a
	/// cell's fields); none where it is null. <see cref="People"/> is every guest and member of staff the park
	/// holds, written in place of the file's (<see cref="ParkWorld.PutPeople"/>); the file's own where it is null.
	/// <see cref="StaffPool"/> is the pool of candidates (<see cref="ParkWorld.PutStaffPool"/>) and
	/// <see cref="Arrival"/> the arrival timer (<see cref="ParkWorld.PutArrival"/>), each the file's where it is null.
	/// <see cref="Things"/> is the objects, their scripts and their models as they run; the file's where it is null.
	/// <see cref="LetGo"/> is the balloons let go and still bursting, written with the people; the file's where it is null.
	/// <see cref="Effects"/> is every cell's region effects (<see cref="ParkWorld.PutEffects"/>); the file's where it is null.
	/// </summary>
	public readonly record struct Running( int GameTick, bool ParkClosed, int VisitorsToDate, int Balance,
		ParkCameraModule.View Camera, IReadOnlyDictionary<int, ParkWorld.MapCell>? Cells = null,
		IReadOnlyList<ParkWorld.WrittenPerson>? People = null, ParkWorld.WrittenStaffPool? StaffPool = null,
		ArrivalTimer? Arrival = null, RunningThings? Things = null, IReadOnlyList<ParkWorld.WrittenSprite>? LetGo = null,
		IReadOnlyList<short>? Effects = null );

	/// <summary>
	/// The file's objects, scripts and models as the running park has them (<c>docs/exe/saves.md</c>, "OpenTPW's
	/// writer, the objects"): each written over its own record where it lies.
	/// </summary>
	/// <param name="Objects">Each object's record as it runs (<see cref="ParkWorld.PutObjects"/>).</param>
	/// <param name="SchedulerTick">The script scheduler's tick, the module header's second dword.</param>
	/// <param name="NextHandle">The handle the next new script will be given, its third.</param>
	/// <param name="Scripts">Each running script (<see cref="ParkScriptStates.Put"/>).</param>
	/// <param name="ModelStates">The file's model module, walked with each item's count of channels.</param>
	/// <param name="Models">Each model's channels and hoarding (<see cref="ParkThingStates.Put"/>).</param>
	/// <param name="Clock">
	/// The game clock's reading as the file is written (<see cref="ParkClock.Put"/>): every deadline and stamp in
	/// <paramref name="Scripts"/> and <paramref name="Models"/> is a reading of it.
	/// </param>
	/// <param name="Made">The objects bought since the load, each written whole (<see cref="MadeThing"/>).</param>
	/// <param name="Gone">The file's objects sold since: each one's three records are left out.</param>
	/// <param name="Built">Each touched item's standing count and first-build stamp (<see cref="ParkWorld.PutControls"/>).</param>
	/// <param name="QueueCells">
	/// Each cell whose queue piece is not the file's, by its place in <see cref="ParkWorld.Cells"/>: the piece it
	/// holds now, or null where it holds none (<see cref="QueuePiece"/>).
	/// </param>
	/// <param name="MadeTracks">The track-rides module's record of each track ride in <paramref name="Made"/> (<see cref="ParkTrackRides.Splice"/>).</param>
	/// <param name="GoneTracks">The handle of each track ride in <paramref name="Gone"/>, whose chunks are left out.</param>
	/// <param name="KeptTracks">Each of the file's track rides whose record is written over as it runs (<see cref="ParkTrackRides.Put"/>).</param>
	/// <param name="Tracks">
	/// Each track ride written whole with its cars (<see cref="WrittenTrack"/>), made since the load or the file's:
	/// every chunk the file holds under its handle is taken out and the ride put in again as it runs.
	/// </param>
	public sealed record RunningThings( IReadOnlyList<ParkWorld.CatalogueObject> Objects, int SchedulerTick,
		int NextHandle, IReadOnlyList<WrittenScript> Scripts, ParkThingStates ModelStates,
		IReadOnlyList<WrittenModel> Models, uint Clock, IReadOnlyList<MadeThing>? Made = null,
		IReadOnlySet<int>? Gone = null, IReadOnlyDictionary<int, (int Standing, uint FirstBuilt)>? Built = null,
		IReadOnlyDictionary<int, QueuePiece?>? QueueCells = null, IReadOnlyList<SavedTrackRide>? MadeTracks = null,
		IReadOnlyCollection<int>? GoneTracks = null, IReadOnlyList<SavedTrackRide>? KeptTracks = null,
		IReadOnlyList<WrittenTrack>? Tracks = null );

	/// <summary>One model of a track car, its own or its wake's: the supplemental mesh's item, its one channel and its two tables (null declares none).</summary>
	public sealed record CarModel( int Item, SavedChannel Channel, ParkThingStates.ModelTables? Tables );

	/// <summary>
	/// A track car as it runs (<c>docs/exe/saves.md</c>, "OpenTPW's writer, a track ride's cars"): its record with
	/// the two model handles left nought, its model, its wake's (null for a car with none), and each rider's head by
	/// the lookup record of its seat's node. The writer deals the two models their slots and writes the handles
	/// into the car's <c>+0x08</c> and <c>+0x0c</c>.
	///
	/// <para>
	/// <paramref name="Smoke"/> is the emitter a smoking car's smoke rises from, where its emitter node stands
	/// ("OpenTPW's writer, a smoking car"); null for a car with none. The writer moves the emitter the car's
	/// <c>+0x2c</c> names there, or starts one and writes its handle.
	/// </para>
	/// </summary>
	public sealed record WrittenCar( SavedTrackCar Car, CarModel Model, CarModel? Wake,
		IReadOnlyList<(int Record, int Visitor)> Heads, ParkParticles.Spawn? Smoke = null );

	/// <summary>A track ride written whole: its record, its cars in pool order, and its leaving and boarding lists, head first.</summary>
	public sealed record WrittenTrack( SavedTrackRide Ride, IReadOnlyList<WrittenCar> Cars, IReadOnlyList<int> Leaving,
		IReadOnlyList<int> Boarding );

	/// <summary>
	/// The piece a queue cell holds: its tile's index and angle (<c>docs/exe/saves.md</c>, "OpenTPW's writer, a queue
	/// cell's model"). The original's retile frees the model the cell names and makes this one's in the slot at the
	/// cursor (<c>FUN_005365d0</c>); a cell that leaves the queue frees its own and names none.
	/// </summary>
	public readonly record struct QueuePiece( int TileIndex, int TileAngle );

	/// <summary>
	/// An object bought since the load with its other two records (<c>docs/exe/saves.md</c>, "OpenTPW's writer, a
	/// thing bought and a thing sold"): its footprint in cells for its model's record, its script as it runs (null
	/// for a thing with none), and its model's channels, hoarding and two tables (null declares none), with the
	/// riders' heads hung on its nodes, each written into the tables' lookup records.
	/// </summary>
	public sealed record MadeThing( ParkWorld.MadeObject Object, int Across, int Down, MadeScript? Script,
		IReadOnlyList<SavedChannel> Channels, uint HoardingFlags = 0, float HoardingProgress = 0f,
		ParkThingStates.ModelTables? Tables = null, WrittenHeads? Heads = null );

	/// <summary>What was done with <see cref="RunningThings"/>: the records written over, the script tables left the file's, the things made and taken out, and each queue cell's new handle beside the one it gave up.</summary>
	public readonly record struct ThingsWritten( int Objects, int Scripts, int ScriptTablesLeft, int Models,
		int Made = 0, int Gone = 0, int ModelSlots = 0, IReadOnlyList<(int Cell, int Handle, int Freed)>? QueueCells = null,
		EmittersWritten? Emitters = null );

	/// <summary>
	/// What was done with the particles module (<c>docs/exe/saves.md</c>, "OpenTPW's writer, an emitter started"):
	/// each emitter started with the script whose record names it, each of the file's killed, and how many asked
	/// for were not started, their effect linking an effector or the module unread; and each smoking car's emitter
	/// with its ride's handle, started or the file's kept ("OpenTPW's writer, a smoking car"). Null where there is
	/// none of the four.
	/// </summary>
	public sealed record EmittersWritten( IReadOnlyList<(int Script, ParkParticles.Emitter Emitter)> Started,
		IReadOnlyList<ParkParticles.Emitter> Killed, int NotStarted,
		IReadOnlyList<(int Ride, ParkParticles.Emitter Emitter, bool Kept)>? Smoke = null );

	/// <summary>
	/// The arrival timer as it is written: the <c>mGameTick</c> the next load's wait is counted from
	/// (<c>mTimeSig</c>), how many of a load held are still to drop (<c>mPeopleOnBus</c>), whether one is held
	/// (<c>mOffloading</c>), and the thing of the vehicle that is current (the header's
	/// <c>mCurrentArrivalVehicle</c>), nought with none and null to leave the file's.
	/// </summary>
	public readonly record struct ArrivalTimer( int TimeSig, int PeopleOnBus, bool Offloading, int? CurrentVehicle = null );

	/// <summary>
	/// The inflated body of the file: a copy of <paramref name="loaded"/>'s with <paramref name="running"/> written
	/// over it.
	/// </summary>
	/// <exception cref="InvalidOperationException">
	/// The body was not walked to its end, or holds no economy thing or no camera module, so a field's place in it is
	/// not known; or a cell to write has no record in it.
	/// </exception>
	public static byte[] Body( ParkWorld loaded, Running running ) => Body( loaded, running, out _ );

	/// <inheritdoc cref="Body(ParkWorld, Running)"/>
	/// <param name="people">What was done with the people; null where <see cref="Running.People"/> is.</param>
	public static byte[] Body( ParkWorld loaded, Running running, out ParkWorld.PeopleWritten? people )
		=> Body( loaded, running, out people, out _ );

	/// <inheritdoc cref="Body(ParkWorld, Running)"/>
	/// <param name="people">What was done with the people; null where <see cref="Running.People"/> is.</param>
	/// <param name="things">What was done with the things; null where <see cref="Running.Things"/> is.</param>
	/// <exception cref="InvalidOperationException">
	/// And where <see cref="Running.Things"/> is given: the file's scripts, models or clock were not read whole.
	/// </exception>
	public static byte[] Body( ParkWorld loaded, Running running, out ParkWorld.PeopleWritten? people,
		out ThingsWritten? things )
	{
		ArgumentNullException.ThrowIfNull( loaded );

		if ( loaded.Problem != null )
			throw new InvalidOperationException( $"the park file it was loaded from was not read whole: {loaded.Problem}" );

		if ( !loaded.ClosedOnTrailer || loaded.HeaderAt < 0 )
			throw new InvalidOperationException( "the park file it was loaded from did not end where its world block should" );

		if ( loaded.EconomyAt < 0 )
			throw new InvalidOperationException( "the park file it was loaded from holds no economy thing" );

		var body = (byte[])loaded.Body.Clone();

		PutInt32( body, loaded.HeaderAt + ParkWorld.GameTickAt, running.GameTick );
		PutInt32( body, loaded.HeaderAt + ParkWorld.ParkClosedAt, running.ParkClosed ? 1 : 0 );
		PutInt32( body, loaded.HeaderAt + ParkWorld.NumberOfVisitorsToDateAt, running.VisitorsToDate );
		PutInt32( body, loaded.EconomyAt + ParkWorld.BalanceAt, running.Balance );

		loaded.Camera.Put( body, running.Camera );

		if ( running.Cells is { } cells )
			loaded.PutCells( body, cells );

		if ( running.StaffPool is { } pool )
			loaded.PutStaffPool( body, pool );

		if ( running.Arrival is { } arrival )
			loaded.PutArrival( body, arrival.TimeSig, arrival.PeopleOnBus, arrival.Offloading, arrival.CurrentVehicle );

		things = null;

		ParkWorld.ObjectEdits? edits = null;
		ParkWorld.HeadEdits? heads = null;
		var headSlots = new Dictionary<(int Slot, int Record), int>();
		var madeHeadSlots = new Dictionary<(int Thing, int Record), int>();
		var carHeadSlots = new Dictionary<(int Car, int Record), int>();

		if ( running.Things is { } run )
		{
			// First: a started emitter's handle goes into its script's record, which is written below.
			var emitters = PutEmitters( loaded, body, ref run );

			var objects = loaded.PutObjects( body, run.Objects );
			var scripts = loaded.ScriptStates.Put( body, run.SchedulerTick, run.NextHandle, run.Scripts );

			// A track ride taken out, sold or to be written again, gives up its cars' models and the heads on them.
			var tracks = run.Tracks ?? [];
			HashSet<int> tracksOut = [.. run.GoneTracks ?? [], .. tracks.Select( track => track.Ride.Handle )];

			HashSet<int> carSlots = [.. loaded.TrackRides.CarModelSlots( tracksOut ).Where( slot => run.ModelStates.ItemIn( slot ) != null )];

			var cars = tracks.SelectMany( track => track.Cars ).ToList();

			// The heads are sprites, so they are written with the people or not at all.
			if ( running.People != null )
				heads = PlanHeads( loaded, run, running.People, headSlots, madeHeadSlots, carSlots, cars, carHeadSlots );

			var models = run.ModelStates.Put( body, run.Models, heads != null ? headSlots : null );

			loaded.Clock.Put( body, run.Clock );
			loaded.TrackRides.Put( body, run.KeptTracks ?? [] );
			things = new ThingsWritten( objects, scripts.Scripts, scripts.TablesLeft, models, Emitters: emitters );

			var made = run.Made ?? [];
			var gone = run.Gone ?? new HashSet<int>();

			var pieces = run.QueueCells ?? new Dictionary<int, QueuePiece?>();

			// A kept script whose object list is another length: its record is written again with the rest.
			var relisted = loaded.ScriptStates.Relisted( run.Scripts );

			if ( made.Count > 0 || gone.Count > 0 || pieces.Count > 0 || relisted.Count > 0 || tracksOut.Count > 0 )
			{
				if ( running.People == null && (made.Count > 0 || gone.Count > 0) )
					throw new InvalidOperationException( "a thing bought or sold is written with the people, and none were given" );

				// From the back of the file forwards, so each module is still where the file has it when its turn
				// comes: the scripts, the track rides, the models, then the world with the people.
				var goneSlots = new HashSet<int>( carSlots );
				var goneScripts = new HashSet<int>();

				foreach ( var thing in loaded.Objects.Where( thing => gone.Contains( thing.ThingId ) ) )
				{
					if ( thing.MeshInstance > 0 )
						goneSlots.Add( thing.MeshInstance - 1 );

					goneScripts.UnionWith( loaded.ScriptStates.HandlesOf( thing.ThingId ) );
				}

				// A cell gives up the piece the file has it name, whether it takes another or none. A handle
				// that names no queue piece's record is not the cell's to free, and its slot is left alone.
				var cellsInOrder = pieces.OrderBy( entry => entry.Key ).ToList();
				var freed = new Dictionary<int, int>();

				foreach ( var (cell, _) in cellsInOrder )
				{
					var held = loaded.Cells[cell].MeshInstance;

					if ( held > 0 && run.ModelStates.ItemIn( held - 1 ) is { } item && ParkThingStates.IsQueuePiece( item ) )
					{
						goneSlots.Add( held - 1 );
						freed[cell] = held;
					}
				}

				// The oldest takes the first slot, as it did when it was bought.
				//
				// A deviation: the original gives each model the lowest empty slot as it is made, in the order
				// things were bought and cells tiled. Here the slots are dealt as the file is written: the
				// objects first, the oldest first, then the queue cells in the map's order, then each car's and its
				// wake's in pool order. Every handle still names its own record, which is all a load reads.
				var oldestFirst = made.OrderBy( thing => thing.Object.Object.ThingId ).ToList();
				var laid = cellsInOrder.Where( entry => entry.Value != null ).ToList();
				var carModels = cars.Sum( car => car.Wake != null ? 2 : 1 );
				var slots = run.ModelStates.Plan( goneSlots, oldestFirst.Count + laid.Count + carModels );
				var madeModels = new List<(int Slot, byte[] Record)>();
				var records = new List<(int Id, byte[] Record)>();
				var scriptRecords = new List<(int Handle, byte[] Record)>();

				for ( var i = 0; i < oldestFirst.Count; ++i )
				{
					var thing = oldestFirst[i];
					var placed = thing.Object.Object;
					var handle = thing.Script?.Script.Handle ?? 0;
					var cell = placed.TopLeft != 0 ? placed.TopLeft - 1 : ((placed.RawY >> 8) * ParkWorld.MapSize) + (placed.RawX >> 8);

					madeModels.Add( (slots[i], ParkThingStates.MadeRecord( placed.CatalogueId, cell % ParkWorld.MapSize,
						cell / ParkWorld.MapSize, thing.Across, thing.Down, handle, thing.HoardingFlags,
						thing.HoardingProgress, placed.Angle, thing.Channels,
						heads != null ? WithHeads( thing, madeHeadSlots ) : thing.Tables )) );

					if ( thing.Script is { } script )
						scriptRecords.Add( (handle, ParkScriptStates.MadeRecord( script, slots[i] + 1, loaded.ScriptStates.StructSize )) );

					records.Add( (placed.ThingId, ParkWorld.MadeObjectRecord(
						thing.Object with { Object = placed with { RideScript = handle } }, slots[i] + 1 )) );
				}

				var handles = cellsInOrder.ToDictionary( entry => entry.Key, _ => 0 );

				for ( var i = 0; i < laid.Count; ++i )
				{
					var (cell, piece) = laid[i];
					var slot = slots[oldestFirst.Count + i];

					madeModels.Add( (slot, ParkThingStates.QueuePieceRecord( cell % ParkWorld.MapSize, cell / ParkWorld.MapSize,
						piece!.Value.TileIndex, piece.Value.TileAngle )) );
					handles[cell] = slot + 1;
				}

				// Each car's model and its wake's, and the two handles in the car's own bytes.
				var nextCar = oldestFirst.Count + laid.Count;
				var writtenTracks = new List<WrittenTrackRide>( (run.MadeTracks ?? []).Select( ride => new WrittenTrackRide( ride, [], [], [] ) ) );

				foreach ( var track in tracks )
				{
					var floated = new List<SavedTrackCar>();

					foreach ( var car in track.Cars )
					{
						var index = cars.IndexOf( car );
						var own = slots[nextCar++];
						var bytes = (byte[])car.Car.Bytes.Clone();

						madeModels.Add( (own, ParkThingStates.CarRecord( car.Model.Item, car.Model.Channel,
							heads != null ? WithHeads( car, index, carHeadSlots ) : car.Model.Tables )) );
						PutInt32( bytes, 0x08, own + 1 );

						if ( car.Wake is { } wake )
						{
							var its = slots[nextCar++];

							madeModels.Add( (its, ParkThingStates.CarRecord( wake.Item, wake.Channel, wake.Tables )) );
							PutInt32( bytes, 0x0c, its + 1 );
						}

						var record = new SavedTrackCar( car.Car.Handle, bytes, car.Car.CentreX, car.Car.CentreZ, car.Car.BuoyRide,
							car.Car.BuoyX, car.Car.BuoyZ, 0 );

						record.Riders.AddRange( car.Car.Riders );
						floated.Add( record );
					}

					writtenTracks.Add( new WrittenTrackRide( track.Ride, floated, track.Leaving, track.Boarding ) );
				}

				// Where it lies, so before anything ahead of the map changes length.
				loaded.PutCellModels( body, handles );

				body = loaded.ScriptStates.Splice( body,
					[.. scriptRecords.OrderByDescending( entry => entry.Handle ).Select( entry => entry.Record )], goneScripts,
					relisted );

				body = loaded.TrackRides.SpliceWhole( body, tracksOut, writtenTracks );
				body = run.ModelStates.Splice( body, goneSlots, madeModels );

				if ( run.Built is { } built )
					loaded.PutControls( body, built );

				if ( made.Count > 0 || gone.Count > 0 )
					edits = new ParkWorld.ObjectEdits( records, gone );

				things = things.Value with
				{
					Made = made.Count,
					Gone = gone.Count,
					ModelSlots = slots.Length > 0 ? slots.Max() + 1 : 0,
					QueueCells = [.. cellsInOrder.Select( entry => (entry.Key, handles[entry.Key], freed.GetValueOrDefault( entry.Key )) )]
				};
			}
		}

		people = null;

		// Last: the people change the body's length, and everything above is written where the file has it.
		if ( running.People is { } written )
		{
			body = loaded.PutPeople( body, written, edits, running.LetGo, heads, out var report );
			people = report;
		}

		// The map lies before the thing list, so it is still where the file has it; its length changes last of all.
		if ( running.Effects is { } effects )
			body = loaded.PutEffects( body, effects );

		return body;
	}

	/// <summary>
	/// Makes the file's live emitters follow the scripts' records (<c>docs/exe/saves.md</c>, "OpenTPW's writer, an
	/// emitter started"), and writes the live image where it lies. A particle record the file's script held and the
	/// running one does not, and every one of a script sold with its thing, has its emitter killed as
	/// <c>KILLOBJ</c> and the script's end kill it; each record that asks for an emitter is given one, and its
	/// handle, in <paramref name="run"/>.
	///
	/// <para>
	/// A deviation: the original starts each emitter as its <c>ADDOBJ</c> runs, so its slots and counts are in
	/// the order the scripts ran in. Here they are dealt as the file is written: the kept scripts in the
	/// scheduler's order, then the bought things' oldest first, each list from its oldest record. Every handle
	/// names its own emitter, which is all a load reads. And each is written as it starts: one whose effect has a
	/// life begins it again at the load.
	/// </para>
	/// <para>
	/// A smoking car's emitter follows its car the same way ("OpenTPW's writer, a smoking car"): the file's is
	/// kept and moved, one is started for a car that began to smoke here, and the smoke of a file's car that is
	/// fixed, gone or sold is killed.
	/// </para>
	/// </summary>
	private static EmittersWritten? PutEmitters( ParkWorld loaded, byte[] body, ref RunningThings run )
	{
		var made = run.Made ?? [];
		var tracks = run.Tracks ?? [];
		var asked = run.Scripts.Sum( script => script.Emitters?.Count( spawn => spawn != null ) ?? 0 )
			+ made.Sum( thing => thing.Script?.Script.Emitters?.Count( spawn => spawn != null ) ?? 0 )
			+ tracks.Sum( track => track.Cars.Count( car => car.Smoke != null ) );

		if ( loaded.Particles.Problem != null )
			return asked > 0 ? new EmittersWritten( [], [], asked ) : null;

		var edit = loaded.Particles.Begin();
		var started = new List<(int Script, ParkParticles.Emitter Emitter)>();
		var killed = new List<ParkParticles.Emitter>();
		var notStarted = 0;

		void Kill( IEnumerable<SavedEffect> records )
		{
			foreach ( var record in records.Where( record => record.Type <= LastParticleType ) )
			{
				if ( edit.Kill( record.Handle ) )
					killed.Add( edit.At( record.Handle & 0xffff ) );
			}
		}

		var gone = run.Gone ?? new HashSet<int>();

		foreach ( var thing in loaded.Objects.Where( thing => gone.Contains( thing.ThingId ) ) )
		{
			foreach ( var handle in loaded.ScriptStates.HandlesOf( thing.ThingId ) )
				Kill( loaded.ScriptStates.For( handle )?.Effects ?? [] );
		}

		foreach ( var script in run.Scripts )
		{
			if ( script.Effects is { } now && loaded.ScriptStates.For( script.Handle )?.Effects is { } held )
				Kill( held.Where( record => !now.Any( kept => kept.Type == record.Type && kept.Handle == record.Handle ) ) );
		}

		WrittenScript Started( WrittenScript script )
		{
			if ( script.Effects is not { } effects || script.Emitters is not { } spawns || !spawns.Any( spawn => spawn != null ) )
				return script;

			var records = (SavedEffect[])effects.Clone();

			// The list's head is its newest record.
			for ( var index = Math.Min( records.Length, spawns.Length ) - 1; index >= 0; --index )
			{
				if ( spawns[index] is not { } spawn )
					continue;

				if ( !edit.Starts( spawn.Template ) )
				{
					++notStarted;
					continue;
				}

				var handle = edit.Start( spawn );

				records[index] = records[index] with { Handle = handle };

				if ( handle is not (0 or ParkParticles.NoSlot) )
					started.Add( (script.Handle, edit.At( handle & 0xffff )) );
			}

			return script with { Effects = records };
		}

		run = run with
		{
			Scripts = [.. run.Scripts.Select( Started )],
			Made = made.Count == 0 ? run.Made : [.. made.OrderBy( thing => thing.Object.Object.ThingId )
				.Select( thing => thing.Script is { } script ? thing with { Script = script with { Script = Started( script.Script ) } } : thing )]
		};

		// A smoking car's emitter (FUN_00544c80): the one its file's handle names is put where the car's node
		// stands, as the car's step puts it every tick, and a car that began to smoke here is given one. After the
		// scripts', where the original starts each as its ride breaks.
		var smoke = new List<(int Ride, ParkParticles.Emitter Emitter, bool Kept)>();
		var kept = new HashSet<int>();

		WrittenCar Smoking( WrittenCar car )
		{
			if ( car.Smoke is not { } spawn )
				return car;

			var held = car.Car.Word( SmokeHandleAt );

			if ( held != NoSmoke && edit.At( held & 0xffff ).Template == spawn.Template && edit.Move( held, spawn.X, spawn.Height, spawn.Z ) )
			{
				kept.Add( held );
				smoke.Add( (car.Car.Handle, edit.At( held & 0xffff ), true) );
				return car;
			}

			var handle = edit.Starts( spawn.Template ) ? edit.Start( spawn ) : NoSmoke;

			if ( handle is 0 or ParkParticles.NoSlot )
			{
				++notStarted;
				handle = NoSmoke;
			}
			else
				smoke.Add( (car.Car.Handle, edit.At( handle & 0xffff ), false) );

			var bytes = (byte[])car.Car.Bytes.Clone();

			PutInt32( bytes, SmokeHandleAt, handle );

			var record = new SavedTrackCar( car.Car.Handle, bytes, car.Car.CentreX, car.Car.CentreZ, car.Car.BuoyRide,
				car.Car.BuoyX, car.Car.BuoyZ, car.Car.RidesBefore );

			record.Riders.AddRange( car.Car.Riders );

			return car with { Car = record };
		}

		if ( tracks.Count > 0 )
			run = run with { Tracks = [.. tracks.Select( track => track with { Cars = [.. track.Cars.Select( Smoking )] } )] };

		// The smoke of a file's car whose ride is sold, or written again with the car fixed or gone, is killed
		// as the fix and the car's removal kill it (FUN_00544e50, 0x0054b077).
		HashSet<int> tracksOut = [.. run.GoneTracks ?? [], .. tracks.Select( track => track.Ride.Handle )];

		foreach ( var car in loaded.TrackRides.Cars.Where( car => tracksOut.Contains( car.Handle ) ) )
		{
			var held = car.Word( SmokeHandleAt );

			if ( held != NoSmoke && !kept.Contains( held ) && edit.Kill( held ) )
				killed.Add( edit.At( held & 0xffff ) );
		}

		loaded.Particles.Put( body, edit );

		return started.Count > 0 || killed.Count > 0 || notStarted > 0 || smoke.Count > 0
			? new EmittersWritten( started, killed, notStarted, smoke ) : null;
	}

	/// <summary>Where a car keeps the handle of its smoke's emitter, and what it holds with none.</summary>
	private const int SmokeHandleAt = 0x2c;

	private const int NoSmoke = -1;

	/// <summary>The last of a script record's types that is a particle; the rest are sounds.</summary>
	private const int LastParticleType = 2;


	/// on a lookup record the file has a head on keeps that slot; every other takes the lowest slot the file's
	/// sprite table leaves empty, the kept models' first and then the made things', oldest first. A head the file
	/// has on a record of a head table that holds none now, or on any record of a thing sold, gives its slot up.
	///
	/// <para>
	/// A deviation: the original deals a head the lowest empty slot as it is hung, among the people's as they
	/// come and go. Here a new head takes none the file's table holds, a person gone since or not, so the slots
	/// are not the original's; every lookup record still names its own head's sprite, which is all a load reads.
	/// </para>
	/// </summary>
	private static ParkWorld.HeadEdits PlanHeads( ParkWorld loaded, RunningThings run, IReadOnlyList<ParkWorld.WrittenPerson> people,
		Dictionary<(int Slot, int Record), int> kept, Dictionary<(int Thing, int Record), int> made,
		IReadOnlySet<int> carSlots, IReadOnlyList<WrittenCar> cars, Dictionary<(int Car, int Record), int> onCars )
	{
		var looks = people.ToDictionary( person => person.Person.ThingId, person => (person.Person.SpriteKind, person.Person.SpriteBank) );
		var hung = new List<ParkWorld.WrittenHead>();
		var gone = new HashSet<int>();
		var taken = new HashSet<int>();
		var fresh = new List<(int Visitor, Action<int> Take)>();

		// The head a visitor's look gives: a costume's where they wear one, else a child's, on their own bank
		// (FUN_004fcac0); a visitor the park no longer holds is bank nought, as a head hung for nobody is.
		ParkWorld.WrittenHead Head( int slot, int visitor )
		{
			var (kind, bank) = looks.GetValueOrDefault( visitor );

			return new ParkWorld.WrittenHead( slot, kind == CostumeSpriteKind ? ParkWorld.CostumeHeadKind : ParkWorld.ChildHeadKind, bank );
		}

		foreach ( var model in run.Models )
		{
			if ( model.Heads is not { } heads )
				continue;

			var file = run.ModelStates.AttachedOn( model.Slot, heads.Records );
			var still = new HashSet<int>();

			foreach ( var (record, visitor) in heads.Hung )
			{
				still.Add( record );

				// The file's slot is the head's still where it holds a head and no other record has taken it.
				if ( file.TryGetValue( record, out var slot ) && loaded.SpriteKindIn( slot ) is ParkWorld.ChildHeadKind or ParkWorld.CostumeHeadKind
					&& taken.Add( slot ) )
				{
					kept[(model.Slot, record)] = slot;
					hung.Add( Head( slot, visitor ) );
				}
				else
				{
					var (at, who) = (model.Slot, visitor);

					fresh.Add( (who, dealt => kept[(at, record)] = dealt) );
				}
			}

			foreach ( var (record, slot) in file )
			{
				if ( !still.Contains( record ) )
					gone.Add( slot );
			}
		}

		// A thing sold takes its model with it, and whatever hung on it.
		foreach ( var thing in loaded.Objects.Where( thing => run.Gone?.Contains( thing.ThingId ) == true && thing.MeshInstance > 0 ) )
			gone.UnionWith( run.ModelStates.AttachedOn( thing.MeshInstance - 1 ).Values );

		foreach ( var thing in (run.Made ?? []).OrderBy( thing => thing.Object.Object.ThingId ) )
		{
			foreach ( var (record, visitor) in thing.Heads?.Hung ?? [] )
			{
				var id = thing.Object.Object.ThingId;

				fresh.Add( (visitor, dealt => made[(id, record)] = dealt) );
			}
		}

		// A car's model is made again in the file, so the head on it is too: the file's cars' heads give their slots
		// up, and each rider's head takes a new one.
		foreach ( var slot in carSlots )
			gone.UnionWith( run.ModelStates.AttachedOn( slot ).Values );

		for ( var car = 0; car < cars.Count; ++car )
		{
			foreach ( var (record, visitor) in cars[car].Heads )
			{
				var index = car;

				fresh.Add( (visitor, dealt => onCars[(index, record)] = dealt) );
			}
		}

		var empty = loaded.EmptySpriteSlots( fresh.Count, taken );

		for ( var i = 0; i < fresh.Count; ++i )
		{
			fresh[i].Take( empty[i] );
			hung.Add( Head( empty[i], fresh[i].Visitor ) );
		}

		return new ParkWorld.HeadEdits( hung, gone );
	}

	/// <summary>The person base's <c>mESPSprite</c> of somebody in a costume (<c>FUN_004fcac0</c>).</summary>
	private const int CostumeSpriteKind = 2;

	/// <summary>A made thing's tables with each head hung in its lookup record: <c>0x2</c> and the sprite's slot, and the shared <c>0x4</c>.</summary>
	private static ParkThingStates.ModelTables? WithHeads( MadeThing thing, Dictionary<(int Thing, int Record), int> slots )
	{
		if ( thing.Tables is not { } tables || thing.Heads is not { } heads )
			return thing.Tables;

		var lookups = tables.Lookups.ToArray();
		var any = false;

		foreach ( var (record, _) in heads.Hung )
		{
			if ( record < 0 || record >= lookups.Length || !slots.TryGetValue( (thing.Object.Object.ThingId, record), out var slot ) )
				continue;

			lookups[record] = (lookups[record].Flags | ParkThingStates.LookupAttached, slot);
			any = true;
		}

		return tables with { Lookups = lookups, Shared = tables.Shared | (any ? ParkThingStates.SharedAttached : 0) };
	}

	/// <summary>A car's model's tables with each rider's head hung in its seat's lookup record: <c>0x2</c> and the sprite's slot, and the shared <c>0x4</c>.</summary>
	private static ParkThingStates.ModelTables? WithHeads( WrittenCar car, int index, Dictionary<(int Car, int Record), int> slots )
	{
		if ( car.Model.Tables is not { } tables )
			return null;

		var lookups = tables.Lookups.ToArray();
		var any = false;

		foreach ( var (record, _) in car.Heads )
		{
			if ( record < 0 || record >= lookups.Length || !slots.TryGetValue( (index, record), out var slot ) )
				continue;

			lookups[record] = (lookups[record].Flags | ParkThingStates.LookupAttached, slot);
			any = true;
		}

		return tables with { Lookups = lookups, Shared = tables.Shared | (any ? ParkThingStates.SharedAttached : 0) };
	}

	/// <summary>The whole file: <see cref="Body"/> in its container.</summary>
	/// <exception cref="InvalidOperationException"><see cref="Body"/>'s, or the park was loaded from no file.</exception>
	public static byte[] Write( ParkWorld loaded, Running running ) => Write( loaded, running, out _ );

	/// <inheritdoc cref="Write(ParkWorld, Running)"/>
	public static byte[] Write( ParkWorld loaded, Running running, out ParkWorld.PeopleWritten? people )
		=> Write( loaded, running, out people, out _ );

	/// <inheritdoc cref="Write(ParkWorld, Running)"/>
	public static byte[] Write( ParkWorld loaded, Running running, out ParkWorld.PeopleWritten? people,
		out ThingsWritten? things )
	{
		ArgumentNullException.ThrowIfNull( loaded );

		if ( loaded.Preamble is not { } preamble )
			throw new InvalidOperationException( "the park was loaded from no file, so there is no preamble to carry" );

		return Container( preamble, Body( loaded, running, out people, out things ) );
	}

	/// <summary>A body behind a preamble: the version, the preamble's own bytes after its version, the block.</summary>
	private static byte[] Container( byte[] preamble, byte[] body )
	{
		using var stream = new MemoryStream();

		using ( var deflate = new ZLibStream( stream, CompressionLevel.Optimal, leaveOpen: true ) )
			deflate.Write( body );

		using var file = new MemoryStream();
		using var writer = new BinaryWriter( file );

		writer.Write( Version );
		writer.Write( preamble, 4, preamble.Length - 4 );
		writer.Write( "BILZ"u8 );
		writer.Write( body.Length );
		writer.Write( checked((int)stream.Length + BlockHeaderSize) );
		writer.Write( WindowBits );
		writer.Write( MemoryLevel );
		writer.Write( 0 );
		writer.Write( 0 );
		writer.Write( stream.GetBuffer(), 0, (int)stream.Length );
		writer.Flush();

		return file.ToArray();
	}

	private static void PutInt32( byte[] body, int at, int value ) =>
		BinaryPrimitives.WriteInt32LittleEndian( body.AsSpan( at, 4 ), value );
}
