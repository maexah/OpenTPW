namespace OpenTPW;

/// <summary>
/// The records a park made fresh is written from: the whole of a cell, which the patching writers (<see cref="PutCells"/>)
/// only write over (FileFormats <c>saves.md</c>, "The map", and "A park made fresh").
/// </summary>
public sealed partial class ParkWorld
{
	/// <summary>The bytes of a cell with a map record and a track record and no effects: the status byte and 83 more.</summary>
	public const int FreshCellSize = 1 + MapCellSize + TrackCellSize;

	/// <summary>
	/// What a cell's track record holds that <see cref="MapCell"/> does not: its model (<c>mMeshInstance</c>, the boundary's
	/// piece's slot plus one), its <c>mHoardingNeighbours</c>, and the segment number, <c>0xffff</c> on every cell of every
	/// file measured.
	/// </summary>
	public readonly record struct TrackExtras( int MeshInstance = 0, byte HoardingNeighbours = 0, ushort Segment = 0xffff );

	/// <summary>
	/// Writes <paramref name="cell"/> whole at <paramref name="at"/> in <paramref name="body"/>, as the original's writer does
	/// every cell: status 3 and the map and track records. The effects record is the effects writer's
	/// (<see cref="PutEffects"/>), and the litter block is as the cell holds it.
	/// </summary>
	public static void PutFreshCell( byte[] body, int at, MapCell cell, TrackExtras track )
	{
		body[at] = MapRecord | TrackRecord;

		var map = at + 1;
		body[map + CellDirection] = cell.Direction;
		PutUInt16( body, map + CellFlags, cell.Flags );
		PutInt32( body, map + CellMeshInstance, cell.MeshInstance );
		body[map + CellNeighbours] = cell.Neighbours;
		PutUInt16( body, map + CellOverlapCounter, unchecked((ushort)cell.OverlapCounter) );
		PutUInt16( body, map + CellParent, cell.ParentId );
		PutInt32( body, map + CellTileData, cell.TileSet );
		PutInt32( body, map + CellTileData + 4, cell.TileIndex );
		PutInt32( body, map + CellTileData + 8, cell.TileAngle );
		PutInt32( body, map + CellType, cell.Type );
		PutInt32( body, map + CellLitter, cell.Litter );
		PutUInt16( body, map + CellLitterCollector, cell.LitterCollector );
		PutUInt16( body, map + CellPylonIndex, cell.PylonIndex );
		body[map + CellStatusFlags] = cell.StatusFlags;
		PutInt32( body, map + CellTimeMarkedForLitterCollection, cell.TimeMarkedForLitterCollection );
		PutUInt16( body, map + CellOccupant, cell.Occupant );

		var tr = map + MapCellSize;
		body[tr + CellDirection] = cell.TrackDirection;
		PutUInt16( body, tr + CellFlags, cell.TrackFlags );
		PutInt32( body, tr + CellMeshInstance, track.MeshInstance );
		body[tr + CellNeighbours] = cell.TrackNeighbours;
		PutUInt16( body, tr + CellOverlapCounter, unchecked((ushort)cell.TrackOverlapCounter) );
		PutUInt16( body, tr + CellParent, cell.TrackParentId );
		PutInt32( body, tr + CellTileData, cell.TrackTileSet );
		PutInt32( body, tr + CellTileData + 4, cell.TrackTileIndex );
		PutInt32( body, tr + CellTileData + 8, cell.TrackTileAngle );
		PutInt32( body, tr + CellType, cell.TrackType );
		body[tr + CellHoardingNeighbours] = track.HoardingNeighbours;
		PutUInt16( body, tr + CellSegment, track.Segment );
	}

	private const int CellHoardingNeighbours = 28;
	private const int CellSegment = 29;
}
