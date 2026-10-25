using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// The camera goes to a thing and the screen closes: a right click on a row of the all-staff, visitors and all-items
/// lists, and a click on an object window's preview (<c>FUN_004867b0</c>; <c>docs/exe/park-engine.md</c>, "The camera
/// goes to a thing"). Reads the shipped park; skipped without the game (<see cref="GameData"/>).
/// </summary>
[TestClass]
public class ParkCameraToThingTests
{
	private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

	private Level? _level;
	private Point2 _screen;
	private float _now;
	private Vector3 _looksAt;
	private float _yaw;
	private float _zoom;
	private ParkState _state = null!;
	private ParkPeople _people = null!;
	private WindowStack _stack = null!;

	[TestInitialize]
	public void LoadThePark()
	{
		_level = Level.Current;
		_screen = Screen.Size;
		_now = Time.Now;
		_looksAt = ParkOrbitCameraMode.PointOfInterest;
		_yaw = ParkOrbitCameraMode.Yaw;
		_zoom = ParkOrbitCameraMode.Zoom;

		// The interface's own 2048x1536, so a point in the window is the same point on a screen's layout.
		Screen.Size = new Point2( 2048, 1536 );
		Log ??= new();
		FileSystem = GameData.Required();
		Input.Mouse = new();

		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );

		_state = new ParkState( world );

		var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, world );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, _state );
		typeof( Level ).GetProperty( nameof( Level.Catalogue ) )!.SetValue( level, catalogue );
		Level.Current = level;

		_people = new ParkPeople( world, new ParkBalance( "jungle", easyMode: true ), null, _state, catalogue );
		_stack = new WindowStack();

		ParkOrbitCameraMode.PointOfInterest = new Vector3( 300f, 300f, 0f );
		ParkOrbitCameraMode.Yaw = 1.25f;
		ParkOrbitCameraMode.Zoom = 95f;
	}

	/// <summary>
	/// <b>An object window fills its figures again every four seconds of real time</b>, as a control's timer runs
	/// (<c>0x004af67b</c>, <c>FUN_00661fe5</c>): ten seconds on the frame clock fill nothing, 3999 ms of real time
	/// nothing, 4000 once, and the next not before 8000.
	/// </summary>
	/// <remarks><b>Mutations:</b> the refill timed on <c>Time.Now</c>; a period of 3999 or 4001.</remarks>
	[TestMethod]
	public void AnObjectWindowRefillsEveryFourSecondsOfRealTime()
	{
		var fills = 0;
		Logger.LogDelegate count = ( _, text ) => fills += text.Contains( "Ride window: thing 13 stats" ) ? 1 : 0;

		Time.PinWall( 50000 );
		var window = new ParkObjectWindow( _stack, 13 );
		Logger.OnLog += count;

		try
		{
			Time.Now += 10f;
			Time.PinWall( 53999 );
			window.Update();
			Assert.AreEqual( 0, fills, "3999 ms of real time, ten seconds on the frame clock" );

			Time.PinWall( 54000 );
			window.Update();
			Assert.AreEqual( 1, fills, "4000 ms" );

			Time.PinWall( 57999 );
			window.Update();
			Assert.AreEqual( 1, fills, "3999 ms after that" );

			Time.PinWall( 58000 );
			window.Update();
			Assert.AreEqual( 2, fills, "and 4000" );
		}
		finally
		{
			Logger.OnLog -= count;
		}
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
		ParkOrbitCameraMode.Yaw = _yaw;
		ParkOrbitCameraMode.Zoom = _zoom;
	}

	private static readonly Vector3 Start = new( 300f, 300f, 0f );

	/// <summary>The corner of a cell in the camera's units, ten to a cell.</summary>
	private static Vector3 Corner( (int X, int Y) cell ) => new( cell.X * 10f, cell.Y * 10f, 0f );

	private static UiControl Find( UiControl root, int id )
		=> root.Id == id ? root : root.Children.Select( child => FindOrNull( child, id ) ).First( found => found != null )!;

	private static UiControl? FindOrNull( UiControl root, int id )
		=> root.Id == id ? root : root.Children.Select( child => FindOrNull( child, id ) ).FirstOrDefault( found => found != null );

	/// <summary>The middle of a list's row, by its place on the screen, in window pixels.</summary>
	private static Vector2 RowMiddle( UiList list, int slot )
		=> new( (list.RowArea.Left + list.RowArea.Right) / 2f, list.RowArea.Top + (slot * list.RowHeight) + (list.RowHeight / 2f) );

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	/// <summary>One frame of the game: the right button and the pointer at a time, then the stack's update.</summary>
	private void Frame( bool right, Vector2 at, float now )
	{
		Time.PinWall( (long)System.MathF.Round( now * 1000f ) );
		Input.Mouse = new() { Right = right, RightWentDown = right && !Input.Mouse.Right, Position = at };
		_stack.Update();
	}

	/// <summary>
	/// <b>The cell is the whole part of the thing's position</b>, the original's bytes <c>+5</c> and <c>+7</c>: a placed
	/// object's own origin, a person's where they walk. Nought and an id nothing has give none.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the object's entry cell for its own; a person's cell rounded; the columns and rows exchanged;
	/// nought answered with a cell.
	/// </remarks>
	[TestMethod]
	public void AThingsCellIsTheWholePartOfItsPosition()
	{
		// The Belly Bounce, thing 13: the save's position 0x3300, 0x1700 in 256ths of a cell.
		var ride = _state.Objects.Single( placed => placed.ThingId == 13 );
		Assert.AreEqual( (51, 23), (ride.RawX >> 8, ride.RawY >> 8) );
		Assert.AreEqual( (51, 23), ParkOrbitCameraMode.CellOfThing( 13, _people, _state ) );

		var member = _people.Staff[0];
		member.Navigator.Position = new FixedVector( (40 << 16) + 0xe000, (28 << 16) + 0x9000 );
		Assert.AreEqual( (40, 28), ParkOrbitCameraMode.CellOfThing( member.ThingId, _people, _state ), "a member of staff, truncated" );

		var id = _people.Admit( 42, 5 );
		var guest = _people.Guests[id];
		guest.Navigator.Position = new FixedVector( (47 << 16) + 0xffff, (9 << 16) + 1 );
		Assert.AreEqual( (47, 9), ParkOrbitCameraMode.CellOfThing( id, _people, _state ), "a guest, truncated" );

		Assert.IsNull( ParkOrbitCameraMode.CellOfThing( 0, _people, _state ), "nought is no thing" );
		Assert.IsNull( ParkOrbitCameraMode.CellOfThing( 9999, _people, _state ), "an id nothing has" );
		Assert.IsNull( ParkOrbitCameraMode.CellOfThing( 13, null, null ), "no park" );
	}

	/// <summary>
	/// <b>The point of interest goes to the cell's corner</b>, ten times its column and row, and the spin and the zoom
	/// stay (<c>FUN_004867b0</c> hands <c>FUN_0042aab0</c> nothing else).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the cell's middle (plus 5); the thing's own position, not the corner; the spin or the zoom
	/// reset; an unknown thing moving the camera; the pan's momentum left.
	/// </remarks>
	[TestMethod]
	public void TheCameraGoesToTheCornerOfTheThingsCell()
	{
		Assert.IsFalse( ParkOrbitCameraMode.GoToThing( 9999 ) );
		Assert.IsFalse( ParkOrbitCameraMode.GoToThing( 0 ) );
		Assert.AreEqual( Start, ParkOrbitCameraMode.PointOfInterest, "an id nothing has moves nothing" );

		ParkOrbitCameraMode.Scroll = new Vector2( 60f, -40f );
		Assert.IsTrue( ParkOrbitCameraMode.GoToThing( 13 ) );
		Assert.AreEqual( new Vector3( 510f, 230f, 0f ), ParkOrbitCameraMode.PointOfInterest );
		Assert.AreEqual( Vector2.Zero, ParkOrbitCameraMode.Scroll, "and the pan's momentum is noughted (FUN_0042aab0)" );
		Assert.AreEqual( 1.25f, ParkOrbitCameraMode.Yaw, "the spin is left" );
		Assert.AreEqual( 95f, ParkOrbitCameraMode.Zoom, "and the zoom" );
	}

	/// <summary>
	/// <b>A right click on a visitor's row takes the camera to that guest and closes the screen</b> - through the
	/// stack, so by the click's own point: the press's, kept while the button is down.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the row not wired; the screen left open; acting on the press, before the release; the click
	/// handed the release's point or nought for the press's; a press held past the limit taken for a click.
	/// </remarks>
	[TestMethod]
	public void ARightClickOnAVisitorsRowGoesToThatGuestAndClosesTheScreen()
	{
		for ( var n = 0; n < 4; ++n )
			Assert.AreNotEqual( 0, _people.Admit( 42 + n, 5 + n ) );

		var screen = new ParkVisitorsScreen( _stack );
		_stack.Open( screen );

		var list = (UiList)typeof( ParkVisitorsScreen ).GetField( "_list", Private )!.GetValue( screen )!;
		Assert.IsTrue( list.Rows.Count >= 4 );

		var third = list.Rows[2].Id;
		var cell = _people.Guests[third].Navigator.Position.Cell;
		Assert.AreNotEqual( Start, Corner( cell ) );
		Assert.AreNotEqual( Corner( _people.Guests[list.Rows[0].Id].Navigator.Position.Cell ), Corner( cell ), "rows one and three stand on different cells" );

		// Held past the 500 ms limit: no click.
		Frame( true, RowMiddle( list, 2 ), 10.0f );
		Frame( false, RowMiddle( list, 2 ), 10.8f );
		Assert.AreEqual( Start, ParkOrbitCameraMode.PointOfInterest, "a slow press is no click" );
		Assert.IsTrue( _stack.Windows.Contains( screen ) );

		Frame( true, RowMiddle( list, 2 ), 20.00f );
		Assert.AreEqual( Start, ParkOrbitCameraMode.PointOfInterest, "the press alone moves nothing" );
		Assert.IsTrue( _stack.Windows.Contains( screen ) );

		// Let go over row one: the click is the press's point, row three's.
		Frame( false, RowMiddle( list, 0 ), 20.08f );
		Assert.AreEqual( Corner( cell ), ParkOrbitCameraMode.PointOfInterest, "the camera on the third row's guest" );
		Assert.AreEqual( 1.25f, ParkOrbitCameraMode.Yaw );
		Assert.IsFalse( _stack.Windows.Contains( screen ), "and the screen closed" );
	}

	/// <remarks><b>Mutations:</b> the staff list not wired; its screen left open.</remarks>
	[TestMethod]
	public void ARightClickOnAStaffRowGoesToThatMemberAndClosesTheScreen()
	{
		var screen = new ParkStaffScreen( _stack );
		_stack.Open( screen );

		var list = (UiList)typeof( ParkStaffScreen ).GetField( "_list", Private )!.GetValue( screen )!;
		Assert.IsTrue( list.Rows.Count >= 1, "the tab lists someone" );

		var member = _people.Staff.Single( each => each.ThingId == list.Rows[0].Id );
		var at = RowMiddle( list, 0 );

		list.RightClickedAt( at.X, at.Y );

		Assert.AreEqual( Corner( member.Navigator.Position.Cell ), ParkOrbitCameraMode.PointOfInterest );
		Assert.IsFalse( _stack.Windows.Contains( screen ) );
	}

	/// <summary>
	/// <b>Each all-staff tab picks its first row as it fills</b>, the row sorted first (<c>FUN_0066403b</c>'s
	/// <c>FUN_0066525c( 0 )</c>; the handler <c>FUN_00495da0</c> has no <c>0x401</c> arm), keeps the index over a
	/// heading's re-sort, and a right click that misses every row goes to that row's member.
	/// </summary>
	/// <remarks><b>Mutations:</b> no pick; the pick on the row added first; the pick dropped by a re-sort; the miss counted.</remarks>
	[TestMethod]
	public void EachStaffTabPicksItsFirstRowAndARightClickMissingEveryRowGoesThere()
	{
		var picks = Times( "LIST_FIRST_ROW_SELECTED" );
		var misses = Times( "LIST_RIGHT_CLICK_FIRST_ROW_NOT_SELECTED" );
		var sort = typeof( ParkStaffScreen ).GetField( "_sort", BindingFlags.Static | BindingFlags.NonPublic )!;
		var kinds = typeof( ParkStaffScreen ).GetField( "_lastKind", BindingFlags.Static | BindingFlags.NonPublic )!;
		var lastKind = kinds.GetValue( null );

		try
		{
			// Two mechanics hired in falling name order: each goes in at the staff list's head, so Zed is added last.
			Assert.AreNotEqual( 0, _people.Hire( new ParkStaffPool.Candidate( Id: 998, Kind: 1, Name: "Ada Test", Grade: 0, Costume: 0, Wage: 69 ), 47, 21 ) );
			Assert.AreNotEqual( 0, _people.Hire( new ParkStaffPool.Candidate( Id: 999, Kind: 1, Name: "Zed Test", Grade: 0, Costume: 0, Wage: 69 ), 55, 30 ) );
			kinds.SetValue( null, 1 );

			var screen = new ParkStaffScreen( _stack );
			_stack.Open( screen );
			var list = (UiList)typeof( ParkStaffScreen ).GetField( "_list", Private )!.GetValue( screen )!;
			var show = typeof( ParkStaffScreen ).GetMethod( "Show", Private )!;

			Assert.IsTrue( list.Rows.Count >= 2, "two mechanics at least" );
			Assert.AreEqual( "Ada Test", list.Rows[0].Name, "by name rising" );
			Assert.AreEqual( list.Rows[0].Id, list.Selected, "the row sorted first, not the one added first" );

			for ( var kind = 0; kind < 5; ++kind )
			{
				show.Invoke( screen, [kind] );
				Assert.AreEqual( list.Rows.Count > 0 ? list.Rows[0].Id : -1, list.Selected, $"tab {kind}: the row sorted first" );
			}

			show.Invoke( screen, [1] );

			// The name again, falling: Zed goes to the top, and the index kept names him.
			((UiButton)list.Children.Single( child => child.Id == 0x10 )).Clicked!();
			Assert.AreEqual( "Zed Test", list.Rows[0].Name );
			Assert.AreEqual( list.Rows[0].Id, list.Selected, "the index kept: the new top row" );

			Assert.AreEqual( picks, Times( "LIST_FIRST_ROW_SELECTED" ), "no tab counts the pick" );

			var zed = _people.Staff.Single( each => each.ThingId == list.Rows[0].Id );
			var below = RowMiddle( list, list.Rows.Count + 1 );
			list.RightClickedAt( below.X, below.Y );

			Assert.AreEqual( Corner( zed.Navigator.Position.Cell ), ParkOrbitCameraMode.PointOfInterest, "the miss goes to the picked row's member" );
			Assert.IsFalse( _stack.Windows.Contains( screen ) );
			Assert.AreEqual( misses, Times( "LIST_RIGHT_CLICK_FIRST_ROW_NOT_SELECTED" ), "not counted" );
		}
		finally
		{
			sort.SetValue( null, 1 );
			kinds.SetValue( null, lastKind );
			ParkOrbitCameraMode.PointOfInterest = Start;
		}
	}

	/// <summary>
	/// The all-items lists: a ride's row goes to the ride, and so does a miscellaneous row to its object. The
	/// miscellaneous tab holds a row a placed object, by name, each with its item's standing count (the adder
	/// <c>0x004941e0</c>), sorted on the name as the tab's word <c>[0x00750d68]</c> starts: the stock park's eight.
	/// </summary>
	/// <remarks><b>Mutations:</b> the items lists not wired; a row a kind; the count 1 a row; no sort word handed over.</remarks>
	[TestMethod]
	public void ARightClickOnARidesRowGoesToTheRideAndAMiscellaneousRowToItsObject()
	{
		var screen = new ParkItemsScreen( _stack );
		_stack.Open( screen );
		typeof( ParkItemsScreen ).GetMethod( "Show", Private )!.Invoke( screen, [0] );

		var lists = (Dictionary<int, UiList>)typeof( ParkItemsScreen ).GetField( "_lists", Private )!.GetValue( screen )!;
		var rides = lists[5];
		var index = rides.Rows.ToList().FindIndex( row => row.Id == 13 );
		Assert.IsTrue( index >= 0, "the Belly Bounce is listed" );

		var at = RowMiddle( rides, index );
		rides.RightClickedAt( at.X, at.Y );

		Assert.AreEqual( new Vector3( 510f, 230f, 0f ), ParkOrbitCameraMode.PointOfInterest );
		Assert.IsFalse( _stack.Windows.Contains( screen ) );

		ParkOrbitCameraMode.PointOfInterest = Start;

		var again = new ParkItemsScreen( _stack );
		_stack.Open( again );
		typeof( ParkItemsScreen ).GetMethod( "Show", Private )!.Invoke( again, [3] );

		var misc = ((Dictionary<int, UiList>)typeof( ParkItemsScreen ).GetField( "_lists", Private )!.GetValue( again )!)[2];

		Assert.AreEqual( "Litter Bin 1, Round Fountain 1, Security Camera 2, Security Camera 2, Small Toilet 3, Small Toilet 3, "
			+ "Small Toilet 3, Staff Room 1", string.Join( ", ", misc.Rows.Select( row => $"{row.Name} {row.Value}" ) ) );
		Assert.AreEqual( 8, misc.Rows.Select( row => row.Id ).Distinct().Count(), "a row a thing" );

		var camera = misc.Rows[2].Id;
		var cell = ParkOrbitCameraMode.CellOfThing( camera, null, _state )!.Value;
		Assert.AreNotEqual( Start, Corner( cell ) );

		at = RowMiddle( misc, 2 );
		misc.RightClickedAt( at.X, at.Y );

		Assert.AreEqual( Corner( cell ), ParkOrbitCameraMode.PointOfInterest, "a miscellaneous row goes to its object" );
		Assert.IsFalse( _stack.Windows.Contains( again ) );
	}

	/// <summary>
	/// <b>The visitors, all-staff and all-items lists pick the row under the moving pointer</b>: each tree's list record
	/// carries flag <c>0x80</c> (<c>0x00750716</c>, <c>0x00750e5e</c>, <c>0x00750ab8</c>, <c>0x00750ba0</c>,
	/// <c>0x00750ca0</c>), so <c>FUN_006656a0</c> selects the row the pointer rests on with no press, and a right click
	/// that then misses every row names it (<c>FUN_0066563d</c>). Through the stack: a frame of the pointer alone on
	/// the last row shown, a frame of it on the strip under the rows, then the right click there.
	/// </summary>
	/// <remarks><b>Mutations:</b> any one of the three lists not following the pointer; a move under a held button picking.</remarks>
	[TestMethod]
	public void TheVisitorsStaffAndItemsListsPickTheRowUnderThePointer()
	{
		for ( var n = 0; n < 4; ++n )
			Assert.AreNotEqual( 0, _people.Admit( 42 + n, 5 + n ) );

		var picks = 0;
		void Heard( Logger.Level severity, string line ) => picks += line.Contains( " picked under the pointer" ) ? 1 : 0;
		Logger.OnLog += Heard;
		object? lastKind = null;

		// One frame of the stack with the pointer's move as the frame's own, as the window's input hands it.
		void Move( bool right, Vector2 at, float now )
		{
			var from = Input.Mouse.Position;
			Time.PinWall( (long)System.MathF.Round( now * 1000f ) );
			Input.Mouse = new() { Right = right, RightWentDown = right && !Input.Mouse.Right, Position = at, Delta = at - from };
			_stack.Update();
		}

		void Hover( UiWindow screen, UiList list, string what, System.Func<int, Vector3> cornerOf, float now )
		{
			ParkOrbitCameraMode.PointOfInterest = Start;
			Assert.IsTrue( list.SelectsUnderPointer, $"{what}: flag 0x80" );
			Assert.IsTrue( list.Rows.Count >= 2, $"{what}: two rows at least" );

			var last = Math.Min( list.Rows.Count, list.VisibleRows ) - 1;
			var id = list.Rows[last].Id;
			Assert.AreNotEqual( Start, cornerOf( id ), what );

			// A press held on the list keeps the pick where it was (0x006656ba).
			Move( false, RowMiddle( list, 0 ), now - 0.1f );
			var before = list.Selected;
			Assert.AreEqual( list.Rows[0].Id, before, $"{what}: the first row under the pointer" );
			Move( true, RowMiddle( list, 0 ), now );
			Move( true, RowMiddle( list, last ), now + 0.6f );
			Assert.AreEqual( before, list.Selected, $"{what}: held" );
			Move( false, RowMiddle( list, last ), now + 0.7f );
			ParkOrbitCameraMode.PointOfInterest = Start;

			Move( false, RowMiddle( list, 0 ) + new Vector2( 1, 1 ), now + 1f );
			Move( false, RowMiddle( list, last ), now + 1.1f );
			Assert.AreEqual( id, list.Selected, $"{what}: the row under the pointer" );

			// The strip at the foot of the row area, under the last whole row.
			var below = new Vector2( RowMiddle( list, 0 ).X, list.RowArea.Bottom - 3 );
			Move( false, below, now + 1.2f );
			Assert.AreEqual( id, list.Selected, $"{what}: a point over no row leaves it" );

			Move( true, below, now + 1.3f );
			Move( false, below, now + 1.4f );
			Assert.AreEqual( cornerOf( id ), ParkOrbitCameraMode.PointOfInterest, $"{what}: the miss goes to the row under the pointer" );
			Assert.IsFalse( _stack.Windows.Contains( screen ), $"{what}: and the screen closed" );
		}

		try
		{
			var visitors = new ParkVisitorsScreen( _stack );
			_stack.Open( visitors );
			Hover( visitors, (UiList)typeof( ParkVisitorsScreen ).GetField( "_list", Private )!.GetValue( visitors )!, "visitors",
				id => Corner( _people.Guests[id].Navigator.Position.Cell ), 30f );

			// Two more of one kind on two cells, and the kind with the most members shown, so the list has rows to choose between.
			Assert.AreNotEqual( 0, _people.Hire( new ParkStaffPool.Candidate( Id: 998, Kind: 1, Name: "Ada Test", Grade: 0, Costume: 0, Wage: 69 ), 47, 21 ) );
			Assert.AreNotEqual( 0, _people.Hire( new ParkStaffPool.Candidate( Id: 999, Kind: 1, Name: "Bo Test", Grade: 0, Costume: 0, Wage: 69 ), 55, 30 ) );
			var kinds = typeof( ParkStaffScreen ).GetField( "_lastKind", BindingFlags.Static | BindingFlags.NonPublic )!;
			lastKind = kinds.GetValue( null );
			kinds.SetValue( null, _people.Staff.GroupBy( each => ParkStaffPool.KindFor( each.Model ) ).MaxBy( group => group.Count() )!.Key );

			var staff = new ParkStaffScreen( _stack );
			_stack.Open( staff );
			Hover( staff, (UiList)typeof( ParkStaffScreen ).GetField( "_list", Private )!.GetValue( staff )!, "staff",
				id => Corner( _people.Staff.Single( each => each.ThingId == id ).Navigator.Position.Cell ), 40f );

			var items = new ParkItemsScreen( _stack );
			_stack.Open( items );
			typeof( ParkItemsScreen ).GetMethod( "Show", Private )!.Invoke( items, [3] );
			Hover( items, ((Dictionary<int, UiList>)typeof( ParkItemsScreen ).GetField( "_lists", Private )!.GetValue( items )!)[2],
				"misc", id => Corner( ParkOrbitCameraMode.CellOfThing( id, null, _state )!.Value ), 50f );

			// The first row and the last on the visitors; the last alone on the staff and the tab, which picked their first.
			Assert.AreEqual( 4, picks, "one log line a pick" );
		}
		finally
		{
			Logger.OnLog -= Heard;
			if ( lastKind != null )
				typeof( ParkStaffScreen ).GetField( "_lastKind", BindingFlags.Static | BindingFlags.NonPublic )!.SetValue( null, lastKind );
			ParkOrbitCameraMode.PointOfInterest = Start;
		}
	}

	/// <summary>
	/// <b>Each all-items tab picks its first row as it fills</b>, the row sorted first (<c>FUN_0066403b</c>'s
	/// <c>FUN_0066525c( 0 )</c>; the handler <c>FUN_00495360</c> has no <c>0x401</c> arm), keeps the index over a
	/// heading's re-sort, and a right click that misses every row goes to that row's thing.
	/// </summary>
	/// <remarks><b>Mutations:</b> no pick; the pick on the row added first; the pick dropped by a re-sort; the miss counted.</remarks>
	[TestMethod]
	public void EachItemsTabPicksItsFirstRowAndARightClickMissingEveryRowGoesThere()
	{
		var picks = Times( "LIST_FIRST_ROW_SELECTED" );
		var misses = Times( "LIST_RIGHT_CLICK_FIRST_ROW_NOT_SELECTED" );
		var sorts = (int[])typeof( ParkItemsScreen ).GetField( "_sorts", BindingFlags.Static | BindingFlags.NonPublic )!.GetValue( null )!;

		try
		{
			var screen = new ParkItemsScreen( _stack );
			_stack.Open( screen );
			var show = typeof( ParkItemsScreen ).GetMethod( "Show", Private )!;
			var lists = (Dictionary<int, UiList>)typeof( ParkItemsScreen ).GetField( "_lists", Private )!.GetValue( screen )!;

			foreach ( var (tab, shape) in new[] { (0, 5), (1, 5), (2, 6), (3, 2) } )
			{
				show.Invoke( screen, [tab] );
				Assert.IsTrue( lists[shape].Rows.Count > 0, $"tab {tab} has rows" );
				Assert.AreEqual( lists[shape].Rows[0].Id, lists[shape].Selected, $"tab {tab}: the row sorted first" );
			}

			var misc = lists[2];
			Assert.AreEqual( "Litter Bin", misc.Rows[0].Name );
			Assert.AreNotEqual( misc.Rows[0].Id, misc.Rows[^1].Id );

			((UiButton)misc.Children.Single( child => child.Id == 0x10 )).Clicked!();
			Assert.AreEqual( "Staff Room", misc.Rows[0].Name );
			Assert.AreEqual( misc.Rows[0].Id, misc.Selected, "the index kept: the new top row" );

			// Filled again falling: the Litter Bin goes in first and stands last; the pick is the index, the Staff Room.
			show.Invoke( screen, [0] );
			show.Invoke( screen, [3] );
			Assert.AreEqual( "Litter Bin", misc.Rows[^1].Name );
			Assert.AreEqual( "Staff Room", misc.Rows[0].Name );
			Assert.AreEqual( misc.Rows[0].Id, misc.Selected, "the row sorted first, not the one added first" );

			Assert.AreEqual( picks, Times( "LIST_FIRST_ROW_SELECTED" ), "no tab counts the pick" );

			var room = misc.Rows[0].Id;
			var cell = ParkOrbitCameraMode.CellOfThing( room, null, _state )!.Value;
			Assert.AreNotEqual( Start, Corner( cell ) );

			var below = RowMiddle( misc, misc.Rows.Count + 1 );
			misc.RightClickedAt( below.X, below.Y );

			Assert.AreEqual( Corner( cell ), ParkOrbitCameraMode.PointOfInterest, "the miss goes to the picked row's thing" );
			Assert.IsFalse( _stack.Windows.Contains( screen ) );
			Assert.AreEqual( misses, Times( "LIST_RIGHT_CLICK_FIRST_ROW_NOT_SELECTED" ), "not counted" );
		}
		finally
		{
			Array.Fill( sorts, 1 );
			ParkOrbitCameraMode.PointOfInterest = Start;
		}
	}

	/// <summary>
	/// <b>Each all-items tab keeps its own sort for the session</b> (<c>[0x00750d5c]</c> to <c>[0x00750d68]</c>, each 1,
	/// written on <c>0x406</c> by the remembered tab, <c>0x004954eb</c>): the name clicked on the miscellaneous tab
	/// turns it to -1 and the rows about; the rides' tab stays by name rising, and the miscellaneous one opened again
	/// stands falling.
	/// </summary>
	/// <remarks><b>Mutations:</b> one word for every tab; the word not kept; the name sorted as a number.</remarks>
	[TestMethod]
	public void EachItemsTabKeepsItsOwnSort()
	{
		var sorts = (int[])typeof( ParkItemsScreen ).GetField( "_sorts", BindingFlags.Static | BindingFlags.NonPublic )!.GetValue( null )!;

		try
		{
			var screen = new ParkItemsScreen( _stack );
			_stack.Open( screen );
			var show = typeof( ParkItemsScreen ).GetMethod( "Show", Private )!;
			var lists = (Dictionary<int, UiList>)typeof( ParkItemsScreen ).GetField( "_lists", Private )!.GetValue( screen )!;

			static string[] Names( UiList list ) => list.Rows.Select( row => row.Name ).ToArray();

			show.Invoke( screen, [3] );
			Assert.AreEqual( 1, lists[2].SortWord );
			Assert.AreEqual( "Litter Bin", Names( lists[2] )[0] );

			((UiButton)lists[2].Children.Single( child => child.Id == 0x10 )).Clicked!();
			Assert.AreEqual( -1, sorts[3], "the miscellaneous tab's word" );
			Assert.AreEqual( "Staff Room", Names( lists[2] )[0] );

			show.Invoke( screen, [0] );
			var rides = Names( lists[5] );
			Assert.AreEqual( 1, lists[5].SortWord, "the rides' own word" );
			CollectionAssert.AreEqual( rides.OrderBy( name => name, StringComparer.Ordinal ).ToArray(), rides, "by name rising" );

			show.Invoke( screen, [3] );
			Assert.AreEqual( -1, lists[2].SortWord );
			Assert.AreEqual( "Staff Room", Names( lists[2] )[0], "kept" );
		}
		finally
		{
			Array.Fill( sorts, 1 );
		}
	}

	/// <summary>
	/// <b>A left click on a title that the original renames from is counted</b>: an object window's title asks for
	/// the thing's rename box (<c>FUN_0048d2e0</c>) and the entry price screen's heading for the park's
	/// (<c>FUN_00499060</c>). Neither box is built (Q28), so each click is counted, and a press held 600 ms, which is
	/// no click, is not.
	/// </summary>
	/// <remarks><b>Mutations:</b> either title deaf to the click; counted on a release of any length.</remarks>
	[TestMethod]
	public void AClickOnATitleTheOriginalRenamesFromIsCounted()
	{
		foreach ( var (make, id, counted) in new (System.Func<UiWindow> Make, int Id, string Counted)[]
			{ (() => new ParkObjectWindow( _stack, 13 ), 0x3e28, "RENAME_OBJECT"), (() => new ParkEntryPriceScreen( _stack ), 0x4f3af, "RENAME_PARK") } )
		{
			var window = make();
			_stack.Open( window );

			var title = Find( window.Root, id );
			var middle = new Vector2( (title.Rect.Left + title.Rect.Right) / 2f, (title.Rect.Top + title.Rect.Bottom) / 2f );
			Assert.AreSame( title, window.Root.HitTest( middle.X, middle.Y ), $"{id:x} takes the pointer" );

			var before = Times( counted );

			foreach ( var (from, to, clicks) in new[] { (20000L, 20600L, 0), (30000L, 30080L, 1) } )
			{
				Time.PinWall( from );
				Input.Mouse = new() { Left = true, LeftWentDown = true, Position = middle };
				_stack.Update();
				Time.PinWall( to );
				Input.Mouse = new() { Position = middle };
				_stack.Update();

				Assert.AreEqual( before + clicks, Times( counted ), $"{counted} after a press held {to - from} ms" );
			}

			_stack.Close( window );
			Input.Mouse = new();
			Time.PinWall( 40000 );
			_stack.Update();
		}
	}

	/// <summary>
	/// <b>A click on an object window's preview, with either button, goes to the window's thing and closes it</b>
	/// (<c>0x0048d1a0</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the left click not wired; the right click not wired; the window left open.</remarks>
	[TestMethod]
	[DataRow( false )]
	[DataRow( true )]
	public void AClickOnThePreviewGoesToTheThingAndClosesTheWindow( bool right )
	{
		var window = new ParkObjectWindow( _stack, 13 );
		_stack.Open( window );

		var preview = Find( window.Root, 0x3e24 );
		var middle = new Vector2( (preview.Rect.Left + preview.Rect.Right) / 2f, preview.Rect.Top + 40f );
		Assert.AreSame( preview, window.Root.HitTest( middle.X, middle.Y ), "the preview takes the pointer" );

		if ( right )
		{
			Frame( true, middle, 30.00f );
			Frame( false, middle, 30.08f );
		}
		else
		{
			// The left button's is the timed click too (0x10006): held 600 ms, the window stays and the camera with it.
			foreach ( var (from, to) in new[] { (20000L, 20600L), (30000L, 30080L) } )
			{
				Time.PinWall( from );
				Input.Mouse = new() { Left = true, LeftWentDown = true, Position = middle };
				_stack.Update();
				Time.PinWall( to );
				Input.Mouse = new() { Position = middle };
				_stack.Update();

				if ( to - from >= 500 )
				{
					Assert.IsTrue( _stack.Windows.Contains( window ), "a press held 600 ms is no click" );
					Assert.AreEqual( new Vector3( 300f, 300f, 0f ), ParkOrbitCameraMode.PointOfInterest );
				}
			}
		}

		Assert.AreEqual( new Vector3( 510f, 230f, 0f ), ParkOrbitCameraMode.PointOfInterest );
		Assert.IsFalse( _stack.Windows.Contains( window ) );
	}
	/// <summary>
	/// <b>The message bar's mb_locate puts the camera on the tag's thing and leaves the bar out</b>
	/// (<c>FUN_0048e0f0</c>'s case 3, <c>FUN_004867b0</c>), and the bar's meshes are the wad's own.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the button not wired; the item, not the thing; the arm folded away by it; a slice's or a
	/// button's mesh misnamed.
	/// </remarks>
	[TestMethod]
	public void TheBarsLocateButtonGoesToTheTagsThing()
	{
		var gadget = new ParkGadget( _stack );
		_stack.Open( gadget );

		var tags = gadget.Tags = new ParkTags();
		tags.Said( 72, 13, 1000, 1100, "Belly Bounce" );
		Frame( false, new Vector2( 1000, 100 ), 1.5f );

		Assert.IsTrue( _stack.ClickAt( 119, 941 ) );
		ParkGadgetPressTests.Settle( gadget );
		Assert.IsTrue( gadget.BarOut );

		// The bar's meshes are files of ui.wad: its slices, its erase button and every button the table dresses.
		Assert.AreEqual( ("mb_locate", 516), ParkGadget.BarButtonLook( 3 ) );
		Assert.AreEqual( ("mb_locate", 515), ParkGadget.BarButtonLook( 4 ) );
		Assert.IsNull( ParkGadget.BarButtonLook( 0 ) );
		Assert.AreEqual( ("mb_accept", -1), ParkGadget.BarButtonLook( 0x1a ) );
		Assert.AreEqual( ("mb_decline", -1), ParkGadget.BarButtonLook( 0x1b ) );
		Assert.IsNull( ParkGadget.BarButtonLook( 0x1c ) );
		var meshes = new[] { ParkGadget.BarLeftMesh, ParkGadget.BarMiddleMesh, ParkGadget.BarRightMesh, ParkGadget.BarEraseMesh }
			.Concat( Enumerable.Range( 0, 0x20 ).Select( type => ParkGadget.BarButtonLook( type )?.Mesh ).OfType<string>() ).Distinct().ToArray();
		Assert.AreEqual( 12, meshes.Length );
		foreach ( var mesh in meshes )
			Assert.IsTrue( new ModelFile( $"ui/{mesh}.md2" ).Meshes.Any(), mesh );

		Assert.AreEqual( Start, ParkOrbitCameraMode.PointOfInterest );
		Assert.IsTrue( _stack.ClickAt( 565, 1172 ) );
		Assert.AreEqual( new Vector3( 510f, 230f, 0f ), ParkOrbitCameraMode.PointOfInterest, "the Belly Bounce's cell's corner, not item 1100's anything" );
		Assert.AreEqual( (true, 1), (gadget.BarOut, tags.Standing.Count), "and the bar stays" );

		static UiControl Find( UiControl control, int id )
			=> control.Id == id ? control : control.Children.Select( child => Find( child, id ) ).FirstOrDefault( found => found != null )!;
	}
}
