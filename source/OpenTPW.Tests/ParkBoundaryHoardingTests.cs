using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The wall between the park and the land not bought (<see cref="ParkBoundaryHoarding"/>; <c>docs/exe/park.md</c>, "The
/// boundary hoarding's piece"): the hoarding table's rows, the pieces Lost Kingdom's file makes, and where each stands.
/// The game's files are read where there is an installation - see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ParkBoundaryHoardingTests
{
	/// <summary>A row with no name takes the named row above it (<c>0x00522ed2</c>); none past the table's 28.</summary>
	[TestMethod]
	[DataRow( 0, "ho_fos1" )]
	[DataRow( 1, "ho_end1" )]
	[DataRow( 3, "ho_fob1" )]
	[DataRow( 8, "ho_fos1" )]
	[DataRow( 11, "ho_fos4" )]
	[DataRow( 19, "ho_fob2" )]
	[DataRow( 22, "ho_fos2" )]
	[DataRow( 25, "ho_fos3" )]
	[DataRow( 26, "ho_fob3" )]
	[DataRow( 27, "ho_fob4" )]
	[DataRow( 28, null )]
	[DataRow( -1, null )]
	public void ARowDrawsTheNamedRowAtOrAboveIt( int row, string? model )
		=> Assert.AreEqual( model, ParkBoundaryHoarding.ModelOf( row ) );

	private static (ParkWorld World, ParkItemCatalogue Catalogue) Stock()
	{
		var data = GameData.Required();
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );

		return (new ParkWorld( new SaveReader( stream ).ReadFile() ), new ParkItemCatalogue( "jungle", data ));
	}

	/// <summary>The stock park's 143 records of items 17302 to 17323, one on each of its 143 anchors.</summary>
	[TestMethod]
	public void LostKingdomsFileMakesAPieceForEveryAnchor()
	{
		var (world, catalogue) = Stock();
		var pieces = ParkBoundaryHoarding.Read( world, catalogue );

		Assert.AreEqual( 143, ParkBoundaryHoarding.AnchorsOf( world ) );
		Assert.AreEqual( 143, pieces.Count );
		Assert.AreEqual( "ho_fob1 12, ho_fob2 8, ho_fos1 90, ho_fos2 19, ho_fos3 13, ho_fos4 1",
			string.Join( ", ", pieces.GroupBy( piece => piece.Model ).OrderBy( group => group.Key ).Select( group => $"{group.Key} {group.Count()}" ) ) );
		Assert.AreEqual( new ParkBoundaryHoarding.Piece( "ho_fos1", 2, 52, 17, 270 ), pieces[0] );
	}

	/// <summary>
	/// Each piece's two cells square, stood from its record's cell and angle, lands on its anchor's own: the type 25 cell
	/// and the three type 12 cells that name it (<c>FUN_00522f20</c> moves the cell with the turn).
	/// </summary>
	[TestMethod]
	public void EveryPieceStandsOnItsAnchorsFourCells()
	{
		var (world, catalogue) = Stock();
		var square = new ParkItemCatalogue.Item( 17302, "ho", "", "", 2, 2, null );
		var anchors = new System.Collections.Generic.HashSet<(int, int)>();

		foreach ( var piece in ParkBoundaryHoarding.Read( world, catalogue ) )
		{
			var (origin, turn) = ParkBoundaryHoarding.StandOf( piece, null );
			var (left, top, right, bottom) = ParkObjects.FootprintOf( square, origin, turn );

			Assert.AreEqual( (left + 1, top + 1), (right, bottom), $"{piece}" );
			Assert.AreEqual( 25, world.CellAt( left, top ).TrackType, $"{piece} on ({left},{top})" );

			foreach ( var (x, y) in new[] { (right, top), (left, bottom), (right, bottom) } )
			{
				Assert.AreEqual( 12, world.CellAt( x, y ).TrackType, $"{piece} on ({x},{y})" );
				Assert.AreEqual( MapStep.CellId( left, top ), world.CellAt( x, y ).TrackParentId, $"{piece} on ({x},{y})" );
			}

			anchors.Add( (left, top) );
		}

		Assert.AreEqual( 143, anchors.Count );
	}

	/// <summary>
	/// A piece stands halfway between the lowest and the highest ground vertex of its two cells square (flags <c>0x33a</c>,
	/// the 2 by 2 footprint at <c>0x008179f8</c>, <c>FUN_00466d70</c>), which is not its cell's corner where the ground falls.
	/// </summary>
	[TestMethod]
	public void APieceStandsMidwayUpTheGroundUnderItsSquare()
	{
		var (world, catalogue) = Stock();
		using var terrain = GameData.Required().OpenRead( "levels/jungle/terrain/base.MD2" )!;
		var field = new HeightfieldFile( terrain );
		var square = new ParkItemCatalogue.Item( 17302, "ho", "", "", 2, 2, null );
		var sloped = 0;

		foreach ( var piece in ParkBoundaryHoarding.Read( world, catalogue ) )
		{
			var (left, top, _, _) = ParkObjects.FootprintOf( square, ParkBoundaryHoarding.StandOf( piece, null ).Origin,
				ParkBoundaryHoarding.StandOf( piece, null ).Turn );
			var heights = Enumerable.Range( 0, 9 ).Select( i => field.HeightAt( left + (i % 3), top + (i / 3) ) ).ToArray();
			var (origin, _) = ParkBoundaryHoarding.StandOf( piece, field );

			Assert.AreEqual( (heights.Min() + heights.Max()) * 0.5f, origin.Z, 0.001f, $"{piece}" );

			if ( heights.Min() != heights.Max() && MathF.Abs( origin.Z - field.HeightAt( piece.CellX, piece.CellY ) ) > 0.01f )
				++sloped;
		}

		Assert.IsTrue( sloped > 0, "no piece stands where its square's middle and its cell's corner differ" );
	}

	private static (FreshPark Park, ParkItemCatalogue Catalogue) Fresh()
	{
		var data = GameData.Required();
		FileSystem = data;
		var catalogue = new ParkItemCatalogue( "jungle", data );

		return (new FreshPark( "jungle", new ParkBalance( "jungle", easyMode: false ), catalogue, data ), catalogue);
	}

	/// <summary>
	/// The original's own fresh Lost Kingdom park (Alexah's Full Simulation <c>restart.INTS</c>; the same in all 34 the
	/// original made, 2026-10-26): each anchor in its model slots' order as x, y, mask, direction, flags, row and turn, a
	/// straight's variant rows 22 and 25 written as its row 2, which its own roll of the generator chooses.
	/// </summary>
	private const string OriginalsFreshAnchors = ""
		+ "51,17,4,4,0,2,90 53,17,41,1,0,19,180 53,15,11,1,0,2,0 53,13,14,4,0,3,0 55,13,44,4,0,2,90 "
		+ "57,13,44,4,0,2,90 59,13,44,4,0,2,90 61,13,44,4,0,2,90 63,13,44,4,0,2,90 65,13,44,4,0,2,90 "
		+ "67,13,44,4,0,2,90 69,13,41,1,0,19,180 69,11,11,1,0,2,0 69,9,11,1,0,2,0 69,7,11,1,0,2,0 "
		+ "69,5,11,1,0,2,0 69,3,11,1,0,2,0 69,1,14,4,0,3,0 71,1,44,4,0,2,90 73,1,44,4,0,2,90 75,1,44,4,0,2,90 "
		+ "77,1,44,4,0,2,90 79,1,44,4,0,2,90 81,1,44,4,0,2,90 83,1,44,4,0,2,90 85,1,44,4,0,2,90 "
		+ "87,1,44,4,0,2,90 89,1,44,4,0,2,90 91,1,44,4,0,2,90 93,1,50,10,0,3,90 93,3,11,10,0,2,180 "
		+ "93,5,11,10,0,2,180 93,7,11,10,0,2,180 93,9,11,10,0,2,180 93,11,11,10,0,2,180 93,13,11,10,0,2,180 "
		+ "93,15,11,10,0,2,180 93,17,11,10,0,2,180 93,19,11,10,0,2,180 93,21,11,10,0,2,180 93,23,11,10,0,2,180 "
		+ "93,25,11,10,0,2,180 93,27,11,10,0,2,180 93,29,11,10,0,2,180 93,31,41,40,0,3,180 91,31,44,40,0,2,270 "
		+ "89,31,44,40,0,2,270 87,31,44,40,0,2,270 85,31,44,40,0,2,270 83,31,44,40,0,2,270 81,31,44,40,0,2,270 "
		+ "79,31,14,10,0,19,0 79,33,11,10,0,2,180 79,35,11,10,0,2,180 79,37,11,10,0,2,180 79,39,41,40,0,3,180 "
		+ "77,39,44,40,0,2,270 75,39,44,40,0,2,270 73,39,44,40,0,2,270 71,39,44,40,0,2,270 69,39,44,40,0,2,270 "
		+ "67,39,44,40,0,2,270 65,39,44,40,0,2,270 63,39,44,40,0,2,270 61,39,44,40,0,2,270 59,39,44,40,1,11,270 "
		+ "57,39,44,40,0,2,270 55,39,44,40,0,2,270 53,39,44,40,0,2,270 51,39,5,1,0,3,270 51,37,50,40,0,19,90 "
		+ "49,37,44,40,0,2,270 47,37,44,40,0,2,270 45,37,44,40,0,2,270 43,37,44,40,0,2,270 41,37,44,40,0,2,270 "
		+ "39,37,44,40,0,2,270 37,37,44,40,0,2,270 35,37,44,40,0,2,270 33,37,44,40,0,2,270 31,37,44,40,0,2,270 "
		+ "29,37,5,1,0,3,270 29,35,11,1,0,2,0 29,33,11,1,0,2,0 29,31,50,40,0,19,90 27,31,44,40,0,2,270 "
		+ "25,31,5,1,0,3,270 25,29,11,1,0,2,0 25,27,11,1,0,2,0 25,25,11,1,0,2,0 25,23,11,1,0,2,0 "
		+ "25,21,11,1,0,2,0 25,19,11,1,0,2,0 25,17,11,1,0,2,0 25,15,11,1,0,2,0 25,13,14,4,0,3,0 "
		+ "27,13,44,4,0,2,90 29,13,44,4,0,2,90 31,13,44,4,0,2,90 33,13,44,4,0,2,90 35,13,44,4,0,2,90 "
		+ "37,13,44,4,0,2,90 39,13,44,4,0,2,90 41,13,50,10,0,3,90 41,15,11,10,0,2,180 41,17,5,4,0,19,270 "
		+ "43,17,40,4,0,2,90";

	/// <summary>A park made fresh chooses each anchor's piece as the original's fresh park holds it, in the same order.</summary>
	[TestMethod]
	public void AFreshParksAnchorsTakeTheOriginalsPieces()
	{
		var (fresh, _) = Fresh();

		var made = string.Join( " ", fresh.Boundary.Select( anchor =>
			$"{anchor.X},{anchor.Y},{anchor.Mask:x},{anchor.Direction:x},{anchor.Flags},{(anchor.Row is 22 or 25 ? 2 : anchor.Row)},{anchor.Turn}" ) );

		Assert.AreEqual( OriginalsFreshAnchors, made );
	}

	/// <summary>
	/// A straight takes the variant its roll names unless it is the one given last, and rolls again; one blocked by the
	/// water (flag 1) takes row 11 and rolls nothing. From seed 1, the rows the decoded rule gives (a model of it outside
	/// this code, <c>q656/model.py</c>): three straights whose roll repeats the variant before stand plain.
	/// </summary>
	[TestMethod]
	public void AStraightsVariantIsRolledAndNeverRepeated()
	{
		var seed = 1u;
		var variants = new ParkBoundaryHoarding.Variants();
		var rows = Enumerable.Range( 0, 16 ).Select( at => ParkBoundaryHoarding.PieceFor( 0x44, 0x04, (ushort)(at == 5 ? 1 : 0), false,
			ref seed, variants ).Row ).ToArray();

		Assert.AreEqual( "2 22 2 2 22 11 2 2 2 2 2 22 2 2 25 2", string.Join( " ", rows ) );
		Assert.AreEqual( 1831881886u, seed );
	}

	/// <summary>
	/// Every kind of anchor the original's park files hold (ten files: its fresh parks of three themes, the stock park, two
	/// played ones): a gate's end looks itself up with its missing side, a straight walked down or left turns 180 more, a
	/// corner the walk turns into one way takes <c>ho_fob2</c>, a blocked straight row 11 and a blocked corner row 0x1a.
	/// </summary>
	[TestMethod]
	[DataRow( 0x04, 0x04, 0, true, 2, 90 )]
	[DataRow( 0x40, 0x04, 0, true, 2, 90 )]
	[DataRow( 0x11, 0x01, 0, false, 2, 0 )]
	[DataRow( 0x11, 0x10, 1, false, 11, 180 )]
	[DataRow( 0x44, 0x40, 1, false, 11, 270 )]
	[DataRow( 0x14, 0x04, 0, false, 3, 0 )]
	[DataRow( 0x14, 0x10, 0, false, 19, 0 )]
	[DataRow( 0x50, 0x40, 0, false, 19, 90 )]
	[DataRow( 0x50, 0x10, 0, false, 3, 90 )]
	[DataRow( 0x50, 0x10, 1, false, 0x1a, 90 )]
	[DataRow( 0x41, 0x01, 0, false, 19, 180 )]
	[DataRow( 0x41, 0x40, 0, false, 3, 180 )]
	[DataRow( 0x05, 0x04, 0, false, 19, 270 )]
	[DataRow( 0x05, 0x01, 0, false, 3, 270 )]
	public void AnAnchorTakesThePieceTheOriginalsFilesHold( int mask, int direction, int flags, bool gateEnd, int row, int turn )
	{
		var seed = 1u;

		Assert.AreEqual( (row, turn), ParkBoundaryHoarding.PieceFor( (byte)mask, (byte)direction, (ushort)flags, gateEnd, ref seed, new() ) );
	}

	/// <summary>
	/// The chooser's answers no park file to hand holds, read from <c>FUN_0053a540</c> alone: an end that is no gate's, the
	/// pair 0x40 with 0x14 that its compare of BL with itself lets through (<c>0x0053a8c4</c>), a blocked corner of a pair,
	/// three and four neighbours past their flags, flag 4, and flags 2 and 8 and 1 with 8 on a straight (each added).
	/// </summary>
	[TestMethod]
	[DataRow( 0x04, 0x04, 0, false, 1, 270 )]
	[DataRow( 0x14, 0x40, 0, false, 19, 0 )]
	[DataRow( 0x05, 0x04, 1, false, 0x1b, 270 )]
	[DataRow( 0x15, 0x01, 1, false, 5, 0 )]
	[DataRow( 0x55, 0x01, 8, false, 4, 0 )]
	[DataRow( 0x44, 0x04, 2, false, 14, 90 )]
	[DataRow( 0x44, 0x04, 8, false, 17, 90 )]
	[DataRow( 0x11, 0x01, 9, false, 26, 0 )]
	[DataRow( 0x10, 0x01, 4, false, 0x12, 0 )]
	public void AnAnchorTakesTheChoosersOtherAnswers( int mask, int direction, int flags, bool gateEnd, int row, int turn )
	{
		var seed = 1u;

		Assert.AreEqual( (row, turn), ParkBoundaryHoarding.PieceFor( (byte)mask, (byte)direction, (ushort)flags, gateEnd, ref seed, new() ) );
	}

	/// <summary>
	/// The boundary's 88 straights with clear flags each draw the park's generator once, and the park starts on what it
	/// became: from seed 1, the state 88 steps on (a model outside this code).
	/// </summary>
	[TestMethod]
	public void AFreshParkStartsOnTheGeneratorItsStraightsDrew()
	{
		var data = GameData.Required();
		FileSystem = data;
		var fresh = new FreshPark( "jungle", new ParkBalance( "jungle", easyMode: false ), new ParkItemCatalogue( "jungle", data ), data, seed: 1 );

		Assert.AreEqual( 88, fresh.Boundary.Count( anchor => anchor.Mask is 0x11 or 0x44 && anchor.Flags == 0 ) );
		Assert.AreEqual( 1360261985, fresh.RandomSeed );
	}

	/// <summary>A piece's record is its row's named item on the anchor moved by the turn, at 360 less it.</summary>
	[TestMethod]
	[DataRow( 2, 90, "ho_fos1", 2, 52, 17, 270 )]
	[DataRow( 22, 180, "ho_fos2", 20, 52, 18, 180 )]
	[DataRow( 25, 270, "ho_fos3", 23, 51, 18, 90 )]
	[DataRow( 11, 0, "ho_fos4", 9, 51, 17, 0 )]
	public void AnAnchorsRecordIsItsNamedRowMovedByItsTurn( int row, int turn, string model, int named, int x, int y, int angle )
		=> Assert.AreEqual( new ParkBoundaryHoarding.Piece( model, named, x, y, angle ),
			ParkBoundaryHoarding.PieceOf( new( 51, 17, 0x44, 0x04, 0, row, turn ) ) );

	/// <summary>
	/// A park made fresh stands a piece on every anchor, each on its anchor's four cells, and counts none unmade: the
	/// original's 10 <c>ho_fob1</c>, 6 <c>ho_fob2</c> and one <c>ho_fos4</c>, and 90 straights and gate ends among the
	/// three <c>ho_fos</c> models.
	/// </summary>
	[TestMethod]
	public void AFreshParkStandsAPieceOnEveryAnchor()
	{
		var (fresh, catalogue) = Fresh();
		var square = new ParkItemCatalogue.Item( 17302, "ho", "", "", 2, 2, null );
		var before = Counted( "BOUNDARY_HOARDING_UNMADE" );
		var wall = new ParkBoundaryHoarding( "jungle", fresh, catalogue );

		try
		{
			// A test has no renderer to make the models with, so the pieces are the chosen ones, not the ones standing.
			var pieces = fresh.Boundary.Select( ParkBoundaryHoarding.PieceOf ).ToList();

			Assert.AreEqual( 107, ParkBoundaryHoarding.AnchorsOf( fresh ) );
			Assert.AreEqual( 107, pieces.Count );
			Assert.AreEqual( 0, Counted( "BOUNDARY_HOARDING_UNMADE" ) - before );

			var models = pieces.GroupBy( piece => piece.Model ).ToDictionary( group => group.Key, group => group.Count() );
			Assert.AreEqual( (10, 6, 1), (models["ho_fob1"], models["ho_fob2"], models["ho_fos4"]) );
			Assert.AreEqual( 90, models["ho_fos1"] + models.GetValueOrDefault( "ho_fos2" ) + models.GetValueOrDefault( "ho_fos3" ) );

			foreach ( var piece in pieces )
			{
				var (origin, turn) = ParkBoundaryHoarding.StandOf( piece, null );
				var (left, top, _, _) = ParkObjects.FootprintOf( square, origin, turn );

				Assert.AreEqual( 25, fresh.CellAt( left, top ).TrackType, $"{piece} on ({left},{top})" );
			}
		}
		finally
		{
			wall.Delete();
		}
	}

	/// <summary>
	/// A park made fresh keeps each piece's family, row and rotation on its anchor's track record, as the original's retile
	/// does (<c>0x0053b02b</c>), so the record names the piece the walk chose and a square over it can be lifted by its file (Q777).
	/// </summary>
	/// <remarks><b>Mutation:</b> the record not written; the row or the turn dropped; the family left nought.</remarks>
	[TestMethod]
	public void AFreshParksAnchorRecordNamesItsChosenPiece()
	{
		var (fresh, _) = Fresh();

		foreach ( var anchor in fresh.Boundary )
		{
			var record = fresh.CellAt( anchor.X, anchor.Y );

			Assert.AreEqual( (ParkBoundaryHoarding.Family, anchor.Row, anchor.Turn), (record.TrackTileSet, record.TrackTileIndex, record.TrackTileAngle),
				$"({anchor.X},{anchor.Y})" );
			Assert.AreEqual( ParkBoundaryHoarding.PieceOf( anchor ), ParkBoundaryHoarding.PieceAt( anchor.X, anchor.Y, record ), $"({anchor.X},{anchor.Y})" );
		}
	}

	private static int Counted( string gap ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == gap ).Times;
}
