using System.Numerics;
using Numerics = System.Numerics;
using System.Runtime.CompilerServices;

namespace OpenTPW;

/// <summary>
/// Each placed coaster's highest track point, rebuilt from its pieces in the save (<see cref="ParkCoasterTrack"/>), for
/// Global 0. The chain is the original's (<c>docs/exe/ride-operation.md</c>, "A coaster's highest track point"):
/// <list type="number">
/// <item>the station's three nodes, the coaster model's track nodes 1, 2 and 3 posed where it stands
/// (<c>Coaster_MakeStationNodes</c> <c>0x00436ac0</c>; the type flag <c>0x20</c> that asks for three is set on every
/// coaster, <c>0x0042f390</c>): the first and the third on a pylon on the ground cell under them, at the height of the
/// track node over the model's root (<c>Coaster_LoftForHeight</c> <c>0x00433790</c>), the second at the track node itself;</item>
/// <item>one node a piece, its pylon's knot at the piece's loft on its cell, facing the piece's yaw and pitched by the
/// pylon's incline clip;</item>
/// <item>a closing node at the first's place.</item>
/// </list>
/// A pylon's cell stands at the midpoint of its four corners' lowest and highest ground (<c>FUN_00466d70</c>).
/// </summary>
/// <remarks>
/// <b>The station's nodes and the closing node face along the station, level</b>, as all four do in the original
/// (<c>q706b/O1/full.txt</c>), the third although the first piece stands above it; which of their flags keeps the third
/// and the closing node from turning to their neighbours is not traced. A stacked pylon, a loop pylon and a pylon with
/// more than one knot or a turned knot are not decoded, and none is in Lost Kingdom's files on hand: each is counted and
/// gives its coaster no height.
/// </remarks>
public static class ParkCoasterHeights
{
	/// <summary>Each saved park's coasters' heights by thing, worked out once: nothing here changes a coaster's track.</summary>
	private static readonly ConditionalWeakTable<ParkWorld, Dictionary<int, float?>> _measured = new();

	/// <summary>
	/// The highest track point of every coaster placed in <paramref name="state"/>: the things whose item's track type is 3
	/// (<c>Thing_IsCoasterTrack</c> <c>0x004e2b50</c>), whatever their UI type (<c>Park_HighestCoasterPoint</c> <c>0x004cc920</c>). A coaster whose
	/// track could not be rebuilt gives nothing.
	/// </summary>
	public static IEnumerable<float> Of( ParkState state, ParkItemCatalogue? catalogue, BaseFileSystem files )
	{
		var save = state.Park?.Save;
		var field = ParkGround.Current?.Heightfield;

		foreach ( var placed in state.Objects )
		{
			if ( catalogue == null || !catalogue.TryGet( placed.CatalogueId, out var item )
				|| item.TrackType != ItemDescriptionFile.CoasterTrack )
				continue;

			if ( save == null || field == null )
			{
				Unimplemented.Report( "COASTER_TRACK_UNSAVED" );
				continue;
			}

			var known = _measured.GetOrCreateValue( save );

			if ( !known.TryGetValue( placed.ThingId, out var height ) )
				known[placed.ThingId] = height = Measure( save, placed, item, field, files );

			if ( height is { } highest )
				yield return highest;
		}
	}

	/// <summary>One coaster's highest track point, or null where its track cannot be rebuilt (each reason counted).</summary>
	internal static float? Measure( ParkWorld save, ParkWorld.CatalogueObject placed, ParkItemCatalogue.Item item,
		HeightfieldFile field, BaseFileSystem files )
	{
		if ( SetUp( save, placed, item, field, files ) is not { } coaster )
			return null;

		var chain = Chain( save.Coasters.FirstPieces, coaster.Pylon, field, coaster.RootHeight, coaster.TrackNode );

		return chain == null ? null : ParkCoasterTrack.Highest( ParkCoasterTrack.Samples( chain, coaster.Curviness ) );
	}

	/// <summary>A placed coaster's pylon type, curviness and station nodes, stood where the coaster stands.</summary>
	/// <param name="RootHeight">The height of the coaster model's root, which a station pylon's height is measured from.</param>
	internal sealed record CoasterSetup( ParkCoasterPylon Pylon, float Curviness, RideNodes Station, float RootHeight )
	{
		/// <summary>Where the coaster model's track node of an id stands, in the engine's axes, or null.</summary>
		public Numerics.Vector3? TrackNode( int id )
			=> Station.Find( id, ParkCoasterPylon.TrackSpace, out var at ) is NodeEnd.Posed or NodeEnd.RestPose ? at : null;
	}

	/// <summary>One coaster's setup, or null where its record, files or pylon cannot be read (each reason counted).</summary>
	internal static CoasterSetup? SetUp( ParkWorld save, ParkWorld.CatalogueObject placed, ParkItemCatalogue.Item item,
		HeightfieldFile field, BaseFileSystem files )
	{
		// A coaster made here carries no model instance, and one past the first in the module cannot be found.
		if ( save.Coasters.For( placed.MeshInstance ) is null )
		{
			Unimplemented.Report( placed.MeshInstance == 0 ? "COASTER_TRACK_UNSAVED" : "SAVED_COASTER_HEADER_UNREAD" );
			return null;
		}

		var pylon = ParkCoasterPylon.Load( item.Directory, files );
		var curviness = Curviness( item.Directory, files );
		var station = RideNodes.Load( item.Directory, item.Stem, files, item.DoHeadProcessing, [] );

		if ( pylon == null || curviness == null || station == null )
		{
			Unimplemented.Report( "COASTER_TYPE_UNREAD" );
			return null;
		}

		if ( !pylon.Posable )
		{
			Unimplemented.Report( "COASTER_PYLON_KNOTS" );
			return null;
		}

		var origin = ParkObjects.ModelOriginFor( item, placed, field );

		ParkRides.Stand( station, placed.CatalogueId, origin, placed.Angle );

		return new CoasterSetup( pylon, curviness.Value, station, origin.Z );
	}

	/// <summary>
	/// One pylon of a coaster's track: the cell it stands on, how far up its range its knot is, and the yaw, incline and bank its
	/// clips are set to; <paramref name="Which"/> names the node it carries, for the census.
	/// </summary>
	public readonly record struct PylonPlace( int CellX, int CellY, float Loft, float Yaw, float Incline, float Bank, string Which );

	/// <summary>
	/// Every pylon a coaster's track stands on a cell: the station's first and third nodes, which face along the station, and a
	/// piece's each (<c>Coaster_AddNode</c>, <c>0x00434276</c>). A stacked or loop piece is counted and has none here; null where a
	/// station node is missing.
	/// </summary>
	public static List<PylonPlace>? Pylons( IReadOnlyList<SavedCoasterPiece> pieces, ParkCoasterPylon pylon, HeightfieldFile field,
		float rootHeight, Func<int, Numerics.Vector3?> trackNode )
	{
		var stations = StationPylons( pylon, rootHeight, trackNode );

		if ( stations == null )
			return null;

		var along = StationPlace( stations[2], pylon, field ) - StationPlace( stations[0], pylon, field );
		var level = Numerics.Vector3.Normalize( new Numerics.Vector3( along.X, 0f, along.Z ) );
		var yaw = pylon.YawFacing( level );

		var found = new List<PylonPlace>
		{
			new( stations[0].X, stations[0].Y, stations[0].Loft, yaw, 0f, 0f, "station 1" ),
			new( stations[2].X, stations[2].Y, stations[2].Loft, yaw, 0f, 0f, "station 3" )
		};

		for ( var index = 0; index < pieces.Count; ++index )
		{
			var piece = pieces[index];

			if ( piece.Stack != 0 || piece.IsLoop )
			{
				Unimplemented.Report( piece.IsLoop ? "COASTER_LOOP_PYLON" : "COASTER_STACKED_PYLON" );
				continue;
			}

			found.Add( new PylonPlace( piece.X, piece.Y, piece.Loft, piece.Yaw, piece.Incline, piece.Bank, $"piece {index + 1}" ) );
		}

		return found;
	}

	/// <summary>A station node's pylon: the cell under the track node and the loft that stands its knot at the node's height.</summary>
	private readonly record struct StationPylon( int X, int Y, float Loft, Numerics.Vector3 Node );

	private static StationPylon[]? StationPylons( ParkCoasterPylon pylon, float rootHeight, Func<int, Numerics.Vector3?> trackNode )
	{
		var found = new StationPylon[3];

		for ( var k = 0; k < 3; ++k )
		{
			if ( trackNode( k + 1 ) is not { } at )
			{
				Unimplemented.Report( "COASTER_STATION_NODE" );
				return null;
			}

			found[k] = new StationPylon( (int)(at.X * 0.1f), (int)(at.Z * 0.1f), pylon.LoftFor( at.Y - rootHeight ), at );
		}

		return found;
	}

	private static Numerics.Vector3 StationPlace( StationPylon station, ParkCoasterPylon pylon, HeightfieldFile field )
		=> OnPylon( station.X, station.Y, station.Loft, pylon, field );

	/// <summary>
	/// The coaster's chain of knots: the station's three, a piece's each, the closing one; null where a station node is
	/// missing or a piece's pylon is not decoded.
	/// </summary>
	/// <param name="rootHeight">The height of the coaster model's root, which a station pylon's height is measured from.</param>
	/// <param name="trackNode">Where the coaster model's track node of an id stands, in the engine's axes, or null.</param>
	public static List<ParkCoasterTrack.Knot>? Chain( IReadOnlyList<SavedCoasterPiece> pieces, ParkCoasterPylon pylon,
		HeightfieldFile field, float rootHeight, Func<int, Numerics.Vector3?> trackNode )
	{
		var stations = StationPylons( pylon, rootHeight, trackNode );

		if ( stations == null )
			return null;

		// The first and the third stand on a pylon on the cell under them, the second where its node is.
		var places = new[] { StationPlace( stations[0], pylon, field ), stations[1].Node, StationPlace( stations[2], pylon, field ) };

		var along = places[2] - places[0];
		var level = Numerics.Vector3.Normalize( new Numerics.Vector3( along.X, 0f, along.Z ) );
		var chain = places.Select( place => new ParkCoasterTrack.Knot( place, level ) ).ToList();

		foreach ( var piece in pieces )
		{
			if ( piece.Stack != 0 || piece.IsLoop )
			{
				Unimplemented.Report( piece.IsLoop ? "COASTER_LOOP_PYLON" : "COASTER_STACKED_PYLON" );
				return null;
			}

			chain.Add( new ParkCoasterTrack.Knot( OnPylon( piece.X, piece.Y, piece.Loft, pylon, field ),
				pylon.DirectionAt( piece.Yaw, piece.Incline ) ) );
		}

		chain.Add( new ParkCoasterTrack.Knot( places[0], level ) );

		return chain;
	}

	/// <summary>
	/// A pylon's knot on the ground cell (<paramref name="x"/>, <paramref name="y"/>) at <paramref name="loft"/>: the cell's
	/// corner, at its ground (<see cref="GroundAt"/>), plus the knot as the pylon's model poses it.
	/// </summary>
	public static Numerics.Vector3 OnPylon( int x, int y, float loft, ParkCoasterPylon pylon, HeightfieldFile field )
		=> new Numerics.Vector3( x * field.CellSizeX, GroundAt( field, x, y ), y * field.CellSizeY ) + pylon.KnotAt( loft );

	/// <summary>
	/// The height a model stands at on one ground cell, as the model maker gives it (<c>FUN_00466d70</c>,
	/// <c>0x00466ef6</c>..<c>0x00466f80</c>): halfway between the lowest and the highest of its four corners.
	/// </summary>
	public static float GroundAt( HeightfieldFile field, int x, int y )
	{
		float a = field.HeightAt( x, y ), b = field.HeightAt( x + 1, y ), c = field.HeightAt( x, y + 1 ), d = field.HeightAt( x + 1, y + 1 );

		return (MathF.Min( MathF.Min( a, b ), MathF.Min( c, d ) ) + MathF.Max( MathF.Max( a, b ), MathF.Max( c, d ) )) * 0.5f;
	}

	/// <summary>The coaster type's <c>sCoasterType.fCurviness</c> (<c>+8</c>), 1.25 where its <c>coaster.sam</c> does not set it (<c>0x0042fd90</c>); null with no file.</summary>
	public static float? Curviness( string directory, BaseFileSystem files )
	{
		using var stream = files.OpenReadOrNull( $"{directory}/coaster.sam" );

		if ( stream == null )
			return null;

		return float.TryParse( new SettingsFile( stream )["sCoasterType.fCurviness"], System.Globalization.NumberStyles.Float,
			System.Globalization.CultureInfo.InvariantCulture, out var value ) && value != 0f ? value : DefaultCurviness;
	}

	private const float DefaultCurviness = 1.25f;
}
