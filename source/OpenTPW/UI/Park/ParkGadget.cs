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
/// click. <b>Five of the six now open something</b>: buy and hire, the map, the camcorder arm, the
/// Information category's staff/items/visitors lists, and the Money category's entry price. <b>Only
/// Research still does nothing</b> - it is not a category and its one screen is six effort sliders
/// over research groups that do not exist - so it says so, the way the park menu's Load and Save
/// already do. This paragraph said four of the six were inert until they were not. <b>The gauge is live now, and this paragraph
/// said there was no such number anywhere until it was.</b> There is one on every guest, and
/// FUN_004c7bb0 is what the original does with them - average them, and read nought while the park is
/// shut, which is what still leaves it resting at its lowest part in a park nobody has entered.
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
/// <b>The arm is a carrier, not a lid.</b> 0x21, with 0x22 and 0x23 on its far end and the retract
/// button 0x24 beside them. Nothing reaches it except through FUN_004a2590, and every one of that
/// function's five callers hands it a panel of its own to hold - so the arm is how this gadget shows any
/// sub-panel at all, and folding it away is how each of them is put back. The one whose contents exist
/// today is the camcorder panel, stream 0x00751720, which the camcorder button puts on it and either of
/// that panel's two buttons takes off again.
/// </para>
/// <para>
/// <b>The message bar is deliberately NOT built, and that is a finding rather than a gap.</b> It has a
/// stream of its own at 0x007501a0 - a messagel/messagem/messager three-slice with an mb_erase button
/// and three stacked buttons - and the arm would carry it exactly as it carries the camcorder panel.
/// But nothing here can ever put a message in it. Every message arrives through FUN_00481580, whose one
/// caller is CTagSystem::ReceiveMessage (FUN_00509bf0), and the only branch of that whose contents can
/// be read is postcard status - sent, to outbox, failed - which is out of scope; the other two take an
/// event id from a simulation that does not exist. A bar built now could only ever be empty, and it
/// would hold at most ten messages it will never be given (DAT_00750610).
/// </para>
/// <para>
/// <b>The aerial cluster (0x2d, 0x2e) is still NOT built.</b> It is the messages control - its help
/// row is "Right-click to delete ALL messages" - so with no message bar there is nothing for it to
/// delete.
/// </para>
/// <para>
/// <b>The bank balance beside it IS built now, and this paragraph used to say a park keeps no balance
/// and no price.</b> 0x2f is the lettering and 0x32 the currency icon, and the number is the save's own
/// <c>mBalance</c> plus what the gates have taken since - see <see cref="ShowMoney"/> for why those are
/// two numbers here and one in the original. What is still out is <b>0x30</b>, the cost of whatever is
/// in your hand, which the original starts hidden and paints yellow because an empty hand has no price;
/// and <b>0x31</b>, the trend arrow, which wants a history of the balance that nothing here keeps. Both
/// reasons are written out where the cluster is built.
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
		// THE ROOT IS A BARE CONTAINER RATHER THAN THE GADGET BODY, and it has to be, because the arm
		// draws BEHIND the body and nothing here can draw a child behind its parent. FUN_004a1d70 gives
		// each control a depth through FUN_0065f16b, which writes it to the control's own +0xe0 and hands
		// children two more; the receivers are legible only in the disassembly, because the setter is
		// __fastcall and Ghidra hides the receiver - the same trap FUN_006ad810 set for the bank balance:
		//
		//     0x21 arm 4   0x22 end 4   0x24 b_retract 7   0x1d body 8   0x23 handle 9   0x25 six 10
		//
		// Higher is nearer the front, and a screenshot is what settles the direction: the six buttons sit
		// at 10 inside a body at 8 and plainly draw over it. So the arm goes in first and the body over
		// it. The first build of this made the arm a child of the body, and it drew a hard seam straight
		// across the panel - which is how the ordering came to be questioned at all.
		//
		// The handle's 9 would put it in front of the body, and it is still left inside the arm: it sits
		// out at x 976-1129 where the body stops at 439, so the two never overlap and no ordering between
		// them can be seen. b_retract's 7 needs no special handling either - inside the arm it already
		// falls between the arm at 4 and the body at 8.
		Root = new UiControl { Rect = VirtualScreen.Whole };

		// The arm, which the gadget carries a sub-panel out on: 0x21 with 0x22 inside it, 0x23 inside that
		// and the retract button 0x24 beside them. It is a carrier, not a lid - FUN_004a2590 is the only
		// way anything gets onto it, and each of its five callers hands it a panel of its own to hold.
		//
		// Branch 54 left it out because "an arm would fold away nothing", which was true then and is not
		// now: the panel it carries is built below, and one of that panel's two buttons works.
		_arm = Root.Add( new UiControl
		{
			Id = 0x21,
			Rect = new UiRect( 331, 1069, 1006, 1495 ),
			Mesh = UiMesh.Get( "panel" ),

			// It reaches out past the body, so UiControl.Add will not let it follow the body's corner and
			// VirtualScreen.AnchorFor would work its own out from its middle instead. That middle is 668
			// across and the left third ends at 683, so it would come out left anyway - but by fourteen
			// units, and an arm bolted to the body should not be held on by a rounding. Its two end pieces
			// are worse: both their middles fall in the middle third, so each would centre itself and the
			// arm would come apart across a wide window.
			PinAcross = Anchor.Left,
			PinDown = VerticalAnchor.Bottom,

			// In until something is put on it. THE ORIGINAL SLIDES IT, and that is not reproduced here:
			// LAB_004a14f0, the handler FUN_004a1d70 installs on THIS control - MOV ECX,ESI at 0x004a2387,
			// where 0x23 is only remembered in DAT_007cb2d0 - is a jump table over messages 0xa to
			// 0x100 working a state in the control's own +0x134, and FUN_004a25f0 reads states 3 and 4 as
			// "still moving". Neither how far nor how long is traced, so the arm is here or it is not,
			// rather than being given an invented travel - as the gauge's moving part was left out rather
			// than given an invented reading.
			Visible = false
		} );

		// The far end of the arm, and the handle on the end of that. panel.md2 and panelend.md2 carry FOUR
		// parts each and they are not animation frames: they are pan_money/pan_info/pan_buy/pan_staff and
		// the matching pane_*, so the arm wears the colour of whichever category it is carrying. Nothing on
		// the camcorder path sets a part, so both stay on part 0 - which is the one the layout stream names
		// each model by, and so the one its mesh hash resolves to.
		var armEnd = _arm.Add( new UiControl
		{
			Id = 0x22,
			Rect = new UiRect( 1006, 1069, 1071, 1382 ),
			Mesh = UiMesh.Get( "panelend" ),
			PinAcross = Anchor.Left,
			PinDown = VerticalAnchor.Bottom
		} );

		armEnd.Add( new UiControl
		{
			Id = 0x23,
			Rect = new UiRect( 976, 1058, 1129, 1503 ),
			Mesh = UiMesh.Get( "handle" ),
			PinAcross = Anchor.Left,
			PinDown = VerticalAnchor.Bottom
		} );

		// "Left-click to close this arm" - UIHELPTEXT row 481. It goes away with the arm, because it is
		// inside it, which is also how it comes to follow the arm's corner without being told to.
		//
		// The original keeps the whole assembly built and switches this button off instead, calling the
		// control's vtable+0x14 with 0 exactly as it does for the readouts that are shown but never
		// clicked. A NOTE CORRECTED HERE: branch 54 drew this button once and wrote that it "showed as a
		// red cross - the disabled part of b_retract". It is not the disabled part. b_retract.md2's six
		// nodes are b_retract, disable, hilite, hidown, helddown and down - UiButton's own order - so part
		// 0 is the NORMAL look, and a button that closes something simply looks like a cross.
		_arm.Add( new UiButton
		{
			Id = 0x24,
			Rect = new UiRect( 349, 1386, 429, 1466 ),
			HelpText = 481,
			Mesh = UiMesh.Get( "b_retract" ),
			Clicked = CloseArm
		} );

		// What the arm carries, out of the camcorder panel's own stream at 0x00751720, which FUN_00498b50
		// builds and parks hidden until FUN_00498bb0 hands it over. Both its buttons close the arm behind
		// them: FUN_00498ad0 acts on the click, withdraws the advisor line the panel posted, and then calls
		// FUN_004a25f0(1) - and that argument is what lifts the camcorder button again.
		var carried = _arm.Add( new UiControl
		{
			Id = 0x62,
			Rect = new UiRect( 466, 1316, 785, 1469 )
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

		// The gadget body itself, over the arm - see the draw-order note where the root is made.
		var body = Root.Add( new UiControl
		{
			Id = 0x1d,
			Rect = new UiRect( 37, 984, 439, 1507 ),
			Mesh = UiMesh.Get( "mainpanel" )
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
		// more fields, and FUN_004a1cd0 arms a 2000ms timer - id 0x80083 - that fills it. It carries no
		// mesh at all, because the original hands it a meter.wct skin instead (FUN_00477870, from
		// FUN_004a1d70), which is why a control that draws only a mesh showed nothing whatever here.
		//
		// IT HAS A NUMBER NOW, AND THIS USED TO SAY THERE WAS NONE ANYWHERE. What the timer asks for is
		// FUN_004c7bb0 - the mean happiness of the park's guests, and nought while the park is shut. See
		// ParkPeople.AverageHappiness for the arithmetic and for the one term of it that is deliberately
		// not reproduced, and UiMeter for how far up the skin is drawn and why that part is a choice.
		_happiness = gaugeHousing.Add( new UiMeter
		{
			Id = 0x1f,
			Rect = new UiRect( 85, 1100, 144, 1324 )
		} );

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

		var buttons = body.Add( new UiControl
		{
			Id = 0x25,
			Rect = new UiRect( 170, 1122, 392, 1372 )
		} );

		// The six, in the stream's own order. Each one's help row is the game's description of it.
		// The first of the four dead ones to have something behind it. FUN_004a0940( 1 ) opens whichever
		// of buy and hire that category was last left on, from a remembered-tab global seeded to 1 -
		// so buy is what a park opens on, and the hire screen is its sibling rather than a second
		// button. See docs/exe/hud.md.
		buttons.Add( new UiButton
		{
			Id = 0x26,
			Rect = new UiRect( 170, 1122, 288, 1240 ),
			HelpText = 469,
			Mesh = UiMesh.Get( "b_buy" ),
			Clicked = () => Stack.Open( new ParkBuyScreen( Stack ) )
		} );

		// The one that works - and it does not enter first person itself, which is what this used to do.
		// FUN_004a0840's case 0x27 splits on whether the button has just gone down: down calls FUN_00498bb0,
		// which puts the camcorder panel on the arm, and up calls FUN_00498bd0, which takes it off again.
		// The button id 99 that actually enters the mode is on that panel, not here. So this is one more
		// click than it was, and it is the original's click: the C key still goes straight there.
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

		// The one of the six with a screen behind it. FUN_004a0840's case 0x29 goes straight to
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
		// staff costs all rest on monthly ring buffers and a loan record this project does not read, and
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

		// THE LAST ONE STILL DOES NOTHING, and it is not a category: FUN_004a0840's case 0x2b goes
		// straight to FUN_004aa480 rather than through the remembered-tab picker, exactly as the map
		// does. Its screen is six effort sliders over research groups, and this game has no research,
		// no researchers and no groups - so there is nothing behind it to open.
		buttons.Add( NotYet( 0x2b, new UiRect( 274, 1254, 392, 1372 ), 472, "b_resrch",
			"Research", "there is nothing to research and nobody to research it" ) );

		// The bank balance, in the OPPOSITE corner - the stream's 0x2f and 0x32. Root controls like the
		// cluster below rather than children of the gadget body, so each anchors top-left on its own.
		//
		// A PARK KEEPS A BALANCE NOW, and this cluster was left out for as long as it did not. The save's
		// economy thing (model 16) carries mBalance and mAdmissionFee, and what the gates have taken since
		// the park opened is on the behaviours - see ShowMoney for why those two are added together rather
		// than either being read on its own.
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

		// The currency icon - the one piece of artwork this cluster has of its own.
		Root.Add( new UiControl
		{
			Id = 0x32,
			Rect = new UiRect( 37, 58, 242, 262 ),
			Mesh = UiMesh.Get( "i_dollar" )
		} );

		// TWO OF THE CLUSTER ARE DELIBERATELY NOT BUILT, each for a reason rather than for want of a rect.
		//
		// 0x30, the price of whatever is in your hand, (458,273)-(720,363): yellow, font 2, and started
		// HIDDEN by FUN_004a1d70, which is exactly what an empty hand should show. Nothing here picks
		// anything up, so it would be hidden for the whole life of a park.
		//
		// 0x31, the trend arrow, (728,109)-(831,211) - and it is a CHILD of 0x2f rather than a root
		// control, which only the disassembly shows: 0x004a0f05 fetches 0x2f from the interface root and
		// then MOV ECX,EAX fetches 0x31 from THAT. FUN_004a0e30 sets its frame to 0 or 1 by comparing the
		// newest sample of two ring buffers - base, index, count and a wrapped flag - at +0x1f5a4 and
		// +0x1fc90 on the thing FUN_00519510 fetches from the world at +0x1da720.
		//
		// THOSE HISTORIES ARE NOT ON THE ECONOMY, which is what this comment said first. They are on the
		// park's STATISTICS manager: FUN_004c5d70 is the serialiser carrying those exact ring offsets,
		// and it writes mLifetimeVisitors, mMisbehavingKids, mMostPaidForTicket, mLongestStay and
		// mParkLastOpened beside them. The offsets are what joins the two functions; the header field is
		// very likely mParkAnalyser, but only the offsets are proven, so the thing is named by what it
		// serialises rather than by a header slot.
		//
		// Nothing in this tree keeps a history of anything, so the arrow would have no two numbers to
		// compare and would sit on one frame for ever.

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
		ShowMoney();
		ShowHappiness();
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
	/// A button whose screen does not exist - today only research's. It draws and lights and clicks like the
	/// others and then says why nothing happened (not yet counted: docs/QUEUE.md Q69), which is what <see cref="ParkFrontEnd"/>'s menu does
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
	/// The camcorder button was clicked, and the arm follows it out or in.
	///
	/// <para>
	/// It reads the button rather than toggling anything: <see cref="WindowStack"/> has already turned a
	/// switch over by the time the click arrives, so asking whether it is down now is asking what the
	/// click meant. Toggling here as well would put the arm back where it started.
	/// </para>
	/// </summary>
	private void ShowArm() => _arm.Visible = _camcorder.IsDown;

	/// <summary>
	/// Folds the arm away and lifts the camcorder button with it - FUN_004a25f0, whose one argument is
	/// that lift: it fetches control 0x27 and puts it back up.
	/// </summary>
	private void CloseArm()
	{
		_arm.Visible = false;
		_camcorder.IsDown = false;
	}

	/// <summary>
	/// b_1person, the panel's id 99: into first person through FUN_00481a10, and the arm closes behind it.
	/// The C key reaches the same mode without any of this, which is why
	/// <see cref="ParkCamcorderCameraMode"/> is still the only record of whether it is running.
	/// </summary>
	private void EnterCamcorder()
	{
		ParkCamcorderCameraMode.Enter();
		CloseArm();
	}

	/// <summary>
	/// b_postcard, the panel's id 100: FUN_004a9380, which pauses the game and writes a picture out - the
	/// game ships Postcard.wad, postcard.jpg and an HTML template for it. Nothing here writes one yet, and
	/// it is deliberately out of scope, so it says so and closes the arm as the other one does.
	/// </summary>
	private void SendPostcard()
	{
		Log.Info( "Park gadget: Postcard - nothing writes a postcard out yet, so nothing more happens" );
		CloseArm();
	}

	protected internal override void Update()
	{
		ShowDate();
		ShowEarned();
		ShowMoney();
		ShowHappiness();

		// Put the gadget away in first person, because camcorder mode steers the view from where the
		// pointer IS rather than from how it moves: outside a dead zone of 0.4 the view turns at up to
		// 2 radians a second, and every one of this panel's controls is inside that band at the bottom
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
		// Both conditions belong in ONE assignment. They were briefly written as two, one after the
		// other, and the second silently overwrote the first - a line that compiles, measures clean and
		// does nothing.
		Hidden = ParkCamcorderCameraMode.Active || Stack.Windows.Any( window => window is ParkMapScreen );
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

	/// <summary>
	/// What the park is worth, in the corner the original keeps it - the balance the save was left with,
	/// plus everything the gates have taken since it was loaded.
	///
	/// <para>
	/// <b>It is ONE number now, and this said it had to be two until it was.</b> The balance used to be
	/// the save's own figure plus a running total the behaviours kept, because <see cref="ParkWorld"/>
	/// describes a file and a fee could not move it. <see cref="ParkState"/> is the park as it is played,
	/// the level owns it, and taking a fee moves its balance directly - which is what the original does
	/// too (<c>FUN_004d0600</c> adds a fee straight onto <c>mBalance</c> and <c>mProfitThisYear</c>). The
	/// comment here named that as the moment to come back to this line, and this is it.
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
	}

	/// <summary>
	/// How happy the park's visitors are, which is the one thing the gauge has ever been for - see
	/// <see cref="ParkPeople.AverageHappiness"/> for the number, and <see cref="UiMeter"/> for the drawing
	/// and for which part of it is a choice.
	///
	/// <para>
	/// <b>Read every frame where the original reads it every two seconds, and that is a departure.</b> Its
	/// meter arms a 2000ms timer (<c>FUN_004a1cd0</c>, id 0x80083) because the value costs a walk over
	/// every thing in the park; this walks a list of thirteen guests. What differs on screen is a gauge
	/// that moves smoothly rather than in steps, and putting the timer back would mean keeping a clock
	/// here in order to make the reading worse.
	/// </para>
	/// </summary>
	private void ShowHappiness() => _happiness.Value = ParkPeople.Current?.AverageHappiness() ?? 0;
}
