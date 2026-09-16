using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace OpenTPW.Tests;

/// <summary>
/// Whether one side of one cell is closed - the ladder of type comparisons the original makes before it
/// lets anyone step anywhere.
///
/// <para>
/// These need no game files and no device: every cell here is made up, so each test says exactly which
/// two types meet at the edge it is about. The types that appear are the ones Lost Kingdom actually
/// contains.
/// </para>
/// <para>
/// <b>Sidedness is what these are really for.</b> Almost every question the original asks goes to one
/// particular side of the edge - the cell being left or the cell being entered - and the decompiled form
/// of the function shows neither, because the decompiler drops the <c>this</c> pointer. Getting a side
/// wrong produces a predicate that still answers plausibly, so the tests that matter most below are the
/// ones that give the two cells <i>different</i> values and pin which one was consulted.
/// </para>
/// </summary>
[TestClass]
public class CellEdgeTests
{
	/// <summary>A cell, with everything that is not the point of a given test left at nothing.</summary>
	private static ParkWorld.MapCell Cell( int type, int neighbours = 0, int direction = 0, int flags = 0,
		int trackType = 0, int trackFlags = 0, int trackParent = 0 )
		=> new( type, (ushort)flags, (byte)neighbours, (byte)direction, 0, 0, 0, 1,
			trackType, (ushort)trackFlags, (ushort)trackParent );

	/// <summary>
	/// An edge between two cells placed side by side, so a test names the pair and nothing else. The cell
	/// being left is at (40, 40) and the one being entered is one step away in the given direction.
	/// </summary>
	private static CellEdge Between( ParkWorld.MapCell from, ParkWorld.MapCell to,
		StepDirection direction, int mode = 0,
		Func<ParkWorld.MapCell, bool>? trackCloses = null,
		Func<ParkWorld.MapCell, QueueVerdict>? queueAhead = null )
	{
		var (toX, toY) = MapStep.Beyond( 40, 40, direction );

		return new CellEdge(
			( x, y ) => x == 40 && y == 40 ? from : x == toX && y == toY ? to : default,
			mode, trackCloses, queueAhead );
	}

	private static bool Blocked( ParkWorld.MapCell from, ParkWorld.MapCell to,
		StepDirection direction = StepDirection.East, int mode = 0,
		Func<ParkWorld.MapCell, bool>? trackCloses = null,
		Func<ParkWorld.MapCell, QueueVerdict>? queueAhead = null )
		=> Between( from, to, direction, mode, trackCloses, queueAhead ).Blocked( 40, 40, direction );

	/// <summary>
	/// The boundary closes a cell before the map is consulted at all, which is the original's own order and
	/// is what makes the edge of the map safe whatever is built on it.
	/// </summary>
	[TestMethod]
	public void TheEdgeOfTheMapIsClosedWithoutReadingAnything()
	{
		var asked = 0;

		var edge = new CellEdge( ( _, _ ) => { ++asked; return Cell( CellEdge.Path ); }, 0 );

		Assert.IsTrue( edge.Blocked( 0, 10, StepDirection.West ), "west from the first column" );
		Assert.IsTrue( edge.Blocked( 10, 0, StepDirection.North ), "north from the first row" );
		Assert.IsTrue( edge.Blocked( 127, 10, StepDirection.East ), "east from the last column" );
		Assert.IsTrue( edge.Blocked( 10, 127, StepDirection.South ), "south from the last row" );

		Assert.AreEqual( 0, asked, "the boundary should be refused without asking the map anything" );

		// And the same cells are open on their other sides, or the four above prove nothing.
		Assert.IsFalse( edge.Blocked( 0, 10, StepDirection.East ) );
		Assert.AreEqual( 2, asked, "an ordinary step reads the two cells it joins" );
	}

	/// <summary>
	/// Walking onto path is the common case and is allowed from anywhere - except out of a queue, which
	/// leaves by its own rule further down.
	/// </summary>
	[TestMethod]
	public void WalkingOntoPathIsAllowedUnlessLeavingAQueue()
	{
		Assert.IsFalse( Blocked( Cell( CellEdge.Path ), Cell( CellEdge.Path ) ), "path to path" );
		Assert.IsFalse( Blocked( Cell( CellEdge.Nothing ), Cell( CellEdge.Path ) ), "open ground to path" );
		Assert.IsFalse( Blocked( Cell( CellEdge.Footprint ), Cell( CellEdge.Path ) ), "footprint to path" );

		// A queue is the exception: it is let out only where the two cells say they adjoin, which is the
		// next test. Here it is enough that it does not take the free pass above.
		Assert.IsTrue( Blocked( Cell( 3 ), Cell( CellEdge.Path, neighbours: 0 ) ),
			"a queue with no adjoining side should not get path's free pass" );
	}

	/// <summary>
	/// <b>The bit that says two cells adjoin is read from the cell being ENTERED.</b>
	///
	/// <para>
	/// This is the test the whole class exists for. The mask is symmetric across every one of the shipped
	/// park's 65,024 adjacent pairs, so no measurement on real data can tell this reading from its mirror
	/// image - only the disassembly can, and only by recovering the <c>this</c> pointer the decompiler
	/// drops. Here the two cells are given <i>opposite</i> masks, so a predicate that consulted the wrong
	/// one would answer the wrong way round on both halves.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheAdjoiningBitIsReadFromTheCellBeingEntered()
	{
		var east = CellEdge.BitFor( StepDirection.East );

		Assert.IsFalse(
			Blocked( Cell( 3, neighbours: 0 ), Cell( CellEdge.Path, neighbours: east ) ),
			"the cell being entered says it adjoins, so the step should be allowed" );

		Assert.IsTrue(
			Blocked( Cell( 3, neighbours: east ), Cell( CellEdge.Path, neighbours: 0 ) ),
			"only the cell being LEFT says it adjoins, which is not what the original reads" );
	}

	/// <summary>
	/// Each direction has its own bit, and they are the same four constants the facing test uses.
	/// </summary>
	[TestMethod]
	public void EachDirectionHasItsOwnAdjoiningBit()
	{
		Assert.AreEqual( 0x10, CellEdge.BitFor( StepDirection.North ) );
		Assert.AreEqual( 0x40, CellEdge.BitFor( StepDirection.East ) );
		Assert.AreEqual( 0x01, CellEdge.BitFor( StepDirection.South ) );
		Assert.AreEqual( 0x04, CellEdge.BitFor( StepDirection.West ) );

		foreach ( var direction in Enum.GetValues<StepDirection>() )
		{
			var bit = CellEdge.BitFor( direction );

			Assert.IsFalse( Blocked( Cell( 3 ), Cell( CellEdge.Path, neighbours: bit ), direction ),
				$"{direction} with its own bit set" );
			Assert.IsTrue( Blocked( Cell( 3 ), Cell( CellEdge.Path, neighbours: ~bit ), direction ),
				$"{direction} with every other bit set but its own" );
		}
	}

	/// <summary>
	/// A queue is entered only from path or another queue, and only where the two cells adjoin.
	/// </summary>
	[TestMethod]
	public void AQueueIsEnteredOnlyFromPathOrQueue()
	{
		var east = CellEdge.BitFor( StepDirection.East );

		foreach ( var queue in new[] { 3, 9 } )
		{
			Assert.IsFalse( Blocked( Cell( CellEdge.Path ), Cell( queue, neighbours: east ) ),
				$"path into type {queue}" );
			Assert.IsFalse( Blocked( Cell( 3 ), Cell( queue, neighbours: east ) ),
				$"queue into type {queue}" );

			Assert.IsTrue( Blocked( Cell( CellEdge.Nothing ), Cell( queue, neighbours: east ) ),
				$"open ground into type {queue} should be refused whatever the cells say they adjoin" );
			Assert.IsTrue( Blocked( Cell( CellEdge.Path ), Cell( queue, neighbours: 0 ) ),
				$"path into type {queue} where they do not adjoin" );
		}
	}

	/// <summary>
	/// A built object's footprint is walked around inside, and not stepped onto from outside.
	/// </summary>
	[TestMethod]
	public void AFootprintIsEnteredOnlyFromItself()
	{
		Assert.IsFalse( Blocked( Cell( CellEdge.Footprint ), Cell( CellEdge.Footprint ) ),
			"within one object" );

		Assert.IsTrue( Blocked( Cell( CellEdge.Path ), Cell( CellEdge.Footprint ) ), "from path" );
		Assert.IsTrue( Blocked( Cell( CellEdge.Nothing ), Cell( CellEdge.Footprint ) ),
			"from open ground" );
	}

	/// <summary>
	/// The three types the original refuses outright, and the fact that it is the cell being entered that
	/// has to be one of them.
	/// </summary>
	[TestMethod]
	public void SomeTypesAreRefusedOutright()
	{
		foreach ( var type in new[] { 5, 7, 2 } )
		{
			Assert.IsTrue( CellEdge.IsSolid( type ), $"type {type}" );
			Assert.IsTrue( Blocked( Cell( CellEdge.Path ), Cell( type ) ), $"onto type {type}" );

			// Leaving one is a different question, and this ladder does not refuse it here.
			Assert.IsFalse( Blocked( Cell( type ), Cell( CellEdge.Path ) ), $"off type {type} onto path" );
		}

		Assert.IsFalse( CellEdge.IsSolid( CellEdge.Path ) );
		Assert.IsFalse( CellEdge.IsSolid( CellEdge.Nothing ) );
	}

	/// <summary>
	/// <b>The approach is freely walked on every mode but the strict one.</b> This is the clearest thing
	/// the fourth argument does, and it is pinned from a path because someone standing on open ground is
	/// let through by a later rule anyway - testing it from there would pass for the wrong reason.
	/// </summary>
	[TestMethod]
	public void TheApproachIsOpenExceptOnTheStrictMode()
	{
		Assert.IsFalse( Blocked( Cell( CellEdge.Path ), Cell( CellEdge.Approach ), mode: 0 ) );
		Assert.IsFalse( Blocked( Cell( CellEdge.Path ), Cell( CellEdge.Approach ), mode: 1 ) );

		Assert.IsTrue( Blocked( Cell( CellEdge.Path ), Cell( CellEdge.Approach ), mode: 2 ),
			"on mode 2 the approach stops being a free pass" );

		// Unless it carries the flag, which is what the fourteen cells of the entrance avenue have.
		Assert.IsFalse(
			Blocked( Cell( CellEdge.Path ), Cell( CellEdge.Approach, flags: CellEdge.ApproachFlag ),
				mode: 2 ),
			"the entrance avenue's own flag opens it again" );

		// And that flag does nothing on the other modes, because they never reach it.
		Assert.IsFalse(
			Blocked( Cell( CellEdge.Path ), Cell( CellEdge.Approach, flags: CellEdge.ApproachFlag ),
				mode: 1 ) );
	}

	/// <summary>
	/// Someone standing on neither path nor queue is let through, which is the rule that keeps everything
	/// that is not a guest out of the walking rules entirely.
	/// </summary>
	[TestMethod]
	public void SomeoneNotOnPathOrQueueIsLetThrough()
	{
		Assert.IsFalse( Blocked( Cell( CellEdge.Nothing ), Cell( CellEdge.Nothing ) ) );
		Assert.IsFalse( Blocked( Cell( CellEdge.Approach ), Cell( CellEdge.Nothing ) ) );

		// Whereas leaving a path for open ground is refused on this mode, which is what stops a guest
		// walking off across the grass.
		Assert.IsTrue( Blocked( Cell( CellEdge.Path ), Cell( CellEdge.Nothing ), mode: 0 ) );
	}

	/// <summary>
	/// What the two named modes let someone do when leaving a path, which is the difference between them.
	/// </summary>
	[TestMethod]
	public void LeavingAPathDependsOnTheMode()
	{
		// Mode 1: a path may be left for anything that is not path, queue or footprint.
		Assert.IsFalse( Blocked( Cell( CellEdge.Path ), Cell( CellEdge.Nothing ), mode: 1 ) );
		Assert.IsFalse( Blocked( Cell( CellEdge.Path ), Cell( CellEdge.Approach ), mode: 1 ) );

		// Mode 2: only for open ground.
		Assert.IsFalse( Blocked( Cell( CellEdge.Path ), Cell( CellEdge.Nothing ), mode: 2 ) );
		Assert.IsTrue( Blocked( Cell( CellEdge.Path ), Cell( CellEdge.Approach ), mode: 2 ) );

		// Mode 0 allows neither.
		Assert.IsTrue( Blocked( Cell( CellEdge.Path ), Cell( CellEdge.Nothing ), mode: 0 ) );
	}

	/// <summary>
	/// Stepping off the end of a ride is allowed only the way that cell faces.
	/// </summary>
	[TestMethod]
	public void SteppingOffARideEndFollowsTheWayItFaces()
	{
		foreach ( var direction in Enum.GetValues<StepDirection>() )
		{
			var facing = CellEdge.BitFor( direction );

			Assert.IsFalse(
				Blocked( Cell( CellEdge.RideEnd, direction: facing ), Cell( CellEdge.Nothing ), direction ),
				$"off a ride end facing {direction}" );

			Assert.IsTrue(
				Blocked( Cell( CellEdge.RideEnd, direction: 0 ), Cell( CellEdge.Nothing ), direction ),
				$"off a ride end facing nowhere, going {direction}" );
		}

		// And only onto open ground. The cell beyond has to be one that actually reaches the facing test:
		// the approach does not, because it is let through higher up the ladder, and using it here
		// asserted the right thing for a reason that does not hold.
		Assert.IsTrue(
			Blocked( Cell( CellEdge.RideEnd, direction: CellEdge.BitFor( StepDirection.East ) ),
				Cell( CellEdge.RideFarEnd ) ),
			"a ride end faces this way but the cell beyond is not open ground" );

		Assert.IsFalse(
			Blocked( Cell( CellEdge.RideEnd, direction: 0 ), Cell( CellEdge.Approach ) ),
			"the approach is let through before the facing test is reached, whatever the cell faces" );
	}

	/// <summary>
	/// <b>The original tests for the far end of a ride in a place it can never reach</b>, and this pins
	/// that rather than letting a later reader tidy it away.
	///
	/// <para>
	/// By the time the facing test is made, the cell being left is already known to be path or queue - so
	/// it cannot also be type 10, and that arm is dead. A cell of type 10 is let through by the earlier
	/// rule instead, whichever way it faces, which is what is asserted here.
	/// </para>
	/// </summary>
	[TestMethod]
	public void TheFarEndOfARideNeverReachesTheFacingTest()
	{
		foreach ( var direction in Enum.GetValues<StepDirection>() )
		{
			Assert.IsFalse(
				Blocked( Cell( CellEdge.RideFarEnd, direction: 0 ), Cell( CellEdge.Nothing ), direction ),
				$"type 10 facing nowhere is still let through going {direction}" );
		}

		// Which is the opposite of what a ride END does with the same facing, and that contrast is the
		// point: if the dead arm were live these two would agree.
		Assert.IsTrue( Blocked( Cell( CellEdge.RideEnd, direction: 0 ), Cell( CellEdge.Nothing ) ) );
	}

	/// <summary>
	/// The track record can close a cell, and it is asked about the cell being entered.
	/// </summary>
	[TestMethod]
	public void TheTrackRecordClosesTheCellBeingEntered()
	{
		var seen = new List<int>();

		Assert.IsFalse(
			Blocked( Cell( CellEdge.Path ), Cell( CellEdge.Nothing ), mode: 1,
				trackCloses: cell => { seen.Add( cell.Type ); return false; } ),
			"with the track saying nothing, mode 1 lets a path be left for open ground" );

		CollectionAssert.AreEqual( new[] { CellEdge.Nothing }, seen,
			"the track should be asked about the cell being entered, once" );

		Assert.IsTrue(
			Blocked( Cell( CellEdge.Path ), Cell( CellEdge.Nothing ), mode: 1, trackCloses: _ => true ),
			"and it closes the step when it says so" );
	}

	/// <summary>
	/// A thing standing on the cell ahead gets a say, but only on the strict mode and only for the one
	/// type the original looks at.
	/// </summary>
	[TestMethod]
	public void AThingOnTheCellAheadOnlyGetsASayOnTheStrictMode()
	{
		var east = CellEdge.BitFor( StepDirection.East );
		var asked = 0;

		QueueVerdict Count( QueueVerdict verdict )
		{
			++asked;
			return verdict;
		}

		Assert.IsFalse(
			Blocked( Cell( CellEdge.Path ), Cell( CellEdge.RideEnd ), mode: 2,
				queueAhead: _ => Count( QueueVerdict.LetThemThrough ) ) );
		Assert.IsTrue(
			Blocked( Cell( CellEdge.Path ), Cell( CellEdge.RideEnd ), mode: 2,
				queueAhead: _ => Count( QueueVerdict.InTheWay ) ) );
		Assert.AreEqual( 2, asked );

		// Saying there is nothing there carries on to the queue rule, which then decides.
		Assert.IsFalse(
			Blocked( Cell( CellEdge.Path ), Cell( CellEdge.RideEnd, neighbours: east ), mode: 2,
				queueAhead: _ => QueueVerdict.NothingThere ) );
		Assert.IsTrue(
			Blocked( Cell( CellEdge.Path ), Cell( CellEdge.RideEnd, neighbours: 0 ), mode: 2,
				queueAhead: _ => QueueVerdict.NothingThere ) );

		// On any other mode it is never consulted at all.
		asked = 0;

		foreach ( var mode in new[] { 0, 1 } )
		{
			Blocked( Cell( CellEdge.Path ), Cell( CellEdge.RideEnd, neighbours: east ), mode: mode,
				queueAhead: _ => Count( QueueVerdict.InTheWay ) );
		}

		Assert.AreEqual( 0, asked, "only mode 2 asks what is standing on the cell ahead" );
	}

	/// <summary>
	/// The predicate fits straight into the step geometry, which is the whole reason for its shape.
	/// </summary>
	[TestMethod]
	public void ItAnswersTheQuestionTheStepGeometryAsks()
	{
		var path = Cell( CellEdge.Path );

		var edge = new CellEdge( ( _, _ ) => path, 0 );

		Assert.IsTrue( MapStep.CanStep( MapStep.CellId( 40, 40 ), MapStep.CellId( 41, 40 ), edge.Blocked ),
			"path to path, straight" );
		Assert.IsTrue( MapStep.CanStep( MapStep.CellId( 40, 40 ), MapStep.CellId( 41, 39 ), edge.Blocked ),
			"path to path, round a corner" );

		var solid = new CellEdge( ( _, _ ) => Cell( 7 ), 0 );

		Assert.IsFalse( MapStep.CanStep( MapStep.CellId( 40, 40 ), MapStep.CellId( 41, 40 ), solid.Blocked ),
			"nothing may be walked onto a refused type" );
	}

	/// <summary>
	/// A track record closes its cell unless its flags reopen it, and says nothing at all about a cell
	/// whose type this does not apply to.
	/// </summary>
	[TestMethod]
	public void ATrackRecordClosesItsCellUnlessItsFlagsReopenIt()
	{
		// A cell that does not defer must answer from its own record without looking anything up.
		static ParkWorld.MapCell Nowhere( int id )
			=> throw new InvalidOperationException( "a cell that does not defer looked up a parent" );

		foreach ( var type in new[] { 11, 13, 16, 18, 25 } )
		{
			Assert.IsTrue( CellEdge.TrackCounts( type ), $"track type {type}" );
			Assert.IsTrue( CellEdge.TrackCloses( Cell( CellEdge.Nothing, trackType: type ), Nowhere ),
				$"track type {type} with nothing reopening it" );

			Assert.IsFalse(
				CellEdge.TrackCloses( Cell( CellEdge.Nothing, trackType: type, trackFlags: 1 ), Nowhere ),
				$"track type {type} reopened by its low nibble - one cell of the park is like this" );
		}

		foreach ( var type in new[] { 0, 7, 24, 26 } )
		{
			Assert.IsFalse( CellEdge.TrackCounts( type ), $"track type {type}" );
			Assert.IsFalse( CellEdge.TrackCloses( Cell( CellEdge.Nothing, trackType: type ), Nowhere ),
				$"track type {type} is not one this applies to" );
		}
	}

	/// <summary>
	/// <b>A cell whose track defers is answered by its parent's record, and not by its own.</b> Both halves
	/// come from the parent - whether the test applies and whether the flags reopen it - so the deferring
	/// cell here is given flags that would have reopened it, and must be closed anyway.
	/// </summary>
	[TestMethod]
	public void ACellWhoseTrackDefersIsAnsweredByItsParent()
	{
		var asked = new List<int>();

		Func<int, ParkWorld.MapCell> Lookup( ParkWorld.MapCell parent )
			=> id => { asked.Add( id ); return parent; };

		Assert.IsTrue( CellEdge.TrackDefersToParent( 12 ) );
		Assert.IsTrue( CellEdge.TrackDefersToParent( 17 ) );
		Assert.IsFalse( CellEdge.TrackDefersToParent( 25 ) );

		Assert.IsTrue(
			CellEdge.TrackCloses(
				Cell( CellEdge.Nothing, trackType: 12, trackFlags: CellEdge.TrackOpenFlags,
					trackParent: 130 ),
				Lookup( Cell( CellEdge.Nothing, trackType: 25 ) ) ),
			"the parent closes it even though its own flags would have reopened it" );

		CollectionAssert.AreEqual( new[] { 130 }, asked, "the parent is looked up by the id it names" );

		Assert.IsFalse(
			CellEdge.TrackCloses( Cell( CellEdge.Nothing, trackType: 12, trackParent: 130 ),
				Lookup( Cell( CellEdge.Nothing, trackType: 25, trackFlags: 1 ) ) ),
			"a parent whose flags reopen it" );

		Assert.IsFalse(
			CellEdge.TrackCloses( Cell( CellEdge.Nothing, trackType: 12, trackParent: 130 ),
				Lookup( Cell( CellEdge.Nothing, trackType: 7 ) ) ),
			"a parent of a type this does not apply to" );

		// Naming no parent is answered outright, which the original says rather than leaves to fall out.
		Assert.IsFalse(
			CellEdge.TrackCloses( Cell( CellEdge.Nothing, trackType: 12, trackParent: 0 ),
				_ => throw new InvalidOperationException( "parent nought should not be looked up" ) ) );
	}

	/// <summary>
	/// The track answer reaches the step, which is what makes it worth reading off the save at all.
	/// </summary>
	[TestMethod]
	public void TheTrackAnswerReachesTheStep()
	{
		var from = Cell( CellEdge.Path );
		var to = Cell( CellEdge.Nothing, trackType: 25 );

		Assert.IsFalse( Blocked( from, to, mode: 1 ),
			"with the track left unanswered, mode 1 lets a path be left for open ground" );

		Assert.IsTrue( Blocked( from, to, mode: 1, trackCloses: cell => CellEdge.TrackCloses( cell, _ => default ) ),
			"with the real answer supplied, the same step is closed" );
	}
}
