using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The cell <c>FUN_004fa990</c> reads for a position (<see cref="PeepBehaviour.OnACountingCell(FixedVector)"/>): the
/// thing's bytes <c>+5</c> and <c>+7</c> packed as they are, so a position off the 128 x 128 map reads a cell of another
/// row (Q1333; docs/exe/park-gate.md, "Which people count as inside").
/// </summary>
[TestClass]
public class CountingCellTests
{
	/// <summary>Every <see cref="ParkPeople"/> a test made goes with it, out of <see cref="Entity.All"/> and <c>Current</c>.</summary>
	[TestCleanup]
	public void LetThePeopleGo() => TestRun.DeleteEvery<ParkPeople>();

	private static ParkWorld Park( BaseFileSystem data )
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		return new ParkWorld( new SaveReader( stream ).ReadFile() );
	}

	private static int OffMap() => Unimplemented.Summary.FirstOrDefault( entry => entry.What == "COUNTING_CELL_OFF_MAP" ).Times;

	/// <summary>
	/// <b>The pack, by itself.</b> On the map a position reads its own cell; x 128 to 255 reads the next row's cell
	/// x - 128, a whole part below nought reads its low byte (x -1 is 255, so the next row's last cell); a y byte of 128
	/// or more is past the map's last record, and so is the last row's x 128.
	/// </summary>
	/// <remarks><b>Mutations:</b> off the map read as null (the old rule); the bytes read signed (x 130 reads row 4); x
	/// wrapped within its own row (x 130 reads (2, 5)).</remarks>
	[TestMethod]
	public void ThePackReadsTheCellItsTwoBytesName()
	{
		static (int X, int Y)? At( int x, int y ) => PeepBehaviour.CountingRecord( FixedVector.AtCell( x, y ) );

		Assert.AreEqual( (5, 9), At( 5, 9 ), "on the map, its own cell" );
		Assert.AreEqual( (127, 127), At( 127, 127 ), "the map's last cell" );
		Assert.AreEqual( (2, 6), At( 130, 5 ), "x 130 at row 5: row 6, x 2" );
		Assert.AreEqual( (127, 6), At( 255, 5 ), "x 255 at row 5: row 6, x 127" );
		Assert.AreEqual( (127, 6), PeepBehaviour.CountingRecord( new FixedVector( -1, 5 << 16 ) ), "x just below nought is byte 255" );
		Assert.IsNull( At( 128, 127 ), "the last row's x 128 is past the last record" );
		Assert.IsNull( At( 5, 128 ), "a y byte of 128" );
		Assert.IsNull( PeepBehaviour.CountingRecord( new FixedVector( 5 << 16, -1 ) ), "y just below nought is byte 255" );
	}

	/// <summary>
	/// <b>A guest off the map counts by the other row's cell.</b> At x 130 on row 5 the cell read is (2, 6): made a path
	/// it counts them, made an object's footprint (type 4) it does not, whatever (2, 5) and (2, 4) hold; past the last
	/// record nobody counts and the gap is counted.
	/// </summary>
	/// <remarks><b>Mutations:</b> off the map counting nobody (the old rule); reading the same row's (2, 5); the bytes
	/// read signed, (2, 4).</remarks>
	[TestMethod]
	[DoNotParallelize]
	public void AGuestOffTheMapCountsByTheCellOfTheNextRow()
	{
		var people = new ParkPeople( Park( FileSystem = GameData.Required() ) );
		var peep = people.Guests.Values.First();
		var state = people.State;

		void Type( int x, int y, int type ) => state.SetRecord( x, y, state.Record( x, y ) with { Type = type } );

		Type( 2, 4, 4 );
		Type( 2, 5, 4 );
		Type( 2, 6, 1 );
		peep.Navigator.Position = FixedVector.AtCell( 130, 5 );
		Assert.IsTrue( people.OnACountingCell( peep ), "(2, 6) is a path" );

		Type( 2, 4, 1 );
		Type( 2, 5, 1 );
		Type( 2, 6, 4 );
		Assert.IsFalse( people.OnACountingCell( peep ), "(2, 6) is a footprint, whatever (2, 5) and (2, 4) are" );

		var before = OffMap();
		peep.Navigator.Position = FixedVector.AtCell( 5, 128 );
		Assert.IsFalse( people.OnACountingCell( peep ), "past the last record" );
		Assert.AreEqual( before + 1, OffMap() );
	}

	/// <summary>
	/// <b>Placed content stands on the map.</b> The measure of Q1333: every person in the shipped Lost Kingdom park
	/// (the file's own cell bytes and the navigator's position) and the cells arrivals are made on (the bus stop's own,
	/// and the one two rows out where the larger vehicles drop them, <see cref="ParkPeople.ArrivalCell"/>) are on the 128 x 128 map, so nothing placed
	/// reaches the wrapped read at the start; a run measures the walking (docs/STATUS.md, Q1333).
	/// </summary>
	[TestMethod]
	public void EveryoneInTheShippedParkAndEveryArrivalCellIsOnTheMap()
	{
		var world = Park( FileSystem = GameData.Required() );
		static bool OnTheMap( int x, int y ) => x is >= 0 and < ParkWorld.MapSize && y is >= 0 and < ParkWorld.MapSize;

		Assert.AreEqual( 13, world.People.Count( person => person.Guest != null ), "the shipped park's guests" );
		Assert.AreEqual( 18, world.People.Count, "and its people of every kind" );

		foreach ( var person in world.People )
		{
			Assert.IsTrue( OnTheMap( person.RawX >> 8, person.RawY >> 8 ), $"thing {person.ThingId}'s bytes +5, +7" );
			var (x, y) = new FixedVector( person.Navigator.X, person.Navigator.Y ).Cell;
			Assert.IsTrue( OnTheMap( x, y ), $"thing {person.ThingId}'s navigator at ({x}, {y})" );
		}

		var stop = new ParkAdmission( new ParkBalance( "jungle", easyMode: true ), 0 ).BusStopB;
		foreach ( var vehicle in new[] { 0, 1, 2, 3 } )
		{
			var (x, y) = ParkPeople.ArrivalCell( stop, vehicle );
			Assert.IsTrue( OnTheMap( x, y ), $"vehicle {vehicle}'s arrival cell ({x}, {y})" );
		}
	}
}
