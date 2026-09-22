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
			|| level.Catalogue is not { } catalogue || ParkObjects.Current is not { } objects
			|| level.Park is not { } park )
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
		// mExitPos the same with the exit's. Left at the record's default of nought, CanBeOffered refuses
		// the thing before the queue is ever walked - so a queue laid and joined to a path still measures
		// no cells at all, which reads exactly like a queue fault and is not one.
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

		if ( !objects.PlaceNow( placed, catalogue ) )
			return $"buy: '{item.Name}' would not load, so nothing was built and nothing was charged";

		state.AddObject( placed );
		Stamp( state, footprint, cellX, cellY );

		// And onto its anchor cell's own list, which is the ONE cell the save puts a placed thing on -
		// through EnterCell, so a guest standing there is kept behind it rather than evicted. The rest
		// of the footprint is reached through the owner Stamp just wrote, not through occupancy: a
		// thing cannot be on twelve cells' lists, because those links are keyed per thing and the
		// twelfth would overwrite the first.
		state.EnterCell( cellX, cellY, thingId );

		// After the footprint, never before: Stamp types every cell it covers, and the way in and the way
		// out are two of those cells wearing a different type.
		MarkWaysInAndOut( state, park, cellX + entryX, cellY + entryY,
			cellX + exitX, cellY + exitY, angle );

		state.Spend( item.BuildPrice );

		// The ground draws every cell nothing else owns, so it has to be told that these are owned now
		// or it keeps drawing grass through the new floor - and the paths and queues are rebuilt with it,
		// because the three divide the map by cell and one rebuilt alone leaves a hole. See ParkSurfaces.
		ParkSurfaces.Rebuild();

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

		Unstamp( state, footprint, placed.CellX, placed.CellY, thingId );

		state.Refund( refund );

		ParkSurfaces.Rebuild();

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
	/// The direction byte the shipped park puts on a ride's way in and way out, at no turn. <b>Both are
	/// measured off the game rather than derived</b>, because the two compasses in this tree disagree by
	/// name: <see cref="CellEdge.BitFor"/> calls <c>0x10</c> north while the save's own compass
	/// (<c>docs/exe/park.md</c>) calls <c>0x01</c> north at −y. Reasoning from either would have had even
	/// odds of storing the byte inverted, which reads exactly like the cell not being marked at all.
	/// </summary>
	/// <remarks>
	/// Read out of the running game at the Belly Bounce, thing 13, anchored (51,23) at angle 0:
	/// its entrance (52,23) is <c>type 9 direction 0x01</c> and its exit (52,26) <c>type 10 direction
	/// 0x10</c> — the two ends of its middle column, each pointing out of the footprint — while every
	/// other cell of the box is <c>type 4 direction 0x00</c>.
	/// </remarks>
	private const int WayIn = 0x01;

	/// <inheritdoc cref="WayIn"/>
	private const int WayOut = 0x10;

	/// <summary>
	/// Turns one compass bit by a quarter for each quarter the thing is turned. The compass is a ring of
	/// eight - <c>0x01 N, 0x02 NE, 0x04 E, …</c> - so a quarter turn is two places round it, which is the
	/// same nibble arithmetic <see cref="CellEdge.Opposite"/> does for a half turn.
	/// </summary>
	internal static int RotateBit( int bit, int angle )
	{
		var quarters = ((((angle % 360) + 360) % 360) / 90) * 2;

		return ((bit << quarters) | (bit >> (8 - quarters))) & 0xff;
	}

	/// <summary>
	/// Types the cell a guest goes in by and the one they come out of, so that something can be joined to
	/// them. <b>Without this a bought thing is type 4 all over</b>, and
	/// <see cref="ParkPathNeighbours"/>'s cardinal rule links a neighbour of type 1, 9 or 10 and nothing
	/// else - so no path and therefore no queue could ever attach to it, and
	/// <see cref="ParkRideChoice.StartOfQueue"/> would read an empty neighbour mask for ever.
	/// </summary>
	private static void MarkWaysInAndOut( ParkState state, ParkWorld park, int entryCellX, int entryCellY,
		int exitCellX, int exitCellY, int angle )
	{
		var wayIn = RotateBit( WayIn, angle );

		Mark( state, entryCellX, entryCellY, CellEdge.RideEnd, wayIn );
		JoinToWhateverIsThere( state, park, entryCellX, entryCellY, wayIn );

		// The far end only where the item declares one. An item whose picture marks no exit leaves it on
		// the entrance, and writing type 10 over the 9 would lose the way in.
		if ( exitCellX == entryCellX && exitCellY == entryCellY )
			return;

		var wayOut = RotateBit( WayOut, angle );

		Mark( state, exitCellX, exitCellY, CellEdge.RideFarEnd, wayOut );
		JoinToWhateverIsThere( state, park, exitCellX, exitCellY, wayOut );
	}

	/// <summary>
	/// Runs the neighbour rule over whatever the way in or out points at, <b>and only where that is
	/// already a path</b> - the original's <c>FUN_00528a70</c>, whose <c>case 9</c> and <c>case 10</c>
	/// arms each turn the cell's heading by the placement angle, step to the cell it names, and call
	/// <c>FUN_005348d0</c> there when that cell's type is 1.
	/// </summary>
	/// <remarks>
	/// <b>So the join is earned at PLACEMENT, against what is already standing.</b> Building beside a
	/// path links the two; laying a path beside a thing already built is the other arm, and belongs to
	/// the path tool rather than here.
	/// </remarks>
	private static void JoinToWhateverIsThere( ParkState state, ParkWorld park, int x, int y, int heading )
	{
		var (acrossBy, downBy) = StepFor( heading );
		var (nextX, nextY) = (x + acrossBy, y + downBy);

		if ( (acrossBy == 0 && downBy == 0) || !ParkState.OnMap( nextX, nextY ) )
			return;

		if ( ParkState.CellFor( park, nextX, nextY ).Type != CellEdge.Path )
			return;

		ParkPathNeighbours.LinkPath( state, park, nextX, nextY );
	}

	/// <summary>
	/// The step one compass bit stands for. The ring is the executable's own, confirmed from the static
	/// initialisers and <c>FUN_004d97e0</c>'s jump table rather than from save statistics: <c>0x01</c> is
	/// (0,−1) and <c>0x10</c> is (0,+1). <b>This is the outward sense</b>, the mirror of
	/// <see cref="CellEdge.BitFor"/>, and mixing the two inverts every answer.
	/// </summary>
	private static (int X, int Y) StepFor( int bit ) => bit switch
	{
		0x01 => (0, -1),
		0x04 => (1, 0),
		0x10 => (0, 1),
		0x40 => (-1, 0),
		_ => (0, 0)
	};

	/// <summary>
	/// One cell of a footprint given the type, the heading, and the link that lets a queue be found from
	/// it.
	/// </summary>
	/// <remarks>
	/// <b>The neighbour bit is a DECLARED DEVIATION: the end state is the original's, the step that
	/// produces it is not.</b> Every route the engine takes to that bit has been followed and none of
	/// them writes it for a thing built in play. <c>FUN_005348d0</c> is the only writer, through paired
	/// <c>FUN_00522700</c> calls, and its cardinal test refuses here: a cell laid on the entrance's own
	/// queue side steps toward it with the opposite bit to the heading the entrance carries, so
	/// <c>neighbour.Direction &amp; bit</c> is nought. <c>FUN_00528a70</c>'s <c>case 9</c> only re-runs
	/// that same rule on an adjacent path, and <c>FUN_00532fc0</c>'s ops <c>0x81</c>, <c>0x85</c> and
	/// <c>0x86</c> - the obvious candidates, called right after it - retile, do track bookkeeping and
	/// notify the thing. <b>The shipped park's own entrance could not have earned its bit under the
	/// decoded rule either</b>, so in the original it comes from somewhere still unfound.
	/// <para>
	/// What IS measured is the state that must hold: the Belly Bounce's entrance reads
	/// <c>neighbours 0x01 direction 0x01</c> with its queue on the <c>-y</c> side, and
	/// <see cref="ParkRideChoice.StartOfQueue"/> maps that <c>0x01</c> to the step <c>(0,-1)</c> - the
	/// queue cell. So the bit is written to match the shipped data rather than invented, and without it
	/// nothing a player builds can ever be queued for, which is the risky blank rule 11 is about.
	/// The mechanism is counted, not the value.
	/// </para>
	/// </remarks>
	private static void Mark( ParkState state, int x, int y, int type, int direction )
	{
		if ( !ParkState.OnMap( x, y ) )
			return;

		Unimplemented.Report( "RIDE_END_NEIGHBOUR_AUTHORING" );

		state.SetRecord( x, y, state.Record( x, y ) with
		{
			Type = type,
			Direction = (byte)direction,
			Neighbours = (byte)direction
		} );
	}

	/// <summary>
	/// Takes a footprint back off the map - the exact undoing of <see cref="Stamp"/>, which is why the two
	/// sit together rather than this living as a loop inside <see cref="Sell"/>.
	/// </summary>
	/// <remarks>
	/// <b>Every cell loses its record, but only the ANCHOR is left.</b> A placed thing is on exactly one
	/// cell's occupancy list, so that is the only cell there is anything to leave - and
	/// <see cref="ParkState.LeaveCell"/> ends by dropping the thing's own next and previous whether or not
	/// it found it on the cell asked about. Sweeping it across the whole footprint therefore wipes those
	/// links on the first cell and arrives at the anchor with nothing left to relink, putting nought into
	/// the head instead of promoting whoever stood behind the thing. The sweep starts at
	/// <c>footprint.Top/Left</c>, which is NOT the anchor for a turned thing - the shipped Staff Room is
	/// anchored (58,16) and covers (58,15)..(59,16) - so that ordering lost a guest for real, and only
	/// for turned things, which is why every test and a whole driven run stayed green over it.
	/// </remarks>
	internal static void Unstamp( ParkState state, (int Left, int Top, int Right, int Bottom) footprint,
		int anchorX, int anchorY, int thingId )
	{
		for ( var y = footprint.Top; y <= footprint.Bottom; ++y )
		{
			for ( var x = footprint.Left; x <= footprint.Right; ++x )
				state.ClearRecord( x, y );
		}

		state.LeaveCell( anchorX, anchorY, thingId );
	}

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
	/// ever taken.
	/// </para>
	/// </summary>
	public static int Carrying { get; private set; }

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

		Carrying = catalogueId;

		return $"carry: holding '{item.Name}' ({catalogueId}) at {item.BuildPrice}";
	}

	/// <summary>
	/// Puts down whatever is in the hand. <b>No refund, because nothing was charged</b> - see
	/// <see cref="Carrying"/>.
	/// </summary>
	public static string Drop()
	{
		if ( Carrying == 0 )
			return "drop: the hand is already empty";

		var was = Carrying;
		Carrying = 0;

		return $"drop: put item {was} back - nothing was charged for holding it";
	}

	/// <summary>
	/// Builds what is in the hand at a cell. The hand is emptied only if it actually went up, so a
	/// refused placement leaves the item held rather than losing it.
	/// </summary>
	public static string PlaceCarried( int cellX, int cellY, int angle = 0 )
	{
		if ( Carrying == 0 )
			return "put: the hand is empty - `carry <item>` first, or click a row on the buy screen";

		var answer = Buy( Carrying, cellX, cellY, angle );

		if ( answer.StartsWith( "buy: '" ) && answer.Contains( "built as thing" ) )
			Carrying = 0;

		return answer;
	}

	/// <summary>What is in the hand, for the debug console.</summary>
	public static string HandState()
	{
		if ( Carrying == 0 )
			return "hand: empty";

		var name = Level.Current?.Catalogue is { } catalogue && catalogue.TryGet( Carrying, out var item )
			? $"'{item.Name}' ({Carrying}) at {item.BuildPrice}"
			: $"item {Carrying}";

		return $"hand: holding {name}";
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
