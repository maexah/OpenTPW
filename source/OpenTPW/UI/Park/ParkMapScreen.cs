namespace OpenTPW.UI;

/// <summary>
/// The park map: the screen behind the gadget's map button, and the first one in this game that is
/// real rather than a button saying why nothing happens.
///
/// <para>
/// <b>Where it comes from.</b> FUN_005f0b40 builds it from the compiled layout stream at 0x00774da0,
/// with LAB_005f0b00 as its handler. Every rectangle and every id below is that stream's, walked
/// opcode by opcode, and every mesh is named by the hash the stream carries - so the scroll arrows are
/// asked for as <c>nuup</c>, <c>nudown</c>, <c>nuleft</c> and <c>nuright</c>, which are the first nodes
/// of b_mapup/b_mapdn/b_mapleft/b_mapright.md2, and the three panel backings as <c>buttpan1</c>,
/// <c>buttpan2</c> and <c>textpan</c>.
/// </para>
/// <para>
/// <b>It pauses, and that makes it the first park window that should.</b> FUN_005f0b40 calls
/// FUN_004092a0, clears <c>g_ParkRunning</c> and quietens the advisor with Advisor_StopQuietly(1) - the
/// fading stop, not the crying one. <see cref="ParkGadget"/> must leave <see cref="UiWindow.Pauses"/>
/// false because it is up for the whole of a park; this is the opposite case, and
/// <see cref="ParkFrontEnd"/> already holds the advisor for any window that pauses.
/// </para>
/// <para>
/// <b>The map really is the shipped picture.</b> Every park carries its own 512x512 <c>2dmap.tga</c>,
/// and the original loads it: the name is a static global built at 0x005f0ad0 into DAT_00f86d20, which
/// FUN_005f1ea0 hands to the image loader before storing the width and height that size its tile set -
/// <c>(512+63)&gt;&gt;6</c> is 8, so 8x8 tiles of 64x64. That arithmetic is what confirms the image the
/// original draws is the one on disk.
/// </para>
/// <para>
/// <b>What is NOT built, and why it is a refusal rather than a gap.</b> Over that picture the original
/// paints a live 128x128 grid of classification codes (FUN_005f2050, FUN_005f2380): rides, shops,
/// sideshows, tracks, visitors and staff, each cell coloured by FUN_005f09a0, which is a three-stop
/// colour ramp over a per-thing metric from 0 to 100. Its six layer switches and its two radio groups
/// - staff overlays and the metric picker - choose what that overlay shows. <b>Every one of those is a
/// simulation value this game does not have</b>, and the game's own help rows say so in as many words:
/// "excitement ratings", "customer satisfaction", "ride reliability", "queue times". Built now they
/// would be switches over an empty overlay, which is what the aerial, the bank balance and the message
/// bar were each refused for. So this screen is the map, and the map is honest.
/// </para>
/// <para>
/// <b>The terrain colours are not invented either.</b> The original's own land codes (9 outside the
/// grid, 10, 11, 12) take descriptors from globals that are filled at run time, so what colour it
/// paints them is not readable from the image - and the shipped picture already shows the park's
/// terrain. Painting cells over it in colours chosen here would be decoration, not fidelity. The cell
/// to pixel mapping is worked out and recorded all the same, because it is what will place a ride on
/// this map later: cell (x,y) lands at pixel (16 + 5x, 43 + 5y) of that image.
/// </para>
/// <para>
/// <b>Two things about how it looks, said plainly rather than quietly shipped.</b> The two button
/// panels are <i>bare</i>: buttpan1 and buttpan2 back the six layer switches along y 1409-1512, and
/// those switches are refused above, so the panels carry nothing. They are kept because they are the
/// screen's own backing art and the layout is the point - which is a different thing from drawing a
/// control that would do nothing, and the distinction is worth stating because three things have been
/// refused nearby for looking exactly like this.
/// </para>
/// <para>
/// And <b>the picture keeps the black border the shipped art has</b>, so a thick frame sits inside the
/// square. Its inner field is measured - x 16..495, y 43..467, which is 96x85 cells of exactly five
/// pixels - so cropping to it would fill more of the viewport and look better. It is not cropped: the
/// original's tile set is 8x8 of 64, which is the whole 512, so the whole image is what it loads, and
/// what it then shows is governed by a zoom and scroll that are not traced. Cropping would be
/// inventing behaviour to improve the look, which is the one thing this file may not do.
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

	private readonly MapView _view;

	public ParkMapScreen( WindowStack stack ) : base( stack )
	{
		// The original pauses the park here and quietens the advisor - see the class remarks. Modal so
		// that nothing behind it can be clicked, which is what putting the park's interface away
		// (message 6 to DAT_007cb2ac) comes to here. No dimming backdrop: the stream's root carries no
		// mesh at all, so there is nothing traced to dim with.
		Pauses = true;
		Modal = true;

		Root = new UiControl { Id = 0x980, Rect = new UiRect( 0, 0, 2047, 1537 ) };

		// The map itself, and the one control with a handler of its own in the original (LAB_005f0b20).
		// It is added first so everything else draws over it; nothing actually overlaps it, because the
		// panels below start at y 1387 and it stops at 1364.
		_view = Root.Add( new MapView
		{
			Id = 0x992,
			Rect = new UiRect( 49, 33, 1877, 1364 )
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
		// with (0xff, 0xff, 0, 0xff). WHAT IT SAYS IS NOT TRACED: the original fills it from a resource
		// this has not read back, so it is drawn empty rather than given words it may not have.
		Root.Add( new UiControl
		{
			Id = 0x991,
			Rect = new UiRect( 688, 1395, 1351, 1525 ),
			Mesh = UiMesh.Get( "f_maptext" ),
			TextRect = new UiRect( 730, 1424, 1295, 1503 ),
			Font = LabelFont,
			TextColour = new UiColour( 255, 255, 0 )
		} );

		// The four scroll arrows. Each carries a seven-point hit region in the stream, which is not
		// reproduced - UiControl takes a rectangle, and the arrows do not overlap anything that a
		// squarer hit area could steal a click from.
		Root.Add( Arrow( 0x98a, new UiRect( 80, 1079, 237, 1236 ), 263, "b_mapup", 0, -1 ) );
		Root.Add( Arrow( 0x981, new UiRect( 80, 1236, 237, 1393 ), 264, "b_mapdn", 0, 1 ) );
		Root.Add( Arrow( 0x984, new UiRect( 1, 1157, 158, 1315 ), 265, "b_mapleft", -1, 0 ) );
		Root.Add( Arrow( 0x988, new UiRect( 158, 1157, 315, 1315 ), 266, "b_mapright", 1, 0 ) );

		// Zoom. The stream gives these two a CIRCULAR hit region - op 4 sub-op 2, three shorts of centre
		// and radius, (246,1355) r28 and (61,1356) r29 - which is likewise left as the rectangle.
		Root.Add( new UiButton
		{
			Id = 0x986,
			Rect = new UiRect( 218, 1327, 275, 1384 ),
			HelpText = 261,
			Mesh = UiMesh.Get( "b_plus" ),
			Clicked = () => _view.Zoom( In: true )
		} );

		Root.Add( new UiButton
		{
			Id = 0x985,
			Rect = new UiRect( 32, 1327, 90, 1385 ),
			HelpText = 262,
			Mesh = UiMesh.Get( "b_minus" ),
			Clicked = () => _view.Zoom( In: false )
		} );

		// The stream gives this one id -1 rather than a number of its own, and UIHELPTEXT row 1 -
		// "Left-click to close this screen".
		Root.Add( new UiButton
		{
			Id = -1,
			Rect = new UiRect( 1941, 1339, 2024, 1422 ),
			HelpText = 1,
			Mesh = UiMesh.Get( "b_okay" ),
			Clicked = Close
		} );

		// The quiet stop, which is what the original takes here: Advisor_StopQuietly(1) fades his voice
		// and throws the line away, where Hush would have him cry out. OptionsScreen is the only other
		// thing in this game that takes it, and for the same reason.
		Advisor.Current?.StopQuietly();
	}

	/// <summary>One of the four scroll arrows, which nudge the view by a cell step - see <see cref="MapView"/>.</summary>
	private UiButton Arrow( int id, UiRect rect, int helpText, string mesh, int across, int down )
		=> new()
		{
			Id = id,
			Rect = rect,
			HelpText = helpText,
			Mesh = UiMesh.Get( mesh ),
			Clicked = () => _view.Scroll( across, down )
		};

	private void Close() => Stack.Close( this );

	// THERE IS DELIBERATELY NO Cancel() HERE, and there was one until it was found to be dead. Escape
	// cannot reach this window: WindowStack.Keyboard sends it to EscapeWithoutFocus whenever no box has
	// focus, and ParkFrontEnd.MenuKey returns at once on a modal front window - so an override would
	// never once be called while reading as though it worked. The way out is b_okay, which is what the
	// game's own help row 1 says of it: "Left-click to close this screen".

	/// <summary>
	/// The map's own picture goes when the screen does. It is built from a stream rather than a path, so
	/// nothing caches it and nothing else shares it - left alone it would be one more pathless texture a
	/// scene never lets go of, which is exactly the residue branch 51 spent itself getting to zero.
	/// </summary>
	protected internal override void Closed() => _view.Release();

	/// <summary>
	/// The map picture, drawn into the viewport with the part of it that the scrolling and zooming have
	/// arrived at.
	///
	/// <para>
	/// The image is the park's own <c>levels/&lt;theme&gt;/2dmap.tga</c>, decoded by
	/// <see cref="Texture"/>'s stream constructor - the same StbImageSharp path the sky's art takes. It
	/// is drawn as one quad with a texture-coordinate rectangle, which is all the scrolling and zooming
	/// amount to.
	/// </para>
	/// <para>
	/// <b>The picture keeps its shape.</b> The viewport is 1828x1331 and the image is square, so filling
	/// the viewport would stretch the park sideways by a third. It is fitted by whichever side runs out
	/// first instead, which is what <see cref="VirtualScreen"/> already does with the whole interface.
	/// </para>
	/// <para>
	/// <b>How far a step and a zoom go is chosen, and marked as chosen.</b> The original has these six
	/// buttons and nothing traced says what one press is worth, so a press moves an eighth of the
	/// visible width and the zoom doubles between 1x and 4x.
	/// </para>
	/// </summary>
	private sealed class MapView : UiControl
	{
		/// <summary>How much of the picture a press moves, as a fraction of what is on screen - a choice.</summary>
		private const float Step = 0.125f;

		private const float MinZoom = 1f;
		private const float MaxZoom = 4f;

		private Texture? _map;
		private bool _tried;

		private float _zoom = MinZoom;
		private float _x = 0.5f;
		private float _y = 0.5f;

		internal void Scroll( int across, int down )
		{
			var visible = 1f / _zoom;

			_x = Math.Clamp( _x + (across * Step * visible), visible * 0.5f, 1f - (visible * 0.5f) );
			_y = Math.Clamp( _y + (down * Step * visible), visible * 0.5f, 1f - (visible * 0.5f) );
		}

		internal void Zoom( bool In )
		{
			_zoom = Math.Clamp( In ? _zoom * 2f : _zoom * 0.5f, MinZoom, MaxZoom );

			// Re-centre inside the new window, so zooming out at an edge does not leave the view off the
			// picture.
			Scroll( 0, 0 );
		}

		internal void Release()
		{
			_map?.Delete();
			_map = null;
		}

		protected override void OnDraw()
		{
			if ( Load() is not { } map )
				return;

			var visible = 1f / _zoom;
			var uvs = new Rectangle( _x - (visible * 0.5f), _y - (visible * 0.5f), visible, visible );

			// Fitted rather than stretched - see the class remarks. The picture is square, the viewport
			// is not, so the shorter side decides and the rest is left as it was.
			var area = Pixels;
			var side = MathF.Min( area.Width, area.Height );
			var placed = new Rectangle(
				area.X + ((area.Width - side) * 0.5f),
				area.Y + ((area.Height - side) * 0.5f),
				side, side );

			Material.UI.Set( "Color", map );

			using ( _ = new Graphics.Scope( Screen.Size ) )
				Graphics.Quad( placed, uvs, Material.UI );
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
