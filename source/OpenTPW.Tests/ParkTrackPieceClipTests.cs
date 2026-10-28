using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The pieces' clips after a close and an add-on's put-down - <see cref="ParkTrackTool.PlayPieces"/>, the walks
/// <c>TrackRide_PlayPiecesFromMouth</c> (<c>FUN_0052a490</c>) and <c>TrackRide_PlayPiecesFromEnd</c> (<c>FUN_0052a700</c>),
/// and what each caller passes them (<c>docs/exe/park.md</c>, "The pieces' clips"). The loop is the original's own, as
/// <see cref="ParkTrackPulseTests"/>: a Dino Karts on (30,24) at turn 0, runs to (34,29), (28,29) and back to (28,25).
/// </summary>
[TestClass]
public class ParkTrackPieceClipTests
{
	private static readonly ushort Owner = (ushort)MapStep.CellId( 30, 24 );

	private static readonly (int, int)[] Loop = [(34, 25), (34, 27), (34, 29), (32, 29), (30, 29), (28, 29), (28, 27), (28, 25)];

	private readonly List<(int X, int Y, bool Running)> _clips = [];
	private System.Func<int, int, bool, bool>? _clipTo;
	private System.Action<int, int, bool>? _pulseTo;

	[TestInitialize]
	public void RecordTheClips()
	{
		ParkBuildMode.Forget();
		_clipTo = ParkTrackTool.ClipTo;
		_pulseTo = ParkTrackTool.PulseTo;
		ParkTrackTool.ClipTo = ( x, y, running ) =>
		{
			_clips.Add( (x, y, running) );
			return true;
		};
		ParkTrackTool.PulseTo = ( _, _, _ ) => { };
	}

	[TestCleanup]
	public void PutThePiecesBack()
	{
		ParkBuildMode.Forget();
		ParkTrackTool.ClipTo = _clipTo!;
		ParkTrackTool.PulseTo = _pulseTo!;
	}

	private static (ParkState State, ParkWorld.CatalogueObject Ride) Standing( int x = 30, int y = 24, int angle = 0 )
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: 53522 );
		ParkTrackTool.Forget();
		ParkTrackPieces.LayStation( state, 1, x, y, angle );

		var ride = new ParkWorld.CatalogueObject( state.NextThingId(), 1150, x << 8, y << 8, angle, TrackRide: state.TrackRides.Take( -4 ) );
		state.AddObject( ride );

		return (state, ride);
	}

	private static (ParkState State, ParkWorld.CatalogueObject Ride) Open()
	{
		var (state, ride) = Standing();
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );

		return (state, ride);
	}

	private static (ParkState State, ParkWorld.CatalogueObject Ride) Closed()
	{
		var (state, ride) = Open();
		ParkTrackTool.LayRun( state, 0xb, Owner, (28, 29), (28, 25), 200 );

		return (state, ride);
	}

	private static (int, int)[] Cells( IEnumerable<(int X, int Y, bool Running)> clips ) => [.. clips.Select( c => (c.X, c.Y) )];

	/// <summary>
	/// On the closed loop the mouth's walk plays all eight anchors in the loop's order, through both bends, and stops
	/// short of the station it meets from the far side. Made to fail by stopping at a bend, by playing the station, or by
	/// leaving a bend the way it was entered.
	/// </summary>
	[TestMethod]
	public void TheMouthsWalkPlaysTheClosedLoopInOrder()
	{
		var (state, _) = Closed();

		var played = ParkTrackTool.PlayPieces( state, 30, 24, 0, fromOtherMouth: false, running: true );

		CollectionAssert.AreEqual( Loop, played );
		CollectionAssert.AreEqual( Loop.Select( c => (c.Item1, c.Item2, true) ).ToArray(), _clips );
	}

	/// <summary>
	/// On the open loop the mouth's walk stops on the open end (28,29), and the other mouth's walk, heading west, plays the
	/// other mouth (28,25) alone, an end that links no further that way. Made to fail by walking the other mouth on the
	/// turn's heading, which goes east into the station.
	/// </summary>
	[TestMethod]
	public void OnTheOpenLoopEachWalkStopsOnItsOwnEnd()
	{
		var (state, _) = Open();

		CollectionAssert.AreEqual( Loop[..6], ParkTrackTool.PlayPieces( state, 30, 24, 0, fromOtherMouth: false, running: false ) );
		CollectionAssert.AreEqual( new[] { (28, 25) }, ParkTrackTool.PlayPieces( state, 30, 24, 0, fromOtherMouth: true, running: false ) );
	}

	/// <summary>
	/// At turn 90 the other mouth is (31,25) and its walk heads south along the run laid from it, playing all three; made to
	/// fail by heading north, which meets the station at once and plays the other mouth alone.
	/// </summary>
	[TestMethod]
	public void AtTurnNinetyTheOtherMouthsWalkHeadsSouth()
	{
		var (state, _) = Standing( angle: 90 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (31, 25), (31, 29), 200 );

		CollectionAssert.AreEqual( new[] { (31, 25), (31, 27), (31, 29) },
			ParkTrackTool.PlayPieces( state, 30, 24, 90, fromOtherMouth: true, running: false ) );
	}

	/// <summary>
	/// A bend entered with no link back leaves by the link it has beyond the one it was entered along: (34,25) given links
	/// east and south, entered going east, leaves south and the walk plays on to the open end. Made to fail by always taking
	/// the way in as the opposite of the heading, which leaves the walk standing on the bend.
	/// </summary>
	[TestMethod]
	public void ABendEnteredWithNoLinkBackLeavesTheWayItHas()
	{
		var (state, _) = Open();
		state.SetRecord( 34, 25, state.Record( 34, 25 ) with { TrackNeighbours = 0x14 } );

		CollectionAssert.AreEqual( Loop[..6], ParkTrackTool.PlayPieces( state, 30, 24, 0, fromOtherMouth: false, running: false ) );
	}

	/// <summary>
	/// A cell that is not track stops the walk there, its links or not: (34,27) made type 0 with its links kept, the walk
	/// plays the mouth and stops. Made to fail by keeping the heading over a cell that is not track.
	/// </summary>
	[TestMethod]
	public void ACellThatIsNotTrackStopsTheWalk()
	{
		var (state, _) = Closed();
		state.SetRecord( 34, 27, state.Record( 34, 27 ) with { TrackType = 0 } );

		CollectionAssert.AreEqual( new[] { (34, 25) }, ParkTrackTool.PlayPieces( state, 30, 24, 0, fromOtherMouth: false, running: true ) );
	}

	/// <summary>
	/// A cell whose piece has no model is passed over and the walk goes on: the recorder answering no for (34,27) leaves it
	/// out of the answer and the rest played. Made to fail by stopping where nothing was played.
	/// </summary>
	[TestMethod]
	public void ACellWithNoModelIsPassedOver()
	{
		var (state, _) = Closed();
		ParkTrackTool.ClipTo = ( x, y, _ ) => (x, y) != (34, 27);

		CollectionAssert.AreEqual( Loop.Where( c => c != (34, 27) ).ToArray(),
			ParkTrackTool.PlayPieces( state, 30, 24, 0, fromOtherMouth: false, running: true ) );
	}

	/// <summary>
	/// The closing click puts every piece at rest from the mouth alone where its walk answered 1 (<c>0x00526f58</c>): the
	/// eight anchors, none running. Made to fail by passing the add-on's 1, or by walking the other mouth too.
	/// </summary>
	[TestMethod]
	public void TheClosingClickPutsTheLoopAtRest()
	{
		var (state, ride) = Open();
		ParkBuildMode.ArmAt( ParkBuildMode.KartTrack, ride.ThingId, 28, 29 );

		ParkTrackTool.RunOn( state, null, null, ride, 1, 200, 28, 25, true );

		CollectionAssert.AreEqual( Loop.Select( c => (c.Item1, c.Item2, false) ).ToArray(), _clips );
	}

	/// <summary>
	/// A click that puts the tool away on an open loop (the run met track short of closing) walks both mouths at rest: the
	/// mouth's six to the open end, then the other mouth (<c>0x00526f69</c>..<c>0x00526f7f</c>). Made to fail by skipping the
	/// other mouth's walk where the circuit is not valid.
	/// </summary>
	[TestMethod]
	public void AnOpenLoopsCloseWalksBothMouthsAtRest()
	{
		var (state, ride) = Open();
		_clips.Clear();

		Assert.AreEqual( 7, ParkTrackTool.PlayPiecesAfterWalk( state, ride, valid: 0, runningWhereValid: true ) );
		CollectionAssert.AreEqual( Loop[..6].Append( (28, 25) ).Select( c => (c.Item1, c.Item2, false) ).ToArray(), _clips );
	}

	/// <summary>
	/// An add-on's put-down on the closed loop walks the circuit (1) and plays clip 5 on every piece from the mouth
	/// (<c>0x00524d26</c>); on the open loop it answers nought and both mouths' pieces rest. Made to fail by passing the
	/// closing click's nought, or by playing clip 5 where the circuit is not valid.
	/// </summary>
	[TestMethod]
	public void AnAddOnsPutDownPlaysTheClosedLoopAndRestsAnOpenOne()
	{
		var (state, ride) = Closed();

		Assert.AreEqual( 1, ParkTrackTool.WalkAfterAddOn( state, null, null, ride, 1 ).Valid );
		CollectionAssert.AreEqual( Loop.Select( c => (c.Item1, c.Item2, true) ).ToArray(), _clips );

		(state, ride) = Open();
		_clips.Clear();

		Assert.AreEqual( 0, ParkTrackTool.WalkAfterAddOn( state, null, null, ride, 1 ).Valid );
		Assert.AreEqual( 7, _clips.Count );
		Assert.IsTrue( _clips.All( c => !c.Running ), "an open loop's pieces rest" );
	}
}
