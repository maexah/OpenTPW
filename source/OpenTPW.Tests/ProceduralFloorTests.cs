using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A built thing's floor blended into the ground's tiles (<c>docs/exe/park-engine.md</c>, "A built thing's floor is
/// blended into the ground's tiles"): the footprint table <c>ProceduralBase_SkirtFlags</c> builds and the words the model
/// maker stamps from it. The tables are the original's own, read from its memory with the stock park loaded
/// (<c>original/footprints.py</c>, <c>q649/orig/a-footprints.txt</c>), and the words its blend was handed for the 44
/// cells (<c>q617/orig/a-load.log</c>, the same in <c>q649/orig/a-load.log</c>).
/// </summary>
[TestClass]
public class ProceduralFloorTests
{
	private BaseFileSystem was = null!;

	[TestInitialize]
	public void MountTheGame()
	{
		was = FileSystem;
		FileSystem = GameData.Required();
	}

	[TestCleanup]
	public void PutItBack() => FileSystem = was;

	private static readonly ProceduralBase.Grid Park = new( 0f, 0f, 10f, 10f );

	/// <summary>A table as footprints.py prints it: rows by z, each cell <c>flags:frame</c>, or <c>-</c> for none.</summary>
	private static string Print( ModelFile model, ProceduralFloor.Cell?[,] table )
		=> string.Join( " / ", Enumerable.Range( 0, table.GetLength( 1 ) ).Select( z => string.Join( " ",
			Enumerable.Range( 0, table.GetLength( 0 ) ).Select( x => table[x, z] is { } c
				? $"{c.Flags:x4}:{(c.FrameOffset - model.FrameTableOffset) / 8}" : "-" ) ) ) );

	private static ProceduralFloor.Cell?[,] ItemTable( ModelFile model, ItemShape shape )
		=> ProceduralFloor.Table( model, ProceduralBase.FloorMesh( model, shape, Park, out _ ) ?? model.RootNode, shape,
			shape.Width, shape.Depth, (model.HeaderFlags & 0x80) != 0, Park );

	/// <summary>
	/// The 41 Lost Kingdom item models the original loads with a footprint table, by model, as its memory held them with the
	/// stock park loaded (the catalogue loads every item on offer; <c>original/footprints.py PID items</c>,
	/// <c>q649/orig/b-items.txt</c>). Its two coaster pylons hold no entry, and its eight queue models are
	/// <see cref="AQueuePiecesOneCellIsEntered"/>'s.
	/// </summary>
	private static readonly (string Model, string Table)[] Original =
	[
		("bouncy", "00c8:11 00e0:10 0090:9 / 00a0:8 - 0088:6 / 00a0:5 - 0088:3 / 0080:2 0090:1 00e0:0"),
		("bumper", "00c8:0 00c8:1 0080:2 00c8:3 0090:4 / 00a0:5 - - - 0088:9 / 00a0:10 - - - 0088:14 / 00a0:15 - - - 0088:19 / 0080:20 0080:21 00c8:22 0080:23 00e0:24"),
		("gokarts", "00c8:0 0080:1 0080:2 00c8:3 / 0080:11 - - 0080:6 / 0080:7 0080:8 0080:9 0080:10"),
		("lookout", "00c8:8 00c8:9 0090:10 0090:11 / 00c8:7 - - 0090:12 / 0080:6 - - 00e0:1 / 0080:5 0080:4 00e0:3 00e0:2"),
		("minecart", "0080:0 0080:1 0080:2 00e0:3 / 0080:11 - - 0090:6 / 00c8:7 00d0:8 0088:9 0090:10"),
		("monkey", "00c8:0 0080:1 00c8:2 0090:3 / 00a0:4 - - 0088:7 / 00a0:8 - - 0088:11 / 0080:12 0080:13 0090:14 00e0:15"),
		("mumbo", "00c8:0 0080:12 00c8:1 0090:2 / 00d0:3 - - 00c0:4 / 00d0:5 - - 00c0:6 / 0080:7 0080:8 00c8:14 00e0:9"),
		("porkpie", "00c8:0 0080:1 0090:2 0090:3 / 00a0:4 - - 0088:7 / 00a0:8 - - 0088:11 / 0080:12 0080:13 00c8:14 00e0:15"),
		("spider", "00c8:0 0088:1 0080:2 00d0:3 0090:4 / 0090:5 - - - 0080:6 / 0090:7 - - - 00c8:8 / 00e0:9 - - - 0080:10 / 0080:11 00c0:12 00c8:13 00a0:14 00e0:15"),
		("totem", "00c8:10 0080:9 0090:8 / 00e0:6 - 0080:7 / 00e0:1 - 0080:2 / 0080:5 0080:4 00e0:3"),
		("tvsim", "00c8:2 0080:3 0080:4 0090:5 / 00a0:6 - - 0088:7 / 00a0:8 - - 0088:10 / 0080:11 0080:12 0080:13 00e0:14"),
		("wateride", "00c8:0 0080:1 0080:2 0090:3 / 00c8:4 - - 00c8:7 / 0080:8 - - 0080:11 / 0080:12 0080:13 0080:14 0080:15"),
		("1x1East", "0080:0"),
		("1x1rck1", "0080:0"),
		("1x1rck2", "0080:0"),
		("2x1log", "0080:1 0090:0"),
		("2x2rck", "0080:0 00a0:1 / 0088:2 0090:3"),
		("4x4rock", "0080:1 00a0:3 00a0:4 00a0:0 / 0080:5 - - 0090:2 / 0088:7 - - 0090:10 / - 0088:6 0090:11 -"),
		("bigpalm", "0080:0 00a0:1 / 0088:2 0090:3"),
		("bush", "0080:0"),
		("bush02", "0080:0"),
		("camera", "0090:0"),
		("fountain", "00c8:1 00c8:2 0090:3 / 00a0:4 - 0088:5 / 0080:6 0080:7 00e0:8"),
		("pelbin", "0080:0"),
		("sign1", "00c8:5 00c8:7 00c8:3 0090:1 / 0080:4 0080:6 0080:2 00e0:0"),
		("speaker1", "0080:0"),
		("staff", "0080:0 0080:2 / 0088:1 0090:3"),
		("statue1", "0080:0"),
		("supbog", "00a0:0 0080:3 0090:2 / 0080:1 0080:4 0088:5"),
		("s_plant", "0080:0"),
		("toilet", "0080:0"),
		("balloon", "00c8:0 0080:1 0090:2 / 00a0:3 - 00c0:5 / 0080:6 0080:7 00e0:8"),
		("burger", "00c8:0 00e0:1 / 0080:2 00e0:3"),
		("Coconut", "0080:0 0080:1 / 0080:2 0080:3"),
		("Cost_shp", "00a0:7 0080:4 0090:2 / 00a0:6 - 0088:1 / 0080:8 0080:5 0088:0"),
		("Fries", "00a0:0 0080:2 / 0080:1 00e0:3"),
		("icecream", "0080:0 0090:1 / 0080:2 00e0:3"),
		("Hyenas", "0080:8 0080:6 0080:7 / 0080:5 - 00e0:4 / 0080:0 0080:1 00e0:2"),
		("Junspray", "0080:8 0080:6 0080:7 / 0080:5 - 00e0:4 / 0080:0 0080:1 00e0:2"),
		("puzzle", "00c8:1 0080:4 0090:6 / 0090:2 - 00c8:7 / 0080:3 00a0:5 00e0:8"),
		("squark", "00c8:0 0080:1 / 00a0:2 0088:3 / 0080:4 00e0:5"),
	];

	/// <summary>
	/// <b>Every Lost Kingdom item's table is the original's</b>, cell for cell: the frame of the floor face over each cell,
	/// its turn and its mirror, and no entry on a cell ringed by floor (the Belly Bounce's middle two, the Hot Pot's middle
	/// nine).
	/// </summary>
	/// <remarks><b>Mutations:</b> the nearest corner taken as the last; the mirror's test the wrong way; the missing corner's
	/// case 2 completed from the wrong pair; the edge mask's ring of four taken as none; the face's middle cut to cells by z
	/// for x; a later face kept only where it lies more squarely on both axes; the x edge's ends swapped; a material's run off by one; the edge mask not asked.</remarks>
	[TestMethod]
	public void EveryItemsTableIsTheOriginals()
	{
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		var compared = 0;

		foreach ( var (stem, expected) in Original )
		{
			var items = catalogue.All.Where( i => i.Shape != null && string.Equals( i.Stem, stem, System.StringComparison.OrdinalIgnoreCase ) ).ToList();
			Assert.AreNotEqual( 0, items.Count, $"an item drawn by {stem}" );

			foreach ( var item in items )
			{
				var model = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );
				Assert.AreEqual( expected, Print( model, ItemTable( model, item.Shape! ) ), $"{item.Stem} (item {item.Id})'s table" );
				++compared;
			}
		}

		Assert.IsTrue( compared >= 41, $"{compared} items compared" );
	}

	/// <summary>
	/// <b>A queue piece's one cell is entered whatever its kind</b>: loaded with flag <c>0x2</c>, its header carries
	/// <c>0x80</c> (<c>FUN_00461f10</c>), and its one face over the cell gives the turn.
	/// </summary>
	/// <remarks><b>Mutations:</b> every cell not entered for a queue piece (its kind is nought).</remarks>
	[TestMethod]
	public void AQueuePiecesOneCellIsEntered()
	{
		foreach ( var (piece, expected) in new[] { ("quebnd2", "00c8:0"), ("questra", "00c8:0"), ("queend", "0080:0") } )
		{
			var model = new ModelFile( $"levels/jungle/queue/{piece}.MD2" );
			var table = ProceduralFloor.Table( model, model.RootNode, new ItemShape( 1, 1, [[0]] ), 1, 1, true, Park );

			Assert.AreEqual( expected, Print( model, table ), piece );
			Assert.AreEqual( "-", Print( model, ProceduralFloor.Table( model, model.RootNode, new ItemShape( 1, 1, [[0]] ), 1, 1, false, Park ) ),
				$"{piece} without 0x80: its kind is nought" );
		}
	}

	/// <summary>
	/// <b>The stamp lands each table cell where the original's did, turned as it was</b>: the 40 item cells the stock park
	/// blends, at 0, 90 and 270 degrees, each the frame and the word the blend was handed less the cell's own <c>0x6</c>, which
	/// <see cref="ProceduralFloor.Blend"/> hands on as nought (<c>ProceduralGround</c> reads none of them).
	/// </summary>
	/// <remarks><b>Mutations:</b> 90 carried round as 270; the mirror dropped.</remarks>
	[TestMethod]
	public void TheStampLandsEachCellWhereTheOriginalsDid()
	{
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );

		// Cell -> the word the blend was handed (q617/orig/a-load.log) and the frame (q649/orig/a-footprints.txt).
		var measured = new Dictionary<(int, int), (int Word, int Frame)>
		{
			[(51, 23)] = (0x2cb, 11), [(52, 23)] = (0x2e3, 10), [(53, 23)] = (0x293, 9), [(51, 24)] = (0x2a3, 8),
			[(53, 24)] = (0x28b, 6), [(51, 25)] = (0x2a3, 5), [(53, 25)] = (0x28b, 3), [(51, 26)] = (0x283, 2),
			[(52, 26)] = (0x293, 1), [(53, 26)] = (0x2e3, 0),
			[(51, 30)] = (0x283, 8), [(52, 30)] = (0x283, 6), [(53, 30)] = (0x283, 7), [(51, 31)] = (0x283, 5),
			[(53, 31)] = (0x2e3, 4), [(51, 32)] = (0x283, 0), [(52, 32)] = (0x283, 1), [(53, 32)] = (0x2e3, 2),
			[(43, 30)] = (0x283, 0), [(44, 30)] = (0x283, 1), [(43, 31)] = (0x283, 2), [(44, 31)] = (0x283, 3),
			[(44, 29)] = (0x283, 0), [(55, 29)] = (0x293, 0), [(40, 29)] = (0x293, 0),
			[(58, 15)] = (0x28b, 2), [(59, 15)] = (0x2a3, 3), [(58, 16)] = (0x28b, 0), [(59, 16)] = (0x293, 1),
			[(55, 17)] = (0x2a3, 0), [(55, 16)] = (0x2a3, 0), [(55, 15)] = (0x2a3, 0),
			[(57, 17)] = (0x2a3, 3), [(58, 17)] = (0x293, 5), [(59, 17)] = (0x2c3, 8), [(57, 18)] = (0x2d3, 2),
			[(59, 18)] = (0x28b, 7), [(57, 19)] = (0x2d3, 1), [(58, 19)] = (0x283, 4), [(59, 19)] = (0x28b, 6),
		};

		var stamped = new Dictionary<(int, int), (int Word, int Frame)>();

		// The stock park's things as its file places them: item, anchor, angle.
		foreach ( var (id, x, y, angle) in new[]
		{
			(1100, 51, 23, 0), (1303, 51, 30, 0), (1203, 43, 30, 0), (1406, 44, 29, 0), (1413, 55, 29, 0), (1413, 40, 29, 0),
			(1411, 58, 16, 90), (1402, 55, 17, 270), (1402, 55, 16, 270), (1402, 55, 15, 270), (1403, 57, 19, 90),
		} )
		{
			Assert.IsTrue( catalogue.TryGet( id, out var item ), $"item {id}" );
			var model = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );
			var table = ItemTable( model, item.Shape! );
			var covered = ParkObjects.FloorFootprint( item, x, y, angle );

			for ( var row = 0; row < table.GetLength( 1 ); ++row )
			{
				for ( var column = 0; column < table.GetLength( 0 ); ++column )
				{
					if ( table[column, row] is not { } cell )
						continue;

					stamped[ProceduralFloor.CellOf( column, row, angle, covered )] =
						(ProceduralFloor.Stamped( cell.Flags, angle, 0 ), (int)((cell.FrameOffset - model.FrameTableOffset) / 8));
				}
			}
		}

		Assert.AreEqual( measured.Count, stamped.Count, "the cells stamped" );

		foreach ( var (at, want) in measured )
		{
			Assert.IsTrue( stamped.TryGetValue( at, out var got ), $"{at} stamped" );
			Assert.AreEqual( $"{want.Word & ~0x6:x3}:{want.Frame}", $"{got.Word:x3}:{got.Frame}", $"{at}" );
		}
	}

	/// <summary>
	/// <b>A map-offset item's floor is stamped over the cells its model covers, not its anchor's</b>: the Huge Hollow Rock
	/// anchored at (19, 27) stands its model on (19, 26) (<c>mTopLeft</c>, <c>FUN_00467030</c>'s <c>+0xc0</c>), and the Dino
	/// Karts Tunnel at (17, 21) turned 90 on (16, 21). The cells are the ones Alexah's Full Simulation jungle save marks.
	/// </summary>
	/// <remarks><b>Mutation:</b> the footprint taken from the anchor (<see cref="ParkObjects.FootprintAt"/>) puts the rock's
	/// cell (1, 0) on (20, 27) and the tunnel's (0, 1) on (18, 21).</remarks>
	[TestMethod]
	public void AMapOffsetItemsFloorIsStampedOverTheCellsItsModelCovers()
	{
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );

		Assert.IsTrue( catalogue.TryGet( 1427, out var rock ) );
		Assert.IsTrue( catalogue.TryGet( 1501, out var tunnel ) );

		var rockCovered = ParkObjects.FloorFootprint( rock, 19, 27, 0 );
		Assert.AreEqual( (19, 26, 23, 30), rockCovered );
		Assert.AreEqual( (20, 26), ProceduralFloor.CellOf( 1, 0, 0, rockCovered ) );
		Assert.AreEqual( (22, 30), ProceduralFloor.CellOf( 3, 4, 0, rockCovered ) );

		var tunnelCovered = ParkObjects.FloorFootprint( tunnel, 17, 21, 90 );
		Assert.AreEqual( (17, 21), ProceduralFloor.CellOf( 0, 1, 90, tunnelCovered ) );
		Assert.AreEqual( (18, 21), ProceduralFloor.CellOf( 0, 2, 90, tunnelCovered ) );

		// The table cells those are: the rock's edge ring, less its two corners, and the tunnel's two floor cells.
		var rockTable = ItemTable( new ModelFile( $"{rock.Directory}/{rock.Stem}.MD2" ), rock.Shape! );
		Assert.IsNotNull( rockTable[1, 0] );
		Assert.IsNotNull( rockTable[3, 4] );

		var tunnelTable = ItemTable( new ModelFile( $"{tunnel.Directory}/{tunnel.Stem}.MD2" ), tunnel.Shape! );
		Assert.IsNotNull( tunnelTable[0, 1] );
		Assert.IsNotNull( tunnelTable[0, 2] );

		// An item with no map offset is unmoved: the anchor's own footprint.
		Assert.IsTrue( catalogue.TryGet( 1100, out var bounce ) );
		Assert.AreEqual( ParkObjects.FootprintAt( bounce, 51, 23, 0 ), ParkObjects.FloorFootprint( bounce, 51, 23, 0 ) );
	}

	/// <summary>
	/// <b>Every Lost Kingdom floor frame blended either has its UVs in 0..1 or wraps</b> (its word's <c>0x10</c> or <c>0x20</c>),
	/// so the shift the original's loader gives each floor face's UVs (<c>FUN_004679d0</c>: a face's corners moved alike by
	/// whole numbers into 0..1) draws the same picture as the file's own UVs drawn here, where this game wraps: 36 frames of
	/// eight rides leave 0..1, every one wrapping. The original's composite has no wrap bits and its draw clamps it
	/// (<c>0x00590a80</c>, <c>0x00581790</c>; park-engine.md, "The blended floor's texture clamps"), so the two pictures
	/// agree only where each face's shifted UVs stay inside 0..1.
	/// </summary>
	/// <remarks><b>Mutations:</b> none: a check on the data that the drawing's shortcut holds.</remarks>
	[TestMethod]
	public void EveryBlendedFloorFrameLiesInsideOneOrWraps()
	{
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		int blended = 0, outside = 0;

		foreach ( var item in catalogue.All.Where( i => i.Shape != null ) )
		{
			ModelFile model;

			try
			{
				model = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );
			}
			catch ( System.Exception )
			{
				continue;
			}

			if ( (model.HeaderFlags & 0x8) == 0 || ProceduralBase.FloorMesh( model, item.Shape, Park, out _ ) is not { } mesh )
				continue;

			var frames = ItemTable( model, item.Shape! ).Cast<ProceduralFloor.Cell?>().OfType<ProceduralFloor.Cell>()
				.Select( c => c.FrameOffset ).ToHashSet();
			var floor = model.Meshes[mesh];

			foreach ( var material in floor.Materials.Where( m => frames.Contains( m.FrameOffset ) ).DistinctBy( m => m.FrameOffset ) )
			{
				++blended;

				var leaves = floor.Materials.Where( m => m.FrameOffset == material.FrameOffset )
					.SelectMany( m => Enumerable.Range( m.a, m.b ) )
					.SelectMany( face => Enumerable.Range( 0, 3 ).Select( k => floor.TexCoords[(int)floor.Indices[(face * 3) + k]] ) )
					.Any( uv => uv.X < -0.02f || uv.X > 1.02f || uv.Y < -0.02f || uv.Y > 1.02f );

				if ( !leaves )
					continue;

				++outside;
				Assert.AreNotEqual( 0u, material.Flags & 0x30, $"{item.Stem} frame {(material.FrameOffset - model.FrameTableOffset) / 8} leaves 0..1 and clamps" );
			}
		}

		Assert.AreEqual( 36, outside, "frames whose UVs leave 0..1" );
		Assert.IsTrue( blended > 100, $"{blended} blended floor frames looked at" );
	}

	/// <summary>
	/// <b>At 180 and 270 the stamp runs back from the covered cells' far side</b>, as <c>FUN_00463060</c>'s walk does
	/// (<c>0x004633c0</c>: the model's cell less one in x at 180 and 270, in z at 90 and 180), and turns each cell's turn by
	/// the angle, the mirror kept. The stock park stands nothing bigger than one cell at either, so these are the reading's.
	/// </summary>
	/// <remarks><b>Mutations:</b> 270's cell from the first column's last row; 180's from the first; 180 turned as 90.</remarks>
	[TestMethod]
	public void At180And270TheStampRunsBackFromTheFarSide()
	{
		var at270 = (Left: 10, Top: 20, Right: 11, Bottom: 22);
		Assert.AreEqual( (11, 20), ProceduralFloor.CellOf( 0, 0, 270, at270 ) );
		Assert.AreEqual( (11, 22), ProceduralFloor.CellOf( 2, 0, 270, at270 ) );
		Assert.AreEqual( (10, 21), ProceduralFloor.CellOf( 1, 1, 270, at270 ) );

		var at180 = (Left: 10, Top: 20, Right: 12, Bottom: 21);
		Assert.AreEqual( (12, 21), ProceduralFloor.CellOf( 0, 0, 180, at180 ) );
		Assert.AreEqual( (10, 20), ProceduralFloor.CellOf( 2, 1, 180, at180 ) );
		Assert.AreEqual( (11, 21), ProceduralFloor.CellOf( 1, 0, 180, at180 ) );

		Assert.AreEqual( 0x291, ProceduralFloor.Stamped( 0x80, 180, 0 ) );
		Assert.AreEqual( 0x2a1, ProceduralFloor.Stamped( 0x88, 180, 0 ) );
		Assert.AreEqual( 0x289, ProceduralFloor.Stamped( 0xa0, 180, 0 ) );
		Assert.AreEqual( 0x281, ProceduralFloor.Stamped( 0x90, 180, 0 ) );
		Assert.AreEqual( 0x2d1, ProceduralFloor.Stamped( 0xc0, 180, 0 ) );
		Assert.AreEqual( 0x2a1, ProceduralFloor.Stamped( 0x80, 270, 0 ) );
		Assert.AreEqual( 0x289, ProceduralFloor.Stamped( 0x90, 270, 0 ) );
		Assert.AreEqual( 0x281, ProceduralFloor.Stamped( 0x88, 270, 6 ) & ~0x6 );
	}

	/// <summary>
	/// <b>A face is kept for its cell only with both edges more than 0.9 along their axes</b>, and replaces the kept one where
	/// either lies more squarely. No Lost Kingdom floor face has one such edge and not the other, so the tables above cannot
	/// tell the rule from either one being enough.
	/// </summary>
	/// <remarks><b>Mutations:</b> one edge square enough; a later face kept only where both lie more squarely.</remarks>
	[TestMethod]
	public void AFaceIsKeptOnlyWithBothEdgesSquare()
	{
		Assert.IsFalse( ProceduralFloor.Keeps( 0.95f, 0.5f, 0f, 0f ), "x square, z not" );
		Assert.IsFalse( ProceduralFloor.Keeps( 0.5f, 0.95f, 0f, 0f ), "z square, x not" );
		Assert.IsFalse( ProceduralFloor.Keeps( 0.9f, 0.95f, 0f, 0f ), "x at 0.9 is not past it" );
		Assert.IsTrue( ProceduralFloor.Keeps( 0.95f, 0.95f, 0f, 0f ), "both" );
		Assert.IsTrue( ProceduralFloor.Keeps( 0.99f, 0.95f, 0.97f, 0.97f ), "squarer in x alone replaces" );
		Assert.IsFalse( ProceduralFloor.Keeps( 0.97f, 0.97f, 0.97f, 0.97f ), "as square as the kept one does not" );
	}

	/// <summary><b>The edge mask</b>: a floor cell ringed on all four sides by floor is out; one by the picture's edge is in.</summary>
	[TestMethod]
	public void TheEdgeMaskLeavesOutACellRingedByFloor()
	{
		var mask = ProceduralFloor.EdgeMask( new ItemShape( 3, 3, [[4, 9, 4], [4, 10, 4], [4, 4, 0x17]] ) );

		Assert.IsFalse( mask[1, 1], "the middle, ringed by 4, 9, 10 and 4" );
		Assert.IsTrue( mask[0, 0] && mask[2, 2] && mask[1, 0], "the edge" );
		Assert.IsFalse( ProceduralFloor.EdgeMask( new ItemShape( 1, 1, [[3]] ) )[0, 0], "kind 3 is no floor" );
	}
}
