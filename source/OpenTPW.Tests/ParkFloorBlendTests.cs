using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.Files;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The wiring round a built thing's floor blend (<c>docs/exe/park-engine.md</c>, "A built thing's floor is blended into the
/// ground's tiles"), the parts that stand without a graphics device: which models blend, the tile the ground hands on, the
/// box it covers, the queue's turn, and the textures a thing lets go of. <see cref="ProceduralFloorTests"/> holds the tables
/// and the stamp.
/// </summary>
[TestClass]
public class ParkFloorBlendTests
{
	private BaseFileSystem? was;

	[TestInitialize]
	public void MountTheGame()
	{
		was = FileSystemOrNull;
		FileSystem = GameData.Required();
	}

	[TestCleanup]
	public void PutItBack() => FileSystemOrNull = was;

	/// <summary>
	/// <b>The tile is handed on bottom up</b>: its row k lands at the texture's row n - 1 - k, opaque, each channel kept.
	/// </summary>
	/// <remarks><b>Mutation:</b> the rows written top down (<c>k</c> for <c>n - 1 - k</c>) fails here.</remarks>
	[TestMethod]
	public void TheTileIsHandedOnBottomUp()
	{
		// Three 2 x 2 rows' worth: each pixel's red is its row, green its column, blue 7.
		byte[] tile = [0, 0, 7, 0, 1, 7, 1, 0, 7, 1, 1, 7];

		var pixels = ParkGround.TextureRows( tile, 2 );

		CollectionAssert.AreEqual( new byte[]
		{
			1, 0, 7, 255, 1, 1, 7, 255, // the tile's row 1, now the texture's row 0
			0, 0, 7, 255, 0, 1, 7, 255,
		}, pixels );
	}

	/// <summary>
	/// <b>A cell is covered only inside the box, its edges too</b>, and the ground hands nothing for one outside it (or with the
	/// compositor off: no tiles).
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>Covers</c> comparing a bound exclusively, or one axis only, fails here.</remarks>
	[TestMethod]
	public void ACellIsBlendedOnlyInsideTheTilesBox()
	{
		const BindingFlags any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

		var tiles = (GroundTiles)RuntimeHelpers.GetUninitializedObject( typeof( GroundTiles ) );

		foreach ( var (name, value) in new[] { ("X0", 10), ("Y0", 20), ("X1", 12), ("Y1", 22) } )
			typeof( GroundTiles ).GetField( $"<{name}>k__BackingField", any )!.SetValue( tiles, value );

		Assert.IsTrue( tiles.Covers( 10, 20 ) && tiles.Covers( 12, 22 ) && tiles.Covers( 11, 21 ), "the corners and the middle" );
		Assert.IsFalse( tiles.Covers( 9, 21 ), "left of it" );
		Assert.IsFalse( tiles.Covers( 13, 21 ), "right of it" );
		Assert.IsFalse( tiles.Covers( 11, 19 ), "above it" );
		Assert.IsFalse( tiles.Covers( 11, 23 ), "below it" );

		var ground = (ParkGround)RuntimeHelpers.GetUninitializedObject( typeof( ParkGround ) );

		Assert.IsNull( ground.BlendFloor( 11, 21, 0x281, "nowhere.wct" ), "no tiles: the compositor is off" );

		typeof( ParkGround ).GetField( "_tiles", any )!.SetValue( ground, tiles );

		Assert.IsNull( ground.BlendFloor( 13, 21, 0x281, "nowhere.wct" ), "outside the box" );
	}

	/// <summary>
	/// <b>Only a model exported with a procedural base blends</b> (header <c>0x8</c>): the rock and the tunnel do, from their
	/// floor mesh; the gates have a floor mesh the table rule finds and no header bit, so blend none.
	/// </summary>
	/// <remarks><b>Mutation:</b> the header test dropped fails on the gates.</remarks>
	[TestMethod]
	public void OnlyAModelWithAProceduralBaseBlends()
	{
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );

		foreach ( var (id, mesh) in new[] { (1427, 0), (1501, 0), (1601, -1) } )
		{
			Assert.IsTrue( catalogue.TryGet( id, out var item ), $"item {id}" );
			var model = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );

			Assert.AreEqual( mesh, ParkObjects.FloorMeshOf( model, item.Shape! ), item.Stem );
		}

		Assert.IsTrue( catalogue.TryGet( 1601, out var gates ) );
		Assert.IsNotNull( ProceduralBase.FloorMesh( new ModelFile( $"{gates.Directory}/{gates.Stem}.MD2" ), gates.Shape,
			ProceduralBase.ParkGrid, out _ ), "the gates do have a floor mesh, so the header alone refuses them" );
	}

	/// <summary>
	/// <b>The queue's four cells are stamped at the turn the model maker is given</b>, 360 less the saved angle
	/// (<c>FUN_005229e0</c>): the stock park's bend, two straights and end at (52..49, 22) blend with the words the original's
	/// blend was handed (<c>q649/orig/a-load.log</c>, hits 11 to 14: <c>2c3 2d3 2d3 28b</c>) less the cell's own <c>0x6</c>.
	/// </summary>
	/// <remarks><b>Mutation:</b> <see cref="ParkQueues.TurnOf"/> returning the cell's own angle swaps the bend's 270 and
	/// the straights' and end's 90, and fails here.</remarks>
	[TestMethod]
	public void TheQueuesFourCellsAreStampedAtTheirTurn()
	{
		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		var cells = new (int X, int Y, string Piece, int Word)[]
		{
			(52, 22, "quebnd2", 0x2c3), (51, 22, "questra", 0x2d3), (50, 22, "questra", 0x2d3), (49, 22, "queend", 0x28b),
		};

		foreach ( var (x, y, piece, word) in cells )
		{
			var cell = world.CellAt( x, y );
			Assert.IsTrue( ParkQueues.IsQueue( cell ), $"({x},{y}) is a queue cell" );

			var model = new ModelFile( $"levels/jungle/queue/{piece}.MD2" );
			var table = ProceduralFloor.Table( model, model.RootNode, new ItemShape( 1, 1, [[0]] ), 1, 1, true, new ProceduralBase.Grid( 0f, 0f, 10f, 10f ) );

			Assert.IsNotNull( table[0, 0], $"{piece} enters its one cell" );
			Assert.AreEqual( $"{word & ~0x6:x3}", $"{ProceduralFloor.Stamped( table[0, 0]!.Value.Flags, ParkQueues.TurnOf( cell ), 0 ):x3}", $"({x},{y}) {piece}, saved at {cell.TileAngle}" );
		}
	}

	/// <summary>
	/// <b>Selling a thing lets go of the floor textures it blended</b>, and only its own.
	/// </summary>
	/// <remarks><b>Mutation:</b> the let-go loop in <see cref="ParkObjects.Remove"/> removed fails here.</remarks>
	[TestMethod]
	public void SellingAThingLetsGoOfItsBlendedFloor()
	{
		const BindingFlags any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

		Log ??= new();

		var objects = new ParkObjects( "jungle", null, null );

		var model = (LobbyModel)RuntimeHelpers.GetUninitializedObject( typeof( LobbyModel ) );
		typeof( LobbyModel ).GetField( "<Entities>k__BackingField", any )!.SetValue( model, Array.Empty<ModelEntity>() );

		var mine = new List<Texture> { Fake(), Fake() };
		var others = Fake();

		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		Assert.IsTrue( catalogue.TryGet( 1100, out var item ) );
		objects.Stand( 7, model, RideAnimations.Load( item.Directory, item.Stem, FileSystem, item.AnimationChannels ) );

		var standing = ((IDictionary)typeof( ParkObjects ).GetField( "_standing", any )!.GetValue( objects )!)[7]!;
		standing.GetType().GetProperty( "Blended" )!.SetValue( standing, mine );

		var render = Global.Render;
		Global.Render = (Renderer)RuntimeHelpers.GetUninitializedObject( typeof( Renderer ) );
		typeof( Renderer ).GetField( "_deletionQueue", any )!.SetValue( Global.Render, new List<Action>() );

		try
		{
			Assert.IsTrue( Asset.All.Contains( mine[0] ) && Asset.All.Contains( mine[1] ) && Asset.All.Contains( others ) );

			Assert.IsTrue( objects.Remove( 7 ) );

			Assert.IsFalse( Asset.All.Contains( mine[0] ) || Asset.All.Contains( mine[1] ), "its own textures are let go" );
			Assert.IsTrue( Asset.All.Contains( others ), "another thing's is not" );
		}
		finally
		{
			Global.Render = render;
			Asset.All.Remove( others );
			Asset.All.Remove( mine[0] );
			Asset.All.Remove( mine[1] );
		}

		Texture Fake()
		{
			var texture = (Texture)RuntimeHelpers.GetUninitializedObject( typeof( Texture ) );
			Asset.All.Add( texture );
			return texture;
		}
	}
}
