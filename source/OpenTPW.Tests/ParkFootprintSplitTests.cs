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
	/// <b>The load flags 589 cells of the stock park, and a stamped one stands on the near corner's plane.</b> The ground's split
	/// answer goes and the file's word stays; the height at the cell's far corner is then the plane's, not the bent cell's; restored, both
	/// are the load's again. A cell the load did not flag does not get the flag from a restore, and a cell off the grid is left.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>StampSplit</c> doing nothing; <c>UnstampSplit</c> doing nothing; <c>ParkGround.ScapeHeight</c> not passing
	/// the mask; <c>Splits</c> ignoring the mask.</remarks>
	[TestMethod]
	public void AClearedCellStandsOnThePlaneAndARestoredOneOnItsBend()
	{
		var field = Landscape();
		var (bent, flat) = TwoCells( field );
		var ground = GroundOf( field, 8 );

		var flagged = Enumerable.Range( 0, field.CellsX * field.CellsY ).Count( i => (field.FlagsAt( i % field.CellsX, i / field.CellsX ) & 0x800) != 0 );
		Assert.AreEqual( 589, flagged, "the cells the load flags (Q823's stock dump)" );

		// A point close to the cell's far corner, where the bent cell and the near corner's plane part most.
		var (worldX, worldY) = ((bent.X + 0.9f) * field.CellSizeX, (bent.Y + 0.9f) * field.CellSizeY);
		var (word, bentHeight) = (field.FlagsAt( bent.X, bent.Y ), ground.ScapeHeight( worldX, worldY ));

		Assert.IsFalse( ground.IsStamped( bent.X, bent.Y ) );
		Assert.IsTrue( ground.Splits( bent.X, bent.Y ) );
		ground.StampSplit( bent.X, bent.Y );
		Assert.IsTrue( ground.IsStamped( bent.X, bent.Y ), "a stamp holds it" );

		Assert.IsFalse( ground.Splits( bent.X, bent.Y ), "the split flag goes" );
		Assert.AreEqual( word, field.FlagsAt( bent.X, bent.Y ), "the file's word is left as it was" );
		Assert.AreEqual( field.CornerPlaneAt( worldX, worldY ), ground.ScapeHeight( worldX, worldY ), 1e-4f, "it stands on the near corner's plane" );
		Assert.AreNotEqual( bentHeight, ground.ScapeHeight( worldX, worldY ), 1e-3f, "which is not the bent cell's height" );
		Assert.AreEqual( bentHeight, field.ScapeHeight( worldX, worldY ), 1e-6f, "a field asked with no mask keeps the load's flags" );

		// The callers that hold only the field (a person's feet, a marker's corner) reach the running park's mask through Current.
		var current = ParkGround.Current;
		SetCurrent( ground );

		try
		{
			Assert.AreEqual( field.CornerPlaneAt( worldX, worldY ), ParkGround.ScapeHeight( field, worldX, worldY ), 1e-4f, "the running park's field takes the mask" );
			Assert.AreEqual( bentHeight, ParkGround.ScapeHeight( Landscape(), worldX, worldY ), 1e-6f, "another field has none" );
		}
		finally
		{
			SetCurrent( current );
		}

		ground.UnstampSplit( bent.X, bent.Y );

		Assert.IsFalse( ground.IsStamped( bent.X, bent.Y ), "ground again: no stamp holds it" );
		Assert.IsTrue( ground.Splits( bent.X, bent.Y ) );
		Assert.AreEqual( bentHeight, ground.ScapeHeight( worldX, worldY ), 1e-6f );

		ground.UnstampSplit( flat.X, flat.Y );
		Assert.IsFalse( ground.Splits( flat.X, flat.Y ), "a cell the load did not flag is not flagged by a restore" );

		ground.StampSplit( flat.X, flat.Y );
		Assert.IsFalse( ground.Splits( flat.X, flat.Y ), "stamping a cell without the flag changes nothing" );

		ground.StampSplit( -1, 0 );
		ground.StampSplit( field.CellsX, 0 );
		ground.UnstampSplit( 0, field.CellsY );
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

		var off = GroundOf( field, 0 );
		Assert.IsFalse( off.StampSplit( bent.X, bent.Y ), "no tiles: no stamp" );
		Assert.IsTrue( off.Splits( bent.X, bent.Y ), "the flag stays" );

		var on = GroundOf( field, 8 );
		Assert.IsTrue( on.StampSplit( bent.X, bent.Y ), "tiles: stamped" );
		Assert.IsFalse( on.Splits( bent.X, bent.Y ) );
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
		Assert.IsFalse( ground.Splits( bent.X, bent.Y ), "but the cell is stamped" );
		CollectionAssert.AreEqual( new[] { bent }, stamped );
	}

	/// <summary>
	/// <b>The stamp is made for each of Lost Kingdom's items that blend, as many cells as the table has entries</b>, not the cells
	/// that found a frame's texture.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> the stamp after the frame-name test survives, because no entry of Lost Kingdom's names no frame, in the
	/// catalogue's items or in the queue's pieces (dead by CONTENT, counted below): the order is the code's, from <c>0x0046347a</c>
	/// to <c>0x0046353b</c> having no name to look at.
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

		// The queue's pieces blend their one cell from the root mesh (ParkQueues.BlendFloor), which no catalogue item does.
		var pieces = ((string[])typeof( ParkQueues ).GetField( "Pieces", AnyStatic )!.GetValue( null )!).Distinct().ToArray();
		var checkedPieces = 0;

		foreach ( var piece in pieces )
		{
			var model = new ModelFile( $"levels/jungle/queue/{piece}.MD2" );

			if ( (model.HeaderFlags & 0x8) == 0 )
				continue;

			var table = ProceduralFloor.Table( model, model.RootNode, null, 1, 1, true, ProceduralBase.ParkGrid );
			var named = model.Meshes[model.RootNode].Materials.Where( m => m.FrameOffset != 0 ).Select( m => m.FrameOffset ).ToHashSet();
			var entry = table[0, 0];

			Assert.IsNotNull( entry, $"queue piece {piece}: its one cell has an entry" );
			unnamed += named.Contains( entry!.Value.FrameOffset ) ? 0 : 1;
			++checkedPieces;
		}

		Assert.AreEqual( 7, pieces.Length, "the table's distinct pieces" );
		Assert.IsTrue( checkedPieces >= 5, $"{checkedPieces} queue pieces checked" );
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

				var ground = GroundOf( field, tileSize );

				ProceduralFloor.Blend( model, mesh, item.Shape, item.Shape.Width, item.Shape.Depth, everyCell, 0, footprint,
					ground, name => name, [], out _ );

				var flagged = cells.Count( c => ground.Splits( c.X, c.Y ) );

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

			Assert.IsTrue( ground.Splits( bent.X, bent.Y ), "its bent cell is given the flag back" );
			Assert.IsFalse( ground.Splits( flat.X, flat.Y ), "a cell the load never flagged has none to get back" );
			Assert.IsFalse( ground.Splits( other.X, other.Y ), "another thing's cell stays stamped" );
		}
		finally
		{
			SetCurrent( current );
		}
	}

	/// <summary>
	/// <b>A rebuilt ground keeps its heightfield</b>: only a ground with none reads the file.
	/// </summary>
	/// <remarks><b>Mutation:</b> <see cref="ParkGround.KeptOrRead"/> always reading fails here.</remarks>
	[TestMethod]
	public void ARebuiltGroundKeepsItsHeightfield()
	{
		var field = Landscape();

		Assert.AreSame( field, ParkGround.KeptOrRead( field, () => throw new InvalidOperationException( "a second read" ) ) );

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
			typeof( ParkQueues ).GetMethod( "TakeDown", Any )!.Invoke( queues, [true] );

			Assert.IsTrue( ground.Splits( bent.X, bent.Y ), "the cell is ground again" );
			Assert.AreEqual( 0, queues.StampedCells );
		}
		finally
		{
			SetCurrent( current );
		}
	}

	/// <summary>
	/// <b>A queue cell laid over with path keeps its stamp; one that is ground gives it back</b> (Q841, the original's live map: a path laid
	/// over the flagged queue cell (31,18) of the New Save left it without <c>0x800</c>, the path arm copying nothing back). A cell that
	/// is still a piece is stamped again by the rebuild and is not the give-back's to touch.
	/// </summary>
	/// <remarks><b>Mutations:</b> the give-back ignoring the cell's type (a path cell given its flag back); keeping every cell (a ground cell not given back).</remarks>
	[TestMethod]
	public void ARebuildKeepsTheStampOfAQueueCellLaidOverWithPathAndGivesBackGround()
	{
		var field = Landscape();
		var (bent, _) = TwoCells( field );
		var second = FirstBentAfter( field, bent );
		var third = FirstBentAfter( field, second );
		var ground = GroundOf( field, 8 );

		foreach ( var (x, y) in new[] { bent, second, third } )
			ground.StampSplit( x, y );

		var types = new Dictionary<(int, int), int> { [bent] = CellEdge.Nothing, [second] = CellEdge.Path, [third] = ParkRideChoice.QueueCellType };

		var given = ParkQueues.GiveBackGround( [bent, second, third], [third], ( x, y ) => types[(x, y)], ground );

		Assert.AreEqual( 1, given, "only the ground cell is given back" );
		Assert.IsTrue( ground.Splits( bent.X, bent.Y ), "ground takes the load's flag again" );
		Assert.IsFalse( ground.Splits( second.X, second.Y ), "a path laid over a piece keeps the stamp: no 0x800" );
		Assert.IsTrue( ground.IsStamped( second.X, second.Y ) );
		Assert.IsFalse( ground.Splits( third.X, third.Y ), "a cell still a piece stays stamped" );

		types[second] = 5;
		Assert.AreEqual( 1, ParkQueues.GiveBackGround( [second], [], ( x, y ) => types[(x, y)], ground ), "type 5 is ground too" );
		Assert.IsTrue( ground.Splits( second.X, second.Y ) );
	}

	/// <summary>
	/// <b>A queue's rebuild keeps the stamp of a cell a path was laid over, and gives a ground cell its flag back</b> (Q841): the whole
	/// <see cref="ParkQueues.Rebuild"/>, over the stock park's four queue cells, whose pieces cannot be made without a graphics device (each is
	/// logged as missing) so nothing stands again and every old stamp is the give-back's to judge.
	/// </summary>
	/// <remarks><b>Mutation:</b> <c>TakeDown( giveBack: false )</c> in <see cref="ParkQueues.Rebuild"/> made <c>true</c> fails here (the path cell is
	/// given its flag back); the call to <c>GiveBackGround</c> removed fails here (the ground cell is not).</remarks>
	[TestMethod]
	public void ARebuildOfTheQueuesKeepsAPathCellsStampAndFreesAGroundOne()
	{
		Log ??= new();

		var field = Landscape();
		var (bent, _) = TwoCells( field );
		var second = FirstBentAfter( field, bent );
		var ground = GroundOf( field, 8 );

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var park = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( park );
		state.SetRecord( second.X, second.Y, ParkState.CellFor( park, second.X, second.Y ) with { Type = CellEdge.Path } );
		state.SetRecord( bent.X, bent.Y, ParkState.CellFor( park, bent.X, bent.Y ) with { Type = CellEdge.Nothing } );

		var queues = (ParkQueues)RuntimeHelpers.GetUninitializedObject( typeof( ParkQueues ) );
		typeof( ParkQueues ).GetField( "_models", Any )!.SetValue( queues, new List<LobbyModel>() );
		typeof( ParkQueues ).GetField( "_blended", Any )!.SetValue( queues, new List<Texture>() );
		typeof( ParkQueues ).GetField( "_stamped", Any )!.SetValue( queues, new List<(int X, int Y)> { bent, second } );
		typeof( ParkQueues ).GetField( "_world", Any )!.SetValue( queues, park );
		typeof( ParkQueues ).GetField( "<ThemeName>k__BackingField", Any )!.SetValue( queues, "jungle" );

		ground.StampSplit( bent.X, bent.Y );
		ground.StampSplit( second.X, second.Y );

		var current = ParkGround.Current;
		SetCurrent( ground );

		try
		{
			queues.Rebuild();

			Assert.IsTrue( ground.Splits( bent.X, bent.Y ), "the cell that is ground again takes the load's flag" );
			Assert.IsFalse( ground.Splits( second.X, second.Y ), "the cell a path lies over keeps the stamp" );
		}
		finally
		{
			SetCurrent( current );
		}
	}

	/// <summary>
	/// <b>Lifting a path gives back the stamp a queue piece left under it</b>: the clear ends in a retile of the cell as ground
	/// (<c>FUN_005367a0</c>, <c>FUN_005346d0( 0 )</c> then <c>FUN_005365d0</c>), which copies the load's word. A path cell the
	/// clear refuses (its counter above nought) keeps what it has.
	/// </summary>
	/// <remarks><b>Mutation:</b> the <c>UnstampSplit</c> in <see cref="ParkPathBuilding.ClearPathCell"/> removed fails here.</remarks>
	[TestMethod]
	public void LiftingAPathGivesBackTheStampAQueuePieceLeftUnderIt()
	{
		var field = Landscape();
		var (bent, _) = TwoCells( field );
		var ground = GroundOf( field, 8 );

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var park = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var state = new ParkState( park );

		state.SetRecord( bent.X, bent.Y, ParkState.CellFor( park, bent.X, bent.Y ) with { Type = CellEdge.Path, Flags = 0, OverlapCounter = 1 } );
		ground.StampSplit( bent.X, bent.Y );

		var current = ParkGround.Current;
		SetCurrent( ground );

		try
		{
			Assert.IsFalse( ParkPathBuilding.ClearPathCell( state, park, bent.X, bent.Y, stepped: true ), "the first press only counts down" );
			Assert.IsFalse( ground.Splits( bent.X, bent.Y ), "and the stamp stays" );

			Assert.IsTrue( ParkPathBuilding.ClearPathCell( state, park, bent.X, bent.Y, stepped: true ) );
			Assert.IsTrue( ground.Splits( bent.X, bent.Y ), "the cell is ground again and takes the load's flag" );
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

			Assert.IsTrue( ground.Splits( bent.X, bent.Y ), "type 0: ground again, bent again" );
			Assert.IsFalse( ground.Splits( third.X, third.Y ), "type 4: stays stamped" );
			Assert.IsTrue( ground.Splits( fourth.X, fourth.Y ), "type 5: ground again" );
			Assert.IsFalse( ground.Splits( flat.X, flat.Y ), "type 9: stays stamped" );
			Assert.AreEqual( 2, objects.StampedCells, "the thing now holds the two body cells" );

			Assert.AreEqual( 2, objects.RetileGround( 7, ( x, y ) => 0 ), "a second pass over type 0 gives the rest back" );
			Assert.AreEqual( 0, objects.StampedCells );
		}
		finally
		{
			SetCurrent( current );
		}
	}

	/// <summary>Stands a thing in <paramref name="objects"/> as the load and a purchase do (the model of <see cref="ParkObjects.Place"/>
	/// needs a graphics device), with <paramref name="cells"/> stamped through the ground, so the tile pass has something to give back.</summary>
	private void StandStamped( ParkObjects objects, ParkGround ground, int thingId, int catalogueId, IEnumerable<(int X, int Y)> cells )
	{
		var model = (LobbyModel)RuntimeHelpers.GetUninitializedObject( typeof( LobbyModel ) );
		typeof( LobbyModel ).GetField( "<Entities>k__BackingField", Any )!.SetValue( model, Array.Empty<ModelEntity>() );

		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		Assert.IsTrue( catalogue.TryGet( catalogueId, out var item ) );
		objects.Stand( thingId, model, RideAnimations.Load( item.Directory, item.Stem, FileSystem, item.AnimationChannels ), catalogueId );

		var stamped = cells.Where( cell => ground.StampSplit( cell.X, cell.Y ) ).ToList();
		var standing = ((IDictionary)typeof( ParkObjects ).GetField( "_standing", Any )!.GetValue( objects )!)[thingId]!;
		standing.GetType().GetProperty( "Stamped" )!.SetValue( standing, stamped );
	}

	private static void SetType( ParkWorld world, int x, int y, int type )
	{
		var cells = (ParkWorld.MapCell[])typeof( ParkWorld ).GetField( "_cells", Any )!.GetValue( world )!;
		cells[(y * ParkWorld.MapSize) + x] = cells[(y * ParkWorld.MapSize) + x] with { Type = type };
	}

	private static IEnumerable<(int X, int Y)> CellsOf( (int Left, int Top, int Right, int Bottom) box )
		=> Enumerable.Range( box.Top, box.Bottom - box.Top + 1 ).SelectMany( y => Enumerable.Range( box.Left, box.Right - box.Left + 1 ).Select( x => (x, y) ) );

	/// <summary>
	/// <b>The load's tile pass runs after every thing is stood</b> (<c>ParkObjects</c>' constructor): a stamped cell the park file
	/// holds as type 0 or 5 is given back to the ground, one of any other type stays stamped. Two cells of the stock park's placed
	/// things are set to types 0 and 5 in the world this reads (the New Save's Staff Room at (42,23) has two such cells), every
	/// placed thing is stood with its footprint stamped, and the objects are built over that world.
	/// </summary>
	/// <remarks><b>Mutation:</b> the pass over <c>_standing</c> in the constructor removed leaves both cells stamped.</remarks>
	[TestMethod]
	public void TheLoadsTilePassGivesBackTheCellsTheFileHoldsAsGround()
	{
		Log ??= new();

		var field = Landscape();
		var ground = GroundOf( field, 8 );
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );

		var things = world.Objects.Where( o => o.IsPlaced && catalogue.TryGet( o.CatalogueId, out var i ) && i.Shape != null ).ToArray();
		var covered = things.ToDictionary( o => o.ThingId, o => CellsOf( ParkObjects.FootprintAt( catalogue.All.Single( i => i.Id == o.CatalogueId ), o.CellX, o.CellY, o.Angle ) ).ToArray() );
		var all = covered.Values.SelectMany( c => c ).ToArray();

		Assert.IsTrue( all.Length > 10, "the stock park's things cover cells" );
		Assert.AreEqual( all.Length, all.Distinct().Count(), "no two things share a cell" );

		var (ground0, ground5) = (all[0], all[^1]);
		SetType( world, ground0.X, ground0.Y, 0 );
		SetType( world, ground5.X, ground5.Y, 5 );

		var current = ParkGround.Current;
		SetCurrent( ground );
		ParkObjects.PlaceInstead = ( objects, placed ) =>
			StandStamped( objects, ground, placed.ThingId, placed.CatalogueId, covered.TryGetValue( placed.ThingId, out var cells ) ? cells : [] );

		try
		{
			var objects = new ParkObjects( "jungle", world, catalogue );

			Assert.AreEqual( all.Length - 2, objects.StampedCells, "the two cells the file holds as ground are given back" );
			Assert.IsFalse( ground.IsStamped( ground0.X, ground0.Y ), "type 0: ground again" );
			Assert.IsFalse( ground.IsStamped( ground5.X, ground5.Y ), "type 5: ground again" );
			Assert.IsTrue( all.Skip( 1 ).SkipLast( 1 ).All( cell => ground.IsStamped( cell.X, cell.Y ) ), "every other cell stays stamped" );
		}
		finally
		{
			ParkObjects.PlaceInstead = null;
			SetCurrent( current );
		}
	}

	/// <summary>
	/// <b>A purchase of the Huge Hollow Rock types the model's cells, and runs no tile pass over the stamped ones</b>: the rock's model
	/// stands one row above its anchor, and the original's purchase typed 23 cells (the model's 5 x 5 less its two empty corners, owner
	/// the anchor) and stamped all 14 table cells (Q888, <c>q888/orig/run1/</c>); this game typed the anchor's rectangle and gave the
	/// row above it back (Q744, now fixed). The original's purchase has no pass that gives a stamped cell back: the placer retiles
	/// only the cells its picture marks, to tile 8 (Q890), so a stamped cell the map holds as ground stays stamped; the stand-in here
	/// stamps one such cell beside the rock, which must stay.
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>Stamp</c> over the anchor's rectangle (<see cref="ParkObjects.FootprintAt"/>) types row 28 and leaves
	/// the model's top row ground, so fewer cells are typed; the load's tile pass (<c>RetileGround</c>) called after the stamp gives the
	/// extra cell back and one fewer stays stamped.</remarks>
	[TestMethod]
	public void APurchaseOfTheRockTypesTheModelsCellsAndGivesNoStampBack()
	{
		Log ??= new();

		const int Rock = 1427;
		var level = Level.Current;
		var (catalogue, _, _) = ParkFootprintPictureTests.BuyScreen();
		var state = Level.Current!.ParkState!;
		typeof( ParkState ).GetProperty( nameof( ParkState.Balance ) )!.SetValue( state, 90000 );

		Assert.IsTrue( catalogue.TryGet( Rock, out var item ) );

		var field = Landscape();
		var ground = GroundOf( field, 8 );
		var objects = new ParkObjects( "jungle", null, null );
		var build = typeof( ParkBuilding ).GetMethods( BindingFlags.NonPublic | BindingFlags.Static )
			.Single( method => method.Name == "Build" && method.GetParameters().Length == 10 );

		ParkWorld.CatalogueObject? stood = null;

		var current = ParkGround.Current;
		SetCurrent( ground );

		try
		{
			string Answer( int x, int y )
			{
				var built = build.Invoke( null, [state, Level.Current!.Park, catalogue, objects, null, Rock, x, y, 0, 0] )!;
				return (string)built.GetType().GetProperty( "Answer" )!.GetValue( built )!;
			}

			// The first cell the rock may stand on: with nothing to stand it, the build answers that it would not load.
			ParkBuilding.StandThing = _ => false;
			var at = Enumerable.Range( 0, 128 * 128 ).Select( i => (X: i % 128, Y: i / 128) )
				.Where( cell => cell.X is >= 10 and < 100 && cell.Y is >= 10 and < 100 ).First( cell => Answer( cell.X, cell.Y ).Contains( "would not load" ) );

			// The ground's own rebuild, which ends a build (ParkSurfaces.Rebuild), wants a graphics device and fails on the stand-in
			// ground; the stamp and the tile pass have run before it.
			ParkBuilding.StandThing = placed =>
			{
				// One stamped cell the picture does not mark (the cell above the rock's top-left corner, ground): the case the original's
				// purchase leaves stamped and the load's pass would give back.
				StandStamped( objects, ground, placed.ThingId, placed.CatalogueId,
					ParkObjects.CoveredCells( item, placed.CellX, placed.CellY, placed.Angle ).Append( (placed.CellX, placed.CellY - 2) ) );
				stood = placed;
				return true;
			};

			var failure = Assert.ThrowsException<TargetInvocationException>( () => Answer( at.X, at.Y ) );
			StringAssert.Contains( failure.InnerException!.StackTrace, "ParkGround.Build", "it is the ground's rebuild that fails" );

			Assert.AreEqual( at, (stood!.Value.CellX, stood.Value.CellY) );

			var marked = CellsOf( (at.X - 1, at.Y - 2, at.X + 6, at.Y + 6) )
				.Where( cell => state.Record( cell.X, cell.Y ).Type == CellEdge.Footprint ).ToHashSet();
			var model = ParkObjects.CoveredCells( item, at.X, at.Y, 0 ).ToHashSet();

			Assert.AreEqual( 23, marked.Count, "the model's 5 x 5 less its two empty corners" );
			Assert.IsTrue( marked.SetEquals( model ), "typed on the model's cells, not the anchor's rectangle" );
			Assert.IsTrue( marked.All( cell => state.Record( cell.X, cell.Y ).ParentId == MapStep.CellId( at.X, at.Y ) ), "owned by the anchor" );
			Assert.IsTrue( Enumerable.Range( at.X + 1, 4 ).All( x => marked.Contains( (x, at.Y - 1) ) ), "the row above the anchor is typed" );
			Assert.IsFalse( marked.Any( cell => cell.Y == at.Y + 4 ), "and the anchor rectangle's last row is not" );
			Assert.IsTrue( state.Record( at.X, at.Y - 2 ).Type is <= 0 or 5, "the extra cell is ground in the map" );
			Assert.AreEqual( 24, objects.StampedCells, "no tile pass follows the purchase: the ground cell stamped beside the rock stays stamped" );
		}
		finally
		{
			ParkBuilding.StandThing = null;
			SetCurrent( current );

			// The purchase was paid in this level's park state: its month's costs are what the staff costs screen reads of the current level.
			Level.Current = level;
		}
	}

	/// <summary>
	/// <b>The items whose saved model records never carry <c>0x8</c> have no floor here, so ours stamps none of them.</b> The load
	/// stamps a record whose word (<c>+0x15</c>, <c>0x00464942</c>) carries <c>0x8</c>; across the 859 distinct park files on the
	/// maintainer's machine the records of items 1600 and 1602 (the bus and the seaplane) never do, and those of 1601, 1603 and 1604
	/// (the gates, the lights, the ferry) do in 14, 14 and 1 of 851, 851 and 90, all of them `0x32f` records this game's writer made (the
	/// original's own files never carry it there). Their models carry no procedural base (header
	/// <c>0x8</c>), so <see cref="ParkObjects.FloorMeshOf"/> finds no floor and <see cref="ProceduralFloor.Blend"/> is not reached: the
	/// two games agree there by the model, whatever the word (the original's table for a header-<c>0x1</c> model is empty:
	/// <c>0x0046a9ad</c>, <c>0x0046b37b</c>, <c>0x00463363</c>, <c>docs/exe/park-engine.md</c>). The other never-<c>0x8</c> ids (1141, 1142, 1151 to 1154, 1171, 1183,
	/// 4141: wakes, cars and carts, the vehicles' <c>0x101</c>) are not Lost Kingdom catalogue items and are never placed here.
	/// </summary>
	/// <remarks><b>Mutation:</b> <c>FloorMeshOf</c> answering a mesh without the header's <c>0x8</c> fails here.</remarks>
	[TestMethod]
	public void TheItemsWhoseSavedRecordsNeverCarry0x8HaveNoFloor()
	{
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );

		foreach ( var id in new[] { 1600, 1601, 1602, 1603, 1604 } )
		{
			Assert.IsTrue( catalogue.TryGet( id, out var item ), $"item {id}" );

			var model = new ModelFile( $"{item.Directory}/{item.Stem}.MD2" );

			Assert.AreEqual( 0u, model.HeaderFlags & 0x8, $"{item.Name} ({id}): no procedural base" );
			Assert.AreEqual( -1, ParkObjects.FloorMeshOf( model, item.Shape! ), $"{item.Name} ({id}): no floor, no stamp" );
		}

		foreach ( var id in new[] { 1141, 1142, 1151, 1152, 1153, 1154, 1171, 1183, 4141 } )
			Assert.IsFalse( catalogue.TryGet( id, out _ ), $"item {id} is a vehicle, not a Lost Kingdom catalogue item" );

		// And no item of that family is among those that do blend a floor.
		var withFloor = catalogue.All.Where( i => i.Shape != null && ParkObjects.FloorMeshOf( new ModelFile( $"{i.Directory}/{i.Stem}.MD2" ), i.Shape ) >= 0 )
			.Select( i => i.Id ).ToHashSet();

		Assert.IsFalse( withFloor.Overlaps( new[] { 1600, 1601, 1602, 1603, 1604 } ) );
		Assert.IsTrue( withFloor.Count >= 30, $"{withFloor.Count} items with a floor" );
	}

	/// <summary>
	/// <b>The track kits' and the hoardings' piece models carry no procedural base in any theme, and the queue's do, so ours stamps the queue's
	/// and none of theirs, as the original does.</b> The original's pieces are made flags <c>0x33a</c> (<c>0x8</c> set) like the queue's, but
	/// the stamp needs the model's table (<c>0x00463363</c>), which <c>ProceduralBase_Build</c> makes at registration only when the
	/// load flag <c>0x1</c> is set (<c>0x00462c69</c>): the kits' and the hoardings' registrations pass <c>0x10422</c> and <c>0x422</c>
	/// (<c>0x00522b1e</c>, <c>0x00522cde</c>, <c>0x00522eb1</c>) and every one of the models has header <c>0x1</c>, where the queue's seven have
	/// <c>0x9</c>. In the original a Dino Karts' and a Splish Splash's loops, and a fresh park's 107 hoardings, stamp no cell and the
	/// New Save's 163 hoardings and 31 kart cells carry no <c>0x200</c> (<c>docs/exe/park-engine.md</c>, "The track kit's, the
	/// hoardings' and the coaster's cells stamp nothing").
	/// </summary>
	/// <remarks>
	/// A data guard: it reads the pieces' files and <c>FloorMeshOf</c>, not the kits' or hoardings' placement
	/// (<see cref="TheKitsAndTheHoardingsPlacementStampsNoCell"/> drives that). <b>Mutation:</b> <c>FloorMeshOf</c> ignoring the
	/// header's <c>0x8</c> fails here.
	/// </remarks>
	[TestMethod]
	public void TheKitsAndTheHoardingsModelsHaveNoProceduralBaseAndTheQueuesDo()
	{
		var shape = new ItemShape( 2, 2, [[1, 1], [1, 1]] );
		var checkedModels = 0;

		foreach ( var theme in new[] { "jungle", "fantasy", "hallow", "space" } )
		{
			var pieces = new List<string>();

			foreach ( var anchor in new[] { ParkTrackPieces.KartAnchor, ParkTrackPieces.WaterAnchor } )
			{
				for ( var row = 1; row < 25; ++row )
				{
					if ( ParkTrackPieces.ModelOf( anchor, row ) is { } model )
						pieces.Add( $"{ParkTrackPieces.KitDirectory( theme, anchor )}/{model}.MD2" );
				}
			}

			pieces.AddRange( new[] { "ho_end1", "ho_fos1", "ho_fos2", "ho_fos3", "ho_fos4", "ho_fob1", "ho_fob2", "ho_fob3", "ho_fob4" }
				.Select( name => $"levels/{theme}/hoarding/{name}.MD2" ) );

			foreach ( var path in pieces.Distinct( StringComparer.OrdinalIgnoreCase ) )
			{
				using ( var stream = FileSystem.OpenReadOrNull( path ) )
				{
					if ( stream == null )
						continue;
				}

				var model = new ModelFile( path );

				Assert.AreEqual( 0u, model.HeaderFlags & 0x8, $"{path}: no procedural base" );
				Assert.AreEqual( -1, ParkObjects.FloorMeshOf( model, shape ), $"{path}: no floor, no stamp" );
				++checkedModels;
			}
		}

		Assert.IsTrue( checkedModels >= 100, $"{checkedModels} piece models read" );

		var queuePieces = (string[])typeof( ParkQueues ).GetField( "Pieces", AnyStatic )!.GetValue( null )!;

		foreach ( var name in queuePieces.Distinct() )
			Assert.AreNotEqual( 0u, new ModelFile( $"levels/jungle/queue/{name}.MD2" ).HeaderFlags & 0x8, $"queue piece {name}: a procedural base, so it stamps" );
	}

	/// <summary>
	/// <b>Standing the track kits' pieces and a fresh park's boundary hoardings stamps no cell</b>, as the original's stamp writes
	/// none for them (<c>docs/exe/park-engine.md</c>, "The track kit's, the hoardings' and the coaster's cells stamp nothing"):
	/// with a ground that has a tile size, so a stamp would hold a cell, both are stood and no cell of the map is held. A test has
	/// no renderer, so each piece's model is not made; the placement up to the model (its stand and transform) does run.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> <c>ground.StampSplit( piece.CellX, piece.CellY )</c> added to <c>ParkBoundaryHoarding.Place</c> and to
	/// <c>ParkTrackPieces.Make</c> before the model is made fails both halves. A stamp added after the model is made
	/// would not be reached here, since a test cannot make one.
	/// </remarks>
	[TestMethod]
	public void TheKitsAndTheHoardingsPlacementStampsNoCell()
	{
		Log ??= new();

		var field = Landscape();
		var ground = GroundOf( field, 8 );
		var current = ParkGround.Current;
		SetCurrent( ground );

		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var cells = (ParkWorld.MapCell[])typeof( ParkWorld ).GetField( "_cells", Any )!.GetValue( world )!;
		var laid = 0;

		foreach ( var (anchor, y) in new[] { (ParkTrackPieces.KartAnchor, 40), (ParkTrackPieces.WaterAnchor, 44) } )
		{
			for ( var row = 1; row < 25; ++row )
			{
				if ( ParkTrackPieces.ModelOf( anchor, row ) == null )
					continue;

				cells[(y * ParkWorld.MapSize) + 10 + row] = cells[(y * ParkWorld.MapSize) + 10 + row] with { TrackType = anchor, TrackTileIndex = row, TrackTileAngle = 0 };
				++laid;
			}
		}

		var catalogue = new ParkItemCatalogue( "jungle", data );
		var fresh = new FreshPark( "jungle", new ParkBalance( "jungle", easyMode: false ), catalogue, data );
		ParkTrackPieces? kits = null;
		ParkBoundaryHoarding? wall = null;

		try
		{
			kits = new ParkTrackPieces( "jungle", world );
			wall = new ParkBoundaryHoarding( "jungle", fresh, catalogue );

			Assert.IsTrue( laid >= 10, $"{laid} kit pieces laid" );
			Assert.AreEqual( laid, Enumerable.Range( 0, ParkWorld.MapSize * ParkWorld.MapSize )
				.Count( at => ParkTrackPieces.PieceAt( at % ParkWorld.MapSize, at / ParkWorld.MapSize, cells[at] ) != null ), "each one names a piece" );
			Assert.AreEqual( 107, ParkBoundaryHoarding.AnchorsOf( fresh ), "the fresh park's boundary is stood" );

			var held = Enumerable.Range( 0, field.CellsX * field.CellsY ).Count( at => ground.IsStamped( at % field.CellsX, at / field.CellsX ) );
			Assert.AreEqual( 0, held, "no piece of either stamped a cell" );
		}
		finally
		{
			kits?.Delete();
			wall?.Delete();
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
