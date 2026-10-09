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
/// (<see cref="MadeThing"/>); a queue cell laid or tiled again names a model made for it, and one cleared names none
/// (<see cref="QueuePiece"/>).
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
	/// </summary>
	public readonly record struct Running( int GameTick, bool ParkClosed, int VisitorsToDate, int Balance,
		ParkCameraModule.View Camera, IReadOnlyDictionary<int, ParkWorld.MapCell>? Cells = null,
		IReadOnlyList<ParkWorld.WrittenPerson>? People = null, ParkWorld.WrittenStaffPool? StaffPool = null,
		ArrivalTimer? Arrival = null, RunningThings? Things = null, IReadOnlyList<ParkWorld.WrittenSprite>? LetGo = null );

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
	public sealed record RunningThings( IReadOnlyList<ParkWorld.CatalogueObject> Objects, int SchedulerTick,
		int NextHandle, IReadOnlyList<WrittenScript> Scripts, ParkThingStates ModelStates,
		IReadOnlyList<WrittenModel> Models, uint Clock, IReadOnlyList<MadeThing>? Made = null,
		IReadOnlySet<int>? Gone = null, IReadOnlyDictionary<int, (int Standing, uint FirstBuilt)>? Built = null,
		IReadOnlyDictionary<int, QueuePiece?>? QueueCells = null );

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
		int Made = 0, int Gone = 0, int ModelSlots = 0, IReadOnlyList<(int Cell, int Handle, int Freed)>? QueueCells = null );

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

		if ( running.Things is { } run )
		{
			var objects = loaded.PutObjects( body, run.Objects );
			var scripts = loaded.ScriptStates.Put( body, run.SchedulerTick, run.NextHandle, run.Scripts );

			// The heads are sprites, so they are written with the people or not at all.
			if ( running.People != null )
				heads = PlanHeads( loaded, run, running.People, headSlots, madeHeadSlots );

			var models = run.ModelStates.Put( body, run.Models, heads != null ? headSlots : null );

			loaded.Clock.Put( body, run.Clock );
			things = new ThingsWritten( objects, scripts.Scripts, scripts.TablesLeft, models );

			var made = run.Made ?? [];
			var gone = run.Gone ?? new HashSet<int>();

			var pieces = run.QueueCells ?? new Dictionary<int, QueuePiece?>();

			if ( made.Count > 0 || gone.Count > 0 || pieces.Count > 0 )
			{
				if ( running.People == null && (made.Count > 0 || gone.Count > 0) )
					throw new InvalidOperationException( "a thing bought or sold is written with the people, and none were given" );

				// From the back of the file forwards, so each module is still where the file has it when its turn
				// comes: the scripts, the models, then the world with the people.
				var goneSlots = new HashSet<int>();
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
				// objects first, the oldest first, then the queue cells in the map's order. Every handle still
				// names its own record, which is all a load reads.
				var oldestFirst = made.OrderBy( thing => thing.Object.Object.ThingId ).ToList();
				var laid = cellsInOrder.Where( entry => entry.Value != null ).ToList();
				var slots = run.ModelStates.Plan( goneSlots, oldestFirst.Count + laid.Count );
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

				// Where it lies, so before anything ahead of the map changes length.
				loaded.PutCellModels( body, handles );

				body = loaded.ScriptStates.Splice( body,
					[.. scriptRecords.OrderByDescending( entry => entry.Handle ).Select( entry => entry.Record )], goneScripts );

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

		return body;
	}

	/// <summary>
	/// Gives every head hung a sprite slot (<c>docs/exe/saves.md</c>, "OpenTPW's writer, a rider's head"). A head
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
		Dictionary<(int Slot, int Record), int> kept, Dictionary<(int Thing, int Record), int> made )
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
