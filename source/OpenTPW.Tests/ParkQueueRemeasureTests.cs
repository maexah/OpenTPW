using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// What a queue measured again does to the people in it - <c>FUN_004de1f0</c>'s walk and each queuer's
/// answer, <c>FUN_00501390</c>: whoever now stands at or past four to a cell is put out of it, for
/// <c>MediumHappinessChange</c> alone. See <c>docs/exe/ride-operation.md</c>, "Every way out of a queue".
///
/// <para>
/// Every guest here is one of Lost Kingdom's own, put into the Belly Bounce's queue by hand with happiness
/// 50. The queue is cut as the console's <c>path</c> cuts it, by laying path over its second cell
/// (<see cref="ParkPathBuilding.LayPathRun"/>), which leaves it one cell long: room for four. The path tool
/// refuses that cell (Q94).
/// </para>
/// <para>
/// These read real game files and are skipped where there is no installation - see <see cref="GameData"/>.
/// </para>
/// </summary>
[TestClass]
public class ParkQueueRemeasureTests
{
	private BaseFileSystem data = null!;

	[TestInitialize]
	public void MountTheGame() => FileSystem = data = GameData.Required();

	private const string Theme = "jungle";

	private const int BellyBounce = 13;

	/// <summary>The Belly Bounce's second queue cell; the first, (52,22), is NOMODIFY.</summary>
	private const int SecondCellX = 51, SecondCellY = 22;

	private const int PathPrice = 20;

	private const float Before = 50f;

	/// <summary>A queuer put out: <c>MediumHappinessChange</c>, 15, in <c>FUN_005012f0</c>, and nothing else.</summary>
	private const float PutOut = 35f;

	/// <summary>A queuer a sale puts off: 15 in <c>FUN_005012f0</c>, then the sale's own 5.</summary>
	private const float SoldFrom = 30f;

	private sealed record Park( ParkWorld World, ParkState State, ParkItemCatalogue Catalogue, ParkPeople People );

	private Park Open( Func<int, RideScript?>? scriptFor = null )
	{
		using var stream = new MemoryStream( data.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );

		var state = new ParkState( world );
		var catalogue = new ParkItemCatalogue( Theme, data );
		var people = new ParkPeople( world, new ParkBalance( Theme, easyMode: true ), gateStatus: null, state,
			catalogue, scriptFor );

		return new( world, state, catalogue, people );
	}

	private static void Close( Park park )
	{
		park.People.Delete();
		Entity.ApplyDeletions();
	}

	/// <summary>The Belly Bounce's own script, which declares <c>VAR_LETMEON</c> by name.</summary>
	private RideScript Script()
	{
		using var stream = data.OpenRead( "levels/jungle/rides/bouncy/bouncy.RSE" );

		return new RideScript( new RideScriptFile( stream ) );
	}

	/// <summary>Puts the park's first <paramref name="count"/> guests in the Belly Bounce's queue, in order.</summary>
	private static Peep[] Queue( Park park, int count )
	{
		var queued = park.People.Peeps.Take( count ).ToArray();

		Assert.AreEqual( count, queued.Length, "the save has enough guests" );

		foreach ( var peep in queued )
		{
			peep.MajorDest = BellyBounce;
			peep.Happiness = Before;
			peep.SetState( PeepState.InQueue, tick: 1, new Random( 1 ) );
			peep.QueuePos = park.State.JoinQueue( BellyBounce, peep.ThingId );
		}

		return queued;
	}

	/// <summary>Lays path over the queue's second cell, as a click of the path tool does.</summary>
	private static void CutTheQueue( Park park )
	{
		Assert.AreEqual( ParkRideChoice.QueueCellType, ParkState.CellFor( park.World, SecondCellX, SecondCellY ).Type,
			"the cell is the Belly Bounce's queue before the cut" );

		ParkPathBuilding.LayPathRun( park.State, park.World, SecondCellX, SecondCellY, SecondCellX, SecondCellY,
			PathPrice );

		Assert.AreEqual( 1, ParkRideChoice.QueueCellsFor( park.World, park.State.Objects.Single( o => o.ThingId == BellyBounce ) ).Cells,
			"the queue measures one cell after the cut" );
	}

	private static void AssertStillQueueing( Park park, Peep peep, string why )
	{
		Assert.AreEqual( BellyBounce, peep.MajorDest, $"guest {peep.ThingId} still names the ride: {why}" );
		Assert.AreEqual( Before, peep.Happiness, 0.001f, $"guest {peep.ThingId} loses nothing: {why}" );
		Assert.IsTrue( park.State.PositionInQueue( BellyBounce, peep.ThingId ) >= 0,
			$"guest {peep.ThingId} is still in the queue: {why}" );
	}

	private static void AssertPutOut( Park park, Peep peep, string why )
	{
		Assert.AreEqual( PeepState.Deciding, peep.State, $"guest {peep.ThingId} thinks again: {why}" );
		Assert.AreEqual( 0, peep.MajorDest, $"guest {peep.ThingId} names the ride no longer: {why}" );
		Assert.AreEqual( PutOut, peep.Happiness, 0.001f, $"guest {peep.ThingId} loses MediumHappinessChange alone: {why}" );
		Assert.AreEqual( 0, peep.QueuePos, $"guest {peep.ThingId} has no place: {why}" );
		Assert.IsFalse( peep.BeenAdmitted, $"guest {peep.ThingId} holds no invitation: {why}" );
		Assert.AreEqual( -1, park.State.PositionInQueue( BellyBounce, peep.ThingId ),
			$"guest {peep.ThingId} is out of the queue: {why}" );
		Assert.AreEqual( 0, park.State.NextInQueue( peep.ThingId ), $"guest {peep.ThingId} links forward to nobody" );
		Assert.AreEqual( 0, park.State.PreviousInQueue( peep.ThingId ), $"guest {peep.ThingId} links back to nobody" );
	}

	/// <summary>
	/// A queue cut to one cell holds four: the first four keep their places and the rest are put out, each for
	/// <c>MediumHappinessChange</c> and nothing more, and the queue is joined up behind the fourth
	/// (<c>FUN_004ddd20</c>) so it walks four long.
	/// </summary>
	[TestMethod]
	public void CuttingTheQueuePutsOutEveryoneFromTheFifthPlace()
	{
		var park = Open();

		try
		{
			var queued = Queue( park, 6 );

			CutTheQueue( park );

			for ( var place = 0; place < 4; ++place )
				AssertStillQueueing( park, queued[place], $"place {place} is inside the four" );

			for ( var place = 4; place < 6; ++place )
				AssertPutOut( park, queued[place], $"place {place} is past the four" );

			Assert.AreEqual( 4, park.State.QueueLength( BellyBounce ), "the queue walks four long" );
			Assert.AreEqual( 0, park.State.NextInQueue( queued[3].ThingId ), "and ends at the fourth" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// Two are never put out whatever their place: the ride's nominee, whom the walk skips
	/// (<c>0x004de2b9</c>), and a guest already <see cref="PeepState.EnteringRide"/> (<c>0x00501422</c>). The
	/// guest behind them, past the four like them, is.
	/// </summary>
	[TestMethod]
	public void TheNomineeAndAGuestEnteringTheRideKeepTheirPlaces()
	{
		var park = Open();

		try
		{
			var queued = Queue( park, 7 );

			queued[4].SetState( PeepState.EnteringRide, tick: 1, new Random( 1 ) );
			park.State.NominateForLoading( BellyBounce, queued[5].ThingId );

			CutTheQueue( park );

			AssertStillQueueing( park, queued[4], "entering the ride" );
			AssertStillQueueing( park, queued[5], "the ride's nominee" );
			AssertPutOut( park, queued[6], "past the four and neither" );
			Assert.AreEqual( 6, park.State.QueueLength( BellyBounce ), "only the last left" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// <b>A place the walk cannot reach is past the end.</b> The walk gives up at a guest it steps past who is
	/// no longer queueing (<c>FUN_004ddf50</c>, <c>0x004ddfa9</c>), answering -1 for everyone behind them, and
	/// the comparison is unsigned (<c>0x0050141c</c>) - so they are put out from inside the four. The guest who
	/// stopped queueing is found at their own place, since the guest sought is never asked, and stays.
	/// </summary>
	[TestMethod]
	public void EveryoneBehindAGuestNoLongerQueueingIsPutOut()
	{
		var park = Open();

		try
		{
			var queued = Queue( park, 3 );

			queued[1].SetState( PeepState.Wandering, tick: 1, new Random( 1 ) );

			CutTheQueue( park );

			AssertStillQueueing( park, queued[0], "the head is found" );
			Assert.AreEqual( 1, park.State.PositionInQueue( BellyBounce, queued[1].ThingId ),
				"the guest who stopped queueing is still linked where they stood" );
			Assert.AreEqual( Before, queued[1].Happiness, 0.001f, "and loses nothing" );
			AssertPutOut( park, queued[2], "the walk gave up before reaching them" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// Going out of the queue empties the ride's admission slot when it names the guest, as
	/// <c>FUN_004ddd20</c> does (<c>0x004ddd4e</c>..<c>0x004ddd7d</c>).
	/// </summary>
	[TestMethod]
	public void GoingOutLetsGoOfTheAdmissionSlotThatNamesThem()
	{
		var script = Script();
		var park = Open( id => id == BellyBounce ? script : null );

		try
		{
			var queued = Queue( park, 5 );

			Assert.IsTrue( script.Set( ParkRideOperation.AdmitVariable, queued[4].ThingId ), "the slot is declared" );

			CutTheQueue( park );

			AssertPutOut( park, queued[4], "past the four" );
			Assert.AreEqual( 0, script[ParkRideOperation.AdmitVariable], "the slot named them, so it is emptied" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// <b>Four to a cell, exactly</b>: a one-cell queue keeps a fourth guest, and a fifth who joins it is put
	/// out the next time it is measured - as the queue tool's own edits and a thing's placer measure it
	/// (<c>0x00526118</c>, <c>0x00527541</c>, <c>0x00529890</c>) without changing its length.
	/// </summary>
	[TestMethod]
	public void AOneCellQueueHoldsFourAndNoMore()
	{
		var park = Open();

		try
		{
			var queued = Queue( park, 4 );

			CutTheQueue( park );

			foreach ( var peep in queued )
				AssertStillQueueing( park, peep, "four fit one cell" );

			var fifth = park.People.Peeps[4];

			fifth.MajorDest = BellyBounce;
			fifth.Happiness = Before;
			fifth.SetState( PeepState.InQueue, tick: 1, new Random( 1 ) );
			fifth.QueuePos = park.State.JoinQueue( BellyBounce, fifth.ThingId );

			park.State.RemeasureQueue( BellyBounce );

			foreach ( var peep in queued )
				AssertStillQueueing( park, peep, "measured again, still four" );

			AssertPutOut( park, fifth, "the fifth place is past one cell" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// <b>A sale's drain puts out everybody from the fifth place back before the sale reaches them</b>
	/// (<c>docs/exe/ride-operation.md</c>, "The sale's drain"). Its first run clears all four cells and measures
	/// the one the entrance still links to, room for four: the guests at places 4 and on go for 15 and name the
	/// ride no longer, so the sale passes them by. The first four, the ride's nominee and a guest already
	/// boarding are left to the sale, which puts each off for 15 and 5.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> throwing the measurement away instead (<see cref="ParkState.InvalidateQueue"/>)
	/// leaves all eight to the sale, at 30.
	/// </remarks>
	[TestMethod]
	public void ASaleDrainPutsOutEveryQueuerFromTheFifthPlaceBack()
	{
		var park = Open();

		try
		{
			var queued = Queue( park, 8 );

			park.State.NominateForLoading( BellyBounce, queued[5].ThingId );
			queued[6].SetState( PeepState.EnteringRide, tick: 1, new Random( 1 ) );

			var calls = Times( "QUEUE_DRAIN_ADVISOR_0xCB" );
			var measures = Times( "QUEUE_REMEASURE_BACK_CELL_STAMP" );
			var answer = ParkBuilding.Sell( park.State, park.World, park.Catalogue, null, null, BellyBounce, park.People );

			for ( var place = 0; place < 4; ++place )
				Assert.AreEqual( SoldFrom, queued[place].Happiness, 0.001f, $"place {place} is left to the sale" );

			Assert.AreEqual( PutOut, queued[4].Happiness, 0.001f, "place 4 is past one cell's room" );
			Assert.AreEqual( SoldFrom, queued[5].Happiness, 0.001f, "the nominee is never asked" );
			Assert.AreEqual( SoldFrom, queued[6].Happiness, 0.001f, "a guest boarding is never put out" );
			Assert.AreEqual( PutOut, queued[7].Happiness, 0.001f, "the last goes too" );

			foreach ( var peep in queued )
				Assert.AreEqual( 0, peep.MajorDest, $"guest {peep.ThingId} names the sold ride no longer" );

			Assert.AreEqual( calls + 4, Times( "QUEUE_DRAIN_ADVISOR_0xCB" ), "mode 3, then a re-arm on each of three calls" );
			Assert.AreEqual( measures + 2, Times( "QUEUE_REMEASURE_BACK_CELL_STAMP" ), "and measured after each" );
			StringAssert.Contains( answer, "its queue for 225", "four cells refunded, one taken back" );
		}
		finally
		{
			Close( park );
		}
	}

	/// <summary>
	/// <b>The Belly Bounce's queue ends are its node twice and its last cell</b> - <c>FUN_00530120</c>: the cell
	/// its entrance faces, (52,22); again as the corner the walk from the entrance turns west at; and (49,22),
	/// where the walk reaches the path at (48,22) and lets go of it, each losing its bit toward the other.
	/// The node's counter is set to 1, and the straight cells' and the entrance's to nought - which the save
	/// already holds, so the test sets them otherwise first.
	/// </summary>
	/// <remarks>
	/// <b>Mutation:</b> pushing the faced cell only as the walk's corner leaves the list two long, and the drain
	/// one measure short; dropping the counter write leaves the counters as the test set them.
	/// </remarks>
	[TestMethod]
	public void TheBellyBouncesQueueEndsAreItsNodeTwiceAndItsLastCell()
	{
		var park = Open();

		try
		{
			var bellyBounce = park.State.Objects.Single( o => o.ThingId == BellyBounce );

			foreach ( var (x, y, counter) in new[] { (52, 22, 0), (51, 22, 3), (50, 22, 3), (49, 22, 3), (52, 23, 3) } )
				park.State.SetRecord( x, y, park.State.Record( x, y ) with { OverlapCounter = (short)counter } );

			var ends = ParkPathBuilding.QueueEnds( park.State, park.World, bellyBounce );

			CollectionAssert.AreEqual( new[] { (52, 22), (52, 22), (49, 22) }, ends.ToArray(), "the node twice, then the end" );

			Assert.AreEqual( 0x04, ParkState.CellFor( park.World, 49, 22 ).Neighbours, "the end lets the path go" );
			Assert.AreEqual( 0, ParkState.CellFor( park.World, 48, 22 ).Neighbours & 0x04, "and the path lets it go" );
			Assert.AreEqual( ParkRideChoice.QueueCellType, ParkState.CellFor( park.World, 49, 22 ).Type, "nothing is cleared" );

			Assert.AreEqual( 1, ParkState.CellFor( park.World, 52, 22 ).OverlapCounter, "the corner's counter" );

			for ( var x = 49; x <= 51; ++x )
				Assert.AreEqual( 0, ParkState.CellFor( park.World, x, 22 ).OverlapCounter, $"({x},22) is straight" );

			Assert.AreEqual( 0, ParkState.CellFor( park.World, 52, 23 ).OverlapCounter, "the entrance is walked too" );
		}
		finally
		{
			Close( park );
		}
	}

	private static int Times( string what )
		=> Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;
}
