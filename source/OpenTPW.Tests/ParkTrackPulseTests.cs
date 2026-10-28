using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The karts' and the water rides' open end, which pulses red to white and back - <see cref="ParkTrackTool.Shape"/>, the
/// mouths' walks (<c>TrackRide_ShapeFromMouth</c> <c>0x0052c6f0</c>, <c>TrackRide_ShapeFromOtherMouth</c> <c>0x0052ccb0</c>),
/// where each site runs them and with which value, and the pulse's colour (<c>Model_PulseTick</c> <c>0x00469840</c>;
/// <c>docs/exe/park.md</c>, "A track piece's model, price and open end"). The loop is the original's own, as
/// <see cref="ParkTrackToolTests"/>: a Dino Karts on (30,24) at turn 0, runs to (34,29), (28,29) and back to (28,25).
/// </summary>
[TestClass]
public class ParkTrackPulseTests
{
	private static readonly ushort Owner = (ushort)MapStep.CellId( 30, 24 );

	private readonly List<(int X, int Y, bool On)> _pulses = [];
	private System.Action<int, int, bool>? _pulseTo;

	[TestInitialize]
	public void RecordThePulses()
	{
		ParkBuildMode.Forget();
		_pulseTo = ParkTrackTool.PulseTo;
		ParkTrackTool.PulseTo = ( x, y, on ) => _pulses.Add( (x, y, on) );
	}

	[TestCleanup]
	public void PutThePiecesBack()
	{
		ParkBuildMode.Forget();
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

	private static ParkTrackTool.Shaped Mouth( ParkState state, int angle = 0 ) => ParkTrackTool.Shape( state, 30, 24, angle, fromOtherMouth: false );
	private static ParkTrackTool.Shaped Other( ParkState state, int angle = 0 ) => ParkTrackTool.Shape( state, 30, 24, angle, fromOtherMouth: true );

	/// <summary>
	/// Each walk stops on its own open end: at the put-down the two mouths, after two runs the second run's end past two
	/// bends and still the other mouth, and on the closed loop each meets the station from its far side. Made to fail by
	/// walking the other mouth on the turn's heading, by stopping at a bend, or by not meeting the station.
	/// </summary>
	[TestMethod]
	public void EachWalkStopsOnItsOwnOpenEnd()
	{
		var (state, _) = Standing();

		Assert.AreEqual( new ParkTrackTool.Shaped( false, (34, 25) ), Mouth( state ) );
		Assert.AreEqual( new ParkTrackTool.Shaped( false, (28, 25) ), Other( state ) );

		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );

		Assert.AreEqual( new ParkTrackTool.Shaped( false, (28, 29) ), Mouth( state ) );
		Assert.AreEqual( new ParkTrackTool.Shaped( false, (28, 25) ), Other( state ) );

		ParkTrackTool.LayRun( state, 0xb, Owner, (28, 29), (28, 25), 200 );

		Assert.AreEqual( new ParkTrackTool.Shaped( true, null ), Mouth( state ), "the closed loop meets the station" );
		Assert.AreEqual( new ParkTrackTool.Shaped( true, null ), Other( state ), "from either mouth" );
	}

	/// <summary>
	/// At turn 90 the mouth's walk heads north from (31,19) along the run laid there; made to fail by heading the way of
	/// turn 0's or the other mouth's, which walks back through the station to the other mouth (31,25).
	/// </summary>
	[TestMethod]
	public void AtTurnNinetyTheMouthsWalkHeadsNorth()
	{
		var (state, _) = Standing( angle: 90 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (31, 19), (31, 15), 200 );

		Assert.AreEqual( new ParkTrackTool.Shaped( false, (31, 15) ), Mouth( state, 90 ) );
		Assert.AreEqual( new ParkTrackTool.Shaped( false, (31, 25) ), Other( state, 90 ) );
	}

	/// <summary>A bend entered past its links leaves by the link it has beyond the one it was entered along, and the walk goes on.</summary>
	[TestMethod]
	public void ABendEnteredWithNoLinkBackLeavesTheWayItHas()
	{
		var (state, _) = Standing();
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );

		// Entered going east, (34,25) has links east and south and none back west: it leaves south.
		state.SetRecord( 34, 25, state.Record( 34, 25 ) with { TrackNeighbours = 0x14 } );

		Assert.AreEqual( new ParkTrackTool.Shaped( false, (28, 29) ), Mouth( state ) );
	}

	/// <summary>
	/// An open end beside the station's near side is no meeting: (30,23) heads south onto the station (30,25), which links
	/// east and west only, so the walk answers nought there. Made to fail by meeting any station two cells on.
	/// </summary>
	[TestMethod]
	public void AStationThatDoesNotLinkBackIsNotMet()
	{
		var (state, _) = Standing();
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 21), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 21), (30, 21), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (30, 21), (30, 23), 200 );

		Assert.AreEqual( new ParkTrackTool.Shaped( false, (30, 23) ), Mouth( state ) );
	}

	/// <summary>
	/// The closed loop's station named as another ride's is not met, so the walk goes round and round and stops on nothing.
	/// Made to fail by meeting a station of any owner.
	/// </summary>
	[TestMethod]
	public void AnotherRidesStationIsNotMet()
	{
		var (state, _) = Standing();
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (28, 29), (28, 25), 200 );
		state.SetRecord( 30, 25, state.Record( 30, 25 ) with { TrackParentId = (ushort)MapStep.CellId( 60, 60 ) } );

		Assert.AreEqual( new ParkTrackTool.Shaped( false, null ), Mouth( state ) );
	}

	/// <summary>
	/// Where the first walk of a pair that asks for it answers 1, here from a mouth that is no track, the second is not
	/// walked and the other mouth's open end is left as it was; without the ask it is walked. Made to fail by always walking it.
	/// </summary>
	[TestMethod]
	public void APairSkipsItsSecondWalkWhereTheFirstMetTheStation()
	{
		var (state, _) = Standing();
		state.SetRecord( 34, 25, state.Record( 34, 25 ) with { TrackType = 0 } );

		var (mouth, other) = ParkTrackTool.ShapeMouths( state, 30, 24, 0, mouthPulses: true, onlyWhereOpen: true );

		Assert.AreEqual( (new ParkTrackTool.Shaped( true, null ), (ParkTrackTool.Shaped?)null), (mouth, other) );
		CollectionAssert.AreEqual( new (int, int, bool)[0], _pulses );

		ParkTrackTool.ShapeMouths( state, 30, 24, 0, mouthPulses: true, onlyWhereOpen: false );

		CollectionAssert.AreEqual( new[] { (28, 25, false) }, _pulses );
	}

	/// <summary>A walk from a mouth that is not track answers 1 and pulses nothing (<c>0x0052c7b4</c>..<c>0x0052c7c2</c>).</summary>
	[TestMethod]
	public void AWalkFromNoTrackAnswersOneAtOnce()
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: 0 );

		Assert.AreEqual( new ParkTrackTool.Shaped( true, null ), Mouth( state ) );
		Assert.AreEqual( new ParkTrackTool.Shaped( true, null ), Other( state ) );
	}

	/// <summary>
	/// The put-down's op 0x86 pair: the mouth pulses and the other mouth's walk, its first not having met the station, stops
	/// its own. Made to fail by giving the pair the arming's values.
	/// </summary>
	[TestMethod]
	public void ThePutDownLeavesTheMouthPulsing()
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: 53522 );

		Assert.AreEqual( 4, ParkTrackTool.PutDown( state, 1, 30, 24, 0 ) );
		CollectionAssert.AreEqual( new[] { (34, 25, true), (28, 25, false) }, _pulses );
	}

	/// <summary>
	/// A run's op 0x86 pulses the mouth's open end and stops the other mouth's, and the moving click's own pair then flips
	/// them, so the other mouth is left pulsing (<c>q587/orig/B-3-run1.png</c>). Made to fail by swapping either pair's values.
	/// </summary>
	[TestMethod]
	public void AMovingClickLeavesTheOtherMouthPulsing()
	{
		var (state, ride) = Standing();
		ParkBuildMode.ArmAt( ParkBuildMode.KartTrack, ride.ThingId, 34, 25 );
		ParkBuildMode.Push( 34, 25 );

		ParkTrackTool.RunOn( state, null, null, ride, 1, 200, 34, 29, true );

		CollectionAssert.AreEqual( new[] { (34, 29, true), (28, 25, false), (34, 29, false), (28, 25, true) }, _pulses );
	}

	/// <summary>The arming walks from the mouth with 0 and from the other mouth with 1, whatever the first met.</summary>
	[TestMethod]
	public void ArmingStopsTheMouthsEndAndPulsesTheOtherMouth()
	{
		var (state, _) = Standing();
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );
		_pulses.Clear();

		ParkTrackTool.ArmOn( state, _ => 1, 35, 26 );

		CollectionAssert.AreEqual( new[] { (28, 29, false), (28, 25, true) }, _pulses );
	}

	/// <summary>
	/// The run that closes the loop: op 0x86's walk from the mouth meets the station, so its other walk is skipped, and the
	/// close's pair meets it from both mouths, so nothing pulses. Made to fail by running op 0x86's second walk regardless.
	/// </summary>
	[TestMethod]
	public void TheClosingRunPulsesNothing()
	{
		var (state, ride) = Standing();
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );
		ParkBuildMode.ArmAt( ParkBuildMode.KartTrack, ride.ThingId, 28, 29 );
		_pulses.Clear();

		ParkTrackTool.RunOn( state, null, null, ride, 1, 200, 28, 25, true );

		CollectionAssert.AreEqual( new (int, int, bool)[0], _pulses );
	}

	/// <summary>
	/// A click that closes the tool on an open loop runs the close's pair: the mouth's open end pulses and the other mouth's
	/// stops (<c>0x00526f3c</c>, <c>0x00526f4e</c>). Made to fail by giving the close the moving click's values.
	/// </summary>
	[TestMethod]
	public void AClickThatClosesTheToolOnAnOpenLoopPulsesTheMouthsEnd()
	{
		var (state, ride) = Standing();
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkBuildMode.ArmAt( ParkBuildMode.KartTrack, ride.ThingId, 34, 29 );
		ParkBuildMode.Push( 34, 29 );
		_pulses.Clear();

		ParkTrackTool.RunOn( state, null, null, ride, 1, 200, 34, 29, true );

		CollectionAssert.AreEqual( new[] { (34, 29, true), (28, 25, false) }, _pulses );
	}

	/// <summary>
	/// Backspace's pair: the open end left stops and the other mouth pulses, and with nothing to take back the same.
	/// Made to fail by swapping its values or dropping either branch's pair.
	/// </summary>
	[TestMethod]
	public void BackspaceLeavesTheOtherMouthPulsing()
	{
		var (state, ride) = Standing();
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );
		ParkBuildMode.ArmAt( ParkBuildMode.KartTrack, ride.ThingId, 28, 29 );

		foreach ( var at in new[] { (34, 25), (34, 25), (34, 29), (28, 29) } )
			ParkBuildMode.Push( at.Item1, at.Item2 );

		_pulses.Clear();
		ParkTrackTool.BacktrackOn( state, ride, 200 );

		CollectionAssert.AreEqual( new[] { (34, 29, false), (28, 25, true) }, _pulses );

		ParkBuildMode.Forget();
		ParkBuildMode.ArmAt( ParkBuildMode.KartTrack, ride.ThingId, 34, 29 );
		ParkBuildMode.Push( 34, 25 );
		_pulses.Clear();

		Assert.IsTrue( ParkTrackTool.BacktrackOn( state, ride, 200 ).Contains( "nothing laid" ) );
		CollectionAssert.AreEqual( new[] { (34, 29, false), (28, 25, true) }, _pulses );
	}

	/// <summary>
	/// The pulse's green and blue: the phase's triangle times 255, cut to a whole number, so 127 at a half and not 128, and
	/// 255 at 1; the phase moves on by twice the frame's seconds and wraps at 2.
	/// </summary>
	[TestMethod]
	public void ThePulseRisesToWhiteAndFallsBackOnceASecond()
	{
		Assert.AreEqual( 0, ParkTrackPieces.PulseValue( 0f ) );
		Assert.AreEqual( 127, ParkTrackPieces.PulseValue( 0.5f ) );
		Assert.AreEqual( 255, ParkTrackPieces.PulseValue( 1f ) );
		Assert.AreEqual( 191, ParkTrackPieces.PulseValue( 1.25f ) );
		Assert.AreEqual( 12, ParkTrackPieces.PulseValue( 1.95f ) );

		Assert.AreEqual( 0.75f, ParkTrackPieces.PulsePhaseAfter( 0.25f, 0.25f ), 1e-6f );
		Assert.AreEqual( 0.1f, ParkTrackPieces.PulsePhaseAfter( 1.9f, 0.1f ), 1e-5f, "wrapped at 2" );

		var phase = 0f;

		for ( var frame = 0; frame < 144; ++frame )
			phase = ParkTrackPieces.PulsePhaseAfter( phase, 1f / 144f );

		Assert.AreEqual( 0f, phase % 2f < 1f ? phase : phase - 2f, 1e-4f, "a second is one whole beat" );
	}
}
