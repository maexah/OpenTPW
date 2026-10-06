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

	[TestCleanup]
	public void PutItAllBack()
	{
		_people?.Delete();
		Entity.ApplyDeletions();
		Level.Current = _level!;
		Screen.Size = _screen;
		Time.Now = _now;
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
		Time.Now = now;
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
	/// reset; an unknown thing moving the camera.
	/// </remarks>
	[TestMethod]
	public void TheCameraGoesToTheCornerOfTheThingsCell()
	{
		Assert.IsFalse( ParkOrbitCameraMode.GoToThing( 9999 ) );
		Assert.IsFalse( ParkOrbitCameraMode.GoToThing( 0 ) );
		Assert.AreEqual( Start, ParkOrbitCameraMode.PointOfInterest, "an id nothing has moves nothing" );

		Assert.IsTrue( ParkOrbitCameraMode.GoToThing( 13 ) );
		Assert.AreEqual( new Vector3( 510f, 230f, 0f ), ParkOrbitCameraMode.PointOfInterest );
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
	/// The all-items lists: a ride's row goes to the ride; the miscellaneous tab's rows are item types here, so its
	/// right click is counted and the screen stays.
	/// </summary>
	/// <remarks><b>Mutations:</b> the items lists not wired; the miscellaneous tab's row id taken for a thing.</remarks>
	[TestMethod]
	public void ARightClickOnARidesRowGoesToTheRideAndTheMiscellaneousTabIsCounted()
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
		Assert.IsTrue( misc.Rows.Count >= 1, "the park owns a miscellaneous item" );

		var before = Times( "ALL_ITEMS_MISC_ROW_RIGHT_CLICK" );
		at = RowMiddle( misc, 0 );
		misc.RightClickedAt( at.X, at.Y );

		Assert.AreEqual( before + 1, Times( "ALL_ITEMS_MISC_ROW_RIGHT_CLICK" ) );
		Assert.AreEqual( Start, ParkOrbitCameraMode.PointOfInterest, "a type is nowhere to go" );
		Assert.IsTrue( _stack.Windows.Contains( again ) );
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
			typeof( WindowStack ).GetField( "_pressed", Private )!.SetValue( _stack, preview );
			typeof( WindowStack ).GetMethod( "Release", Private )!.Invoke( _stack, [preview] );
		}

		Assert.AreEqual( new Vector3( 510f, 230f, 0f ), ParkOrbitCameraMode.PointOfInterest );
		Assert.IsFalse( _stack.Windows.Contains( window ) );
	}
}
