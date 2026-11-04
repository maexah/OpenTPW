using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The footprint stamp's write to a cell's live word drops the split flag <c>0x800</c> (<c>FUN_00463060</c>, <c>0x0046352a</c>;
/// <c>docs/exe/park-engine.md</c>, "The footprint stamp clears <c>0x800</c>"), and a cell put back to ground takes the
/// load's flag again (<c>FUN_0046df50</c>, <c>0x0046e031</c>..<c>0x0046e06f</c>). The tests read Lost Kingdom's own heightfield
/// and are skipped where there is no installation.
/// </summary>
[TestClass]
public class ParkFootprintSplitTests
{
	private const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
	private const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

	private BaseFileSystem? was;
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame()
	{
		was = FileSystemOrNull;
		data = GameData.Required();
		FileSystem = data;
	}

	[TestCleanup]
	public void PutItBack() => FileSystemOrNull = was;

	private HeightfieldFile Landscape()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/terrain/base.MD2" ) );
		return new HeightfieldFile( stream );
	}

	/// <summary>A ground that is only a heightfield and a tile size, as the stamp needs it (<c>0</c>: the compositor off).</summary>
	private static ParkGround GroundOf( HeightfieldFile field, int tileSize )
	{
		var ground = (ParkGround)RuntimeHelpers.GetUninitializedObject( typeof( ParkGround ) );
		typeof( ParkGround ).GetField( "<Heightfield>k__BackingField", Any )!.SetValue( ground, field );
		typeof( ParkGround ).GetField( "_tileSize", Any )!.SetValue( ground, tileSize );
		return ground;
	}

	private static void SetCurrent( ParkGround? ground )
		=> typeof( ParkGround ).GetProperty( nameof( ParkGround.Current ), AnyStatic )!.SetValue( null, ground );

	/// <summary>The first cell the load flagged <c>0x800</c>, and the first it did not, in reading order.</summary>
	private static ((int X, int Y) Bent, (int X, int Y) Flat) TwoCells( HeightfieldFile field )
	{
		(int, int)? bent = null, flat = null;

		for ( var y = 0; y < field.CellsY; ++y )
		{
			for ( var x = 0; x < field.CellsX; ++x )
			{
				if ( (field.FlagsAt( x, y ) & 0x800) != 0 ) bent ??= (x, y);
				else flat ??= (x, y);
			}
		}

		return (bent!.Value, flat!.Value);
	}

	/// <summary>
	/// <b>The load flags 589 cells of the stock park, and a cleared one stands on the near corner's plane.</b> The flag goes and
	/// the rest of the word stays; the height at the cell's far corner is then the plane's, not the bent cell's; restored, both
	/// are the load's again. A cell the load did not flag does not get the flag from a restore, and a cell off the grid is left.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>ClearSplit</c> clearing the whole word; <c>RestoreSplit</c> setting the flag on every cell;
	/// <c>ClearSplit</c> doing nothing.</remarks>
	[TestMethod]
	public void AClearedCellStandsOnThePlaneAndARestoredOneOnItsBend()
	{
		var field = Landscape();
		var (bent, flat) = TwoCells( field );

		var flagged = Enumerable.Range( 0, field.CellsX * field.CellsY ).Count( i => (field.FlagsAt( i % field.CellsX, i / field.CellsX ) & 0x800) != 0 );
		Assert.AreEqual( 589, flagged, "the cells the load flags (Q823's stock dump)" );

		// A point close to the cell's far corner, where the bent cell and the near corner's plane part most.
		var (worldX, worldY) = ((bent.X + 0.9f) * field.CellSizeX, (bent.Y + 0.9f) * field.CellSizeY);
		var (word, bentHeight) = (field.FlagsAt( bent.X, bent.Y ), field.ScapeHeight( worldX, worldY ));

		Assert.IsFalse( field.IsStamped( bent.X, bent.Y ) );
		field.ClearSplit( bent.X, bent.Y );
		Assert.IsTrue( field.IsStamped( bent.X, bent.Y ), "a stamp holds it" );

		Assert.AreEqual( word & ~0x800, field.FlagsAt( bent.X, bent.Y ), "only the split flag goes" );
		Assert.AreEqual( field.CornerPlaneAt( worldX, worldY ), field.ScapeHeight( worldX, worldY ), 1e-4f, "it stands on the near corner's plane" );
		Assert.AreNotEqual( bentHeight, field.ScapeHeight( worldX, worldY ), 1e-3f, "which is not the bent cell's height" );

		field.RestoreSplit( bent.X, bent.Y );

		Assert.IsFalse( field.IsStamped( bent.X, bent.Y ), "ground again: no stamp holds it" );
		Assert.AreEqual( word, field.FlagsAt( bent.X, bent.Y ) );
		Assert.AreEqual( bentHeight, field.ScapeHeight( worldX, worldY ), 1e-6f );

		var flatWord = field.FlagsAt( flat.X, flat.Y );
		field.RestoreSplit( flat.X, flat.Y );
		Assert.AreEqual( flatWord, field.FlagsAt( flat.X, flat.Y ), "a cell the load did not flag is not flagged by a restore" );

		field.ClearSplit( flat.X, flat.Y );
		Assert.AreEqual( flatWord, field.FlagsAt( flat.X, flat.Y ), "clearing a cell without the flag changes nothing" );

		field.ClearSplit( -1, 0 );
		field.ClearSplit( field.CellsX, 0 );
		field.RestoreSplit( 0, field.CellsY );
	}

	/// <summary>
	/// <b>The stamp runs only where the compositor is on</b> (<c>[0x007a1a8c] &amp; 0x4000</c>, <c>0x00463354</c>): a ground with no tile
	/// size keeps the flag and says it stamped nothing.
	/// </summary>
	/// <remarks><b>Mutation:</b> the tile size test removed fails here.</remarks>
	[TestMethod]
	public void TheStampIsSkippedWithTheCompositorOff()
	{
		var field = Landscape();
		var (bent, _) = TwoCells( field );

		Assert.IsFalse( GroundOf( field, 0 ).StampSplit( bent.X, bent.Y ), "no tiles: no stamp" );
		Assert.AreNotEqual( 0, field.FlagsAt( bent.X, bent.Y ) & 0x800, "the flag stays" );

		Assert.IsTrue( GroundOf( field, 8 ).StampSplit( bent.X, bent.Y ), "tiles: stamped" );
		Assert.AreEqual( 0, field.FlagsAt( bent.X, bent.Y ) & 0x800 );
	}

	/// <summary>
	/// <b>Every table entry is stamped, before its texture is looked for</b> (<c>0x0046352a</c> writes the word, <c>0x004635af</c>
	/// blends): with no tiles to blend into (<see cref="ParkGround.BlendFloor"/> answers null) the entry's cell still loses the flag
	/// and is listed, and nothing is blended. A queue piece enters its one cell whatever its kind.
	/// </summary>
	/// <remarks><b>Mutations:</b> the stamp after the <c>BlendFloor</c> test; the stamp not listing its cell.</remarks>
	[TestMethod]
	public void EveryEntryIsStampedBeforeItsTextureIsLookedFor()
	{
		var field = Landscape();
		var (bent, _) = TwoCells( field );
		var ground = GroundOf( field, 8 );

		var model = new ModelFile( "levels/jungle/queue/questra.MD2" );
		var stamped = new List<(int X, int Y)>();
		var made = new List<Texture>();

		var blended = ProceduralFloor.Blend( model, model.RootNode, null, 1, 1, true, 0, (bent.X, bent.Y, bent.X, bent.Y), ground,
			name => name, made, out var cells, stamped );

		Assert.AreEqual( 0, cells, "no tiles: nothing blended" );
		Assert.AreEqual( 0, blended.Count );
		Assert.AreEqual( 0, field.FlagsAt( bent.X, bent.Y ) & 0x800, "but the cell is stamped" );
		CollectionAssert.AreEqual( new[] { bent }, stamped );
	}

	/// <summary>
	/// <b>The stamp is made for each of Lost Kingdom's items that blend, as many cells as the table has entries</b>, not the cells
	/// that found a frame's texture.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> the stamp after the frame-name test survives, because no entry of Lost Kingdom's names no frame (dead by
	/// CONTENT, counted below): the order is the code's, from <c>0x0046347a</c> to <c>0x0046353b</c> having no name to look at.
	/// </remarks>
	[TestMethod]
	public void EveryTableEntryOfEveryItemIsStamped()
	{
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		var checkedItems = 0;
		var unnamed = 0;

		foreach ( var item in catalogue.All.Where( i => i.Shape != null ) )
		{
			var model = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );

			if ( ParkObjects.FloorMeshOf( model, item.Shape! ) is not (>= 0 and var mesh) )
				continue;

			var everyCell = (model.HeaderFlags & 0x80) != 0;
			var table = ProceduralFloor.Table( model, mesh, item.Shape, item.Shape!.Width, item.Shape.Depth, everyCell, ProceduralBase.ParkGrid );
			var entries = table.Cast<ProceduralFloor.Cell?>().Count( c => c != null );
			var named = model.Meshes[mesh].Materials.Where( m => m.FrameOffset != 0 ).Select( m => m.FrameOffset ).ToHashSet();
			unnamed += table.Cast<ProceduralFloor.Cell?>().Count( c => c is { } cell && !named.Contains( cell.FrameOffset ) );

			var ground = GroundOf( Landscape(), 8 );
			var stamped = new List<(int X, int Y)>();

			ProceduralFloor.Blend( model, mesh, item.Shape, item.Shape.Width, item.Shape.Depth, everyCell, 0, ParkObjects.FloorFootprint( item, 40, 40, 0 ),
				ground, name => name, [], out _, stamped );

			Assert.AreEqual( entries, stamped.Count, $"{item.Stem} (item {item.Id}): a cell for each table entry the shape does not leave blank" );
			++checkedItems;
		}

		Assert.IsTrue( checkedItems >= 30, $"{checkedItems} items checked" );
		Assert.AreEqual( 0, unnamed, "table entries naming no frame, which the stamp would still write" );
	}

	/// <summary>
	/// <b>A built cell with no table entry keeps its flag</b> (the original's stamp writes only the entries with <c>0x80</c>,
	/// <c>0x0046347a</c>: 77 of the New Save's type 4 cells read <c>0x101</c> without <c>0x200</c>), and <b>nothing is stamped
	/// with the compositor off</b> (<c>0x00463354</c>; low.sam's <c>PROCEDURALTEXTURING 0</c>): every covered cell is flagged here, so
	/// the cells cleared are exactly the table's entries.
	/// </summary>
	/// <remarks><b>Mutations:</b> the stamp clearing every covered cell; the compositor test removed.</remarks>
	[TestMethod]
	public void OnlyTheTablesEntriesAreStampedAndOnlyWithTheCompositorOn()
	{
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		var covered = 0;

		foreach ( var item in catalogue.All.Where( i => i.Shape != null ) )
		{
			var model = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );

			if ( ParkObjects.FloorMeshOf( model, item.Shape! ) is not (>= 0 and var mesh) )
				continue;

			var everyCell = (model.HeaderFlags & 0x80) != 0;
			var footprint = ParkObjects.FloorFootprint( item, 40, 40, 0 );
			var entries = ProceduralFloor.Table( model, mesh, item.Shape, item.Shape!.Width, item.Shape.Depth, everyCell, ProceduralBase.ParkGrid )
				.Cast<ProceduralFloor.Cell?>().Count( c => c != null );

			foreach ( var tileSize in new[] { 0, 8 } )
			{
				var field = Landscape();
				var cells = new List<(int X, int Y)>();

				for ( var y = footprint.Top; y <= footprint.Bottom; ++y )
				{
					for ( var x = footprint.Left; x <= footprint.Right; ++x )
					{
						field.Cells[(y * field.CellsX) + x] |= 0x800;
						cells.Add( (x, y) );
					}
				}

				ProceduralFloor.Blend( model, mesh, item.Shape, item.Shape.Width, item.Shape.Depth, everyCell, 0, footprint,
					GroundOf( field, tileSize ), name => name, [], out _ );

				var flagged = cells.Count( c => (field.FlagsAt( c.X, c.Y ) & 0x800) != 0 );

				Assert.AreEqual( tileSize == 0 ? cells.Count : cells.Count - entries, flagged, $"{item.Stem} at tile size {tileSize}: cells still flagged" );
			}

			if ( entries < (footprint.Right - footprint.Left + 1) * (footprint.Bottom - footprint.Top + 1) )
				++covered;
		}

		Assert.IsTrue( covered >= 1, "at least one item covers cells its table has no entry for" );
	}

	/// <summary>
	/// <b>Selling a thing gives its stamped cells their flag back</b>, and only its own cells.
	/// </summary>
	/// <remarks><b>Mutation:</b> the restore loop in <see cref="ParkObjects.Remove"/> removed fails here.</remarks>
	[TestMethod]
	public void SellingAThingGivesItsCellsTheirFlagBack()
	{
		Log ??= new();

		var field = Landscape();
		var (bent, flat) = TwoCells( field );
		var other = FirstBentAfter( field, bent );
		var ground = GroundOf( field, 8 );

		var objects = new ParkObjects( "jungle", null, null );
		var model = (LobbyModel)RuntimeHelpers.GetUninitializedObject( typeof( LobbyModel ) );
		typeof( LobbyModel ).GetField( "<Entities>k__BackingField", Any )!.SetValue( model, Array.Empty<ModelEntity>() );

		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		Assert.IsTrue( catalogue.TryGet( 1100, out var item ) );
		objects.Stand( 7, model, RideAnimations.Load( item.Directory, item.Stem, FileSystem, item.AnimationChannels ) );

		ground.StampSplit( bent.X, bent.Y );
		ground.StampSplit( flat.X, flat.Y );
		ground.StampSplit( other.X, other.Y );

		var standing = ((IDictionary)typeof( ParkObjects ).GetField( "_standing", Any )!.GetValue( objects )!)[7]!;
		standing.GetType().GetProperty( "Stamped" )!.SetValue( standing, new List<(int X, int Y)> { bent, flat } );

		var current = ParkGround.Current;
		SetCurrent( ground );

		try
		{
			Assert.IsTrue( objects.Remove( 7 ) );

			Assert.AreNotEqual( 0, field.FlagsAt( bent.X, bent.Y ) & 0x800, "its bent cell is given the flag back" );
			Assert.AreEqual( 0, field.FlagsAt( flat.X, flat.Y ) & 0x800, "a cell the load never flagged has none to get back" );
			Assert.AreEqual( 0, field.FlagsAt( other.X, other.Y ) & 0x800, "another thing's cell stays stamped" );
		}
		finally
		{
			SetCurrent( current );
		}
	}

	/// <summary>
	/// <b>A rebuilt ground keeps its heightfield</b>, with the cells a stamp has cleared: only a ground with none reads the file.
	/// </summary>
	/// <remarks><b>Mutation:</b> <see cref="ParkGround.KeptOrRead"/> always reading fails here.</remarks>
	[TestMethod]
	public void ARebuiltGroundKeepsItsHeightfield()
	{
		var field = Landscape();
		var (bent, _) = TwoCells( field );
		field.ClearSplit( bent.X, bent.Y );

		Assert.AreSame( field, ParkGround.KeptOrRead( field, () => throw new InvalidOperationException( "a second read" ) ) );
		Assert.AreEqual( 0, field.FlagsAt( bent.X, bent.Y ) & 0x800 );

		var first = Landscape();
		Assert.AreSame( first, ParkGround.KeptOrRead( null, () => first ), "the first build reads it" );
	}

	/// <summary>
	/// <b>Taking the queue's pieces down gives their cells the flag back</b> (the rebuild stamps the ones still queue again).
	/// </summary>
	/// <remarks><b>Mutation:</b> the restore loop in the queue's take-down removed fails here.</remarks>
	[TestMethod]
	public void TakingTheQueueDownGivesItsCellsTheirFlagBack()
	{
		var field = Landscape();
		var (bent, _) = TwoCells( field );
		var ground = GroundOf( field, 8 );

		var queues = (ParkQueues)RuntimeHelpers.GetUninitializedObject( typeof( ParkQueues ) );
		typeof( ParkQueues ).GetField( "_models", Any )!.SetValue( queues, new List<LobbyModel>() );
		typeof( ParkQueues ).GetField( "_blended", Any )!.SetValue( queues, new List<Texture>() );
		var stamped = new List<(int X, int Y)> { bent };
		typeof( ParkQueues ).GetField( "_stamped", Any )!.SetValue( queues, stamped );

		ground.StampSplit( bent.X, bent.Y );
		Assert.AreEqual( 1, queues.StampedCells );

		var current = ParkGround.Current;
		SetCurrent( ground );

		try
		{
			typeof( ParkQueues ).GetMethod( "TakeDown", Any )!.Invoke( queues, null );

			Assert.AreNotEqual( 0, field.FlagsAt( bent.X, bent.Y ) & 0x800, "the cell is ground again" );
			Assert.AreEqual( 0, queues.StampedCells );
		}
		finally
		{
			SetCurrent( current );
		}
	}

	/// <summary>
	/// <b>The tile pass after the stamp gives back the cells the map holds as ground</b> (type 0 or 5; the New Save's Staff Room at
	/// (42,23) covers (41,23) and (42,23), which the file holds as type 0, and the original's live map has no <c>0x200</c> on
	/// them): a cell of any other type stays stamped, and one given back is no longer the thing's to give back when it is sold.
	/// </summary>
	/// <remarks><b>Mutations:</b> the type test dropping a type 5 cell, or keeping a type 0 one; the stamped list left whole.</remarks>
	[TestMethod]
	public void TheTilePassGivesBackTheCellsTheMapHoldsAsGround()
	{
		Log ??= new();

		var field = Landscape();
		var (bent, flat) = TwoCells( field );
		var third = FirstBentAfter( field, bent );
		var fourth = FirstBentAfter( field, third );
		var ground = GroundOf( field, 8 );

		var objects = new ParkObjects( "jungle", null, null );
		var model = (LobbyModel)RuntimeHelpers.GetUninitializedObject( typeof( LobbyModel ) );
		typeof( LobbyModel ).GetField( "<Entities>k__BackingField", Any )!.SetValue( model, Array.Empty<ModelEntity>() );

		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		Assert.IsTrue( catalogue.TryGet( 1411, out var item ) );
		objects.Stand( 7, model, RideAnimations.Load( item.Directory, item.Stem, FileSystem, item.AnimationChannels ) );

		var cells = new[] { bent, third, fourth, flat };

		foreach ( var (x, y) in cells )
			ground.StampSplit( x, y );

		var standing = ((IDictionary)typeof( ParkObjects ).GetField( "_standing", Any )!.GetValue( objects )!)[7]!;
		standing.GetType().GetProperty( "Stamped" )!.SetValue( standing, cells.ToList() );

		var current = ParkGround.Current;
		SetCurrent( ground );

		try
		{
			// Types: the bent cell is ground (0), the next a body (4), the next ground of the other kind (5), the last an entrance (9).
			var types = new Dictionary<(int, int), int> { [bent] = 0, [third] = 4, [fourth] = 5, [flat] = 9 };

			Assert.AreEqual( 2, objects.RetileGround( 7, ( x, y ) => types[(x, y)] ) );

			Assert.AreNotEqual( 0, field.FlagsAt( bent.X, bent.Y ) & 0x800, "type 0: ground again, bent again" );
			Assert.AreEqual( 0, field.FlagsAt( third.X, third.Y ) & 0x800, "type 4: stays stamped" );
			Assert.AreNotEqual( 0, field.FlagsAt( fourth.X, fourth.Y ) & 0x800, "type 5: ground again" );
			Assert.AreEqual( 0, field.FlagsAt( flat.X, flat.Y ) & 0x800, "type 9: stays stamped" );
			Assert.AreEqual( 2, objects.StampedCells, "the thing now holds the two body cells" );

			Assert.AreEqual( 2, objects.RetileGround( 7, ( x, y ) => 0 ), "a second pass over type 0 gives the rest back" );
			Assert.AreEqual( 0, objects.StampedCells );
		}
		finally
		{
			SetCurrent( current );
		}
	}

	private static (int X, int Y) FirstBentAfter( HeightfieldFile field, (int X, int Y) after )
	{
		for ( var i = (after.Y * field.CellsX) + after.X + 1; i < field.CellsX * field.CellsY; ++i )
		{
			if ( (field.FlagsAt( i % field.CellsX, i / field.CellsX ) & 0x800) != 0 )
				return (i % field.CellsX, i / field.CellsX);
		}

		throw new InvalidOperationException( "no second bent cell" );
	}
}
