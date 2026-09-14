namespace OpenTPW.UI;

/// <summary>
/// The park's management gadget - the arm along the bottom-left corner with the date, the visitors'
/// happiness gauge and the six round buttons.
///
/// <para>
/// <b>Where the layout comes from.</b> FUN_004a1d70 builds it from the compiled layout stream at
/// 0x00752940, one of six panels FUN_00489f50 loads when a park's interface is assembled. Every
/// rectangle and every id below is that stream's, walked opcode by opcode, and every mesh is named by
/// the hash the stream carries: the original files each ui.wad model under the name of its first mesh
/// node and asks for it as <c>h = (c ^ h) * 47</c> from zero, so 0x2135e13d is <c>b_buy</c> and
/// 0x1beb0695 is <c>base</c>, which is the model shipped as <c>mainpanel.md2</c>. The stream also gives
/// each control its row of UIHELPTEXT.str, which is what the help bar says over it.
/// </para>
/// <para>
/// <b>It is six category pickers, not seventeen buttons.</b> The gadget's handler FUN_004a0840 sends
/// three of the six into FUN_004a0940, which opens <i>the screen that category was last left on</i> out
/// of a remembered-tab global - buy or hire, four information screens, four finance screens. The other
/// three go straight to one thing each. That is why the seventeen keyboard shortcut actions never
/// reconciled with the gadget's button count: they were never meant to. The help rows say the same
/// thing in the game's own words - 469 is "buy and build attractions or to hire staff", 470 "view
/// information screens", 471 "view finance screens".
/// </para>
/// <para>
/// <b>What is here and what is not.</b> The gadget draws, the date is live and the buttons light and
/// click. <b>Five of the six have nothing to open yet</b> - no screen in this game buys an attraction,
/// hires anyone, or shows the finances - so they say so and do nothing, the way the park menu's Load
/// and Save already do. Camcorder mode is the one that works, because it exists. <b>The gauge is not
/// live</b>: it says "Happiness of the park visitors" and there is no such number anywhere yet, so it
/// rests at its lowest part rather than being given an invented one.
/// </para>
/// <para>
/// <b>The golden keys and tickets in the top right are real.</b> The stream's fourth root control,
/// 0x33, holds two rows of icon-and-count - the ticket 0x35 with its number 0x37, the key 0x34 with its
/// number 0x36 - and both numbers already exist here, because a player's gms.dat has carried them since
/// the lobby was built. So this one is not chrome: it shows what the player actually has, in the format
/// the original seeds it with, "0 x". The key row goes away entirely in Instant Action, which is what
/// FUN_004a1d70 does with it, and is why <see cref="FrontEnd.Screens.IslandPanel"/> hides its own.
/// </para>
/// <para>
/// <b>Two of the stream's clusters are deliberately NOT built, for the reason the fold-away arm is
/// not.</b> The aerial (0x2d, 0x2e) is the messages control - its help row is "Right-click to delete ALL
/// messages" - and there is no message bar and nothing to delete. The bank balance beside it (0x2f
/// through 0x32) has no artwork of its own but a currency icon, and a park keeps no balance and no
/// price: 0x30 is the cost of whatever is in your hand, which is why the original starts it hidden and
/// paints it yellow. Built now, each would be a control with nothing behind it - which is exactly what
/// b_retract looked like when it was tried.
/// </para>
/// <para>
/// <b>Engine and content.</b> The controls, the pointer and the help bar are engine. Which buttons a
/// park has, where they sit and what each one does is this file - the park's content, as
/// <see cref="FrontEnd.FrontEnd"/>'s island panel is the lobby's.
/// </para>
/// </summary>
internal sealed class ParkGadget : UiWindow
{
	/// <summary>DATETINY/DATESMALL/DATEBIG - slot 3 is the date's own font in all four resolution sets.</summary>
	private const int DateFont = 3;

	private readonly UiControl _date;

	/// <summary>The camcorder button, which follows the mode rather than keeping its own answer.</summary>
	private readonly UiButton _camcorder;

	/// <summary>
	/// The count beside each icon, in the font the original gives them - FUN_004a1d70 fetches 0x36 and
	/// 0x37 and hands both a label skin, white, and font slot 2. The lobby's key count uses the same
	/// slot for the same reason.
	/// </summary>
	private const int CountFont = 2;

	/// <summary>The key row of the top-right cluster, which Instant Action does without.</summary>
	private readonly UiControl _keyRow;

	private readonly UiControl _keyCount;
	private readonly UiControl _ticketCount;

	public ParkGadget( WindowStack stack ) : base( stack )
	{
		// Not modal and not pausing: the park carries on behind it, as the lobby's island panel lets the
		// lobby carry on. A window that paused here would stop the clock, the calendar and every model.
		Root = new UiControl
		{
			Id = 0x1d,
			Rect = new UiRect( 37, 984, 439, 1507 ),
			Mesh = UiMesh.Get( "mainpanel" )
		};

		// gauge.md2 belongs to THIS control, not to the meter inside it. In the stream the mesh record
		// comes after the op that closes 0x1f's block, and UI_ParseTreeStream binds a mesh to the
		// control whose block is running - its case 1 loads ECX from the invocation's own `this`
		// (0x0065fe75), where case 0 recursed with the new child instead. So once 0x1f has closed, the
		// running block is 0x1e's again.
		var gaugeHousing = Root.Add( new UiControl
		{
			Id = 0x1e,
			Rect = new UiRect( 67, 1080, 163, 1345 ),
			HelpText = 477,
			Mesh = UiMesh.Get( "gauge" )
		} );

		// Control type 9 in the stream, which is a meter: FUN_0066d750 is the plain control with three
		// more fields and a 2000ms timer (FUN_004a1cd0) driving it. It carries no mesh at all - the
		// original hands it a meter.wct skin instead (FUN_00477870, from FUN_004a1d70) - and nothing
		// measures visitors' happiness yet, so the moving part is left unbuilt rather than invented.
		gaugeHousing.Add( new UiControl
		{
			Id = 0x1f,
			Rect = new UiRect( 85, 1100, 144, 1324 )
		} );

		_date = Root.Add( new UiControl
		{
			Id = 0x20,
			Rect = new UiRect( 153, 1044, 404, 1121 ),
			HelpText = 478,
			Mesh = UiMesh.Get( "date" ),

			// The stream gives the lettering its own rectangle inside the panel.
			TextRect = new UiRect( 182, 1061, 383, 1103 ),
			Font = DateFont,

			// Black, which is what the original sets and not the interface's usual white: having
			// fetched control 0x20, FUN_004a1d70 calls the font setter with slot 3 and then the colour
			// setter with (0, 0, 0, 0xff) - `PUSH 0xff` over three pushes of a register holding zero,
			// at 0x004a2529. The panel's white labels go through the same call with 0xffffffff.
			TextColour = UiColour.Black
		} );

		// The stream also carries the arm the gadget folds away on (0x21, with 0x22/0x23 inside it) and
		// the button that folds it (0x24, "Left-click to close this arm"). None of it is built here: the
		// arm exists to uncover the message bar and the rest of the interface, and none of that is built
		// either, so an arm would fold away nothing. Its retract button was drawn once while this was
		// being written and it showed as a red cross - the disabled part of b_retract - floating clear of
		// the panel, which is exactly what a control with nothing behind it looks like.

		var buttons = Root.Add( new UiControl
		{
			Id = 0x25,
			Rect = new UiRect( 170, 1122, 392, 1372 )
		} );

		// The six, in the stream's own order. Each one's help row is the game's description of it.
		buttons.Add( NotYet( 0x26, new UiRect( 170, 1122, 288, 1240 ), 469, "b_buy",
			"Buy and build", "nothing buys an attraction or hires anyone yet" ) );

		// The one that works: camcorder mode already exists, and this is the button the original puts it
		// on - id 99 of the sub-panel FUN_00498b50 builds, reached from this button's press. The mode
		// has no toggle of its own because the C key asks it the same question from the camera side, so
		// this asks it here rather than a second way of holding the answer being invented.
		_camcorder = buttons.Add( new UiButton
		{
			Id = 0x27,
			Rect = new UiRect( 202, 1355, 320, 1474 ),
			HelpText = 474,
			Mesh = UiMesh.Get( "b_camera" ),
			Toggles = true,
			Clicked = ToggleCamcorder
		} );

		buttons.Add( NotYet( 0x28, new UiRect( 287, 1133, 405, 1252 ), 470, "b_info",
			"Information", "there are no park, staff, item or visitor screens yet" ) );

		buttons.Add( NotYet( 0x29, new UiRect( 81, 1340, 200, 1458 ), 473, "b_map",
			"Map", "the park map screen is not built" ) );

		buttons.Add( NotYet( 0x2a, new UiRect( 156, 1241, 274, 1360 ), 471, "b_money",
			"Finances", "a park keeps no running balance yet" ) );

		buttons.Add( NotYet( 0x2b, new UiRect( 274, 1254, 392, 1372 ), 472, "b_resrch",
			"Research", "there is nothing to research and nobody to research it" ) );

		// The stream's fourth root control, away in the top right corner of the screen rather than on the
		// panel - so it is added to the window's root, not to the gadget body, and it anchors top-right on
		// its own. Two rows: the ticket above, the key below, each an icon with its count to the left.
		var earned = Root.Add( new UiControl
		{
			Id = 0x33,
			Rect = new UiRect( 1688, 48, 1963, 278 )
		} );

		// The ticket, which every player has whether or not they have earned any.
		earned.Add( new UiControl
		{
			Id = 0x35,
			Rect = new UiRect( 1853, 54, 1955, 156 ),
			Mesh = UiMesh.Get( "gtick" )
		} );

		_ticketCount = earned.Add( Count( 0x37, new UiRect( 1688, 60, 1829, 150 ) ) );

		// The key row, which FUN_004a1d70 hides outright while the game type is 2 - Instant Action, where
		// keys mean nothing because every park is already open.
		_keyRow = earned.Add( new UiControl
		{
			Id = 0x1e0f0,
			Rect = new UiRect( 1688, 165, 1963, 268 )
		} );

		_keyRow.Add( new UiControl
		{
			Id = 0x34,
			Rect = new UiRect( 1853, 165, 1955, 268 ),
			Mesh = UiMesh.Get( "gkey" ),

			// gkey.md2 carries four frames - gkey0 to gkey3 in ui.wad - and the original asks for the
			// last of them here (FUN_0065d3a3 with 3), as the lobby's island panel does with the same
			// model.
			Frame = 3
		} );

		_keyCount = _keyRow.Add( Count( 0x36, new UiRect( 1688, 171, 1829, 261 ) ) );

		ShowDate();
		ShowEarned();
	}

	/// <summary>
	/// One of the two counts in the top right corner: white, font slot 2, and pushed up against the icon
	/// it belongs to, which is what <see cref="FrontEnd.Screens.IslandPanel"/> does with the lobby's.
	/// </summary>
	private static UiControl Count( int id, UiRect rect )
		=> new()
		{
			Id = id,
			Rect = rect,
			Font = CountFont,
			TextAcross = TextAlign.End,
			TextWraps = true
		};

	/// <summary>
	/// One of the five buttons whose screen does not exist. It draws and lights and clicks like the
	/// others and then says why nothing happened, which is what <see cref="ParkFrontEnd"/>'s menu does
	/// for the choices it cannot answer. Leaving them out would misreport the gadget's shape, and
	/// letting them look as though they had worked would be worse than either.
	/// </summary>
	private static UiButton NotYet( int id, UiRect rect, int helpText, string mesh, string what, string why )
		=> new()
		{
			Id = id,
			Rect = rect,
			HelpText = helpText,
			Mesh = UiMesh.Get( mesh ),
			Clicked = () => Log.Info( $"Park gadget: {what} - {why}, so nothing more happens" )
		};

	/// <summary>
	/// In and out of the first-person view, which is what the original's button id 99 does through
	/// FUN_00481a10. <see cref="ParkCamcorderCameraMode"/> keeps whether it is on, so this asks it
	/// rather than keeping a second answer that could disagree with the C key's.
	/// </summary>
	private static void ToggleCamcorder()
	{
		if ( ParkCamcorderCameraMode.Active )
			ParkCamcorderCameraMode.Leave();
		else
			ParkCamcorderCameraMode.Enter();
	}

	protected internal override void Update()
	{
		ShowDate();
		ShowEarned();

		// The C key reaches the same mode without going through this button, so the button follows the
		// mode rather than the other way about.
		_camcorder.IsDown = ParkCamcorderCameraMode.Active;

		// Put the gadget away in first person, because camcorder mode steers the view from where the
		// pointer IS rather than from how it moves: outside a dead zone of 0.4 the view turns at up to
		// 2 radians a second, and every one of this panel's controls is inside that band at the bottom
		// left of any window. Left up, reaching for a button would sweep the view round - about a
		// quarter turn during a two-second reach - and leaving the mode hands that yaw to the orbit
		// camera, so the park would come back facing somewhere nobody chose.
		//
		// The original does not solve this by suppressing the steering: entering camcorder mode
		// (FUN_00481a10) neither hides the interface nor changes its page. It puts a VIEWFINDER over
		// the screen instead - its own layout stream at 0x0074fa98, a full-frame surround with an
		// eject button in the far corner - which is the first-person interface in place of this one.
		// That is not built yet, so this hides and the C key remains the way out. Hidden rather than
		// closed, which is the stack's own word for a window that is neither drawn nor pointed at.
		Hidden = ParkCamcorderCameraMode.Active;
	}

	/// <summary>
	/// The date the park's calendar has reached.
	///
	/// <para>
	/// <b>The format is an approximation, and knowingly one.</b> The original fills this control in
	/// FUN_004a0e30: it builds the text through FUN_006acca0 from a compiled resource - entry 0x1f of
	/// the bundle at DAT_0078ba94 - handed three numbered arguments typed 0x10, 0x11 and 0x12, which
	/// are the day, the month and the year. So the layout of the date is <b>the game's own localised
	/// resource</b>, not the machine's setting: the executable imports no GetDateFormat at all, and
	/// GetLocaleInfoA/W appear only inside CRT locale code.
	/// </para>
	/// <para>
	/// An earlier version of this comment claimed the opposite - that the original asked the machine,
	/// so a short date here was "the same rule rather than the same output". <b>That was wrong</b>, and
	/// it came of searching for a printf-style format string and concluding from its absence. The
	/// resource's own text has not been read back yet - it is not a row of any shipped .str - so what
	/// it looks like is still unknown, and this shows a short date until it is. That is a stand-in, not
	/// a match.
	/// </para>
	/// </summary>
	private void ShowDate() => _date.Text = GameCalendar.Now.ToShortDateString();

	/// <summary>
	/// The golden keys and tickets the player has earned, read from their gms.dat the way the lobby's
	/// island panel reads them - see <see cref="FrontEnd.Screens.IslandPanel.ShowKeys"/>.
	///
	/// <para>
	/// The format is the original's own: the string FUN_004a1d70 seeds both counts with, at 0x00752f24,
	/// is "0 x", so the number leads and the multiplication sign follows it. Nobody being picked yet -
	/// a park reached from the debug console rather than through the lobby - reads as none rather than
	/// as an error, because the count is a fact about a player and there is no player.
	/// </para>
	/// </summary>
	private void ShowEarned()
	{
		var player = Players.Roster.Current;

		_keyRow.Visible = player is not { InstantAction: true };
		_keyCount.Text = $"{player?.Keys ?? 0} x";
		_ticketCount.Text = $"{player?.Tickets ?? 0} x";
	}
}
