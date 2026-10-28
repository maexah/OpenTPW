namespace OpenTPW;

/// <summary>
/// The initial hoarding polyline and owned cells (FUN_005323f0). This handles the empty world's
/// axis-aligned, two-cell grid; editing an existing hoarding is a separate build operation.
/// See docs/exe/park-engine.md, "A fresh Full Simulation world".
/// </summary>
internal static class FreshParkBoundary
{
	/// <summary>
	/// Writes the boundary into <paramref name="cells"/> and answers its anchors in the walk's order, each with the piece the
	/// original's chooser gives it (<see cref="ParkBoundaryHoarding.PieceFor"/>), the straights' variants drawn from
	/// <paramref name="seed"/>, the park's generator (<c>docs/exe/park.md</c>, "The boundary hoarding's piece").
	/// </summary>
	internal static IReadOnlyList<ParkBoundaryHoarding.Anchor> Apply( ParkWorld.MapCell[] cells, ParkBalance balance,
		SettingsFile settings, ref uint seed, ParkBoundaryHoarding.Variants variants )
	{
		var origin = (X: balance.Int( "FixedItemInfo.EntranceBPosX" ) + 3,
			Y: balance.Int( "FixedItemInfo.EntranceBPosY" ));
		var points = new List<(int X, int Y)>();
		var terminated = false;
		for ( var at = 0; at < 64; ++at )
		{
			if ( !int.TryParse( settings[$"HoardingClicks[{at}].X"], out var x )
				|| !int.TryParse( settings[$"HoardingClicks[{at}].Y"], out var y ) )
				throw new InvalidDataException( "The initial hoarding has no terminating point" );
			if ( x == 512 )
			{
				terminated = true;
				break;
			}
			points.Add( (origin.X + x, origin.Y + y) );
		}
		if ( !terminated )
			throw new InvalidDataException( "The initial hoarding has no terminating point" );
		if ( points.Count < 2 )
			throw new InvalidDataException( "The initial hoarding has fewer than two points" );

		var anchors = new HashSet<(int X, int Y)>();
		var walk = new List<(int X, int Y)>();
		for ( var at = 1; at < points.Count; ++at )
		{
			var (x, y) = points[at - 1];
			var end = points[at];
			if ( (x != end.X && y != end.Y) || (Math.Abs( x - end.X ) % 2) != 0 || (Math.Abs( y - end.Y ) % 2) != 0 )
				throw new InvalidDataException( "The initial hoarding is not on the axis-aligned two-cell grid" );
			var dx = Math.Sign( end.X - x ) * 2;
			var dy = Math.Sign( end.Y - y ) * 2;
			while ( true )
			{
				if ( !ParkState.OnMap( x, y ) || !ParkState.OnMap( x + 1, y + 1 ) )
					throw new InvalidDataException( "The initial hoarding leaves the map" );
				if ( anchors.Add( (x, y) ) )
					walk.Add( (x, y) );
				if ( (x, y) == end )
					break;
				x += dx;
				y += dy;
			}
		}

		// Empty-map equivalent of the inward scans in FUN_005311c0. The gate closes the polygon
		// for ownership, but has no hoarding track. A re-entrant corner owns its whole 2x2 block.
		var owned = new bool[cells.Length];
		for ( var y = 0; y < ParkWorld.MapSize; ++y )
			for ( var x = 0; x < ParkWorld.MapSize; ++x )
				owned[y * ParkWorld.MapSize + x] = Inside( points, x - .5, y - .5 );

		var blocks = new HashSet<(int X, int Y)>();
		foreach ( var (x, y) in anchors )
		{
			var ids = new[] { y * ParkWorld.MapSize + x, y * ParkWorld.MapSize + x + 1,
				(y + 1) * ParkWorld.MapSize + x, (y + 1) * ParkWorld.MapSize + x + 1 };
			if ( ids.Count( i => owned[i] ) == 3 )
				foreach ( var i in ids ) owned[i] = true;

			// FUN_005370e0's 0x85 arm marks a straight crossing hidden/water ground; corners are exempt.
			var horizontal = anchors.Contains( (x - 2, y) ) && anchors.Contains( (x + 2, y) );
			var vertical = anchors.Contains( (x, y - 2) ) && anchors.Contains( (x, y + 2) );
			var blocked = (horizontal || vertical) && ids.Any( i => cells[i].Type is 2 or 7 or 30 );
			if ( blocked )
				blocks.Add( (x, y) );
			cells[ids[0]] = cells[ids[0]] with { TrackType = 25, TrackFlags = (ushort)(blocked ? 1 : 0) };
			foreach ( var i in ids.Skip( 1 ) )
				cells[i] = cells[i] with { TrackType = 12, TrackParentId = (ushort)(ids[0] + 1) };
		}

		// The two rows between the gate ends are explicitly cleared after the hoarding walk.
		var left = balance.Int( "FixedItemInfo.EntranceAPosX" ) - 4;
		for ( var x = left + 2; x < origin.X; ++x )
			for ( var y = origin.Y; y <= origin.Y + 1; ++y )
				if ( ParkState.OnMap( x, y ) ) owned[y * ParkWorld.MapSize + x] = true;
		for ( var i = 0; i < cells.Length; ++i )
			if ( owned[i] ) cells[i] = cells[i] with { Flags = (ushort)(cells[i].Flags & ~0x40) };

		// The gate's two ends, [0x007639f0] and [0x007639f8]: entrance B three on, entrance A four back.
		var gateEnds = new[] { origin, (X: left, Y: balance.Int( "FixedItemInfo.EntranceAPosY" )) };

		if ( walk.Count < 2 )
			throw new InvalidDataException( "The initial hoarding has fewer than two anchors" );

		// The anchors are chosen once each, in the walk's order, the order the original's model slots keep and the one its
		// straights' variants follow. How often its laying chooses an anchor before that, so how far it moves the
		// generator, is not read.
		Unimplemented.Report( "FRESH_BOUNDARY_LAYING_DRAWS" );

		var pieces = new List<ParkBoundaryHoarding.Anchor>( walk.Count );
		for ( var at = 0; at < walk.Count; ++at )
		{
			var (x, y) = walk[at];
			var mask = (byte)((anchors.Contains( (x, y - 2) ) ? 0x01 : 0) | (anchors.Contains( (x + 2, y) ) ? 0x04 : 0)
				| (anchors.Contains( (x, y + 2) ) ? 0x10 : 0) | (anchors.Contains( (x - 2, y) ) ? 0x40 : 0));
			// mDirection: the way the walk leaves the anchor, or for its last the way it came.
			var (from, to) = at + 1 < walk.Count ? (walk[at], walk[at + 1]) : (walk[at - 1], walk[at]);
			var direction = (byte)(to.X > from.X ? 0x04 : to.X < from.X ? 0x40 : to.Y > from.Y ? 0x10 : 0x01);
			var flags = (ushort)(blocks.Contains( (x, y) ) ? 1 : 0);
			var (row, turn) = ParkBoundaryHoarding.PieceFor( mask, direction, flags, gateEnds.Contains( (x, y) ), ref seed, variants );
			pieces.Add( new( x, y, mask, direction, flags, row, turn ) );

			// The retile keeps the piece's family, row and rotation on the anchor's track record (0x0053b02b..0x0053b04c).
			cells[y * ParkWorld.MapSize + x] = cells[y * ParkWorld.MapSize + x] with
			{
				TrackTileSet = ParkBoundaryHoarding.Family, TrackTileIndex = row, TrackTileAngle = turn
			};
		}

		return pieces;
	}

	private static bool Inside( List<(int X, int Y)> points, double x, double y )
	{
		var inside = false;
		for ( var at = 0; at < points.Count; ++at )
		{
			var a = points[at];
			var b = points[(at + 1) % points.Count];
			if ( (a.Y > y) != (b.Y > y) && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X )
				inside = !inside;
		}
		return inside;
	}
}
