namespace OpenTPW;

/// <summary>
/// The scene's height under a point of the park, as the original's rides ask for it: by the object standing on the
/// point's cell (<c>docs/exe/park.md</c>, "The scene's height under a point").
/// </summary>
internal sealed class ParkSceneHeight( ParkItemCatalogue? catalogue )
{
	/// <summary>What each item's model adds to the landscape's height under a point, by item id, read once; null where it adds none.</summary>
	private readonly Dictionary<int, float?> _lifts = [];

	/// <summary>
	/// The scene's height under a point - <c>FUN_00450ac0</c>, <c>FUN_00450ea0</c> and <c>FUN_004511a0</c>
	/// (<c>docs/exe/park.md</c>, "The scene's height under a point"): the object the point's cell belongs to says how.
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

		var cellX = Math.Clamp( (int)(x / field.CellSizeX), 0, field.CellsX - 1 );
		var cellY = Math.Clamp( (int)(y / field.CellSizeY), 0, field.CellsY - 1 );

		if ( StandingOn( state.Record( cellX, cellY ), state ) is not { } item )
			return last;

		if ( !_lifts.TryGetValue( item.Id, out var lift ) )
		{
			lift = LiftOf( item );
			_lifts[item.Id] = lift;
		}

		return lift is { } above ? field.ScapeHeight( x, y ) + above : last;
	}

	/// <summary>
	/// The catalogue item of the object a cell belongs to, or null where it is no object's - <c>FUN_00527d60</c>: a
	/// footprint, entrance or exit cell's parent cell names it.
	/// </summary>
	private ParkItemCatalogue.Item? StandingOn( ParkWorld.MapCell cell, ParkState state )
	{
		if ( catalogue is null || cell.Type is not (CellEdge.Footprint or CellEdge.RideEnd or CellEdge.RideFarEnd) )
			return null;

		var owner = ParkPathBuilding.OwnerOf( state, cell );

		foreach ( var placed in state.Objects )
		{
			if ( owner != 0 && placed.ThingId == owner )
				return catalogue.TryGet( placed.CatalogueId, out var item ) ? item : null;
		}

		return null;
	}

	/// <summary>What an item's model adds to the landscape's height, or null where its height is taken another way, counted once.</summary>
	private float? LiftOf( ParkItemCatalogue.Item item )
	{
		using var stream = FileSystem.OpenRead( $"{item.Directory}/{item.Stem}.MD2" );

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
