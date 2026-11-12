namespace OpenTPW;

/// <summary>
/// The scene's height under a point of the park, as the original's rides ask for it: by the object standing on the
/// point's cell (<c>docs/exe/park.md</c>, "The scene's height under a point").
/// </summary>
internal sealed class ParkSceneHeight( ParkItemCatalogue? catalogue, Func<ParkItemHeights.GateRow>? gateRow = null )
{
	/// <summary>The gate's thing and its row's two entrance cells, asked at each call: the park's own unless a test gives its own.</summary>
	private readonly Func<ParkItemHeights.GateRow> _gateRow = gateRow ?? ParkItemHeights.ParkGateRow;

	/// <summary>What each item's model adds to the landscape's height under a point, by item id, read once; null where it adds none.</summary>
	private readonly Dictionary<int, float?> _lifts = [];

	/// <summary>
	/// The scene's height under a point - <c>FUN_00450ac0</c>, <c>FUN_00450ea0</c> and <c>FUN_004511a0</c>
	/// (<c>docs/exe/park.md</c>, "The scene's height under a point"): the object the point's cell answers says how, the
	/// gate on every cell of its row.
	/// One whose model's header float is not nought (<see cref="ModelFile.SurfaceLift"/>, the Hot Pot's 29.8) gives the
	/// landscape's height there plus that float. A cell that is no object's keeps <paramref name="last"/>, the height the
	/// caller last had, as does each way not built: a model with meshes to stand on
	/// (<c>SCENE_HEIGHT_SURFACE_MESHES</c>), one with neither (<c>SCENE_HEIGHT_OWN_MESH</c>), and a track piece's,
	/// which no car here drives over.
	/// </summary>
	public float Under( float x, float y, ParkState state, HeightfieldFile? field, float last )
	{
		if ( field is null )
			return last;

		if ( ItemUnder( x, y, state, field ) is not { } item )
			return last;

		if ( !_lifts.TryGetValue( item.Id, out var lift ) )
		{
			lift = LiftOf( item );
			_lifts[item.Id] = lift;
		}

		return lift is { } above ? ParkGround.ScapeHeight( field, x, y ) + above : last;
	}

	/// <summary>The catalogue item of the object whose model says the height under a point, or null where none does (<see cref="StandingOn"/>).</summary>
	internal ParkItemCatalogue.Item? ItemUnder( float x, float y, ParkState state, HeightfieldFile field )
		=> StandingOn( Math.Clamp( (int)(x / field.CellSizeX), 0, field.CellsX - 1 ),
			Math.Clamp( (int)(y / field.CellSizeY), 0, field.CellsY - 1 ), state );

	/// <summary>
	/// The catalogue item of the object a cell answers, or null where it answers none - <c>FUN_00527e80</c>: a cell of the
	/// gate's row (<see cref="ParkItemHeights.OnGateRow"/>) answers the gate, whatever it holds, and nothing where the gate
	/// is no placed object (the original's type byte 3, <c>0x00452ac8</c>); any other goes to <c>FUN_00527d60</c>, where a
	/// footprint, entrance or exit cell's parent cell names it.
	/// </summary>
	private ParkItemCatalogue.Item? StandingOn( int cellX, int cellY, ParkState state )
	{
		if ( catalogue is null )
			return null;

		var row = _gateRow();

		if ( ParkItemHeights.OnGateRow( row.EntranceA, row.EntranceB, cellX, cellY ) )
			return ItemOf( state, row.Gate );

		var cell = state.Record( cellX, cellY );

		if ( cell.Type is not (CellEdge.Footprint or CellEdge.RideEnd or CellEdge.RideFarEnd) )
			return null;

		return ItemOf( state, ParkPathBuilding.OwnerOf( state, cell ) );
	}

	/// <summary>The catalogue item of the placed object <paramref name="owner"/>, or null where no placed object is it.</summary>
	private ParkItemCatalogue.Item? ItemOf( ParkState state, int owner )
	{
		foreach ( var placed in state.Objects )
		{
			if ( owner != 0 && placed.ThingId == owner )
				return catalogue!.TryGet( placed.CatalogueId, out var item ) ? item : null;
		}

		return null;
	}

	/// <summary>What an item's model adds to the landscape's height, or null where its height is taken another way, counted once.</summary>
	private float? LiftOf( ParkItemCatalogue.Item item )
	{
		using var stream = FileSystem.OpenReadOrNull( $"{item.Directory}/{item.Stem}.MD2" );

		if ( stream is null )
			return null;

		var file = new ModelFile( stream );

		if ( file.HasSurfaceMeshes )
		{
			Unimplemented.Report( "SCENE_HEIGHT_SURFACE_MESHES" );
			return null;
		}

		if ( file.SurfaceLift == 0f )
		{
			Unimplemented.Report( "SCENE_HEIGHT_OWN_MESH" );
			return null;
		}

		return file.SurfaceLift;
	}
}
