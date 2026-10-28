using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The armed track tool's preview - <see cref="ParkTrackTool.Preview"/>: a two-by-two square an anchor of the run, blue, red
/// or <c>m_end</c>, and the run's total with each red square charged twice (<c>docs/exe/park.md</c>, "The track tool",
/// the preview). The ride is the original's Dino Karts bought on (30,24) at turn 0, its mouth (34,25).
/// </summary>
[TestClass]
public class ParkTrackPreviewTests
{
	private static readonly ushort Owner = (ushort)MapStep.CellId( 30, 24 );

	[TestInitialize]
	[TestCleanup]
	public void ForgetTheTool() => ParkBuildMode.Forget();

	private static ParkState Bought( int balance = 53522 )
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: balance );
		ParkTrackTool.Forget();
		ParkTrackPieces.LayStation( state, 1, 30, 24, 0 );
		return state;
	}

	private static string Read( ParkTrackTool.Previewed preview )
		=> string.Join( " ", preview.Squares.Select( square => $"({square.X},{square.Y}){square.Marker}/{square.Owed}"
			+ (square.Unaffordable ? "$" : "") ) );

	[TestMethod]
	public void ARunIsABlueSquareAnAnchorOverItsTwoByTwo()
	{
		var preview = ParkTrackTool.Preview( Bought(), 0xb, Owner, (34, 25), (34, 29), 200 );

		Assert.AreEqual( "(34,25)0/0 (34,27)0/200 (34,29)0/400", Read( preview ), "the mouth is not charged, each new cell 200" );
		Assert.IsTrue( preview.Squares.All( square => square.Wide ), "each square covers its anchor's two by two" );
		Assert.AreEqual( (false, false, false, false, 400), (preview.Red, preview.End, preview.OverBalance, preview.Latched, preview.Total) );
		Assert.AreEqual( Input.CursorTypes.Track, ParkTrackTool.CursorOf( preview ) );
	}

	[TestMethod]
	public void ThePointerOnTheAnchorShowsMEnd()
	{
		var preview = ParkTrackTool.Preview( Bought(), 0xb, Owner, (34, 25), (34, 25), 200 );

		Assert.AreEqual( "(34,25)11/0", Read( preview ), "the run's one square is its first and its last, its own track" );
		Assert.AreEqual( Input.CursorTypes.End, ParkTrackTool.CursorOf( preview ) );
	}

	[TestMethod]
	public void ARunClosingOnItsOwnEndShowsMEndThere()
	{
		var state = Bought();
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );

		var preview = ParkTrackTool.Preview( state, 0xb, Owner, (28, 29), (28, 25), 200 );

		Assert.AreEqual( "(28,29)0/0 (28,27)0/200 (28,25)11/200", Read( preview ), "the other mouth closes the loop" );
		Assert.AreEqual( Input.CursorTypes.End, ParkTrackTool.CursorOf( preview ) );

		var crossing = ParkTrackTool.Preview( state, 0xb, Owner, (28, 29), (36, 29), 200 );

		Assert.AreEqual( "(28,29)1/0 (30,29)1/0 (32,29)1/0 (34,29)1/0 (36,29)1/200", Read( crossing ),
			"back against the anchor's way: it is red, its own track mid-run is red, and every square after it, each charged only as a red square's second judgement" );
	}

	[TestMethod]
	public void LeadingBackAgainstTheAnchorsWayRedsTheAnchor()
	{
		var preview = ParkTrackTool.Preview( Bought(), 0xb, Owner, (34, 25), (30, 25), 200 );

		Assert.AreEqual( "(34,25)1/0 (32,25)1/0 (30,25)1/0", Read( preview ), "the mouth, then its station's two sections" );

		var forward = ParkTrackTool.Preview( Bought(), 0xb, Owner, (34, 25), (34, 23), 200 );

		Assert.AreEqual( "(34,25)0/0 (34,23)0/200", Read( forward ), "across its way, the mouth stays blue" );
	}

	[TestMethod]
	public void LandOutsideTheParkIsRedAloneAndUncharged()
	{
		var state = Bought();
		state.SetRecord( 35, 28, state.Record( 35, 28 ) with { Flags = 0x40 } );

		var preview = ParkTrackTool.Preview( state, 0xb, Owner, (34, 25), (34, 31), 200 );

		Assert.AreEqual( "(34,25)0/0 (34,27)1/0 (34,29)0/200 (34,31)0/400", Read( preview ) );
		Assert.AreEqual( (true, false), (preview.Red, preview.Latched) );
		Assert.AreEqual( Input.CursorTypes.NoGo, ParkTrackTool.CursorOf( preview ) );

		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );
		ParkTrackTool.LayRun( state, 0xb, Owner, (34, 29), (28, 29), 200 );
		state.SetRecord( 29, 28, state.Record( 29, 28 ) with { Flags = 0x40 } );

		var closing = ParkTrackTool.Preview( state, 0xb, Owner, (28, 29), (28, 25), 200 );

		Assert.AreEqual( "(28,29)0/0 (28,27)1/0 (28,25)11/0", Read( closing ), "the land latches nothing: the last still meets its own end" );
		Assert.AreEqual( (true, true), (closing.Red, closing.End) );
		Assert.AreEqual( Input.CursorTypes.NoGo, ParkTrackTool.CursorOf( closing ), "a red square wins over c_end" );
	}

	[TestMethod]
	public void AnotherRidesTrackBesideLatchesBeforeTheCharge()
	{
		var state = Bought();
		state.SetRecord( 35, 28, state.Record( 35, 28 ) with { TrackType = 0xb, TrackParentId = (ushort)MapStep.CellId( 60, 60 ) } );

		var preview = ParkTrackTool.Preview( state, 0xb, Owner, (34, 25), (34, 31), 200 );

		Assert.AreEqual( "(34,25)0/0 (34,27)1/200 (34,29)1/400 (34,31)1/600", Read( preview ),
			"red from there on, each charged once, by its second judgement" );
		Assert.IsTrue( preview.Latched );
	}

	[TestMethod]
	public void AnObjectIsJudgedAfterTheChargeAndRedsTheRest()
	{
		var state = Bought();
		state.SetRecord( 35, 28, state.Record( 35, 28 ) with { Type = CellEdge.Footprint } );

		var preview = ParkTrackTool.Preview( state, 0xb, Owner, (34, 25), (34, 31), 200 );

		Assert.AreEqual( "(34,25)0/0 (34,27)1/400 (34,29)1/600 (34,31)1/800", Read( preview ),
			"the object's cell is charged by both judgements, the later ones by their second" );
		Assert.IsTrue( preview.Latched, "the pending refusal latched at the next cell" );
	}

	[TestMethod]
	public void TheMoneyRedsFromTheSquareThatPassesTheBalance()
	{
		var preview = ParkTrackTool.Preview( Bought( 500 ), 0xb, Owner, (34, 25), (34, 33), 200 );

		Assert.AreEqual( "(34,25)0/0 (34,27)0/200 (34,29)0/400 (34,31)1/800$ (34,33)1/1000", Read( preview ) );
		Assert.AreEqual( (true, true, true), (preview.Red, preview.OverBalance, preview.Latched) );
		Assert.AreEqual( Input.CursorTypes.Cash, ParkTrackTool.CursorOf( preview ) );
	}

	[TestMethod]
	public void AnAnchorOffTheMapHasNoSquare()
	{
		var state = Bought();

		var preview = ParkTrackTool.Preview( state, 0xb, Owner, (124, 40), (128, 40), 200 );

		Assert.AreEqual( "(124,40)0/200 (126,40)0/400", Read( preview ), "(128,40) is off the map and has none" );

		var edge = ParkTrackTool.Preview( state, 0xb, Owner, (123, 40), (127, 40), 200 );

		Assert.AreEqual( "(123,40)0/200 (125,40)0/400 (127,40)1/400", Read( edge ), "(127,40)'s two by two leaves the map: red, uncharged" );
		Assert.AreEqual( 400, ParkTrackTool.Verdict( state, 0xb, Owner, (123, 40), (127, 40), 200 ).Total, "the click charges it nothing either" );
	}
}
