using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.Files;
using System;
using System.IO;
using System.Numerics;

namespace OpenTPW.Tests;

/// <summary>
/// Each ground cell is cut on its own diagonal: the terrain pass reads the cell's flag <c>0x0002</c> and the height under the
/// park camera's point reads <c>0x0004</c> (docs/exe/park-engine.md, "The triangle diagonal"; Q398).
/// </summary>
[TestClass]
public class ParkCellDiagonalTests
{
	/// <summary>The cell's four corners as the vertices are laid out: (x, y), (x + 1, y), (x + 1, y + 1), (x, y + 1).</summary>
	private static readonly Vector2[] Corners = [new( 0, 0 ), new( 1, 0 ), new( 1, 1 ), new( 0, 1 )];

	private static uint[] ElementsFor( ushort flags )
	{
		var elements = new uint[6];
		Assert.AreEqual( 6, ParkGround.PutCellElements( elements, 0, 0, flags ) );
		return elements;
	}

	/// <summary>
	/// <b>A cell with <c>0x0002</c> is cut from its near corner to its far one, and one without from its along-x corner to its along-y
	/// one</b>, whatever else the word carries (the mirror <c>0x40</c>, a turn <c>0x20</c>, the path cell's <c>0x80</c>), and both
	/// triangles face the same way.
	/// </summary>
	/// <remarks><b>Mutations:</b> the flag test inverted; <c>== 0x2</c> for <c>&amp; 0x2</c> (fails on 0x42 and 0x62); <c>0x4</c> read for
	/// <c>0x2</c>; the second triangle of the other diagonal wound the other way.</remarks>
	[TestMethod]
	public void ACellIsCutOnTheDiagonalItsFlagNames()
	{
		foreach ( var flags in new ushort[] { 0x02, 0x42, 0x62, 0x82, 0x06 } )
			CollectionAssert.AreEqual( new uint[] { 0, 1, 2, 0, 2, 3 }, ElementsFor( flags ), $"0x{flags:x}" );

		foreach ( var flags in new ushort[] { 0x04, 0x44, 0x64, 0x84, 0x00 } )
			CollectionAssert.AreEqual( new uint[] { 0, 1, 3, 1, 2, 3 }, ElementsFor( flags ), $"0x{flags:x}" );

		foreach ( var flags in new ushort[] { 0x02, 0x04 } )
		{
			var elements = ElementsFor( flags );

			for ( var triangle = 0; triangle < 6; triangle += 3 )
			{
				var a = Corners[elements[triangle]];
				var b = Corners[elements[triangle + 1]];
				var c = Corners[elements[triangle + 2]];

				Assert.IsTrue( ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X)) > 0f, $"0x{flags:x} triangle {triangle / 3} winds the other way" );
			}
		}
	}

	/// <summary>
	/// <b>The console's <c>cut old</c> switch cuts every cell from (x, y) to (x + 1, y + 1)</b>, as this game did before Q398, whatever the flag
	/// says, and <c>cut new</c> puts the flag back in charge.
	/// </summary>
	/// <remarks><b>Mutation:</b> the switch not read in <c>SplitsNearToFar</c> fails here.</remarks>
	[TestMethod]
	public void TheDebugSwitchCutsEveryCellTheOldWay()
	{
		try
		{
			ParkGround.CutByFlag = false;
			CollectionAssert.AreEqual( new uint[] { 0, 1, 2, 0, 2, 3 }, ElementsFor( 0x44 ) );
			CollectionAssert.AreEqual( new uint[] { 0, 1, 2, 0, 2, 3 }, ElementsFor( 0x42 ) );
		}
		finally
		{
			ParkGround.CutByFlag = true;
		}

		CollectionAssert.AreEqual( new uint[] { 0, 1, 3, 1, 2, 3 }, ElementsFor( 0x44 ) );
	}

	/// <summary>The element offset is advanced by six and the vertices named are the cell's own, whatever corner they start at.</summary>
	[TestMethod]
	public void ACellsElementsStartWhereTheyAreAskedAndNameItsOwnVertices()
	{
		var elements = new uint[18];
		var next = ParkGround.PutCellElements( elements, 6, 8, 0x04 );

		Assert.AreEqual( 12, next );
		CollectionAssert.AreEqual( new uint[] { 8, 9, 11, 9, 10, 11 }, elements[6..12] );
		Assert.AreEqual( 0u, elements[0] );
		Assert.AreEqual( 0u, elements[12] );
	}

	private static HeightfieldFile Landscape( string theme )
	{
		using var stream = new MemoryStream( GameData.Required().ReadAllBytes( $"levels/{theme}/terrain/base.MD2" ) );
		return new HeightfieldFile( stream );
	}

	/// <summary>
	/// <b>The drawn diagonal and the height's are the same one</b> on every ground cell of the four parks: exactly one of <c>0x0002</c>
	/// and <c>0x0004</c> (FileFormats <c>models.md</c>, "A cell's diagonal"), so the ground this game draws and the height the camera
	/// stands on never disagree about a cell.
	/// </summary>
	[TestMethod]
	public void EveryGroundCellCarriesExactlyOneOfTheTwoDiagonalFlags()
	{
		var drawnOther = 0;

		foreach ( var theme in new[] { "jungle", "fantasy", "hallow", "space" } )
		{
			var field = Landscape( theme );

			for ( var y = 0; y < field.CellsY; ++y )
			{
				for ( var x = 0; x < field.CellsX; ++x )
				{
					var flags = field.FlagsAt( x, y );

					if ( (flags & 0x1) != 0 )
						continue;

					Assert.AreNotEqual( (flags & 0x2) != 0, (flags & 0x4) != 0, $"{theme} ({x},{y}) 0x{flags:x}" );

					if ( !ParkGround.SplitsNearToFar( flags ) )
						++drawnOther;
				}
			}
		}

		Assert.IsTrue( drawnOther > 1000, $"the parks cut cells the other way ({drawnOther})" );
	}

	/// <summary>
	/// <b>The camera's height is the plane of the triangle the cell's <c>0x4</c> picks, on every cell</b> (<c>FUN_00452560</c>, no
	/// <c>0x800</c> test): four points of a bent cell of each kind, on both sides of its diagonal and at its corners, against the plane
	/// through three corners solved by the normal; and bilinear <see cref="HeightfieldFile.HeightAtWorld"/> is not it on a bent cell.
	/// </summary>
	/// <remarks><b>Mutations:</b> the flag test inverted; the comparison reversed; <c>0x800</c> required; <c>x</c> and <c>y</c> swapped.</remarks>
	[TestMethod]
	public void TheCamerasHeightIsThePlaneOfTheTriangleTheCellsFlagPicks()
	{
		static float OnPlane( Vector3 a, Vector3 b, Vector3 c, float x, float y )
		{
			var normal = Vector3.Cross( b - a, c - a );
			return a.Z - (((normal.X * (x - a.X)) + (normal.Y * (y - a.Y))) / normal.Z);
		}

		var field = Landscape( "jungle" );
		var tested = new bool[2];
		var bilinearDiffers = 0;

		for ( var y = 0; y < field.CellsY; ++y )
		{
			for ( var x = 0; x < field.CellsX; ++x )
			{
				var h = (field.HeightAt( x, y ), field.HeightAt( x + 1, y ), field.HeightAt( x, y + 1 ), field.HeightAt( x + 1, y + 1 ));
				var twist = (h.Item1 + h.Item4) - (h.Item2 + h.Item3);
				var other = (field.FlagsAt( x, y ) & 0x4) != 0;

				if ( MathF.Abs( twist ) < 1f )
					continue;

				tested[other ? 1 : 0] = true;

				Vector3 Corner( int cx, int cy ) => new( cx * field.CellSizeX, cy * field.CellSizeY, field.HeightAt( cx, cy ) );
				var (c00, c10, c01, c11) = (Corner( x, y ), Corner( x + 1, y ), Corner( x, y + 1 ), Corner( x + 1, y + 1 ));

				foreach ( var (u, v) in new[] { (0.75f, 0.2f), (0.2f, 0.75f), (0.9f, 0.7f), (0.1f, 0.3f) } )
				{
					var at = new Vector2( (x + u) * field.CellSizeX, (y + v) * field.CellSizeY );
					var expected = !other
						? (v <= u ? OnPlane( c00, c10, c11, at.X, at.Y ) : OnPlane( c00, c01, c11, at.X, at.Y ))
						: (1f - v <= u ? OnPlane( c01, c11, c10, at.X, at.Y ) : OnPlane( c00, c10, c01, at.X, at.Y ));

					Assert.AreEqual( expected, field.CameraHeight( at.X, at.Y ), 0.002f, $"cell ({x},{y}) other {other} at ({u},{v})" );

					if ( MathF.Abs( field.HeightAtWorld( at.X, at.Y ) - expected ) > 0.05f )
						++bilinearDiffers;
				}
			}
		}

		Assert.IsTrue( tested[0] && tested[1], "the jungle has a bent cell of each diagonal" );
		Assert.IsTrue( bilinearDiffers > 0, "the bilinear height differs from the camera's on bent cells, so the test can tell them apart" );
	}
}
