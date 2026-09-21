namespace OpenTPW;

/// <summary>
/// Which tile a path or queue cell draws, and which way round it stands - the original's
/// <c>FUN_00535dd0</c>, which is a pair of lookup tables compiled into the executable.
///
/// <para>
/// <b>The tables are the game's own, read out of <c>testme.exe</c> rather than inferred from the
/// shipped park.</b> The path table is 49 entries at <c>DAT_00763138</c> and the queue table 11 at
/// <c>DAT_007630b0</c>, each entry twelve bytes: <c>{ u32 (set &lt;&lt; 16) | index, i32 angle,
/// u8 mask, 3 pad }</c>. <see cref="ParkPaths"/> and <see cref="ParkQueues"/> draw what a saved park
/// already stores; this is what a park being EDITED needs, because a cell nobody has drawn yet has
/// no stored tile.
/// </para>
///
/// <para>
/// <b>Both tables reproduce Lost Kingdom exactly, and neither source could have been fitted to the
/// other</b> - the executable never saw the save when the save was written. Measured over the
/// shipped park: all <b>78</b> path cells match on angle, all 78 match on index once the random
/// variant below is allowed, and all <b>4</b> queue cells match on both index and angle. The
/// lookup reached pass one on 53 of the 78 and pass two on the other 25, so both passes are
/// exercised by real data rather than only the first.
/// </para>
/// </summary>
public static class ParkPathTiles
{
	/// <summary>One row of either table: the neighbour mask it answers for, and what it draws.</summary>
	public readonly record struct Tile( byte Mask, int Set, int Index, int Angle );

	/// <summary>
	/// The path table, <b>in the executable's own order, which is load-bearing</b>: the second pass
	/// takes the FIRST row whose mask is a subset, so re-sorting it silently changes which tile a
	/// cell draws.
	///
	/// <para>
	/// <b>It is NOT sorted richest-mask-first, and believing that it is would be a reason to "tidy"
	/// it.</b> The popcounts in table order run 5,5,5,5, then twelve 4s, then 3,3,3,3, then 2,2 - and
	/// then <b>8</b>, the full mask <c>0xff</c> at row 18, after two two-bit masks. So the order is
	/// simply the order the original ships, and the only safe rule is to keep it exactly.
	/// </para>
	/// </summary>
	private static readonly Tile[] PathTable =
	[
		new( 0x1f, 1, 10, 0 ), new( 0x7c, 1, 10, 90 ), new( 0xf1, 1, 10, 180 ), new( 0xc7, 1, 10, 270 ),
		new( 0x1d, 1, 7, 0 ), new( 0x74, 1, 7, 90 ), new( 0xd1, 1, 7, 180 ), new( 0x47, 1, 7, 270 ),
		new( 0x71, 1, 15, 0 ), new( 0xc5, 1, 15, 90 ), new( 0x17, 1, 15, 180 ), new( 0x5c, 1, 15, 270 ),
		new( 0x15, 1, 4, 0 ), new( 0x54, 1, 4, 90 ), new( 0x51, 1, 4, 180 ), new( 0x45, 1, 4, 270 ),
		new( 0x11, 1, 2, 0 ), new( 0x44, 1, 2, 90 ),
		new( 0xff, 1, 13, 0 ),
		new( 0x7f, 1, 11, 0 ), new( 0xfd, 1, 11, 90 ), new( 0xf7, 1, 11, 180 ), new( 0xdf, 1, 11, 270 ),
		new( 0x7d, 1, 6, 0 ), new( 0xf5, 1, 6, 90 ), new( 0xd7, 1, 6, 180 ), new( 0x5f, 1, 6, 270 ),

		// 0x77 and 0xdd each appear TWICE, and the first match wins - so tile 12 at 180 and at 270
		// can never be selected by either pass. That is a defect in the shipped table and it is
		// reproduced rather than tidied: "correcting" it would draw a tile the original never draws.
		new( 0x77, 1, 12, 0 ), new( 0xdd, 1, 12, 90 ), new( 0x77, 1, 12, 180 ), new( 0xdd, 1, 12, 270 ),

		new( 0xd5, 1, 8, 0 ), new( 0x57, 1, 8, 90 ), new( 0x5d, 1, 8, 180 ), new( 0x75, 1, 8, 270 ),
		new( 0x55, 1, 5, 0 ),
		new( 0x1c, 1, 9, 0 ), new( 0x70, 1, 9, 90 ), new( 0xc1, 1, 9, 180 ), new( 0x07, 1, 9, 270 ),
		new( 0x14, 1, 3, 0 ), new( 0x50, 1, 3, 90 ), new( 0x41, 1, 3, 180 ), new( 0x05, 1, 3, 270 ),
		new( 0x10, 1, 1, 0 ), new( 0x40, 1, 1, 90 ), new( 0x01, 1, 1, 180 ), new( 0x04, 1, 1, 270 ),
		new( 0x00, 1, 0, 0 )
	];

	/// <summary>The queue table, same shape and the same ordering rule.</summary>
	private static readonly Tile[] QueueTable =
	[
		new( 0x11, 2, 2, 0 ), new( 0x44, 2, 2, 90 ),
		new( 0x10, 2, 1, 0 ), new( 0x40, 2, 1, 90 ), new( 0x01, 2, 1, 180 ), new( 0x04, 2, 1, 270 ),
		new( 0x14, 2, 3, 0 ), new( 0x50, 2, 3, 90 ), new( 0x41, 2, 3, 180 ), new( 0x05, 2, 3, 270 ),
		new( 0x00, 2, 0, 0 )
	];

	/// <summary>The cell type a queue is laid on - the same 3 <see cref="ParkRideChoice.QueueCellType"/> names.</summary>
	private const int QueueType = ParkRideChoice.QueueCellType;

	/// <summary>
	/// Whether a cell's two cardinal links run straight through it - north and south, or east and
	/// west. It is what picks a queue's angle base, and it is <b>not</b> the L test beside it.
	/// </summary>
	private static bool IsStraight( byte neighbours )
	{
		var cardinals = ((neighbours & 0x01) != 0 ? 1 : 0) + ((neighbours & 0x04) != 0 ? 1 : 0)
			+ ((neighbours & 0x10) != 0 ? 1 : 0) + ((neighbours & 0x40) != 0 ? 1 : 0);

		if ( cardinals != 2 )
			return false;

		return ((neighbours & 0x01) != 0 && (neighbours & 0x10) != 0)
			|| ((neighbours & 0x04) != 0 && (neighbours & 0x40) != 0);
	}

	/// <summary>
	/// The types that draw no tile of their own but are still real cells: the flat tile 8. Measured
	/// from <c>FUN_00535dd0</c>'s own early-out, which tests the ABSOLUTE value of the type.
	/// </summary>
	private static readonly int[] Flat = [2, 4, 7, 9, 10, 0x15, 0x1e];

	/// <summary>
	/// What a cell should draw. <paramref name="pathLinks"/> is how many of its cardinal links reach
	/// a path cell, which only a queue uses; <paramref name="variant"/> is the coin the original
	/// carries between calls - see <see cref="Vary"/>.
	/// </summary>
	public static (int Set, int Index, int Angle) TileFor(
		int type, byte neighbours, byte direction, int pathLinks = 0, bool variant = false )
	{
		// The two early-outs, before either table is consulted.
		if ( type <= 0 || type == 5 )
			return (0, 55, 0);

		var kind = Math.Abs( type );

		foreach ( var flat in Flat )
		{
			if ( kind == flat )
				return (0, 8, 0);
		}

		// The decode says the TABLE is chosen on abs(mType) while the corner bump below reads the RAW
		// type, and remarks that a cell of mType -3 would therefore take the queue table and skip the
		// bump. That cannot happen here, and the same decode is why: the early-out above returns for
		// every type <= 0, so a negative type never reaches this line. The two statements are in
		// tension in the source material rather than in this code, so the tension is recorded here and
		// nothing is invented to resolve it - no shipped park has ever carried a negative mType.
		var queue = type == QueueType;
		var table = queue ? QueueTable : PathTable;

		if ( Look( table, neighbours ) is not { } tile )
		{
			// Every mask the shipped park uses is answered, so reaching this means a mask no table row
			// covers - counted rather than filled in with a guess that would draw something wrong.
			Unimplemented.Report( "PATH_TILE_NO_TABLE_ROW" );

			return (0, 8, 0);
		}

		var index = tile.Index;
		var angle = tile.Angle;

		if ( queue )
		{
			// THE ANGLE BASE BELONGS TO A STRAIGHT, NOT TO A CORNER. The first reading of the original
			// attached it to an L, and that was measured wrong: the guard is a cardinal count of two
			// with the two being opposite (N+S or E+W), which is a straight. The L test belongs to the
			// index bump below instead.
			//
			// Lost Kingdom cannot tell the two readings apart and it is worth saying so rather than
			// claiming the park as evidence: its one corner carries direction 0x10, which gives a base
			// of nought under BOTH readings. What the park does pin is the base itself - its three
			// straights carry direction 0x04 and store 270 against a table angle of 90.
			if ( IsStraight( neighbours ) )
				angle = (angle + (direction is 0x40 or 0x10 ? 0 : 180)) % 360;

			// The corner bump, for the four (direction, mask) pairs the original names.
			if ( (direction, neighbours) is (0x40, 0x50) or (0x10, 0x14) or (0x01, 0x41) or (0x04, 0x05) )
				++index;

			// And three more per cardinal link that reaches a path - which is what makes the cell where
			// a queue MEETS a path draw the end piece. Lost Kingdom's (49,22) is the one such cell and
			// it stores index 5 against a table index of 2.
			//
			// TWO THINGS THE CALLER OWES THIS, because neither can be answered from a cell alone. The
			// link must be MUTUAL - the neighbour's own mask must carry the opposite bit - and the
			// original then gates the link on a low-nibble flags test whose receiver is the TRACK cell
			// beside this one (a separate 0x28-stride array, re-targeted through its parent where that
			// cell defers), NOT the path cell. This project has no track-cell layer, so that gate is
			// counted rather than guessed at and the caller passes only the links it can vouch for.
			index += 3 * pathLinks;
		}
		else if ( variant )
		{
			index = Vary( index );
		}

		return (tile.Set, index, angle);
	}

	/// <summary>
	/// The two passes, in order: a row whose mask is exactly this cell's, then the first row whose
	/// mask is a subset of it.
	/// </summary>
	private static Tile? Look( Tile[] table, byte neighbours )
	{
		foreach ( var tile in table )
		{
			if ( tile.Mask == neighbours )
				return tile;
		}

		foreach ( var tile in table )
		{
			if ( (neighbours & tile.Mask) == tile.Mask )
				return tile;
		}

		return null;
	}

	/// <summary>
	/// The second art variant of a straight or an edge, which the original picks with a coin.
	///
	/// <para>
	/// <b>A path tile is therefore NOT a function of the neighbour mask, and a test that demands one
	/// fixed index for a straight will be flaky.</b> <c>DAT_00820ac0</c> holds <c>rand() &amp; 1</c>
	/// carried between calls and rewrites exactly these two indices for tile set 1. The shipped park
	/// is the evidence from the other side: of its 78 path cells only <b>51</b> carry the base index,
	/// and every one of the 27 others carries one of these two variants - never anything else.
	/// </para>
	/// </summary>
	public static int Vary( int index ) => index switch
	{
		2 => 19,
		10 => 20,
		_ => index
	};
}
