using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Mark = OpenTPW.UI.ParkFootprintPicture.Mark;
using Square = OpenTPW.UI.ParkFootprintPicture.Square;

namespace OpenTPW.Tests;

/// <summary>
/// The buy screen's footprint picture (<c>docs/exe/park-engine.md</c>, "The buy screen's footprint picture"): the
/// grid <c>FUN_0052c5b0</c> makes of a shape, the squares <c>FUN_004ab1b0</c> paints, the half second a row waits and
/// the list's selection under a moving pointer. The expected numbers are the executable's arithmetic and the
/// original's grid as read from its memory, not this code's output.
/// </summary>
[TestClass]
public class ParkFootprintPictureTests
{
	private Level? _level;
	private Point2 _screen;
	private float _now;

	[TestInitialize]
	public void Keep()
	{
		_level = Level.Current;
		_screen = Screen.Size;
		_now = Time.Now;
		Log ??= new();
		Input.Mouse = new();
	}

	[TestCleanup]
	public void PutBack()
	{
		Level.Current = _level!;
		Screen.Size = _screen;
		Time.Now = _now;
		Time.PinWall( 0 );
		Input.Mouse = new();
	}

	/// <summary>A shape from its picture as the file draws it, top line first.</summary>
	private static ItemShape Shape( params int[][] lines )
		=> new( lines.Max( line => line.Length ), lines.Length, lines.Reverse().ToArray() );

	/// <remarks>
	/// <b>Mutations:</b> kind 1 a body; <c>0x17</c> nothing; the entrance and the exit exchanged; kind 0x0b painted; the
	/// grid indexed a row then a column; the rows not turned over.
	/// </remarks>
	[TestMethod]
	public void EachKindBecomesItsCode()
	{
		// Top line: body, coaster body, path, lava (+). Bottom line: entrance, exit, nothing (.), queue (Q).
		var grid = ParkFootprintPicture.Grid( Shape( [4, 0x17, 1, 0x0b], [9, 10, 0, 3] ) );

		CollectionAssert.AreEqual( new[] { Mark.Entrance, Mark.Exit, Mark.None, Mark.None },
			Enumerable.Range( 0, 4 ).Select( column => grid[column, 0] ).ToArray(), "row nought is the picture's last line" );
		CollectionAssert.AreEqual( new[] { Mark.Body, Mark.Body, Mark.Path, Mark.None },
			Enumerable.Range( 0, 4 ).Select( column => grid[column, 1] ).ToArray() );
		Assert.AreEqual( Mark.None, grid[4, 0] );
		Assert.AreEqual( Mark.None, grid[0, 2] );
	}

	/// <summary>
	/// The Aztec Mayhem on the original's 640 by 480 screen, where the control is 48 pixels each way: a cell 6 by 6,
	/// the block 24 wide from the left edge, its lowest painted row of pixels the surface's last but one.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the cell a sixteenth; row nought at the top; the lowest row of pixels painted; the columns and
	/// rows exchanged; a nothing cell painted; the width's cell used down.
	/// </remarks>
	[TestMethod]
	public void TheSquaresGrowFromTheLowerLeftAnEighthOfTheControlEach()
	{
		var aztec = Shape( [4, 4, 4, 4], [4, 4, 4, 4], [4, 4, 4, 4], [4, 9, 10, 4] );
		var squares = ParkFootprintPicture.Squares( aztec, 48, 48 );

		Assert.AreEqual( 16, squares.Count );
		Assert.AreEqual( new Square( 0, 41, 6, 47, Mark.Body ), squares.Single( square => square.Left == 0 && square.Bottom == 47 ) );
		Assert.AreEqual( new Square( 6, 41, 12, 47, Mark.Entrance ), squares.Single( square => square.Mark == Mark.Entrance ) );
		Assert.AreEqual( new Square( 12, 41, 18, 47, Mark.Exit ), squares.Single( square => square.Mark == Mark.Exit ) );
		Assert.AreEqual( 24, squares.Max( square => square.Right ) );
		Assert.AreEqual( 23, squares.Min( square => square.Top ) );
		Assert.AreEqual( 47, squares.Max( square => square.Bottom ), "the surface's last row of pixels, 47, is not painted" );

		// A surface that is not square, and not a multiple of eight: 77 by 50 gives a cell 9 by 6.
		var staffRoom = Shape( [4, 4], [4, 9] );
		var uneven = ParkFootprintPicture.Squares( staffRoom, 77, 50 );
		Assert.AreEqual( 4, uneven.Count );
		Assert.AreEqual( new Square( 9, 43, 18, 49, Mark.Entrance ), uneven.Single( square => square.Mark == Mark.Entrance ) );
		Assert.AreEqual( new Square( 0, 37, 9, 43, Mark.Body ), uneven.Single( square => square.Left == 0 && square.Top == 37 ) );

		// Lost Kingdom's lavajump is all '+': nothing is painted. And no shape, nothing.
		Assert.AreEqual( 0, ParkFootprintPicture.Squares( Shape( [0x0b, 0x0b], [0x0b, 0x0b] ), 48, 48 ).Count );
		Assert.AreEqual( 0, ParkFootprintPicture.Squares( null, 48, 48 ).Count );
	}

	/// <remarks><b>Mutations:</b> the entrance brown; the path its own colour; the strength a half.</remarks>
	[TestMethod]
	public void TheColoursAreTheFillsAndTheStrengthTheOneMeasured()
	{
		Assert.AreEqual( new UiColour( 0x1e, 0xaa, 0xff ), ParkFootprintPicture.ColourOf( Mark.Body ) );
		Assert.AreEqual( new UiColour( 0x1e, 0xaa, 0xff ), ParkFootprintPicture.ColourOf( Mark.Path ) );
		Assert.AreEqual( new UiColour( 0x0f, 0xdc, 0x32 ), ParkFootprintPicture.ColourOf( Mark.Entrance ) );
		Assert.AreEqual( new UiColour( 0xdc, 0x64, 0x0f ), ParkFootprintPicture.ColourOf( Mark.Exit ) );

		// The original's frame over black: green's 0xdc reads 117, blue's 0xff 140, on a 16-bit screen. A half gives 110 and 128.
		Assert.AreEqual( 117, (int)(0xdc * ParkFootprintPicture.Strength) );
		Assert.AreEqual( 136, (int)(0xff * ParkFootprintPicture.Strength) );
	}

	/// <summary>
	/// The control's surface is its rectangle in whole pixels: 48 by 48 on a 640 by 480 screen and 77 by 77 on a
	/// 1024 by 768 one, as the original sizes it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the surface rounded up; the rectangle's size in virtual units.</remarks>
	[TestMethod]
	public void TheSurfaceIsTheControlInWholePixels()
	{
		var picture = new ParkFootprintPicture { Rect = new UiRect( 440, 407, 594, 561 ) };

		Screen.Size = new Point2( 640, 480 );
		Assert.AreEqual( (137, 127, 48, 48), picture.Surface );

		Screen.Size = new Point2( 1024, 768 );
		Assert.AreEqual( (220, 203, 77, 77), picture.Surface );
	}

	/// <summary>
	/// The moving pointer selects the row under it on a list that carries flag <c>0x80</c>, and on no other; over no
	/// row the selection stays.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the flag not read; a move over no row clearing the selection; the selection told again for the
	/// row already selected; the move activating the row.
	/// </remarks>
	[TestMethod]
	public void AMovingPointerSelectsTheRowUnderItOnlyWhereTheListSaysSo()
	{
		Screen.Size = new Point2( 2048, 1536 );

		foreach ( var flagged in new[] { true, false } )
		{
			var told = new System.Collections.Generic.List<int>();
			var activated = 0;
			var list = new UiList
			{
				RowArea = new UiRect( 0, 0, 100, 440 ),
				Columns = [(0, 100)],
				SelectsUnderPointer = flagged,
				SelectionChanged = told.Add,
				Activated = _ => ++activated
			};

			list.Build();

			for ( var n = 0; n < 3; ++n )
				list.Add( new UiList.Row( 100 + n, $"{n}", n ) );

			list.PointerMoved( 50, 44 + 22 );
			list.PointerMoved( 50, 44 + 30 );
			list.PointerMoved( 50, (5 * 44) + 22 );

			Assert.AreEqual( flagged ? 101 : -1, list.Selected );
			CollectionAssert.AreEqual( flagged ? new[] { 101 } : System.Array.Empty<int>(), told );
			Assert.AreEqual( 0, activated );
		}
	}

	private sealed class Heard : UiWindow
	{
		public int Moves;

		public Heard( WindowStack stack ) : base( stack )
			=> Root = new Listener { Rect = new UiRect( 0, 0, 400, 400 ), StopsPointer = true, Owner = this };

		private sealed class Listener : UiControl
		{
			public Heard Owner = null!;

			internal override void PointerMoved( float x, float y ) => ++Owner.Moves;
		}
	}

	/// <remarks><b>Mutations:</b> the move sent every frame; never sent; sent with the pointer over no control.</remarks>
	[TestMethod]
	public void TheStackSendsAMoveOnlyOnAFrameThePointerMovedOverAControl()
	{
		Screen.Size = new Point2( 2048, 1536 );

		var stack = new WindowStack();
		var window = new Heard( stack );
		stack.Open( window );

		Input.Mouse = new() { Position = new Vector2( 100, 100 ), Delta = new Vector2( 3, 0 ) };
		stack.Update();
		Assert.AreEqual( 1, window.Moves );

		Input.Mouse = new() { Position = new Vector2( 100, 100 ) };
		stack.Update();
		Assert.AreEqual( 1, window.Moves, "a pointer at rest sends nothing" );

		Input.Mouse = new() { Position = new Vector2( 900, 900 ), Delta = new Vector2( 5, 5 ) };
		stack.Update();
		Assert.AreEqual( 1, window.Moves, "nor one moving over no control" );
	}

	/// <summary>Lost Kingdom's catalogue and research around the buy screen, as the game has them.</summary>
	private static (ParkItemCatalogue Catalogue, ParkBuyScreen Screen, WindowStack Stack) BuyScreen()
	{
		Screen.Size = new Point2( 2048, 1536 );
		FileSystem = GameData.Required();

		using var stream = new MemoryStream( FileSystem.ReadAllBytes( "levels/jungle/Easymode.TPWI" ) );
		var world = new ParkWorld( new SaveReader( stream ).ReadFile() );
		var catalogue = new ParkItemCatalogue( "jungle", FileSystem );

		var level = (Level)RuntimeHelpers.GetUninitializedObject( typeof( Level ) );
		typeof( Level ).GetProperty( nameof( Level.Park ) )!.SetValue( level, world );
		typeof( Level ).GetProperty( nameof( Level.ParkState ) )!.SetValue( level, new ParkState( world ) );
		typeof( Level ).GetProperty( nameof( Level.Catalogue ) )!.SetValue( level, catalogue );
		typeof( Level ).GetProperty( nameof( Level.Research ) )!.SetValue( level, new ParkResearch( world.ObjectControlRecords, catalogue ) );
		Level.Current = level;

		var stack = new WindowStack();
		var screen = new ParkBuyScreen( stack );
		stack.Open( screen );

		return (catalogue, screen, stack);
	}

	private static string Row( ItemShape shape, int row )
		=> string.Join( " ", Enumerable.Range( 0, shape.Width ).Select( column => (int)ParkFootprintPicture.Grid( shape )[column, row] ) );

	/// <summary>
	/// The grids the original's memory held for five rows of its buy list (Q233): the Aztec Mayhem, the Crazy Ape, the
	/// Belly Bounce, the Balloon Shop and the Staff Room. And no shape in Lost Kingdom's catalogue passes eight cells.
	/// </summary>
	/// <remarks><b>Mutations:</b> the shape not carried to the item; its rows not turned over; the footprint override's box for the picture's.</remarks>
	[TestMethod]
	public void TheShippedShapesGiveTheGridsTheOriginalHeld()
	{
		var (catalogue, _, _) = BuyScreen();

		ItemShape Of( string name ) => catalogue.All.Single( item => item.Name == name ).Shape!;

		var aztec = Of( "Aztec Mayhem" );
		Assert.AreEqual( (4, 4), (aztec.Width, aztec.Depth) );
		Assert.AreEqual( "1 2 3 1", Row( aztec, 0 ) );
		Assert.AreEqual( "1 1 1 1", Row( aztec, 3 ) );

		var ape = Of( "Crazy Ape" );
		Assert.AreEqual( (4, 4), (ape.Width, ape.Depth) );
		Assert.AreEqual( "1 2 1 1", Row( ape, 0 ) );
		Assert.AreEqual( "1 1 3 1", Row( ape, 3 ) );

		var bounce = Of( "Belly Bounce" );
		Assert.AreEqual( (3, 4), (bounce.Width, bounce.Depth) );
		Assert.AreEqual( "1 2 1", Row( bounce, 0 ) );
		Assert.AreEqual( "1 3 1", Row( bounce, 3 ) );

		var balloons = Of( "Balloon Shop" );
		Assert.AreEqual( (3, 3), (balloons.Width, balloons.Depth) );
		Assert.AreEqual( "1 2 1", Row( balloons, 0 ) );

		var staffRoom = Of( "Staff Room" );
		Assert.AreEqual( (2, 2), (staffRoom.Width, staffRoom.Depth) );
		Assert.AreEqual( "1 2", Row( staffRoom, 0 ) );
		Assert.AreEqual( "1 1", Row( staffRoom, 1 ) );

		foreach ( var item in catalogue.All.Where( item => item.Shape != null ) )
			Assert.IsTrue( item.Shape!.Width <= ParkFootprintPicture.CellsAcross && item.Shape.Depth <= ParkFootprintPicture.CellsAcross, item.Name );
	}

	/// <summary>
	/// <b>The row's wait is real time, not the frame clock's</b> (<c>FUN_0065968e</c> at <c>0x004ac438</c>): five seconds
	/// on the frame clock show nothing, and 501 ms of real time with the frame clock still show the row.
	/// </summary>
	/// <remarks><b>Mutations:</b> the wait stamped or measured on <c>Time.Now</c>.</remarks>
	[TestMethod]
	public void TheRowsWaitIsTimedInRealTime()
	{
		var (catalogue, screen, _) = BuyScreen();
		var aztec = catalogue.All.Single( item => item.Name == "Aztec Mayhem" );

		Time.PinWall( 40000 );
		screen.RowSelected( aztec.Id );
		Time.Now += 5f;
		screen.Update();
		Assert.IsNull( screen.Footprint.Shape, "five seconds on the frame clock, none of real time: not shown" );

		Time.PinWall( 40501 );
		screen.Update();
		Assert.IsNotNull( screen.Footprint.Shape, "501 ms of real time: shown" );
	}

	/// <summary>
	/// A row is shown once it has waited more than 500 ms, not at 500; a second row inside the wait takes its place and
	/// starts the wait again; the row waiting again does not; a land row clears the picture, and a mystery ride.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> shown at once; at exactly 500 ms; the wait not started again by another row; started again by
	/// the same row; the land row keeping the picture before; a mystery ride's shape painted; the row shown again every
	/// frame after its wait.
	/// </remarks>
	[TestMethod]
	public void ARowIsShownOnceItHasWaitedMoreThanHalfASecond()
	{
		var (catalogue, screen, _) = BuyScreen();
		var aztec = catalogue.All.Single( item => item.Name == "Aztec Mayhem" );
		var staffRoom = catalogue.All.Single( item => item.Name == "Staff Room" );

		Time.PinWall( 16000 );
		screen.RowSelected( aztec.Id );
		screen.Update();
		Assert.IsNull( screen.Footprint.Shape );

		Time.PinWall( 16500 );
		screen.Update();
		Assert.IsNull( screen.Footprint.Shape, "500 ms is not more than 500" );

		// The same row again does not start the wait again; another does.
		screen.RowSelected( aztec.Id );
		Time.PinWall( 16625 );
		screen.RowSelected( staffRoom.Id );
		screen.Update();
		Assert.IsNull( screen.Footprint.Shape, "the Staff Room took the Aztec Mayhem's place before it was shown" );

		Time.PinWall( 17000 );
		screen.Update();
		Assert.IsNull( screen.Footprint.Shape, "375 ms" );

		Time.PinWall( 17250 );
		screen.Update();
		Assert.AreSame( staffRoom.Shape, screen.Footprint.Shape );
		Assert.AreEqual( staffRoom.Id, screen.Previewed );

		screen.RowSelected( aztec.Id );
		Time.PinWall( 17500 );
		screen.RowSelected( aztec.Id );
		Time.PinWall( 17875 );
		screen.Update();
		Assert.AreSame( aztec.Shape, screen.Footprint.Shape, "625 ms after the first, 375 after the second" );

		// Shown once: a picture put there since is not painted over by the row that has stopped waiting.
		screen.Footprint.Shape = staffRoom.Shape;
		Time.PinWall( 19000 );
		screen.Update();
		Assert.AreSame( staffRoom.Shape, screen.Footprint.Shape );

		// Buy Land is row -1.
		screen.RowSelected( -1 );
		Time.PinWall( 20000 );
		screen.Update();
		Assert.IsNull( screen.Footprint.Shape );
		Assert.AreEqual( -1, screen.Previewed );

		// A ride with a golden-ticket cost nobody has unlocked: Lost Kingdom's tourride or volcano.
		var mystery = catalogue.All.First( item => item.GoldenTicketCost > 0 );
		Assert.IsNotNull( mystery.Shape );
		screen.Footprint.Shape = staffRoom.Shape;
		screen.RowSelected( mystery.Id );
		Time.PinWall( 21000 );
		screen.Update();
		Assert.IsNull( screen.Footprint.Shape, mystery.Name );
	}

	/// <summary>The pointer moved onto the buy list's first row, through the stack: half a second on, its picture is there.</summary>
	/// <remarks><b>Mutations:</b> the buy list without the flag; the selection not wired to the wait; the picture not in the panel.</remarks>
	[TestMethod]
	public void ThePointerOnABuyRowShowsItsFootprint()
	{
		var (catalogue, screen, stack) = BuyScreen();
		var list = (UiList)typeof( ParkBuyScreen ).GetField( "_list", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance )!.GetValue( screen )!;
		var first = catalogue.All.Single( item => item.Id == list.Rows[0].Id );

		Time.PinWall( 32000 );
		Input.Mouse = new()
		{
			Position = new Vector2( (list.RowArea.Left + list.RowArea.Right) / 2f, list.RowArea.Top + (list.RowHeight / 2f) ),
			Delta = new Vector2( 1, 0 )
		};
		stack.Update();
		Assert.AreEqual( first.Id, list.Selected );
		Assert.IsTrue( stack.Windows.Contains( screen ), "a move chooses nothing" );
		Assert.IsNull( screen.Footprint.Shape );

		Time.PinWall( 32750 );
		Input.Mouse = new() { Position = Input.Mouse.Position };
		stack.Update();
		Assert.AreSame( first.Shape, screen.Footprint.Shape );

		var panel = screen.Root.Children.Single( control => control.Id == 0x1ea );
		Assert.AreSame( screen.Footprint, panel.Children.Single( control => control.Id == 0x1eb ) );
		Assert.AreEqual( new UiRect( 440, 407, 594, 561 ), screen.Footprint.Rect );
	}
}
