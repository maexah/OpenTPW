namespace OpenTPW;

/// <summary>
/// What the thing that owns an entrance says to a viewer walking at it - the entrance arm of the original's edge
/// test on mode 2 (<c>0x004d8883</c>; docs/exe/park-engine.md, "An entrance is shut to the viewer"), the answer
/// <see cref="CellEdge"/> takes as its <c>queueAhead</c>.
///
/// <para>
/// The first catalogue object on the owner's cell decides alone: shut unless it has a ride view and its item's
/// <c>UsageInfo.CannotRide</c> is nought. No link of the cell is read, so an open entrance is open from its own
/// footprint and a shut one is shut from the path it joins.
/// </para>
/// </summary>
/// <remarks>
/// The original walks the owner cell's thing chain; the placed objects anchored on that cell, in the park's order,
/// are this port's copy of it, as they are for <see cref="ParkCamcorderCameraMode.RideAt"/>. No owner's cell of the
/// shipped park holds two.
/// </remarks>
public sealed class ParkEntranceGate
{
	/// <summary>The space a model's view node is in (<c>FUN_0044b220( model, 0x1000, 1 )</c>).</summary>
	public const uint ViewSpace = 0x1000;

	/// <summary>The id a model's view node carries.</summary>
	public const int ViewNodeId = 1;

	private readonly Func<IReadOnlyList<ParkWorld.CatalogueObject>> _objects;
	private readonly ParkItemCatalogue _catalogue;
	private readonly Func<ParkItemCatalogue.Item, bool> _hasViewNode;
	private readonly Func<int, bool> _runsATour;
	private readonly Dictionary<int, bool> _viewNodes = [];

	/// <param name="objects">The running park's placed objects (<see cref="ParkState.Objects"/>).</param>
	/// <param name="catalogue">The park's items.</param>
	/// <param name="hasViewNode">
	/// Whether an item's model carries the view node - <see cref="ModelHasViewNode"/> for a real park. Asked once
	/// an item.
	/// </param>
	/// <param name="runsATour">Whether a thing's script carries <c>TOUR</c>, by thing id. Left out, none does.</param>
	public ParkEntranceGate( Func<IReadOnlyList<ParkWorld.CatalogueObject>> objects, ParkItemCatalogue catalogue,
		Func<ParkItemCatalogue.Item, bool> hasViewNode, Func<int, bool>? runsATour = null )
	{
		_objects = objects ?? throw new ArgumentNullException( nameof( objects ) );
		_catalogue = catalogue ?? throw new ArgumentNullException( nameof( catalogue ) );
		_hasViewNode = hasViewNode ?? throw new ArgumentNullException( nameof( hasViewNode ) );
		_runsATour = runsATour ?? (_ => false);
	}

	/// <summary>The gate of a running park, its models read from where the catalogue's items are.</summary>
	public static ParkEntranceGate For( ParkState state, ParkItemCatalogue catalogue )
	{
		ArgumentNullException.ThrowIfNull( state );
		ArgumentNullException.ThrowIfNull( catalogue );

		return new ParkEntranceGate( () => state.Objects, catalogue,
			item => ModelHasViewNode( item, catalogue.Files ),
			thing => ParkRides.Current is { } rides && rides.ScriptFor( thing ) is var id and not 0
				&& rides.Scheduler.Find( id )?.UsesTour == true );
	}

	/// <summary>
	/// The verdict on an entrance cell. <see cref="QueueVerdict.NothingThere"/> where its owner's cell holds no
	/// catalogue object, which hands the step back to the ordinary tests.
	/// </summary>
	public QueueVerdict Ahead( ParkWorld.MapCell entrance )
	{
		// An entrance naming no owner is in no shipped park; what the original's chain walk reads for it is not
		// decoded.
		if ( entrance.ParentId == 0 )
			return QueueVerdict.NothingThere;

		var (ownerX, ownerY) = MapStep.CellAt( entrance.ParentId );

		foreach ( var placed in _objects() )
		{
			if ( placed.CellX != ownerX || placed.CellY != ownerY )
				continue;

			// The first decides, and nothing after it is asked. The view is asked before the item (0x004d8b00).
			if ( !_catalogue.TryGet( placed.CatalogueId, out var item ) || !HasView( placed, item ) )
				return QueueVerdict.InTheWay;

			return item.CannotRide ? QueueVerdict.InTheWay : QueueVerdict.LetThemThrough;
		}

		return QueueVerdict.NothingThere;
	}

	/// <summary>
	/// Whether a placed thing has a ride view - <c>FUN_0042a440</c>, which answers at the first of four that holds.
	/// <b>Only the fourth is built</b>, the model's own view node. The three before it need a track ride's lead car,
	/// a <c>TOUR</c> record's first car and a coaster's node, none of which is kept here: each is counted where the
	/// original would ask it and taken as not holding, so such a thing answers by its model's node alone. Lost
	/// Kingdom's stock park meets none of the three.
	/// </summary>
	public bool HasView( ParkWorld.CatalogueObject placed, ParkItemCatalogue.Item item )
	{
		if ( placed.TrackRide != 0 )
			Unimplemented.Report( "RIDE_VIEW_TRACK_RIDE_LEAD_CAR" );

		if ( _runsATour( placed.ThingId ) )
			Unimplemented.Report( "RIDE_VIEW_TOUR_CAR" );

		if ( item.TrackType == ItemDescriptionFile.CoasterTrack )
			Unimplemented.Report( "RIDE_VIEW_COASTER_NODE" );

		if ( !_viewNodes.TryGetValue( item.Id, out var has ) )
			_viewNodes[item.Id] = has = _hasViewNode( item );

		return has;
	}

	/// <summary>
	/// Whether an item's model, <c>&lt;stem&gt;.md2</c> in its own directory, carries the view node. A model that is
	/// missing or will not read has none, and the log says so.
	/// </summary>
	public static bool ModelHasViewNode( ParkItemCatalogue.Item item, BaseFileSystem files )
	{
		try
		{
			using var stream = files.OpenRead( $"{item.Directory}/{item.Stem}.MD2" );

			if ( stream != null )
				return new ModelFile( stream ).FindNode( ViewNodeId, ViewSpace ) >= 0;

			Log.Warning( $"Entrance gate: '{item.Name}' has no model, so no ride view" );
		}
		catch ( Exception e )
		{
			Log.Warning( $"Entrance gate: the model of '{item.Name}' will not read, so no ride view - {e.Message}" );
		}

		return false;
	}
}
