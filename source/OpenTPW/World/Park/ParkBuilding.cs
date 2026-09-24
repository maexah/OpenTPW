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
/// <b>What is deliberately not built yet, and is counted rather than guessed:</b> the markers drawn while
/// carrying (<c>CARRY_PREVIEW_MARKERS</c>) and the age-based scrap curve - see <see cref="Sell"/>.
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
	/// <remarks>
	/// <b>This builds and nothing more.</b> What a player's own placement goes on to do - hand them the
	/// queue tool on the cell before the entrance - is <see cref="PlaceCarried"/>'s, because that is the
	/// commit handler's work in the original rather than the placer's.
	/// </remarks>
	public static string Buy( int catalogueId, int cellX, int cellY, int angle = 0 )
		=> Build( catalogueId, cellX, cellY, angle ).Answer;

	/// <summary>
	/// What one build did: the line to show, the thing it made, the queue cell laid before it, and whether the
	/// cell refused it the way the original's preview reddens one.
	/// </summary>
	private readonly record struct Built( string Answer, int ThingId = 0, (int X, int Y)? QueueNode = null,
		bool HasQueue = false, int TrackType = 0, bool Refused = false );

	private static Built Build( int catalogueId, int cellX, int cellY, int angle )
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state
			|| level.Catalogue is not { } catalogue || ParkObjects.Current is not { } objects
			|| level.Park is not { } park )
			return new( "buy: a park has to be loaded" );

		return Build( state, park, catalogue, objects, ParkRides.Current, catalogueId, cellX, cellY, angle );
	}

	/// <summary>
	/// The whole of a build once the park is in hand, so a test can reach every refusal without a running
	/// <see cref="Level"/>. With no <paramref name="objects"/> nothing can be stood, so a placement that passes
	/// every test answers that it would not load.
	/// </summary>
	private static Built Build( ParkState state, ParkWorld park, ParkItemCatalogue catalogue, ParkObjects? objects,
		ParkRides? rides, int catalogueId, int cellX, int cellY, int angle )
	{
		if ( !catalogue.TryGet( catalogueId, out var item ) )
			return new( $"buy: this theme has no item {catalogueId}" );

		// The four the buy list itself refuses - see docs/exe/hud.md. WhichUIType 4 is the game's own
		// "not shown in the UI", which is what the vehicles, the gates and the land tools carry.
		if ( item.UiType is < 0 or > ItemDescriptionFile.Feature )
			return new( $"buy: '{item.Name}' is UI type {item.UiType}, which no buy tab lists" );

		var footprint = ParkObjects.FootprintAt( item, cellX, cellY, angle );

		if ( Refusal( state, footprint ) is { } why )
			return new( $"buy: '{item.Name}' will not fit at ({cellX},{cellY}) - {why}", Refused: true );

		// The affordability test the original makes when a buy row is clicked, before it ever builds
		// the placement mode - and, for a move, which has no such click, in the put-down's own preview,
		// where a price above the balance turns the cell red (FUN_00535670).
		if ( state.Balance < item.BuildPrice )
			return new( $"buy: '{item.Name}' costs {item.BuildPrice} and the park has {state.Balance}", Refused: true );

		if ( EndRefusal( state, item, cellX, cellY, angle ) is { } blocked )
			return new( $"buy: '{item.Name}' will not fit at ({cellX},{cellY}) - {blocked}", Refused: true );

		var thingId = state.NextThingId();

		// A NEWLY BUILT RIDE STARTS AT ITS ITEM'S OWN SETTINGS, not at nought. The original seeds these
		// three from the purchase tier - Upgrades[0].InitSpeed / InitCapacity / InitDuration - and they
		// are what the ride window's sliders open on. Left at nought a bought ride would carry nobody
		// and last no time, and ParkRides.BindNew would push those noughts straight into its script.
		// The flags word the original builds bit by bit out of the item's own description while it
		// constructs the object, and mCanLoad, which the same constructor writes as 1 outright
		// (FUN_004db090: `*(param_1 + 0x68) = 1`). A bought thing left at the record's defaults carries
		// neither, and then invites nobody: ParkRideOperation.Invite bails on mCanLoad before it reads a
		// single script variable, and ParkRideChoice.CanBeOffered refuses anything without the visitable
		// bit - so the thing stands and animates and is never used.
		//
		// ONLY the two bits whose descriptor key is established are set. Info.IsChoosable is the save's
		// visitable bit and the two agree on all six flagged objects in the shipped park;
		// UsageInfo.ProvidesRelief is the toilet bit, which exactly the three Small Toilets carry. Each
		// remaining bit comes from a different descriptor field, none of them pinned - the queue-path bit
		// in particular reads from a field this project has not named, and agreeing with Info.HasQueue on
		// the two objects this park can compare is not establishing it. They are left clear and counted.
		var flags = 0;

		if ( item.IsChoosable )
			flags |= ParkWorld.CatalogueObject.VisitableFlag;

		if ( item.ProvidesRelief )
			flags |= ParkWorld.CatalogueObject.ToiletFlag;

		Unimplemented.Report( "BOUGHT_OBJECT_FLAG_BITS" );

		// Where a guest walks up to it, and where one is put down leaving it. The original derives both in
		// the same constructor, from the item's own footprint picture turned by the angle it is being
		// built at: mEntryPos is the anchor cell plus MapDelta::Rotate( entrance delta, angle ), and
		// mExitPos the same with the exit's. An item whose picture marks no entrance has both deltas at
		// nought, so both land on the anchor cell itself - which is what the shipped park stores for each
		// of its placed things that has none.
		//
		// mTopLeft is NOT set. The original writes it from a third descriptor pair this project has not
		// named, and nothing in this tree reads the field, so setting it would be a guess with no
		// consequence either way.
		var (entryX, entryY) = RotateDelta( item.EntryDeltaX, item.EntryDeltaY, angle );
		var (exitX, exitY) = RotateDelta( item.ExitDeltaX, item.ExitDeltaY, angle );

		var entryPos = ParkState.OnMap( cellX + entryX, cellY + entryY )
			? MapStep.CellId( cellX + entryX, cellY + entryY )
			: 0;

		var exitPos = ParkState.OnMap( cellX + exitX, cellY + exitY )
			? MapStep.CellId( cellX + exitX, cellY + exitY )
			: 0;

		// The placer stamps each picture cell by its own kind, mapping the track kinds 0x0b, 0x10 and 0x17
		// to the body's 4 - so those three and the body are what Stamp's single type reproduces. The empty
		// cell '.' is stamped as body here too, and a 0x17 cell's neighbour is given type 0x15 in the
		// original; neither is built.
		foreach ( var kind in item.CellKinds ?? [] )
		{
			if ( kind is not (0x04 or 0x0b or 0x10 or ItemDescriptionFile.EntranceKind or ItemDescriptionFile.ExitKind) )
				Unimplemented.Report( $"SHAPE_KIND_{kind}_PLACEMENT" );
		}

		var placed = new ParkWorld.CatalogueObject(
			ThingId: thingId, CatalogueId: catalogueId,
			RawX: cellX << 8, RawY: cellY << 8, Angle: angle,
			Flags: (ushort)flags,
			CanLoad: 1,
			EntryPos: (ushort)entryPos,
			ExitPos: (ushort)exitPos,
			OperatingSpeed: item.InitSpeed,
			OperatingCapacity: item.InitCapacity,
			OperatingDuration: item.InitDuration );

		if ( objects?.PlaceNow( placed, catalogue ) != true )
			return new( $"buy: '{item.Name}' would not load, so nothing was built and nothing was charged" );

		state.AddObject( placed );
		Stamp( state, footprint, cellX, cellY );

		// And onto its anchor cell's own list, which is the ONE cell the save puts a placed thing on -
		// through EnterCell, so a guest standing there is kept behind it rather than evicted. The rest
		// of the footprint is reached through the owner Stamp just wrote, not through occupancy: a
		// thing cannot be on twelve cells' lists, because those links are keyed per thing and the
		// twelfth would overwrite the first.
		state.EnterCell( cellX, cellY, thingId );

		// After the footprint, never before: Stamp types every cell it covers, and the way in and the way
		// out are two of those cells wearing a different type. A picture with no entrance has neither.
		var node = item.HasEntrance
			? MarkWaysInAndOut( state, park, cellX + entryX, cellY + entryY, cellX + exitX, cellY + exitY,
				item.EntryDirection, item.ExitDirection, angle, item.HasQueue, MapStep.CellId( cellX, cellY ) )
			: null;

		// The placer rewalks the queue once its stub is down (FUN_004de1f0 at 0x00529890), so the ride
		// measures the one cell it already has.
		if ( node != null )
			state.InvalidateQueue( thingId );

		state.Spend( item.BuildPrice );

		// The ground draws every cell nothing else owns, so it has to be told that these are owned now
		// or it keeps drawing grass through the new floor - and the paths and queues are rebuilt with it,
		// because the three divide the map by cell and one rebuilt alone leaves a hole. See ParkSurfaces.
		ParkSurfaces.Rebuild();

		rides?.BindNew( placed, item );

		Log.Info( $"Building: bought '{item.Name}' for {item.BuildPrice} as thing {thingId} at " +
			$"({cellX},{cellY}) turned {angle}, covering ({footprint.Left},{footprint.Top}).." +
			$"({footprint.Right},{footprint.Bottom}) - the park has {state.Balance} left" );

		return new( $"buy: '{item.Name}' built as thing {thingId} at ({cellX},{cellY}) for {item.BuildPrice}, " +
			$"balance {state.Balance}" + (node is { } at ? $", queue node at ({at.X},{at.Y})" : ""), thingId, node,
			item.HasQueue, item.TrackType );
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
			|| level.Catalogue is not { } catalogue || ParkObjects.Current is not { } objects
			|| level.Park is not { } park )
			return "sell: a park has to be loaded";

		return Sell( state, park, catalogue, objects, ParkRides.Current, thingId, ParkPeople.Current );
	}

	/// <summary>
	/// The whole of <see cref="Sell(int)"/> once the park is in hand - internal so a test can sell something
	/// without a running <see cref="Level"/>, the way <see cref="Stamp"/> and <see cref="ReleaseEnds"/> are.
	/// </summary>
	internal static string Sell( ParkState state, ParkWorld park, ParkItemCatalogue catalogue, ParkObjects? objects,
		ParkRides? rides, int thingId, ParkPeople? people = null )
		=> Demolish( state, park, catalogue, objects, rides, thingId, people ).Answer;

	/// <summary>What one sale did: the line to show, and whether the thing is gone.</summary>
	private readonly record struct Sold( string Answer, bool Done = false );

	/// <summary>Sells one thing, and answers whether it went.</summary>
	/// <remarks>
	/// <b>The order is the original's</b> where anything can see it: the demolisher (<c>FUN_00527ee0</c>)
	/// drains the queue, lets the ends go and clears the footprint, and then the object destructor unlinks the
	/// thing, tells the park's people it has gone (the type-10 message, <c>0x004dd150</c>), refunds it, takes
	/// its script down and destroys its model last - <c>docs/exe/park-engine.md</c>, "The demolisher's order,
	/// and the cells it leaves". Nothing runs between the unlink and the footprint's clear, and the people
	/// answer only after both, so where the clear falls against the unlink changes nothing.
	/// </remarks>
	private static Sold Demolish( ParkState state, ParkWorld park, ParkItemCatalogue catalogue, ParkObjects? objects,
		ParkRides? rides, int thingId, ParkPeople? people )
	{
		if ( !state.TryObject( thingId, out var placed ) )
			return new( $"sell: nothing in the park is thing {thingId}" );

		if ( !catalogue.TryGet( placed.CatalogueId, out var item ) )
			return new( $"sell: thing {thingId} is catalogue item {placed.CatalogueId}, which this theme has none of" );

		// The test Buy makes. The demolisher finds a thing only through a cell typed 4, 9 or 10
		// (FUN_00527d60), and the gates, the lights and the vehicles stand on no such cell, so nothing
		// in the original can sell one - inferred from that, not traced. Selling the gate would take its
		// script down with it, and with no gate no guest is ever let in again.
		if ( item.UiType is < 0 or > ItemDescriptionFile.Feature )
			return new( $"sell: thing {thingId} ('{item.Name}') is UI type {item.UiType}, which nothing can demolish" );

		// The age-based scrap percentage, which nothing here can compute - see the remarks. Reported
		// once and counted, rather than a number invented to fill the gap.
		Unimplemented.Report( "SCRAP_VALUE_DEPRECIATION" );

		// The demolish sound, one of 0x96..0x99 by how many body cells the thing covered (FUN_00527ee0,
		// after the destructor, whenever a saved layout is not being replayed).
		Unimplemented.Report( "DEMOLISH_SOUND" );

		var refund = item.BuildPrice;
		var footprint = ParkObjects.FootprintAt( item, placed.CellX, placed.CellY, placed.Angle );

		// Before the footprint goes, what the demolisher does outside it (FUN_00527ee0): a queued thing's
		// queue is drained, node and all, and the paths laid before its ends go back to being ordinary path.
		var queueRefund = item.HasQueue ? ParkPathBuilding.DrainQueue( state, park, placed ) : 0;

		ReleaseEnds( state, park, placed );

		state.RemoveObject( thingId );

		Unstamp( state, footprint, placed.CellX, placed.CellY, thingId );

		// The destructor's "object removed" message (0x004dd150): a rider is put off where they are, a
		// queuer is put out of the queue, and staff resting in it or heading there to rest give it up. It
		// goes after the unlink and before the refund and the script teardown, as the original's does.
		people?.ThingRemoved( placed );

		state.Refund( refund );

		// The demolisher puts back the tool it was called under (0x0052818d). With no tool that is the idle mode,
		// installed through the setter, so a candidate or a worker in the hand is let go of; an item in the hand
		// or an armed build tool is a tool, and stays.
		if ( Carrying == 0 && ParkBuildMode.Current == ParkBuildMode.None && ParkHand.LetGo() is { } letGo )
			Log.Info( $"Hand: {letGo}" );

		rides?.Unbind( thingId, item );
		objects?.Remove( thingId );

		ParkSurfaces.Rebuild();

		Log.Info( $"Building: sold '{item.Name}' (thing {thingId}) for {refund} - the park has {state.Balance}" );

		return new( $"sell: '{item.Name}' thing {thingId} sold for {refund}" +
			(queueRefund != 0 ? $", its queue for {queueRefund}" : "") + $", balance {state.Balance}", Done: true );
	}

	/// <summary>
	/// Lets go of what stands before a demolished thing's ends - the demolisher's two footprint passes
	/// (<c>FUN_00527ee0</c>), run before its own cells are cleared.
	/// </summary>
	/// <remarks>
	/// <b>The NOMODIFY path before an end loses the flag</b> and stays an ordinary path: the one before the
	/// exit, and the one before the entrance of a thing with no queue - the entrance is looked past along
	/// the opposite of its direction byte, the exit along it, which for a queued entrance leads into its
	/// own footprint and so finds nothing. It keeps the flag while it still serves two or more ends, and
	/// on design-map path, which the original tests with bit 8 of the cell's <c>+0x26</c> seed. That seed
	/// is read here from <c>base.map</c>, where bit 8 marks exactly the ten-cell avenue - inferred to be
	/// the seed's source, not traced.
	/// <para>
	/// <b>Then each end is unlinked from what it adjoined</b>, which is what clearing it does: the paths
	/// before it lose their bit toward it.
	/// </para>
	/// </remarks>
	internal static void ReleaseEnds( ParkState state, ParkWorld park, ParkWorld.CatalogueObject placed )
	{
		if ( placed.EntryPos == 0 )
			return;

		var ends = new[] { (placed.EntryCellX, placed.EntryCellY), (placed.ExitCellX, placed.ExitCellY) }.Distinct().ToArray();

		foreach ( var (x, y) in ends )
		{
			if ( !ParkState.OnMap( x, y ) )
				continue;

			var end = state.Record( x, y );
			var looking = end.Type switch
			{
				CellEdge.RideEnd => CellEdge.Opposite( end.Direction ),
				CellEdge.RideFarEnd => end.Direction,
				_ => 0
			};

			var (pathX, pathY) = Step( x, y, looking );

			if ( looking == 0 || !ParkState.OnMap( pathX, pathY ) )
				continue;

			var path = state.Record( pathX, pathY );

			if ( path.Type != CellEdge.Path || (path.Flags & ParkPathBuilding.NoModify) == 0 )
				continue;

			var servedEnds = new[] { 0x01, 0x04, 0x10, 0x40 }.Count( bit =>
			{
				var (nextX, nextY) = Step( pathX, pathY, bit );

				return (path.Neighbours & bit) != 0 && ParkState.OnMap( nextX, nextY )
					&& state.Record( nextX, nextY ).Type is CellEdge.RideEnd or CellEdge.RideFarEnd;
			} );

			var designMap = ((ParkGround.Current?.Attributes?.At( pathX, pathY ) ?? 0) & DesignMapPath) != 0;

			if ( servedEnds < 2 && !designMap )
				state.SetRecord( pathX, pathY, path with { Flags = (ushort)(path.Flags & ~ParkPathBuilding.NoModify) } );
		}

		foreach ( var (x, y) in ends )
		{
			if ( ParkState.OnMap( x, y ) )
				ParkPathBuilding.Unlink( state, park, x, y );
		}
	}

	/// <summary>The seed bit a design-map path carries - <c>FUN_00536490</c> makes such a cell a NOMODIFY path.</summary>
	private const int DesignMapPath = 0x08;

	/// <summary>
	/// Picks something standing up to move it: sells it, then takes the same item into the hand, facing the
	/// way it stood. The object window's move verb, and the first half of the console's.
	/// </summary>
	/// <remarks>
	/// <b>This is the original's move</b>, <c>FUN_0048cfa0</c> - <c>docs/exe/park-engine.md</c>, "Moving a
	/// thing". It runs the demolish Delete runs, without Delete's confirm box, so the refund is banked there
	/// and then; then it installs the move tool, <c>0x3b</c>, holding the item and the thing's own angle.
	/// Putting it down builds it afresh at full price through the purchase's own placer, so a move given up
	/// is a sale.
	/// <para>
	/// <b>Nothing is refused for money here</b>, where <see cref="Carry"/> refuses what the park cannot
	/// afford: the pickup makes no such test, and the put-down refuses an unaffordable cell as it refuses a
	/// taken one.
	/// </para>
	/// <para>
	/// <b>A deviation:</b> the original takes the item into the hand whether or not its demolish found the
	/// thing, because <c>FUN_00524960</c> answers nothing. Here the hand stays empty unless the sale went
	/// through, so nothing is ever carried while it still stands.
	/// </para>
	/// </remarks>
	public static string PickUp( int thingId )
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state
			|| level.Catalogue is not { } catalogue || ParkObjects.Current is not { } objects
			|| level.Park is not { } park )
			return "pick up: a park has to be loaded";

		return PickUp( state, park, catalogue, objects, ParkRides.Current, thingId, ParkPeople.Current ).Answer;
	}

	/// <summary>What one pickup did: the line to show, and whether the item is in the hand.</summary>
	private readonly record struct Taken( string Answer, bool Holding = false );

	private static Taken PickUp( ParkState state, ParkWorld park, ParkItemCatalogue catalogue, ParkObjects? objects,
		ParkRides? rides, int thingId, ParkPeople? people )
	{
		if ( !state.TryObject( thingId, out var placed ) )
			return new( $"pick up: nothing in the park is thing {thingId}" );

		var sold = Demolish( state, park, catalogue, objects, rides, thingId, people );

		if ( !sold.Done )
			return new( $"pick up: {sold.Answer}" );

		Hold( placed.CatalogueId, placed.Angle );

		return new( $"pick up: {sold.Answer} - {HandState()}", Holding: true );
	}

	/// <summary>
	/// Moves something standing to another cell, for the debug console: <see cref="PickUp(int)"/>, then one
	/// click at the cell. A cell that refuses leaves the item in the hand, as a click on a red cell does in
	/// the original, for a later <c>put</c> or click to take it from there.
	/// </summary>
	public static string Move( int thingId, int cellX, int cellY, int? angle = null )
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state
			|| level.Catalogue is not { } catalogue || ParkObjects.Current is not { } objects
			|| level.Park is not { } park )
			return "move: a park has to be loaded";

		return Move( state, park, catalogue, objects, ParkRides.Current, thingId, cellX, cellY, angle,
			ParkPeople.Current );
	}

	/// <summary>
	/// The whole of <see cref="Move(int, int, int, int?)"/> once the park is in hand, for a test.
	/// </summary>
	internal static string Move( ParkState state, ParkWorld park, ParkItemCatalogue catalogue, ParkObjects? objects,
		ParkRides? rides, int thingId, int cellX, int cellY, int? angle = null, ParkPeople? people = null )
	{
		var taken = PickUp( state, park, catalogue, objects, rides, thingId, people );

		if ( !taken.Holding )
			return $"move: {taken.Answer}";

		return $"move: {taken.Answer} - {PlaceCarried( state, park, catalogue, objects, rides, cellX, cellY, angle )}";
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

	/// <summary>
	/// Why the cells a thing's ends face refuse it, or null when they do not - the placer's own test
	/// pass (<c>FUN_00528a70</c> with the test flag), which turns every marker red and so refuses the
	/// whole placement.
	/// </summary>
	/// <remarks>
	/// <b>An end facing off the map</b> makes the placer return null (<c>0x00529744</c>..<c>0x00529757</c>
	/// for the entrance, <c>0x005299e2</c>..<c>0x005299f5</c> for the exit). <b>A queued thing's entrance</b>
	/// is tested as op 4, whose verdict passes only bare ground or a path that is not NOMODIFY - so another
	/// queue, another thing's placer cell, water or rock all refuse. <b>Any other end</b> - a queued thing's
	/// exit, or the entrance of a thing with no queue - is tested as op 1, which refuses another thing's
	/// cells (every shipped item's overwrite priority is 5, and the verdict wants the other's lower) and
	/// accepts a NOMODIFY path.
	/// </remarks>
	internal static string? EndRefusal( ParkState state, ParkItemCatalogue.Item item, int cellX, int cellY, int angle )
	{
		if ( !item.HasEntrance )
			return null;

		var (entryX, entryY) = RotateDelta( item.EntryDeltaX, item.EntryDeltaY, angle );
		var (exitX, exitY) = RotateDelta( item.ExitDeltaX, item.ExitDeltaY, angle );
		var heading = RotateBit( item.EntryDirection, angle );
		var exitHeading = RotateBit( item.ExitDirection, angle );

		var faced = Step( cellX + entryX, cellY + entryY, CellEdge.Opposite( heading ) );

		if ( (item.HasQueue ? QueueEndBlocked( state, faced ) : EndBlocked( state, faced )) is { } why )
			return $"its entrance would face ({faced.X},{faced.Y}), which {why}";

		if ( !item.HasQueue || (entryX == exitX && entryY == exitY) )
			return null;

		var exitFaced = Step( cellX + exitX, cellY + exitY, exitHeading );

		return EndBlocked( state, exitFaced ) is { } exitWhy
			? $"its exit would face ({exitFaced.X},{exitFaced.Y}), which {exitWhy}"
			: null;
	}

	/// <summary>The op-4 verdict on the cell before a queued entrance: bare ground, or a path that is not NOMODIFY.</summary>
	private static string? QueueEndBlocked( ParkState state, (int X, int Y) cell )
	{
		if ( !ParkState.OnMap( cell.X, cell.Y ) )
			return "is off the map";

		var record = state.Record( cell.X, cell.Y );

		if ( record.Type == CellEdge.Nothing || (record.Type == CellEdge.Path && (record.Flags & ParkPathBuilding.NoModify) == 0) )
			return null;

		return ParkObjects.CoversGround( record ) ? "has something built on it"
			: record.Type == CellEdge.Path ? "is marked NOMODIFY"
			: $"is type {record.Type}, which a queue may not start on";
	}

	/// <summary>The op-1 verdict on the cell before any other end: anything but another thing's cells.</summary>
	private static string? EndBlocked( ParkState state, (int X, int Y) cell )
	{
		if ( !ParkState.OnMap( cell.X, cell.Y ) )
			return "is off the map";

		return ParkObjects.CoversGround( state.Record( cell.X, cell.Y ) ) ? "has something built on it" : null;
	}

	/// <summary>
	/// Turns a cell delta by the angle a thing is built at - the original's <c>MapDelta::Rotate</c>,
	/// <c>FUN_004d9cc0</c>, which names itself in its own assert string.
	/// </summary>
	/// <remarks>
	/// <b>This is not <see cref="ParkObjects.Turn"/> and must not be folded into it.</b> That one is a
	/// quaternion about the world's up axis, with a deliberately negative sense measured off the save's
	/// own footprint cells; this is an integer map delta in cell space. The original keeps them apart too,
	/// and pairing the wrong one with the wrong space is the mistake <see cref="ParkObjects"/>'s own
	/// remarks record having made once already.
	/// <para>
	/// The engine asserts on any angle that is not a quarter turn, so the four arms are the whole of it.
	/// </para>
	/// </remarks>
	/// <remarks>
	/// Internal rather than private only so that it can be tested, the same reason
	/// <see cref="ParkFixedItems.ThingByCatalogue"/> is: a test that re-derived the turn table over the
	/// public verb would need a loaded park and would pass just as well with the table put back wrong.
	/// </remarks>
	internal static (int X, int Y) RotateDelta( int x, int y, int angle )
		=> (((angle % 360) + 360) % 360) switch
		{
			90 => (y, -x),
			180 => (-x, -y),
			270 => (-y, x),
			_ => (x, y)
		};

	/// <summary>
	/// Turns one compass bit by a quarter for each quarter the thing is turned. The compass is a ring of
	/// eight - <c>0x01 N, 0x02 NE, 0x04 E, …</c> - and a quarter turn is two places <b>backwards</b> round
	/// it, which is the way <see cref="RotateDelta"/> turns a cell delta.
	/// </summary>
	/// <remarks>
	/// <b>The sense is read off <c>FUN_004d8c20</c>.</b> That function left-rotates the byte by
	/// <c>log2</c> of the angle's base bit, and the placer pairs the bases <c>1</c>, <c>0x40</c>,
	/// <c>0x10</c>, <c>4</c> with the angles 0, 90, 180 and 270 (<c>0x00528f62</c>..<c>0x00528f8b</c>) - so
	/// a quarter is a left-rotate of six, which is a right-rotate of two: east <c>0x04</c> becomes north
	/// <c>0x01</c>. <c>MapDelta::Rotate</c> sends the east delta <c>(1,0)</c> to <c>(0,-1)</c> at that same
	/// angle, so the two agree at all four.
	/// <para>
	/// <b>Turned the other way, a thing built at a quarter or three quarters puts its way in 180 degrees
	/// from where <see cref="RotateDelta"/> has just put its entry cell</b> - pointing back into its own
	/// footprint - and the two agree at 0 and 180 whichever way this turns.
	/// </para>
	/// </remarks>
	internal static int RotateBit( int bit, int angle )
	{
		var places = (8 - (((((angle % 360) + 360) % 360) / 90) * 2)) & 7;

		return ((bit << places) | (bit >> (8 - places))) & 0xff;
	}

	/// <summary>
	/// Types a thing's way in and way out, and builds what the placer builds in front of them: <b>a
	/// one-cell queue before the entrance of anything that has a queue</b>, which is the node a player
	/// lays the rest of the queue from, and a one-cell path before its exit - or, for a thing with no
	/// queue, a one-cell path before its entrance. Answers the queue cell, or null when none was laid.
	/// </summary>
	/// <remarks>
	/// <b>This is <c>FUN_00528a70</c>'s own order.</b> Its footprint sweep types each end and turns the
	/// end character's bit by the angle - <c>H</c>, kept in <c>DAT_00818c30</c> - and re-links the cell
	/// the end faces where that is already a path. Then, after the sweep and only when building
	/// (<c>0x005297e7</c>..<c>0x00529890</c> for a queued thing, <c>0x005298d5</c>..<c>0x005299aa</c> for
	/// one without, <c>0x005299cb</c>..<c>0x00529b18</c> for the exit half), it lays the stubs. The shipped
	/// park carries every one of them: the Belly Bounce's queue cell (52,22) and path cell (52,27), and a
	/// path cell before each of its seven other entrances, all flagged NOMODIFY. See
	/// <c>docs/exe/park-engine.md</c>, "What the placer builds in front of a thing".
	/// <para>
	/// <b>The entrance faces the way opposite to its character's bit.</b> Every shipped entrance is a
	/// <c>2</c>, whose bit is <c>0x10</c>, and the cell it faces is one step toward <c>Opposite(H)</c> -
	/// (52,22) for the Belly Bounce's entrance at (52,23). A queued thing's entrance then takes
	/// <c>Opposite(H)</c> as its own direction byte; anything else keeps <c>H</c>. The exit faces its own
	/// bit.
	/// </para>
	/// </remarks>
	/// <param name="ownerCell">The packed cell of the thing's anchor, which a queue cell names as its owner.</param>
	internal static (int X, int Y)? MarkWaysInAndOut( ParkState state, ParkWorld park,
		int entryCellX, int entryCellY, int exitCellX, int exitCellY,
		int entryDirection, int exitDirection, int angle, bool hasQueue, int ownerCell )
	{
		var heading = RotateBit( entryDirection, angle );
		var exitHeading = RotateBit( exitDirection, angle );

		// An item whose picture marks no exit leaves it on the entrance, and writing type 10 over the 9
		// would lose the way in.
		var separateExit = exitCellX != entryCellX || exitCellY != entryCellY;

		var (exitFacedX, exitFacedY) = Step( exitCellX, exitCellY, exitHeading );
		var (facedX, facedY) = Step( entryCellX, entryCellY, CellEdge.Opposite( heading ) );

		// The sweep's case 10 and case 9 arms: the type, the turned bit, and a re-link of the faced cell
		// where it is already a path, which is what joins a thing built against an existing path.
		if ( separateExit )
		{
			Mark( state, exitCellX, exitCellY, CellEdge.RideFarEnd, exitHeading );
			RelinkIfPath( state, park, exitFacedX, exitFacedY );
		}

		Mark( state, entryCellX, entryCellY, CellEdge.RideEnd, heading );
		RelinkIfPath( state, park, facedX, facedY );

		if ( !hasQueue )
		{
			LayPathStub( state, park, facedX, facedY );

			return null;
		}

		// The queue arm writes the entrance's half of the pair itself, pointing at the cell it faces.
		if ( ParkState.OnMap( entryCellX, entryCellY ) )
		{
			var inward = CellEdge.Opposite( heading );
			var entrance = state.Record( entryCellX, entryCellY );

			state.SetRecord( entryCellX, entryCellY, entrance with
			{
				Neighbours = (byte)(entrance.Neighbours | inward),
				Direction = (byte)inward
			} );
		}

		var stub = LayQueueStub( state, park, facedX, facedY, heading, ownerCell );

		if ( separateExit )
			LayPathStub( state, park, exitFacedX, exitFacedY );

		return stub;
	}

	/// <summary>
	/// The queue cell the placer lays before a queued thing's entrance - the node the queue tool is
	/// anchored on once the thing is down.
	/// </summary>
	/// <remarks>
	/// <b>Everything here is written outright, in the placer's order</b> (<c>0x00529808</c>..
	/// <c>0x00529885</c>): the queue stamp, the bit pointing back at the entrance and the same bit as
	/// its flow byte, the thing that owns it, NOMODIFY assigned rather than OR'd, then the retile - which
	/// finds a single link and stands <c>quedead</c> on it. <b>It is free</b>: the commit raises
	/// <c>DAT_008186d4</c> before the placer runs, and the stamp debits nothing while that is set.
	/// </remarks>
	private static (int X, int Y)? LayQueueStub( ParkState state, ParkWorld park, int x, int y,
		int heading, int ownerCell )
	{
		// EndRefusal has already refused a placement whose entrance faces off the map.
		if ( !ParkState.OnMap( x, y ) )
			return null;

		var cell = state.Record( x, y );

		// EndRefusal has already refused anything but bare ground or an ordinary path here; this holds
		// for a caller that builds without asking it first.
		if ( !ParkPathBuilding.MayBecome( cell.Type, ParkRideChoice.QueueCellType, lastOfRun: false )
			|| cell.Type == ParkRideChoice.QueueCellType || (cell.Flags & ParkPathBuilding.NoModify) != 0 )
		{
			Unimplemented.Report( "QUEUE_STUB_OVER_SOMETHING" );

			return null;
		}

		// A path under the stub is cleared first, which unlinks it from the paths around it.
		ParkPathBuilding.ForceClearPath( state, park, x, y );
		cell = state.Record( x, y );

		state.SetRecord( x, y, cell with
		{
			Type = ParkRideChoice.QueueCellType,
			TileSet = ParkQueues.QueueTileSet,
			Neighbours = (byte)(cell.Neighbours | heading),
			Direction = (byte)heading,
			ParentId = (ushort)ownerCell,
			Flags = ParkPathBuilding.NoModify
		} );

		ParkPathBuilding.RetileAround( state, park, x, y );

		return (x, y);
	}

	/// <summary>
	/// The path cell the placer lays before a queued thing's exit, or before the entrance of a thing
	/// with no queue - stamped, joined to what is around it, retiled, and flagged NOMODIFY, with <b>no
	/// direction byte</b>. The shipped park's (52,27) carries exactly that: the bit, and direction 0.
	/// </summary>
	private static void LayPathStub( ParkState state, ParkWorld park, int x, int y )
	{
		if ( !ParkState.OnMap( x, y ) )
			return;

		var cell = state.Record( x, y );

		if ( !ParkPathBuilding.MayBecome( cell.Type, CellEdge.Path )
			|| (cell.Type != CellEdge.Path && (cell.Flags & ParkPathBuilding.NoModify) != 0) )
		{
			Unimplemented.Report( "PATH_STUB_OVER_SOMETHING" );

			return;
		}

		// A queue cell here is turned to path and the queue it belonged to measured again.
		var queueOwner = cell.Type == ParkRideChoice.QueueCellType ? ParkPathBuilding.OwnerOf( state, cell ) : 0;

		if ( cell.Type != CellEdge.Path )
			state.SetRecord( x, y, cell with { Type = CellEdge.Path, TileSet = ParkPaths.PathTileSet } );

		ParkPathNeighbours.LinkPath( state, park, x, y );
		ParkPathBuilding.RetileAround( state, park, x, y );

		state.SetRecord( x, y, state.Record( x, y ) with { Flags = ParkPathBuilding.NoModify } );

		if ( queueOwner != 0 )
			state.InvalidateQueue( queueOwner );

		// Then each path it is now joined to on a cardinal side is joined again, from its own side
		// (0x00529951..0x005299aa for the entrance's stub, 0x00529abf..0x00529b18 for the exit's).
		foreach ( var bit in new[] { 0x01, 0x04, 0x10, 0x40 } )
		{
			var (nextX, nextY) = Step( x, y, bit );

			if ( (state.Record( x, y ).Neighbours & bit) == 0 || !ParkState.OnMap( nextX, nextY )
				|| state.Record( nextX, nextY ).Type != CellEdge.Path )
				continue;

			ParkPathNeighbours.LinkPath( state, park, nextX, nextY );
			ParkPathBuilding.RetileAround( state, park, nextX, nextY );
		}
	}

	/// <summary>
	/// Runs the neighbour rule over the cell an end faces <b>where that is already a path</b>, and redraws
	/// it - so a thing built against a path joins it, and the path shows that it has.
	/// </summary>
	/// <remarks>
	/// <b>The retile matters as much as the link.</b> <see cref="ParkPaths"/> redraws each cell from its
	/// STORED tile index, so without it the masks say joined and the path goes on drawing the piece it drew
	/// before the thing arrived.
	/// </remarks>
	private static void RelinkIfPath( ParkState state, ParkWorld park, int x, int y )
	{
		if ( !ParkState.OnMap( x, y ) || state.Record( x, y ).Type != CellEdge.Path )
			return;

		ParkPathNeighbours.LinkPath( state, park, x, y );
		ParkPathBuilding.RetileAround( state, park, x, y );
	}

	/// <summary>
	/// One step along a compass bit. The ring is the executable's own, confirmed from the static
	/// initialisers and <c>FUN_004d97e0</c>'s jump table rather than from save statistics: <c>0x01</c> is
	/// (0,−1) and <c>0x10</c> is (0,+1). <b>This is the outward sense</b>, the mirror of
	/// <see cref="CellEdge.BitFor"/>, and mixing the two inverts every answer.
	/// </summary>
	private static (int X, int Y) Step( int x, int y, int bit ) => bit switch
	{
		0x01 => (x, y - 1),
		0x04 => (x + 1, y),
		0x10 => (x, y + 1),
		0x40 => (x - 1, y),
		_ => (x, y)
	};

	/// <summary>The compass bit for the one cardinal step from a cell to its neighbour, or nought.</summary>
	private static int BitToward( int x, int y, int toX, int toY ) => (toX - x, toY - y) switch
	{
		(0, -1) => 0x01,
		(1, 0) => 0x04,
		(0, 1) => 0x10,
		(-1, 0) => 0x40,
		_ => 0
	};

	/// <summary>
	/// Types one end of a thing and gives it its turned bit as its direction byte - the sweep's
	/// <c>FUN_005227e0</c> at <c>0x005292b2</c> for the exit and <c>0x005293c3</c> for the entrance.
	/// <b>It writes no neighbour bit</b>; the original's only <c>FUN_00522700</c> calls in the placer are
	/// the queue arm's pair, and an end's other links come from the stub laid before it.
	/// </summary>
	private static void Mark( ParkState state, int x, int y, int type, int direction )
	{
		if ( !ParkState.OnMap( x, y ) )
			return;

		state.SetRecord( x, y, state.Record( x, y ) with
		{
			Type = type,
			Direction = (byte)direction
		} );
	}

	/// <summary>
	/// Takes a footprint back off the map - the demolisher's second footprint pass (<c>0x0052842b</c>),
	/// which clears each cell of the thing's shape. Each becomes <see cref="ParkPathBuilding.Cleared"/> bare
	/// ground, whoever placed the thing: the save's own things included, whose cells the save still records
	/// as theirs. See <c>docs/exe/park-engine.md</c>, "The demolisher's order, and the cells it leaves".
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The ends' overlap counters go to nought and the body's are kept</b>, as the clear's arms do for
	/// types 9 and 10 and do not for type 4.
	/// </para>
	/// <para>
	/// <b>A cell of the rectangle the thing does not own is left alone.</b> That is the shape's empty
	/// <c>.</c> cells, which the demolisher skips, for a thing the save placed. <see cref="Stamp"/> owns
	/// them for a thing bought here, so for that one they are cleared too (<c>SHAPE_KIND_0_PLACEMENT</c>).
	/// </para>
	/// <para>
	/// <b>Two deviations, both because the placement verdict is unbuilt</b> (<c>PLACEMENT_TERRAIN_RULE</c>),
	/// so a thing can stand where the original's refuses one. The outside-the-park flag is kept where the
	/// original clears the whole flag word, or selling would turn land outside the park into park. And a
	/// cell the save records as terrain the original never builds on - anything but bare ground, path,
	/// queue or a thing's footprint - goes back to the save's own record, since nothing but building on it
	/// can have changed it; clearing it would leave walkable ground where the save has rock.
	/// </para>
	/// <para>
	/// <b>Every cell it owns is cleared, but only the ANCHOR is left.</b> A placed thing is on exactly one
	/// cell's occupancy list, so that is the only cell there is anything to leave - and
	/// <see cref="ParkState.LeaveCell"/> ends by dropping the thing's own next and previous whether or not
	/// it found it on the cell asked about. Sweeping it across the whole footprint therefore wipes those
	/// links on the first cell and arrives at the anchor with nothing left to relink, putting nought into
	/// the head instead of promoting whoever stood behind the thing. The sweep starts at
	/// <c>footprint.Top/Left</c>, which is NOT the anchor for a turned thing - the shipped Staff Room is
	/// anchored (58,16) and covers (58,15)..(59,16) - so that ordering lost a guest for real, and only
	/// for turned things, which is why every test and a whole driven run stayed green over it.
	/// </para>
	/// </remarks>
	internal static void Unstamp( ParkState state, (int Left, int Top, int Right, int Bottom) footprint,
		int anchorX, int anchorY, int thingId )
	{
		for ( var y = footprint.Top; y <= footprint.Bottom; ++y )
		{
			for ( var x = footprint.Left; x <= footprint.Right; ++x )
			{
				if ( !ParkState.OnMap( x, y ) )
					continue;

				var cell = state.Record( x, y );

				if ( cell.ParentId != MapStep.CellId( anchorX, anchorY ) )
					continue;

				if ( state.Park?.CellAt( x, y ) is { } saved && !BuiltOver( saved.Type ) )
				{
					state.ClearRecord( x, y );
					continue;
				}

				var end = cell.Type is CellEdge.RideEnd or CellEdge.RideFarEnd;

				state.SetRecord( x, y, ParkPathBuilding.Cleared( cell ) with
				{
					Flags = (ushort)(cell.Flags & ParkPathBuilding.OutsideThePark),
					OverlapCounter = end ? (short)0 : cell.OverlapCounter
				} );
			}
		}

		state.LeaveCell( anchorX, anchorY, thingId );
	}

	/// <summary>
	/// Whether a cell of this type is one the original builds a thing over or clears to bare ground - bare
	/// ground, path, queue and a footprint's three kinds. Anything else is terrain; see <see cref="Unstamp"/>.
	/// </summary>
	private static bool BuiltOver( int type )
		=> type is CellEdge.Nothing or CellEdge.Path or ParkRideChoice.QueueCellType
			or FootprintType or CellEdge.RideEnd or CellEdge.RideFarEnd;

	/// <summary>Marks every cell of a footprint as built on, and names the thing standing there.</summary>
	/// <param name="anchorX">
	/// The cell the thing is anchored at, which is what every cell of its footprint names as its owner.
	/// <b>It is not the footprint's top-left</b>, and for a turned thing the two differ: the shipped
	/// park's Staff Room is anchored (58,16) and covers (58,15)..(59,16), its Round Fountain anchored
	/// (57,19) covering (57,17)..(59,19). <see cref="ParkPicking.ThingOn"/> matches the owner against
	/// each object's own cell, so keying this on the corner would leave exactly those two unfindable.
	/// </param>
	/// <remarks>
	/// Internal rather than private only so that it can be tested, the same reason
	/// <see cref="ParkPicking.ThingOn"/> is. A test that re-derives the owner instead of calling this one
	/// passes just as happily with the owner keyed on the footprint's corner - which is precisely the
	/// mutation that survived twice, until this became reachable.
	/// </remarks>
	internal static void Stamp( ParkState state, (int Left, int Top, int Right, int Bottom) footprint,
		int anchorX, int anchorY )
	{
		for ( var y = footprint.Top; y <= footprint.Bottom; ++y )
		{
			for ( var x = footprint.Left; x <= footprint.Right; ++x )
			{
				if ( !ParkState.OnMap( x, y ) )
					continue;

				var cell = state.Record( x, y );

				// The type, and the OWNER - which is how the save marks a footprint and therefore how a
				// click finds a thing anywhere but its anchor cell. Every cell of the shipped park's
				// footprints carries its owner's packed cell in mParentID while only the anchor carries
				// an occupant, so writing one without the other would leave a bought thing clickable on
				// a twelfth of itself. See ParkPicking.ThingOn.
				state.SetRecord( x, y, cell with
				{
					Type = FootprintType,
					ParentId = (ushort)MapStep.CellId( anchorX, anchorY )
				} );
			}
		}
	}

	/// <summary>
	/// The catalogue item in the player's hand, or nought for an empty one.
	///
	/// <para>
	/// <b>Carrying costs nothing, and that is the original's arrangement rather than a convenience.</b>
	/// Clicking a buy row compares the price against the balance and builds a mode holding the item's
	/// id - <c>FUN_0046c5a0( 4, itemId )</c> - but the money is not taken until the thing is actually
	/// put down, inside the object constructor. Which is why cancelling needs no refund: nothing was
	/// ever taken. A moved thing is in the hand already sold, so letting it go leaves it sold.
	/// </para>
	/// </summary>
	public static int Carrying { get; private set; }

	/// <summary>
	/// The way what is in the hand faces when it goes down, in degrees: a moved thing's own, and nought for a
	/// purchase. Nothing here turns it while it is carried.
	/// </summary>
	/// <remarks>
	/// <b>A deviation:</b> the original keeps one rotation, <c>DAT_0081d7a4</c>, which a purchase does not set,
	/// so an item bought over a carried move faces the moved thing's way (<c>docs/exe/park-engine.md</c>, "The
	/// hand's ways out"). Here a purchase always starts at nought.
	/// </remarks>
	public static int CarryingAngle { get; private set; }

	/// <summary>
	/// Takes an item into the hand, refusing the ones no buy list offers and the ones the park cannot
	/// afford - the two tests the original makes before it builds the placement mode.
	/// </summary>
	public static string Carry( int catalogueId )
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state
			|| level.Catalogue is not { } catalogue )
			return "carry: a park has to be loaded";

		if ( !catalogue.TryGet( catalogueId, out var item ) )
			return $"carry: this theme has no item {catalogueId}";

		if ( item.UiType is < 0 or > ItemDescriptionFile.Feature )
			return $"carry: '{item.Name}' is UI type {item.UiType}, which no buy tab lists";

		if ( state.Balance < item.BuildPrice )
			return $"carry: '{item.Name}' costs {item.BuildPrice} and the park has {state.Balance}";

		Hold( catalogueId, 0 );

		return $"carry: holding '{item.Name}' ({catalogueId}) at {item.BuildPrice}";
	}

	/// <summary>Puts an item in the hand, facing a way.</summary>
	private static void Hold( int catalogueId, int angle )
	{
		// Taking something into the hand installs the place mode over whatever was current - there is one
		// mode in the original - so a tool is put away and anything else in the hand let go of first.
		if ( ParkHand.LetGo() is { } letGo )
			Log.Info( $"Hand: {letGo}" );

		Carrying = catalogueId;
		CarryingAngle = angle;

		// While carrying, the original draws the footprint in coloured squares every tick - m_front along
		// its front row, m_enter on the cell before the entrance, m_exit before the exit - out of the same
		// marker vocabulary the queue tool uses. Only the queue tool's strip is built.
		Unimplemented.Report( "CARRY_PREVIEW_MARKERS" );
	}

	/// <summary>
	/// Empties the hand and builds nothing. <b>No refund, because holding costs nothing</b> - see
	/// <see cref="Carrying"/>.
	/// </summary>
	public static string Drop()
	{
		if ( Carrying == 0 )
			return "drop: the hand is already empty";

		var was = Carrying;
		Carrying = 0;

		return $"drop: let go of item {was} - nothing is built, and holding it cost nothing";
	}

	/// <summary>
	/// Builds what is in the hand at a cell. The hand is emptied only if it actually went up, so a
	/// refused placement leaves the item held rather than losing it.
	/// </summary>
	/// <remarks>
	/// <b>A thing with a queue hands the player the queue tool the moment it is down</b>, anchored on the
	/// queue cell the placer has just laid before its entrance - the original's commit handler, which
	/// writes that cell into the anchor with <c>FUN_0052a050</c> and switches to mode 3 with the setter
	/// that keeps it (<c>0x00525264</c>..<c>0x0052529e</c>). So the player's next click lays the queue
	/// from the ride to wherever they point; nobody has to find the entrance first.
	/// <para>
	/// The same commit posts advisor message <c>0xcb</c> and sets the <c>c_queue</c> cursor. The cursor is
	/// <see cref="Level"/>'s, read from the armed mode each frame; the message's words are not decoded.
	/// </para>
	/// </remarks>
	public static string PlaceCarried( int cellX, int cellY, int? angle = null )
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state
			|| level.Catalogue is not { } catalogue || ParkObjects.Current is not { } objects
			|| level.Park is not { } park )
			return "put: a park has to be loaded";

		return PlaceCarried( state, park, catalogue, objects, ParkRides.Current, cellX, cellY, angle );
	}

	/// <summary>
	/// The whole of <see cref="PlaceCarried(int, int, int?)"/> once the park is in hand. With no
	/// <paramref name="angle"/> the thing faces the way the hand holds it.
	/// </summary>
	private static string PlaceCarried( ParkState state, ParkWorld park, ParkItemCatalogue catalogue,
		ParkObjects? objects, ParkRides? rides, int cellX, int cellY, int? angle )
	{
		if ( Carrying == 0 )
			return "put: the hand is empty - `carry <item>` first, or click a row on the buy screen";

		var built = Build( state, park, catalogue, objects, rides, Carrying, cellX, cellY, angle ?? CarryingAngle );

		// A click on a red cell plays sound 0xaf (0x00524aae) and leaves the hand as it is, so the next click
		// tries again.
		if ( built.Refused )
			Unimplemented.Report( "PLACEMENT_REFUSED_SOUND_0xAF" );

		if ( built.ThingId == 0 )
			return built.Answer;

		// Karts and the water ride lay their first track cells here, before the queue is seeded
		// (0x00524db7..0x00524e49). There is no track layer to lay them in.
		if ( built.TrackType is 1 or 2 )
			Unimplemented.Report( "PLACED_TRACK_RIDE_FIRST_TRACK_CELLS" );

		if ( built.QueueNode is not { } node )
		{
			// A thing with no queue sends the tool back to idle (FUN_0052f580( 0, 0 ) at 0x005253c4) -
			// unless Ctrl alone is held, which keeps it in the hand to place another of the same.
			ParkBuildMode.Disarm();

			if ( !built.HasQueue && Input.ControlAlone )
				return $"{built.Answer} - Ctrl is held, so another is still in the hand";

			Carrying = 0;

			return built.Answer;
		}

		Carrying = 0;

		// FUN_0052a050 ORs the entrance's bit toward the node back in before it anchors there, which
		// matters when the node was laid over a path: clearing that path unlinked the entrance from it.
		if ( state.TryObject( built.ThingId, out var placed )
			&& ParkState.OnMap( placed.EntryCellX, placed.EntryCellY ) )
		{
			var towardNode = BitToward( placed.EntryCellX, placed.EntryCellY, node.X, node.Y );
			var entrance = state.Record( placed.EntryCellX, placed.EntryCellY );

			state.SetRecord( placed.EntryCellX, placed.EntryCellY,
				entrance with { Neighbours = (byte)(entrance.Neighbours | towardNode) } );
		}

		Unimplemented.Report( "QUEUE_MODE_ADVISOR_MESSAGE_0xCB" );

		return $"{built.Answer} - {ParkBuildMode.ArmAt( ParkBuildMode.Queue, built.ThingId, node.X, node.Y )}";
	}

	/// <summary>What is in the hand, for the debug console.</summary>
	public static string HandState()
	{
		if ( Carrying == 0 )
			return "hand: empty";

		var name = Level.Current?.Catalogue is { } catalogue && catalogue.TryGet( Carrying, out var item )
			? $"'{item.Name}' ({Carrying}) at {item.BuildPrice}"
			: $"item {Carrying}";

		return $"hand: holding {name}, turned {CarryingAngle}";
	}

	/// <summary>Every object standing in the park now, for the debug console.</summary>
	public static IEnumerable<string> Census()
	{
		if ( Level.Current is not { } level || level.ParkState is not { } state )
			yield break;

		foreach ( var placed in state.Objects )
		{
			var name = $"item {placed.CatalogueId}";
			var type = -1;

			if ( level.Catalogue is { } catalogue && catalogue.TryGet( placed.CatalogueId, out var item ) )
			{
				name = item.Name;
				type = item.UiType;
			}

			// The UI TYPE is here because it is what decides which of the nine object windows a click
			// opens, and a test that wants a ride should not have to probe the park one thing at a time
			// to find one - doing that stopped at the first ride and left two things unclassified.
			yield return $"thing {placed.ThingId,3} '{name}' item {placed.CatalogueId} type {type} " +
				$"at ({placed.CellX},{placed.CellY}) turned {placed.Angle}" +
				$"{(placed.IsPlaced ? "" : " (not placed)")}";
		}
	}
}
