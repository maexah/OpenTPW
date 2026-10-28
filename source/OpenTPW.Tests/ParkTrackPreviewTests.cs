using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
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

	private static readonly Func<ParkState, ushort, int?> LevelPriority = ParkTrackTool.OverwritePriorityOf;

	/// <summary>A ride's category priority, 5; a feature's is 0 or 1.</summary>
	private const int RidePriority = 5;

	private static int TimesReported( string what )
		=> Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	[TestInitialize]
	[TestCleanup]
	public void ForgetTheTool()
	{
		ParkBuildMode.Forget();
		ParkTrackTool.OverwritePriorityOf = LevelPriority;
	}

	/// <summary>Stands every footprint cell's owner at this priority, as the item catalogue would answer.</summary>
	private static void OwnersHold( int priority ) => ParkTrackTool.OverwritePriorityOf = ( _, owner ) => owner == Owner ? priority : null;

	/// <summary>Stamps a footprint cell owned by the bought ride.</summary>
	private static void Footprint( ParkState state, int x, int y )
		=> state.SetRecord( x, y, state.Record( x, y ) with { Type = CellEdge.Footprint, ParentId = Owner } );

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
		OwnersHold( RidePriority );
		var state = Bought();
		Footprint( state, 35, 28 );

		var preview = ParkTrackTool.Preview( state, 0xb, Owner, (34, 25), (34, 31), 200 );

		Assert.AreEqual( "(34,25)0/0 (34,27)1/400 (34,29)1/600 (34,31)1/800", Read( preview ),
			"the object's cell is charged by both judgements, the later ones by their second" );
		Assert.IsTrue( preview.Latched, "the pending refusal latched at once, by the red square's second judgement" );
	}

	/// <summary>
	/// The copy of the pending refusal into the latch (<c>0x00539dbc</c>..<c>0x00539dca</c>) comes before the verdict reads its
	/// third argument, so the second judgement of a red square latches it at the same anchor: a run whose last anchor is
	/// refused after the charge leaves the latch set. Made to fail by latching only at the next anchor.
	/// </summary>
	[TestMethod]
	public void AnAfterRefusalAtTheLastAnchorLeavesTheLatchSet()
	{
		OwnersHold( RidePriority );
		var state = Bought();
		Footprint( state, 34, 30 );

		var preview = ParkTrackTool.Preview( state, 0xb, Owner, (34, 25), (34, 29), 200 );

		Assert.AreEqual( "(34,25)0/0 (34,27)0/200 (34,29)1/600", Read( preview ) );
		Assert.IsTrue( preview.Latched, "no anchor follows, and the latch is set" );
	}

	/// <summary>
	/// A land refusal is judged first and touches no latch: an After refusal's latch stands through a land anchor and reds
	/// the plain one after it. Made to fail by a land arm that clears the latch.
	/// </summary>
	[TestMethod]
	public void AnAfterRefusalThenLandThenAPlainAnchorStaysLatched()
	{
		OwnersHold( RidePriority );
		var state = Bought();
		Footprint( state, 35, 28 );
		state.SetRecord( 34, 29, state.Record( 34, 29 ) with { Flags = 0x40 } );

		var preview = ParkTrackTool.Preview( state, 0xb, Owner, (34, 25), (34, 31), 200 );

		Assert.AreEqual( "(34,25)0/0 (34,27)1/400 (34,29)1/400 (34,31)1/600", Read( preview ),
			"the land square charges nothing; the plain anchor is red by the latch, charged once, by its second judgement" );
		Assert.IsTrue( preview.Latched );
	}

	/// <summary>
	/// The verdict answers 0 over a feature (a thing whose item's priority is below 3): blue, and the cell is not charged
	/// (<c>0x00539961</c>..<c>0x00539c24</c>). A ride's priority 5 and a priority of 3 are red, as a map type 4's cell is.
	/// </summary>
	[TestMethod]
	public void AFeatureUnderTheRunIsBlueAndUnchargedAndARideIsRed()
	{
		var state = Bought();
		Footprint( state, 35, 28 );

		OwnersHold( RidePriority );
		Assert.IsTrue( ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 29), 200 ).Refused );

		OwnersHold( 3 );
		Assert.IsTrue( ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 29), 200 ).Refused, "priority 3 is not below 3" );

		foreach ( var feature in new[] { 0, 1 } )
		{
			OwnersHold( feature );
			var before = TimesReported( "TRACK_RUN_OVER_SCENERY_MIXED" );
			var verdict = ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 29), 200 );

			Assert.IsFalse( verdict.Refused, $"a feature of priority {feature}: {verdict.Why}" );
			Assert.AreEqual( 200, verdict.Total, "the anchor over the feature is not charged, the one beyond it is" );
			Assert.AreEqual( before, TimesReported( "TRACK_RUN_OVER_SCENERY_MIXED" ) );
			Assert.AreEqual( "(34,25)0/0 (34,27)0/0 (34,29)0/200",
				Read( ParkTrackTool.Preview( state, 0xb, Owner, (34, 25), (34, 29), 200 ) ), "blue squares all the way" );
		}
	}

	/// <summary>
	/// The verdict asks the anchor, its east cell and its south-east cell about the owner, never its south cell, nor a cell
	/// of the run's other anchors. Made to fail by asking the south cell as well.
	/// </summary>
	[TestMethod]
	public void OnlyTheAnchorsEastAndSouthEastCellsAreAskedTheirPriority()
	{
		OwnersHold( 0 );

		foreach ( var (cx, cy, asked) in new[] { (34, 27, true), (35, 27, true), (35, 28, true), (34, 28, false) } )
		{
			var state = Bought();
			Footprint( state, cx, cy );

			var verdict = ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 27), 200 );

			Assert.AreEqual( !asked, verdict.Refused, $"({cx},{cy}) is a footprint: blue where it is asked, red where it is not" );
		}
	}

	/// <summary>
	/// The blue answer comes at once: it latches nothing, even where another ride's track beside an earlier anchor has latched,
	/// and it follows the land check, which is red whatever a cell of the anchor holds.
	/// </summary>
	[TestMethod]
	public void TheBlueAnswerComesAfterTheLandAndBeforeTheLatch()
	{
		var state = Bought();
		OwnersHold( 0 );
		Footprint( state, 35, 30 );
		state.SetRecord( 35, 28, state.Record( 35, 28 ) with { TrackType = 0xb, TrackParentId = (ushort)MapStep.CellId( 60, 60 ) } );

		var preview = ParkTrackTool.Preview( state, 0xb, Owner, (34, 25), (34, 31), 200 );

		Assert.AreEqual( "(34,25)0/0 (34,27)1/200 (34,29)0/200 (34,31)1/400", Read( preview ),
			"the track beside (34,27) latches, then red; (34,29) holds a feature at its south-east cell and answers blue before the latch is read; (34,31) is red by it" );

		var land = Bought();
		Footprint( land, 35, 27 );
		land.SetRecord( 34, 28, land.Record( 34, 28 ) with { Flags = 0x40 } );

		var mixed = TimesReported( "TRACK_RUN_OVER_SCENERY_MIXED" );

		Assert.IsTrue( ParkTrackTool.Verdict( land, 0xb, Owner, (34, 25), (34, 27), 200 ).Refused, "land outside the park is asked first" );
		Assert.AreEqual( mixed, TimesReported( "TRACK_RUN_OVER_SCENERY_MIXED" ), "plain red, where the original's is: nothing to count" );
	}

	/// <summary>
	/// Where a cell of the anchor beside the feature would be refused by a rule of this game, the refusal stands and the run is
	/// counted (<c>TRACK_RUN_OVER_SCENERY_MIXED</c>): the original lays its piece over that cell too.
	/// </summary>
	[TestMethod]
	public void AFeatureBesideAPathIsRefusedAndCounted()
	{
		var state = Bought();
		OwnersHold( 0 );
		Footprint( state, 35, 27 );
		state.SetRecord( 34, 28, state.Record( 34, 28 ) with { Type = CellEdge.Path } );

		var before = TimesReported( "TRACK_RUN_OVER_SCENERY_MIXED" );
		var overPath = TimesReported( "TRACK_RUN_OVER_PATH" );

		Assert.IsTrue( ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 27), 200 ).Refused );
		Assert.AreEqual( before + 1, TimesReported( "TRACK_RUN_OVER_SCENERY_MIXED" ) );
		Assert.AreEqual( overPath, TimesReported( "TRACK_RUN_OVER_PATH" ), "the mixed count stands in for the path's" );
	}

	/// <summary>
	/// A rock of two by two covers all four of the anchor's cells: the south cell, never asked, is part of the same feature
	/// and goes with it. A ride's footprint on the south cell beside a feature is refused and counted instead.
	/// </summary>
	[TestMethod]
	public void AFeatureOverAllFourCellsIsBlueAndARideOnTheSouthCellIsMixed()
	{
		var state = Bought();
		OwnersHold( 0 );

		foreach ( var (cx, cy) in new[] { (34, 27), (35, 27), (34, 28), (35, 28) } )
			Footprint( state, cx, cy );

		Assert.AreEqual( "(34,25)0/0 (34,27)0/0", Read( ParkTrackTool.Preview( state, 0xb, Owner, (34, 25), (34, 27), 200 ) ),
			"the whole rock under the square: blue, and not charged" );

		var ride = (ushort)MapStep.CellId( 60, 60 );
		ParkTrackTool.OverwritePriorityOf = ( _, owner ) => owner == ride ? RidePriority : owner == Owner ? 0 : null;
		state.SetRecord( 34, 28, state.Record( 34, 28 ) with { ParentId = ride } );

		var before = TimesReported( "TRACK_RUN_OVER_SCENERY_MIXED" );

		Assert.IsTrue( ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 27), 200 ).Refused );
		Assert.AreEqual( before + 1, TimesReported( "TRACK_RUN_OVER_SCENERY_MIXED" ) );
	}

	/// <summary>
	/// The click's op 0x87 takes down the thing under each anchor that is not track, in the cell order (x,y), (x+1,y), (x,y+1),
	/// (x+1,y+1), and finds nothing under an anchor that is.
	/// </summary>
	[TestMethod]
	public void TheClearPassTearsDownWhatStandsUnderTheNonTrackAnchors()
	{
		var state = Bought();
		var sold = new System.Collections.Generic.List<int>();

		state.AddObject( new ParkWorld.CatalogueObject( 701, 1150, 60 << 8, 60 << 8, 0 ) );
		state.AddObject( new ParkWorld.CatalogueObject( 702, 1150, 61 << 8, 61 << 8, 0 ) );
		state.AddObject( new ParkWorld.CatalogueObject( 704, 1150, 62 << 8, 62 << 8, 0 ) );
		state.SetRecord( 34, 28, state.Record( 34, 28 ) with { Type = CellEdge.Footprint, ParentId = (ushort)MapStep.CellId( 62, 62 ) } );
		state.SetRecord( 35, 28, state.Record( 35, 28 ) with { Type = CellEdge.Footprint, ParentId = (ushort)MapStep.CellId( 60, 60 ) } );
		state.SetRecord( 34, 27, state.Record( 34, 27 ) with { Type = CellEdge.RideEnd, ParentId = (ushort)MapStep.CellId( 61, 61 ) } );
		state.SetRecord( 34, 25, state.Record( 34, 25 ) with { Type = CellEdge.Footprint, ParentId = (ushort)MapStep.CellId( 61, 61 ) } );

		var was = ParkTrackTool.TearDownThing;
		ParkTrackTool.TearDownThing = id => { sold.Add( id ); return "sold"; };

		try
		{
			ParkTrackTool.TearDownUnder( state, [(34, 25), (34, 27), (34, 29)] );
		}
		finally
		{
			ParkTrackTool.TearDownThing = was;
		}

		Assert.AreEqual( "702,704,701", string.Join( ",", sold ),
			"(34,25) is the station's mouth, track, and is passed over; (34,27) takes (34,27), then (34,28), then (35,28)" );
	}

	/// <summary>
	/// A run's click tears the feature down before it stamps the piece over its cell, and charges that piece as any other:
	/// the verdict's total leaves the cell out, the stamp does not.
	/// </summary>
	[TestMethod]
	public void LayingARunTearsTheFeatureDownThenStampsAndChargesTheCell()
	{
		var state = Bought();
		var sold = new System.Collections.Generic.List<int>();

		state.AddObject( new ParkWorld.CatalogueObject( 703, 1150, 60 << 8, 60 << 8, 0 ) );
		state.SetRecord( 34, 29, state.Record( 34, 29 ) with { Type = CellEdge.Footprint, ParentId = (ushort)MapStep.CellId( 60, 60 ) } );
		ParkTrackTool.OverwritePriorityOf = ( _, _ ) => 0;

		var was = ParkTrackTool.TearDownThing;
		ParkTrackTool.TearDownThing = id => { sold.Add( id ); return "sold"; };

		try
		{
			var verdict = ParkTrackTool.Verdict( state, 0xb, Owner, (34, 25), (34, 29), 200 );
			var laid = ParkTrackTool.LayRun( state, 0xb, Owner, (34, 25), (34, 29), 200 );

			Assert.AreEqual( (false, 200), (verdict.Refused, verdict.Total), "the preview's total leaves the feature's anchor out" );
			Assert.AreEqual( "703", string.Join( ",", sold ) );
			Assert.AreEqual( (2, 400), (laid.NewPieces, laid.Charged), "the stamp charges the cell all the same" );
			Assert.AreEqual( 0xb, state.Record( 34, 29 ).TrackType );
		}
		finally
		{
			ParkTrackTool.TearDownThing = was;
		}
	}

	/// <summary>
	/// The square's lift starts from the anchor's own track record type (<c>0x0053716f</c>..<c>0x00537239</c>), so a laid piece on
	/// plain ground counts the lift gap where no map type does. Made to fail by reading map types alone.
	/// </summary>
	[TestMethod]
	public void ALaidPieceUnderTheSquareCountsTheLiftGap()
	{
		var state = Bought();
		var before = TimesReported( "MARKER_LIFT_OVER_TRACK_THING" );

		ParkBuildMarkers.TrackLiftOver( state, 40, 40 );
		Assert.AreEqual( before, TimesReported( "MARKER_LIFT_OVER_TRACK_THING" ), "bare ground lifts nothing" );

		state.SetRecord( 40, 40, state.Record( 40, 40 ) with { TrackType = 0xb } );
		ParkBuildMarkers.TrackLiftOver( state, 40, 40 );
		Assert.AreEqual( before + 1, TimesReported( "MARKER_LIFT_OVER_TRACK_THING" ), "counted once" );
	}

	/// <summary>
	/// The catalogue reads <c>Info.OverwritePriority</c> as the data states it (FileFormats <c>sam.md</c>): the category's 5 for every
	/// ride, shop and sideshow, an item's own for a feature (<c>bigpalm</c> 0, <c>bush</c> 1), 5 again for the End. Made to fail
	/// by leaving the key unread.
	/// </summary>
	[TestMethod]
	public void TheCatalogueReadsEachItemsOverwritePriority()
	{
		var catalogue = new ParkItemCatalogue( "jungle", GameData.Required(), instantAction: false );
		var byStem = catalogue.All.ToDictionary( item => item.Stem, StringComparer.OrdinalIgnoreCase );

		Assert.AreEqual( 0, byStem["bigpalm"].OverwritePriority );
		Assert.AreEqual( 1, byStem["bush"].OverwritePriority );
		Assert.IsTrue( catalogue.All.Where( item => item.Id is >= 1100 and < 1400 ).All( item => item.OverwritePriority == 5 ),
			"every ride, shop and sideshow keeps its category's 5" );
		Assert.AreEqual( 5, catalogue.TryGet( 1605, out var end ) ? end.OverwritePriority : -1, "the End keeps 5" );
	}

	/// <summary>The water ride's track is the same rules over its own anchor type, 0x10, whose mouth a karts' run refuses.</summary>
	[TestMethod]
	public void TheWaterRidesPreviewIsTheSameRulesOverItsOwnType()
	{
		var state = new ParkState( parkIsClosed: false, visitorsToDate: 0, balance: 53522 );
		ParkTrackTool.Forget();
		ParkTrackPieces.LayStation( state, 2, 30, 24, 0 );

		var water = ParkTrackTool.Preview( state, 0x10, Owner, (34, 25), (34, 29), 200 );

		Assert.AreEqual( "(34,25)0/0 (34,27)0/200 (34,29)0/400", Read( water ) );
		Assert.AreEqual( (false, 400), (water.Red, water.Total) );

		var karts = ParkTrackTool.Preview( state, 0xb, Owner, (34, 25), (34, 29), 200 );

		Assert.IsTrue( karts.Red, "the water ride's mouth is not a kart piece" );
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
