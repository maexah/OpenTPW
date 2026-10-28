using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The items' <c>.hmp</c> files and the lift the build squares take from them. These read real game files and are
/// skipped where there is no installation: see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ItemHeightMapTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private static readonly string[] Themes = ["jungle", "fantasy", "hallow", "space"];

	/// <summary>
	/// Every shipped <c>.hmp</c> - an item's own, and the track pieces, pylons, queue pieces and park hoarding in the
	/// same archives - reads with the original's signature, is <c>48 + 27n</c> bytes, and keeps over each cell the
	/// highest of that cell's 25 raster bytes; the four gates are the exception, a raster bare but for its last cell
	/// under a grid of one height. An item's own is the size its <c>.sam</c>'s footprint says, so the original would
	/// rebuild none of them.
	/// </summary>
	[TestMethod]
	public void EveryShippedFileReadsAndFitsItsItem()
	{
		var read = 0;
		var items = 0;
		var gates = 0;

		foreach ( var theme in Themes )
		{
			foreach ( var wad in Directory.GetFiles( Path.Combine( GameDir.Data, "levels", theme ), "*.wad", SearchOption.AllDirectories ) )
			{
				var directory = Path.GetRelativePath( GameDir.Data, Path.ChangeExtension( wad, null ) ).Replace( '\\', '/' );
				var stem = Path.GetFileName( directory );

				foreach ( var member in data.GetFiles( directory ).Where( name => name.EndsWith( ".hmp", StringComparison.OrdinalIgnoreCase ) ) )
				{
					var name = $"{directory}/{Path.GetFileName( member )}";
					var bytes = data.ReadAllBytes( name );

					var file = ItemHeightMapFile.Read( bytes );
					Assert.IsNotNull( file, $"{name}: the original's loader would not accept its signature" );

					var cells = file.Columns * file.Rows;
					Assert.AreEqual( 48 + (27 * cells), bytes.Length, $"{name}: {file.Columns}x{file.Rows}" );

					var width = file.Columns * ItemHeightMapFile.SamplesPerCell;
					var maxima = Enumerable.Range( 0, cells ).Select( cell => (byte)Enumerable.Range( 0, 25 ).Max( sample =>
						file.Raster[(((cell / file.Columns * 5) + (sample / 5)) * width) + (cell % file.Columns * 5) + (sample % 5)] ) ).ToArray();

					if ( stem.Equals( "gates", StringComparison.OrdinalIgnoreCase ) )
					{
						Assert.AreEqual( 1, file.CellMaxima.Distinct().Count(), $"{name}: the gate's grid is one height" );
						Assert.IsTrue( maxima.Take( cells - 1 ).All( b => b == 0 ), $"{name}: the gate's raster is bare to its last cell" );
						++gates;
					}
					else
					{
						CollectionAssert.AreEqual( maxima, file.CellMaxima, $"{name}: the per-cell grid" );
					}

					Assert.IsTrue( file.Marks.All( b => b is 0 or 1 ), $"{name}: the mark plane" );

					if ( Path.GetFileNameWithoutExtension( member ).Equals( stem, StringComparison.OrdinalIgnoreCase )
						&& data.GetFiles( directory ).Any( other => Path.GetFileName( other ).Equals( $"{stem}.sam", StringComparison.OrdinalIgnoreCase ) ) )
					{
						using var sam = data.OpenRead( $"{directory}/{stem}.sam" );
						var item = new ItemDescriptionFile( sam );
						Assert.AreEqual( (item.FootprintWidth, item.FootprintDepth), (file.Columns, file.Rows),
							$"{name}: the .sam's footprint against the .hmp's" );
						++items;
					}

					++read;
				}
			}
		}

		Assert.AreEqual( 435, read, "every .hmp the four themes ship" );
		Assert.AreEqual( 274, items, "an item's own, beside its .sam" );
		Assert.AreEqual( 4, gates );
	}

	/// <summary>
	/// The lift over the shipped park's built cells: the Belly Bounce (51,23) unturned, the fountain (57,19) a quarter
	/// turn, a path cell and bare ground. Each is ceil10 of the item's height there, the fountain's read through its
	/// turn - (59,19) is the one corner its <c>.hmp</c> keeps low (14 / 2.55, so 10), which a turn ignored misses.
	/// </summary>
	[TestMethod]
	public void TheSquaresLiftOverTheShippedParksBuiltCells()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( world );
		var catalogue = new ParkItemCatalogue( "jungle", data, instantAction: true );

		HeightfieldFile field;

		using ( var terrain = data.OpenRead( "levels/jungle/terrain/base.MD2" ) )
			field = new HeightfieldFile( terrain );

		(int X, int Y, float Lift)[] expected =
		[
			(51, 23, 20f), (52, 24, 20f), (53, 23, 20f), (53, 26, 10f),
			(58, 18, 40f), (57, 19, 20f), (57, 17, 20f), (59, 17, 20f), (59, 19, 10f),
			(48, 22, 0f), (20, 60, 0f)
		];

		foreach ( var (x, y, lift) in expected )
			Assert.AreEqual( lift, ParkBuildMarkers.LiftOver( state, catalogue, field, x, y ), $"({x},{y})" );
	}

	/// <summary>
	/// A Belly Bounce stood on the hill at (73,66), where the ground is 32.7: its corner cell is 29 / 2.55 = 11.4 high
	/// over its base, 43.1, halfway between the lowest and the highest ground under its footprint (its model's height,
	/// <c>ParkObjects.BaseHeight</c>), so ceil10( 54 ) = 60. The ground alone would give 40, the item alone 20, and a base on
	/// the ground under its cell 50.
	/// </summary>
	/// <remarks><b>Mutation:</b> the base on the ground under the model's cell.</remarks>
	[TestMethod]
	public void TheLiftCountsTheGroundUnderTheThing()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var state = new ParkState( new ParkWorld( new SaveReader( stream ).ReadFile() ) );
		var catalogue = new ParkItemCatalogue( "jungle", data, instantAction: true );

		HeightfieldFile field;

		using ( var terrain = data.OpenRead( "levels/jungle/terrain/base.MD2" ) )
			field = new HeightfieldFile( terrain );

		Assert.AreEqual( 32, (int)field.HeightAt( 73, 66 ), "the hill" );
		Assert.AreEqual( 40f, ParkBuildMarkers.LiftOver( state, catalogue, field, 73, 66 ), "the bare hill: its ground" );

		state.AddObject( new ParkWorld.CatalogueObject( state.NextThingId(), 1100, 73 << 8, 66 << 8, 0 ) );

		Assert.AreEqual( 60f, ParkBuildMarkers.LiftOver( state, catalogue, field, 73, 66 ) );
	}

	/// <summary>
	/// A cell under a kart piece answers the piece's own <c>.hmp</c> plus its root's height (<c>FUN_0053bf30</c> before the
	/// thing, <c>FUN_00452ae0</c>), its 0xc cells by their anchor's piece: a Dino Karts' station laid from (30,24), as the
	/// original's on <c>q581/karts.TPWS</c>, whose mouths' roots read -2 and -7 there (<c>q775/orig</c>, <c>liftcells.py</c>).
	/// So the mouth's square lifts to 10 and the other mouth's to nought, the original's 11.5 and 1.5. The ground alone gives
	/// -3 and -8, the 0xc cell asked as itself the ground too.
	/// </summary>
	/// <remarks><b>Mutation:</b> the piece not asked; a 0xc cell not followed to its anchor.</remarks>
	[TestMethod]
	public void ACellUnderAKartPieceAnswersThePiecesHeight()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var state = new ParkState( new ParkWorld( new SaveReader( stream ).ReadFile() ) );
		var catalogue = new ParkItemCatalogue( "jungle", data, instantAction: true );

		HeightfieldFile field;

		using ( var terrain = data.OpenRead( "levels/jungle/terrain/base.MD2" ) )
			field = new HeightfieldFile( terrain );

		ParkTrackPieces.LayStation( state, 1, 30, 24, 0 );

		float At( int x, int y ) => ParkItemHeights.AtCell( state, catalogue, field, x, y, "jungle" );

		Assert.AreEqual( -3f, field.HeightAt( 34, 25 ), 0.001f, "the ground under the mouth" );
		Assert.AreEqual( -2f + (14 * ItemHeightMapFile.UnitsPerByte), At( 34, 25 ), 0.001f, "the mouth's piece" );
		Assert.AreEqual( -2f + (15 * ItemHeightMapFile.UnitsPerByte), At( 34, 26 ), 0.001f, "its 0xc cell, by its anchor's piece" );
		Assert.AreEqual( -7f + (14 * ItemHeightMapFile.UnitsPerByte), At( 28, 25 ), 0.001f, "the other mouth's piece" );
		Assert.AreEqual( 10f, ParkBuildMarkers.TrackLiftOver( state, 34, 25, At ) );
		Assert.AreEqual( 0f, ParkBuildMarkers.TrackLiftOver( state, 28, 25, At ) );
	}

	/// <summary>
	/// A path or queue tool's square over a kart piece lifts by the piece's height, the same rule that lifts the track
	/// tool's (Q776; <c>FUN_00532fc0</c> asks <c>FUN_00452ae0</c>, whose track-layer object comes first): over the
	/// station laid from (30,24) the mouth (34,25) reads 3.49 so 10, its 0xc cell (34,26) by its anchor 3.88 so 10, the
	/// other mouth (28,25) nought. The ground there is -3 and -8, and a cell asked as the ground (or a 0xc cell not
	/// followed to its anchor) lifts nought. Nothing is counted; a boundary hoarding's is
	/// <see cref="ASquareOverABoundaryHoardingIsLiftedByItsFile"/>.
	/// </summary>
	/// <remarks><b>Mutation:</b> the piece not asked; a 0xc cell not followed to its anchor; a map type 4 or 7 cell asked as the ground.</remarks>
	[TestMethod]
	public void APathSquareOverAKartPieceIsLiftedByThePiece()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( world );
		var catalogue = new ParkItemCatalogue( "jungle", data, instantAction: true );

		HeightfieldFile field;

		using ( var terrain = data.OpenRead( "levels/jungle/terrain/base.MD2" ) )
			field = new HeightfieldFile( terrain );

		ParkTrackPieces.LayStation( state, 1, 30, 24, 0 );
		Unimplemented.Forget();

		Assert.AreEqual( 10f, ParkBuildMarkers.LiftOver( state, catalogue, field, 34, 25, "jungle" ), "the mouth's piece" );
		Assert.AreEqual( 10f, ParkBuildMarkers.LiftOver( state, catalogue, field, 34, 26, "jungle" ), "its 0xc cell, by its anchor" );
		Assert.AreEqual( 0f, ParkBuildMarkers.LiftOver( state, catalogue, field, 28, 25, "jungle" ), "the other mouth's piece" );
		Assert.IsFalse( Unimplemented.Summary.Any( entry => entry.What == "MARKER_LIFT_OVER_TRACK_THING" ), "a built piece is not a gap" );

		Unimplemented.Forget();

		Assert.AreEqual( 0f, ParkBuildMarkers.LiftOver( state, catalogue, field, 34, 25, "nosuchtheme" ), "a piece whose file is not read stands on the ground" );
		Assert.IsFalse( Unimplemented.Summary.Any( entry => entry.What == "MARKER_LIFT_OVER_TRACK_THING" ), "and is no gap of this count" );

		state.SetRecord( 34, 26, state.Record( 34, 26 ) with { Type = 7 } );

		Assert.AreEqual( 10f, ParkBuildMarkers.LiftOver( state, catalogue, field, 34, 26, "jungle" ), "land of type 7 under the piece: the piece first" );

		Unimplemented.Forget();

		// A boundary anchor whose record holds no piece of family 5 is the gap, and stays on the ground.
		state.SetRecord( 73, 66, ParkState.CellFor( world, 73, 66 ) with { Type = 0, TrackType = 25 } );
		state.SetRecord( 74, 66, ParkState.CellFor( world, 74, 66 ) with { Type = 0, TrackType = ParkTrackPieces.KartCell, TrackParentId = (ushort)((66 * 128) + 73 + 1) } );

		Assert.AreEqual( 0f, ParkBuildMarkers.LiftOver( state, catalogue, field, 74, 66, "jungle" ), "an anchor with no piece named stays on the ground" );
		Assert.IsTrue( Unimplemented.Summary.Any( entry => entry.What == "MARKER_LIFT_OVER_TRACK_THING" ), "and is counted" );

		Unimplemented.Forget();

		Assert.AreEqual( 0f, ParkBuildMarkers.LiftOver( state, catalogue, field, 73, 66, "jungle" ), "the 0x19 anchor itself, with no piece named, stays on the ground" );
		Assert.IsTrue( Unimplemented.Summary.Any( entry => entry.What == "MARKER_LIFT_OVER_TRACK_THING" ), "and is counted: its own cell is asked" );

		Unimplemented.Forget();

		// A water ride's mouth (0x10) and its 0xc cell lift by the water kit's piece .hmp too.
		ParkTrackPieces.LayStation( state, 2, 18, 18, 0 );
		Unimplemented.Forget();
		Assert.AreEqual( 10f, ParkBuildMarkers.LiftOver( state, catalogue, field, 22, 19, "jungle" ), "the water mouth's piece, over ground at -6" );
		Assert.AreEqual( 10f, ParkBuildMarkers.LiftOver( state, catalogue, field, 22, 20, "jungle" ), "its 0xc cell, by its anchor" );

		state.SetRecord( 22, 20, state.Record( 22, 20 ) with { TrackType = ParkTrackPieces.WaterCell, TrackParentId = (ushort)MapStep.CellId( 22, 19 ) } );

		Assert.AreEqual( 10f, ParkBuildMarkers.LiftOver( state, catalogue, field, 22, 20, "jungle" ), "a 0x11 cell, by its anchor" );
		Assert.IsFalse( Unimplemented.Summary.Any( entry => entry.What == "MARKER_LIFT_OVER_TRACK_THING" ), "a water piece is not a gap" );
	}

	/// <summary>
	/// A path or queue tool's square over a boundary hoarding, or over a 0xc cell parented to one, lifts by the hoarding's
	/// <c>.hmp</c> (Q777; the original's (93,21) and (94,21) lift 20, Q436). The stock park's anchor (93,21) is record row 2
	/// (<c>ho_fos1</c>), turned 180, whose file is 51 over all four cells, 19.99 so 20 over ground 0; its 0xc cell (94,21) reads
	/// the same by its anchor. Cells outside the two by two, and a cell asked as ground, lift nought. Nothing is counted.
	/// On ground raised above 20 the model's root stands there, so the lift is the file's height plus the root's, not the ground's.
	/// </summary>
	/// <remarks><b>Mutation:</b> the hoarding not asked (ground only); a 0xc cell not followed to its anchor; the file's height dropped
	/// (the root alone); the root dropped (the file alone); the record's row not read (row 0); the family not required.</remarks>
	[TestMethod]
	public void ASquareOverABoundaryHoardingIsLiftedByItsFile()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( world );
		var catalogue = new ParkItemCatalogue( "jungle", data, instantAction: true );

		HeightfieldFile field;

		using ( var terrain = data.OpenRead( "levels/jungle/terrain/base.MD2" ) )
			field = new HeightfieldFile( terrain );

		Unimplemented.Forget();

		Assert.AreEqual( 25, state.Record( 93, 21 ).TrackType, "the stock park's east wall" );
		Assert.AreEqual( 20f, ParkBuildMarkers.LiftOver( state, catalogue, field, 93, 21, "jungle" ), "the anchor" );
		Assert.AreEqual( 20f, ParkBuildMarkers.LiftOver( state, catalogue, field, 94, 21, "jungle" ), "its 0xc cell, by its anchor" );
		Assert.AreEqual( 0f, ParkBuildMarkers.LiftOver( state, catalogue, field, 92, 21, "jungle" ), "bare ground beside it" );
		Assert.IsFalse( Unimplemented.Summary.Any( entry => entry.What == "MARKER_LIFT_OVER_TRACK_THING" ), "a built hoarding is not a gap" );

		// Raised ground: row 2 turned 0 at (73,66), whose file is 51 and whose root stands on the ground under its two by two.
		Assert.IsTrue( field.HeightAt( 74, 66 ) > 20, "raised ground, which a lift by the ground would show" );

		state.SetRecord( 73, 66, ParkState.CellFor( world, 73, 66 ) with { Type = 0, TrackType = 25, TrackTileSet = 5, TrackTileIndex = 2, TrackTileAngle = 0 } );
		state.SetRecord( 74, 66, ParkState.CellFor( world, 74, 66 ) with { Type = 0, TrackType = ParkTrackPieces.KartCell, TrackParentId = (ushort)((66 * 128) + 73 + 1) } );

		var root = ParkBoundaryHoarding.StandOf( ParkBoundaryHoarding.PieceAt( 73, 66, state.Record( 73, 66 ) )!.Value, field ).Origin.Z;
		var top = (int)((51 * ItemHeightMapFile.UnitsPerByte) + root);

		Assert.IsTrue( root > 20 && top > (int)root, "the file adds to the root" );
		Assert.AreEqual( (top + 9) / 10 * 10, ParkBuildMarkers.LiftOver( state, catalogue, field, 74, 66, "jungle" ), "the file over the root" );
		Assert.AreEqual( (top + 9) / 10 * 10, ParkBuildMarkers.LiftOver( state, catalogue, field, 73, 66, "jungle" ), "and at the anchor" );
		Assert.AreNotEqual( ((int)root + 9) / 10 * 10, ParkBuildMarkers.LiftOver( state, catalogue, field, 74, 66, "jungle" ), "not the root alone" );
		Assert.IsFalse( Unimplemented.Summary.Any( entry => entry.What == "MARKER_LIFT_OVER_TRACK_THING" ), "nothing is counted" );

		// The track tool asks the same height of each cell of its two by two.
		float At( int x, int y ) => ParkItemHeights.AtCell( state, catalogue, field, x, y, "jungle" );
		Assert.AreEqual( top, (int)At( 74, 66 ), "the track tool's cell height" );
		Assert.AreEqual( top, (int)At( 73, 66 ), "at the anchor" );
	}

	/// <summary>
	/// Every anchor of the stock park's boundary (143, rows of five models) lifts a path square over its own two by two by its
	/// row's file: over the four cells the lowest lift is the file's lowest byte (as a height over the root, rounded up to ten)
	/// and the highest its highest, whatever the turn, so the row the record names is the model read (Q777; ho_fos1's 51 is
	/// 20, ho_fos2's 76 and 71 are 30, ho_fob2's 25 is 10 beside three 20s, ho_fob1's 0 beside three 20s).
	/// </summary>
	/// <remarks><b>Mutation:</b> the record's row not read (row 0); the file's own cells not asked; the family not required; the
	/// anchor's piece standing a cell off.</remarks>
	[TestMethod]
	public void EveryStockBoundaryAnchorLiftsByItsRowsFile()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( world );
		var catalogue = new ParkItemCatalogue( "jungle", data, instantAction: true );

		HeightfieldFile field;

		using ( var terrain = data.OpenRead( "levels/jungle/terrain/base.MD2" ) )
			field = new HeightfieldFile( terrain );

		Unimplemented.Forget();

		var anchors = 0;
		var rows = new System.Collections.Generic.HashSet<int>();

		for ( var y = 0; y < ParkWorld.MapSize; ++y )
		{
			for ( var x = 0; x < ParkWorld.MapSize; ++x )
			{
				var cell = state.Record( x, y );

				if ( cell.TrackType != ParkTrackPieces.Boundary )
					continue;

				++anchors;
				rows.Add( cell.TrackTileIndex );

				var piece = ParkBoundaryHoarding.PieceAt( x, y, cell )!.Value;
				var file = ItemHeightMapFile.Read( data.ReadAllBytes( $"levels/jungle/hoarding/{ParkBoundaryHoarding.ModelOf( cell.TrackTileIndex )}.hmp" ) )!;
				var root = ParkBoundaryHoarding.StandOf( piece, field ).Origin.Z;
				var lifts = new[] { (x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1) }
					.Select( at => ParkBuildMarkers.LiftOver( state, catalogue, field, at.Item1, at.Item2, "jungle" ) ).ToArray();

				Assert.AreEqual( ParkBuildMarkers.CeilingTen( (int)((file.CellMaxima.Min() * ItemHeightMapFile.UnitsPerByte) + root) ), lifts.Min(),
					$"({x},{y}) row {cell.TrackTileIndex}: the lowest" );
				Assert.AreEqual( ParkBuildMarkers.CeilingTen( (int)((file.CellMaxima.Max() * ItemHeightMapFile.UnitsPerByte) + root) ), lifts.Max(),
					$"({x},{y}) row {cell.TrackTileIndex}: the highest" );
			}
		}

		Assert.AreEqual( 143, anchors );
		Assert.IsTrue( rows.Count >= 5, $"{rows.Count} rows of the table stand in the park, among them the unevenly high ones" );
		Assert.IsFalse( Unimplemented.Summary.Any( entry => entry.What == "MARKER_LIFT_OVER_TRACK_THING" ), "nothing is counted" );
	}

	/// <summary>The original's rounding: <c>(t + 9) / 10 * 10</c>, integer division towards nought.</summary>
	[TestMethod]
	public void TheLiftRoundsUpToTen()
	{
		Assert.AreEqual( 0f, ParkBuildMarkers.CeilingTen( 0 ) );
		Assert.AreEqual( 10f, ParkBuildMarkers.CeilingTen( 1 ) );
		Assert.AreEqual( 10f, ParkBuildMarkers.CeilingTen( 10 ) );
		Assert.AreEqual( 20f, ParkBuildMarkers.CeilingTen( 11 ) );
		Assert.AreEqual( 0f, ParkBuildMarkers.CeilingTen( -5 ) );
	}
}
