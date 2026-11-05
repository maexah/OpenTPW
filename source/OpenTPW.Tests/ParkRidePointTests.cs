using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The point inside the exit and entry cell a guest is put down at and taken to stand at: <c>FUN_004dedf0</c>'s byte,
/// turned by the thing's angle (<c>0x004df0ae</c>..<c>0x004df11d</c>), and the exit put-down that uses it
/// (<c>docs/exe/ride-operation.md</c>, "Leaving a ride").
/// </summary>
[TestClass]
public class ParkRidePointTests
{
	public TestContext TestContext { get; set; } = null!;

	/// <summary>
	/// <b>0.5 is <c>0x7f</c> at every turn</b> (127.5 cut, not rounded), and an off-centre pair is carried round the way
	/// the compare chain carries it: 90 gives <c>(y, 1 - x)</c>, 180 <c>(1 - x, 1 - y)</c>, 270 <c>(1 - y, x)</c>, and any
	/// other angle <c>(x, x)</c>. The values are ones a wrong rule gets wrong: 0.1 and 0.9 do not swap into each other
	/// (25 and 229), and x differs from y.
	/// </summary>
	/// <remarks><b>Mutations:</b> rounding rather than cutting; the 90 and 270 rows exchanged; no <c>1 -</c>; the fall-through returning (x, y); the 270 path's or 180's first slot cut to a float.</remarks>
	[TestMethod]
	public void TheByteIsTheFractionTimes255CutAndTheAngleTurnsThePair()
	{
		foreach ( var angle in new[] { 0, 90, 180, 270 } )
			Assert.AreEqual( ((byte)127, (byte)127), ParkRidePoint.Bytes( 0.5f, 0.5f, angle ), $"0.5 at {angle}" );

		Assert.AreEqual( ((byte)127, (byte)25 ), ParkRidePoint.Bytes( 0.5f, 0.1f, 0 ) );
		Assert.AreEqual( ((byte)25, (byte)127 ), ParkRidePoint.Bytes( 0.5f, 0.1f, 90 ) );
		Assert.AreEqual( ((byte)127, (byte)229 ), ParkRidePoint.Bytes( 0.5f, 0.1f, 180 ) );
		Assert.AreEqual( ((byte)229, (byte)127 ), ParkRidePoint.Bytes( 0.5f, 0.1f, 270 ) );

		// The second slot's 1 - f is stored as a float before the multiply at 90 and 180 (0x004df0dc, 0x004df119): in
		// doubles 1 - 0.2f and 1 - 0.4f would give 203 and 152.
		Assert.AreEqual( ((byte)127, (byte)153), ParkRidePoint.Bytes( 0.4f, 0.5f, 90 ) );
		Assert.AreEqual( ((byte)127, (byte)204), ParkRidePoint.Bytes( 0.2f, 0.5f, 90 ) );
		Assert.AreEqual( ((byte)127, (byte)153), ParkRidePoint.Bytes( 0.5f, 0.4f, 180 ) );

		// The 270 path's 1 - y and 180's first slot 1 - x stay in the x87 register, which is 53-bit (0x27f read at
		// 0x004df0ae in the running original), so they are not cut to a float: 24-bit would give 204 and 153.
		Assert.AreEqual( ((byte)203, (byte)127), ParkRidePoint.Bytes( 0.5f, 0.2f, 270 ) );
		Assert.AreEqual( ((byte)152, (byte)127), ParkRidePoint.Bytes( 0.5f, 0.4f, 270 ) );
		Assert.AreEqual( ((byte)203, (byte)127), ParkRidePoint.Bytes( 0.2f, 0.5f, 180 ) );
		Assert.AreEqual( ((byte)152, (byte)127), ParkRidePoint.Bytes( 0.4f, 0.5f, 180 ) );

		Assert.AreEqual( ((byte)76, (byte)76), ParkRidePoint.Bytes( 0.3f, 0.9f, 45 ), "an angle off the four leaves x in both" );
		Assert.AreEqual( ((byte)76, (byte)229), ParkRidePoint.Bytes( 0.3f, 0.9f, 0 ) );
		Assert.AreEqual( ((byte)229, (byte)178), ParkRidePoint.Bytes( 0.3f, 0.9f, 90 ), "(y, 1 - x) = (0.9, 0.7)" );
		Assert.AreEqual( ((byte)178, (byte)25), ParkRidePoint.Bytes( 0.3f, 0.9f, 180 ), "(1 - x, 1 - y) = (0.7, 0.1)" );
		Assert.AreEqual( ((byte)25, (byte)76), ParkRidePoint.Bytes( 0.3f, 0.9f, 270 ), "(1 - y, x) = (0.1, 0.3)" );
	}

	/// <summary>
	/// <b>The position is the cell and the byte as the original makes them</b> (<c>FUN_004fa930</c>: the 16-bit word,
	/// the cell in its high byte, shifted left 8), so the Belly Bounce's 52 and 127 is 52 + 127/256 cells.
	/// </summary>
	/// <remarks><b>Mutations:</b> the byte in 65536ths; the byte added to the cell's middle.</remarks>
	[TestMethod]
	public void TheFixedPositionIsTheCellAndTheByteIn256ths()
	{
		Assert.AreEqual( 52 * PeepNavigator.One + 127 * 256, ParkRidePoint.Fixed( 52, 127 ) );
		Assert.AreEqual( 26 * 65536 + 0x7f00, ParkRidePoint.Fixed( 26, 0x7f ) );
		Assert.AreEqual( 3 * 65536, ParkRidePoint.Fixed( 3, 0 ) );
	}

	/// <summary>
	/// <b>The descriptor reads the four floats and a category's show through</b>, nought where neither says (the reader
	/// takes text directly, so this needs no game).
	/// </summary>
	/// <remarks><b>Mutations:</b> an exit key read as the stand point's; the category ignored.</remarks>
	[TestMethod]
	public void TheDescriptorReadsTheFourFloatsAndTheCategoryShowsThrough()
	{
		var category = new ItemDescriptionFile( "UsageInfo.EntryCellStandPosX 0.5\nUsageInfo.EntryCellStandPosY 0.5\n"
			+ "UsageInfo.ExitCellAppearPosX 0.5\nUsageInfo.ExitCellAppearPosY 0.5\n" );
		var item = new ItemDescriptionFile( "Info.Id 1\nUsageInfo.ExitCellAppearPosY 0.9\nUsageInfo.EntryCellStandPosX 0.25\n", category );

		Assert.AreEqual( (0.25f, 0.5f, 0.5f, 0.9f),
			(item.EntryCellStandPosX, item.EntryCellStandPosY, item.ExitCellAppearPosX, item.ExitCellAppearPosY) );
		Assert.AreEqual( (0f, 0f, 0f, 0f), (new ItemDescriptionFile( "Info.Id 2\n" ).EntryCellStandPosX,
			new ItemDescriptionFile( "Info.Id 2\n" ).EntryCellStandPosY, new ItemDescriptionFile( "Info.Id 2\n" ).ExitCellAppearPosX,
			new ItemDescriptionFile( "Info.Id 2\n" ).ExitCellAppearPosY), "nought where neither says" );
	}

	/// <summary>
	/// <b>The census.</b> Every item of the four themes: what pair it resolves, how many are not the rides' 0.5 and 0.5,
	/// that no product with 255 (the fraction or its 1 -) lands within 1e-9 of a whole byte without being one, and that
	/// the four turns all give a byte for every item. Needs the game.
	/// </summary>
	[TestMethod]
	public void EveryItemsPointFallsOutOfItsOwnSam()
	{
		var data = GameData.Required();
		var pairs = new SortedDictionary<string, int>();
		int items = 0, queued = 0, queuedOutside = 0, queuedExit = 0, queuedStand = 0;

		foreach ( var theme in new[] { "jungle", "fantasy", "hallow", "space" } )
			foreach ( var item in new ParkItemCatalogue( theme, data ).All )
			{
				++items;
				var key = $"exit ({item.ExitCellAppearPosX}, {item.ExitCellAppearPosY}) stand ({item.EntryCellStandPosX}, {item.EntryCellStandPosY})";
				pairs[key] = pairs.GetValueOrDefault( key ) + 1;

				// Both the 53-bit and the 64-bit x87 cut the product the same way unless it is within a double's rounding of a
				// whole byte (1 - 0.2 and 1 - 0.4 are 204 and 153 less a few millionths, the ones the float store at 90 and 180
				// decides: the pinned bytes above).
				foreach ( var f in new[] { item.ExitCellAppearPosX, item.ExitCellAppearPosY, item.EntryCellStandPosX, item.EntryCellStandPosY } )
					foreach ( var g in new[] { (double)f, 1.0 - f } )
					{
						var scaled = g * 255.0;
						Assert.IsFalse( Math.Abs( scaled - Math.Round( scaled ) ) < 1e-9 && scaled != Math.Round( scaled ),
							$"{theme} '{item.Name}' {f} lands on a byte's edge" );
					}

				// The four turns carry the same pair round: a byte of f and a byte of 1 - f sum to 254, or 255 where f is whole.
				var (x0, y0) = ParkRidePoint.Bytes( item.ExitCellAppearPosX, item.ExitCellAppearPosY, 0 );
				var (x90, y90) = ParkRidePoint.Bytes( item.ExitCellAppearPosX, item.ExitCellAppearPosY, 90 );
				var (x180, y180) = ParkRidePoint.Bytes( item.ExitCellAppearPosX, item.ExitCellAppearPosY, 180 );
				var (x270, y270) = ParkRidePoint.Bytes( item.ExitCellAppearPosX, item.ExitCellAppearPosY, 270 );
				Assert.IsTrue( x0 + x180 is 254 or 255 && y0 + y180 is 254 or 255 && y90 + x0 is 254 or 255 && x270 + y0 is 254 or 255
					&& x90 == y0 && y270 == x0, $"{theme} '{item.Name}': a turn does not carry the pair round" );

				if ( !item.HasQueue )
					continue;

				++queued;

				if ( item.ExitCellAppearPosX is <= 0f or >= 1f || item.ExitCellAppearPosY is <= 0f or >= 1f )
					++queuedExit;

				if ( item.EntryCellStandPosX is <= 0f or >= 1f || item.EntryCellStandPosY is <= 0f or >= 1f )
					++queuedStand;

				if ( (item.ExitCellAppearPosX, item.ExitCellAppearPosY) != (0.5f, 0.5f) )
					++queuedOutside;
			}

		Assert.AreEqual( (0, 0), (queuedExit, queuedStand), "every queued item states both points, inside (0, 1): the original's log never fires for one" );
		Assert.IsTrue( queued > 60 && queuedOutside > 0, $"{queued} queued, {queuedOutside} off the middle" );

		// Lost Kingdom's rides whose exit and stand differ, each key read for its own point (the Belly Bounce's are the
		// category's 0.5 and 0.5 for both, so it cannot tell them apart).
		var jungle = new ParkItemCatalogue( "jungle", data );

		foreach ( var (id, exit, stand) in new[] { (1140, (0.5f, 0.1f), (0.5f, 0.9f)), (1102, (0.5f, 0.1f), (0.7f, 0.9f)),
			(1106, (0.5f, 0.8f), (0.5f, 0.4f)), (1185, (0.5f, 0.9f), (0.5f, 0.7f)) } )
		{
			Assert.IsTrue( jungle.TryGet( id, out var ride ), $"{id}" );
			Assert.AreEqual( (exit, stand), ((ride.ExitCellAppearPosX, ride.ExitCellAppearPosY), (ride.EntryCellStandPosX, ride.EntryCellStandPosY)), ride.Name );
		}

		foreach ( var (key, count) in pairs )
			TestContext.WriteLine( $"{count,4} items: {key}" );

		TestContext.WriteLine( $"{items} items, {queued} queued; {queuedExit} queued items with an exit pair outside (0,1), {queuedStand} with a stand pair outside, {queuedOutside} with an exit pair other than 0.5, 0.5" );
	}

	/// <summary>
	/// <b>The put-down uses the item's bytes in the right slots at the thing's angle</b>: the exit cell's x gets the first byte
	/// and its z the second, after the turn. An item with x 0.3 and y 0.9 turned 90 stands at (0.9, 0.7); the Belly Bounce's
	/// 0.5 and 0.5 cannot tell the slots or the angle apart.
	/// </summary>
	/// <remarks><b>Mutations:</b> the slots exchanged; the angle ignored; the item left out.</remarks>
	[TestMethod]
	public void ThePutDownTakesTheBytesInOrderAtTheThingsAngle()
	{
		var item = new ParkItemCatalogue.Item( 1, "T", "d", "t", 1, 1, null, ExitCellAppearPosX: 0.3f, ExitCellAppearPosY: 0.9f );

		foreach ( var (angle, subX, subY) in new[] { (0, 76, 229), (90, 229, 178), (180, 178, 25), (270, 25, 76) } )
		{
			var peep = new Peep( 7, new ParkWorld.GuestState( State: (int)PeepState.Riding, SavedState: (int)PeepState.Deciding, PersonType: 0,
				Cash: 300, ExitLevel: 100, Happiness: 50f, Thirst: 10f, Hunger: 10f, Toilet: 10f, Vomit: 0f, Litter: 0f,
				MajorDest: 0, QueuePos: 0, PrankeryIndex: 0 ),
				new ParkWorld.NavigatorState( X: 40 * PeepNavigator.One, Y: 58 * PeepNavigator.One, VelocityX: 0, VelocityY: 0, TargetX: 0, TargetY: 0,
					Mass: ParkWorld.NavigatorState.DefaultMass, Radius: ParkWorld.NavigatorState.DefaultRadius, MaxForce: 0, MaxSpeed: 0,
					NavMode: 0, CantReachDest: 0, PathFinished: false, PathCount: 0, PathTotalCount: 0, PathBufferCount: 0,
					BufferedDistance: 0, TailDistance: 0, TotalDistance: 0, StuckBits: 0 ) );
			var ride = new ParkWorld.CatalogueObject( 13, 1, 0, 0, angle, ExitPos: (ushort)(26 * 128 + 52 + 1) );
			var walk = new PeepWalk( peep.Navigator, ( _, _, _ ) => true );

			ParkRideOperation.PutDownAtTheExit( peep, walk, ride, item, park: null );

			Assert.AreEqual( (ParkRidePoint.Fixed( 52, (byte)subX ), ParkRidePoint.Fixed( 26, (byte)subY )),
				(peep.Navigator.Position.X, peep.Navigator.Position.Y), $"angle {angle}" );
		}
	}

	/// <summary>The item hands out its exit bytes from the exit pair and its stand bytes from the stand pair.</summary>
	/// <remarks><b>Mutations:</b> the stand point read from the exit pair.</remarks>
	[TestMethod]
	public void TheItemKeepsItsExitAndItsStandPointApart()
	{
		var item = new ParkItemCatalogue.Item( 1, "T", "d", "t", 1, 1, null,
			EntryCellStandPosX: 0.7f, EntryCellStandPosY: 0.1f, ExitCellAppearPosX: 0.3f, ExitCellAppearPosY: 0.9f );

		Assert.AreEqual( ((byte)76, (byte)229), item.ExitPointBytes( 0 ) );
		Assert.AreEqual( ((byte)178, (byte)25), item.StandPointBytes( 0 ) );
		Assert.AreEqual( ((byte)25, (byte)76), item.ExitPointBytes( 270 ) );
		Assert.AreEqual( ((byte)229, (byte)178), item.StandPointBytes( 270 ) );
	}
}
