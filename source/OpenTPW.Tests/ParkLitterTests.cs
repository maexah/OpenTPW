using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;

namespace OpenTPW.Tests;

/// <summary>
/// A cell's litter: its place by the reseed of the park's generator with the cell id, its flat picture, and its sprite and
/// cell block written to the park file and read back (<c>docs/exe/ride-operation.md</c>, "A cell's litter" and "What it is
/// drawn as"; <c>docs/exe/saves.md</c>, "OpenTPW's writer, a cell's litter"). The places below were worked out apart from
/// the code, by the generator's own steps.
/// </summary>
[TestClass]
public class ParkLitterTests
{
	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	[TestCleanup]
	public void Forget()
	{
		TestRun.DeleteEvery<ParkGuestSprites>();
		TestRun.DeleteEvery<ParkPeople>();
		ParkState.ForgetCurrent();
		Unimplemented.Forget();
	}

	private static int Cell( int x, int y ) => (y * ParkWorld.MapSize) + x;

	private static ParkWorld Read( byte[] file )
	{
		using var stream = new MemoryStream( file );
		var reader = new SaveReader( stream );
		return new ParkWorld( reader.ReadFile(), reader.Preamble );
	}

	/// <summary>
	/// The place is the cell id's (y × 128 + x + 1), reseeded and drawn twice: (47,22), id 2864, stands at (476, 221); the id
	/// without its one would give (474, 222), and x and y swapped (22,47), id 6039, stands at (227, 474), not (221, 476).
	/// </summary>
	/// <remarks><b>Mutations:</b> the id's <c>+ 1</c> taken out; <c>r1</c> and <c>r2</c> swapped; <c>% 9</c> as <c>% 10</c>.</remarks>
	[TestMethod]
	public void ACellsLitterStandsWhereTheReseedWithItsIdPutsIt()
	{
		Assert.AreEqual( (476f, 221f), ParkState.LitterPlace( 47, 22 ) );
		Assert.AreEqual( (227f, 474f), ParkState.LitterPlace( 22, 47 ) );
		Assert.AreEqual( (415f, 212f), ParkState.LitterPlace( 41, 21 ) );
	}

	/// <summary>
	/// A drop reseeds the park's one generator with the cell id and leaves it two draws on, 0x6e02790d after (47,22),
	/// whatever it held; the cell keeps the kind, the sprite and its place. A second drop on that cell does nothing, the
	/// generator not touched. The writer is handed the one drop.
	/// </summary>
	/// <remarks><b>Mutations:</b> the reseed taken out of <c>DropLitter</c> (the generator keeps nought); the reseed after the
	/// refusal of a littered cell.</remarks>
	[TestMethod]
	public void ADropReseedsTheParksGeneratorAndASecondDropThereDoesNothing()
	{
		var state = new ParkState( null );

		Assert.AreEqual( 0u, state.RandomSeed );
		Assert.IsTrue( state.DropLitter( 47, 22, 3 ) );
		Assert.AreEqual( 0x6e02790du, state.RandomSeed, "reseeded with 2864 and drawn twice" );

		var cell = state.CellAt( 47, 22 );

		Assert.AreEqual( (3, true, true, 476f, 221f), (cell.Litter, cell.LitterSprite, cell.LitterDropped, cell.LitterX, cell.LitterY) );

		state.Draw();
		var drawn = state.RandomSeed;

		Assert.IsFalse( state.DropLitter( 47, 22, 5 ) );
		Assert.AreEqual( (drawn, 3), (state.RandomSeed, state.CellAt( 47, 22 ).Litter), "a littered cell keeps its litter and the generator" );
	}

	/// <summary>
	/// Each kind's program word: the record <c>0x0074f050 + 0x28</c> × its slot less <c>0x0074dab8</c>, over four. Kinds 1 to 5 on
	/// slots 0 to 4, 7 and 8 on 6 and 7, 6 and anything else on 5.
	/// </summary>
	[TestMethod]
	public void EachKindIsMadeOnItsOwnRecord()
	{
		CollectionAssert.AreEqual( new[] { 1432, 1382, 1392, 1402, 1412, 1422, 1432, 1442, 1452, 1432 },
			Enumerable.Range( 0, 10 ).Select( ParkWorld.LitterProgram ).ToArray() );
		CollectionAssert.AreEqual( new[] { 5, 0, 1, 2, 3, 4, 5, 6, 7, 5 }, Enumerable.Range( 0, 10 ).Select( ParkWorld.LitterSlot ).ToArray() );
	}

	/// <summary>
	/// The flat quad of a 10 × 7 picture whose <c>.TPC</c> origin is (0, -8), kind 1's, at (476, 221): across 476 to 477.171875
	/// (ten texels of 0.1171875), down 219.75 to 220.84375 (from -8 to -1 texels of 0.15625), all four at the one height, the
	/// picture's top left texel at the smallest x and y. A 12 × 13 picture at (-3, -14), kind 2's, reaches back 0.3515625 across.
	/// </summary>
	/// <remarks><b>Mutations:</b> the 0.75 dropped from across; the origin's sign flipped; corners 1 and 3 swapped.</remarks>
	[TestMethod]
	public void TheQuadLiesFlatAtTheAnchorByThePicturesOrigin()
	{
		var corners = new Vertex[4];

		ParkLitter.Corners( corners, new Vector2( 476f, 221f ), 5.6f, new ParkGuestSprites.Region( 0.1f, 0.2f, 0.3f, 0.4f, 10, 7, 0, 8 ) );

		CollectionAssert.AreEqual( new[]
		{
			new Vector3( 476f, 219.75f, 5.6f ), new Vector3( 477.171875f, 219.75f, 5.6f ),
			new Vector3( 477.171875f, 220.84375f, 5.6f ), new Vector3( 476f, 220.84375f, 5.6f )
		}, corners.Select( corner => corner.Position ).ToArray() );
		CollectionAssert.AreEqual( new[] { new Vector2( 0.1f, 0.2f ), new Vector2( 0.3f, 0.2f ), new Vector2( 0.3f, 0.4f ), new Vector2( 0.1f, 0.4f ) },
			corners.Select( corner => corner.TexCoords ).ToArray() );

		ParkLitter.Corners( corners, new Vector2( 476f, 221f ), 0f, new ParkGuestSprites.Region( 0f, 0f, 1f, 1f, 12, 13, 3, 14 ) );

		Assert.AreEqual( (475.6484375f, 477.0546875f, 218.8125f, 220.84375f), (corners[0].Position.X, corners[2].Position.X, corners[0].Position.Y, corners[2].Position.Y) );
	}

	/// <summary>
	/// With the game: a drop on (47,22) of kind 3 drawn from the park's own <c>SPR_LI</c> through the sprite pool, one quad, the
	/// set 2 picture (11 × 13, origin (0, -16)) across 476 to 477.2890625 and down 218.5 to 220.53125, 0.6 above the ground (none
	/// loaded: nought). Kind 7 on (41,21) shows set 6's one picture, picture 7 (17 × 12, origin (-5, -13)), across 414.4140625 to
	/// 416.40625. Every set's picture is the size and origin <c>ride-operation.md</c>'s table gives.
	/// </summary>
	/// <remarks><b>Mutations:</b> the litter bank left out of <c>BanksToPack</c> (nothing drawn); the set's number taken as its
	/// picture (kind 7 would show picture 6, the 34 × 21 cloud).</remarks>
	[TestMethod]
	public void ADropIsDrawnFromTheLitterBank()
	{
		FileSystem = GameData.Required();
		Log ??= new();

		var files = ParkGuestSprites.BanksIn( FileSystem, ParkLitter.Folder, ParkWorld.LitterSpriteKind );

		Assert.AreEqual( "SPR_LI", Path.GetFileNameWithoutExtension( files.Single() ).ToUpperInvariant() );

		var bank = new SpriteBankFile( files[0] );
		var pack = new SpritePackFile( Path.ChangeExtension( files[0], ".TPC" ) );
		var expected = new (int W, int H, int Ox, int Oy)[] { (10, 7, 0, -8), (12, 13, -3, -14), (11, 13, 0, -16), (11, 10, -2, -13),
			(17, 14, -6, -16), (9, 8, 1, -10), (17, 12, -5, -13), (34, 21, -13, -23) };

		for ( var kind = 1; kind <= 8; ++kind )
		{
			var picture = pack.Pictures[bank.Sets[ParkWorld.LitterSlot( kind )].First];

			Assert.AreEqual( expected[kind - 1], (picture.Width, picture.Height, -picture.OriginX, -picture.OriginY), $"kind {kind}" );
		}

		var shipped = Read( FileSystem.ReadAllBytes( ShippedPark ) );
		var art = new ParkGuestSprites.GuestArt(
			pictures => (new NoAtlas(), [.. pictures.Select( picture => new ParkGuestSprites.Region( 0f, 0f, 1f, 1f, picture.Width, picture.Height, picture.OriginX, picture.OriginY ) )]),
			( _, _ ) => { } );
		var pool = new ParkGuestSprites( "jungle", shipped, null, art );
		var state = new ParkState( shipped );

		pool.WriteLitter( null );
		Assert.AreEqual( 0, pool.LitterDrawn.Count, "the shipped park holds no litter" );

		Assert.IsTrue( state.DropLitter( 47, 22, 3 ) );
		pool.WriteLitter( null );

		var (x, y, corners) = pool.LitterDrawn.Single();

		Assert.AreEqual( (47, 22), (x, y) );
		CollectionAssert.AreEqual( new[]
		{
			new Vector3( 476f, 218.5f, 0.6f ), new Vector3( 477.2890625f, 218.5f, 0.6f ),
			new Vector3( 477.2890625f, 220.53125f, 0.6f ), new Vector3( 476f, 220.53125f, 0.6f )
		}, corners );

		Assert.IsTrue( state.DropLitter( 41, 21, 7 ) );
		pool.WriteLitter( null );

		var sick = pool.LitterDrawn.Single( drawn => (drawn.X, drawn.Y) == (41, 21) ).Corners;

		Assert.AreEqual( (414.4140625f, 416.40625f, 209.96875f, 211.84375f), (sick[0].X, sick[2].X, sick[0].Y, sick[2].Y) );
	}

	private sealed class NoAtlas : IPictureAtlas
	{
		public void Delete()
		{
		}
	}

	/// <summary>
	/// With the game: the shipped park written with its own people and one drop on (47,22) of kind 3 holds, read back, that
	/// cell's litter 3, collector nought, no particle and a sprite slot that names a sprite of kind 11, bank 0, set 2, frame 0,
	/// at (476, 0, 221), on program 1402 resting on 1409, run (state 2), flags <c>0x80000</c> and the set's frames a direction;
	/// the slot is the lowest the file leaves empty. A park state made from that file draws the litter where the sprite stands,
	/// and the file read once more is written again unchanged in its litter: the drop is the file's now.
	/// </summary>
	/// <remarks><b>Mutations:</b> the cell's sprite slot not written; the flags not written.</remarks>
	[TestMethod]
	public void ADropIsWrittenWithItsSpriteAndReadBack()
	{
		FileSystem = GameData.Required();
		Log ??= new();

		var shipped = Read( FileSystem.ReadAllBytes( ShippedPark ) );
		var cell = Cell( 47, 22 );

		Assert.IsTrue( shipped.Cells[cell].IsMapped && shipped.Cells[cell].Litter == 0, "(47,22) is a cell with a map record and no litter" );

		var running = new ParkState( shipped );

		Assert.IsTrue( running.DropLitter( 47, 22, 3 ) );

		var dropped = running.DroppedLitter( shipped, kind => kind == 3 ? 1 : null );

		Assert.AreEqual( new ParkWorld.WrittenLitter( cell, 3, 476f, 221f, 1 ), dropped.Single() );

		var people = new ParkPeople( shipped );
		var body = ParkFileWriter.Body( shipped, new ParkFileWriter.Running( shipped.GameTick, false, 0, 0, shipped.Camera.Saved!.Value,
			People: people.Written( Level.WrittenThings( shipped ).Contains ), Litter: dropped ), out var report );
		var written = new ParkWorld( body );

		Assert.IsNull( written.Problem );
		Assert.IsTrue( written.ClosedOnTrailer && written.ClosedOnSpriteTrailer );
		Assert.AreEqual( 1, report!.Value.Litter );

		var slot = shipped.EmptySpriteSlots( 1, new HashSet<int>() )[0];
		var back = written.Cells[cell];

		Assert.AreEqual( (3, (ushort)0, slot, 0), (back.Litter, back.LitterCollector, back.LitterSprite, back.LitterParticle) );
		Assert.AreEqual( shipped.Sprites.Count + 1, written.Sprites.Count );

		var sprite = written.Sprites.Single( held => held.Slot == slot );

		Assert.AreEqual( new ParkWorld.Sprite( slot, 11, 0, 2, 476f, 0f, 221f, 0, 0, 0xff, 2, 1402, 1409 ), sprite );
		Assert.AreEqual( 0x80000, written.SpriteFlagsOf( slot ) );
		Assert.AreEqual( 1, written.SpriteStateWordsOf( slot ).FramesPerDirection );
		Assert.AreEqual( 0, Enumerable.Range( 0, ParkWorld.MapSize * ParkWorld.MapSize ).Count( i => i != cell && written.Cells[i].Litter != shipped.Cells[i].Litter ),
			"no other cell's litter moves" );

		// Loaded: the litter is the file's, drawn where its sprite stands, and not written again as a drop.
		var loaded = new ParkState( written );
		var again = loaded.CellAt( 47, 22 );

		Assert.AreEqual( (3, true, false, 476f, 221f), (again.Litter, again.LitterSprite, again.LitterDropped, again.LitterX, again.LitterY) );
		Assert.AreEqual( 0, loaded.DroppedLitter( written ).Count );
		Assert.IsFalse( loaded.LitterDiffersFrom( written ) );
		Assert.AreEqual( (47, 22, 2, new Vector2( 476f, 221f )), ParkLitter.Littered( loaded ).Single() );
	}

	/// <summary>
	/// The level counts <c>SAVE_PARK_RUNNING_LITTER</c> only where the drop is not written: with no people to write it with (a
	/// park state alone), a drop is counted; with the people, it is written and not counted.
	/// </summary>
	[TestMethod]
	public void ADropIsCountedOnlyWhereItIsNotWritten()
	{
		var state = new ParkState( null );
		var clean = new ParkState( null );

		Assert.IsTrue( state.DropLitter( 47, 22, 3 ) );

		var park = DispatchProxy.Create<IParkInitialState, CellsOnly>();

		Assert.IsTrue( state.LitterDiffersFrom( park ), "no people: counted" );
		Assert.IsFalse( state.LitterDiffersFrom( park, droppedWritten: true ), "written with the people: not counted" );
		Assert.IsFalse( clean.LitterDiffersFrom( park ) );
	}

	/// <summary>A park whose cells are all clean, and nothing else.</summary>
	private class CellsOnly : DispatchProxy
	{
		private static readonly ParkWorld.MapCell[] cells = new ParkWorld.MapCell[ParkWorld.MapSize * ParkWorld.MapSize];

		protected override object? Invoke( MethodInfo? method, object?[]? args ) => method!.Name == "get_Cells"
			? cells
			: method.ReturnType.IsValueType && method.ReturnType != typeof( void ) ? System.Activator.CreateInstance( method.ReturnType ) : null;
	}
}
