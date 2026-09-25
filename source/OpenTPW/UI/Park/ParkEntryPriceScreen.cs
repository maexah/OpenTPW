namespace OpenTPW.UI;

/// <summary>
/// What the park charges at the gate, and whether the gate is open at all - the original's
/// <c>entryprice</c>, and the screen the gadget's Money button opens on.
///
/// <para>
/// <b>Where the layout comes from.</b> <c>FUN_00498d80</c> builds it from the compiled stream at
/// <c>0x00751798</c>, handler <c>FUN_00498c60</c>, walked here opcode by opcode to a balanced close.
/// Every rect and id below is that stream's, and every mesh is named by the hash it carries - resolved
/// against ui.wad's own table by the game's <c>h = (c ^ h) * 47</c>, checked first against two hashes
/// this project already knew (<c>b_buy</c> and <c>base</c>). See <c>docs/exe/hud.md</c>.
/// </para>
///
/// <para>
/// <b>The three buttons down the right are NOT a spinner, and reading them as one was wrong.</b> They
/// resolve to <c>b_staffcost</c>, <c>b_loans</c> and <c>b_finance</c>, and the handler confirms it:
/// <c>0x4f3ad</c> calls <c>FUN_004b2750</c> (staff costs), <c>0x4f3ac</c> calls <c>FUN_0049fb30</c>
/// (loans) and <c>0x4f3ab</c> calls <c>FUN_0049ac60</c> (finances). They are this category's own
/// navigation, exactly as the buy and hire screens carry a button to each other. The real spinner is
/// <c>0x4f3ae</c>, control <b>type 12</b>, whose two children are the minus and plus buttons.
/// </para>
///
/// <para>
/// <b>The fee's range and step are the original's, not chosen here.</b> The builder gives the spinner
/// <c>FUN_0066a5c4( &amp;0, &amp;10000 )</c> and <c>FUN_0066a68d( &amp;1 )</c> - nought to ten thousand,
/// one at a time - and seeds it from the park object's <c>+0x118</c>.
/// </para>
///
/// <para>
/// <b>One deviation, declared.</b> The original holds the new fee in a global as the spinner moves
/// (message <c>0x800</c>) and writes it to the park only when the screen is dismissed (message
/// <c>0x14</c>, through <c>FUN_004d05d0</c>). This writes it as it moves. The end state is identical
/// and there is no cancel button on this screen to make the difference visible - the one button that
/// closes it is <c>b_okay</c>, an accept.
/// </para>
/// </summary>
internal sealed class ParkEntryPriceScreen : UiWindow
{
	/// <summary>The spinner's ends and its step, from the builder - see the class remarks.</summary>
	private const int Lowest = 0;

	private const int Highest = 10000;

	private const int Step = 1;

	/// <summary>Font slot 6, which the builder gives both the label and the spinner's own text.</summary>
	private const int TextFont = 6;

	private readonly UiControl _fee;
	private readonly UiButton _door;

	public ParkEntryPriceScreen( WindowStack stack ) : base( stack )
	{
		// Modal, and NOT pausing - the same reading as the buy and hire screens, whose builders likewise
		// ask for no pause where the map screen's plainly does.
		Modal = true;

		// Built onto the park's own layer (0x00498db5), so a right press beside it is the park's.
		ParkScreen = true;

		// w_small, and it took reading the MODELS to find it. The stream asks for this frame as hash
		// 0xf76e42eb, which matches no file stem in ui.wad - which is why this screen, and the buy and
		// hire screens before it, drew as text floating over the park. The hash is over a model's first
		// NODE name, and the node is "window1" inside w_small.MD2. See docs/exe/hud.md.
		Root = new UiControl
		{
			Id = 0x4f3aa,
			Rect = new UiRect( 328, 130, 1720, 901 ),
			Mesh = UiMesh.Get( "w_small" )
		};

		// The heading. THE ORIGINAL PUTS A THING'S NAME HERE, not a fixed string: the builder fetches a
		// thing out of the world by an index at +0x1da732 and calls UI_SetTitle with its name. Which
		// thing that is has not been established, so this is left empty and counted rather than filled
		// with the label below, which belongs to a different control.
		Root.Add( new UiControl
		{
			Id = 0x4f3af,
			Rect = new UiRect( 749, 174, 1221, 253 ),
			HelpText = 191,
			Font = 5,
			TextColour = UiColour.White
		} );

		// The spinner itself - control type 12. Its own rect is the frame; the number sits in the text
		// rect the stream gives it separately (op 3), between the two buttons.
		var spinner = Root.Add( new UiControl
		{
			Id = 0x4f3ae,
			Rect = new UiRect( 773, 446, 1197, 619 ),
			HelpText = 192,

			// f_varibox - hash 0x257b71f9, the node "varibox". A type-12 control is a boxed number
			// with a button either side, and this is the box.
			Mesh = UiMesh.Get( "f_varibox" )
		} );

		_fee = spinner.Add( new UiControl
		{
			Rect = new UiRect( 873, 477, 1106, 521 ),
			Font = TextFont,
			TextColour = UiColour.Black
		} );

		// The two children of the spinner, op 0x0f and op 0x0e - minus on the left, plus on the right,
		// which is what their resolved meshes say rather than what their order suggests.
		spinner.Add( Nudge( new UiRect( 789, 468, 849, 529 ), "b_minus", -Step ) );
		spinner.Add( Nudge( new UiRect( 1127, 468, 1188, 529 ), "b_plus", Step ) );

		// "Ticket Price" - UITEXT 160, which the builder passes as FUN_00485b00( 0xa0 ). Black, font 6:
		// the builder calls the colour setter with (0,0,0,0xff) for this control and for the spinner
		// alike, as the gadget's date is black where the rest of the interface is white.
		//
		// ADDED AFTER THE SPINNER, because its rect (796,551)-(1182,596) sits INSIDE the spinner's
		// (773,446)-(1197,619) and children draw in the order they are added. The stream has the same
		// order - 0x4f3ae closes before 0x4f3b0 opens - so this is the original's own layering, not a
		// workaround. Added first, it was painted over the moment the spinner was given its f_varibox
		// artwork, and the label simply vanished; before that the spinner drew nothing and hid nothing.
		Root.Add( new UiControl
		{
			Id = 0x4f3b0,
			Rect = new UiRect( 796, 551, 1182, 596 ),
			Font = TextFont,
			TextColour = UiColour.Black,
			Text = Localization.Get( UIStrings.TicketPrice )
		} );

		// This category's other three screens. Each closes this one behind it, as the buy screen's
		// cross-link to hire does.
		Root.Add( CrossLink( 0x4f3ad, new UiRect( 1568, 303, 1670, 406 ), 195, "b_staffcost", 9 ) );
		Root.Add( CrossLink( 0x4f3ac, new UiRect( 1568, 411, 1670, 513 ), 196, "b_loans", 8 ) );
		Root.Add( CrossLink( 0x4f3ab, new UiRect( 1568, 518, 1670, 620 ), 197, "b_finance", 7 ) );

		// The door: whether the park is open to visitors at all. DOWN IS CLOSED. The handler calls
		// FUN_00519ef0( down != 1, 0 ) with the switch's new down-state (0x00498d33), and a non-zero first
		// argument is the arm that opens (0x00519f76); the builder shows the switch down for a closed park
		// (0x00498fc3..0x00498fd9).
		_door = Root.Add( new UiButton
		{
			Id = 0x4f3b1,
			Rect = new UiRect( 1350, 678, 1555, 847 ),
			HelpText = 194,
			Mesh = UiMesh.Get( "b_door" ),
			Toggles = true,
			Clicked = () => Level.Current?.ParkState?.SetParkClosed( _door!.IsDown )
		} );

		// b_okay, id -1, help row 1 - an accept rather than a cancel, which is why the fee is written as
		// it moves. See the class remarks.
		Root.Add( new UiButton
		{
			Id = -1,
			Rect = new UiRect( 1591, 714, 1674, 797 ),
			HelpText = 1,
			Mesh = UiMesh.Get( "b_okay" ),
			Clicked = () => Stack.Close( this )
		} );

		// The gate reads the fee through ParkState, so a price changed here is charged at the turnstile
		// immediately - but nothing yet re-judges a guest who has ALREADY decided the old price was too
		// much, which the original does by leaving the judgement to the guest's own state each time.
		Unimplemented.Report( "ENTRY_PRICE_REJUDGE_WAITING_GUESTS" );

		Show();
	}

	/// <summary>One of the spinner's two buttons.</summary>
	private UiButton Nudge( UiRect rect, string mesh, int by )
		=> new()
		{
			Rect = rect,
			Mesh = UiMesh.Get( mesh ),
			Clicked = () =>
			{
				if ( Level.Current?.ParkState is not { } state )
					return;

				state.SetAdmissionFee( Math.Clamp( state.AdmissionFee + by, Lowest, Highest ) );
				Show();
			}
		};

	/// <summary>One of the three buttons to this category's other screens.</summary>
	private UiButton CrossLink( int id, UiRect rect, int help, string mesh, int screen )
		=> new()
		{
			Id = id,
			Rect = rect,
			HelpText = help,
			Mesh = UiMesh.Get( mesh ),
			Clicked = () =>
			{
				Stack.Close( this );
				ParkCategoryScreens.Show( Stack, ParkCategoryScreens.Finance, screen );
			}
		};

	/// <summary>The fee and the door, as the running park holds them.</summary>
	private void Show()
	{
		if ( Level.Current?.ParkState is not { } state )
		{
			_fee.Text = null;
			return;
		}

		_fee.Text = $"{state.AdmissionFee}";
		_door.IsDown = state.ParkIsClosed;
	}

	protected internal override void Update() => Show();
}
