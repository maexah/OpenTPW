using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The visitor's window (<c>FUN_004b79b0</c>, its handler <c>FUN_004b7720</c>; <c>docs/exe/hud.md</c>, "The person's
/// windows"), opened from the locator's strip and the visitors list on the shipped park's guests. Skipped without the
/// game (<see cref="GameData"/>).
/// </summary>
[TestClass]
public class ParkVisitorWindowTests
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

		_people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), null, state, catalogue );
		_stack = new WindowStack();
		_gadget = new ParkGadget( _stack );
		_stack.Open( _gadget );

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
		Time.PinWall( 0 );
		Input.Mouse = new();
		ParkOrbitCameraMode.PointOfInterest = _looksAt;
		Unimplemented.Forget();
	}

	private ParkLocator Locator => _gadget.Locator;

	private ParkVisitorWindow? Window => _stack.Windows.OfType<ParkVisitorWindow>().FirstOrDefault();

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( gap => gap.What == what ).Times;

	private void Click( UiControl root, int id )
	{
		var control = Find( root, id );
		Assert.IsTrue( _stack.ClickAt( (control.Rect.Left + control.Rect.Right) / 2, (control.Rect.Top + control.Rect.Bottom) / 2 ), $"{id:x} clicked" );
	}

	/// <summary>Ctrl+V, the visitors' button, then the strip: the window on the guest the strip names.</summary>
	private Peep OpenFromTheStrip()
	{
		Locator.OpenVisitors( _gadget.CarryLocatorPanel, () => _stack.CloseParkScreen() );
		ParkGadgetPressTests.Settle( _gadget );
		Click( _gadget.Root, 0x964 );
		ParkGadgetPressTests.Settle( _gadget );

		var guest = _people.Guests[Locator.Actor];
		Click( _gadget.Root, 0x146 );
		_gadget.Update();
		ParkGadgetPressTests.Settle( _gadget );

		return guest;
	}

	/// <summary>
	/// <b>The strip's click opens the window on its guest</b> (<c>FUN_004b3c10</c>): a park screen, so the arm folds the
	/// locator closed; titled "Visitor #" and the number, its eight labels UITEXT 70, 71, 72, 75, 73, 74, 76 and 77; the
	/// preview counted. A member's strip opens the staff member's window instead.
	/// </summary>
	/// <remarks><b>Mutations:</b> the strip's opener gone; a label's row swapped; the title's parameter or rows; the preview not counted.</remarks>
	[TestMethod]
	public void TheStripsClickOpensTheWindowOnItsGuest()
	{
		_people.Peeps[0].VisitorNumber = 13;

		var guest = OpenFromTheStrip();

		Assert.IsNotNull( Window, "the window is up" );
		Assert.AreEqual( guest.ThingId, Window!.ThingId );
		Assert.AreEqual( ParkLocator.Kind.None, Locator.Open, "the arm folded the locator away" );
		Assert.AreEqual( "Visitor #13", Find( Window.Root, 0x3736 ).Text );
		Assert.AreEqual( 1, Times( "VISITOR_WINDOW_PREVIEW" ) );
		Assert.AreEqual( 0, Times( "VISITOR_WINDOW" ) );

		CollectionAssert.AreEqual(
			new[] { "Cash remaining", "Time in park", "Rides ridden", "Purchases made", "Sideshows played", "Sideshows won", "Happiness", "Current status" },
			new[] { 0x372c, 0x3725, 0x3726, 0x3727, 0x3728, 0x3729, 0x372a, 0x372b }.Select( id => Find( Window.Root, id ).Text ).ToArray() );

		_stack.Close( Window );

		Locator.OpenStaff( _gadget.CarryLocatorPanel, () => _stack.CloseParkScreen() );
		ParkGadgetPressTests.Settle( _gadget );
		Click( _gadget.Root, 0x95e );
		Click( _gadget.Root, 0x146 );
		Assert.AreEqual( (0, (ParkVisitorWindow?)null, 1), (Times( "STAFF_WINDOW" ), Window, _stack.Windows.OfType<ParkStaffWindow>().Count()) );
	}

	/// <summary>
	/// <b>The figures are the guest's</b> (<c>FUN_004b72a0</c>): the cash a money cell, the time four times the stay,
	/// <c>+0x1c4</c> to <c>+0x1d0</c> in the labels' order, the bar of the happiness's low byte, the status the
	/// <c>KIDSTATES</c> row of the state, and the thought's part less one with its <c>THOUGHTS</c> words; "New Visitor"
	/// turns to the number at the next refresh, four seconds of real time on.
	/// </summary>
	/// <remarks><b>Mutations:</b> the stay not times four; two counters' cells swapped; the bar's byte mask; the thought not less one; the refresh's period; the title kept at nought.</remarks>
	[TestMethod]
	public void TheFiguresAreTheGuestsAndRefreshEveryFourSeconds()
	{
		Time.PinWall( 1000 );
		var guest = _people.Peeps[0];
		guest.VisitorNumber = 0;
		guest.Cash = 684;
		guest.NumRides = 3;
		guest.NumShops = 5;
		guest.NumSideshows = 7;
		guest.NumSideshowsWon = 2;
		guest.Happiness = 56.0f + 256f;
		guest.Thoughts.Restore( 3, 0 );
		SetState( guest, PeepState.InQueue );

		ParkVisitorWindow.Open( _stack, guest.ThingId );
		var window = Window!;

		Assert.AreEqual( "New Visitor", Find( window.Root, 0x3736 ).Text );
		var stay = guest.StayAt( _people.State.GameTick ) << 2;
		var time = ParkVisitorsScreen.TimeText( ParkVisitorsScreen.HoursIn( stay ) );
		CollectionAssert.AreEqual( new[] { "$ 684", time, "3", "5", "7", "2", "Queueing for a ride" },
			new[] { 0x3724, 0x372d, 0x372e, 0x372f, 0x3730, 0x3731, 0x3733 }.Select( id => Find( window.Root, id ).Text ).ToArray() );
		Assert.AreEqual( (56 << 10) / 100, ((ParkObjectWindow.UiStatBar)Find( window.Root, 0x3732 )).Units );
		Assert.AreEqual( (2, "Hungry and thirsty"), (Find( window.Root, 0x3735 ).Frame, Find( window.Root, 0x3734 ).Text) );

		guest.VisitorNumber = 21;
		guest.Cash = 600;
		Time.PinWall( 4999 );
		window.Update();
		Assert.AreEqual( ("New Visitor", "$ 684"), (Find( window.Root, 0x3736 ).Text, Find( window.Root, 0x3724 ).Text), "not yet" );

		Time.PinWall( 5000 );
		window.Update();
		Assert.AreEqual( ("Visitor #21", "$ 600"), (Find( window.Root, 0x3736 ).Text, Find( window.Root, 0x3724 ).Text) );

		guest.Thoughts.Restore( 0, 0 );
		Time.PinWall( 9000 );
		window.Update();
		Assert.AreEqual( (ParkVisitorWindow.NoThought, ""), (Find( window.Root, 0x3735 ).Frame, Find( window.Root, 0x3734 ).Text) );
	}

	/// <summary>
	/// <b>The status names what the guest uses</b> at row 16, "Using: {P0}", by the thing's two name lines joined as
	/// <c>FUN_004e2780</c> joins them; a row past the table's end letters nothing.
	/// </summary>
	/// <remarks><b>Mutations:</b> the using row; the hyphen kept; the space dropped; the second line alone.</remarks>
	[TestMethod]
	public void TheStatusNamesWhatTheGuestUses()
	{
		Assert.AreEqual( "Using: Belly Bounce", ParkVisitorWindow.StatusOf( 16, 7, id => id == 7 ? "Belly Bounce" : "?" ) );
		Assert.AreEqual( "Doing nothing", ParkVisitorWindow.StatusOf( 6, 7, _ => "?" ) );
		Assert.AreEqual( "", ParkVisitorWindow.StatusOf( 40, 7, _ => "?" ) );

		Assert.AreEqual( "Dino Karts", ParkVisitorWindow.JoinName( "Dino", "Karts" ) );
		Assert.AreEqual( "DinoKarts", ParkVisitorWindow.JoinName( "Dino-", "Karts" ) );
		Assert.AreEqual( "Karts", ParkVisitorWindow.JoinName( "", "Karts" ) );
		Assert.AreEqual( "Dino", ParkVisitorWindow.JoinName( "Dino", "" ) );
	}

	/// <summary>
	/// <b>The arrows walk the guests</b>: b_arup towards the newest, round from the oldest (<c>FUN_00482da0</c>), b_ardown
	/// towards the oldest, round from the newest (<c>FUN_004824d0</c>); the title follows.
	/// </summary>
	/// <remarks><b>Mutations:</b> the arrows swapped; a wrap to the wrong end; the title not lettered again.</remarks>
	[TestMethod]
	public void TheArrowsWalkTheGuests()
	{
		Assert.AreEqual( 2, ParkVisitorWindow.Next( [1, 2, 3], 1, newer: false ) );
		Assert.AreEqual( 1, ParkVisitorWindow.Next( [1, 2, 3], 3, newer: false ) );
		Assert.AreEqual( 3, ParkVisitorWindow.Next( [1, 2, 3], 1, newer: true ) );
		Assert.AreEqual( 1, ParkVisitorWindow.Next( [1, 2, 3], 2, newer: true ) );
		Assert.AreEqual( 3, ParkVisitorWindow.Next( [1, 2, 3], 9, newer: true ), "one gone starts from the oldest" );
		Assert.AreEqual( 1, ParkVisitorWindow.Next( [1, 2, 3], 9, newer: false ), "or the newest" );

		var peeps = _people.Peeps;
		Assert.IsTrue( peeps.Count >= 3 );
		peeps[0].VisitorNumber = 13;
		peeps[1].VisitorNumber = 12;
		peeps[^1].VisitorNumber = 1;

		ParkVisitorWindow.Open( _stack, peeps[0].ThingId );
		var window = Window!;

		Click( window.Root, 0x3721 );
		Assert.AreEqual( (peeps[1].ThingId, "Visitor #12"), (window.ThingId, Find( window.Root, 0x3736 ).Text) );

		Click( window.Root, 0x3720 );
		Click( window.Root, 0x3720 );
		Assert.AreEqual( (peeps[^1].ThingId, "Visitor #1"), (window.ThingId, Find( window.Root, 0x3736 ).Text) );
	}

	/// <summary>
	/// <b>The advisor tells of the window as it is built and is taken back as it closes</b>: message <c>0x12e</c>,
	/// response 562, sample 580, one line, a tutorial line.
	/// </summary>
	/// <remarks><b>Mutations:</b> the post missing or another id; no withdrawal at the close; the row's response or sample.</remarks>
	[TestMethod]
	public void TheAdvisorTellsOfTheWindowAndIsTakenBackAsItCloses()
	{
		using var clock = new SimulationClockScope();
		var lines = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ), tutorial: () => true );
		var park = Level.Current!.ParkState!;
		park.AdvisorMessages = lines;
		var said = new List<(int Response, int Sample)>();
		int Play( int response, int sample ) { said.Add( (response, sample) ); return 9000; }

		ParkVisitorWindow.Open( _stack, _people.Peeps[0].ThingId );
		StringAssert.Contains( lines.Census(), "pending=1 attempts=0 threshold=25 slot=302/subject=0 " );

		lines.Tick( park.GameTick, 0, Play );
		CollectionAssert.AreEqual( new[] { (562, 580) }, said );

		_stack.Close( Window! );
		StringAssert.Contains( lines.Census(), "message=302/score=1000/line=0/" );
		StringAssert.Contains( lines.Census(), "/said=0/slaps=1", "cut off" );
	}

	/// <summary>
	/// <b>b_locate and Ctrl+V with the window up open the visitors' locator on its guest</b> (<c>FUN_004b3f60( guest )</c>):
	/// the panel out, the visitors' button down, the strip on them and followed, the thought in its corner, and the
	/// window closed.
	/// </summary>
	/// <remarks><b>Mutations:</b> the button not put down; the strip hidden; not followed; the status shown; the window left up.</remarks>
	[TestMethod]
	public void LocateOpensTheVisitorsLocatorOnTheGuest()
	{
		var guest = _people.Peeps[0];
		ParkVisitorWindow.Open( _stack, guest.ThingId );
		ParkGadgetPressTests.Settle( _gadget );

		Click( Window!.Root, 0x3737 );
		ParkGadgetPressTests.Settle( _gadget );

		Assert.IsNull( Window, "the window closed" );
		Assert.AreEqual( (ParkLocator.Kind.Visitors, ParkLocator.VisitorsRole, guest.ThingId, true, true),
			(Locator.Open, Locator.Role, Locator.Actor, Locator.Following, Locator.Strip.Visible) );
		StringAssert.Contains( Locator.Census(), "down=964" );
		Assert.AreEqual( (false, true), (Find( Locator.Strip, 0x148 ).Visible, Find( Locator.Strip, 0x14a ).Visible) );
	}

	/// <summary>
	/// <b>b_kids opens the visitors list with the guest picked and in view</b> (<c>FUN_00493530( guest )</c>), which closes
	/// the window; <b>a row's click opens the window on its guest</b> (<c>0x0049351e</c>), which closes the list.
	/// </summary>
	/// <remarks><b>Mutations:</b> the pick not made; the top not moved to it; the top not held to the last row; the row's opener gone; the list's line not counted.</remarks>
	[TestMethod]
	public void TheListAndTheWindowOpenEachOther()
	{
		var last = _people.Peeps[^1];
		ParkVisitorWindow.Open( _stack, last.ThingId );

		Click( Window!.Root, 0x3738 );
		var list = _stack.Windows.OfType<ParkVisitorsScreen>().Single();
		var rows = (UiList)typeof( ParkVisitorsScreen ).GetField( "_list", BindingFlags.Instance | BindingFlags.NonPublic )!.GetValue( list )!;
		Assert.IsNull( Window, "the window closed" );
		Assert.AreEqual( last.ThingId, rows.Selected );
		Assert.AreEqual( 1, Times( "VISITORS_LIST_ADVISOR_LINE" ), "the list's own line, 0x11e, counted" );

		var at = rows.Rows.ToList().FindIndex( row => row.Id == last.ThingId );
		Assert.IsTrue( at >= rows.ScrollTop && at < rows.ScrollTop + rows.VisibleRows, "in view" );
		if ( rows.Rows.Count > rows.VisibleRows )
			Assert.IsTrue( rows.ScrollTop <= rows.Rows.Count - rows.VisibleRows, "held to the last row" );

		var first = rows.Rows[rows.ScrollTop];
		rows.LeftClickedAt( (rows.RowArea.Left + rows.RowArea.Right) / 2f, rows.RowArea.Top + rows.RowHeight / 2f );
		Assert.AreEqual( first.Id, Window?.ThingId );
		Assert.AreEqual( 0, _stack.Windows.OfType<ParkVisitorsScreen>().Count(), "the list closed" );
	}

	/// <summary>
	/// <b>A list longer than its window brings a far row in as its top, held so the window ends at the last row</b>
	/// (<c>FUN_00665739</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the hold missing; a row in view moved; no selection.</remarks>
	[TestMethod]
	public void APickedRowIsBroughtIntoView()
	{
		var list = new UiList { Rect = new UiRect( 0, 0, 100, 100 ), RowArea = new UiRect( 0, 0, 100, 100 ), RowHeight = 25, Columns = [(0, 100)] };
		list.Build();

		for ( var id = 1; id <= 10; ++id )
			list.Add( new UiList.Row( id, $"{id}", id ) );

		list.Pick( 3 );
		Assert.AreEqual( (3, 0), (list.Selected, list.ScrollTop), "in view: the top stays" );

		list.Pick( 6 );
		Assert.AreEqual( (6, 5), (list.Selected, list.ScrollTop), "its row the top" );

		list.Pick( 10 );
		Assert.AreEqual( (10, 6), (list.Selected, list.ScrollTop), "held to the last row" );

		list.Pick( 99 );
		Assert.AreEqual( (10, 6), (list.Selected, list.ScrollTop), "no such row" );
	}

	/// <summary>
	/// <b>The preview's click goes to the guest and closes the window</b> (the base's <c>0x0048d1a0</c>), with either button.
	/// </summary>
	/// <remarks><b>Mutations:</b> the window left up; the camera not moved.</remarks>
	[TestMethod]
	public void ThePreviewsClickGoesToTheGuest()
	{
		var guest = _people.Peeps[0];
		var (x, y) = guest.Navigator.Position.Cell;
		ParkVisitorWindow.Open( _stack, guest.ThingId );

		Click( Window!.Root, 0x3722 );
		Assert.IsNull( Window, "closed" );
		Assert.AreEqual( new Vector3( x * 10f, y * 10f, 0f ), ParkOrbitCameraMode.PointOfInterest );

		ParkVisitorWindow.Open( _stack, guest.ThingId );
		var preview = Find( Window!.Root, 0x3722 );
		preview.RightClicked!();
		Assert.IsNull( Window, "the right button too" );
	}

	/// <summary>
	/// <b>A guest who leaves with the window up closes it at the next refresh</b>, counted, the original's behaviour not
	/// decoded.
	/// </summary>
	/// <remarks><b>Mutations:</b> the window left up; not counted.</remarks>
	[TestMethod]
	public void AGuestWhoLeavesClosesTheWindow()
	{
		Time.PinWall( 0 );
		var guest = _people.Peeps[0];
		ParkVisitorWindow.Open( _stack, guest.ThingId );
		Assert.IsTrue( _people.Depart( guest.ThingId ) );

		Time.PinWall( 4000 );
		Window!.Update();
		Assert.AreEqual( ((ParkVisitorWindow?)null, 1), (Window, Times( "VISITOR_WINDOW_GUEST_GONE" )) );
	}

	private static void SetState( Peep guest, PeepState state )
		=> typeof( Peep ).GetProperty( nameof( Peep.State ) )!.SetValue( guest, state );

	private static UiControl Find( UiControl root, int id )
		=> FindOrNull( root, id ) ?? throw new AssertFailedException( $"no control {id:x}" );

	private static UiControl? FindOrNull( UiControl root, int id )
		=> root.Id == id ? root : root.Children.Select( child => FindOrNull( child, id ) ).FirstOrDefault( found => found != null );
}
