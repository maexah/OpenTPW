using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The cells a built thing stands on, where it ends up standing on them, and the queue that leads to it.
/// These read real game files and are skipped where there is no installation: see <see cref="GameData"/>.
/// </summary>
[TestClass]
public class ParkFootprintTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	private const string ShippedPark = "levels/jungle/Easymode.TPWI";

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( ShippedPark ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>
	/// Why the ground has to skip these cells rather than drawing under everything.
	///
	/// <para>
	/// A footprint cell is <b>ordinary drawn ground</b> in the model - it carries a real texture index,
	/// not the 0 that means "the scenery covers this" - while the item standing on it opens its own model
	/// with a flat floor plate as wide as the whole footprint. Left to itself the ground builds grass
	/// across the same cells at the same heights, and the two fight for the same depth. The grass won,
	/// which is what left a shop standing on bare grass.
	/// </para>
	/// <para>
	/// The count is the load-bearing part: 44 cells over three types, which is exactly the eleven placed
	/// objects' footprints with nothing left over.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheCellsUnderBuiltThingsAreDrawnGroundAndAreLeftToThem()
	{
		HeightfieldFile field;

		using ( var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/terrain/base.MD2" ) ) )
			field = new HeightfieldFile( stream );

		var world = World();
		var built = 0;
		var queued = 0;

		for ( var y = 0; y < field.CellsY; ++y )
		{
			for ( var x = 0; x < field.CellsX; ++x )
			{
				var cell = world.CellAt( x, y );

				if ( ParkObjects.CoversGround( cell ) )
				{
					Assert.AreNotEqual( 0, field.TextureAt( x, y ),
						$"the footprint cell at ({x},{y}) should be drawn ground, which is why it has to be skipped" );
					++built;
				}

				if ( !ParkQueues.IsQueue( cell ) )
					continue;

				Assert.AreNotEqual( 0, field.TextureAt( x, y ),
					$"the queue cell at ({x},{y}) should be drawn ground too" );
				++queued;
			}
		}

		Assert.AreEqual( 44, built, "cells the park's eleven objects stand on - 35 of type 4, 8 of type 9, 1 of type 10" );
		Assert.AreEqual( 4, queued, "queue cells" );
	}

	/// <summary>
	/// Where a thing anchored on a cell actually stands, against the cells the save marks under it.
	///
	/// <para>
	/// These two statements are made by different parts of the file and neither is derived from the other:
	/// an object record gives an anchor cell and an angle, and the map cells separately mark which ground
	/// each built thing covers. They only agree if the footprint is turned about the middle of its
	/// <b>anchor cell</b> and the stored angle turns the opposite way to a positive rotation - which is
	/// what <see cref="ParkObjects.OriginFor"/> and <see cref="ParkObjects.Turn"/> do.
	/// </para>
	/// <para>
	/// The staff room and the fountain are the whole of the evidence, because they are the only rotated
	/// things in the park with a footprint bigger than one cell. Turning the other way puts the fountain
	/// on (55,19)..(57,21); turning about the footprint's own middle puts it somewhere else again. Both
	/// wrong answers are cells the save does not mark.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ABuiltThingCoversTheCellsItsParkMarksForIt()
	{
		// anchor cell, angle, footprint, and the cells the save marks under it.
		var placements = new[]
		{
			(Name: "staff room", X: 58, Y: 16, Angle: 90, Width: 2, Depth: 2, X0: 58, Y0: 15, X1: 59, Y1: 16),
			(Name: "fountain", X: 57, Y: 19, Angle: 90, Width: 3, Depth: 3, X0: 57, Y0: 17, X1: 59, Y1: 19),
			(Name: "belly bounce", X: 51, Y: 23, Angle: 0, Width: 3, Depth: 4, X0: 51, Y0: 23, X1: 53, Y1: 26),
			(Name: "jungle spray", X: 51, Y: 30, Angle: 0, Width: 3, Depth: 3, X0: 51, Y0: 30, X1: 53, Y1: 32),
			(Name: "drinks shop", X: 43, Y: 30, Angle: 0, Width: 2, Depth: 2, X0: 43, Y0: 30, X1: 44, Y1: 31),
			(Name: "a toilet", X: 55, Y: 16, Angle: 270, Width: 1, Depth: 1, X0: 55, Y0: 16, X1: 55, Y1: 16),
		};

		foreach ( var placed in placements )
		{
			var covered = Covers( placed.X, placed.Y, placed.Angle, placed.Width, placed.Depth );

			Assert.AreEqual( (placed.X0, placed.Y0, placed.X1, placed.Y1), covered,
				$"the {placed.Name}, anchored at ({placed.X},{placed.Y}) and turned {placed.Angle}" );
		}

		// And the marks themselves, so the expectations above are the save's word rather than this test's.
		var world = World();

		foreach ( var placed in placements )
		{
			for ( var y = placed.Y0; y <= placed.Y1; ++y )
			{
				for ( var x = placed.X0; x <= placed.X1; ++x )
				{
					Assert.IsTrue( ParkObjects.CoversGround( world.CellAt( x, y ) ),
						$"the save should mark ({x},{y}) as built on, under the {placed.Name}" );
				}
			}
		}
	}

	/// <summary>
	/// Which cells a footprint of this size, anchored here and turned this far, ends up on.
	///
	/// <para>
	/// <b>It asks the game rather than working it out, and it used to do the second.</b> This held its own
	/// copy of the corner-carrying arithmetic, written out beside <see cref="ParkObjects"/>'s - so the
	/// assertions below pinned the copy, and the shipped method could have broken without one of them
	/// noticing. The real one answers cells now instead of a line of log text, so there is no longer any
	/// reason to keep a second.
	/// </para>
	/// </summary>
	/// <remarks>
	/// Only <see cref="ParkItemCatalogue.Item.Width"/> and <see cref="ParkItemCatalogue.Item.Depth"/> are
	/// filled in: the footprint is the whole of what this arithmetic reads, and naming the rest would
	/// suggest otherwise. With no park built, cell size falls back to its default ten and the ground under
	/// the anchor reads flat - which is what the numbers below were measured against.
	/// </remarks>
	private static (int X0, int Y0, int X1, int Y1) Covers( int cellX, int cellY, int angle, int width, int depth )
		=> ParkObjects.FootprintAt(
			new ParkItemCatalogue.Item( 0, string.Empty, string.Empty, string.Empty, width, depth, null ),
			cellX, cellY, angle );

	/// <summary>
	/// A queue cell's tile index names a model, not a texture row - which is what the index of 5 on one of
	/// these four cells was saying all along, where the theme's <c>QueueTex</c> has only rows 0 to 3.
	///
	/// <para>
	/// The names below are the executable's own table, and the shapes they make are checked against the
	/// neighbour masks the cells separately store: the piece that meets the path is an <c>end</c>, the two
	/// with mask <c>0x44</c> - east and west, collinear - are <c>straight</c>s, and the one with mask
	/// <c>0x50</c>, south and west, is a <c>bend</c>. Four cells out of four.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheQueueIsBuiltFromTheGamesOwnPieces()
	{
		var world = World();

		// The angles are pinned as well as the pieces, because a piece of queue turns by 360 minus this
		// rather than by it - the opposite of a built thing - and the end piece's angle of 270 is the one
		// that shows it. What a rendered piece is facing cannot be asserted here; that is settled by the
		// art, on the torches queend carries along a single edge of its plate.
		var expected = new[]
		{
			(X: 49, Y: 22, Index: 5, Angle: 270, Mask: 0x44, Piece: "queend"),
			(X: 50, Y: 22, Index: 2, Angle: 270, Mask: 0x44, Piece: "questra"),
			(X: 51, Y: 22, Index: 2, Angle: 270, Mask: 0x44, Piece: "questra"),
			(X: 52, Y: 22, Index: 3, Angle: 90, Mask: 0x50, Piece: "quebnd2"),
		};

		foreach ( var (x, y, index, angle, mask, piece) in expected )
		{
			var cell = world.CellAt( x, y );

			Assert.IsTrue( ParkQueues.IsQueue( cell ), $"({x},{y}) should be a queue cell" );
			Assert.AreEqual( index, cell.TileIndex, $"the piece ({x},{y}) names" );
			Assert.AreEqual( angle, cell.TileAngle, $"the angle ({x},{y}) is saved at" );
			Assert.AreEqual( mask, cell.Neighbours, $"the shape ({x},{y}) makes" );

			var model = data.ReadAllBytes( $"levels/jungle/queue/{piece}.MD2" );

			Assert.IsTrue( model.Length > 0, $"the theme should ship {piece}.MD2 for ({x},{y})" );
		}

		Assert.AreEqual( expected.Length, world.Cells.Count( ParkQueues.IsQueue ),
			"queue cells in the whole park" );
	}
}
