using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace OpenTPW.Tests;

/// <summary>
/// The rule that joins a newly laid path cell to the cells around it - <see cref="ParkPathNeighbours"/>.
///
/// <para>
/// <b>These lay cells ONE AT A TIME and assert what results, because that is the only honest way to
/// test this.</b> The original's pass is incremental and order-dependent, which is exactly why a sweep
/// over the finished park topped out at 73 of its 78 path cells and could never reach all of them. A
/// test that set up a finished arrangement and asked whether the mask "looked right" would be testing
/// a different function from the one the game runs.
/// </para>
/// <para>
/// They run against the shipped park so the cells around the test area are real ground rather than an
/// invented grid, and each begins by asserting that the ground really is bare - so picking a cell that
/// turned out to be built on would fail loudly instead of quietly testing nothing.
/// </para>
/// <para>
/// <b>That guard has already earned its place.</b> A first version of the third test used (30,10) and
/// (40,10), which are mType <b>7</b> rather than bare ground, and it failed on its own setup assertion
/// instead of quietly exercising nothing. <b>Measured, so the next edit need not guess: on y = 10 the
/// bare band runs x = 0..27</b>, and every cell these tests use sits inside it.
/// </para>
/// </summary>
[TestClass]
public class ParkPathNeighbourTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => data = GameData.Required();

	private ParkWorld World()
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	/// <summary>Stamps a cell as path the way the commit does, then joins it up.</summary>
	private static void Lay( ParkState state, ParkWorld park, int x, int y )
	{
		state.SetRecord( x, y, ParkState.CellFor( park, x, y ) with { Type = CellEdge.Path } );

		ParkPathNeighbours.LinkPath( state, park, x, y );
	}

	private static byte MaskAt( ParkWorld park, int x, int y )
		=> ParkState.CellFor( park, x, y ).Neighbours;

	/// <summary>
	/// A run of three laid west to east joins up as it goes, and the middle cell ends up drawing a
	/// straight that runs east-west.
	///
	/// <para>
	/// This is the whole mechanism end to end: the link pass produces the mask, and the tile table
	/// turns that mask into art. <c>docs/exe/park.md</c>, "`mNeighbours`, `mDirection` and the
	/// compass", states the check from the other side - "`0x44` (E+W) is a horizontal straight at angle 90" - so a turn the wrong
	/// way would show as paths crossing their own junctions.
	/// </para>
	/// </summary>
	[TestMethod]
	public void ARunOfThreeJoinsUpAsItIsLaid()
	{
		var park = World();
		var state = new ParkState( park );

		// Bare ground, well clear of the park's own paths (which run x 39..57, y 15..29).
		foreach ( var x in new[] { 10, 11, 12 } )
		{
			Assert.AreEqual( 0, ParkState.CellFor( park, x, 10 ).Type, $"({x},10) should be bare ground to begin with" );
			Assert.AreEqual( 0, MaskAt( park, x, 10 ), $"({x},10) should start with no connections" );
		}

		Lay( state, park, 10, 10 );

		Assert.AreEqual( 0, MaskAt( park, 10, 10 ), "the first cell of a run has nothing to join to yet" );

		Lay( state, park, 11, 10 );

		// 0x40 is WEST and 0x04 is EAST in the outward sense - the mirror of CellEdge.BitFor, which
		// answers about the cell being entered. Both are right; mixing them inverts every answer.
		Assert.AreEqual( 0x40, MaskAt( park, 11, 10 ), "the second cell joins back to the first, westward" );
		Assert.AreEqual( 0x04, MaskAt( park, 10, 10 ), "and the first gains the link eastward - the pair is symmetric" );

		Lay( state, park, 12, 10 );

		Assert.AreEqual( 0x04, MaskAt( park, 10, 10 ), "the far end is untouched by the third cell" );
		Assert.AreEqual( 0x44, MaskAt( park, 11, 10 ), "the middle now connects both ways" );
		Assert.AreEqual( 0x40, MaskAt( park, 12, 10 ), "and the new end joins back westward" );

		var (set, index, angle) = ParkPathTiles.TileFor( CellEdge.Path, MaskAt( park, 11, 10 ), 0 );

		Assert.AreEqual( ParkPaths.PathTileSet, set );
		Assert.AreEqual( 2, index, "mask 0x44 is a straight" );
		Assert.AreEqual( 90, angle, "and it runs east-west, not north-south" );
	}

	/// <summary>
	/// A queue cell beside a new path <b>never forms a link</b>, and that is measured rather than
	/// assumed: the original's cardinal block for an mType 3 neighbour only lets it retile.
	/// </summary>
	[TestMethod]
	public void AQueueCellNeverFormsANewLink()
	{
		var park = World();
		var state = new ParkState( park );

		Assert.AreEqual( 0, ParkState.CellFor( park, 20, 10 ).Type, "(20,10) should be bare ground" );
		Assert.AreEqual( 0, ParkState.CellFor( park, 21, 10 ).Type, "(21,10) should be bare ground" );

		// A queue cell to the east of where the path is about to go.
		state.SetRecord( 21, 10, ParkState.CellFor( park, 21, 10 ) with
		{
			Type = ParkRideChoice.QueueCellType,
			Direction = 0x40
		} );

		Lay( state, park, 20, 10 );

		Assert.AreEqual( 0, MaskAt( park, 20, 10 ),
			"a path laid beside a queue gains no link toward it - only a path, a ride entrance or a ride exit connects" );
		Assert.AreEqual( 0, MaskAt( park, 21, 10 ), "and the queue gains nothing either" );
	}

	/// <summary>
	/// A ride's own cells connect <b>only on their direction byte</b>, and entrance and exit test
	/// opposite senses of it - an mType 9 on the bit pointing at it, an mType 10 on the opposite bit.
	/// </summary>
	[TestMethod]
	public void ARideEndConnectsOnlyTheWayItFaces()
	{
		var park = World();
		var state = new ParkState( park );

		Assert.AreEqual( 0, ParkState.CellFor( park, 2, 10 ).Type, "(2,10) should be bare ground" );

		// An entrance east of the path, facing the wrong way: the path steps EAST (0x04) to reach it,
		// and an mType 9 links only when its own direction carries that same bit.
		state.SetRecord( 3, 10, ParkState.CellFor( park, 3, 10 ) with
		{
			Type = CellEdge.RideEnd,
			Direction = 0x01
		} );

		Lay( state, park, 2, 10 );

		Assert.AreEqual( 0, MaskAt( park, 2, 10 ), "an entrance facing elsewhere does not connect" );

		// Now one facing the right way. A second overlay, so the first half's records cannot answer:
		// ParkState.Current is whichever was built last, and MaskAt reads through it.
		var second = new ParkState( park );

		second.SetRecord( 7, 10, ParkState.CellFor( park, 7, 10 ) with
		{
			Type = CellEdge.RideEnd,
			Direction = 0x04
		} );

		Lay( second, park, 6, 10 );

		Assert.AreEqual( 0x04, MaskAt( park, 6, 10 ), "an entrance whose direction carries the bit does connect" );
	}
}
