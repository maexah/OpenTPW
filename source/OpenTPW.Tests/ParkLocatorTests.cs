using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoVeldrid;
using OpenTPW.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The staff and visitor locators, Ctrl+S and Ctrl+V (<c>FUN_004b3f60</c>, <c>FUN_004b4280</c>, the panel's handler
/// <c>FUN_004b3790</c>; <c>docs/exe/hud.md</c>, "The staff and visitor locators"), on the shipped park's people, the
/// buttons clicked where the arm puts them. Skipped without the game (<see cref="GameData"/>).
/// </summary>
[TestClass]
public class ParkLocatorTests
{
	private Level? _level;
	private Point2 _screen;
	private float _now;
	private Vector3 _looksAt;
	private ParkPeople _people = null!;
	private WindowStack _stack = null!;
	private ParkGadget _gadget = null!;

	[TestInitialize]
	public void LoadThePark()
	{
		_level = Level.Current;
		_screen = Screen.Size;
		_now = Time.Now;
		_looksAt = ParkOrbitCameraMode.PointOfInterest;

		Screen.Size = new Point2( 2048, 1536 );
		Log ??= new();
		FileSystem = GameData.Required();
		Input.Mouse = new();
		Unimplemented.Forget();

		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );
		var state = new ParkState( world );

		var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, world );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, state );
		typeof( Level ).GetProperty( nameof( Level.Catalogue ) )!.SetValue( level, catalogue );
		Level.Current = level;

		var balance = new ParkBalance( "jungle", easyMode: true );
		typeof( Level ).GetProperty( nameof( Level.Balance ) )!.SetValue( level, balance );
		_people = new ParkPeople( world, balance, null, state, catalogue );
		_stack = new WindowStack();
		_gadget = new ParkGadget( _stack );
		_stack.Open( _gadget );
		level.Locator = _gadget.Locator;
		typeof( Level ).GetField( "_windows", BindingFlags.NonPublic | BindingFlags.Instance )!.SetValue( level, _stack );

		ParkOrbitCameraMode.PointOfInterest = new Vector3( 300f, 300f, 0f );
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		_people?.Delete();
		Entity.ApplyDeletions();
		Level.Current = _level!;
		Screen.Size = _screen;
		Time.Now = _now;
		Input.Mouse = new();
		Input.Keyboard = new();
		ParkOrbitCameraMode.PointOfInterest = _looksAt;
		ParkBuildMode.Forget();
		Unimplemented.Forget();
	}

	private ParkLocator Locator => _gadget.Locator;

	private void CtrlS()
	{
		Locator.OpenStaff( _gadget.CarryLocatorPanel, () => _stack.CloseParkScreen() );
		ParkGadgetPressTests.Settle( _gadget );
	}

	private void CtrlV()
	{
		Locator.OpenVisitors( _gadget.CarryLocatorPanel, () => _stack.CloseParkScreen() );
		ParkGadgetPressTests.Settle( _gadget );
	}

	/// <summary>Escape over an open locator, and the arm's slide in run to its end.</summary>
	private bool Fold()
	{
		var folded = _gadget.FoldLocator();
		ParkGadgetPressTests.Settle( _gadget );
		return folded;
	}

	/// <summary>A button clicked at its middle, where the arm puts it.</summary>
	private void Click( int id )
	{
		var button = Find( _gadget.Root, id );
		Assert.IsTrue( button.Visible && button.Parent!.Visible && button.Parent.Parent!.Visible, $"{id:x} shown" );
		Assert.IsTrue( _stack.ClickAt( (button.Rect.Left + button.Rect.Right) / 2, (button.Rect.Top + button.Rect.Bottom) / 2 ), $"{id:x} clicked" );
	}

	/// <summary>
	/// <b>The arm puts a panel where its handler figures it</b>: the message bar's 820 and (486, 1112) from the same rule
	/// as the staff panel's 670 and (484, 1127), and the visitors' 670 and (485, 1127); each button moved with its panel.
	/// </summary>
	/// <remarks><b>Mutations:</b> the travel's hundred or its whole division; the middle not halved; the visitors' panel moved as the staff's.</remarks>
	[TestMethod]
	public void ThePanelsStandWhereTheArmPutsThem()
	{
		Assert.AreEqual( 820, ParkGadget.TravelFor( 615 ) );
		Assert.AreEqual( (486, 1112), ParkGadget.PlaceFor( new UiRect( 458, 1101, 1073, 1442 ) ), "the bar, measured in the original" );
		Assert.AreEqual( 670, ParkGadget.TravelFor( 467 ) );

		CtrlS();
		Assert.AreEqual( new UiRect( 484, 1127, 951, 1438 ), Locator.StaffPanel.Rect );
		Assert.AreEqual( new UiRect( 556, 1175, 659, 1278 ), Find( Locator.StaffPanel, 0x95e ).Rect, "the handymen's button" );
		Assert.AreEqual( new UiRect( 331, 1069, 1006 - 645 + 670, 1495 ), Find( _gadget.Root, 0x21 ).Rect, "the arm out 670" );

		CtrlV();
		Assert.AreEqual( new UiRect( 485, 1127, 951, 1438 ), Locator.VisitorPanel.Rect );
		Assert.AreEqual( new UiRect( 666, 1231, 769, 1334 ), Find( Locator.VisitorPanel, 0x964 ).Rect, "the visitors' button" );
		Assert.IsFalse( Locator.StaffPanel.Visible );
	}

	/// <summary>
	/// <b>A kind's button steps its members, newest first and round again</b>: the first press the first, the same
	/// button the next, past the last the first; the strip shows their name and the camera is on them. A kind with
	/// nobody hides the strip and lets the camera go.
	/// </summary>
	/// <remarks><b>Mutations:</b> the step not wrapping; the list taken oldest first; another kind's press stepping on from the last kind's member; the strip left up for a kind with nobody.</remarks>
	[TestMethod]
	public void AKindsButtonStepsItsMembers()
	{
		// Two of a kind hired after the file's, so the kind has at least two and the newest heads it.
		var ada = _people.Hire( new ParkStaffPool.Candidate( Id: 998, Kind: 1, Name: "Ada Test", Grade: 0, Costume: 0, Wage: 69 ), 47, 21 );
		var zed = _people.Hire( new ParkStaffPool.Candidate( Id: 999, Kind: 1, Name: "Zed Test", Grade: 0, Costume: 0, Wage: 69 ), 55, 30 );
		var model = _people.Staff.First( member => member.ThingId == zed ).Model;

		// The stock park has one of each kind: its guard fired, so the guards have nobody.
		Assert.IsTrue( _people.Fire( _people.Staff.Single( member => member.Model == ParkLocator.ModelOf( 3 ) ).ThingId ) );

		var byKind = Enumerable.Range( 1, 5 ).ToDictionary( role => role,
			role => _people.Staff.Where( member => member.Model == ParkLocator.ModelOf( role ) ).ToList() );
		var many = byKind.First( kind => ParkLocator.ModelOf( kind.Key ) == model );
		var none = byKind.First( kind => kind.Value.Count == 0 );
		var button = new[] { 0x95e, 0x95f, 0x962, 0x961, 0x960 }[many.Key - 1];
		var empty = new[] { 0x95e, 0x95f, 0x962, 0x961, 0x960 }[none.Key - 1];
		Assert.AreEqual( (zed, ada), (many.Value[0].ThingId, many.Value[1].ThingId), "the newest first" );

		CtrlS();
		Assert.AreEqual( (ParkLocator.Kind.Staff, 0, 0, false), (Locator.Open, Locator.Role, Locator.Actor, Locator.Strip.Visible) );

		// The handymen's one first, so the mechanics' button is pressed with another kind's member found.
		Click( 0x95e );
		Assert.AreEqual( byKind[1][0].ThingId, Locator.Actor );

		ParkOrbitCameraMode.Scroll = new Vector2( 60f, -40f );
		Click( button );
		var first = many.Value[0];
		Assert.AreEqual( (first.ThingId, true, true), (Locator.Actor, Locator.Strip.Visible, Locator.Following) );
		Assert.AreEqual( Vector2.Zero, ParkOrbitCameraMode.Scroll, "the follow's write noughts the pan's momentum (FUN_0042aab0)" );
		Assert.AreEqual( first.Name, Find( Locator.Strip, 0x147 ).Text );
		Assert.AreEqual( ParkLocator.PointOf( first.Navigator.Previous, first.Navigator.Position, ParkPeople.ThingTickFraction ),
			ParkOrbitCameraMode.PointOfInterest );

		for ( var i = 1; i <= many.Value.Count; ++i )
		{
			Click( button );
			Assert.AreEqual( many.Value[i % many.Value.Count].ThingId, Locator.Actor, $"press {i + 1}" );
		}

		var at = ParkOrbitCameraMode.PointOfInterest;
		Click( empty );
		Assert.AreEqual( (0, false, false), (Locator.Actor, Locator.Strip.Visible, Locator.Following), "a kind with nobody" );

		Click( button );
		Assert.AreEqual( first.ThingId, Locator.Actor, "another kind's press starts from its first" );
		Assert.AreNotEqual( at, new Vector3( 300f, 300f, 0f ) );
	}

	/// <summary>
	/// <b>Each frame the camera is put on whoever it follows</b>, where they are drawn: moved by hand, the next frame
	/// has the camera there, and the arm folding away (Escape) lets it go and closes the locator.
	/// </summary>
	/// <remarks><b>Mutations:</b> the follow not run each frame; Escape leaving the follow on; the fold not closing the locator.</remarks>
	[TestMethod]
	public void TheCameraFollowsUntilTheArmFolds()
	{
		CtrlS();
		var role = Enumerable.Range( 1, 5 ).First( role => _people.Staff.Any( member => member.Model == ParkLocator.ModelOf( role ) ) );
		Click( new[] { 0x95e, 0x95f, 0x962, 0x961, 0x960 }[role - 1] );
		var member = _people.Staff.First( member => member.ThingId == Locator.Actor );

		member.Navigator.Position = new FixedVector( 40 * FixedVector.One + 12345, 30 * FixedVector.One );
		member.Navigator.StampPrevious();
		_gadget.Update();
		Assert.AreEqual( new Vector3( (40 * FixedVector.One + 12345) * 10f / FixedVector.One, 300f, 0f ), ParkOrbitCameraMode.PointOfInterest );

		// Gone from the park: let go of, the strip hidden, the camera left where it was.
		var there = ParkOrbitCameraMode.PointOfInterest;
		Assert.IsTrue( _people.Fire( member.ThingId ) );
		_gadget.Update();
		Assert.AreEqual( (0, false, false, there), (Locator.Actor, Locator.Strip.Visible, Locator.Following, ParkOrbitCameraMode.PointOfInterest) );
		Assert.AreEqual( ParkLocator.Kind.Staff, Locator.Open, "and still open" );

		Assert.IsTrue( Fold() );
		Assert.AreEqual( (ParkLocator.Kind.None, 0, false, false), (Locator.Open, Locator.Actor, Locator.Following, Locator.Strip.Visible) );
		Assert.IsFalse( _gadget.ArmOut );
		Assert.IsFalse( Fold(), "nothing to fold" );

	}

	/// <summary>
	/// <b>Ctrl+S with a locator out leaves its panel out and closed</b>, as the original sets its locator open before
	/// the arm folds the panel it carries: a button then goes down and finds nobody. Ctrl+V sets its own after, and works.
	/// </summary>
	/// <remarks><b>Mutations:</b> Ctrl+S setting its locator open after the arm; Ctrl+V setting its own before.</remarks>
	[TestMethod]
	public void CtrlSOverALocatorLeavesItClosed()
	{
		var role = Enumerable.Range( 1, 5 ).First( role => _people.Staff.Any( member => member.Model == ParkLocator.ModelOf( role ) ) );
		var button = new[] { 0x95e, 0x95f, 0x962, 0x961, 0x960 }[role - 1];

		CtrlS();
		Click( button );
		Assert.AreEqual( (true, true), (Locator.Strip.Visible, ((UiButton)Find( Locator.StaffPanel, button )).IsDown) );

		CtrlS();
		Assert.AreEqual( (ParkLocator.Kind.None, true, false, false), (Locator.Open, Locator.StaffPanel.Visible, Locator.Strip.Visible, Locator.Following) );
		Assert.IsFalse( Locator.StaffPanel.Children[0].Children.OfType<UiButton>().Any( option => option.IsDown ), "every button up" );

		Click( button );
		Assert.AreEqual( (0, false, true), (Locator.Actor, Locator.Strip.Visible, ((UiButton)Find( Locator.StaffPanel, button )).IsDown), "down, and nobody" );

		CtrlV();
		CtrlV();
		Assert.AreEqual( ParkLocator.Kind.Visitors, Locator.Open );

		CtrlS();
		Assert.AreEqual( ParkLocator.Kind.None, Locator.Open, "over the visitors' too" );
	}

	/// <summary>
	/// <b>The visitors' button steps every guest, newest first</b>, the strip lettering "Visitor #" and their number,
	/// or "New Visitor" for one not yet counted, and the thought shown in its corner.
	/// </summary>
	/// <remarks><b>Mutations:</b> the guests oldest first; the number's row or parameter wrong; the status shown for a guest.</remarks>
	[TestMethod]
	public void TheVisitorsButtonStepsTheGuests()
	{
		Assert.IsTrue( _people.Peeps.Count >= 2 );

		CtrlV();
		Assert.AreEqual( (ParkLocator.Kind.Visitors, ParkLocator.VisitorsRole, 0), (Locator.Open, Locator.Role, Locator.Actor) );

		// The game's stock park had its newest guest Visitor #13; a guest not yet counted is the other row.
		_people.Peeps[0].VisitorNumber = 13;
		_people.Peeps[1].VisitorNumber = 0;

		Click( 0x964 );
		var first = _people.Peeps[0];
		Assert.AreEqual( (first.ThingId, true), (Locator.Actor, Locator.Strip.Visible) );
		Assert.AreEqual( "Visitor #13", Find( Locator.Strip, 0x147 ).Text );
		Assert.AreEqual( (false, true), (Find( Locator.Strip, 0x148 ).Visible, Find( Locator.Strip, 0x14a ).Visible) );

		Click( 0x964 );
		Assert.AreEqual( (_people.Peeps[1].ThingId, "New Visitor"), (Locator.Actor, Find( Locator.Strip, 0x147 ).Text) );
	}

	/// <summary>
	/// <b>The advisor tells of a locator as it opens and is taken back as the arm folds it</b>: Ctrl+S posts message
	/// <c>0xc6</c> after the arm takes its panel (<c>FUN_004b3f60</c>), Ctrl+V <c>0x11f</c> last (<c>FUN_004b4280</c>), each
	/// on the park's tick, and the fold (<c>FUN_004b3790</c>'s <c>0x1001d8</c>) withdraws both, which empties their slots
	/// and, while he says one, cuts him off on the game's clock. Ctrl+S over its own locator withdraws its line and posts
	/// it again, so one copy waits. Both are tutorial lines: with the Tutorial option off neither is taken.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> either post missing or the other's id; a post before the arm folds; no withdrawal at the fold,
	/// or one id alone withdrawn; the wall clock for the game's; tick nought for the park's; either row's response or
	/// sample, its category not the tutorial's, or two lines; no park.
	/// </remarks>
	[TestMethod]
	public void TheAdvisorTellsOfALocatorAsItOpensAndIsTakenBackAsItFolds()
	{
		using var clock = new SimulationClockScope();
		var tutorial = true;
		var lines = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ), tutorial: () => tutorial );
		var park = Level.Current!.ParkState!;
		park.AdvisorMessages = lines;
		var said = new List<(int Response, int Sample)>();
		int Play( int response, int sample ) { said.Add( (response, sample) ); return 9000; }

		for ( var i = 0; i < 40; ++i )
			park.AdvanceGameTick();
		var tick = park.GameTick;
		Assert.AreNotEqual( 0, tick );

		// The wall clock far past any line's end: a cut is judged on the game's clock, which stands at nought.
		Time.PinWall( 60000 );

		try
		{
			// The Tutorial option off: neither line is taken.
			tutorial = false;
			CtrlS();
			CtrlV();
			StringAssert.Contains( lines.Census(), "pending=0 ", "the Tutorial option off" );
			Assert.IsTrue( Fold() );
			tutorial = true;

			CtrlS();
			StringAssert.Contains( lines.Census(), "pending=1 attempts=0 threshold=25 slot=198/subject=0 " );
			StringAssert.Contains( lines.Census(), "message=185/score=110/", "posted on the park's own tick" );

			CtrlS();
			StringAssert.Contains( lines.Census(), "pending=1 attempts=0 threshold=25 slot=198/subject=0 ", "withdrawn as the arm folds, then posted again" );

			// The staff's panel folded for the visitors': the staff's line taken back, the visitors' posted.
			CtrlV();
			StringAssert.Contains( lines.Census(), "pending=1 attempts=0 threshold=25 slot=287/subject=0 " );

			lines.Tick( park.GameTick, 0, Play );
			CollectionAssert.AreEqual( new[] { (547, 564) }, said, "response 547, sample 564" );
			StringAssert.Contains( lines.Census(), $"message=287/score=1000/line=0/lastTick={tick}/said=1/slaps=0" );

			// Folded while he says it: cut off.
			Assert.IsTrue( Fold() );
			StringAssert.Contains( lines.Census(), $"message=287/score=1000/line=0/lastTick={tick}/said=0/slaps=1", "cut off" );

			CtrlS();
			lines.Tick( park.GameTick, 0, Play );
			Assert.AreEqual( (417, 389), said[1], "response 417, sample 389" );
			Assert.IsTrue( Fold() );
			StringAssert.Contains( lines.Census(), $"message=198/score=1000/line=0/lastTick={tick}/said=0/slaps=1", "cut off" );
			StringAssert.Contains( lines.Census(), "pending=0 " );

			// One line, so said again it is the same.
			CtrlS();
			lines.Tick( park.GameTick, 0, Play );
			Assert.AreEqual( (417, 389), said[2], "its one line again" );
		}
		finally
		{
			Time.PinWall( null );
		}
	}

	/// <summary>
	/// <b>The camera's point is the person's blend, truncated, ten to a cell</b> (<c>FUN_004f9f00</c>), and the status
	/// picture is the state group's (<c>FUN_00485ec0</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the blend rounded, not truncated; the fraction ignored; a picture off by one.</remarks>
	[TestMethod]
	public void ThePointAndThePictureAreTheOriginals()
	{
		var previous = new FixedVector( 10 * FixedVector.One, 20 * FixedVector.One );
		var position = new FixedVector( 10 * FixedVector.One + 3, 20 * FixedVector.One - 3 );

		Assert.AreEqual( new Vector3( (10 * FixedVector.One + 1) * 10f / FixedVector.One, (20 * FixedVector.One - 2) * 10f / FixedVector.One, 0f ),
			ParkLocator.PointOf( previous, position, 0.5f ), "plus 1.5 and less 1.5, each sum truncated" );
		Assert.AreEqual( new Vector3( (10 * FixedVector.One + 3) * 10f / FixedVector.One, (20 * FixedVector.One - 3) * 10f / FixedVector.One, 0f ),
			ParkLocator.PointOf( previous, position, 1f ) );

		Assert.AreEqual( "0 0 1 2 3 9", string.Join( " ", Enumerable.Range( 0, 6 ).Select( ParkLocator.StatusPart ) ) );
	}

	/// <summary>
	/// <b>A patrol area's edges are <c>FUN_004b3640</c>'s four lines</b>: the top row less its corners, the right column
	/// down, the bottom row, the left column up; a row two cells wide runs back from its right corner, so both corners
	/// are laid twice; a cell off the map is passed over (<c>FUN_00536100</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the rows' corners kept; a narrow row's corners not taken; the left column walked down; the
	/// line along the shorter way; an off-map cell laid.</remarks>
	[TestMethod]
	public void APatrolAreasEdgesAreTheOriginalsFourLines()
	{
		// The stock mechanic's (39,21)-(56,28): every outline cell once, 48.
		var edges = ParkLocator.PatrolEdgeCells( (39, 21), (56, 28) );
		Assert.AreEqual( 48, edges.Count );
		Assert.AreEqual( 48, edges.Distinct().Count() );
		Assert.AreEqual( "(40, 21) (55, 21) (56, 21) (56, 28) (40, 28) (55, 28) (39, 28) (39, 21)",
			string.Join( " ", new[] { 0, 15, 16, 23, 24, 39, 40, 47 }.Select( i => edges[i] ) ) );

		// The stock entertainer's (47,18)-(48,25): two wide, so the top and bottom rows each lay both corners again.
		var narrow = ParkLocator.PatrolEdgeCells( (47, 18), (48, 25) );
		Assert.AreEqual( 20, narrow.Count );
		Assert.AreEqual( 16, narrow.Distinct().Count() );
		Assert.AreEqual( "(48, 18) (47, 18) (48, 18)", string.Join( " ", narrow.Take( 3 ) ) );

		// One wide: the rows are the corner alone.
		Assert.AreEqual( "(5, 5) (5, 5) (5, 6) (5, 7) (5, 7) (5, 7) (5, 6) (5, 5)",
			string.Join( " ", ParkLocator.PatrolEdgeCells( (5, 5), (5, 7) ) ) );

		Assert.AreEqual( "(127, 0) (127, 1) (126, 1) (126, 0)", string.Join( " ", ParkLocator.PatrolEdgeCells( (126, 0), (128, 1) ) ) );
	}

	/// <summary>
	/// <b>The edges are laid each frame for a fenced member followed, and for nobody else</b>: the stock mechanic's 48,
	/// handed to the marker mesh for its frame alone; the researcher, whose area is the whole map, none; none once the
	/// follow ends or the arm folds the locator away.
	/// </summary>
	/// <remarks><b>Mutations:</b> the edges laid for the whole map; left after the frame; never handed to the mesh; kept by
	/// the mesh past its frame.</remarks>
	[TestMethod]
	public void AFencedMembersEdgesAreLaidWhileFollowed()
	{
		CtrlS();
		Click( 0x95f );
		var mechanic = _people.Staff.First( member => member.ThingId == Locator.Actor );
		Assert.AreEqual( ((39, 21), (56, 28)), (mechanic.PatrolFrom, mechanic.PatrolTo), "the stock mechanic's fence" );

		_gadget.Update();
		CollectionAssert.AreEqual( ParkLocator.PatrolEdgeCells( (39, 21), (56, 28) ), Locator.PatrolEdges );
		StringAssert.EndsWith( Locator.Census(), " patrol=48" );

		Click( 0x960 );
		Assert.IsTrue( _people.Staff.First( member => member.ThingId == Locator.Actor ).PatrolsWholeMap, "the researcher's is the map" );
		_gadget.Update();
		Assert.AreEqual( 0, Locator.PatrolEdges.Count, "the whole map" );

		Click( 0x95f );
		_gadget.Update();
		Assert.AreEqual( 48, Locator.PatrolEdges.Count );

		// The camera taken off him (the camcorder's first person) ends the follow, and with it the edges.
		typeof( ParkLocator ).GetProperty( nameof( ParkLocator.Following ), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic )!.SetValue( Locator, false );
		_gadget.Update();
		Assert.AreEqual( 0, Locator.PatrolEdges.Count, "not followed" );

		// Each frame's edges go to the marker mesh, which takes them for its frame and keeps none for the next.
		var markers = (ParkBuildMarkers)RuntimeHelpers.GetUninitializedObject( typeof( ParkBuildMarkers ) );
		var current = typeof( ParkBuildMarkers ).GetProperty( nameof( ParkBuildMarkers.Current ) )!;
		current.SetValue( null, markers );

		try
		{
			Click( 0x95f );
			_gadget.Update();
			Assert.AreEqual( 48, Locator.PatrolEdges.Count );
			CollectionAssert.AreEqual( Locator.PatrolEdges, markers.PatrolQueued.ToList(), "queued for the mesh" );

			typeof( ParkBuildMarkers ).GetMethod( "OnUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic )!
				.Invoke( markers, null );
			Assert.AreEqual( 0, markers.PatrolQueued.Count, "taken by the frame" );
		}
		finally
		{
			current.SetValue( null, null );
		}

		Assert.IsTrue( Fold() );
		_gadget.Update();
		Assert.AreEqual( 0, Locator.PatrolEdges.Count, "folded away" );
	}

	private static UiControl Find( UiControl root, int id )
		=> FindOrNull( root, id ) ?? throw new AssertFailedException( $"no control {id:x}" );

	private static UiControl? FindOrNull( UiControl root, int id )
		=> root.Id == id ? root : root.Children.Select( child => FindOrNull( child, id ) ).FirstOrDefault( found => found != null );

	/// <summary>The first cell of path in the shipped park, row by row.</summary>
	private static (int X, int Y) APathCell()
	{
		for ( var y = 0; y < ParkWorld.MapSize; y++ )
		{
			for ( var x = 0; x < ParkWorld.MapSize; x++ )
			{
				if ( ParkState.CellFor( Level.Current!.Park!, x, y ).Type == CellEdge.Path )
					return (x, y);
			}
		}

		throw new AssertFailedException( "the shipped park has path" );
	}

	/// <summary>A guest in the park, admitted on the road's far side where the shipped park has none inside.</summary>
	private int AGuest() => _people.Guests.Count > 0 ? _people.Guests.Keys.First() : _people.Admit( 55, 30 );

	/// <summary>A click on a cell with one thing under the pointer: a person as the person, anything else as the object.</summary>
	private string ClickAt( int x, int y, int thing )
		=> _people.Guests.ContainsKey( thing ) || _people.IsStaff( thing )
			? Level.Current!.ClickWorldAt( x, y, thing, 0 )
			: Level.Current!.ClickWorldAt( x, y, 0, thing );

	/// <summary>
	/// <b>A guest under the pointer leaves a click on a ride to the ride</b> (<c>FUN_004879d0</c>: category 4 opens the
	/// object the hover took from the cell, <c>[0x007c2654]</c>, kept apart from the person, <c>[0x007c2650]</c>). The
	/// pointer over the Belly Bounce's anchor (51,23), a guest heading its list and latched, takes the guest and the ride
	/// as two values; nobody latched, the ride alone. With no locator and no modifier the click opens the ride's window;
	/// with Shift alone the same two open the guest's; the frame's own click opens the ride's; a member under the pointer
	/// is still their window first.
	/// </summary>
	/// <remarks><b>Mutations:</b> the latched person dropped; the cell asked with people; the object window given the
	/// person; the frame's click given the person twice.</remarks>
	[TestMethod]
	public void AGuestUnderThePointerLeavesAClickOnARideToTheRide()
	{
		var state = Level.Current!.ParkState!;
		var guest = AGuest();
		var member = _people.Staff[0].ThingId;
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( Level.Current, Level.Scene.Park );

		state.StandOn( guest, 51, 23 );
		Assert.AreEqual( guest, state.CellAt( 51, 23 ).Occupant, "the guest heads the ride's anchor" );

		var (person, placed) = ParkPicking.Take( state, guest, 51, 23 );
		Assert.AreEqual( (guest, 13), (person, placed), "the guest latched, and the ride under them" );
		Assert.AreEqual( (0, 13), ParkPicking.Take( state, 0, 51, 23 ), "nobody latched: the ride alone" );

		StringAssert.Contains( Level.Current.ClickWorldAt( 51, 23, person, placed ), "opened the window for thing 13" );
		Assert.AreEqual( 13, _stack.Windows.OfType<ParkObjectWindow>().Single().ThingId );
		Assert.IsFalse( _stack.Windows.OfType<ParkVisitorWindow>().Any() );
		_stack.Close( _stack.Windows.OfType<ParkObjectWindow>().Single() );

		Input.Keyboard = new Input.KeyboardInfo( new List<Key> { Key.ShiftLeft } );
		StringAssert.Contains( Level.Current.ClickWorldAt( 51, 23, person, placed ), $"opened the visitor window for thing {guest}" );
		Input.Keyboard = new();
		_stack.Close( _stack.Windows.OfType<ParkVisitorWindow>().Single() );

		// The frame's own click hands the two values on as the pointer left them (Level.WorldClick).
		var cell = typeof( ParkPicking ).GetProperty( nameof( ParkPicking.Cell ) )!;
		var personUnder = typeof( ParkPicking ).GetProperty( nameof( ParkPicking.PersonUnderCursor ) )!;
		var objectUnder = typeof( ParkPicking ).GetProperty( nameof( ParkPicking.ObjectUnderCursor ) )!;
		var worldClick = typeof( Level ).GetMethod( "WorldClick", BindingFlags.Instance | BindingFlags.NonPublic )!;

		try
		{
			// What the interface's update leaves for the park's click, which an earlier test's press can leave set.
			typeof( WindowStack ).GetProperty( nameof( WindowStack.PointerTaken ), BindingFlags.Static | BindingFlags.NonPublic )!.SetValue( null, false );
			typeof( WindowStack ).GetProperty( nameof( WindowStack.ParkScreenOpen ), BindingFlags.Static | BindingFlags.NonPublic )!.SetValue( null, false );
			cell.SetValue( null, MapStep.CellId( 51, 23 ) );
			personUnder.SetValue( null, person );
			objectUnder.SetValue( null, placed );

			foreach ( var down in new[] { true, false } )
			{
				Input.Mouse = new() { Left = down, LeftWentDown = down, Position = new Vector2( 1024, 600 ) };
				worldClick.Invoke( Level.Current, [] );
			}

			Assert.AreEqual( 13, _stack.Windows.OfType<ParkObjectWindow>().Single().ThingId, "the frame's click: the ride's window" );
			_stack.Close( _stack.Windows.OfType<ParkObjectWindow>().Single() );
		}
		finally
		{
			cell.SetValue( null, 0 );
			personUnder.SetValue( null, 0 );
			objectUnder.SetValue( null, 0 );
			Input.Mouse = new();
		}

		StringAssert.Contains( Level.Current.ClickWorldAt( 51, 23, member, placed ), $"opened the staff window for thing {member}" );
		Assert.IsFalse( _stack.Windows.OfType<ParkObjectWindow>().Any() );
	}

	/// <summary>
	/// <b>A Ctrl+click on a placed thing puts another of it in the hand, turned as it is, and opens no window</b>
	/// (<c>0x00487ac0</c>..<c>0x00487bf4</c>, <c>docs/exe/park-engine.md</c>, "Ctrl+click buys another of a placed
	/// thing"): the Belly Bounce (thing 13, item 1100, 500) with Ctrl alone, with Ctrl and Shift (the original asks
	/// <c>VK_CONTROL</c> alone), and with the bank at exactly its price (<c>jle</c> at <c>0x00487b44</c>); the first stock
	/// thing standing turned keeps its angle in the hand; and one whose item no buy tab lists is held all the same, the clone
	/// asking the bank alone, as does a placed mystery ride with no golden tickets (its cash price against the bank, held at
	/// exactly the price and refused one short). Nothing is charged and a held clone plays no sound. With the bank one short
	/// it plays <c>cat_ui</c> <c>0x1D</c>, opens nothing and leaves the hand empty; with no Ctrl the window opens as before.
	/// </summary>
	/// <remarks><b>Mutations:</b> the arm removed; Ctrl alone asked for; the angle passed as nought; the bank test made
	/// strict; the bank test removed; <c>Carry</c>'s buy-tab test reused; <c>Carry</c>'s ticket test reused; the refusal's
	/// sound changed or played for a clone held.</remarks>
	[TestMethod]
	public void ACtrlClickOnAPlacedThingPutsAnotherOfItInTheHand()
	{
		typeof( Level ).GetProperty( nameof( Level.Kind ) )!.SetValue( Level.Current, Level.Scene.Park );
		var state = Level.Current!.ParkState!;
		var catalogue = Level.Current.Catalogue!;
		var window = () => _stack.Windows.OfType<ParkObjectWindow>().SingleOrDefault();
		var ctrl = () => Input.Keyboard = new Input.KeyboardInfo( new List<Key> { Key.ControlLeft } );

		try
		{
			state.SetBalance( 88000 );
			ctrl();
			UiSounds.LastAsked = null;
			StringAssert.Contains( Level.Current.ClickWorldAt( 51, 23, 0, 13 ), "carry another: holding 'Belly Bounce' (1100) at 500, turned 0" );
			Assert.AreEqual( 1100, ParkBuilding.Carrying );
			Assert.IsNull( UiSounds.LastAsked, "a clone held plays no sound" );
			Assert.IsNull( window(), "Ctrl: no window" );
			Assert.AreEqual( 88000, state.Balance, "nothing charged until the put-down" );
			ParkHand.LetGo();

			Input.Keyboard = new Input.KeyboardInfo( new List<Key> { Key.ControlRight, Key.ShiftLeft } );
			Level.Current.ClickWorldAt( 51, 23, 0, 13 );
			Assert.AreEqual( 1100, ParkBuilding.Carrying, "Ctrl with Shift is still Ctrl" );
			ParkHand.LetGo();

			var turned = state.Objects.First( placed => placed.Angle != 0 && catalogue.TryGet( placed.CatalogueId, out _ ) );
			ctrl();
			Level.Current.ClickWorldAt( 51, 23, 0, turned.ThingId );
			Assert.AreEqual( (turned.CatalogueId, turned.Angle), (ParkBuilding.Carrying, ParkBuilding.CarryingAngle), $"thing {turned.ThingId} turned" );
			ParkHand.LetGo();

			var unlisted = state.Objects.First( placed => catalogue.TryGet( placed.CatalogueId, out var item )
				&& item.UiType is < 0 or > ItemDescriptionFile.Feature && item.BuildPrice <= state.Balance );
			Level.Current.ClickWorldAt( 51, 23, 0, unlisted.ThingId );
			Assert.AreEqual( unlisted.CatalogueId, ParkBuilding.Carrying, $"thing {unlisted.ThingId}, listed on no buy tab, is held" );
			ParkHand.LetGo();

			// A mystery ride stands in the park: its clone asks its cash price alone, never the tickets (none are held here).
			var mystery = catalogue.All.First( item => item.GoldenTicketCost > 0 && item.BuildPrice > 0 );
			var mysteryId = state.NextThingId();
			state.AddObject( new ParkWorld.CatalogueObject( mysteryId, mystery.Id, 73 << 8, 66 << 8, 0 ) );
			Assert.AreEqual( 0, GoldenTickets.TicketsLeft, "no golden tickets" );

			state.SetBalance( mystery.BuildPrice );
			UiSounds.LastAsked = null;
			Level.Current.ClickWorldAt( 51, 23, 0, mysteryId );
			Assert.AreEqual( mystery.Id, ParkBuilding.Carrying, "a mystery ride is held for its cash price, with no tickets" );
			Assert.IsNull( UiSounds.LastAsked );
			ParkHand.LetGo();

			state.SetBalance( mystery.BuildPrice - 1 );
			StringAssert.Contains( Level.Current.ClickWorldAt( 51, 23, 0, mysteryId ), "for the refusal" );
			Assert.AreEqual( 0, ParkBuilding.Carrying, "a mystery ride with the bank one short is refused" );
			Assert.AreEqual( 0x1d, UiSounds.LastAsked );

			state.SetBalance( 500 );
			Level.Current.ClickWorldAt( 51, 23, 0, 13 );
			Assert.AreEqual( 1100, ParkBuilding.Carrying, "a bank of exactly the price takes it" );
			ParkHand.LetGo();

			state.SetBalance( 499 );
			UiSounds.LastAsked = null;
			StringAssert.Contains( Level.Current.ClickWorldAt( 51, 23, 0, 13 ), "costs 500 and the park has 499 - cat_ui 0x1d for the refusal" );
			Assert.AreEqual( 0x1d, UiSounds.LastAsked, "the buy screen's refusal" );
			Assert.AreEqual( 0, ParkBuilding.Carrying, "refused: the hand stays empty" );
			Assert.IsNull( window(), "refused: no window either" );

			Input.Keyboard = new();
			StringAssert.Contains( Level.Current.ClickWorldAt( 51, 23, 0, 13 ), "opened the window for thing 13" );
			Assert.AreEqual( 0, ParkBuilding.Carrying, "no Ctrl: the window, not the hand" );
			_stack.Close( window()! );
		}
		finally
		{
			Input.Keyboard = new();
			ParkHand.LetGo();
		}
	}

	/// <summary>
	/// <b>With the staff's locator open a click on the park arms no tool and opens no window</b> (<c>0x0048721c</c>): bare
	/// ground (43,16), path (49,23), the Belly Bounce's anchor (51,23) and a guest all do nothing; a member under the pointer
	/// still has their window. With it folded away the same ground arms the path tool, which (43,16), outside the park, refuses at once.
	/// </summary>
	/// <remarks><b>Mutations:</b> the locator not asked; the visitors' alone stopping the ground; the member's window refused.</remarks>
	[TestMethod]
	public void WithTheStaffLocatorOpenAClickOnTheParkDoesNothingButAMembersWindow()
	{
		var (pathX, pathY) = APathCell();
		var guest = AGuest();

		CtrlS();
		Assert.AreEqual( ParkLocator.Kind.Staff, Locator.Open );

		foreach ( var (x, y, thing) in new[] { (43, 16, 0), (pathX, pathY, 0), (51, 23, 13), (49, 23, guest) } )
		{
			StringAssert.Contains( ClickAt( x, y, thing ), "nothing to do", $"({x},{y}) thing {thing}" );
			Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current, $"({x},{y}) armed nothing" );
		}

		Assert.IsFalse( _stack.Windows.Any( window => window is ParkObjectWindow or ParkVisitorWindow ) );

		var member = _people.Staff[0];
		StringAssert.Contains( ClickAt( 49, 23, member.ThingId ), $"opened the staff window for thing {member.ThingId}" );
		Assert.IsTrue( _stack.Windows.OfType<ParkStaffWindow>().Any() );

		_stack.Close( _stack.Windows.OfType<ParkStaffWindow>().Single() );
		Fold();
		Assert.AreEqual( ParkLocator.Kind.None, Locator.Open );
		StringAssert.StartsWith( ClickAt( 43, 16, 0 ), "world click: path", "with no locator the ground arms the path tool" );
	}

	/// <summary>
	/// <b>With the visitors' locator open and no modifier a guest clicked opens their window and nothing else does</b>
	/// (<c>0x00487009</c> to <c>0x0048702d</c>, category 7 to <c>FUN_004b79b0</c>), not even a member; with Control held the member's window
	/// opens again and the ground still does nothing.
	/// </summary>
	/// <remarks><b>Mutations:</b> the guest's window not opened; a member's opened with no modifier; Control taken as none.</remarks>
	[TestMethod]
	public void WithTheVisitorsLocatorOpenAGuestClickedOpensTheirWindowAndAMemberNothing()
	{
		var guest = AGuest();
		var member = _people.Staff[0];

		CtrlV();
		Assert.AreEqual( ParkLocator.Kind.Visitors, Locator.Open );

		StringAssert.Contains( ClickAt( 49, 23, member.ThingId ), "nothing to do" );
		StringAssert.Contains( ClickAt( 43, 16, 0 ), "nothing to do" );
		Assert.IsFalse( _stack.Windows.Any( window => window is ParkStaffWindow or ParkVisitorWindow ) );

		StringAssert.Contains( ClickAt( 49, 23, guest ), $"opened the visitor window for thing {guest}" );
		Assert.AreEqual( guest, _stack.Windows.OfType<ParkVisitorWindow>().Single().ThingId );
		_stack.Close( _stack.Windows.OfType<ParkVisitorWindow>().Single() );

		CtrlV();
		Input.Keyboard = new Input.KeyboardInfo( new List<Key> { Key.ControlLeft } );
		StringAssert.Contains( ClickAt( 43, 16, 0 ), "nothing to do" );
		Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current );
		StringAssert.Contains( ClickAt( 49, 23, member.ThingId ), "opened the staff window" );
	}

	/// <summary>
	/// <b>Shift alone with no locator open is the guest pick</b> (<c>0x00487009</c> to <c>0x0048702d</c>): a guest's window, and nothing for the
	/// ground, the ride or a member; let go, the ground arms the path tool.
	/// </summary>
	/// <remarks><b>Mutations:</b> Shift not asked; Shift with a locator taken as the pick; the member's window under Shift.</remarks>
	[TestMethod]
	public void ShiftAloneWithNoLocatorPicksAGuestAndNothingElse()
	{
		var guest = AGuest();
		var member = _people.Staff[0];

		Input.Keyboard = new Input.KeyboardInfo( new List<Key> { Key.ShiftLeft } );

		foreach ( var (x, y, thing) in new[] { (43, 16, 0), (51, 23, 13), (49, 23, member.ThingId) } )
			StringAssert.Contains( ClickAt( x, y, thing ), "nothing to do", $"({x},{y}) thing {thing}" );

		Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current );
		StringAssert.Contains( ClickAt( 49, 23, guest ), $"opened the visitor window for thing {guest}" );

		Input.Keyboard = new();
		_stack.Close( _stack.Windows.OfType<ParkVisitorWindow>().Single() );
		StringAssert.StartsWith( ClickAt( 43, 16, 0 ), "world click: path" );
	}

	/// <summary>
	/// <b>The hover shows the path's row where the click would arm the path tool, and a person's where it would take
	/// them</b>: 441 over bare ground and 442 over path with no locator open; a member 555 with their name and a guest 553
	/// with their number (554 at nought), each with the pick pointer; with the staff's locator open and nobody taken the
	/// row last posted stands, and where the guests decide and nobody is taken the row is emptied (<c>FUN_00486d90</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the rows swapped, the values dropped, the row not kept under a locator, the row kept where
	/// the guests decide, the pointer not put up.</remarks>
	[TestMethod]
	public void TheHoverShowsThePathsRowOrThePersonsWhereTheClickWouldTakeThem()
	{
		_stack.Update();
		var (pathX, pathY) = APathCell();
		var guest = AGuest();
		var number = 13;
		var member = _people.Staff[0];
		var level = Level.Current!;
		int Row( int x, int y, int thing ) => level.WorldHelpRow( x, y, thing );

		Assert.AreEqual( (441, 442, 441), (Row( 43, 16, 0 ), Row( pathX, pathY, 0 ), Row( 43, 16, guest )) );
		Assert.IsFalse( level.PickPointer );

		Assert.AreEqual( 555, Row( 43, 16, member.ThingId ), "a member with no locator open" );
		Assert.AreEqual( $"{member.Name} : Left-click to view this staff member's details",
			Localization.FormatHelp( 555, level.WorldHelpValues ) );
		Assert.IsTrue( level.PickPointer );
		Assert.AreEqual( 441, Row( 43, 16, 0 ) );

		CtrlS();
		Assert.AreEqual( (441, 441), (Row( pathX, pathY, 0 ), Row( 43, 16, guest )), "the ground's row stands under the locator" );
		Assert.IsFalse( level.PickPointer );
		Assert.AreEqual( 555, Row( 43, 16, member.ThingId ) );
		Assert.AreEqual( 555, Row( 43, 16, member.ThingId ) );
		Assert.AreEqual( (555, 555), (Row( 43, 16, 0 ), Row( 43, 16, guest )), "the member's row stands" );
		Assert.AreEqual( member.Name, level.WorldHelpValues.Single().Value );
		Assert.IsFalse( level.PickPointer );

		Fold();
		CtrlV();
		var numbered = _people.Guests[guest].VisitorNumber;
		_people.Guests[guest].VisitorNumber = 0;
		Assert.AreEqual( 554, Row( 43, 16, guest ), "New Visitor at nought" );
		Assert.AreEqual( 0, level.WorldHelpValues.Length );
		_people.Guests[guest].VisitorNumber = number;
		Assert.AreEqual( 554, Row( 43, 16, guest ), "the same person and category: nothing posted again" );
		Assert.IsNull( level.HoverPost( 554 ), "and the frame hands the bar nothing" );
		Row( 43, 16, 0 );
		Assert.AreEqual( 553, Row( 43, 16, guest ) );
		Assert.AreEqual( $"Visitor #{number} : Left-click to view this person's details", Localization.FormatHelp( 553, level.WorldHelpValues ) );
		Assert.IsTrue( level.PickPointer );

		// The help bar fills the row's parameters with the hover's values.
		var popupHelp = GameOptions.Current.PopupHelp;
		GameOptions.Current.PopupHelp = true;
		UI.WindowStack.WorldHelpPost = level.HoverPost( 553 );
		Assert.AreEqual( 553, UI.WindowStack.WorldHelpPost!.Value.Row );
		Assert.AreEqual( number.ToString(), UI.WindowStack.WorldHelpPost!.Value.Values.Single().Value, "with the person's number" );
		Input.Mouse = new();
		_stack.Update();
		Assert.AreEqual( $"Visitor #{number} : Left-click to view this person's details", _stack.HelpBarText );
		GameOptions.Current.PopupHelp = popupHelp;
		_people.Guests[guest].VisitorNumber = numbered;
		_stack.Update();
		Assert.AreEqual( $"Visitor #{number} : Left-click to view this person's details", _stack.HelpBarText, "a posted row stands until another posts" );
		UI.WindowStack.WorldHelpPost = (-1, []);
		_stack.Update();
		Assert.AreEqual( "", _stack.HelpBarText );
		Assert.AreEqual( (-1, -1), (Row( 43, 16, member.ThingId ), Row( 43, 16, 0 )), "the guests decide: the row is emptied" );
		Assert.IsFalse( level.PickPointer );
		Assert.AreEqual( 0, level.WorldHelpValues.Length );
	}

	/// <summary>
	/// <b>Where the guests decide and nobody is taken, the empty row is posted only after a category</b>
	/// (<c>0x004870f3</c>): Shift over bare ground empties the ground's row; with the visitors' locator open, Ctrl held
	/// takes the category to nought with the ground's row standing, and let go the row still stands; off a guest it is
	/// emptied; and coming back onto the view from a control the first hovers are at the last place on the view
	/// (<c>q473/orig</c>, round c), so a guest left there posts their row, and a guest come onto from where nobody was
	/// waits a hover.
	/// </summary>
	/// <remarks><b>Mutations:</b> always emptied; never emptied; the last category not kept; Ctrl's hover not nought; the
	/// entry not nought; the return at the pointer's own place.</remarks>
	[TestMethod]
	public void TheGuestsEmptyRowIsPostedOnlyAfterACategory()
	{
		_stack.Update();
		var guest = AGuest();
		var level = Level.Current!;
		int Row( int thing, bool onTheView = true ) => level.WorldHelpRow( 43, 16, thing, onTheView );
		void Hold( params Key[] keys ) => Input.Keyboard = new Input.KeyboardInfo( keys.ToList() );

		Assert.AreEqual( (441, 441), (Row( 0 ), Row( 0 )), "onto the view, then the ground's category" );
		Hold( Key.ShiftLeft );
		Assert.AreEqual( (-1, -1), (Row( 0 ), Row( 0 )), "Shift alone: emptied after the ground's category" );

		Hold();
		Assert.AreEqual( 441, Row( 0 ) );
		CtrlV();
		Hold( Key.ControlLeft );
		Assert.AreEqual( 441, Row( 0 ), "Ctrl held: the locator's, nought, the row standing" );
		Hold();
		Assert.AreEqual( (441, 441), (Row( 0 ), Row( 0 )), "let go: nought before, so the row stands" );

		var numbered = _people.Guests[guest].VisitorNumber;
		_people.Guests[guest].VisitorNumber = 13;
		Assert.AreEqual( 553, Row( guest ) );
		Assert.AreEqual( (-1, -1), (Row( 0 ), Row( 0 )), "off the guest: emptied after their category" );

		Row( guest );
		Row( guest, onTheView: false );
		Assert.AreEqual( 553, Row( 0 ), "back onto the view: the hovers at the last place, the guest" );
		Assert.AreEqual( -1, Row( 0 ), "then the pointer's own place: emptied after the guest's category" );
		Row( 0, onTheView: false );
		Assert.AreEqual( -1, Row( guest ), "back onto a guest from where nobody was: the last place's, nothing, the bar left empty" );
		Assert.AreEqual( 553, Row( guest ), "then the guest's own" );
		_people.Guests[guest].VisitorNumber = numbered;
	}

	/// <summary>
	/// <b>The hover's arms that post nothing leave the row standing, and the switch's default empties it only after a
	/// category</b> (<c>FUN_00486d90</c>): from bare ground onto a hoarding 441 stands (<c>0x004873bd</c>), and on from
	/// there onto a cell of map type 7 beside the gate and one of type 2, the default (<c>0x004877a3</c>), it still
	/// stands; from bare ground straight onto either it is emptied; off the map it stands (<c>0x00487240</c>), counted; an object's
	/// cell whose parent holds no object stands (<c>0x00487667</c>), and one whose parent holds one posts its row, 550; a type-12
	/// track cell under another parent is counted. With a tool armed the empty row is posted only after a category
	/// (<c>0x00486fd9</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the hoarding not silent; the default always emptied; the default never emptied; no cell
	/// not silent; the edge cell's count dropped; the object's cell silent with its object; not silent without; the tool's arm always emptied; never
	/// emptied; the category not nought after the tool's arm; the track's count dropped.</remarks>
	[TestMethod]
	public void TheHoversSilentArmsLeaveTheRowStanding()
	{
		_stack.Update();
		var level = Level.Current!;
		var state = level.ParkState!;
		int Row( (int X, int Y) cell ) => level.WorldHelpRow( cell.X, cell.Y, 0 );
		var (ground, hoarding, outside) = ((43, 16), (42, 16), (44, 16));
		var two = Enumerable.Range( 0, 128 * 128 ).Select( i => (X: i % 128, Y: i / 128) ).First( c => state.Record( c.X, c.Y ).Type == 2 );
		var footprint = Enumerable.Range( 0, 128 * 128 ).Select( i => (X: i % 128, Y: i / 128) )
			.First( c => state.Record( c.X, c.Y ).Type == CellEdge.Footprint && ParkItemHeights.Owner( state, c.X, c.Y ) != null );
		Assert.AreEqual( (0, 12, 7), (state.Record( 42, 16 ).Type, state.Record( 42, 16 ).TrackType, state.Record( 44, 16 ).Type) );

		Assert.AreEqual( (441, 441), (Row( ground ), Row( ground )), "onto the view, then the ground's category" );
		Assert.AreEqual( 441, Row( hoarding ), "onto a hoarding: nothing posted" );
		Assert.AreEqual( (441, 441), (Row( outside ), Row( two )), "on to the default from nought: the row stands" );
		Assert.AreEqual( 441, Row( ground ) );
		Assert.AreEqual( (-1, -1), (Row( outside ), Row( hoarding )), "straight onto the default: emptied after the ground's" );
		Row( ground );
		Assert.AreEqual( -1, Row( two ), "map type 2 the same" );
		Row( ground );
		Assert.AreEqual( (441, 441), (Row( (-1, -1) ), Row( outside )), "no cell, before the first meeting: nothing posted, and nought after it" );

		Row( ground );
		Assert.AreEqual( (550, -1), (Row( footprint ), Row( outside )), "an object's cell: its category and row, then emptied" );
		var orphan = (X: 60, Y: 30);
		var was = state.Record( orphan.X, orphan.Y );
		Assert.AreEqual( 441, Row( ground ) );
		state.SetRecord( orphan.X, orphan.Y, was with { Type = CellEdge.Footprint, ParentId = (ushort)((orphan.Y * 128) + orphan.X + 1) } );
		Assert.AreEqual( 441, Row( orphan ), "an object's cell whose parent holds no object: nothing posted" );
		state.SetRecord( orphan.X, orphan.Y, was with { TrackType = 12, TrackParentId = (ushort)((orphan.Y * 128) + orphan.X + 2) } );
		Row( orphan );
		Assert.IsTrue( Unimplemented.Summary.Any( gap => gap.What == "HOVER_TRACK_CELL_CATEGORY" ), "a type-12 cell under another parent" );
		state.SetRecord( orphan.X, orphan.Y, was );

		Row( ground );
		Row( hoarding );
		ParkBuildMode.Arm( ParkBuildMode.Path );
		Assert.AreEqual( (441, 441), (Row( ground ), Row( ground )), "a tool armed after nought: the row stands" );
		ParkBuildMode.Disarm();
		Assert.AreEqual( 441, Row( ground ) );
		ParkBuildMode.Arm( ParkBuildMode.Path );
		Assert.AreEqual( (-1, -1), (Row( ground ), Row( hoarding )), "after the ground's category: emptied, then nought" );
		ParkBuildMode.Disarm();
		Assert.AreEqual( (441, 441), (Row( ground ), Row( hoarding )) );
	}

	/// <summary>
	/// <b>The park gate and the ticket booths come before the cell's own categories</b> (<c>FUN_00486d90</c>,
	/// <c>0x004872a4</c> to <c>0x0048730d</c>): over the gate, (46,17) to (49,17), the hover posts 459 with the pick hand,
	/// over a booth 460, though the gate's middle cells are path and its sides map type 7; a click on either opens the entry
	/// price (<c>FUN_00498d80</c>) and arms no tool. Past the gate's sides the cells are the ground's again. Type
	/// <c>0x15</c>'s row is counted.
	/// </summary>
	/// <remarks><b>Mutations:</b> the gate's side cells dropped; a cell further out taken; the booths' row the gate's; the
	/// gate test after the cell's own; no pick hand; the click arms the path tool; each count dropped.</remarks>
	[TestMethod]
	public void TheGateAndTheBoothsPostTheirRowsAndOpenTheEntryPrice()
	{
		_stack.Update();
		var level = Level.Current!;
		var state = level.ParkState!;
		int Row( int x, int y ) => level.WorldHelpRow( x, y, 0 );
		Assert.AreEqual( (1, 1, 7, 7), (state.Record( 47, 17 ).Type, state.Record( 48, 17 ).Type, state.Record( 46, 17 ).Type, state.Record( 49, 17 ).Type) );

		Row( 43, 16 );
		Assert.AreEqual( (459, 459, 459, 459), (Row( 46, 17 ), Row( 47, 17 ), Row( 48, 17 ), Row( 49, 17 )), "the gate's four cells" );
		Assert.IsTrue( level.PickPointer, "the pick hand over the gate" );
		Assert.AreEqual( (460, 460), (Row( 47, 13 ), Row( 48, 13 )), "the booths" );
		Assert.IsTrue( level.PickPointer, "the pick hand over a booth" );
		Assert.AreEqual( -1, Row( 45, 17 ), "past the gate's side, map type 7: emptied after the booth's category" );
		Row( 47, 17 );
		Assert.AreEqual( -1, Row( 50, 17 ), "past the other side" );
		Assert.IsFalse( level.PickPointer );

		ParkCategoryScreens.Remember( ParkCategoryScreens.Finance, 9 );
		StringAssert.Contains( level.ClickWorldAt( 47, 17, 0 ), "the gate at (47,17) opens the entry price" );
		Assert.AreEqual( ParkBuildMode.None, ParkBuildMode.Current, "no path tool" );
		Assert.AreEqual( 1, _stack.Windows.Count( window => window is ParkEntryPriceScreen ), "the entry price opened" );
		StringAssert.Contains( level.ClickWorldAt( 48, 13, 0 ), "the ticket booth at (48,13) opens the entry price" );
		Assert.AreEqual( 1, _stack.Windows.Count( window => window is ParkEntryPriceScreen ), "not a second" );
		Assert.AreEqual( 10, ParkCategoryScreens.LeftOn( ParkCategoryScreens.Finance ), "noted as the finance category's screen" );

		var cell = (X: 60, Y: 30);
		var was = state.Record( cell.X, cell.Y );
		Unimplemented.Forget();
		Row( 55, 15 );
		state.SetRecord( cell.X, cell.Y, was with { Type = 3 } );
		Row( cell.X, cell.Y );
		state.SetRecord( cell.X, cell.Y, was with { Type = 0x15 } );
		Row( cell.X, cell.Y );
		state.SetRecord( cell.X, cell.Y, was );
		Assert.AreEqual( "HOVER_TYPE_15_ROW", string.Join( ",", Unimplemented.Summary.Select( gap => gap.What ) ),
			"an object's and a queue's rows are built; type 0x15's is counted" );
	}

	/// <summary>
	/// <b>Only the hover counts a row it has not built</b>: the click asks the same arm of the ground (to see whether the
	/// path tool is armed) and posts no row, so a click on a type-<c>0x15</c> cell adds nothing to <c>HOVER_TYPE_15_ROW</c>,
	/// which the hover counts once for each of its frames.
	/// </summary>
	/// <remarks><b>Mutations:</b> the count put back in the ground's arm, so a click counts too.</remarks>
	[TestMethod]
	public void OnlyTheHoverCountsARowItHasNotBuilt()
	{
		_stack.Update();
		var level = Level.Current!;
		var state = level.ParkState!;
		var footprint = (X: 60, Y: 30);
		state.SetRecord( footprint.X, footprint.Y, state.Record( footprint.X, footprint.Y ) with { Type = 0x15 } );
		int Counted() => Unimplemented.Summary.Where( gap => gap.What == "HOVER_TYPE_15_ROW" ).Sum( gap => gap.Times );

		level.WorldHelpRow( footprint.X, footprint.Y, 0 );
		Unimplemented.Forget();
		level.WorldHelpRow( footprint.X, footprint.Y, 0 );
		level.WorldHelpRow( footprint.X, footprint.Y, 0 );
		Assert.AreEqual( 2, Counted(), "the hover, twice" );

		level.GroundArmAt( footprint.X, footprint.Y );
		level.ClickWorldAt( footprint.X, footprint.Y, 0 );
		Assert.AreEqual( 2, Counted(), "the ground's arm and a click add none" );
	}

	/// <summary>
	/// <b>A person on the gate comes before the gate</b> (<c>FUN_00486d90</c>: the person's tests at <c>0x00487009</c> and
	/// <c>0x0048721c</c> decide before <c>0x00487231</c>, and the click reads the hover's category): a member of staff
	/// on (47,17) shows their row and opens their window, not the gate's row and the entry price; with Shift alone a guest
	/// there is the visitor, and with the staff's locator open and nobody taken the click does nothing and the hover posts
	/// no gate row.
	/// </summary>
	/// <remarks><b>Mutations:</b> the gate's test moved above the person's, in the hover and in the click.</remarks>
	[TestMethod]
	public void APersonOnTheGateIsTakenBeforeTheGate()
	{
		_stack.Update();
		var level = Level.Current!;
		var member = _people.Staff[0];
		var guest = AGuest();
		int Row( int thing ) => level.WorldHelpRow( 47, 17, thing );

		Row( 0 );
		Assert.AreEqual( 459, Row( guest ), "a guest with no locator and no Shift is the ground's: the gate" );
		Assert.AreEqual( 555, Row( member.ThingId ), "a member, before the gate" );
		StringAssert.Contains( ClickAt( 47, 17, member.ThingId ), $"opened the staff window for thing {member.ThingId}" );
		Assert.AreEqual( 0, _stack.Windows.Count( window => window is ParkEntryPriceScreen ), "not the entry price" );
		_stack.Close( _stack.Windows.OfType<ParkStaffWindow>().Single() );
		_stack.Update();

		_people.Guests[guest].VisitorNumber = 13;
		Input.Keyboard = new Input.KeyboardInfo( [Key.ShiftLeft] );
		Assert.AreEqual( 553, Row( guest ), "Shift alone: the guest decides" );
		StringAssert.Contains( ClickAt( 47, 17, guest ), $"opened the visitor window for thing {guest}" );
		Assert.AreEqual( 0, _stack.Windows.Count( window => window is ParkEntryPriceScreen ) );
		_stack.Close( _stack.Windows.OfType<ParkVisitorWindow>().Single() );
		Input.Keyboard = new();

		CtrlS();
		Row( 0 );
		Assert.AreNotEqual( 459, Row( 0 ), "the staff's locator open and nobody taken: no gate row" );
		StringAssert.Contains( ClickAt( 47, 17, 0 ), "nothing to do" );
		Assert.AreEqual( 0, _stack.Windows.Count( window => window is ParkEntryPriceScreen ) );
	}

	/// <summary>
	/// <b>Under a park screen the hover runs none</b> (<c>0x00488569</c>): the row last posted stands and the category
	/// is kept, so after the screen closes the default arm empties the row, as bare ground's category 2 calls for. Were the
	/// hover run as a hand full or a tool armed, the screen would take the category to nought and the row would stand
	/// after it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the park screen dropped from the not-taken test of <c>WorldHelpRow</c>.</remarks>
	[TestMethod]
	public void UnderAParkScreenTheRowStandsAndTheCategoryIsKept()
	{
		_stack.Update();
		var level = Level.Current!;
		int Row( int x, int y ) => level.WorldHelpRow( x, y, 0 );

		Assert.AreEqual( 441, Row( 43, 16 ), "bare ground, category 2" );
		ParkStaffWindow.Open( _stack, _people.Staff[0].ThingId );
		_stack.Update();
		Assert.IsTrue( WindowStack.ParkScreenOpen, "the staff window is a park screen" );
		Assert.AreEqual( (441, 441), (Row( 43, 16 ), Row( 44, 16 )), "the screen open: the row stands, even on the default arm" );

		_stack.Close( _stack.Windows.OfType<ParkStaffWindow>().Single() );
		_stack.Update();
		Assert.IsFalse( WindowStack.ParkScreenOpen );
		Assert.AreEqual( -1, Row( 44, 16 ), "closed: the category before the screen still decides, so the default empties the row" );
	}

	/// <summary>
	/// <b>The pointer asks the cell only for what is not a person</b> (<see cref="ParkPicking.ThingOn"/> with
	/// <c>people: false</c>): people are taken by their pictures (<see cref="SpritePick"/>). A guest heading a cell's list
	/// is passed over for what stands behind them, a member too, and a console click on the cell still takes the head.
	/// </summary>
	/// <remarks><b>Mutations:</b> people never passed over; staff not passed over; the list's head alone asked.</remarks>
	[TestMethod]
	public void ThePointerAsksTheCellOnlyForWhatIsNotAPerson()
	{
		var state = Level.Current!.ParkState!;
		var guest = AGuest();
		var member = _people.Staff[0].ThingId;
		var (x, y) = (Enumerable.Range( 30, 60 ).First( column => state.CellAt( column, 60 ).Occupant == 0 ), 60);
		var behind = state.Objects.First().ThingId;

		state.StandOn( behind, x, y );
		state.StandOn( member, x, y );
		state.StandOn( guest, x, y );

		Assert.AreEqual( guest, ParkPicking.ThingOn( state, x, y ), "a click on the cell: the head" );
		Assert.AreEqual( behind, ParkPicking.ThingOn( state, x, y, people: false ), "the guest and the member passed over" );
	}

	/// <summary>
	/// <b>The hover asks whether the pointer is on the park's view where the pointer is now</b>, through the frame's own
	/// entry (<c>Level.WorldHelpRow()</c>), which runs before the interface's update: the pointer moved onto the gadget,
	/// and the stack not yet updated, nothing behind it is taken and the row stands, with the arrow.
	/// </summary>
	/// <remarks><b>Mutations:</b> the gate taken as the view; the gate from the stack's last update.</remarks>
	[TestMethod]
	public void TheHoverAsksTheViewWhereThePointerIsNow()
	{
		var level = Level.Current!;
		var entry = typeof( Level ).GetMethod( "WorldHelpRow", BindingFlags.NonPublic | BindingFlags.Instance, System.Type.EmptyTypes )!;
		int Hover() => (int)entry.Invoke( level, [] )!;

		Input.Mouse = new() { Position = new Vector2( 1800, 200 ) };
		_stack.Update();
		level.WorldHelpRow( 43, 16, 0 );
		Assert.AreEqual( 441, level.WorldHelpRow( 43, 16, 0 ) );

		Input.Mouse = new() { Position = new Vector2( 200, 1350 ) };
		Assert.AreEqual( 441, Hover(), "over the gadget before the stack's update: the row stands" );
		Assert.AreEqual( Input.CursorTypes.Normal, level.IdleCursor() );
		Input.Mouse = new();
	}

	/// <summary>
	/// <b>The help bar holds one row, whoever posted it last</b> (<c>docs/exe/hud.md</c>, "The help bar holds one posted
	/// row"; <c>q473/orig</c>): the gadget's Buy button posts its own row and leaving it empties the bar; back on the view
	/// the hovers are at the last place first, so with the ground left last its row comes back over a hoarding (round a,
	/// O3) and with the hoarding left last the bar stays empty (round b, B4). Under a park screen the hover posts nothing:
	/// the ground's row stands beside the screen (O7), its body empties the bar, and coming off the body the category is
	/// nought, so after it closes the ground posts again.
	/// </summary>
	/// <remarks><b>Mutations:</b> a control's leave posts nothing; the world's row taken over a control; the return at
	/// the pointer's own place; the last place never kept; the category not nought between the return's hovers; the
	/// screen's body not emptied; the body taken for the view; the hover posting under a park screen; the hover posting
	/// every frame; a row changed under the pointer not posted.</remarks>
	[TestMethod]
	public void TheHelpBarHoldsOneRowWhoeverPostedIt()
	{
		_stack.Update();
		var level = Level.Current!;
		var popupHelp = GameOptions.Current.PopupHelp;
		GameOptions.Current.PopupHelp = true;
		var (ground, hoarding) = ((43, 16), (42, 16));
		var (park, body) = (new Vector2( 1800, 200 ), new Vector2( 1000, 700 ));
		var build = Localization.Help( 441 );
		const string buyRow = "Left-click to buy and build attractions or to hire staff";

		// The gadget's Buy button, found by its row.
		var gadget = Enumerable.Range( 0, 64 * 48 ).Select( i => new Vector2( i % 64 * 32 + 16, i / 64 * 32 + 16 ) ).First( point =>
		{
			Input.Mouse = new() { Position = point };
			_stack.Update();
			return _stack.Hovered is { } control && Localization.Help( control.HelpText ) == buyRow;
		} );

		// One frame as Level.Update runs it: the hover at the cell, then the interface.
		string Frame( (int X, int Y) cell, Vector2 at )
		{
			Input.Mouse = new() { Position = at };
			var row = level.WorldHelpRow( cell.X, cell.Y, 0, _stack.PointerOnTheView );
			UI.WindowStack.WorldHelpPost = level.HoverPost( row );
			_stack.Update();
			return _stack.HelpBarText;
		}

		try
		{
			Assert.AreEqual( build, Frame( ground, park ) );
			var buy = Frame( ground, gadget );
			Assert.IsNotNull( _stack.Hovered );
			Assert.AreEqual( buyRow, buy, "the Buy button's own row (O2)" );
			var buyButton = _stack.Hovered!;
			buyButton.HelpText = 470;
			Assert.AreEqual( Localization.Help( 470 ), Frame( ground, gadget ), "ours: a row changed under the pointer is posted" );
			buyButton.HelpText = 469;
			Assert.AreEqual( buy, Frame( ground, gadget ) );
			Assert.AreEqual( build, Frame( hoarding, park ), "back onto a hoarding, the ground left last: its row again (O3)" );
			Assert.AreEqual( build, Frame( hoarding, park ), "and it stands" );
			Assert.AreEqual( buy, Frame( hoarding, gadget ) );
			Assert.AreEqual( "", Frame( hoarding, park ), "back onto the hoarding, the hoarding left last: the bar left empty (B4)" );
			Assert.AreEqual( (-1, ""), (_stack.HelpRow, Frame( hoarding, park )) );
			Assert.AreEqual( -1, level.WorldHelpRow( hoarding.Item1, hoarding.Item2, 0 ), "the hover's own row is the emptied bar's" );
			Assert.AreEqual( build, Frame( ground, park ), "the ground's category: posted" );

			Input.Mouse = new() { Position = gadget };
			level.WorldHelpRow( ground.Item1, ground.Item2, 0, _stack.PointerOnTheView );
			UI.WindowStack.WorldHelpPost = (442, []);
			_stack.Update();
			Assert.AreEqual( buy, _stack.HelpBarText, "a row the view posts is not taken over a control" );
			Assert.AreEqual( build, Frame( ground, park ) );

			var screen = new ParkEntryPriceScreen( _stack );
			_stack.Open( screen );
			Assert.AreEqual( build, Frame( ground, park ) );
			Assert.AreEqual( build, Frame( hoarding, park ), "a park screen open beside the pointer: no hover, the row stands (O7)" );
			Assert.IsTrue( _stack.Hovered == null && _stack.OnParkScreen( body.X, body.Y ), "the screen's body" );
			Assert.AreEqual( "", Frame( ground, body ), "onto the screen's body: emptied" );
			Assert.AreEqual( "", Frame( ground, park ), "back beside it: no hover under a park screen" );
			_stack.Close( screen );
			Frame( ground, park );
			Assert.AreEqual( build, Frame( ground, park ), "closed: the category nought since the pointer came off the body, so posted" );

			_stack.Open( screen = new ParkEntryPriceScreen( _stack ) );
			Assert.AreEqual( build, Frame( ground, park ) );
			_stack.Close( screen );
			Assert.AreEqual( build, Frame( ground, park ), "the pointer kept beside the screen: the row stood throughout" );

		}
		finally
		{
			GameOptions.Current.PopupHelp = popupHelp;
			Input.Mouse = new();
		}
	}

	/// <summary>
	/// <b>A carried thing wears <c>c_place</c> where it would go down and <c>c_noplace</c> where it would not</b>
	/// (<c>FUN_0052f950</c>, mode 4: cursor 7, then 8 under the pass's refusal), as the original's did over (48,31) and over
	/// the stock Belly Bounce (51,25) (<c>q633/orig/c-run.out</c>, cursor 7 and 8); with the hand empty, the view's own.
	/// </summary>
	/// <remarks><b>Mutations:</b> the carry left to the idle pointer, as before (the arrow); the Cash branch of <c>CarryCursor</c> removed.</remarks>
	[TestMethod]
	public void ACarriedThingWearsThePlacePointer()
	{
		var level = Level.Current!;

		try
		{
			StringAssert.StartsWith( ParkBuilding.Carry( 1100 ), "carry: holding" );

			ParkPicking.Pinned = (48, 31);
			ParkPicking.Update();
			Assert.IsTrue( ParkPicking.TryCell( out var x, out var y ) && (x, y) == (48, 31), "the pinned cell is the pointer's" );
			Assert.AreEqual( Input.CursorTypes.Place, level.WantedCursor(), "over bare ground" );

			ParkPicking.Pinned = (51, 25);
			ParkPicking.Update();
			Assert.AreEqual( Input.CursorTypes.NoGo, level.WantedCursor(), "over the stock Belly Bounce" );

			// A move can be dearer than the balance (the buy list lets nothing dearer into the hand): the squares go red for
			// the money, over bare ground too, and the pointer is c_cash, cursor 0x14 (FUN_0052f950's tail, 0x0052fa06).
			var state = level.ParkState!;
			ParkPicking.Pinned = (48, 31);
			ParkPicking.Update();
			state.Spend( state.Balance );
			Assert.IsTrue( ParkBuilding.CarriedUnaffordable(), "the thing in the hand costs more than the park has" );
			Assert.AreEqual( Input.CursorTypes.Cash, level.WantedCursor(), "refused for money over bare ground" );

			ParkHand.LetGo();
			Assert.AreNotEqual( Input.CursorTypes.Place, level.WantedCursor(), "the hand empty" );
		}
		finally
		{
			ParkHand.LetGo();
			ParkPicking.Pinned = null;
			ParkPicking.Update();
		}
	}

	/// <summary>
	/// <b>First person lays its own layer over the park's view</b> (<c>FUN_00481470</c> -> <c>FUN_004a2ac0</c>; <c>docs/exe/hud.md</c>,
	/// "The help bar in first person"): the layer is the control under the pointer wherever the eject button is not, its
	/// row none, so the bar empties and no hover runs over the gate or the ground under it, and the pointer is its
	/// crosshair, <c>0x15</c>. The eject button posts its own row and wears the arrow. Out of it the view comes back under
	/// the pointer as from a control: the hover runs at the last place on the view, the click's cell, and posts its row.
	/// </summary>
	/// <remarks><b>Mutations:</b> no layer (the hover runs under the viewfinder); the layer's row taken for the view's; the
	/// layer's cursor not worn; the layer worn over the eject button; the layer asked only of a shown window; the last
	/// place moved under the layer.</remarks>
	[TestMethod]
	public void FirstPersonLaysItsLayerOverTheView()
	{
		var level = Level.Current!;
		var popupHelp = GameOptions.Current.PopupHelp;
		GameOptions.Current.PopupHelp = true;
		var (ground, hoarding, gate) = ((43, 16), (42, 16), (47, 17));
		var (park, eject) = (new Vector2( 1800, 200 ), new Vector2( 1960, 1450 ));
		var build = Localization.Help( 441 );
		var viewfinder = new ParkViewfinder( _stack );
		_stack.Open( viewfinder );

		string Frame( (int X, int Y) cell, Vector2 at )
		{
			Input.Mouse = new() { Position = at };
			var row = level.WorldHelpRow( cell.X, cell.Y, 0, _stack.PointerOnTheView );
			UI.WindowStack.WorldHelpPost = level.HoverPost( row );
			_stack.Update();
			return _stack.HelpBarText;
		}

		void InFirstPerson( bool active )
			=> typeof( ParkCamcorderCameraMode ).GetProperty( nameof( ParkCamcorderCameraMode.Active ) )!.SetValue( null, active );

		try
		{
			Assert.AreEqual( build, Frame( ground, park ) );
			Assert.AreNotEqual( Input.CursorTypes.Crosshair, level.WantedCursor(), "in orbit: the view's own pointer" );

			InFirstPerson( true );
			Assert.IsFalse( _stack.PointerOnTheView, "the layer, from the frame the mode starts" );
			Assert.AreEqual( Input.CursorTypes.Crosshair, level.WantedCursor(), "the layer's 0x15" );
			var bar = Frame( ground, park );
			Assert.AreEqual( (-1, ""), (_stack.HelpRow, bar), "onto the layer: the bar emptied" );
			Assert.AreEqual( "", Frame( gate, park ), "no hover over the gate under it" );
			Assert.AreEqual( "", Frame( ground, park ), "nor over the ground" );
			Assert.AreEqual( Input.CursorTypes.Crosshair, level.WantedCursor() );

			Assert.AreEqual( Localization.Help( 468 ), Frame( gate, eject ), "the eject button's own row" );
			Assert.AreEqual( Input.CursorTypes.Normal, level.WantedCursor(), "and the arrow over it" );
			Assert.AreEqual( "", Frame( gate, park ), "back onto the layer: emptied" );

			InFirstPerson( false );
			Assert.AreEqual( build, Frame( hoarding, park ), "out: the hover at the last place on the view, the ground's row" );
			Assert.IsTrue( _stack.PointerOnTheView );
			Assert.AreNotEqual( Input.CursorTypes.Crosshair, level.WantedCursor() );
			Assert.AreEqual( Localization.Help( 459 ), Frame( gate, eject ), "the eject button gone with the mode: the view's hover over the gate" );
		}
		finally
		{
			InFirstPerson( false );
			_stack.Close( viewfinder );
			GameOptions.Current.PopupHelp = popupHelp;
			Input.Mouse = new();
		}
	}

	/// <summary>
	/// <b>The hover sets the member idle as it posts their row, wherever PICK UP would take them</b>
	/// (<c>FUN_00505f50</c> asks <c>FUN_00505bd0</c>, then <c>SetState( 0 )</c>): once as the pointer comes onto them and
	/// not again while it stays, from a walk with the clock as the stamp, never a member on strike. Off the park's view
	/// (<c>[0x007c24fc]</c>) no hover runs and the row stands; coming back onto it the member is posted twice at the last
	/// place, the thing forgotten before and the category nought after (<c>0x0048884e</c>), the second set from idle. Over a control the pointer is the arrow, as
	/// the original's over the gadget's buttons.
	/// </summary>
	/// <remarks><b>Mutations:</b> not set; set in any state; set every frame; set off the view; the stamp nought from a
	/// walk; the thing not forgotten as the pointer comes onto the view; the category not nought after it; the entry
	/// never seen; the hand off the view; a control under the pointer taken for the view.</remarks>
	[TestMethod]
	public void TheHoverSetsTheMemberIdleWherePickUpWouldTakeThem()
	{
		_stack.Update();
		var level = Level.Current!;
		var state = level.ParkState!;
		var member = _people.Staff.First( staff => Peep.CountsOn( state.Record( staff.Navigator.Position.Cell.X, staff.Navigator.Position.Cell.Y ).Type ) );
		int Row( int thing, bool onTheView = true ) => level.WorldHelpRow( 43, 16, thing, onTheView );
		var sets = 0;

		// Whether the hover set them idle since the last look, counted, and the member walking again either way.
		bool Set()
		{
			var idle = member.Activity == StaffActivity.Idle;
			sets += idle ? 1 : 0;
			member.SetActivity( StaffActivity.Walking, state.GameTick );
			return idle;
		}

		member.SetActivity( StaffActivity.Walking, state.GameTick );

		Input.Mouse = new() { Position = new Vector2( 200, 1350 ) };
		_stack.Update();
		Assert.IsTrue( _stack.Hovered != null && !_stack.PointerOnTheView, "over the gadget" );
		Input.Mouse = new() { Position = new Vector2( 1800, 200 ) };
		_stack.Update();
		Assert.IsTrue( _stack.Hovered == null && _stack.PointerOnTheView, "over the park" );
		Input.Mouse = new();

		Row( 0 );
		Row( 0 );
		Assert.AreEqual( 555, Row( member.ThingId ) );
		Assert.AreEqual( (StaffActivity.Idle, state.GameTick), (member.Activity, member.TimeStartedIdling), "from a walk, the clock" );
		Assert.IsTrue( Set(), "as the pointer comes onto them" );
		Row( member.ThingId );
		Assert.IsFalse( Set(), "not again while the pointer stays on them" );

		Row( 0 );
		member.SetActivity( StaffActivity.OnStrike, state.GameTick );
		Row( member.ThingId );
		Assert.AreEqual( StaffActivity.OnStrike, member.Activity, "on strike, PICK UP would not take them" );
		member.SetActivity( StaffActivity.Walking, state.GameTick );

		Row( 0 );
		Assert.AreEqual( 441, Row( member.ThingId, onTheView: false ), "off the view the row last posted stands" );
		Assert.IsFalse( Set(), "no hover off the view" );
		Assert.AreEqual( Input.CursorTypes.Normal, level.IdleCursor(), "the arrow over a control" );

		Row( 0 );
		Row( member.ThingId );
		Assert.IsTrue( Set() );
		Row( member.ThingId, onTheView: false );
		Assert.AreEqual( Input.CursorTypes.Normal, level.IdleCursor(), "the arrow over a control, the member's row standing" );
		Assert.AreEqual( 555, Row( member.ThingId ), "back onto the view, onto the member they last were" );
		Assert.AreEqual( Input.CursorTypes.Pick, level.IdleCursor() );
		Assert.AreEqual( (StaffActivity.Idle, 0), (member.Activity, member.TimeStartedIdling),
			"set as the thing is forgotten, and again from idle as the category is nought: the stamp nought" );
		Assert.IsTrue( Set() );
		Row( member.ThingId );
		Assert.IsFalse( Set(), "and then not again" );
		Assert.AreEqual( 3, sets );
	}

	/// <summary>
	/// <b>The picker puts the window's own width and height to the latch, hands on the person and the object each as its
	/// own, and keeps the last point where the ray met nothing</b> (<see cref="ParkPicking.Assemble"/>, which
	/// <c>Resolve</c> calls). At 1280 x 720 a guest's picture is latched with the pointer on its circle's middle (0.3264,
	/// 0.611); a ride stands behind the guest on the cell and is the object.
	/// </summary>
	/// <remarks><b>Mutations:</b> the window's width and height swapped; the person and the object swapped; the point
	/// taken from the ray where it met nothing.</remarks>
	[TestMethod]
	public void ThePickerPutsTheWindowAndThePersonAndTheObjectToTheirPlaces()
	{
		var state = Level.Current!.ParkState!;
		var guest = AGuest();
		var (x, y) = (Enumerable.Range( 30, 60 ).First( column => state.CellAt( column, 60 ).Occupant == 0 ), 60);
		var ride = state.Objects.First().ThingId;
		state.StandOn( ride, x, y );
		state.StandOn( guest, x, y );

		var (viewMatrix, projMatrix, size) = (Camera.ViewMatrix, Camera.ProjMatrix, Screen.Size);

		try
		{
			Screen.Size = new Point2( 1280, 720 );
			Camera.ViewMatrix = System.Numerics.Matrix4x4.Identity;
			Camera.ProjMatrix = new System.Numerics.Matrix4x4( 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0 );

			var depth = 65.45f / System.MathF.Sin( System.MathF.PI / 4f );
			var originX = 0.3264f + (21f * 20f / 128f * 0.5625f / (depth * System.MathF.Sin( System.MathF.PI / 4f )));
			var drawn = new SpritePick.Drawn( guest, new Vector3( originX * depth, -0.611f * depth, depth ), 56, 60, 21, 41, false );
			var pointer = new Vector2( (0.3264f + 1f) * 640f, (0.611f + 1f) * 360f );
			var hit = new Vector3( 12f, 34f, 5f );

			var answer = ParkPicking.Assemble( MapStep.CellId( x, y ), true, hit, pointer, [drawn], state );
			Assert.AreEqual( (guest, ride, guest), (answer.Person, answer.Object, answer.Latched) );
			Assert.AreEqual( hit, answer.World );
			Assert.IsTrue( answer.Met );

			answer = ParkPicking.Assemble( MapStep.CellId( x, y ), false, hit, pointer, [drawn], state );
			Assert.AreNotEqual( hit, answer.World, "the ray met nothing: the last point stands" );
			Assert.IsFalse( answer.Met );

			answer = ParkPicking.Assemble( MapStep.CellId( x, y ), true, hit, new Vector2( 0, 0 ), [drawn], state );
			Assert.AreEqual( (0, ride, 0), (answer.Person, answer.Object, answer.Latched), "no picture under the pointer: the ride alone" );
		}
		finally
		{
			(Camera.ViewMatrix, Camera.ProjMatrix, Screen.Size) = (viewMatrix, projMatrix, size);
		}
	}

	/// <summary>
	/// <b>A queue's row and an object's</b> (<c>FUN_00486d90</c>, <c>0x00487603</c> and <c>0x00487665</c> to
	/// <c>0x00487718</c>). Over a queue, category 3: UIHELPTEXT 444 "Left-click to edit this queue", posted on a change of
	/// category, and the pointer <c>c_queue</c>. Over an object, category 4: 550 with the object's name as <c>FUN_004e2780</c>
	/// joins it and the pick hand, posted only for an object other than the thing the hover holds, whatever the category,
	/// so the same object after a hoarding or an object-less cell, which keep the thing, posts nothing, and after the
	/// ground, which forgets it, posts again. An add-on that is a track ride's (<c>AddOn.UpgradeType</c> 1) posts nothing and
	/// keeps the old row; one that is another ride's (2) posts its row. Shift alone with nobody taken empties the row and
	/// keeps the thing, so the same object after it leaves the bar empty; coming back onto the view forgets it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the queue's pointer the pick hand; type <c>0x15</c> given the queue's category; the object's row posted on a change of category; the
	/// thing kept by the ground, the default arm, the queue, the gate, a member or a guest; the thing forgotten by a hoarding or under Shift; the thing kept on coming back onto the
	/// view; the name dropped; the add-on test dropped; the add-on's type test dropped.</remarks>
	[TestMethod]
	public void AQueueAndAnObjectPostTheirRows()
	{
		_stack.Update();
		var level = Level.Current!;
		var state = level.ParkState!;
		(int Row, bool Posted, string Text) Hover( int x, int y )
		{
			var row = level.WorldHelpRow( x, y, 0 );
			return (row, level.HoverPosted, row < 0 ? "" : Localization.FormatHelp( row, level.WorldHelpValues ));
		}

		(int Row, bool Posted) Head( int x, int y, int person = 0 )
		{
			var row = level.WorldHelpRow( x, y, person );
			return (row, level.HoverPosted);
		}

		var queue = (X: 60, Y: 30);
		var ride = (X: 62, Y: 30);
		var addOn = (X: 64, Y: 30);
		var hoarding = (X: 42, Y: 16);
		var orphan = (X: 66, Y: 30);
		state.SetRecord( queue.X, queue.Y, state.Record( queue.X, queue.Y ) with { Type = 3 } );
		state.SetRecord( ride.X, ride.Y, state.Record( ride.X, ride.Y ) with { Type = CellEdge.Footprint, ParentId = 0 } );
		state.AddObject( new ParkWorld.CatalogueObject( state.NextThingId(), 1100, ride.X << 8, ride.Y << 8, 0 ) );
		state.SetRecord( addOn.X, addOn.Y, state.Record( addOn.X, addOn.Y ) with { Type = CellEdge.Footprint, ParentId = 0 } );
		state.AddObject( new ParkWorld.CatalogueObject( state.NextThingId(), 1500, addOn.X << 8, addOn.Y << 8, 0 ) );
		state.SetRecord( orphan.X, orphan.Y, state.Record( orphan.X, orphan.Y ) with { Type = CellEdge.Footprint, ParentId = 0 } );

		Hover( 43, 16 );
		Assert.AreEqual( (444, true, "Left-click to edit this queue"), Hover( queue.X, queue.Y ) );
		Assert.AreEqual( Input.CursorTypes.Queue, level.HoverPointer, "c_queue over a queue" );
		Assert.AreEqual( Input.CursorTypes.Queue, level.IdleCursor() );
		Assert.IsFalse( Hover( queue.X, queue.Y ).Posted, "the same category: not posted again" );
		var pylon = (X: 68, Y: 30);
		state.SetRecord( pylon.X, pylon.Y, state.Record( pylon.X, pylon.Y ) with { Type = 0x15 } );
		Assert.AreEqual( (-1, true), Head( pylon.X, pylon.Y ), "type 0x15 after the queue: its own category 6, the row emptied" );
		Assert.AreEqual( (444, true), Head( queue.X, queue.Y ) );

		var toilet = "Small Toilet: Left-click to view/edit this item's details. Hold CTRL and click to buy another";
		Assert.AreEqual( (550, true, toilet), Hover( 55, 15 ) );
		Assert.IsTrue( level.PickPointer, "the pick hand over an object" );
		Assert.AreEqual( Input.CursorTypes.Pick, level.IdleCursor() );
		Assert.IsFalse( Hover( 55, 15 ).Posted, "the same object" );
		Assert.AreEqual( (550, true), Head( ride.X, ride.Y ), "another object, the same category: posted" );
		StringAssert.StartsWith( Hover( 55, 15 ).Text, "Small Toilet:" );

		// Every arm that forgets the thing posts the object again on the way back (0x004877b9, 0x00487783, 0x004878c8 and
		// the person arms 0x00487216, 0x004870dd), not only the ground's.
		Assert.AreEqual( (-1, true), Head( 44, 16 ), "a cell of map type 7: no category, the row emptied" );
		Assert.AreEqual( (550, true), Head( 55, 15 ), "forgotten by the default arm: posted again" );
		Assert.AreEqual( (444, true), Head( queue.X, queue.Y ) );
		Assert.AreEqual( (550, true), Head( 55, 15 ), "forgotten by the queue" );
		Assert.AreEqual( (459, true), Head( 47, 17 ) );
		Assert.AreEqual( (550, true), Head( 55, 15 ), "forgotten by the gate" );
		Assert.AreEqual( (555, true), Head( 43, 16, _people.Staff[0].ThingId ), "a member taken" );
		Assert.AreEqual( (550, true), Head( 55, 15 ), "forgotten by the member" );
		CtrlV();
		Assert.IsTrue( Head( 43, 16, AGuest() ).Row is 553 or 554, "a guest taken, the visitors' locator open" );
		Assert.IsTrue( Fold() );
		Assert.AreEqual( (550, true), Head( 55, 15 ), "forgotten by the guest" );

		Assert.AreEqual( (550, false), Head( hoarding.X, hoarding.Y ), "a hoarding posts nothing and keeps the thing" );
		Assert.AreEqual( (550, false), Head( 55, 15 ), "so the same object posts nothing" );
		Assert.AreEqual( (550, false), Head( orphan.X, orphan.Y ), "an object's cell with no object the same" );
		Assert.AreEqual( (550, false, toilet), Hover( 55, 15 ) );
		Assert.AreEqual( (550, false), Head( addOn.X, addOn.Y ), "a track ride's add-on: nothing posted" );
		Assert.AreEqual( (550, false), Head( 55, 15 ), "and the thing kept" );
		Hover( hoarding.X, hoarding.Y );
		Assert.AreEqual( (441, true), Head( 43, 16 ), "the ground forgets the thing" );
		Assert.AreEqual( (550, true, toilet), Hover( 55, 15 ) );
		Input.Keyboard = new Input.KeyboardInfo( [Key.ShiftLeft] );
		Assert.AreEqual( (-1, true), Head( 55, 15 ), "Shift alone, nobody taken: emptied after the object's category" );
		Input.Keyboard = new();
		Assert.AreEqual( (-1, false), Head( 55, 15 ), "and the thing kept (0x004870f3): the same object posts nothing" );
		level.WorldHelpRow( 55, 15, 0, onTheView: false );
		Assert.AreEqual( (550, true, toilet), Hover( 55, 15 ), "back on the view the thing is forgotten: posted again" );

		var space = new ParkItemCatalogue( "space", FileSystem );
		Assert.IsTrue( space.TryGet( 3502, out var zob ) );
		Assert.IsTrue( level.Catalogue!.TryGet( 1500, out var lavaJump ) );
		Assert.AreEqual( (3109, 2, 1150, 1), (zob.AddOnUpgradesId, zob.AddOnUpgradeType, lavaJump.AddOnUpgradesId, lavaJump.AddOnUpgradeType) );
		typeof( Level ).GetProperty( nameof( Level.Catalogue ) )!.SetValue( level, space );
		state.AddObject( new ParkWorld.CatalogueObject( state.NextThingId(), 3502, orphan.X << 8, orphan.Y << 8, 0 ) );
		Assert.AreEqual( (550, true), Head( orphan.X, orphan.Y ), "another ride's add-on, UpgradeType 2: its row" );
	}
}
