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
/// click. <b>Each of the six opens something</b>: buy and hire, the map, the camcorder arm, the
/// Information category's staff/items/visitors lists, the Money category's entry price, and Research, which is
/// not a category: its opener's two message boxes are built and its six effort sliders are not
/// (<see cref="ParkResearchOpener"/>). <b>The gauge is live.</b> There is a happiness on every guest, and
/// FUN_004c7bb0 is what the original does with them - average them, and read nought while the park is
/// shut, which is what still leaves it resting at its lowest part in a park nobody has entered.
/// </para>
/// <para>
/// <b>The golden keys and tickets in the top right are real.</b> The stream's fourth root control,
/// 0x33, holds two rows of icon-and-count - the ticket 0x35 with its number 0x37, the key 0x34 with its
/// number 0x36 - and both numbers already exist here, because a player's gms.dat has carried them since
/// the lobby was built. So this one is not chrome: it shows what the player actually has, in the format
/// the original seeds it with, "0  x" (two spaces; <c>docs/exe/hud.md</c>, "How FUN_004a1d70 populates the clusters"). The
/// key row goes away entirely in Instant Action, which is what FUN_004a1d70 does with it, and is why
/// <see cref="IslandPanel"/> hides its own; it goes away at nought keys too, as the ticket does at nought tickets.
/// </para>
/// <para>
/// <b>The arm is a carrier, not a lid.</b> 0x21, with 0x22 and 0x23 on its far end and the retract
/// button 0x24 beside them. Nothing reaches it except through FUN_004a2590, and every one of that
/// function's five callers hands it a panel of its own to hold - so the arm is how this gadget shows any
/// sub-panel at all, and folding it away is how each of them is put back. The one whose contents exist
/// today is the camcorder panel, stream 0x00751720, which the camcorder button puts on it and either of
/// that panel's two buttons takes off again. <b>It slides</b> (the arm's handler, 0x004a14f0): ten steps 20 ms apart
/// on the frame clock, out with a panel and in, each part of the panel shown as its right edge passes the body's, a
/// panel asked for meanwhile waiting for the arm to come in (<see cref="Carry"/>, <see cref="Slide"/>).
/// </para>
/// <para>
/// <b>The message bar</b> has a stream of its own at 0x007501a0 - a messagel/messagem/messager three-slice with
/// two stacked buttons and an mb_erase button under them - and the arm carries it as it carries the camcorder
/// panel, 820 out where the bar is 615 wide. A tag's left click puts its message on it (FUN_0048d9c0): the tag's
/// text, its row's two buttons, of which mb_locate puts the camera on the tag's thing, and the erase button, which
/// takes the tag off. The same tag clicked again, its right click, its going or the retract button folds the arm
/// away (docs/exe/hud.md, "The message bar").
/// </para>
/// <para>
/// <b>The aerial (0x2d, 0x2e) is the messages control.</b> Each tag a said line leaves (<see cref="ParkTags"/>)
/// is a msgtag icon on the mast, which lifts the red top by the icon's height and ten (LAB_004a11e0, 0x100001); a
/// right click on a tag hides it and takes it off half a second on, and one on the top deletes them all
/// (FUN_004a1190) - its help row says so. A tag's left click opens the message bar, and the tag on show swells
/// and shrinks (the stream at 0x007502b8). The top and the tags above one taken off are put down at once, as the
/// original's are, and then squash and spring back on its streams at 0x007502f0 and 0x00752e48 (<see cref="Squash"/>).
/// </para>
/// <para>
/// <b>The body, the arm and the aerial stop the pointer</b>, as every shown control of the original's does: the body
/// inside its 23-point outline, the handle inside its 16-point one, the others over their rectangles. A press there
/// never reaches the park (docs/exe/park-engine.md, "Whose a right press is").
/// </para>
/// <para>
/// <b>The bank balance beside it IS built.</b> 0x2f is the lettering and 0x32 the currency icon, and the
/// number is <see cref="ParkState.Balance"/>: the save's own <c>mBalance</c>, moved by every admission
/// fee, charge, cost and refund - one number, as in the original (see <see cref="ShowMoney"/>). What is
/// still out is <b>0x30</b>, the cost of whatever is
/// in your hand, which the original starts hidden and paints yellow because an empty hand has no price;
/// and <b>0x31</b>, the trend arrow, not built yet (Q674). Both are written out where the cluster is built.
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

	/// <summary>The camcorder button, which stays down for as long as the arm it opens is out.</summary>
	private readonly UiButton _camcorder;

	/// <summary>The arm, and the panel it carries out of the gadget - see the class remarks.</summary>
	private readonly UiControl _arm;

	/// <summary>The arm's end 0x22, which carries the handle 0x23 and keeps to the arm's right edge.</summary>
	private readonly UiControl _armEnd;

	private readonly UiButton _retract;
	private readonly UiControl _carried;

	/// <summary>The message bar 0x10d2d, its text's panel 0x10d32 and its buttons 0x10d30, 0x10d2f and 0x10d2e.</summary>
	private readonly UiControl _bar;
	private readonly UiControl _barText;
	private readonly UiButton _barFirst;
	private readonly UiButton _barSecond;
	private readonly UiButton _barErase;

	/// <summary>What the arm carries, out or on its way out or in (its data word 1): a panel, or nothing with the arm at rest in.</summary>
	private UiControl? _onArm;
	private readonly UiMesh? _armMesh;
	private readonly UiMesh? _armEndMesh;

	/// <summary>
	/// How far the arm's right edge comes in at the build: FUN_004a1d70 adds the arm's left less the handle's, 331 - 976, to the arm's
	/// right (0x004a23da) and sets that rectangle, so the arm is built 30 wide and its end and handle, which follow their
	/// parent's right edge (flag 0x20000, FUN_0065c9bd), sit 645 to the left of the stream's place for them.
	/// </summary>
	private const int ArmTravel = 645;

	/// <summary>The arm's left, top and bottom and the body's right edge, which a carried panel's place and travel are figured from.</summary>
	private const int ArmLeft = 331;
	private const int ArmTop = 1069;
	private const int ArmBottom = 1495;
	private const int BodyRight = 439;

	/// <summary>The arm's depth, behind the body's 8, and its end's.</summary>
	private const int ArmDepth = 4;

	/// <summary>
	/// The depth of a panel the arm carries: the arm's 0xa links it under the arm, which gives it the arm's depth and two
	/// (FUN_0065f447), then sets it three less (0x004a15bf to 0x004a15cd, FUN_0065f16b), so its parts at 5 pass behind the
	/// body (8), the retract button (7) and the handle (9) as it slides, and theirs at 7 tie the retract button.
	/// </summary>
	internal const int CarriedDepth = ArmDepth + 2 - 3;

	/// <summary>
	/// How far the arm goes out with a panel this wide: ten steps of a tenth of its width, the body's right edge less the
	/// arm's left, and a hundred, a whole division (<c>0x004a16d7</c>).
	/// </summary>
	internal static int TravelFor( int width ) => (width + BodyRight - ArmLeft + 100) / 10 * 10;

	/// <summary>
	/// Where a carried panel stands with the arm out: its left the arm's left less its own width and fifty, then the
	/// travel; its middle on the arm's, each half a whole division (<c>0x004a1613</c>).
	/// </summary>
	internal static (int Left, int Top) PlaceFor( UiRect panel )
		=> (ArmLeft - panel.Width - 50 + TravelFor( panel.Width ), ArmTop + (ArmBottom - ArmTop) / 2 - panel.Height / 2);

	/// <summary>The locators' two panels as their stream has them, <c>0x95c</c> and <c>0x963</c>.</summary>
	private static readonly UiRect LocatorStaffStream = new( 474, 1159, 941, 1470 );
	private static readonly UiRect LocatorVisitorStream = new( -37, 1159, 429, 1470 );

	/// <summary>The staff and visitor locators, whose panels this arm carries (Ctrl+S and Ctrl+V).</summary>
	internal ParkLocator Locator { get; }

	/// <summary>How far out the arm's right edge stands now, from the builder's place for it.</summary>
	private int _armTravel;

	/// <summary>Whether the arm carries a panel, out or on its way out or in (its data word 1).</summary>
	internal bool ArmOut => _onArm != null;

	/// <summary>Whether the arm is out with the message bar on it.</summary>
	internal bool BarOut => _onArm == _bar;

	/// <summary>The body's outline - the stream's op 4 sub-op 4 on 0x1d, 23 points at 0x00752ac8.</summary>
	private static readonly UiPoint[] BodyOutline =
	[
		new( 167, 1019 ), new( 258, 1014 ), new( 327, 1015 ), new( 396, 1022 ), new( 415, 1032 ), new( 428, 1057 ),
		new( 439, 1311 ), new( 428, 1343 ), new( 413, 1365 ), new( 389, 1383 ), new( 358, 1391 ), new( 344, 1410 ),
		new( 344, 1499 ), new( 255, 1507 ), new( 150, 1500 ), new( 102, 1482 ), new( 75, 1461 ), new( 48, 1409 ),
		new( 37, 1311 ), new( 64, 1071 ), new( 72, 1046 ), new( 84, 984 ), new( 155, 984 )
	];

	/// <summary>The handle's outline with the arm out - the stream's op 4 sub-op 4 on 0x23, 16 points at 0x00752a42.</summary>
	private static readonly UiPoint[] HandleOutline =
	[
		new( 1060, 1069 ), new( 1071, 1058 ), new( 1093, 1106 ), new( 1113, 1183 ), new( 1129, 1310 ), new( 1120, 1405 ),
		new( 1090, 1461 ), new( 1056, 1486 ), new( 1015, 1503 ), new( 976, 1503 ), new( 976, 1406 ), new( 981, 1392 ),
		new( 992, 1382 ), new( 1024, 1373 ), new( 1045, 1358 ), new( 1062, 1336 )
	];

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
	private readonly UiControl _ticket;

	/// <summary>The ticket count last written to the corner, -1 before the first (0x00752ec4).</summary>
	private int _ticketsWritten = -1;

	/// <summary>The key count last written to the corner, -1 before the first (0x00752ec8).</summary>
	private int _keysWritten = -1;

	/// <summary>
	/// The gauge's moving part - the stream's 0x1f, control type 9. See <see cref="UiMeter"/> for why it
	/// takes a skin rather than a mesh, and <see cref="ParkPeople.AverageHappiness"/> for the number.
	/// </summary>
	private readonly UiMeter _happiness;

	/// <summary>
	/// The bank balance in the top-left corner - the stream's 0x2f, the lettering itself. Its field is
	/// sized in the original by measuring the string "999999999" (0x00752f10) rather than being fixed, so
	/// nine digits is what it is built to hold.
	/// </summary>
	private readonly UiControl _balance;

	/// <summary>
	/// The balance's own font slot - <c>FUN_004a1d70</c> gives 0x2f <c>FUN_00485a70(1)</c>, where the two
	/// counts in the opposite corner get slot 2 and the date gets slot 3.
	/// </summary>
	private const int BalanceFont = 1;

	public ParkGadget( WindowStack stack ) : base( stack )
	{
		// Not modal and not pausing: the park carries on behind it, as the lobby's island panel lets the
		// lobby carry on. A window that paused here would stop the clock, the calendar and every model.
		//
		// The root is a bare container for the stream's four roots: the body, the aerial, the balance and the
		// earned cluster.
		Root = new UiControl { Rect = VirtualScreen.Whole };

		// The gadget body, 0x1d. It takes the pointer inside its outline, and everything under it is drawn in
		// order of depth, because the arm lies BEHIND the body it is a child of. FUN_004a1d70 gives each control a
		// depth through FUN_0065f16b, which writes it to the control's own +0xe0 and hands children two more; the
		// receivers are legible only in the disassembly, because the setter is __fastcall:
		//
		//     0x21 arm 4   0x22 end 4   0x24 b_retract 7   0x1d body 8   0x23 handle 9   the rest 10 and up
		//
		// Higher is nearer the front, which a screenshot settles: the six buttons sit at 10 inside a body at 8 and
		// plainly draw over it. The children are in the stream's order, which is the hit test's: the last child the
		// point is in answers (FUN_0065db25), so the six buttons' panel before the arm, the arm before the date and
		// the gauge, and any of them before the body.
		var body = Root.Add( new UiControl
		{
			Id = 0x1d,
			Rect = new UiRect( 37, 984, 439, 1507 ),
			Mesh = UiMesh.Get( "mainpanel" ),
			Outline = BodyOutline,
			Depth = 8,
			DrawsByDepth = true
		} );

		// gauge.md2 belongs to THIS control, not to the meter inside it. In the stream the mesh record
		// comes after the op that closes 0x1f's block, and UI_ParseTreeStream binds a mesh to the
		// control whose block is running - its case 1 loads ECX from the invocation's own `this`
		// (0x0065fe75), where case 0 recursed with the new child instead. So once 0x1f has closed, the
		// running block is 0x1e's again.
		var gaugeHousing = body.Add( new UiControl
		{
			Id = 0x1e,
			Rect = new UiRect( 67, 1080, 163, 1345 ),
			HelpText = 477,
			Mesh = UiMesh.Get( "gauge" )
		} );

		// Control type 9 in the stream, which is a meter: FUN_0066d750 is the plain control with three
		// more fields, and Gadget_GaugeOnMessage arms a 2000ms timer - id 0x80083 - that fills it. It carries no
		// mesh at all, because the original hands it a meter.wct skin instead (Meter_Construct, from
		// FUN_004a1d70), which is why a control that draws only a mesh shows nothing here.
		//
		// What the timer asks for is
		// FUN_004c7bb0 - the mean happiness of the park's guests, and nought while the park is shut. See
		// ParkPeople.AverageHappiness for the arithmetic and for the one term of it that is deliberately
		// not reproduced, and UiMeter for the skin's two halves.
		_happiness = gaugeHousing.Add( HappinessGauge() );

		_date = body.Add( new UiControl
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

		// The arm, which the gadget carries a sub-panel out on: 0x21 with 0x22 inside it, 0x23 inside that
		// and the retract button 0x24 beside them. It is a carrier, not a lid - FUN_004a2590 is the only
		// way anything gets onto it, and each of its five callers hands it a panel of its own to hold.
		//
		// Built here at the stream's rectangles, the arm out, and put in below as the original's builder does.
		_arm = body.Add( new UiControl
		{
			Id = 0x21,
			Rect = new UiRect( 331, 1069, 1006, 1495 ),
			Mesh = _armMesh = UiMesh.Get( "panel" ),
			Depth = ArmDepth,
			StopsPointer = true,

			// It reaches out past the body, so UiControl.Add will not let it follow the body's corner and
			// VirtualScreen.AnchorFor would work its own out from its middle instead. That middle is 668
			// across and the left third ends at 683, so it would come out left anyway - but by fourteen
			// units, and an arm bolted to the body should not be held on by a rounding. Its two end pieces
			// are worse: both their middles fall in the middle third, so each would centre itself and the
			// arm would come apart across a wide window.
			PinAcross = Anchor.Left,
			PinDown = VerticalAnchor.Bottom
		} );

		// The far end of the arm, and the handle on the end of that. panel.md2 and panelend.md2 carry FOUR
		// parts each and they are not animation frames: they are pan_money/pan_info/pan_buy/pan_staff and
		// the matching pane_*, so the arm wears the colour of whichever category it is carrying. Nothing on
		// the camcorder path sets a part, so both stay on part 0 - which is the one the layout stream names
		// each model by, and so the one its mesh hash resolves to.
		_armEnd = _arm.Add( new UiControl
		{
			Id = 0x22,
			Rect = new UiRect( 1006, 1069, 1071, 1382 ),
			Mesh = _armEndMesh = UiMesh.Get( "panelend" ),
			Depth = ArmDepth,
			StopsPointer = true,
			PinAcross = Anchor.Left,
			PinDown = VerticalAnchor.Bottom
		} );

		// The handle, in front of the body: with the arm in it is what shows past the body's right edge.
		_armEnd.Add( new UiControl
		{
			Id = 0x23,
			Rect = new UiRect( 976, 1058, 1129, 1503 ),
			Mesh = UiMesh.Get( "handle" ),
			Outline = HandleOutline,
			Depth = 9,
			PinAcross = Anchor.Left,
			PinDown = VerticalAnchor.Bottom
		} );

		// "Left-click to close this arm" - UIHELPTEXT row 481. It goes away with the arm, because it is
		// inside it, which is also how it comes to follow the arm's corner without being told to.
		//
		// Coming in the original switches this button off, calling the control's vtable+0x14 with 0 exactly
		// as it does for the readouts that are shown but never clicked, and it stays drawn through the slide
		// until the handle stands over it; here it is switched off through the slide and hidden at rest in
		// (see BuildIn), and switched off it still stops the pointer, doing nothing but its glint and help
		// row 481 (Q66), where the original's hit test passes over it. The red cross it shows is not the disabled part of b_retract: b_retract.md2's six
		// nodes are b_retract, disable, hilite, hidown, helddown and down - UiButton's own order - so part
		// 0 is the NORMAL look, and a button that closes something simply looks like a cross.
		_retract = _arm.Add( new UiButton
		{
			Id = 0x24,
			Rect = new UiRect( 349, 1386, 429, 1466 ),
			HelpText = 481,
			Mesh = UiMesh.Get( "b_retract" ),
			Depth = 7,
			Clicked = () => CloseArm()
		} );

		// What the arm carries, out of the camcorder panel's own stream at 0x00751720, which FUN_00498b50
		// builds and parks hidden until FUN_00498bb0 hands it over. Both its buttons close the arm behind
		// them: FUN_00498ad0 acts on the click, withdraws the advisor's line 0x131, and then calls
		// FUN_004a25f0(1) - and that argument is what lifts the camcorder button again.
		var carried = _carried = _arm.Add( new UiControl
		{
			Id = 0x62,
			Rect = new UiRect( 466, 1316, 785, 1469 ),
			Depth = CarriedDepth
		} );

		carried.Add( new UiButton
		{
			Id = 0x63,
			Rect = new UiRect( 474, 1316, 627, 1469 ),
			HelpText = 479,
			Mesh = UiMesh.Get( "b_1person" ),
			Clicked = EnterCamcorder
		} );

		carried.Add( new UiButton
		{
			Id = 0x64,
			Rect = new UiRect( 632, 1316, 785, 1469 ),
			HelpText = 480,
			Mesh = UiMesh.Get( "b_postcard" ),
			Clicked = SendPostcard
		} );

		// The message bar, stream 0x007501a0 (FUN_0048d8b0), at the place the arm's handler gives it out: its left
		// the arm's left less its own width and fifty, then the arm's 820, and its middle on the arm's (0x004a1613),
		// so (486,1112) where the stream has (458,1101). Three slices, the middle one a label in font 6 whose text
		// rectangle is the stream's; the two stacked buttons carry no mesh until a message is shown.
		_bar = _arm.Add( new UiControl
		{
			Id = 0x10d2d,
			Rect = new UiRect( 486, 1112, 1101, 1453 ),
			Depth = CarriedDepth,
			PinAcross = Anchor.Left,
			PinDown = VerticalAnchor.Bottom
		} );

		// Each is pinned to the body's corner, as the arm's own pieces are: half the bar lies in the screen's middle third.
		_bar.Add( new UiControl { Id = 0x10d31, Rect = new UiRect( 486, 1112, 651, 1453 ), Mesh = UiMesh.Get( BarLeftMesh ), PinAcross = Anchor.Left, PinDown = VerticalAnchor.Bottom } );

		// The right slice goes in before the middle one, whose lettering runs on over it: the stream has it after.
		_bar.Add( new UiControl { Id = 0x10d33, Rect = new UiRect( 863, 1112, 1101, 1453 ), Mesh = UiMesh.Get( BarRightMesh ), PinAcross = Anchor.Left, PinDown = VerticalAnchor.Bottom } );

		_barText = _bar.Add( new UiControl
		{
			Id = 0x10d32,
			Rect = new UiRect( 651, 1112, 863, 1453 ),
			Mesh = UiMesh.Get( BarMiddleMesh ),
			TextRect = new UiRect( 637, 1129, 1084, 1433 ),
			Font = BarFont,
			TextAcross = TextAlign.Start,
			TextDown = TextAlign.Start,
			TextWraps = true,
			PinAcross = Anchor.Left,
			PinDown = VerticalAnchor.Bottom
		} );

		_barFirst = _bar.Add( new UiButton { Id = 0x10d30, Rect = new UiRect( 514, 1121, 617, 1224 ), Clicked = () => BarButton( first: true ), PinAcross = Anchor.Left, PinDown = VerticalAnchor.Bottom } );
		_barSecond = _bar.Add( new UiButton { Id = 0x10d2f, Rect = new UiRect( 514, 1229, 617, 1331 ), Clicked = () => BarButton( first: false ), PinAcross = Anchor.Left, PinDown = VerticalAnchor.Bottom } );
		_barErase = _bar.Add( new UiButton { Id = 0x10d2e, Rect = new UiRect( 514, 1336, 617, 1439 ), Mesh = UiMesh.Get( BarEraseMesh ), Clicked = EraseTag, PinAcross = Anchor.Left, PinDown = VerticalAnchor.Bottom } );

		// The staff and visitor locators' two panels, which FUN_004b3cf0 builds hidden and FUN_004b3f60 and FUN_004b4280
		// hand the arm, each put where the arm's handler puts it out, and their strip above the arm.
		Locator = new ParkLocator( _arm, Root,
			(PlaceFor( LocatorStaffStream ).Left - LocatorStaffStream.Left, PlaceFor( LocatorVisitorStream ).Left - LocatorVisitorStream.Left),
			PlaceFor( LocatorStaffStream ).Top - LocatorStaffStream.Top );
		Locator.OpensVisitorWindow = guest => ParkVisitorWindow.Open( Stack, guest );
		Locator.OpensStaffWindow = member => ParkStaffWindow.Open( Stack, member );

		BuildIn();

		// The six round buttons' panel, 0x25: the last of the body's children, so it answers before the arm where
		// the two meet.
		var buttons = body.Add( new UiControl
		{
			Id = 0x25,
			Rect = new UiRect( 170, 1122, 392, 1372 ),
			StopsPointer = true
		} );

		// The six, in the stream's own order. Each one's help row is the game's description of it.
		// FUN_004a0940( 1 ) opens whichever of buy and hire that category was last left on, seeded to buy - so
		// buy is what a park opens on, and the hire screen is its sibling rather than a second button. See
		// docs/exe/hud.md.
		buttons.Add( new UiButton
		{
			Id = 0x26,
			Rect = new UiRect( 170, 1122, 288, 1240 ),
			HelpText = 469,
			Mesh = UiMesh.Get( "b_buy" ),
			Clicked = () => ParkCategoryScreens.Open( Stack, ParkCategoryScreens.Building )
		} );

		// It does not enter first person itself. FUN_004a0840's case 0x27 splits on whether the button
		// has just gone down: down calls FUN_00498bb0, which puts the camcorder panel on the arm, and up
		// calls FUN_00498bd0, which takes it off again. The button id 99 that actually enters the mode is
		// on that panel, not here; the C key goes straight there.
		_camcorder = buttons.Add( new UiButton
		{
			Id = 0x27,
			Rect = new UiRect( 202, 1355, 320, 1474 ),
			HelpText = 474,
			Mesh = UiMesh.Get( "b_camera" ),
			Toggles = true,
			Clicked = ShowArm
		} );

		buttons.Add( new UiButton
		{
			Id = 0x28,
			Rect = new UiRect( 287, 1133, 405, 1252 ),
			HelpText = 470,
			Mesh = UiMesh.Get( "b_info" ),
			Clicked = () => ParkCategoryScreens.Open( Stack, ParkCategoryScreens.Information )
		} );

		// The map. FUN_004a0840's case 0x29 goes straight to
		// FUN_005f0b40 rather than through the remembered-tab picker, because the map is not a category
		// - see <see cref="ParkMapScreen"/>, which pauses the park as the original does.
		buttons.Add( new UiButton
		{
			Id = 0x29,
			Rect = new UiRect( 81, 1340, 200, 1458 ),
			HelpText = 473,
			Mesh = UiMesh.Get( "b_map" ),
			Clicked = () => Stack.Open( new ParkMapScreen( Stack ) )
		} );

		// Of this category's four screens only ENTRY PRICE is within reach, and it is the one the
		// category is seeded to - FUN_004a0810(3,10) - so the button opens on it. Finances, loans and
		// staff costs all rest on monthly ring buffers and on loans, which ParkWorld reads and nothing runs, and
		// the entry-price screen carries a button to each of the three regardless, because the original's
		// own screen does. See docs/exe/hud.md.
		buttons.Add( new UiButton
		{
			Id = 0x2a,
			Rect = new UiRect( 156, 1241, 274, 1360 ),
			HelpText = 471,
			Mesh = UiMesh.Get( "b_money" ),
			Clicked = () => ParkCategoryScreens.Open( Stack, ParkCategoryScreens.Finance )
		} );

		// Not a category: FUN_004a0840's case 0x2b goes straight to FUN_004aa480 rather than through the
		// remembered-tab picker, exactly as the map does.
		buttons.Add( new UiButton
		{
			Id = 0x2b,
			Rect = new UiRect( 274, 1254, 392, 1372 ),
			HelpText = 472,
			Mesh = UiMesh.Get( "b_resrch" ),
			Clicked = () => ParkResearchOpener.Open( Stack )
		} );

		// The aerial, the stream's second root: the mast 0x2d (94,918)-(144,984) with the red top 0x2e
		// (77,790)-(161,918) over it. FUN_004a1d70 sets the mast's top eight above its bottom (0x004a2057), and the
		// top follows its parent's top edge (flag 0x40000, FUN_0065c9bd), 58 down. A tag lifts both again: PutTags.
		// Each is pinned to the corner the body keeps to, since neither sits in the body's third of the screen.
		_mast = Root.Add( new UiControl
		{
			Id = 0x2d,
			Rect = new UiRect( MastLeft, MastBottom - 8, MastRight, MastBottom ),
			Mesh = UiMesh.Get( "aerial" ),
			StopsPointer = true,
			PinAcross = Anchor.Left,
			PinDown = VerticalAnchor.Bottom
		} );

		// "Right-click to delete ALL messages" - UIHELPTEXT row 476. FUN_004a1190 answers a right click (0x10006,
		// button 1) by sending the aerial 0x100003, which takes every tag off, a tenth of a second apart.
		_mastTop = _mast.Add( new UiControl
		{
			Id = 0x2e,
			Rect = new UiRect( 77, 848, 161, 976 ),
			HelpText = 476,
			Mesh = UiMesh.Get( "aerialtop" ),
			PinAcross = Anchor.Left,
			PinDown = VerticalAnchor.Bottom,
			RightClicked = () => Tags?.DeleteAll( Time.WallMilliseconds )
		} );

		// The bank balance, in the OPPOSITE corner - the stream's 0x2f and 0x32. Root controls like the
		// cluster below rather than children of the gadget body, so each anchors top-left on its own.
		//
		// The save's economy thing (model 16) carries mBalance and mAdmissionFee, and ParkState's balance
		// starts from mBalance and moves with every admission fee, charge, cost and refund - see ShowMoney.
		_balance = Root.Add( new UiControl
		{
			Id = 0x2f,
			Rect = new UiRect( 258, 60, 720, 260 ),
			Font = BalanceFont,

			// Against the icon rather than adrift in the middle of a wide box, and that is a CHOICE. The
			// original sizes this field by measuring "999999999" and then computes 0x32's rect from the
			// width it got, so the two are placed against each other rather than at fixed points. Nothing
			// here re-flows either, so the stream's own rects are kept and the lettering is pulled to the
			// side the icon is on.
			TextAcross = TextAlign.Start
		} );

		// The currency icon. Always frame 0 here; the original sets frame 1 (dollarsignneg) while the balance is
		// below nought (Gadget_ShowMoney 0x004a0c23). Not built: MONEY_TREND_ARROW in ShowMoney, Q674.
		Root.Add( new UiControl
		{
			Id = 0x32,
			Rect = new UiRect( 37, 58, 242, 262 ),
			Mesh = UiMesh.Get( "i_dollar" )
		} );

		// TWO OF THE CLUSTER ARE DELIBERATELY NOT BUILT, each for a reason rather than for want of a rect.
		//
		// 0x30, the price of whatever is in your hand, (458,273)-(720,363): yellow, font 2, and started
		// HIDDEN by FUN_004a1d70, which is exactly what an empty hand should show. The hand carries
		// things (ParkBuilding.Carry), but what makes the original show 0x30 is not traced.
		//
		// 0x31, the trend arrow (cashtrend: green_up, red_down), a CHILD of 0x2f (0x004a0f05 fetches 0x2f,
		// then 0x31 from it). Gadget_SetDateAndTrend shows the red arrow when the newest closed month's cash
		// in is below its costs (0x004a0f8a), once a day before the month closes; Gadget_ShowMoney moves it
		// to the lettering's end. Not built yet: Q674, from ParkState.MonthCashIn and MonthCosts
		// (docs/exe/hud.md, "The money's trend arrow").

		// The stream's fourth root control, away in the top right corner of the screen rather than on the
		// panel - so it is added to the window's root, not to the gadget body, and it anchors top-right on
		// its own. Two rows: the ticket above, the key below, each an icon with its count to the left.
		var earned = Root.Add( new UiControl
		{
			Id = 0x33,
			Rect = new UiRect( 1688, 48, 1963, 278 )
		} );

		// The ticket, shown with its count while the player holds any unspent (ShowEarned).
		_ticket = earned.Add( new UiControl
		{
			Id = 0x35,
			Rect = new UiRect( 1853, 54, 1955, 156 ),
			Mesh = UiMesh.Get( "gtick" )
		} );

		// Seeded "0  x" (two spaces) as FUN_004a1d70 seeds it (0x00752f24); ShowEarned writes it only on a change.
		_ticketCount = earned.Add( Count( 0x37, new UiRect( 1688, 60, 1829, 150 ) ) );
		_ticketCount.Text = "0  x";

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
		_keyCount.Text = "0  x";

		ShowDate();
		ShowEarned();
		ShowMoney();
		ShowHappiness();
	}

	/// <summary>
	/// One of the two counts in the top right corner: white, font slot 2, and pushed up against the icon
	/// it belongs to, which is what <see cref="IslandPanel"/> does with the lobby's.
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
	/// The camcorder button was clicked, and the arm follows it out or in: down asks the arm for the camcorder panel and
	/// then posts the advisor's message 0x131, refused or not (FUN_00498bb0); up folds away whatever the arm carries and
	/// then withdraws it (FUN_00498bd0).
	///
	/// <para>
	/// It reads the button rather than toggling anything: <see cref="WindowStack"/> has already turned a
	/// switch over by the time the click arrives, so asking whether it is down now is asking what the
	/// click meant. Toggling here as well would put the arm back where it started.
	/// </para>
	/// </summary>
	private void ShowArm()
	{
		if ( _camcorder.IsDown )
		{
			Carry( _carried );

			if ( Park is { AdvisorMessages: { } lines } park )
				lines.Post( ParkAdvisorMessages.CamcorderPanel, park.GameTick );
		}
		else
		{
			Fold( liftsCamcorder: false );
			WithdrawCamcorderLine();
		}
	}

	/// <summary>The advisor's message 0x131 withdrawn (FUN_00486b40), which cuts him off where he is saying it.</summary>
	private void WithdrawCamcorderLine()
		=> Park?.AdvisorMessages?.Withdraw( ParkAdvisorMessages.CamcorderPanel, (long)(GameClock.Now * 1000), () => Advisor.Current?.StopSpeaking() );

	/// <summary>The arm's state, its byte +0x134: at rest, going out (2) or coming in (3).</summary>
	/// <remarks>
	/// The handler's state 4, coming in with the message's word -1, ends by posting the arm's parent 0xb in place of the
	/// panel's 0x1d and sound 0xc2; no sender of 0xb to the arm passes -1 (FUN_004a2590, FUN_004a25f0 and the retract
	/// button's 0x100 pass nought), so it is dead by CODE and not built.
	/// </remarks>
	internal enum ArmMove { Rest, Out, In }

	/// <summary>What the arm is doing now.</summary>
	internal ArmMove Moving { get; private set; }

	/// <summary>The steps left of the slide, the arm's +0x130, ten as one starts.</summary>
	private int _stepsLeft;

	/// <summary>How far each step moves the arm's right edge, the arm's data word 0.</summary>
	private int _step;

	/// <summary>The panel the arm goes out with once it is in, its data word 2.</summary>
	private UiControl? _waiting;

	/// <summary>Milliseconds of the frame clock since the arm's timer last fired.</summary>
	private float _sinceStep;

	/// <summary>The arm's timer 0x80080: ten steps, and an eleventh firing that ends the slide, 20 ms apart.</summary>
	private const int SlideSteps = 10;
	private const float StepMilliseconds = 20f;

	/// <summary>The interface's effects the arm plays: as it starts either way, and as it is in (0x004a1742, 0x004a1947).</summary>
	internal const int ArmStartsSound = 0x1c;
	internal const int ArmInSound = 0xc2;

	/// <summary>
	/// The arm asked for a panel, FUN_004a2590: refused, false, while a panel waits for the arm already; else the arm
	/// is sent in (0xb) with what it carries and posted out with this one (0xa), so a panel takes another's place by a
	/// fold and a slide out again, and the arm empty goes straight out.
	/// </summary>
	internal bool Carry( UiControl panel )
	{
		if ( _waiting != null )
		{
			Log.Info( $"Park gadget: arm: panel {panel.Id:x} refused, panel {_waiting.Id:x} waits" );
			return false;
		}

		SlideIn();
		SlideOut( panel );
		return true;
	}

	/// <summary>A locator's panel handed to the arm (<see cref="Carry"/>); the camcorder button is left as it is.</summary>
	internal void CarryLocatorPanel( UiControl panel ) => Carry( panel );

	/// <summary>
	/// Folds the arm away, FUN_004a25f0: coming in already, the panel waiting is forgotten; else the arm is sent in.
	/// Its one argument lifts the camcorder button, control 0x27, with it; the message bar's own ways in pass nought.
	/// The retract button's click (the arm's 0x100 from 0x24) is the same with the button lifted.
	/// </summary>
	internal void Fold( bool liftsCamcorder = true )
	{
		if ( Moving == ArmMove.In )
			_waiting = null;
		else
			SlideIn();

		if ( liftsCamcorder )
			_camcorder.IsDown = false;
	}

	/// <summary>
	/// Escape with a locator open (<c>FUN_004816b0</c>, the game table's row 0, <c>0x0040c1a0</c>): the arm folded away,
	/// the camcorder button left as it is (<c>FUN_004a25f0( 0 )</c>). False, and nothing done, with none open.
	/// </summary>
	internal bool FoldLocator()
	{
		if ( Locator.Open == ParkLocator.Kind.None )
			return false;

		Fold( liftsCamcorder: false );
		return true;
	}

	/// <summary>
	/// The arm's 0xa, out with a panel (0x004a1535): while it moves the panel waits; at rest the panel is put under the
	/// arm at the arm's left less its own width and fifty, its middle on the arm's, every part of it hidden that does
	/// not reach past the arm's left, which is all of it; the step is a tenth of the panel's width, the body's right
	/// edge less the arm's left, and a hundred; the retract button is on, sound 0x1c plays and the panel is told it
	/// starts out (0x1001d9). Not the original's in one way: the original links the panel at its linked depth (the arm's plus two,
	/// FUN_0065f447) less three, one under the arm's (0x004a15bf..0x004a15cd), and each child two deeper, behind the body and the retract cross; here the panel takes
	/// its parent's depth plus two, so the panel and its children draw in front of both (docs/exe/hud.md, Q445).
	/// </summary>
	private void SlideOut( UiControl panel )
	{
		if ( Moving != ArmMove.Rest )
		{
			_waiting = panel;
			return;
		}

		var (left, top) = PlaceFor( panel.Rect );
		panel.Move( left - TravelFor( panel.Rect.Width ) - panel.Rect.Left, top - panel.Rect.Top );
		ShowPast( panel, ArmLeft );

		_onArm = panel;
		_step = TravelFor( panel.Rect.Width ) / SlideSteps;
		Start( ArmMove.Out );
		_arm.Mesh = _armMesh;
		_armEnd.Mesh = _armEndMesh;
		_retract.Visible = true;
		_retract.Enabled = true;

		StartsOut( panel );
	}

	/// <summary>
	/// The arm's 0xb, in (0x004a1793): nothing with no panel carried; else the panel is told it starts in (0x1001d8),
	/// the step is a tenth of the arm's left less the handle's, from wherever the arm stands, so a slide out turns
	/// back where it is; the retract button is switched off, still drawn, and sound 0x1c plays.
	/// </summary>
	private void SlideIn()
	{
		if ( _onArm is not { } panel )
			return;

		StartsIn( panel );

		_step = -_armTravel / SlideSteps;
		Start( ArmMove.In );
		_retract.Enabled = false;
	}

	private void Start( ArmMove move )
	{
		_stepsLeft = SlideSteps;
		_sinceStep = 0;
		Moving = move;
		var heard = UiSounds.Interface( ArmStartsSound );
		Log.Info( $"Park gadget: arm: {move} with panel {_onArm?.Id:x}, step {_step}, handle {_armEnd.Children[0].Rect.Left}, sound 0x{ArmStartsSound:x} {(heard ? "a voice" : "no voice")}" );
	}

	/// <summary>
	/// The arm's timer 0x80080 over <paramref name="milliseconds"/> of the frame clock (FUN_0065ef90 at 20 ms): it
	/// fires every 20 ms while the arm moves, as many times as the time holds.
	/// </summary>
	internal void Slide( float milliseconds )
	{
		if ( Moving == ArmMove.Rest )
			return;

		_sinceStep += milliseconds;

		while ( Moving != ArmMove.Rest && _sinceStep >= StepMilliseconds )
		{
			_sinceStep -= StepMilliseconds;
			Fire();
		}
	}

	/// <summary>
	/// One firing (0x004a18aa): a step left moves the arm's right edge, the end and handle following it, and the panel
	/// as far (the arm's 0x12), each part of the panel shown only once its right edge is past the body's; none left,
	/// the slide ends. In, the arm is put back at its rest, the panel is hidden and taken off the arm, told 0x1d with
	/// nought, and sound 0xc2 plays; out, the panel is told 0x1d with 1. Then a panel waiting is sent out.
	/// </summary>
	private void Fire()
	{
		if ( _stepsLeft > 0 )
		{
			--_stepsLeft;
			_armTravel += _step;
			_arm.Rect = _arm.Rect with { Right = _arm.Rect.Right + _step };
			_armEnd.Move( _step, 0 );

			if ( _onArm is { } carried )
			{
				carried.Move( _step, 0 );
				ShowPast( carried, BodyRight );
			}

			return;
		}

		var panel = _onArm;
		var move = Moving;
		var heard = false;
		Moving = ArmMove.Rest;

		if ( move == ArmMove.In )
		{
			// The arm put back at its rest, the rectangle the builder left it (0x004a18ef, 0x004a1933): a slide turned
			// back part way leaves less than a tenth of its travel over, and this drops it.
			_arm.Rect = _arm.Rect with { Right = _arm.Rect.Right - _armTravel };
			_armEnd.Move( -_armTravel, 0 );
			_armTravel = 0;

			_onArm = null;
			_arm.Mesh = null;
			_armEnd.Mesh = null;
			_retract.Visible = false;

			if ( panel != null )
				panel.Visible = false;

			heard = UiSounds.Interface( ArmInSound );
		}
		else if ( panel == _bar )
			Tags?.FullyOut();

		Log.Info( $"Park gadget: arm: {(move == ArmMove.In ? "in" : "out")} with panel {panel?.Id:x}, handle {_armEnd.Children[0].Rect.Left}"
			+ (move == ArmMove.In ? $", sound 0x{ArmInSound:x} {(heard ? "a voice" : "no voice")}" : "") );

		if ( _waiting is { } waiting )
		{
			_waiting = null;
			SlideOut( waiting );
		}
	}

	/// <summary>FUN_004a1440: a control and everything in it shown where its right edge is past <paramref name="edge"/>, hidden where not.</summary>
	private static void ShowPast( UiControl control, int edge )
	{
		control.Visible = control.Rect.Right > edge;

		foreach ( var child in control.Children )
			ShowPast( child, edge );
	}

	/// <summary>
	/// A panel told it starts out (0x1001d9). The message bar (FUN_0048e2f0) letters its middle panel with the text,
	/// dresses its two buttons by their types and its erase button, and the tag being put on show becomes the one on
	/// show, swelling from now.
	/// </summary>
	private void StartsOut( UiControl panel )
	{
		if ( panel != _bar || Tags is not { } tags || tags.PuttingOnShow is not { } tag )
			return;

		_barText.Text = tag.Text;
		Dress( _barFirst, tag.Row.Button1 );
		Dress( _barSecond, tag.Row.Button2 );
		var erase = EraseLook( tag.Row.Button2 );
		_barErase.Mesh = erase != null ? UiMesh.Get( erase ) : null;
		_barErase.Visible = erase != null;
		tags.Shown( Time.WallMilliseconds );

		Log.Info( $"Park gadget: message bar: tag {tag.Row.Id} subject {tag.Subject} item {tag.Item} {BarCensus()}" );
	}

	/// <summary>
	/// A panel told it starts in (0x1001d8): the message bar has no tag on show and withdraws the advisor's 0x11c; a
	/// locator's panel closes its locator (FUN_004b3790). The camcorder panel's handler has no arm for it.
	/// </summary>
	private void StartsIn( UiControl panel )
	{
		if ( panel == _bar )
		{
			Tags?.Shut();
			WithdrawBarLine();
			Log.Info( "Park gadget: message bar: off the arm" );
		}
		else if ( panel == Locator.StaffPanel || panel == Locator.VisitorPanel )
			Locator.Folded();
	}

	/// <summary>
	/// Puts the arm in where FUN_004a1d70 builds it, at once: the builder adds the arm's left less the handle's to the
	/// arm's right, so its end and handle stand <see cref="ArmTravel"/> to the left of the stream's place for them and
	/// the handle hugs the body's right edge. Nothing is carried.
	/// </summary>
	/// <remarks>
	/// <b>Not the original's in two ways.</b> At rest in, the arm and its end draw nothing here, where the original draws
	/// them fitted to their narrowed rectangles, as it draws the arm through a slide; every point of both rectangles is
	/// inside the body's outline or the handle's, so nothing of them shows either way. And the retract button is hidden
	/// at rest in, where the original keeps it drawn, switched off, behind the handle (docs/exe/hud.md, "The arm's
	/// handler").
	/// </remarks>
	private void BuildIn()
	{
		_arm.Rect = _arm.Rect with { Right = _arm.Rect.Right - ArmTravel };
		_armEnd.Move( -ArmTravel, 0 );
		_armTravel = 0;

		_arm.Mesh = null;
		_armEnd.Mesh = null;
		_retract.Visible = false;
		_carried.Visible = false;
		_bar.Visible = false;
		Locator.StaffPanel.Visible = false;
		Locator.VisitorPanel.Visible = false;
	}

	/// <summary>Folds the arm away and lifts the camcorder button: FUN_004a25f0( 1 ), and the retract button's click.</summary>
	private void CloseArm( bool liftsCamcorder = true ) => Fold( liftsCamcorder );

	/// <summary>The bar's three slices, the stream's messagel, messagem and messager by their files, and its erase button's.</summary>
	internal const string BarLeftMesh = "f_tagl";
	internal const string BarMiddleMesh = "f_tagm";
	internal const string BarRightMesh = "f_tagr";
	internal const string BarEraseMesh = "mb_erase";

	/// <summary>FONTTINY/SMALL/BIG slot 6, the label skin FUN_0048d8b0 gives the bar's middle panel.</summary>
	private const int BarFont = 6;

	/// <summary>The bar's button that puts the camera on the tag's thing, mb_locate: types 3 and 4 (FUN_0048e610).</summary>
	private const int LocateThing = 3;
	private const int LocateItem = 4;
	private const int EntryPrice = 0x18;
	/// <summary>The bar's button that opens the Staff Training Budgets screen, b_staffcost (FUN_0048e0f0 to FUN_004b2750).</summary>
	private const int StaffCosts = 0x15;
	/// <summary>The bar's button that runs the research opener, mb_resrch (FUN_0048e0f0 to FUN_004aa480).</summary>
	private const int Research = 0x13;
	/// <summary>The bar's buttons that open the buy screen, mb_buy: types 9 to 0xc, a tab each from the first (FUN_0048e0f0).</summary>
	private const int BuyFirstTab = 9;
	private const int BuyLastTab = 0xc;
	/// <summary>The bar's buttons that open the hire screen, mb_buy too: types 0xd to 0x11, a tab each from the cleaners' (FUN_0048e0f0).</summary>
	private const int HireFirstTab = 0xd;
	private const int HireLastTab = 0x11;

	/// <summary>The hire screen's tab a bar button's type opens, 0 the cleaners' to 4 the scientists', null for a type that is no hire button (FUN_0048e0f0).</summary>
	internal static int? HireTabOf( int type ) => type is >= HireFirstTab and <= HireLastTab ? type - HireFirstTab : null;

	/// <summary>
	/// A bar button's mesh and help row by its type, the tag row's +0x10 or +0x14 (FUN_0048e610). A type not here
	/// shows no button; mb_accept and mb_decline are given no help row. Types 0x12 and 0x14 name the nodes b_loan and
	/// b_pkinfo, which no file of ui.wad is called: they are not here, and no listed row carries them.
	/// </summary>
	internal static (string Mesh, int Help)? BarButtonLook( int type ) => type switch
	{
		LocateThing => ("mb_locate", 0x204),
		LocateItem => ("mb_locate", 0x203),
		>= 9 and <= 0x11 => ("mb_buy", 0x200 + type),
		0x13 => ("mb_resrch", 0x213),
		StaffCosts => ("b_staffcost", 0x215),
		0x17 => ("mb_callmech", 0x217),
		EntryPrice => ("b_entry", 0xec),
		0x1a => ("mb_accept", -1),
		ParkTags.DeclineButton => ("mb_decline", -1),
		_ => null
	};

	/// <summary>
	/// The erase button's mesh beside a second button of this type: mb_erase, or none and hidden beside a decline
	/// (FUN_0048e2f0's 0x1001d9); the arm's slide shows it again either way.
	/// </summary>
	internal static string? EraseLook( int secondButton ) => secondButton == ParkTags.DeclineButton ? null : BarEraseMesh;

	/// <summary>
	/// A tag's left click (FUN_0048dae0, 0x10006 with the left button): sound 0xc0, then the arm folded away where
	/// this tag's message is the one on show, or its message put on the bar (FUN_0048d9c0): the tag's timers
	/// stopped, the tag the one being put on show, the arm asked for the bar and the advisor posted message 0x11c,
	/// whether or not the arm takes it. The bar's own turn as the arm starts out with it (<see cref="StartsOut"/>)
	/// letters and dresses it from that tag.
	/// </summary>
	/// <remarks>
	/// The advisor's 0x11c is posted after the arm's fold has withdrawn the one of a bar already out (FUN_004a2590,
	/// the bar's 0x1001d8).
	/// </remarks>
	private void OpenTag( ParkTags tags, ParkTags.Tag tag )
	{
		UiSounds.TagClick();

		if ( tags.OnShow == tag )
		{
			CloseArm( liftsCamcorder: false );
			return;
		}

		if ( !tags.Open( tag, Time.WallMilliseconds ) )
			return;

		Carry( _bar );

		if ( Park is { AdvisorMessages: { } lines } park )
			lines.Post( ParkAdvisorMessages.TagMessage, park.GameTick );

		Log.Info( $"Park gadget: message bar: tag {tag.Row.Id} subject {tag.Subject} asked for, arm {Moving}" );
	}

	/// <summary>
	/// The bar's turn as the arm starts in with it (FUN_0048e2f0, 0x1001d8): the advisor's message 0x11c is
	/// withdrawn (FUN_00486b40), which cuts him off where he is saying it.
	/// </summary>
	private void WithdrawBarLine()
		=> Park?.AdvisorMessages?.Withdraw( ParkAdvisorMessages.TagMessage, (long)(GameClock.Now * 1000), () => Advisor.Current?.StopSpeaking() );

	private static void Dress( UiButton button, int type )
	{
		var look = BarButtonLook( type );

		button.Mesh = look is { } found ? UiMesh.Get( found.Mesh ) : null;
		button.HelpText = look?.Help ?? -1;
		button.Visible = look != null;
	}

	/// <summary>
	/// One of the bar's two stacked buttons (FUN_0048e2f0, 0x10d30 and 0x10d2f, to FUN_0048e0f0 with the button's
	/// type, the tag's thing and its item). Type 3 puts the camera on the thing's cell (FUN_004867b0) and leaves the
	/// bar out. Type 0x18 opens the entry-price screen (FUN_00498d80) and answers 2: the tag on show is taken off
	/// the mast as the erase button takes it, and the arm folded away. Types 9 to 0xc open the buy screen on the
	/// tab the type less 9 names, or pick that tab on the one already open (FUN_004acc70), and answer 2 as well;
	/// the row of the tag's item the original then selects is counted where a tag names one,
	/// <c>TAG_BAR_BUY_ITEM_ROW</c>. Types 0xd to 0x11 open the hire screen on the tab the type less 0xd names
	/// (FUN_0049bdd0, which destroys the park screen that is open first, the hire screen too, so the screen is
	/// always built), and answer 2. Type 0x15 opens the Staff Training Budgets screen (FUN_004b2750, nothing where
	/// it is open already) and answers 2. Type 0x13 runs the research opener (<see cref="ParkResearchOpener"/>) and answers 2.
	/// Every other type opens a screen or answers an offer, none of it built: counted,
	/// <c>TAG_BAR_BUTTON</c>, and nothing more.
	/// </summary>
	private void BarButton( bool first )
	{
		if ( Tags?.OnShow is not { } tag )
			return;

		var type = first ? tag.Row.Button1 : tag.Row.Button2;

		// A type FUN_0048e0f0 has no case for does nothing: the button is shown with no mesh once the arm is out.
		if ( type is not (LocateThing or LocateItem or (>= BuyFirstTab and <= EntryPrice) or 0x1a or ParkTags.DeclineButton) )
			return;

		if ( type == LocateThing )
		{
			ParkOrbitCameraMode.GoToThing( tag.Subject );
			return;
		}

		if ( type == EntryPrice )
		{
			Log.Info( $"Park gadget: message bar: button type {type} opens the entry-price screen and takes tag {tag.Row.Id} off" );
			// Screen 10 of the Finance category; nothing where it is open already ([0x007ca210]).
			if ( !Stack.Windows.Any( window => window is ParkEntryPriceScreen ) )
				ParkCategoryScreens.Show( Stack, ParkCategoryScreens.Finance, 10 );
			EraseTag();
			return;
		}

		if ( type == StaffCosts )
		{
			Log.Info( $"Park gadget: message bar: button type {type} opens the Staff Training Budgets screen and takes tag {tag.Row.Id} off" );
			ParkCategoryScreens.Show( Stack, ParkCategoryScreens.Finance, ParkCategoryScreens.StaffCosts );
			EraseTag();
			return;
		}

		if ( type is >= BuyFirstTab and <= BuyLastTab )
		{
			Log.Info( $"Park gadget: message bar: button type {type} opens the buy screen on tab {type - BuyFirstTab} and takes tag {tag.Row.Id} off" );
			if ( Stack.Windows.OfType<ParkBuyScreen>().FirstOrDefault() is { } open )
				open.PickTab( type - BuyFirstTab );
			else
				Stack.Open( new ParkBuyScreen( Stack, type - BuyFirstTab ) );
			if ( tag.Item != 0 )
				Unimplemented.Report( "TAG_BAR_BUY_ITEM_ROW" );
			EraseTag();
			return;
		}

		if ( type == Research )
		{
			Log.Info( $"Park gadget: message bar: button type {type} runs the research opener and takes tag {tag.Row.Id} off" );
			ParkResearchOpener.Open( Stack );
			EraseTag();
			return;
		}

		if ( HireTabOf( type ) is { } hireTab )
		{
			Log.Info( $"Park gadget: message bar: button type {type} opens the hire screen on tab {hireTab} and takes tag {tag.Row.Id} off" );
			Stack.Open( new ParkHireScreen( Stack, Park, hireTab ) );
			EraseTag();
			return;
		}

		Unimplemented.Report( "TAG_BAR_BUTTON" );
		Log.Info( $"Park gadget: message bar: button type {type} is not built, so nothing more happens" );
	}

	/// <summary>
	/// The bar's erase button (0x10d2e): the tag on show is hidden at once and taken off the mast half a second on
	/// (the mast's 0x100002 with nought), and the arm folded away.
	/// </summary>
	private void EraseTag()
	{
		if ( Tags is not { OnShow: { } tag } tags )
			return;

		tags.Hide( tag, Time.WallMilliseconds );
		CloseArm( liftsCamcorder: false );
	}

	/// <summary>
	/// How far the tag on show stands out past its own rectangle each way: the stream at 0x007502b8, played on it
	/// while its message is on the bar (FUN_0065e72d with 100): two more a step for three steps, two fewer for
	/// three, a step at rest, and round again, a step every 100 ms.
	/// </summary>
	internal static int Swell( long sinceOpened ) => (int)(sinceOpened / 100 % 7) switch { 1 or 5 => 2, 2 or 4 => 4, 3 => 6, _ => 0 };

	/// <summary>
	/// The stream at 0x007502f0 or at 0x00752e48 (the two hold the same fourteen rows) as it plays on a control: how far
	/// it stands squashed, its left out by <see cref="Value"/>, its top down and its right out by the same, its bottom
	/// where it was. Six rows of two, two back of four, two of two and four back of two: 2, 4 ... 12, 8, 4, 6, 8, 6, 4,
	/// 2, 0, and the end row. As FUN_0065e9a4 steps it: a row on a frame more than the interval after the last row's
	/// (or the start's), so never two in one frame (the original at 30 frames a second plays a 50 ms row each 67 ms).
	/// </summary>
	internal sealed class Squash( long start, int interval )
	{
		private static readonly int[] Rows = [0, 2, 4, 6, 8, 10, 12, 8, 4, 6, 8, 6, 4, 2, 0];
		private long _last = start;
		private int _row;

		internal int Interval => interval;
		internal int Value => Rows[_row];

		/// <summary>One frame at <paramref name="now"/>: false once the end row is reached and nothing plays.</summary>
		internal bool Step( long now )
		{
			if ( now - _last <= interval )
				return true;

			_last = now;
			return ++_row < Rows.Length;
		}
	}

	/// <summary>The red top's stream interval, put on and taken off alike (0x004a1359, 0x0048dd04).</summary>
	internal const int TopSquashInterval = 50;
	/// <summary>The stream interval of a tag moved down as one below it goes (0x0048dcd8).</summary>
	internal const int TagSquashInterval = 20;

	/// <summary>The bar, its text (a line break written as \n) and buttons, the arm's handle, its move and what it carries, for the debug console's <c>tags</c>.</summary>
	internal string BarCensus() => $"bar: out={(BarOut ? 1 : 0)} rect={Edges( _bar.Rect )} arm={Edges( _arm.Rect )} handle={Edges( _armEnd.Children[0].Rect )}"
		+ $" move={Moving} carried={(_onArm is { } carried ? $"{carried.Id:x}@{Edges( carried.Rect )}/shown={(carried.Visible ? 1 : 0)}/depth={carried.DrawDepth}/{Behind( carried )}" : "none")} waiting={_waiting?.Id.ToString( "x" ) ?? "none"}"
		+ $" retract={(!_retract.Visible ? "hidden" : _retract.Enabled ? "on" : "off")}"
		+ $" first={Button( _barFirst )} second={Button( _barSecond )} erase={Button( _barErase )} camcorder={(_camcorder.IsDown ? 1 : 0)} text=\"{(BarOut ? _barText.Text?.Replace( "\n", "\\n" ) : null)}\"";

	/// <summary>How many of a carried panel's shown parts are drawn before the body, and how many before the retract button, of how many.</summary>
	private string Behind( UiControl panel )
	{
		var order = _arm.Parent!.DrawOrder();
		var parts = order.Where( control => control != panel && IsIn( control, panel ) ).ToList();
		return $"behind body {parts.Count( part => order.IndexOf( part ) < order.IndexOf( _arm.Parent ) )}"
			+ $" retract {parts.Count( part => order.IndexOf( part ) < order.IndexOf( _retract ) )} of {parts.Count}";

		static bool IsIn( UiControl control, UiControl panel ) => control.Parent is { } parent && (parent == panel || IsIn( parent, panel ));
	}

	private static string Button( UiButton button ) => button.Visible ? $"{button.Mesh?.Name ?? "none"}/help={button.HelpText}" : "hidden";

	/// <summary>
	/// b_1person, the panel's id 99: into first person through FUN_00481a10, the advisor's message 0x131 withdrawn, and
	/// the arm closed behind it (FUN_00498ad0).
	/// The C key reaches the same mode without any of this, which is why
	/// <see cref="ParkCamcorderCameraMode"/> is still the only record of whether it is running.
	/// </summary>
	private void EnterCamcorder()
	{
		ParkCamcorderCameraMode.Enter();
		WithdrawCamcorderLine();
		CloseArm();
	}

	/// <summary>
	/// b_postcard, the panel's id 100: FUN_004a9380, which pauses the game and writes a picture out - the
	/// game ships Postcard.wad, postcard.jpg and an HTML template for it. Nothing here writes one yet, and
	/// it is deliberately out of scope, so it is counted, says so, withdraws the advisor's message 0x131 and closes the
	/// arm as the other one does.
	/// </summary>
	private void SendPostcard()
	{
		Unimplemented.Report( "POSTCARD_BUTTON" );
		Log.Info( "Park gadget: Postcard - nothing writes a postcard out yet, so nothing more happens" );
		WithdrawCamcorderLine();
		CloseArm();
	}

	private const int MastLeft = 94;
	private const int MastRight = 144;
	/// <summary>The mast's bottom edge, where its top stands eight above with no tag on it.</summary>
	private const int MastBottom = 984;
	/// <summary>A tag's control, FUN_0048ea00: 0x46 across and 0x41 down.</summary>
	private const int TagWidth = 0x46;
	private const int TagHeight = 0x41;
	/// <summary>What each tag lifts the mast's top by: its height and ten (LAB_004a11e0, 0x100001).</summary>
	private const int TagStep = TagHeight + 10;

	private readonly UiControl _mast;
	private readonly UiControl _mastTop;
	private readonly List<UiControl> _tagControls = [];
	/// <summary>Each tag control's own rectangle, which the tag on show swells past.</summary>
	private readonly List<UiRect> _tagRects = [];
	private ParkTags? _tagsGiven;
	private ParkTags? _tagsShown;
	/// <summary>The red top's place at rest, which its squash plays about.</summary>
	private UiRect _topRest = new( 77, 848, 161, 976 );
	/// <summary>The red top's stream, or null with none playing.</summary>
	private Squash? _topSquash;
	private int? _topSquashLogged;
	/// <summary>Each tag's stream, where one plays.</summary>
	private readonly Dictionary<ParkTags.Tag, Squash> _tagSquash = [];
	/// <summary>The tags standing at the last build, oldest first, to tell which went.</summary>
	private List<ParkTags.Tag> _standingShown = [];

	/// <summary>The tags the mast shows: the running park's, or the list a test hands the gadget.</summary>
	internal ParkTags? Tags { get => _tagsGiven ?? Level.Current?.ParkState?.Tags; set => _tagsGiven = value; }

	private ParkState? _parkGiven;

	/// <summary>The running park whose advisor hears of the message bar: the level's, or one a test gives.</summary>
	internal ParkState? Park { get => _parkGiven ?? Level.Current?.ParkState; set => _parkGiven = value; }
	private int _tagChangesShown = -1;
	private int _tagsHeard;

	/// <summary>
	/// The tags' timers, then the mast as the list stands: the oldest tag lowest, each eight below where the mast's
	/// top stood as it was put on and across its middle, the top lifted <see cref="TagStep"/> a tag. A hidden tag
	/// keeps its place until it is taken off.
	/// </summary>
	private void PutTags()
	{
		var tags = Tags;
		if ( tags == null ) return;

		tags.Tick( Time.WallMilliseconds );

		// The tag on show gone, by its own going or another park's list: the bar leaves with it (FUN_0048dae0, message 5).
		if ( BarOut && Moving != ArmMove.In && tags.OnShow == null )
			CloseArm( liftsCamcorder: false );

		if ( tags != _tagsShown || tags.Changes != _tagChangesShown )
			BuildTags( tags );

		var now = Time.WallMilliseconds;

		// The tag on show, swelling and shrinking about its own rectangle; another squashed while its stream plays.
		for ( var i = 0; i < _tagControls.Count; ++i )
		{
			var tag = tags.Standing[i];
			var own = _tagRects[i];

			if ( tag == tags.OnShow )
			{
				// The swell is a stream of its own on the same control, started over whatever played (FUN_0065e72d).
				_tagSquash.Remove( tag );
				var swell = Swell( now - tag.OpenedAt );
				_tagControls[i].Rect = new UiRect( own.Left - swell, own.Top - swell, own.Right + swell, own.Bottom + swell );
				continue;
			}

			if ( _tagSquash.TryGetValue( tag, out var squash ) && !squash.Step( now ) )
				_tagSquash.Remove( tag );
			_tagControls[i].Rect = Squashed( own, _tagSquash.TryGetValue( tag, out squash ) ? squash.Value : 0 );
		}

		TagEffects( tags, now );

		if ( _topSquash != null && !_topSquash.Step( now ) )
			_topSquash = null;
		int? topSquash = _topSquash?.Value;
		_mastTop.Rect = Squashed( _topRest, topSquash ?? 0 );

		if ( topSquash != _topSquashLogged )
			Log.Info( $"Park gadget: top squash {topSquash?.ToString() ?? "none"} drawn {Edges( _mastTop.Rect )} at {now}" );
		_topSquashLogged = topSquash;
	}

	/// <summary>
	/// The tags' showing and their two effects (FUN_0048dae0), each frame. A tag is shown half a second after it is put
	/// on (the mast's message 6). The tag counts as shown only where the gadget is too (FUN_0065d773 asks the tag and
	/// every parent), so a tag whose half second ends while the gadget is hidden or put away is first shown, and starts
	/// MessageTag1 over its middle, as the gadget comes back (the original's 0x11 reaches it then, FUN_0065d90b; the map's
	/// and the options screen's closers resume the park before they show the layer, and ours has the clock unpaused in
	/// the frame after the window goes), unless the park is paused (FUN_00486b90 asks the paused flag). As timer
	/// 0x80081 comes, EndOfMessage is started over its middle and kept, hidden at once where the tag is not shown; it is
	/// hidden and shown with the tag (message 0x11), moved with it as a tag below goes (message 0x13, which only a move
	/// sends, not a squash or the swell), and killed as the tag is opened with its timers stopped (FUN_0048d9c0) or taken
	/// off (message 5, in <see cref="BuildTags"/>). Both are drawn with the gadget, so a window opened over it covers them
	/// (<see cref="ScreenParticles"/>).
	/// </summary>
	private void TagEffects( ParkTags tags, long now )
	{
		var particles = ParticleSystem.Current;

		for ( var i = 0; i < _tagControls.Count; ++i )
		{
			var tag = tags.Standing[i];
			var control = _tagControls[i];
			// Deviation (cosmetic, at most 6 px): the middle is the rest rectangle's; the original reads the control's
			// rectangle as it stands (0x0065d42e at 0x00486be0, 0x0048ddb7, 0x0048df26), a playing squash's offsets
			// included, so its effect can stand up to 6 px lower than the tag's middle after a squash.
			var middle = EffectMiddle( _tagRects[i] );
			var shown = tag.ShownAt( now );
			var onScreen = shown && !HiddenNow && !PutAway;

			if ( onScreen != tag.Showing )
			{
				tag.Showing = onScreen;

				if ( onScreen && !tag.FirstShown )
				{
					tag.FirstShown = true;
					var started = !GameClock.Paused
						? StartTagEffect( particles, ParkTags.FirstShowEffect, middle, control ) : 0;
					Log.Info( $"Park gadget: tag {tag.Row.Id} shown at age {now - tag.Born}, effect 0x5a at {middle.X},{middle.Z} handle {started:x}" );
				}

				// Deviation (cosmetic, practically unseen): the original tests a handle as a signed number before it hides,
				// shows, moves or kills it (0x0048da18, 0x0048dbf7, 0x0048df02, 0x0048dfdc, 0x0048e011), so one whose
				// generation is 0x8000 or more is left alone; ours tests IsLive (generations to 0xffff).
				if ( particles?.IsLive( tag.Ending ) == true )
				{
					particles.Hide( tag.Ending, !onScreen );
					Log.Info( $"Park gadget: tag {tag.Row.Id} effect 0x5b {(onScreen ? "shown" : "hidden")} at age {now - tag.Born}" );
				}
			}

			control.Visible = shown;

			if ( tag.EndingDue )
			{
				tag.EndingDue = false;

				if ( tag.Ending == 0 )
				{
					tag.Ending = StartTagEffect( particles, ParkTags.EndingEffect, middle, control );
					tag.EndingAt = middle;

					if ( !onScreen )
						particles?.Hide( tag.Ending, true );

					Log.Info( $"Park gadget: tag {tag.Row.Id} effect 0x5b at {middle.X},{middle.Z} handle {tag.Ending:x} at age {now - tag.Born}, shown {(onScreen ? 1 : 0)}" );
				}
			}
			else if ( middle != tag.EndingAt && particles?.IsLive( tag.Ending ) == true )
			{
				particles.Move( tag.Ending, middle.X, 0, middle.Z );
				tag.EndingAt = middle;
				Log.Info( $"Park gadget: tag {tag.Row.Id} effect 0x5b moved to {middle.X},{middle.Z}" );
			}

			if ( tag.EndingStopped )
			{
				tag.EndingStopped = false;
				KillTagEffect( particles, tag, "opened" );
			}
		}
	}

	/// <summary>Effect <paramref name="effect"/> started over a tag's middle, pinned and drawn as the tag is; its handle, or 0.</summary>
	private int StartTagEffect( ParticleSystem? particles, int effect, (int X, int Z) middle, UiControl tag )
		=> particles?.Spawn( effect, middle.X, 0, middle.Z, tag.Anchor, this, down: tag.VerticalAnchor ) ?? 0;

	/// <summary>A tag's EndOfMessage killed where it lives: its particles live out their time.</summary>
	private static void KillTagEffect( ParticleSystem? particles, ParkTags.Tag tag, string why )
	{
		if ( particles?.IsLive( tag.Ending ) != true )
			return;

		particles.Kill( tag.Ending );
		Log.Info( $"Park gadget: tag {tag.Row.Id} effect 0x5b killed, {why}" );
	}

	/// <summary>
	/// A control's middle in the units on-screen effects are started at (FUN_00486b90, 0x0048de18): across, its middle
	/// times 100000 over 2048; down, times 75000 over 1536; each toward nought.
	/// </summary>
	internal static (int X, int Z) EffectMiddle( UiRect rect )
		=> ((((rect.Right - rect.Left) >> 1) + rect.Left) * 100000 / 2048, (((rect.Bottom - rect.Top) >> 1) + rect.Top) * 75000 / 1536);

	private static UiRect Squashed( UiRect rest, int squash ) => new( rest.Left - squash, rest.Top + squash, rest.Right + squash, rest.Bottom );

	/// <summary>
	/// The streams a change of the list starts, measured against the list as it was last built: a tag put on plays the
	/// red top's at 0x00752e48 unless one plays on it already (LAB_004a11e0, 0x004a135b); a tag taken off plays the
	/// stream at 0x007502f0 on each tag above it that plays none, and on the red top, started over (FUN_0048dae0,
	/// 0x0048dcda, 0x0048dd06). Another park's list, a park loaded, starts none: the original's loader posts 0x100001 per
	/// tag (0x0048ed90, through FUN_0048eb20), so its red top plays the put-on squash at a load; whether any of it shows
	/// past the loading screen is not measured.
	/// </summary>
	private void StartSquashes( ParkTags tags, long now )
	{
		if ( tags != _tagsShown )
		{
			_tagSquash.Clear();
			_topSquash = null;
			return;
		}

		var standing = tags.Standing;
		var gone = _standingShown.FindIndex( tag => !standing.Contains( tag ) );

		if ( gone >= 0 )
		{
			foreach ( var tag in _standingShown.Skip( gone + 1 ) )
				if ( standing.Contains( tag ) && tag != tags.OnShow && !_tagSquash.ContainsKey( tag ) )
				{
					_tagSquash[tag] = new Squash( now, TagSquashInterval );
					Log.Info( $"Park gadget: tag squash stream 0x7502f0 at {TagSquashInterval} ms on tag {tag.Row.Id}" );
				}

			_topSquash = new Squash( now, TopSquashInterval );
			Log.Info( $"Park gadget: top squash stream 0x7502f0 at {TopSquashInterval} ms, a tag gone" );
		}

		if ( standing.Any( tag => !_standingShown.Contains( tag ) ) && _topSquash == null )
		{
			_topSquash = new Squash( now, TopSquashInterval );
			Log.Info( $"Park gadget: top squash stream 0x752e48 at {TopSquashInterval} ms, a tag put on" );
		}

		foreach ( var tag in _tagSquash.Keys.Where( tag => !standing.Contains( tag ) ).ToList() )
			_tagSquash.Remove( tag );
	}

	private void BuildTags( ParkTags tags )
	{

		// The new tag's sound, effect 0x1e of the interface's own category (FUN_0048eb20).
		if ( tags == _tagsShown && tags.Made > _tagsHeard ) UiSounds.NewTag();
		_tagsHeard = tags.Made;

		StartSquashes( tags, Time.WallMilliseconds );

		foreach ( var control in _tagControls )
		{
			control.Visible = false;
			_mast.Children.Remove( control );
		}

		// A tag taken off kills its EndOfMessage (FUN_0048dae0, message 5).
		foreach ( var gone in _standingShown.Where( tag => !tags.Standing.Contains( tag ) ) )
			KillTagEffect( ParticleSystem.Current, gone, "taken off" );

		_tagControls.Clear();
		_tagRects.Clear();
		_tagsShown = tags;
		_tagChangesShown = tags.Changes;

		var standing = tags.Standing;
		_standingShown = standing.ToList();
		var top = MastBottom - 8 - TagStep * standing.Count;
		_topRest = new UiRect( _topRest.Left, top - (_topRest.Bottom - _topRest.Top), _topRest.Right, top );
		_mastTop.Rect = _topRest;
		_mast.Rect = new UiRect( MastLeft, top, MastRight, MastBottom );

		for ( var i = 0; i < standing.Count; ++i )
		{
			var tag = standing[i];
			var left = (MastLeft + MastRight - TagWidth) / 2;
			var tagTop = MastBottom - 8 - TagStep * (i + 1) + 8;
			_tagRects.Add( new UiRect( left, tagTop, left + TagWidth, tagTop + TagHeight ) );
			_tagControls.Add( _mast.Add( new UiControl
			{
				Id = 900 + i,
				Rect = new UiRect( left, tagTop, left + TagWidth, tagTop + TagHeight ),
				// "Message - left-click to open, right-click to delete", row 475 (FUN_0048ea00, +0x90).
				HelpText = 475,
				Mesh = UiMesh.Get( "msgtag" ),
				Frame = tag.Row.Picture,
				Visible = tag.Showing,
				PinAcross = Anchor.Left,
				PinDown = VerticalAnchor.Bottom,
				LeftClicked = () => OpenTag( tags, tag ),
				// Hidden, the arm folded away where its message is on show, and off the mast half a second on; nothing for
				// the tag being put on show (FUN_0048dae0's test of [0x007ca094]).
				RightClicked = () =>
				{
					if ( tag == tags.PuttingOnShow )
						return;

					tags.Hide( tag, Time.WallMilliseconds );

					if ( tags.OnShow == tag )
						CloseArm( liftsCamcorder: false );
				}
			} ) );
		}

		Log.Info( $"Park gadget: {AerialCensus()}" );
	}

	/// <summary>Where the mast, its top and each tag stand on the virtual screen, for the debug console's <c>tags</c>.</summary>
	internal string AerialCensus() => $"aerial: mast={Edges( _mast.Rect )} top={Edges( _topRest )} "
		+ string.Join( " ", _tagControls.Select( ( control, i ) => $"tag{control.Id}={Edges( _tagRects[i] )}/frame={control.Frame}/shown={(control.Visible ? 1 : 0)}" ) );

	/// <summary>The rectangles drawn now, squashed or swollen, for the debug console's <c>tags</c>.</summary>
	internal string AerialDrawn() => $"drawn: top={Edges( _mastTop.Rect )} "
		+ string.Join( " ", _tagControls.Select( control => $"tag{control.Id}={Edges( control.Rect )}" ) );

	private static string Edges( UiRect rect ) => $"{rect.Left},{rect.Top},{rect.Right},{rect.Bottom}";

	/// <summary>The park screen that was open when the gadget last looked - see <see cref="Update"/>.</summary>
	private UiWindow? _screenSeen;

	protected internal override void Update()
	{
		// A park screen opening folds the arm away and lifts the camcorder button (FUN_004a25f0( 1 ) from
		// FUN_00485b70, 0x00485cf3).
		var screen = Stack.Windows.LastOrDefault( window => window.ParkScreen );

		if ( screen != null && screen != _screenSeen )
			CloseArm();

		_screenSeen = screen;

		Slide( Time.Delta * 1000f );
		PutTags();
		Locator.Update();
		ShowDate();
		ShowEarned();
		ShowMoney();
		ShowHappiness();

		// Put the gadget away in first person, because camcorder mode steers the view from where the
		// pointer IS rather than from how it moves: outside a dead zone of 0.4 the view turns at up to
		// 1.2 radians a second, and every one of this panel's controls is inside that band at the bottom
		// left of any window. Left up, reaching for a button would sweep the view round - about a
		// quarter turn during a two-second reach - and leaving the mode hands that yaw to the orbit
		// camera, so the park would come back facing somewhere nobody chose.
		//
		// The original does not solve this by suppressing the steering: its entry to first person hides
		// layer 0, which holds the gadget, and shows the viewfinder's layer 1 (0x004a2ac0) - its own layout
		// stream at 0x0074fa98, a full-frame surround with an eject button in the far corner. Here that
		// layer is ParkViewfinder, and this hides: hidden rather than closed, which is the stack's own word
		// for a window that is neither drawn nor pointed at.
		//
		// It also goes away while the map is up, for the same reason in a different form: the original
		// does not leave the two on top of each other. FUN_005f0b40 sends message 6 - put away - to the
		// interface root (DAT_007cb2ac) before it loads the map's own tree. Without this the map's
		// scroll arrows and zoom buttons land exactly on this panel's six, because both live in the
		// bottom-left corner; a screenshot showed them drawn one over the other while every measured
		// check still passed.
		//
		// The map is NAMED rather than asked for as "is anything modal", because GameMenu, MessageBox
		// and OptionsScreen all set Modal as well, and what the gadget should do underneath those is a
		// separate question nobody has asked.
		//
		// Both conditions belong in ONE assignment: written as two, one after the other, the second
		// silently overwrites the first - a line that compiles, measures clean and does nothing.
		Hidden = HiddenNow;
	}

	/// <summary>Whether the gadget is to be hidden this frame: first person, or the map up. <see cref="Hidden"/> follows it at the end of <see cref="Update"/>; the tags ask it at once.</summary>
	private bool HiddenNow => ParkCamcorderCameraMode.Active || Stack.Windows.Any( window => window is ParkMapScreen );

	/// <summary>
	/// The date the park's calendar has reached.
	///
	/// <para>
	/// <b>The format is an approximation, and knowingly one.</b> The original fills this control in
	/// FUN_004a0e30: it builds the text through FUN_006acca0 from a compiled resource - entry 0x1c0 of
	/// the bundle at DAT_0078ba94, into a 31-byte buffer - handed three numbered arguments typed 0x10,
	/// 0x11 and 0x12, which
	/// are the day, the month and the year. So the layout of the date is <b>the game's own localised
	/// resource</b>, not the machine's setting: the executable imports no GetDateFormat at all, and
	/// GetLocaleInfoA/W appear only inside CRT locale code.
	/// </para>
	/// <para>
	/// That entry of that bundle is UITEXT 448, the row the Load Park list writes a save's date with
	/// (<see cref="ParkFileScreen.DateOf"/>; docs/exe/saves.md). This does not use it: it shows the machine's
	/// short date, a stand-in and not a match.
	/// </para>
	/// </summary>
	private void ShowDate() => _date.Text = GameCalendar.Now.ToShortDateString();

	/// <summary>
	/// The golden keys the player holds and the golden tickets they have not spent, read from their progress the way
	/// the lobby's island panel reads them - see <see cref="IslandPanel.ShowKeys"/>. The tickets are the count less the
	/// spent (<c>FUN_005af5d0</c>, <c>0x004a10cd</c>), and the ticket and its count, like the key and its count, are hidden
	/// while theirs is nought (<c>0x004a1045</c>).
	///
	/// <para>
	/// The format is the original's own: the string FUN_004a1d70 seeds both counts with, at 0x00752f24,
	/// is "0  x" and the update's is "%d  x" (0x00752ef8), so the number leads and the multiplication sign follows it
	/// after two spaces. Nobody being picked yet -
	/// a park reached from the debug console rather than through the lobby - reads as none rather than
	/// as an error, because the count is a fact about a player and there is no player.
	/// </para>
	/// </summary>
	internal void ShowEarned()
	{
		var player = Players.Roster.Current;

		// Each count is written only when it differs from the last written, which starts at -1 and goes back to it on the
		// gadget's message 0x15 (0x004a1160, posted once as the gadget is built): keys 0x004a101c, tickets 0x004a10d2. So
		// -1 is never the first count written, and a profile that spent one more than it won keeps the seeded "0  x" and
		// the ticket as the tree has it; at nought a count's icon and text are hidden and no text is written.
		if ( player is { InstantAction: true } )
			_keyRow.Visible = false;
		else
		{
			var keys = player?.Keys ?? 0;

			if ( keys != _keysWritten )
			{
				_keysWritten = keys;
				_keyRow.Visible = keys != 0;

				if ( keys != 0 )
					_keyCount.Text = $"{keys}  x";
			}
		}

		var tickets = player?.TicketsUnspent ?? 0;

		if ( tickets != _ticketsWritten )
		{
			_ticketsWritten = tickets;
			_ticket.Visible = _ticketCount.Visible = tickets != 0;

			if ( tickets != 0 )
				_ticketCount.Text = $"{tickets}  x";
		}
	}

	/// <summary>
	/// What the park is worth, in the corner the original keeps it - the balance the save was left with,
	/// moved by every admission fee, charge, cost and refund since it was loaded.
	///
	/// <para>
	/// <b>It is ONE number.</b> <see cref="ParkWorld"/> describes a file and a fee cannot move it;
	/// <see cref="ParkState"/> is the park as it is played, the level owns it, and taking a fee moves its
	/// balance directly - which is what the original does too (<c>FUN_004d0600</c> adds a fee straight
	/// onto <c>mBalance</c> and <c>mProfitThisYear</c>).
	/// </para>
	/// <para>
	/// Plain digits, with no thousands separator, because the original sizes this field by measuring the
	/// string "999999999" (0x00752f10) - nine digits and nothing between them.
	/// </para>
	/// <para>
	/// A park whose file carries no economy shows <b>nothing at all</b> rather than a nought, for the
	/// reason the key count shows none rather than an error: having no economy is a fact about the file,
	/// where a nought would be a claim that the park is broke.
	/// </para>
	/// </summary>
	private void ShowMoney()
	{
		var level = Level.Current;

		// The number comes from the running state, but whether the park HAS an economy is still a fact
		// about the file - so a theme shipping no economy shows nothing rather than a nought, which would
		// be a claim that it is broke.
		if ( level?.ParkState is not { } park || level.Park?.Economy is null )
		{
			_balance.Text = null;
			return;
		}

		_balance.Text = $"{park.Balance}";

		// Where Gadget_ShowMoney (0x004a0ab0) also moves the trend arrow 0x31 to the lettering's end and sets
		// 0x32's frame from the sign of the balance; neither is built (Q674). Counted once a frame the gadget updates.
		Unimplemented.Report( "MONEY_TREND_ARROW" );
	}

	/// <summary>
	/// How happy the park's visitors are, which is the one thing the gauge has ever been for - see
	/// <see cref="ParkPeople.AverageHappiness"/> for the number, and <see cref="UiMeter"/> for the drawing.
	///
	/// <para>
	/// <b>Read every frame where the original reads it every two seconds, and that is a departure.</b> Its
	/// meter arms a 2000ms timer (<c>Gadget_GaugeOnMessage</c>, id 0x80083) because the value costs a walk over
	/// every thing in the park; this walks the park's guests alone. What differs on screen is a gauge
	/// that moves smoothly rather than in steps, and putting the timer back would mean keeping a clock
	/// here in order to make the reading worse.
	/// </para>
	/// </summary>
	private void ShowHappiness() => _happiness.Value = GaugeUnits( ParkPeople.Current?.AverageHappiness() ?? 0 );

	/// <summary>
	/// What the gauge's timer hands its meter: the mean times 1024 times 0.01f, truncated (<c>0x004a1d44</c>, the
	/// floats <c>0x006ffa34</c> and <c>0x006ffa38</c>, then <c>__ftol</c>), so 50 is 511 of 1024 and 100 is 1023 at
	/// 53-bit precision, as 0.01f is a shade under a hundredth (512 and 1024 at 24 bits; which one the original runs
	/// is not read, <c>docs/exe/hud.md</c>). The original's mean keeps its fraction; this one is whole (Q459).
	/// </summary>
	internal static int GaugeUnits( double happiness ) => (int)( happiness * 1024.0 * 0.01f );

	/// <summary>
	/// The gauge's meter, control 0x1f: full at 1024, the original's own clamp (<c>FUN_0066d885</c>), as
	/// <see cref="GaugeUnits"/> hands it 1024ths.
	/// </summary>
	internal static UiMeter HappinessGauge() => new()
	{
		Id = 0x1f,
		Rect = new UiRect( 85, 1100, 144, 1324 ),
		Max = 0x400
	};
}
