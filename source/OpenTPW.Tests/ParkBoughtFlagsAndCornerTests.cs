using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Buffers.Binary;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A made object's <c>mFlags</c> and <c>mTopLeft</c>, each from its item's description (<c>docs/exe/saves.md</c>,
/// "OpenTPW's writer, a made object's flags and corner").
/// </summary>
[TestClass]
public class ParkBoughtFlagsAndCornerTests
{
	private const int CameraItem = 1413, HollowRock = 1427, BellyBounce = 1100;

	private static ParkWorld Shipped( BaseFileSystem data )
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var reader = new SaveReader( stream );

		return new ParkWorld( reader.ReadFile(), reader.Preamble );
	}

	/// <summary>Each of the nine keys sets its own bit and no other, and a key at nought sets none.</summary>
	[TestMethod]
	[DataRow( "UsageInfo.ProvidesRelief", 0x1 )]
	[DataRow( "UsageInfo.ChillsYouOut", 0x2 )]
	[DataRow( "Info.IsChoosable", 0x4 )]
	[DataRow( "Info.HasQueue", 0x8 )]
	[DataRow( "UsageInfo.ProvidesSecurity", 0x10 )]
	[DataRow( "UsageInfo.RideHandlesSprite", 0x20 )]
	[DataRow( "UsageInfo.HoldsLitter", 0x40 )]
	[DataRow( "UsageInfo.IsFireworks", 0x80 )]
	[DataRow( "Info.RunsContinuously", 0x100 )]
	public void AKeyIsABitOfAnObjectsFlags( string key, int bit )
	{
		Assert.AreEqual( bit, new ItemDescriptionFile( $"{key} 1\n" ).ObjectFlags );
		Assert.AreEqual( 0, new ItemDescriptionFile( $"{key} 0\n" ).ObjectFlags );
	}

	/// <summary>A key the item's own file leaves out is its category's, and one it names is its own, set or cleared.</summary>
	[TestMethod]
	public void AFlagKeyLeftOutIsTheCategorys()
	{
		var category = new ItemDescriptionFile( "UsageInfo.HoldsLitter 1\nUsageInfo.ProvidesSecurity 1\nInfo.MapOffsetX 3\nInfo.MapOffsetY 4\n" );

		Assert.AreEqual( 0x50, new ItemDescriptionFile( "Info.Id 9\n", category ).ObjectFlags );
		Assert.AreEqual( 0x30, new ItemDescriptionFile( "UsageInfo.HoldsLitter 0\nUsageInfo.RideHandlesSprite 1\n", category ).ObjectFlags );
		Assert.AreEqual( (3, 4), new ItemDescriptionFile( "Info.Id 9\n", category ).MapOffset );
		Assert.AreEqual( (3, 7), new ItemDescriptionFile( "Info.MapOffsetY 7\n", category ).MapOffset );
		Assert.AreEqual( (5, 4), new ItemDescriptionFile( "Info.MapOffsetX 5\n", category ).MapOffset );
		Assert.AreEqual( (0, 0), new ItemDescriptionFile( "Info.Id 9\n" ).MapOffset );
	}

	/// <summary>The catalogue hands an item its flags and both halves of its offset, the folder's keys under its own.</summary>
	[TestMethod]
	public void TheCatalogueCarriesAnItemsFlagsAndOffset()
	{
		var root = Path.Combine( Path.GetTempPath(), "opentpw-flags-" + Guid.NewGuid().ToString( "N" ) );

		try
		{
			var folder = Path.Combine( root, "levels", "test", "features" );

			Directory.CreateDirectory( Path.Combine( folder, "post" ) );
			File.WriteAllText( Path.Combine( folder, "features.sam" ), "UsageInfo.HoldsLitter 1\n" );
			File.WriteAllText( Path.Combine( folder, "post", "post.sam" ),
				"Info.Id 1490\nInfo.Name \"Post\"\nUsageInfo.ProvidesSecurity 1\nInfo.MapOffsetX 2\nInfo.MapOffsetY 5\n" );

			Log ??= new();

			Assert.IsTrue( new ParkItemCatalogue( "test", new BaseFileSystem( root ) ).TryGet( 1490, out var post ) );
			Assert.AreEqual( (0x50, 2, 5), (post.ObjectFlags, post.MapOffsetX, post.MapOffsetY) );
		}
		finally
		{
			if ( Directory.Exists( root ) )
				Directory.Delete( root, recursive: true );
		}
	}

	/// <summary>
	/// The corner is the anchor's id plus the offset turned by the angle and a half turn more: an offset of one row
	/// lands a row before the anchor unturned, a column before it a quarter on, and so round.
	/// </summary>
	[TestMethod]
	[DataRow( 0, 0, 1, -128 )]
	[DataRow( 90, 0, 1, -1 )]
	[DataRow( 180, 0, 1, 128 )]
	[DataRow( 270, 0, 1, 1 )]
	[DataRow( 0, 2, 0, -2 )]
	[DataRow( 90, 2, 0, 256 )]
	[DataRow( 180, 2, 0, 2 )]
	[DataRow( 270, 2, 0, -256 )]
	[DataRow( 270, 0, 0, 0 )]
	public void TheCornerIsTheOffsetTurnedAHalfTurnPastTheAngle( int angle, int offsetX, int offsetY, int expected )
	{
		var item = new ParkItemCatalogue.Item( 9, "x", "", "", 1, 1, null, MapOffsetX: offsetX, MapOffsetY: offsetY );

		Assert.AreEqual( MapStep.CellId( 20, 30 ) + expected, ParkBuilding.TopLeftFor( item, 20, 30, angle ) );
	}

	/// <summary>
	/// The items' own files give the bits the shipped park's objects carry: a camera security alone, a litter bin its
	/// litter bit, the Staff Room the rest bit, and the Huge Hollow Rock an offset of one row.
	/// </summary>
	[TestMethod]
	public void TheJunglesItemsCarryTheirKeys()
	{
		var catalogue = new ParkItemCatalogue( "jungle", GameData.Required() );

		Assert.IsTrue( catalogue.TryGet( CameraItem, out var camera ) );
		Assert.IsTrue( catalogue.TryGet( HollowRock, out var rock ) );
		Assert.IsTrue( catalogue.TryGet( BellyBounce, out var bounce ) );

		Assert.AreEqual( (0x10, 0, 0), (camera.ObjectFlags, camera.MapOffsetX, camera.MapOffsetY) );
		Assert.AreEqual( (0, 1), (rock.MapOffsetX, rock.MapOffsetY) );
		Assert.AreEqual( 0x2c, bounce.ObjectFlags & 0x2c, "a ride a guest may choose, with a queue, that keeps its riders' sprites" );
		Assert.AreEqual( 1, catalogue.All.Count( item => (item.ObjectFlags & 0x40) != 0 ), "one item holds litter" );
		Assert.AreEqual( 1, catalogue.All.Count( item => (item.ObjectFlags & 0x2) != 0 ), "one is a rest area" );
		Assert.AreEqual( "1427 5x5rck,1501 mammtunn", string.Join( ",", catalogue.All.Where( item => item.MapOffsetX != 0 || item.MapOffsetY != 0 ).Select( item => $"{item.Id} {item.Stem}" ) ), "the rock, and one upgrade, have an offset" );
	}

	/// <summary>
	/// <b>An object's model stands on its corner</b> (<c>ParkObjects.ModelCell</c>; <c>FUN_004dd500</c> makes the model on
	/// <c>mTopLeft</c>, <c>0x004dd5d3</c>): the Huge Hollow Rock anchored at (19, 27), as in Alexah's Full Simulation save,
	/// stands a row before it unturned (19, 26), where the file's corner is, a column before it at 90 degrees (18, 27), a
	/// row after at 180 (19, 28) and a column after at 270 (20, 27); a camera, with no offset, on its anchor. On the anchor
	/// the rock would stand a cell off at every angle.
	/// </summary>
	/// <remarks>
	/// Its <c>.hmp</c> is laid from the model's own place and height: halfway between the lowest and highest ground under its
	/// footprint (<see cref="APlacedObjectStandsAtItsFootprintsHeight"/>), where the ground under the model's cell would
	/// give -7, -6, -5 and -6. <b>Mutations:</b> the model stood on the anchor; the base on the ground under the model's cell.
	/// </remarks>
	[TestMethod]
	[DataRow( 0, 19, 26, -4.5f )]
	[DataRow( 90, 18, 27, -7.5f )]
	[DataRow( 180, 19, 28, -6.5f )]
	[DataRow( 270, 20, 27, -3.5f )]
	public void AnObjectsModelStandsOnItsCorner( int angle, int x, int y, float height )
	{
		var catalogue = new ParkItemCatalogue( "jungle", GameData.Required() );

		Assert.IsTrue( catalogue.TryGet( HollowRock, out var rock ) );
		Assert.IsTrue( catalogue.TryGet( CameraItem, out var camera ) );

		Assert.AreEqual( (x, y), ParkObjects.ModelCell( rock, 19, 27, angle ) );
		Assert.AreEqual( (19, 27), ParkObjects.ModelCell( camera, 19, 27, angle ) );

		var placed = ParkBuilding.Constructed( rock, 900, 19, 27, angle, 1, 1, default );

		Assert.AreEqual( ParkObjects.OriginFor( x, y, angle ), ParkObjects.ModelOriginFor( rock, placed ) );
		Assert.AreEqual( placed.TopLeft, MapStep.CellId( x, y ), "the cell the file's corner names" );

		// Its .hmp is laid from the same place, on the ground under that cell.
		using var ground = GameData.Required().OpenRead( "levels/jungle/terrain/base.MD2" )!;
		var field = new HeightfieldFile( ground );
		var (origin, under) = ParkItemHeights.BaseOf( rock, placed, field );

		Assert.AreEqual( ParkObjects.OriginFor( x, y, angle ).GetSystemVector3() with { Z = 0f }, origin with { Z = 0f } );
		Assert.AreEqual( height, under, 0.001f );
	}

	/// <summary>
	/// <b>A placed object stands halfway between the lowest and the highest ground under its footprint</b>
	/// (<c>ParkObjects.BaseHeight</c>; <c>FUN_00466d70</c>, <c>docs/exe/park-engine.md</c>, "Where a placed object's model
	/// stands"), so its nodes, and the effects its script starts on them, stand where the original's do: each row is an
	/// emitter of Alexah's Full Simulation jungle autosave, its place in whole units as that file holds it (the emitter's
	/// words over 64; all 55 node-placed emitters of the thirteen park files of the original's read here agree). On the ground
	/// under the model's cell the rock's would stand at 9, the Sun God's at -1, the drinks shop's at 2 and the hyenas' at 0.
	/// </summary>
	/// <remarks><b>Mutation:</b> the model on the ground under its cell.</remarks>
	[TestMethod]
	[DataRow( HollowRock, 19, 27, 0, 1, 216, 12, 291, DisplayName = "the Huge Hollow Rock's water, node 1" )]
	[DataRow( HollowRock, 19, 27, 0, 2, 211, 12, 281, DisplayName = "the Huge Hollow Rock's water, node 2" )]
	[DataRow( 1106, 15, 26, 270, 1, 150, 0, 269, DisplayName = "the Sun God, turned three quarters" )]
	[DataRow( 1203, 32, 29, 0, 1, 330, 3, 302, DisplayName = "a drinks shop on a slope" )]
	[DataRow( 1304, 30, 27, 180, 5, 295, -1, 271, DisplayName = "the Laughing Hyenas, turned a half" )]
	[DataRow( 1411, 20, 24, 180, 1, 199, 11, 240, DisplayName = "a staff room, turned a half" )]
	public void APlacedObjectStandsAtItsFootprintsHeight( int id, int x, int y, int angle, int node, int fileX, int fileHeight, int fileZ )
	{
		var data = GameData.Required();
		var catalogue = new ParkItemCatalogue( "jungle", data );

		using var ground = data.OpenReadOrNull( "levels/jungle/terrain/base.MD2" )!;
		var field = new HeightfieldFile( ground );

		Assert.IsTrue( catalogue.TryGet( id, out var item ) );

		var placed = ParkBuilding.Constructed( item, 900, x, y, angle, 1, 1, default );
		var nodes = RideNodes.Load( item.Directory, item.Stem, data, item.DoHeadProcessing, [] )!;

		nodes.Place( ParkObjects.ModelOriginFor( item, placed, field ), angle );
		nodes.Find( node, RideNodes.ParticleSpace, out var at );

		Assert.AreEqual( (fileX, fileHeight, fileZ), ((int)at.X, (int)at.Y, (int)at.Z) );
	}

	/// <summary>
	/// <b>The ground under a footprint's corners spreads where the land slopes</b>, and the plate stands midway
	/// (<see cref="ParkObjects.FootprintHeights"/>): half the spread is how far the plate's rim stands off the terrain corner beside it,
	/// the step the original's conform closes (<c>docs/exe/park-engine.md</c>, "Where a placed object's model stands") and this game
	/// leaves open, a face the frame's clear colour shows through (Q804). Alexah's Sun God on the slope (<c>rim</c>: ground -7 to -2,
	/// stands -4.5) and the stock park's Belly Bounce on level ground, which draws no gap.
	/// </summary>
	/// <remarks><b>Mutation:</b> the last corner's height for the highest; the shape's kind test turned; the turn ignored; the base at the low end.</remarks>
	[TestMethod]
	[DataRow( 1106, 15, 26, 270, -7f, -2f, DisplayName = "the Sun God on a slope: a step of two and a half" )]
	[DataRow( 1100, 51, 23, 0, 0f, 0f, DisplayName = "the stock Belly Bounce on level ground: none" )]
	[DataRow( 1113, 30, 19, 0, -7f, -2f, DisplayName = "the Tom Tom Twister, unturned: two and a half" )]
	[DataRow( 1501, 17, 21, 90, -9f, -6f, DisplayName = "the Dino Karts Tunnel, turned a quarter: one and a half" )]
	[DataRow( 1304, 30, 27, 180, -8f, -5f, DisplayName = "the Laughing Hyenas, turned a half: one and a half" )]
	[DataRow( 1411, 20, 24, 180, -10f, -8f, DisplayName = "a staff room, turned a half: one" )]
	public void AFootprintsGroundSpreadIsHowFarItsRimStandsOffTheTerrain( int id, int x, int y, int angle, float low, float high )
	{
		var data = GameData.Required();
		var catalogue = new ParkItemCatalogue( "jungle", data );

		using var ground = data.OpenReadOrNull( "levels/jungle/terrain/base.MD2" )!;
		var field = new HeightfieldFile( ground );

		Assert.IsTrue( catalogue.TryGet( id, out var item ) );

		var origin = ParkObjects.ModelOriginFor( item, ParkBuilding.Constructed( item, 900, x, y, angle, 1, 1, default ), field );
		var spread = ParkObjects.FootprintHeights( item.Shape, origin, angle, field );

		Assert.IsNotNull( spread );
		Assert.AreEqual( low, spread.Value.Low, 0.001f );
		Assert.AreEqual( high, spread.Value.High, 0.001f );
		Assert.AreEqual( (low + high) * 0.5f, origin.Z, 0.001f );
	}

	/// <summary>
	/// A footprint's corner a hair short of a cell line is read as on it (<c>FUN_00454050</c> adds a tenth of a cell before it cuts), so a
	/// float's error in a turned corner does not read the cell beyond: the Sun God stood a thousandth of a unit short of its cell has
	/// the spread it has stood on it.
	/// </summary>
	/// <remarks><b>Mutation:</b> the corner cut without its tenth.</remarks>
	[TestMethod]
	public void AFootprintCornerAHairShortOfALineReadsTheCellOnIt()
	{
		var data = GameData.Required();
		var catalogue = new ParkItemCatalogue( "jungle", data );

		using var ground = data.OpenReadOrNull( "levels/jungle/terrain/base.MD2" )!;
		var field = new HeightfieldFile( ground );

		Assert.IsTrue( catalogue.TryGet( 1106, out var item ) );

		var origin = ParkObjects.ModelOriginFor( item, ParkBuilding.Constructed( item, 900, 15, 26, 0, 1, 1, default ), field );
		var exact = ParkObjects.FootprintHeights( item.Shape, origin, 0, field );
		var short_ = ParkObjects.FootprintHeights( item.Shape, origin with { X = origin.X - 0.001f, Y = origin.Y - 0.001f }, 0, field );

		Assert.IsNotNull( exact );
		Assert.AreNotEqual( exact.Value.Low, exact.Value.High );
		Assert.AreEqual( exact, short_ );
	}

	/// <summary>Every placed object of the shipped park holds the corner its item and its angle give.</summary>
	[TestMethod]
	public void EveryPlacedObjectsCornerIsItsItems()
	{
		var data = GameData.Required();
		var catalogue = new ParkItemCatalogue( "jungle", data );
		var compared = 0;

		foreach ( var thing in Shipped( data ).Objects.Where( thing => thing.IsPlaced ) )
		{
			Assert.IsTrue( catalogue.TryGet( thing.CatalogueId, out var item ), $"thing {thing.ThingId}" );
			Assert.AreEqual( thing.TopLeft, ParkBuilding.TopLeftFor( item, thing.RawX >> 8, thing.RawY >> 8, thing.Angle ), $"thing {thing.ThingId}" );
			++compared;
		}

		Assert.AreEqual( 11, compared );
	}

	/// <summary>
	/// A camera built is flagged for security and a Huge Hollow Rock stands a row past its corner, and each goes
	/// into its record: <c>mFlags</c> at 58 and <c>mTopLeft</c> at 204.
	/// </summary>
	[TestMethod]
	public void ABoughtThingsRecordHoldsItsFlagsAndItsCorner()
	{
		var catalogue = new ParkItemCatalogue( "jungle", GameData.Required() );

		Assert.IsTrue( catalogue.TryGet( CameraItem, out var cameraItem ) );
		Assert.IsTrue( catalogue.TryGet( HollowRock, out var rockItem ) );

		var camera = ParkBuilding.Constructed( cameraItem, 43, 46, 27, 0, 1, 1, default );
		var rock = ParkBuilding.Constructed( rockItem, 44, 50, 26, 90, 1, 1, default );

		Assert.AreEqual( (0x10, MapStep.CellId( 46, 27 )), ((int)camera.Flags, (int)camera.TopLeft) );
		Assert.AreEqual( 3, camera.State, "a thing nobody is offered starts on state 3" );

		Assert.IsTrue( catalogue.TryGet( BellyBounce, out var bounceItem ) );
		Assert.AreEqual( 0, ParkBuilding.Constructed( bounceItem, 45, 40, 22, 0, 1, 1, default ).State, "and one a guest may choose on nought" );
		Assert.AreEqual( MapStep.CellId( 49, 26 ), rock.TopLeft, "a quarter turned, the corner is a column before the anchor" );

		var cameraRecord = ParkWorld.MadeObjectRecord( new ParkWorld.MadeObject( camera, "Security Camera", "" ), 7 );
		var rockRecord = ParkWorld.MadeObjectRecord( new ParkWorld.MadeObject( rock, "Huge Hollow Rock", "" ), 8 );

		Assert.AreEqual( (0x10, MapStep.CellId( 46, 27 )), (Short( cameraRecord, 58 ), Short( cameraRecord, 204 )) );
		Assert.AreEqual( ((int)rockItem.ObjectFlags, MapStep.CellId( 49, 26 )), (Short( rockRecord, 58 ), Short( rockRecord, 204 )) );
	}

	private static int Short( byte[] record, int at ) => BinaryPrimitives.ReadUInt16LittleEndian( record.AsSpan( at ) );
}
