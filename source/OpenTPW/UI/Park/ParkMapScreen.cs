using NeoVeldrid;

namespace OpenTPW.UI;

/// <summary>
/// The park map: the screen behind the gadget's map button.
///
/// <para>
/// <b>Where it comes from.</b> The original's compiled layout stream at 0x00774da0
/// (<c>docs/exe/hud.md</c>, "The park map"). Every rectangle and every id below is that stream's, and
/// every mesh is named by the hash the stream carries.
/// </para>
/// <para>
/// <b>It pauses</b>, as the original's does, and quietens the advisor with the fading stop, not the
/// crying one. <see cref="ParkGadget"/> leaves <see cref="UiWindow.Pauses"/> false because it is up for
/// the whole of a park; this is the opposite case, and <see cref="ParkFrontEnd"/> holds the advisor for
/// any window that pauses.
/// </para>
/// <para>
/// <b>The map is the shipped picture, and a window on the park.</b> Every park carries its own 512x512
/// <c>2dmap.tga</c>, drawn upside down under the frame map_win at the original's scale, opened on the camera's cell
/// and moved by the arrows, the arrow keys, the screen's edges or the right button, and zoomed by the two buttons and
/// the keypad's + and - (<see cref="ParkMapView"/>, <c>docs/exe/hud.md</c>, "The map's view"). OK and a click on the map
/// put the camera on the view's cell as the map closes; Escape closes it where it is.
/// </para>
/// <para>
/// <b>The overlay is not built</b> (counted, <c>MAP_OVERLAY</c>; the build is Q684). Over that picture the original paints a
/// 128x128 grid of codes - paths, queues, land outside the park, and each ride, shop, sideshow, feature, toilet, camera
/// and staff room, filled by a three-stop ramp over its repair, excitement or satisfaction - and over it draws a dot for
/// each guest and member of staff, the litter, the guards', cameras' and entertainers' ranges and the chosen member's
/// patrol area. Six layer switches, the staff picker and the metric picker choose what it shows
/// (<c>docs/exe/hud.md</c>, "The map's overlay"). The two button panels that back the switches, buttpan1 and
/// buttpan2, are kept bare, as the screen's own backing art.
/// </para>
/// <para>
/// <b>No colours are painted over the picture</b>, so the shipped picture shows the park's terrain alone.
/// </para>
/// <para>
/// <b>Engine and content.</b> The window, the controls and the help bar are engine. Which controls the
/// map has, where they sit and what each does is this file - the park's content.
/// </para>
/// </summary>
internal sealed class ParkMapScreen : UiWindow
{
	/// <summary>Slot 4, which is what FUN_005f0b40 gives the lettering panel before colouring it yellow.</summary>
	private const int LabelFont = 4;

	/// <summary>Where the help bar's top stands while the map is up (<c>0x005f1015</c>, <c>FUN_00492530( -1, 10 )</c>).</summary>
	private const int HelpBarTopAtTheMap = 10;

	private readonly ParkMapView _view;
	private readonly MapPicture _picture;
	private readonly UiControl _viewport;
	private readonly MapArrow[] _arrows;

	/// <summary>The right button went down on the map and is still held - the viewport's <c>+0x20084</c>.</summary>
	private bool _dragging;

	private ParkState? _parkGiven;

	/// <summary>The running park whose advisor hears of this screen: the level's, or one a test gives.</summary>
	internal ParkState? Park { get => _parkGiven ?? Level.Current?.ParkState; set => _parkGiven = value; }

	// The park whose advisor this screen has asked for its line, none before it is first shown.
	private ParkState? _asked;

	/// <summary>The open map, for the debug console's census; null while none is.</summary>
	internal static ParkMapScreen? Current { get; private set; }

	internal ParkMapView View => _view;

	/// <summary>Where the help bar's top stands now, for the census.</summary>
	internal int? HelpBarTop => Stack.HelpBarTop;

	public ParkMapScreen( WindowStack stack ) : base( stack )
	{
		// The original pauses the park here and quietens the advisor - see the class remarks. Modal so
		// that nothing behind it can be clicked, which is what putting the park's interface away
		// (message 6 to DAT_007cb2ac) comes to here.
		Pauses = true;
		Modal = true;

		// FUN_005f0b40 closes the park screen that is open before it hides the layer (0x005f0bd1).
		ClosesParkScreen = true;

		var poi = ParkOrbitCameraMode.PointOfInterest;
		var ground = ParkGround.Current;
		_view = ParkMapView.OpenAt( poi.X, poi.Y, ground?.MapInfoWidth ?? 95, ground?.MapInfoHeight ?? 84,
			ground?.Heightfield?.CellsX ?? 96, ground?.Heightfield?.CellsY ?? 85 );

		// The root wears map_win (w_map.MD2), the frame round the whole screen: the stream binds it after the metric
		// group's close (0x007752da), so it is the root's. The picture is drawn under it, on black, by the root itself.
		// Laid out as one piece, so on a window wider than 4:3 its controls keep to the frame, not the window's edges.
		_picture = new MapPicture( _view )
		{
			Id = 0x980,
			Rect = new UiRect( 0, 0, 2047, 1537 ),
			Mesh = UiMesh.Get( "w_map" ),
			HoldsChildren = true
		};
		Root = _picture;

		// The map itself, and the one control with a handler of its own in the original (LAB_005f0b20): a left click
		// centres the view on the cell under it and closes the map there, a right press starts the drag. It is added
		// first so everything else is over it: the scroll arrows and the zoom buttons overlap its bottom-left corner.
		// Its 28-point region is left as the rectangle.
		_viewport = Root.Add( new UiControl
		{
			Id = 0x992,
			Rect = new UiRect( 49, 33, 1877, 1364 ),
			StopsPointer = true,
			LeftClicked = CentreAndClose,
			RightPressed = () => _dragging = GameOptions.Current.RightButtonScroll,

			// The viewport's 0x10001 keeps the nearest shown record of the overlay's list within 5 screen pixels of the
			// pointer (0x005f198e) and the thing id of the cell under it, and letters 0x991 with the record's thing or the
			// cell's (docs/exe/hud.md, "The map's hover and click"): not built, it waits on the overlay's records (Q685).
			Entered = () => Unimplemented.Report( "MAP_HOVER" )
		} );

		// The two button panels and the lettering panel between them.
		Root.Add( new UiControl
		{
			Id = 0x990,
			Rect = new UiRect( 35, 1387, 681, 1533 ),
			Mesh = UiMesh.Get( "f_mapbut1" )
		} );

		Root.Add( new UiControl
		{
			Id = 0x995,
			Rect = new UiRect( 1356, 1387, 1909, 1533 ),
			Mesh = UiMesh.Get( "f_mapbut2" )
		} );

		// Yellow, font 4 - FUN_005f0b40 fetches 0x991, hands it a label skin and calls the colour setter
		// with (0xff, 0xff, 0, 0xff). The original fills it as the pointer moves over the map with the hovered record's
		// thing's name, or the name of the thing on the cell under it (docs/exe/hud.md, "The map's hover and click"; Q685),
		// which is not built, so it is drawn empty rather than given words it may not have.
		Root.Add( new UiControl
		{
			Id = 0x991,
			Rect = new UiRect( 688, 1395, 1351, 1525 ),
			Mesh = UiMesh.Get( "f_maptext" ),
			TextRect = new UiRect( 730, 1424, 1295, 1503 ),
			Font = LabelFont,
			TextColour = new UiColour( 255, 255, 0 )
		} );

		// The four scroll arrows, flags 0x21 (0x20 repeating). Each takes the pointer inside its seven-point region (op 4
		// sub-op 4), a wedge from the pad's centre (158,1236) out to its arc: their rectangles overlap, and the up arrow's
		// middle is the right arrow's corner.
		_arrows =
		[
			Root.Add( new MapArrow( 0, 1 )
			{
				Id = 0x98a, Rect = new UiRect( 80, 1079, 237, 1236 ), HelpText = 263, Mesh = UiMesh.Get( "b_mapup" ),
				Outline = [new( 158, 1236 ), new( 80, 1157 ), new( 121, 1134 ), new( 133, 1129 ), new( 184, 1129 ), new( 196, 1132 ), new( 237, 1157 )]
			} ),
			Root.Add( new MapArrow( 0, -1 )
			{
				Id = 0x981, Rect = new UiRect( 80, 1236, 237, 1393 ), HelpText = 264, Mesh = UiMesh.Get( "b_mapdn" ),
				Outline = [new( 158, 1236 ), new( 237, 1315 ), new( 195, 1338 ), new( 183, 1343 ), new( 133, 1343 ), new( 121, 1340 ), new( 80, 1315 )]
			} ),
			Root.Add( new MapArrow( -1, 0 )
			{
				Id = 0x984, Rect = new UiRect( 1, 1157, 158, 1315 ), HelpText = 265, Mesh = UiMesh.Get( "b_mapleft" ),
				Outline = [new( 158, 1236 ), new( 80, 1315 ), new( 56, 1273 ), new( 51, 1261 ), new( 51, 1210 ), new( 54, 1198 ), new( 80, 1157 )]
			} ),
			Root.Add( new MapArrow( 1, 0 )
			{
				Id = 0x988, Rect = new UiRect( 158, 1157, 315, 1315 ), HelpText = 266, Mesh = UiMesh.Get( "b_mapright" ),
				Outline = [new( 158, 1236 ), new( 237, 1157 ), new( 261, 1199 ), new( 266, 1211 ), new( 266, 1262 ), new( 262, 1274 ), new( 237, 1315 )]
			} )
		];

		// Zoom. The stream gives these two a CIRCULAR hit region - op 4 sub-op 2, three shorts of centre
		// and radius, (246,1355) r28 and (61,1356) r29 - which is likewise left as the rectangle.
		Root.Add( new UiButton
		{
			Id = 0x986,
			Rect = new UiRect( 218, 1327, 275, 1384 ),
			HelpText = 261,
			Mesh = UiMesh.Get( "b_plus" ),
			Clicked = () => _view.ZoomBy( In: true )
		} );

		Root.Add( new UiButton
		{
			Id = 0x985,
			Rect = new UiRect( 32, 1327, 90, 1385 ),
			HelpText = 262,
			Mesh = UiMesh.Get( "b_minus" ),
			Clicked = () => _view.ZoomBy( In: false )
		} );

		// The stream gives this one id -1 rather than a number of its own, and UIHELPTEXT row 1 -
		// "Left-click to close this screen". It closes through 0x005f1050, which puts the camera on the view.
		Root.Add( new UiButton
		{
			Id = -1,
			Rect = new UiRect( 1941, 1339, 2024, 1422 ),
			HelpText = 1,
			Mesh = UiMesh.Get( "b_okay" ),
			Clicked = CloseOnTheView
		} );

		// The quiet stop, which is what the original takes here: Advisor_StopQuietly(1) fades his voice
		// and throws the line away, where Hush would have him cry out. OptionsScreen is the only other
		// thing in this game that takes it, and for the same reason.
		Advisor.Current?.StopQuietly();

		// The overlay, its switches and pickers (Q684).
		Unimplemented.Report( "MAP_OVERLAY" );

		Stack.HelpBarTop = HelpBarTopAtTheMap;
		Current = this;
		Log.Info( $"Park map: opened, {_view}" );
	}

	/// <summary>
	/// Every pass the map is up: the held arrows, the arrow keys and the keypad's + and - (the map's key cases,
	/// <c>0x005f16aa</c>, <c>0x005f1722</c>), and the screen's edges or the right button's drag as the Options' Scroll says
	/// (<c>[0x0078d910]</c>, <see cref="GameOptions.RightButtonScroll"/>). Each per-pass move is taken onto
	/// <see cref="Time.Delta"/> at thirty passes a second - see <see cref="ParkMapView"/>.
	/// </summary>
	protected internal override void Update()
	{
		var passes = ParkMapView.PassesASecond * Time.Delta;

		// A held arrow repeats from the first pass after its press (its repeat delays are 0 and 10 ms, 0x005f0e43), and
		// only while the pointer is on it (the button's 0x10009 and 0x10008, 0x00668dd9).
		foreach ( var arrow in _arrows )
		{
			if ( arrow.Pressed && arrow.Hovered )
				_view.Scroll( arrow.Across * ParkMapView.CellsAPass * passes, arrow.Down * ParkMapView.CellsAPass * passes );
		}

		// A key's press and each of its repeats, one cell (0x2500 left, 0x2600 up, 0x2700 right, 0x2800 down), and the
		// keypad's + and - (0x6b00, 0x6d00).
		if ( !Input.TextCaptured )
		{
			foreach ( var key in Input.KeysPressed )
			{
				switch ( key )
				{
					case Key.Left: _view.Scroll( -ParkMapView.CellsAPass, 0f ); break;
					case Key.Right: _view.Scroll( ParkMapView.CellsAPass, 0f ); break;
					case Key.Up: _view.Scroll( 0f, ParkMapView.CellsAPass ); break;
					case Key.Down: _view.Scroll( 0f, -ParkMapView.CellsAPass ); break;
					case Key.KeypadPlus: _view.ZoomBy( In: true ); break;
					case Key.KeypadMinus: _view.ZoomBy( In: false ); break;
				}
			}
		}

		if ( !Input.Mouse.Right )
			_dragging = false;

		if ( GameOptions.Current.RightButtonScroll )
		{
			// The viewport's frame message (0x005f1913): only with the right button held from a press on the map.
			if ( _dragging )
			{
				var (x, y) = PointerOnTheScreen();
				var (across, down) = ParkMapView.RightDrag( x, y );
				_view.Scroll( across * passes, down * passes );
			}
		}
		else if ( Screen.Width > 0 && Screen.Height > 0 )
		{
			// The root's frame message (0x005f126b): the pointer's place on the window from 0 to 1.
			var (across, down) = ParkMapView.EdgePush( Input.Mouse.Position.X / Screen.Width, Input.Mouse.Position.Y / Screen.Height );
			_view.Scroll( across * passes, down * passes );
		}
	}

	/// <summary>The pointer on the virtual screen, by the screen's own centre, which is the map's.</summary>
	private static (float X, float Y) PointerOnTheScreen()
		=> (VirtualScreen.ToVirtualX( Input.Mouse.Position.X, Anchor.Centre ), VirtualScreen.ToVirtualY( Input.Mouse.Position.Y, VerticalAnchor.Middle ));

	/// <summary>A click on the map (0x005f1d19): the view centred on the cell under the pointer, then the close OK makes.</summary>
	private void CentreAndClose()
	{
		// The original hands the hovered record's thing, or else the thing on the cell under the pointer, to its window
		// opener after the close (0x005f1de8, 0x005f1df5, Thing_OpenWindow; Q685); nothing here does, and the count tells a
		// click from a hover.
		Unimplemented.Report( "MAP_CLICK_HANDOFF" );

		var (x, y) = PointerOnTheScreen();
		_view.CentreOn( x, y );
		CloseOnTheView();
	}

	/// <summary>
	/// <c>0x005f1050</c>: the camera put on the view's cell (<c>FUN_0042aab0</c>, which writes the point and noughts the
	/// pan's momentum; the zoom and the turn are left), then the map closed. Escape closes it without this.
	/// </summary>
	private void CloseOnTheView()
	{
		var (x, y) = _view.CameraPoint();
		ParkOrbitCameraMode.PointOfInterest = new Vector3( x, y, ParkOrbitCameraMode.PointOfInterest.Z );
		ParkOrbitCameraMode.Scroll = Vector2.Zero;
		Log.Info( $"Park map: closed on the view, {_view}, the camera's point ({x:0},{y:0})" );
		Stack.Close( this );
	}

	// There is no Cancel() here: that is a box's Escape, and the map has no box. A plain Escape let go closes the map
	// (the map's own key-up case, 0x005f17ef, ParkFrontEnd.MenuKey), and leaves the camera where it was.

	/// <summary>
	/// The opener's last act (Map_Open, <c>0x005f100d</c>): the advisor is posted message 0x130 (FUN_00486b00), the
	/// screen's own line. The map pauses the park, and he says a slot only on his turn in the park's sweep, so the line
	/// waits in its slot while the map is up.
	/// </summary>
	protected internal override void Shown()
	{
		if ( _asked != null || Park is not { AdvisorMessages: { } lines } park )
			return;

		_asked = park;
		lines.Post( ParkAdvisorMessages.MapScreen, park.GameTick );
	}

	/// <summary>
	/// The map's own picture goes when the screen does. It is built from a stream rather than a path, so
	/// nothing caches it and nothing else shares it - left alone it would be one more pathless texture a
	/// scene never lets go of. The help bar goes back to the bottom (<c>FUN_00492530( -1, -1 )</c> on message <c>0x14</c>, <c>0x005f1256</c>),
	/// and the advisor's message 0x130 is withdrawn (FUN_00486b40, <c>0x005f123c</c>), which empties its slot.
	/// </summary>
	protected internal override void Closed()
	{
		_asked?.AdvisorMessages?.Withdraw( ParkAdvisorMessages.MapScreen, (long)(GameClock.Now * 1000), () => Advisor.Current?.StopSpeaking() );
		_picture.Release();
		Stack.HelpBarTop = null;

		if ( Current == this )
			Current = null;
	}

	/// <summary>
	/// A scroll arrow: a repeating button (<c>FUN_00668f35( 0, 10 )</c>, <c>Button_SetRepeatDelays</c> in Ghidra) that moves
	/// the view while it is held. A held button that has repeated posts no click as it is let go (<c>FUN_00668eff</c>), and
	/// the click sound plays only on a click (<c>0x00485780</c>), so a held arrow is silent; the original's arrows answer a
	/// click with nothing, and one let go before its first repeat (a press and release in the same pass) would sound,
	/// which is not built.
	/// </summary>
	private sealed class MapArrow( int across, int down ) : UiButton
	{
		public int Across { get; } = across;

		public int Down { get; } = down;

		internal override bool Clicks => false;
	}

	/// <summary>
	/// The root: black over the whole window, the part of the picture the view has arrived at, then the frame map_win
	/// (w_map.MD2). The
	/// original hides the park's own view as the map opens (message 6 to <c>DAT_007cb2ac</c>), so nothing of the park
	/// shows round or through the picture.
	///
	/// <para>
	/// The image is the park's own <c>levels/&lt;theme&gt;/2dmap.tga</c>, decoded by <see cref="Texture"/>'s stream
	/// constructor - the same StbImageSharp path the sky's art takes - with its rows as a viewer shows them, and drawn
	/// as one quad with a texture rectangle (<see cref="ParkMapView.Placed"/>). The original truncates its source
	/// rectangle to whole pixels; this draws it exactly.
	/// </para>
	/// </summary>
	private sealed class MapPicture( ParkMapView view ) : UiControl
	{
		private Texture? _map;
		private Texture? _black;
		private bool _tried;

		internal void Release()
		{
			_map?.Delete();
			_map = null;
			_black?.Delete();
			_black = null;
		}

		protected override void OnDraw()
		{
			_black ??= new Texture( [0, 0, 0, 255], 1, 1 );
			Material.UI.Set( "Color", _black );

			using ( _ = new Graphics.Scope( Screen.Size ) )
				Graphics.Quad( new Rectangle( 0, 0, Screen.Width, Screen.Height ), Material.UI );

			if ( Load() is { } map )
			{
				var (layout, texture) = view.Placed;
				var pixels = VirtualScreen.ToPixels( layout, Anchor.Centre, VerticalAnchor.Middle );

				Material.UI.Set( "Color", map );

				// Graphics.Quad's window is y-up, where a control's pixels are y-down.
				using ( _ = new Graphics.Scope( Screen.Size ) )
					Graphics.Quad( new Rectangle( pixels.X, Screen.Height - pixels.Y - pixels.Height, pixels.Width, pixels.Height ), texture, Material.UI );
			}

			base.OnDraw();
		}

		/// <summary>
		/// The park's map picture, read once. A park without one draws nothing rather than complaining
		/// every frame, which is why the attempt is remembered as well as the result.
		/// </summary>
		private Texture? Load()
		{
			if ( _tried )
				return _map;

			_tried = true;

			if ( Level.Current?.ThemeName is not { } theme )
				return null;

			var path = $"levels/{theme}/2dmap.tga";

			try
			{
				using var stream = FileSystem.OpenRead( path );

				if ( stream == null )
				{
					Log.Warning( $"Park map: {path} is not there, so the map has no picture" );
					return null;
				}

				_map = new Texture( stream );
			}
			catch ( Exception e )
			{
				Log.Warning( $"Park map: {path} would not load - {e.Message}" );
			}

			return _map;
		}
	}
}
