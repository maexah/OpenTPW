using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace OpenTPW.Tests;

/// <summary>
/// The procedural ground's tiles against the original's own <c>FUN_0055f780</c> run under an emulator on the shipped
/// files (<c>lndtile.py</c>; docs/exe/park-engine.md, "The procedural ground"). Every expected hash is of its tiles'
/// red, green and blue, row 0 first, made there and nowhere else.
/// </summary>
[TestClass]
public class ProceduralGroundTests
{
	private static ProceduralGround Ground( string theme, int size = 16 )
	{
		var data = GameData.Required();

		using var stream = data.OpenRead( $"levels/{theme}/terrain/base.lnd" );
		return new ProceduralGround( new ProceduralGroundFile( stream ), size );
	}

	private static string Hash( byte[] bytes ) => Convert.ToHexString( SHA256.HashData( bytes ) ).ToLowerInvariant();

	/// <summary>
	/// <paramref name="test"/> at the original's texture depth, four bits a channel (opentpw.cfg's <c>graphics faithful</c>),
	/// where a path texture's texels are the ones the original's oracle was given.
	/// </summary>
	private static void AtTheOriginalsDepth( Action test )
	{
		var was = Texture.KeepsEightBits;

		try
		{
			Texture.KeepsEightBits = false;
			test();
		}
		finally
		{
			Texture.KeepsEightBits = was;
		}
	}

	/// <summary>
	/// Every cell of the box, 0..95 x 0..84, of all four parks at 16 pixels, the original's size today, and at 32, the size
	/// OpenTPW draws (<see cref="Level.TileSizeFor"/>), as one run of tiles row by row (<c>lndsheet.py</c>).
	/// </summary>
	/// <remarks><b>Mutation:</b> any step of the tile changed - a ramp, the window, a layer's pair, a span's class, the
	/// frozen u1, the rows' order - changes a hash; so does a set read for the wrong size.</remarks>
	[TestMethod]
	[DataRow( "jungle", 16, "f8c9664ac8568557dadaba9a1610d8edd58d893f6515334cdbf90d6363cb02f7" )]
	[DataRow( "fantasy", 16, "7078add17d1fc5a4bc00c2aa57b16a338919532febfe5fe03d7f5955b4b9cd04" )]
	[DataRow( "hallow", 16, "6e86ce1f643687708f5eaa6140d5ff773615f9312ce16daf9098c75b1c4f30b9" )]
	[DataRow( "space", 16, "b9360aa15ba92dd1fb7d97622f241c520049ecadbbe1148ed8ef4f1d22b0cadd" )]
	[DataRow( "jungle", 32, "cc9cbf186cfbeb9e9b72e498c1332a648a8a4812acec971da64525045cd0058b" )]
	[DataRow( "fantasy", 32, "487cb2ac7ef3d2873730a1a079907fa10e044c653ac16f179a167f68825cfbc1" )]
	[DataRow( "hallow", 32, "8f90b86d01df7aa80d58dcd2bdc22d382b141b6291cb541186a4ccd99aaf6b95" )]
	[DataRow( "space", 32, "2332b15821e398d0443b2b10f061938001e3c0f28839ce0c692703b2b0195335" )]
	public void EveryTileOfEveryParkIsTheOriginals( string theme, int size, string expected )
	{
		var ground = Ground( theme, size );
		using var all = new MemoryStream();

		for ( var y = 0; y < 85; ++y )
			for ( var x = 0; x < 96; ++x )
				all.Write( ground.Tile( x, y ) );

		Assert.AreEqual( expected, Hash( all.ToArray() ) );
	}

	/// <summary>
	/// One jungle cell for each rule a wrong reading gets wrong, so a failure names its rule: (0, 0) has spans with both
	/// masks mixed, where every pixel takes the span's first u1; (28, 0) is refused by the map's byte 0 and grey;
	/// (62, 41) is all the second grass; (95, 40) is the box's last column, whose samples run on into the map's next row.
	/// </summary>
	/// <remarks><b>Mutation:</b> u1 stepped in the mixed spans fails (0, 0); the gate's four samples moved fails
	/// (28, 0); a whole span lerped by 255 fails (62, 41); the map read clamped or by two indices fails (95, 40).</remarks>
	[TestMethod]
	[DataRow( 0, 0, "8b0289e2b488b20dce18920c5f8a7a8e3d74c43bb5d4b761be750eb6a6ed52ac" )]
	[DataRow( 28, 0, "cbab715ff6cfaa81c9b09e014dc1406ceff24034caa265de65f9f948c5434807" )]
	[DataRow( 62, 41, "4298bd86e742d9f0e04c34d20c35fed8d8cfbb97f1297aa5b9fa602d08dcb48c" )]
	[DataRow( 95, 40, "e3a190acb91313675c1e1d65b307c59dee4567f27cbcba800e8c8994b1169c27" )]
	public void EachRuleOnItsOwnCell( int x, int y, string expected )
		=> Assert.AreEqual( expected, Hash( Ground( "jungle" ).Tile( x, y ) ) );

	/// <summary>The gate's grey is <c>0x7f7f7f</c> on every pixel.</summary>
	[TestMethod]
	public void ARefusedCellIsGrey()
		=> Assert.IsTrue( Ground( "jungle" ).Tile( 28, 0 ).All( b => b == ProceduralGround.Grey ) );

	/// <summary>
	/// <c>FUN_0055e710</c> on jungle's first pair (48, 40): lo 28, hi 68, nought at 27, 255 at 69, and at 48
	/// <c>(48 x 255 - 28 x 255) / 41</c> = 124.
	/// </summary>
	/// <remarks><b>Mutation:</b> dividing by w where the original divides by w + 1 gives 127 at 48; 255 from hi itself
	/// rather than above it gives 255 at 68.</remarks>
	[TestMethod]
	public void TheRampIsTheOriginals()
	{
		var ramp = ProceduralGround.Ramp( 48, 40 );

		Assert.AreEqual( 0, ramp[27] );
		Assert.AreEqual( 0, ramp[28] );
		Assert.AreEqual( 124, ramp[48] );
		Assert.AreEqual( 248, ramp[68] );
		Assert.AreEqual( 255, ramp[69] );
	}

	/// <summary>The file reads to its end, its sets are 64, 32 and 16, and its map is 384 x 344 of four bytes.</summary>
	[TestMethod]
	public void TheFileReadsWhole()
	{
		var data = GameData.Required();

		foreach ( var theme in new[] { "jungle", "fantasy", "hallow", "space" } )
		{
			using var stream = data.OpenRead( $"levels/{theme}/terrain/base.lnd" );
			var file = new ProceduralGroundFile( stream );

			Assert.AreEqual( stream.Length, stream.Position, theme );
			CollectionAssert.AreEqual( new[] { 64, 32, 16 }, file.Sets.Select( set => set.TileSize ).ToArray(), theme );
			Assert.AreEqual( (384, 344, 4), (file.Map.Width, file.Map.Height, file.Map.Depth), theme );
		}
	}

	/// <summary>
	/// The texture coordinates of grid corners (x, y), (x + 1, y), (x + 1, y + 1), (x, y + 1) for a cell's word, the
	/// original's ring (x, y), (x, y + 1), (x + 1, y + 1), (x + 1, y) read in <c>FUN_0056f670</c>: a quarter turn
	/// (<c>0x08</c>) gives (x, y) ring corner 3's (1, 0); three quarters (<c>0x20</c>) gives it ring corner 1's (0, 1);
	/// with two turn bits set the first of <c>0x08</c>, <c>0x10</c>, <c>0x20</c> wins.
	/// </summary>
	/// <remarks><b>Mutation:</b> the ring run our old way round (0, 0), (1, 0), (1, 1), (0, 1) swaps the 0x08 and 0x20
	/// rows; the rotation taken as one field (a switch on <c>0x38</c>) gives 0x18 nothing and 0x30 the three-quarter
	/// turn; the mirror dropped fails 0x40 and 0x48.</remarks>
	[TestMethod]
	[DataRow( (ushort)0x00, new[] { 0f, 0f, 1f, 0f, 1f, 1f, 0f, 1f } )]
	[DataRow( (ushort)0x40, new[] { 0f, 0f, 0f, 1f, 1f, 1f, 1f, 0f } )]
	[DataRow( (ushort)0x08, new[] { 1f, 0f, 1f, 1f, 0f, 1f, 0f, 0f } )]
	[DataRow( (ushort)0x10, new[] { 1f, 1f, 0f, 1f, 0f, 0f, 1f, 0f } )]
	[DataRow( (ushort)0x20, new[] { 0f, 1f, 0f, 0f, 1f, 0f, 1f, 1f } )]
	[DataRow( (ushort)0x18, new[] { 1f, 0f, 1f, 1f, 0f, 1f, 0f, 0f } )]
	[DataRow( (ushort)0x30, new[] { 1f, 1f, 0f, 1f, 0f, 0f, 1f, 0f } )]
	[DataRow( (ushort)0x48, new[] { 0f, 1f, 1f, 1f, 1f, 0f, 0f, 0f } )]
	public void ACellsWordTurnsItsArtAsTheOriginalsRing( ushort flags, float[] expected )
	{
		var uvs = ParkGround.CellUvs( flags );

		CollectionAssert.AreEqual( expected, uvs.SelectMany( uv => new[] { uv.X, uv.Y } ).ToArray() );
	}

	/// <summary>
	/// The box: every shipped park's offsets are -1, held to nought, so it is 0, 0 to 95, 84; an offset of 1 or more
	/// widens it.
	/// </summary>
	/// <remarks><b>Mutation:</b> the offset taken as it is gives 94, 83; held to nought at 1 rather than below it gives
	/// 98 or 86 where an offset is 1.</remarks>
	[TestMethod]
	public void TheBoxIsMapInfosAndTheOffsetsHeldAtNought()
	{
		Assert.AreEqual( (0, 0, 95, 84), GroundTiles.Box( 0, 0, 95, 84, -1, -1 ) );
		Assert.AreEqual( (3, 2, 99, 88), GroundTiles.Box( 3, 2, 95, 84, 1, 2 ) );
		Assert.AreEqual( (3, 2, 100, 87), GroundTiles.Box( 3, 2, 95, 84, 2, 1 ) );
	}

	/// <summary>
	/// A tile is drawn from its first texel's centre to its last's: cell (2, 3) of 16-pixel tiles in a 1,536 x 1,360
	/// texture runs from (32.5, 48.5) to (47.5, 63.5) texels.
	/// </summary>
	/// <remarks><b>Mutation:</b> no half-texel inset gives 32 and 48; the full size across gives 48.5.</remarks>
	[TestMethod]
	public void ATileRunsFromTexelCentreToTexelCentre()
	{
		var first = GroundTiles.TileUv( 2, 3, new Vector2( 0f, 0f ), 16, 1536, 1360 );
		var last = GroundTiles.TileUv( 2, 3, new Vector2( 1f, 1f ), 16, 1536, 1360 );

		Assert.AreEqual( 32.5f, first.X * 1536, 1e-3f );
		Assert.AreEqual( 48.5f, first.Y * 1360, 1e-3f );
		Assert.AreEqual( 47.5f, last.X * 1536, 1e-3f );
		Assert.AreEqual( 63.5f, last.Y * 1360, 1e-3f );
	}

	/// <summary>
	/// With the compositor on a ground cell loses its mirror and turn (<c>FUN_0046ded0(1)</c>): jungle's (0, 0) reads
	/// <c>0x42</c> in base.MD2 and is drawn as <c>0x02</c>; a path cell (<c>0x80</c>) and a cell the scenery draws
	/// (<c>0x01</c>) keep theirs; with it off the word stands.
	/// </summary>
	/// <remarks><b>Mutation:</b> the clear dropped keeps 0x42; clearing path cells too gives 0x82 for 0xea; masking with
	/// 0xff87 on 0x01 cells gives 0x03 for 0x7b.</remarks>
	[TestMethod]
	public void WithTheCompositorOnAGroundCellIsNotTurned()
	{
		Assert.AreEqual( (ushort)0x02, ParkGround.DrawnWord( 0x42, true ) );
		Assert.AreEqual( (ushort)0x502, ParkGround.DrawnWord( 0x57a, true ) );
		Assert.AreEqual( (ushort)0xea, ParkGround.DrawnWord( 0xea, true ) );
		Assert.AreEqual( (ushort)0x7b, ParkGround.DrawnWord( 0x7b, true ) );
		Assert.AreEqual( (ushort)0x42, ParkGround.DrawnWord( 0x42, false ) );
	}

	/// <summary>
	/// The tiles are laid bottom up, as <c>test.shader</c> turns every v over: in a box of 4 x 5 jungle tiles (80 rows),
	/// tile (2, 3)'s row 0 stands on row 80 - 1 - 48 = 31 and its row 15 on row 16, from column 32.
	/// </summary>
	/// <remarks><b>Mutation:</b> laid top down, row 0 stands on row 48 and the field is drawn mirrored about its middle
	/// row (Q488's run2, every cell showing another's tile).</remarks>
	[TestMethod]
	public void TheTilesAreLaidBottomUp()
	{
		var ground = Ground( "jungle" );
		var pixels = GroundTiles.Lay( ground, 4, 5, new ProceduralGround.Overlay?[20], out _ );
		var tile = ground.Tile( 2, 3 );

		foreach ( var (k, row) in new[] { (0, 31), (15, 16) } )
		{
			for ( var j = 0; j < 16; ++j )
			{
				var at = ((row * 64) + 32 + j) * 4;
				var from = ((k * 16) + j) * 3;
				CollectionAssert.AreEqual( tile[from..(from + 3)], pixels[at..(at + 3)], $"row {k}, column {j}" );
			}
		}
	}

	/// <summary>A jungle path tile's texture as the compositor reads it, through the repo's own decode.</summary>
	private static ProceduralGround.PathTexture PathTexture( string name )
	{
		using var stream = GameData.Required().OpenRead( $"levels/jungle/terrain/pathtex/{name}.wct" );
		var data = new TextureFile( stream ).Data;
		return ProceduralGround.PathTexture.FromRgba( data.Data, data.Width, data.Height );
	}

	/// <summary>
	/// Every one of the shipped park's 78 path cells, its own tile (by the <c>.tct</c>'s PathTex) laid over its ground
	/// by the word the path writes, against the original's function given the same texels (<c>lib/pathoracle.py N</c>: the
	/// surface's A4R4G4B4, measured in the running original), at 16 pixels and at 32. Without
	/// the path the same 78 hash <c>1724f766...</c> at 16.
	/// </summary>
	/// <remarks><b>Mutation:</b> the alpha taken as eight bits, the texture read top down or at every texel rather than
	/// every w / N, the path turned where the ground should be, or the angle's turn bits swapped, each changes the
	/// hash.</remarks>
	[TestMethod]
	[DataRow( 16, "d231594b1b4fd9cd97f7cfb90e92e6ab66c6f09f5a53e29e64adccce9bb4774e" )]
	[DataRow( 32, "ff4cc84f480366ead174bfcd09a09061a2afc753edeed23f4029971712454332" )]
	public void EveryPathTileOfTheShippedParkIsTheOriginals( int size, string expected ) => AtTheOriginalsDepth( () =>
	{
		var data = GameData.Required();
		FileSystem = data;

		var world = new ParkWorld( new SaveReader( new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) ) ).ReadFile() );
		using var md2 = data.OpenRead( "levels/jungle/terrain/base.MD2" );
		var field = new HeightfieldFile( md2 );
		var table = ParkPaths.ReadTable( "jungle" );
		var ground = Ground( "jungle", size );
		using var all = new MemoryStream();
		var paths = 0;

		for ( var y = 0; y < 85; ++y )
		{
			for ( var x = 0; x < 96; ++x )
			{
				var cell = world.CellAt( x, y );

				if ( !ParkPaths.IsPath( cell ) )
					continue;

				var file = ParkPaths.TileFile( "jungle", table, cell.TileIndex )!;
				using var stream = data.OpenRead( file );
				var texels = new TextureFile( stream ).Data;
				var texture = ProceduralGround.PathTexture.FromRgba( texels.Data, texels.Width, texels.Height );
				var word = ParkGround.PathWord( (ushort)field.Cells[(y * field.CellsX) + x], cell.TileAngle );

				all.Write( ground.Tile( x, y, new ProceduralGround.Overlay( texture, word ) ) );
				++paths;
			}
		}

		Assert.AreEqual( 78, paths );
		Assert.AreEqual( expected, Hash( all.ToArray() ) );
	} );

	/// <summary>
	/// Each of the eight cases at the last pass's head on one cell and one tile (jungle's (10, 10), whose ground tells
	/// every turn and reflection apart, under <c>jpa_tju1</c>): the scratch read as it is, turned three ways, and
	/// reflected four, the path unturned; with two turn bits the first of <c>0x08</c>, <c>0x10</c>, <c>0x20</c> wins.
	/// </summary>
	/// <remarks><b>Mutation:</b> any case's start or either step changed fails its row; the turn bits read as one field
	/// fails 0x98, 0xb0 or 0xf0.</remarks>
	[TestMethod]
	[DataRow( (ushort)0x80, "5c47848e3dd92809d4703b2c01f42e9d167ffe0a65ee251c1a55c8d55de93c8a" )]
	[DataRow( (ushort)0x88, "e5bde34f54b4492745faaff320cfb3823790c53f79551777a280c403fe83db99" )]
	[DataRow( (ushort)0x90, "0e6a2ec806c356fa20ddf028b0ad3549fb3d8b5038ffc93acfba48c7994d8b90" )]
	[DataRow( (ushort)0xa0, "ee10b0f0ac30e2f4449b65930a8fd1397e9b1e7de2e88f96a1d5514451bb6d27" )]
	[DataRow( (ushort)0xc0, "ef2076036f17678daa64e8978f9f66e030b610148dcd6c4e3ccb7b5d39fa24a5" )]
	[DataRow( (ushort)0xc8, "0b1b3cdb70b63c63dde52dc5db57ba58a568bc5be360f082fa97d4027e0dad92" )]
	[DataRow( (ushort)0xd0, "43b6de9ee68e78c411b80cf779c275bb81c42636b9889bd4777e7de35daa5dcd" )]
	[DataRow( (ushort)0xe0, "5523bab82ead0fafd650c43ab47f46d29b6a277c641d4cd94128c2c5ba5d1329" )]
	[DataRow( (ushort)0x98, "e5bde34f54b4492745faaff320cfb3823790c53f79551777a280c403fe83db99" )]
	[DataRow( (ushort)0xb0, "0e6a2ec806c356fa20ddf028b0ad3549fb3d8b5038ffc93acfba48c7994d8b90" )]
	[DataRow( (ushort)0xf0, "43b6de9ee68e78c411b80cf779c275bb81c42636b9889bd4777e7de35daa5dcd" )]
	public void EachTurnOfTheLastPassIsTheOriginals( ushort word, string expected ) => AtTheOriginalsDepth(
		() => Assert.AreEqual( expected, Hash( Ground( "jungle" ).Tile( 10, 10, new ProceduralGround.Overlay( PathTexture( "jpa_tju1" ), word ) ) ) ) );

	/// <summary>A grey tile returns before the last pass: (28, 0) with a path over it is still all grey.</summary>
	[TestMethod]
	public void NoPathIsLaidOverAGreyTile()
		=> Assert.IsTrue( Ground( "jungle" ).Tile( 28, 0, new ProceduralGround.Overlay( PathTexture( "jpa_tju1" ), 0x80 ) )
			.All( b => b == ProceduralGround.Grey ) );

	/// <summary>
	/// The word <c>FUN_0046df50</c> writes for a path tile: the diagonal kept, <c>0x0001</c>, the mirror and the turn
	/// cleared, <c>0x0180</c> set, then 90 <c>0x20</c>, 180 <c>0x10</c>, 270 <c>0x08</c> - the running original's 78 path
	/// cells read <c>0x82</c>, <c>0xa2</c>, <c>0x92</c> and <c>0x8a</c> once the compositor has cleared <c>0x0100</c>.
	/// </summary>
	/// <remarks><b>Mutation:</b> 90 and 270 swapped fails the second and fourth; the mirror kept gives 0x1c2 for 0x42;
	/// 0x0001 kept gives 0x193 for 0x7b.</remarks>
	[TestMethod]
	public void APathWritesItsWordAsTheOriginal()
	{
		Assert.AreEqual( (ushort)0x182, ParkGround.PathWord( 0x0002, 0 ) );
		Assert.AreEqual( (ushort)0x1a2, ParkGround.PathWord( 0x0042, 90 ) );
		Assert.AreEqual( (ushort)0x192, ParkGround.PathWord( 0x007b, 180 ) );
		Assert.AreEqual( (ushort)0x18c, ParkGround.PathWord( 0x0004, 270 ) );
		Assert.AreEqual( (ushort)0x582, ParkGround.PathWord( 0x0402, 45 ) );
	}

	/// <summary>
	/// The texels as the original's surface holds them at its depth: the file's last row first, each channel's top four bits.
	/// </summary>
	/// <remarks><b>Mutation:</b> the rows kept top down puts 0x10 first; rounding to the nearest nibble gives 0xb0 for
	/// 0xaf.</remarks>
	[TestMethod]
	public void APathTextureIsCutToFourBitsAndReadBottomUp() => AtTheOriginalsDepth( () =>
	{
		var texture = ProceduralGround.PathTexture.FromRgba( [0x10, 0x20, 0x30, 0x40, 0xaf, 0x0f, 0xff, 0x88], 1, 2 );

		CollectionAssert.AreEqual( new byte[] { 0xa0, 0x00, 0xf0, 0x80, 0x10, 0x20, 0x30, 0x40 }, texture.Texels );
	} );

	/// <summary>Eight bits a channel, OpenTPW's default, keeps the texels whole (the original's 8:8:8:8 read under <c>TEXTURE32</c> 1), rows still bottom up.</summary>
	/// <remarks><b>Mutation:</b> the mask taken whatever the mode gives 0xa0 first.</remarks>
	[TestMethod]
	public void APathTextureIsKeptWholeAtEightBits()
	{
		var was = Texture.KeepsEightBits;

		try
		{
			Texture.KeepsEightBits = true;

			var texture = ProceduralGround.PathTexture.FromRgba( [0x10, 0x20, 0x30, 0x40, 0xaf, 0x0f, 0xff, 0x88], 1, 2 );

			CollectionAssert.AreEqual( new byte[] { 0xaf, 0x0f, 0xff, 0x88, 0x10, 0x20, 0x30, 0x40 }, texture.Texels );
		}
		finally
		{
			Texture.KeepsEightBits = was;
		}
	}

	/// <summary>
	/// A tile is laid again only where its path laid over changes: in a box of 4 x 5 jungle tiles, a path put on (2, 3)
	/// lays one tile, which is then the tile with the path; the same path again lays none; taken off, one, and the
	/// pixels are the first lay's again.
	/// </summary>
	/// <remarks><b>Mutation:</b> the new overlay not kept lays one again on the second call; every tile laid again
	/// gives 20; the tile made without its overlay leaves the pixels unchanged after the first.</remarks>
	[TestMethod]
	public void ATileIsLaidAgainOnlyWhereItsPathChanges()
	{
		var ground = Ground( "jungle" );
		var overlays = new ProceduralGround.Overlay?[20];
		var pixels = GroundTiles.Lay( ground, 4, 5, overlays, out _ );
		var first = pixels.ToArray();
		var path = new ProceduralGround.Overlay( PathTexture( "jpa_tju1" ), 0x88 );
		Func<int, int, ProceduralGround.Overlay?> on = ( x, y ) => (x, y) == (2, 3) ? path : null;

		Assert.AreEqual( 1, GroundTiles.Relay( ground, pixels, overlays, 4, 5, on ) );
		var tile = ground.Tile( 2, 3, path );
		CollectionAssert.AreNotEqual( ground.Tile( 2, 3 ), tile );

		// Tile (2, 3)'s row k stands on the texture's row 80 - 1 - (48 + k), from column 32 (TheTilesAreLaidBottomUp).
		for ( var k = 0; k < 16; ++k )
		{
			for ( var j = 0; j < 16; ++j )
			{
				var at = ((((31 - k) * 64) + 32 + j) * 4);
				var from = ((k * 16) + j) * 3;
				CollectionAssert.AreEqual( tile[from..(from + 3)], pixels[at..(at + 3)], $"row {k}, column {j}" );
			}
		}

		Assert.AreEqual( 0, GroundTiles.Relay( ground, pixels, overlays, 4, 5, on ) );
		Assert.AreEqual( 1, GroundTiles.Relay( ground, pixels, overlays, 4, 5, ( _, _ ) => null ) );
		CollectionAssert.AreEqual( first, pixels );
	}
}
