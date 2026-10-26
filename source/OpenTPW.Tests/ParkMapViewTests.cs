using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoVeldrid;
using OpenTPW.UI;
using System.Collections.Generic;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// The park map's view (<see cref="ParkMapView"/>, <c>docs/exe/hud.md</c>, "The map's view"): where it opens, how it moves
/// and zooms, what part of the picture it shows and where, and where the camera goes as it closes. The numbers are the
/// original's own, read from its map object in the stock park (<c>q624/orig</c>, rounds a and b).
/// </summary>
[TestClass]
public class ParkMapViewTests
{
	private float _zoom;
	private Vector3 _poi;
	private float _delta;
	private bool _rightButton;

	[TestInitialize]
	public void KeepTheStatics()
	{
		Log ??= new();
		_zoom = ParkMapView.KeptZoom;
		_poi = ParkOrbitCameraMode.PointOfInterest;
		_delta = Time.Delta;
		_rightButton = GameOptions.Current.RightButtonScroll;
		ParkMapView.KeptZoom = ParkMapView.MinZoom;
	}

	[TestCleanup]
	public void PutThemBack()
	{
		ParkMapView.KeptZoom = _zoom;
		ParkOrbitCameraMode.PointOfInterest = _poi;
		Time.Delta = _delta;
		GameOptions.Current.RightButtonScroll = _rightButton;
		Input.ForgetHeldKeys();
		Input.Mouse = new();
	}

	private static ParkMapView Stock( float x = 480f, float y = 265f ) => ParkMapView.OpenAt( x, y, 95, 84, 96, 85 );

	/// <summary>
	/// <b>It opens on the camera's cell, held below <c>MapInfo</c>'s width and height first</b> (<c>0x005f0f0a</c>): the
	/// original opened on (48,26) for a camera on (480,265), and on 94, not 95, for one on (950,260).
	/// </summary>
	[TestMethod]
	public void ItOpensOnTheCamerasCellHeldBelowTheMapsLimits()
	{
		var view = Stock();
		Assert.AreEqual( (48f, 26f), (view.Across, view.Down ) );

		view = Stock( 950f, 260f );
		Assert.AreEqual( (94f, 26f), (view.Across, view.Down), "MapInfo's 95 less one, before the heightfield's 96" );

		view = Stock( -5f, 2000f );
		Assert.AreEqual( (0f, 83f), (view.Across, view.Down) );
	}

	/// <summary>
	/// <b>A move is held to the heightfield's cells less one</b> (<c>0x005f35b0</c>), and up ADDS to the centre down: the
	/// original's right arrow held stopped at 95, its top edge at 84, and its Up key took 57 to 58.
	/// </summary>
	[TestMethod]
	public void AMoveIsHeldToTheCellsLessOneAndUpAdds()
	{
		var view = Stock();
		view.Scroll( 0f, 1f );
		Assert.AreEqual( 27f, view.Down, "up is one more" );

		view.Scroll( 100f, 100f );
		Assert.AreEqual( (95f, 84f), (view.Across, view.Down) );

		view.Scroll( -500f, -500f );
		Assert.AreEqual( (0f, 0f), (view.Across, view.Down) );
	}

	/// <summary>
	/// <b>The zoom steps by a half from 1 to 3, and is kept from one opening to the next</b>: the original's static is set
	/// only by its constructor; it reopened at 1.5 after a plus.
	/// </summary>
	[TestMethod]
	public void TheZoomStepsByAHalfFromOneToThreeAndIsKept()
	{
		var view = Stock();
		var seen = new List<float>();

		for ( var press = 0; press < 5; ++press )
		{
			view.ZoomBy( In: true );
			seen.Add( view.Zoom );
		}

		CollectionAssert.AreEqual( new[] { 1.5f, 2f, 2.5f, 3f, 3f }, seen );
		Assert.AreEqual( 3f, Stock().Zoom, "a new opening keeps it" );

		for ( var press = 0; press < 5; ++press )
			view.ZoomBy( In: false );

		Assert.AreEqual( 1f, view.Zoom );
	}

	/// <summary>
	/// <b>The picture shown is what an 800 x 600 screen holds about the centre, held to the picture, upside down</b>
	/// (<c>0x005f3230</c>): at zoom 1 on (48,26) the whole of the picture's width and its rows 0 to 472, the field's left
	/// at layout 412 and its bottom (row 43) at 1098, where the original's frame had them at 412 and 1096; at zoom 1.5 on
	/// (95,26) the field's right edge at layout 1043, the original's 1044.
	/// </summary>
	[TestMethod]
	public void ThePictureShownIsWhatTheOriginalsScreenHoldsUpsideDown()
	{
		var view = Stock();
		Assert.AreEqual( (0f, 0f, 511f, 472f), view.Source );

		var (layout, texture) = view.Placed;
		Assert.AreEqual( new UiRect( 371, 0, 1679, 1208 ), layout );
		Assert.AreEqual( 412f, 1024f + ((16f - 255f) * view.Scale), 0.5f, "the field's left" );
		Assert.AreEqual( 1098f, 768f + ((172f - 43f) * view.Scale), 0.5f, "the field's bottom, row 43" );

		// The texture runs from the source's first row (v 1, the picture's top as a viewer shows it) DOWN: a negative height.
		Assert.AreEqual( 0f, texture.X );
		Assert.AreEqual( 1f, texture.Y, 1e-6f );
		Assert.AreEqual( 511f / 512f, texture.Width, 1e-6f );
		Assert.AreEqual( -472f / 512f, texture.Height, 1e-6f );

		view.ZoomBy( In: true );
		view.Scroll( 100f, 0f );
		Assert.AreEqual( 223.33f, view.Source.Left, 0.01f );
		Assert.AreEqual( new UiRect( 0, 0, 1105, 1428 ), view.Placed.Layout );
		Assert.AreEqual( 1043f, 1024f + ((495f - 490f) * view.Scale), 0.5f, "the field's right" );
	}

	/// <summary>
	/// <b>A click centres on the cell under it, from the screen's centre, and the close puts the camera on that cell's
	/// corner</b> (<c>0x005f1d19</c>, <c>0x005f1050</c>): the original, at zoom 1.5 on (48,26), clicked at layout (1218,765),
	/// read 58.104 and 26.156 and put its camera on (580,260).
	/// </summary>
	[TestMethod]
	public void AClickCentresOnTheCellUnderItAndTheCameraGoesThere()
	{
		var view = Stock();
		view.ZoomBy( In: true );
		view.CentreOn( 1218f, 765f );

		Assert.AreEqual( 58.104f, view.Across, 0.001f );
		Assert.AreEqual( 26.156f, view.Down, 0.001f );
		Assert.AreEqual( (580f, 260f), view.CameraPoint() );

		view.CentreOn( 5000f, 768f );
		Assert.AreEqual( (950f, 260f), view.CameraPoint(), "held to the cells less one as it closes" );
	}

	/// <summary>
	/// <b>The screen's edges push at 250 cells a pass for each whole of the screen past a hundredth</b> (<c>0x005f126b</c>),
	/// the top edge UP (the original's rose 2.5 a frame there), and the right button drags by the pointer's distance from
	/// the screen's centre (<c>0x005f192e</c>).
	/// </summary>
	[TestMethod]
	public void TheEdgesPushAndTheRightButtonDrags()
	{
		Assert.AreEqual( (0f, 2.5f), ParkMapView.EdgePush( 0.5f, 0f ) );
		Assert.AreEqual( (-2.5f, 0f), ParkMapView.EdgePush( 0f, 0.5f ) );
		var (corner, cornerDown) = ParkMapView.EdgePush( 1f, 1f );
		Assert.AreEqual( 2.5f, corner, 1e-4f );
		Assert.AreEqual( -2.5f, cornerDown, 1e-4f );
		Assert.AreEqual( (0f, 0f), ParkMapView.EdgePush( 0.5f, 0.5f ) );

		var (across, down) = ParkMapView.EdgePush( 0.995f, 0.5f );
		Assert.AreEqual( 1.25f, across, 1e-4f );
		Assert.AreEqual( 0f, down );

		(across, down) = ParkMapView.RightDrag( 1024f + 409.6f, 768f + 307.2f );
		Assert.AreEqual( 1f, across, 1e-5f );
		Assert.AreEqual( -1f, down, 1e-5f, "below the centre moves the view down" );
	}

	/// <summary>
	/// <b>On the screen: an arrow key moves a cell, OK puts the camera on the view's cell, and Escape closes it where it
	/// is</b> (the map's key cases, <c>0x005f16aa</c>; <c>0x005f1050</c>; the key-up case <c>0x005f17ef</c>, which does not
	/// call it). The original's Escape left its camera on (950,260) where OK would have made it (940,260).
	/// </summary>
	[TestMethod]
	public void AnArrowKeyMovesOkGoesThereAndEscapeLeavesTheCamera()
	{
		var stack = ParkEscapeOnReleaseTests.APark();
		GameOptions.Current.RightButtonScroll = true;
		ParkOrbitCameraMode.PointOfInterest = new Vector3( 480f, 265f, 0f );

		var map = new ParkMapScreen( stack );
		stack.Open( map );
		Assert.AreEqual( (48f, 26f), (map.View.Across, map.View.Down) );

		ParkEscapeOnReleaseTests.Frame( stack, ParkEscapeOnReleaseTests.Down( Key.Right ) );
		ParkEscapeOnReleaseTests.Frame( stack, ParkEscapeOnReleaseTests.Up( Key.Right ) );
		ParkEscapeOnReleaseTests.Frame( stack, ParkEscapeOnReleaseTests.Down( Key.Up ) );
		ParkEscapeOnReleaseTests.Frame( stack, ParkEscapeOnReleaseTests.Up( Key.Up ) );
		Assert.AreEqual( (49f, 27f), (map.View.Across, map.View.Down) );

		ParkOrbitCameraMode.Scroll = new Vector2( 60f, -40f );
		Ok( map ).Clicked!();
		Assert.IsFalse( stack.Windows.Contains( map ), "OK closes it" );
		Assert.AreEqual( new Vector3( 490f, 270f, 0f ), ParkOrbitCameraMode.PointOfInterest, "on the view's cell" );
		Assert.AreEqual( Vector2.Zero, ParkOrbitCameraMode.Scroll, "FUN_0042aab0 noughts the pan's momentum with the point" );

		map = new ParkMapScreen( stack );
		stack.Open( map );
		ParkEscapeOnReleaseTests.Frame( stack, ParkEscapeOnReleaseTests.Down( Key.Left ) );
		ParkEscapeOnReleaseTests.Frame( stack, ParkEscapeOnReleaseTests.Down( Key.Escape ) );
		ParkEscapeOnReleaseTests.Frame( stack, ParkEscapeOnReleaseTests.Up( Key.Escape ) );
		Assert.IsFalse( stack.Windows.Contains( map ), "Escape closes it" );
		Assert.AreEqual( new Vector3( 490f, 270f, 0f ), ParkOrbitCameraMode.PointOfInterest, "where it was" );
	}

	/// <summary>
	/// <b>A held arrow moves a cell a pass, thirty passes a second, and only while the pointer is on it</b> (the button's
	/// repeat, <c>0x00668dd9</c>): the original's right arrow took about 30 cells a second. Each of the four moves its own
	/// way (0x98a up adds to the centre down, 0x981 takes, 0x984 left, 0x988 right: the table <c>0x005f18a4</c>), and the
	/// move is a pass's share of the frame, not a cell a frame: three frames of a thirtieth of a second are three cells, of a
	/// sixtieth one and a half.
	/// </summary>
	[TestMethod]
	public void AHeldArrowMovesACellAPassWhileThePointerIsOnIt()
	{
		var stack = ParkEscapeOnReleaseTests.APark();
		GameOptions.Current.RightButtonScroll = true;
		ParkOrbitCameraMode.PointOfInterest = new Vector3( 480f, 265f, 0f );

		var map = new ParkMapScreen( stack );
		stack.Open( map );

		foreach ( var (id, across, down) in new[] { (0x988, 1f, 0f), (0x984, -1f, 0f), (0x98a, 0f, 1f), (0x981, 0f, -1f) } )
		{
			foreach ( var (delta, cells) in new[] { (1f / 30f, 3f), (1f / 60f, 1.5f) } )
			{
				Time.Delta = delta;
				var arrow = (UiButton)Control( map, id );
				var (startAcross, startDown) = (map.View.Across, map.View.Down);
				arrow.Pressed = true;
				arrow.Hovered = true;

				for ( var frame = 0; frame < 3; ++frame )
					ParkEscapeOnReleaseTests.Frame( stack );

				Assert.AreEqual( (startAcross + (across * cells), startDown + (down * cells)), (map.View.Across, map.View.Down),
					$"0x{id:x} held three frames of {delta * 1000f:0} ms" );

				arrow.Hovered = false;
				ParkEscapeOnReleaseTests.Frame( stack );
				Assert.AreEqual( (startAcross + (across * cells), startDown + (down * cells)), (map.View.Across, map.View.Down), "the pointer off it: no move" );
				Assert.IsFalse( arrow.Clicks, "a held arrow is silent" );
				arrow.Pressed = false;

				// Back to the middle, so the next arrow has room both ways.
				map.View.CentreOn( 1024f, 768f );
				map.View.Scroll( 48f - map.View.Across, 26f - map.View.Down );
			}
		}
	}

	/// <summary>
	/// <b>The screen's own pass wires the edge push to Pushscroll and the right button's drag to Right button scroll</b>
	/// (<c>[0x0078d910]</c>; <c>0x005f126b</c>, <c>0x005f1913</c>), the pure rules of which
	/// <see cref="TheEdgesPushAndTheRightButtonDrags"/> holds: the top edge pushes up, the drag follows a press made on
	/// the map, and neither runs under the other option.
	/// </summary>
	[TestMethod]
	public void TheEdgePushAndTheDragAreWiredToTheirOptions()
	{
		var screen = Screen.Size;

		try
		{
			Screen.Size = new Point2( 2048, 1536 );
			Time.Delta = 1f / 30f;
			var stack = ParkEscapeOnReleaseTests.APark();
			ParkOrbitCameraMode.PointOfInterest = new Vector3( 480f, 265f, 0f );
			var map = new ParkMapScreen( stack );
			stack.Open( map );

			GameOptions.Current.RightButtonScroll = false;
			Input.Mouse = new() { Position = new System.Numerics.Vector2( 1024f, 0f ) };
			map.Update();
			Assert.AreEqual( (48f, 28.5f), (map.View.Across, map.View.Down), "Pushscroll: the top edge pushes up 2.5 a pass" );

			Time.Delta = 1f / 60f;
			map.Update();
			Assert.AreEqual( (48f, 29.75f), (map.View.Across, map.View.Down), "half a pass is half the push" );
			Time.Delta = 1f / 30f;

			GameOptions.Current.RightButtonScroll = true;
			map.Update();
			Assert.AreEqual( (48f, 29.75f), (map.View.Across, map.View.Down), "Right button scroll: the edge pushes nothing" );

			Input.Mouse = new() { Right = true, Position = new System.Numerics.Vector2( 1024f + 409.6f, 768f ) };
			map.Update();
			Assert.AreEqual( (48f, 29.75f), (map.View.Across, map.View.Down), "no drag before a press on the map" );

			Control( map, 0x992 ).RightPressed!();
			map.Update();
			Assert.AreEqual( (49f, 29.75f), (map.View.Across, map.View.Down), "dragged right a cell" );

			GameOptions.Current.RightButtonScroll = false;
			Input.Mouse = new() { Right = true, Position = new System.Numerics.Vector2( 1024f, 768f ) };
			map.Update();
			Assert.AreEqual( (49f, 29.75f), (map.View.Across, map.View.Down), "Pushscroll: the right button drags nothing" );
		}
		finally
		{
			Screen.Size = screen;
		}
	}

	/// <summary>
	/// <b>The zoom buttons step it by a half, plus in and minus out</b> (<c>0x986</c>, <c>0x985</c>; the keypad's cases
	/// <c>0x6b00</c>, <c>0x6d00</c>), through the screen's own controls and keys, not the view's method: swapping the two
	/// buttons' ids or their handlers would otherwise pass.
	/// </summary>
	[TestMethod]
	public void TheZoomButtonsAndTheKeypadStepTheZoomOnTheScreen()
	{
		var stack = ParkEscapeOnReleaseTests.APark();
		var map = new ParkMapScreen( stack );
		stack.Open( map );
		Assert.AreEqual( 1f, map.View.Zoom );

		( (UiButton)Control( map, 0x986 ) ).Clicked!();
		Assert.AreEqual( 1.5f, map.View.Zoom, "b_plus" );

		( (UiButton)Control( map, 0x986 ) ).Clicked!();
		( (UiButton)Control( map, 0x985 ) ).Clicked!();
		Assert.AreEqual( 1.5f, map.View.Zoom, "b_minus" );

		ParkEscapeOnReleaseTests.Frame( stack, ParkEscapeOnReleaseTests.Down( Key.KeypadPlus ) );
		Assert.AreEqual( 2f, map.View.Zoom, "the keypad's +" );

		ParkEscapeOnReleaseTests.Frame( stack, ParkEscapeOnReleaseTests.Up( Key.KeypadPlus ), ParkEscapeOnReleaseTests.Down( Key.KeypadMinus ) );
		Assert.AreEqual( 1.5f, map.View.Zoom, "the keypad's -" );
	}

	/// <summary>
	/// <b>The map being up is known to the camera's keys and to the help bar</b>: <see cref="UI.WindowStack.MapOpen"/> is
	/// what <see cref="ParkOrbitCameraMode"/> reads to leave the arrow keys alone, and the bar stands at the top (10) while
	/// the map is up and goes back when it closes (<c>0x005f1015</c>, <c>0x005f1256</c>).
	/// </summary>
	[TestMethod]
	public void TheStackKnowsTheMapIsUpAndTheHelpBarStandsAtTheTop()
	{
		var stack = ParkEscapeOnReleaseTests.APark();
		var update = typeof( UI.WindowStack ).GetMethod( "OnUpdate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic )!;

		// The stack a test makes has neither the bar nor the glint a real one is built with.
		const System.Reflection.BindingFlags any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
		typeof( UI.WindowStack ).GetField( "_helpBar", any )!.SetValue( stack, new UI.HelpBar() );
		typeof( UI.WindowStack ).GetField( "_glint", any )!.SetValue( stack, new UI.ButtonGlint() );
		Assert.IsNull( stack.HelpBarTop );
		var map = new ParkMapScreen( stack );

		stack.Open( map );
		update.Invoke( stack, [] );
		Assert.IsTrue( UI.WindowStack.MapOpen, "the camera's keys are held off" );
		Assert.AreEqual( 10, stack.HelpBarTop );

		stack.Close( map );
		update.Invoke( stack, [] );
		Assert.IsFalse( UI.WindowStack.MapOpen );
		Assert.IsNull( stack.HelpBarTop );
	}

	/// <summary>
	/// <b>The map asks the advisor for its own line as it opens and takes it back as it closes</b>: the opener's last act
	/// posts message 0x130 (<c>0x005f100d</c>), and the destroy, event 0x14, withdraws it (<c>0x005f123c</c>), which empties
	/// its slot and, were he saying it, cuts him off on the game's clock. Every way the map goes is that close: OK, Escape
	/// and a click on the map.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> no post at the opening; a post as the map is made, before it is opened; no withdrawal at the
	/// close; the camcorder's 0x131 or the buy screen's 0xbc for either; response 565 for 564; the row's category, copies,
	/// lines, prop or group another's; the wall clock for the game's.
	/// </remarks>
	[TestMethod]
	public void TheMapAsksTheAdvisorForItsLineAsItOpensAndTakesItBackAsItCloses()
	{
		// Row 304 (0x00772580) is row 188's twin, the buy screen's, but for its response: posted only, category 1, either
		// mode, HelpMessage.Score, one copy, one line; response 564 is sample 582, no prop, group 383.
		Assert.AreEqual( 0x130, ParkAdvisorMessages.MapScreen );
		var buy = ParkAdvisorMessages.ParkTopics.Single( one => one.Id == ParkAdvisorMessages.BuyScreen );
		Assert.AreEqual( buy with { Id = 0x130, FirstResponse = 564, FirstSample = 582 },
			ParkAdvisorMessages.ParkTopics.Single( one => one.Id == ParkAdvisorMessages.MapScreen ) );

		using var clock = new SimulationClockScope();
		var stack = ParkEscapeOnReleaseTests.APark();
		var lines = new ParkAdvisorMessages( new SettingsFile( "Advisor/Advisor.sam" ), tutorial: () => true );
		var park = new ParkState( null ) { AdvisorMessages = lines };
		var said = new List<(int, int)>();
		int Play( int response, int sample ) { said.Add( (response, sample) ); return 26400; }

		for ( var i = 0; i < 500; ++i )
			park.AdvanceGameTick();

		// The wall clock far past any line's end: a cut is judged on the game's clock, which stands at nought.
		Time.PinWall( 60000 );

		try
		{
			var map = new ParkMapScreen( stack ) { Park = park };
			StringAssert.Contains( lines.Census(), "pending=0 attempts=0 ", "made, and not yet opened" );
			stack.Open( map );
			StringAssert.Contains( lines.Census(), "pending=1 attempts=0 threshold=25 slot=304/subject=0 " );

			// Closed by OK before he says it: the slot alone, the record as it was.
			Ok( map ).Clicked!();
			StringAssert.Contains( lines.Census(), "pending=0 attempts=0 " );
			StringAssert.Contains( lines.Census(), "message=304/score=1000/line=-1/lastTick=0/said=0/slaps=0" );

			// Opened, said (were his turn to come), and closed by Escape while he says it.
			map = new ParkMapScreen( stack ) { Park = park };
			stack.Open( map );
			lines.Tick( park.GameTick, 0, Play );
			CollectionAssert.AreEqual( new[] { (564, 582) }, said );
			StringAssert.Contains( lines.Census(), "message=304/score=1000/line=0/lastTick=500/said=1/slaps=0" );
			ParkEscapeOnReleaseTests.Frame( stack, ParkEscapeOnReleaseTests.Down( Key.Escape ) );
			ParkEscapeOnReleaseTests.Frame( stack, ParkEscapeOnReleaseTests.Up( Key.Escape ) );
			Assert.IsFalse( stack.Windows.Contains( map ), "Escape closes it" );
			StringAssert.Contains( lines.Census(), "message=304/score=1000/line=0/lastTick=500/said=0/slaps=1", "cut off" );
			StringAssert.Contains( lines.Census(), "message=305/score=1000/line=-1/lastTick=0/said=0/slaps=0", "the camcorder's record is not this one's" );
		}
		finally
		{
			Time.PinWall( null );
		}
	}

	private static UiButton Ok( ParkMapScreen map ) => (UiButton)Control( map, -1 );

	private static UiControl Control( ParkMapScreen map, int id ) => map.Root.Children.Single( control => control.Id == id );
}
