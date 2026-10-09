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
		Assert.AreEqual( 1, catalogue.All.Count( item => item.MapOffsetX != 0 || item.MapOffsetY != 0 ), "one has an offset" );
	}

	/// <summary>A toilet, a thing that provides security and fireworks stamp a region effect as they are built, and nothing else does.</summary>
	[TestMethod]
	[DataRow( 0x1, true )]
	[DataRow( 0x10, true )]
	[DataRow( 0x80, true )]
	[DataRow( 0x25, true )]
	[DataRow( 0x40, false )]
	[DataRow( 0x22, false )]
	[DataRow( 0x12c, false )]
	[DataRow( 0, false )]
	public void AToiletSecurityAndFireworksStampARegionEffect( int flags, bool stamps )
		=> Assert.AreEqual( stamps, ParkBuilding.StampsRegionEffect( new ParkItemCatalogue.Item( 9, "x", "", "", 1, 1, null, ObjectFlags: flags ) ) );

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
