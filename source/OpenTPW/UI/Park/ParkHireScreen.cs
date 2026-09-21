namespace OpenTPW.UI;

/// <summary>
/// The hire screen - the other half of the gadget's <c>b_buy</c> button, and the sibling of
/// <see cref="ParkBuyScreen"/> rather than a second button of its own.
///
/// <para>
/// <b>Where the layout comes from.</b> <c>FUN_0049bdd0</c> builds it from the compiled layout stream
/// at <c>0x00751fa8</c>, handler <c>FUN_0049b650</c> - 1074 bytes, 31 controls, walked to a balanced
/// close. See <c>docs/exe/hud.md</c>, where both screens' trees are written out.
/// </para>
///
/// <para>
/// <b>The two screens reach each other directly.</b> <c>FUN_004a0940( 1 )</c> opens whichever of them
/// that category was last left on, from a remembered-tab global; each also carries a button to the
/// other - this one's <c>0x2495</c>, and the buy screen's <c>0x1ff</c>.
/// </para>
///
/// <para>
/// <b>Its list has TWO columns, not three.</b> A row's payload is sized by the column count, so where
/// the buy screen pushes name, price and a tick-box, this pushes a name and a wage. That is exactly
/// why <see cref="UiList.Row"/> carries a value per column rather than a fixed record.
/// </para>
///
/// <para>
/// <b>What is deliberately NOT filled in.</b> The mini-balance block (<c>0x2485</c>, help row 163,
/// "indicates the financial effects of hiring the selected person") is a MONTHLY ACCOUNT, and not the
/// park's cash. <c>FUN_0049bdd0</c> reads three monthly ring buffers off the park object - cash in at
/// <c>+0x1fc94</c>, staff costs at <c>+0x1f7f4</c>, total costs at <c>+0x1f5a4</c>, each with its own
/// index and count - and fills the rows as cash in, staff costs, total-minus-staff for other costs,
/// and cash-in-minus-total for the balance. This game keeps no monthly history, so only the staff bill
/// can be answered, and it is answered from the wages actually being paid.
///
/// <para>
/// The other three rows stay blank rather than carry a plausible number. <b>Putting the park's cash
/// under "Balance" is not a rounding error but a different quantity wearing that row's label</b> - the
/// screen would then show the same figure twice, once correctly as the money readout and once as a
/// month's net it is not. All four cells are still built, so the panel keeps the shape the original
/// has and the figures have somewhere to land the day a monthly history exists.
/// </para>
/// </para>
/// </summary>
internal sealed class ParkHireScreen : UiWindow
{
	/// <summary>
	/// The five tabs, in tab-index order, with the control id, help row and mesh the stream gives each.
	/// <b>The ids are not sequential</b> - that is the stream's own doing, as on the buy screen.
	/// </summary>
	private static readonly (int Kind, int Id, int Help, string Mesh, UIStrings Title, UiRect Rect)[] Tabs =
	[
		(0, 0x2490, 154, "b_shandy",    UIStrings.HireCleaners,     new UiRect( 1164, 195, 1267, 297 )),
		(1, 0x2493, 155, "b_smech",     UIStrings.HireMechanics,    new UiRect( 1283, 195, 1386, 297 )),
		(2, 0x2492, 156, "b_senter",    UIStrings.HireEntertainers, new UiRect( 1402, 195, 1504, 297 )),
		(3, 0x2494, 157, "b_sguard",    UIStrings.HireGuards,       new UiRect( 1521, 195, 1623, 297 )),

		// b_sresrhcer - the MODEL FILE is misspelled in ui.wad where its own textures are not.
		(4, 0x2491, 158, "b_sresrhcer", UIStrings.HireScientists,   new UiRect( 1640, 195, 1742, 297 )),
	];

	private readonly UiList _list;
	private readonly UiControl _title;
	private readonly UiControl _money;
	private readonly UiControl _staffCosts;
	private readonly UiControl _chosen;
	private readonly UiRadioGroup _tabs;

	private int _kind;

	/// <summary>Which tab the screen was last left on - the original keeps the same in a global.</summary>
	private static int _lastKind;

	public ParkHireScreen( WindowStack stack ) : base( stack )
	{
		// Modal, and NOT pausing - the same reading as the buy screen: nothing in either builder asks
		// for a pause, unlike the map screen, which plainly does.
		Modal = true;

		Root = new UiControl { Id = 0x247f, Rect = new UiRect( 186, 30, 2018, 1007 ) };

		_title = Root.Add( new UiControl
		{
			Id = 0x2480,
			Rect = new UiRect( 805, 74, 1277, 153 ),
			Font = 5,
			TextColour = UiColour.White
		} );

		_money = Root.Add( new UiControl
		{
			Id = 0x2496,
			Rect = new UiRect( 1297, 74, 1769, 153 ),
			Font = 5,
			TextColour = UiColour.White,
			TextAcross = TextAlign.End
		} );

		// The portrait. The original renders the chosen candidate's own actor into it, rebuilt only
		// when the (kind, costume) pair changes - and it uses the SPRITE BANK numbering, not the thing
		// model's. Nothing is drawn into it here; the frame is the screen's own art.
		Root.Add( new UiControl
		{
			Id = 0x2484,
			Rect = new UiRect( 484, 179, 748, 443 ),
			Mesh = UiMesh.Get( "f_staffpic" )
		} );

		// Who is selected, and their skill. The bar 0x2482 is drawn by the original as
		// (grade << 10) / 5 into a ridestatbar skin - so even a top-grade candidate fills only four
		// fifths of it. That is the original's arithmetic, not a bug; the bar itself is not built.
		var info = Root.Add( new UiControl
		{
			Id = 0x2481,
			Rect = new UiRect( 259, 521, 971, 600 ),
			Mesh = UiMesh.Get( "f_staffinfo" )
		} );

		_chosen = info.Add( new UiControl
		{
			Id = 0x2483,
			Rect = new UiRect( 283, 536, 679, 575 ),
			Font = 6,
			TextColour = UiColour.White,
			TextAcross = TextAlign.Start
		} );

		var balancePanel = Root.Add( new UiControl
		{
			Id = 0x2485,
			Rect = new UiRect( 259, 702, 971, 930 ),
			HelpText = 163,
			Mesh = UiMesh.Get( "f_balance" )
		} );

		// UITEXT 357/358/359/361 - "Cash in", "- Staff costs", "- Other costs", "Balance". The two COST
		// rows are red and the other two white: FUN_0049bdd0 calls the colour setter with (0xff,0,0,0xff)
		// while each cost row is the current control, and never for the cash-in or balance rows.
		balancePanel.Add( Label( 0x2488, new UiRect( 283, 714, 679, 759 ), UIStrings.CashIn ) );
		balancePanel.Add( Label( 0x248d, new UiRect( 283, 765, 679, 810 ), UIStrings.StaffCosts, Cost ) );
		balancePanel.Add( Label( 0x248a, new UiRect( 283, 816, 679, 861 ), UIStrings.OtherCosts, Cost ) );
		balancePanel.Add( Label( 0x2486, new UiRect( 283, 868, 679, 912 ), UIStrings.Balance ) );

		// All four value cells are built and three of them stay empty - see the class remarks. Only the
		// staff bill is a figure this game can answer.
		balancePanel.Add( Figure( 0x2489, new UiRect( 722, 714, 957, 759 ) ) );
		_staffCosts = balancePanel.Add( Figure( 0x248c, new UiRect( 722, 765, 957, 810 ), Cost ) );
		balancePanel.Add( Figure( 0x248b, new UiRect( 722, 816, 957, 861 ), Cost ) );
		balancePanel.Add( Figure( 0x2487, new UiRect( 722, 868, 957, 912 ) ) );

		_list = Root.Add( new UiList
		{
			Id = 0x248e,
			Rect = new UiRect( 1017, 179, 1813, 930 ),
			Mesh = UiMesh.Get( "list_hirestaff" ),
			RowArea = new UiRect( 1043, 441, 1729, 911 ),

			// TWO columns - name and wage. See the class remarks.
			Columns = [(1043, 1456), (1477, 1724)],
			Activated = Chose,
			SelectionChanged = Selected
		} );

		_tabs = _list.Add( new UiRadioGroup
		{
			Id = 0x248f,
			Rect = new UiRect( 1161, 192, 1770, 300 )
		} );

		foreach ( var tab in Tabs )
		{
			_tabs.AddOption( new UiButton
			{
				Id = tab.Id,
				Rect = tab.Rect,
				HelpText = tab.Help,
				Mesh = UiMesh.Get( tab.Mesh ),
				Toggles = true
			} );
		}

		// The group's own handler is what lifts the other members - never assign over it. See
		// ParkBuyScreen, where doing so left a screen whose tabs all stayed down.
		_tabs.SelectionChanged = () =>
		{
			foreach ( var tab in Tabs )
			{
				if ( tab.Id == _tabs.Selected )
					Show( tab.Kind );
			}
		};

		Root.Add( new UiButton
		{
			Id = 0x2495,
			Rect = new UiRect( 1866, 583, 1968, 686 ),
			HelpText = 162,
			Mesh = UiMesh.Get( "b_allthings" ),
			Clicked = () =>
			{
				Stack.Close( this );
				Stack.Open( new ParkBuyScreen( Stack ) );
			}
		} );

		Root.Add( new UiButton
		{
			Id = -2,
			Rect = new UiRect( 1889, 818, 1972, 901 ),
			HelpText = 2,
			Mesh = UiMesh.Get( "b_exit" ),
			Clicked = () => Stack.Close( this )
		} );

		_list.Build();
		Show( _lastKind );

		// Reached every time this screen opens, and not one of them answerable yet: three of the four
		// mini-balance figures need a monthly history this game does not keep, the portrait needs the
		// candidate's own actor rendered into its frame, and the skill bar needs the ridestatbar skin
		// driven by (grade << 10) / 5. Counted rather than left quietly blank.
		Unimplemented.Report( "MONTHLY_ACCOUNT_HISTORY" );
		Unimplemented.Report( "STAFF_PORTRAIT_ACTOR" );
		Unimplemented.Report( "STAFF_SKILL_BAR" );
	}

	/// <summary>What the original letters the two outgoing rows in - <c>FUN_0065c5d5( 0xff, 0, 0, 0xff )</c>.</summary>
	private static readonly UiColour Cost = new( 255, 0, 0 );

	private static UiControl Label( int id, UiRect rect, UIStrings text, UiColour? colour = null )
		=> new()
		{
			Id = id,
			Rect = rect,
			Font = 6,
			TextColour = colour ?? UiColour.White,
			TextAcross = TextAlign.Start,
			Text = Localization.Get( text )
		};

	private static UiControl Figure( int id, UiRect rect, UiColour? colour = null )
		=> new()
		{
			Id = id,
			Rect = rect,
			Font = 6,
			TextColour = colour ?? UiColour.White,
			TextAcross = TextAlign.End
		};

	/// <summary>
	/// Fills the list with one kind's candidates - the original's <c>FUN_0049b5b0</c>, whose filter is
	/// the candidate record's own first field against the tab being shown.
	/// </summary>
	private void Show( int kind )
	{
		_kind = Math.Clamp( kind, 0, Tabs.Length - 1 );
		_lastKind = _kind;

		var tab = Tabs[_kind];

		_title.Text = Localization.Get( tab.Title );
		_tabs.Select( tab.Id );
		_chosen.Text = null;

		_list.Clear();

		if ( Level.Current?.StaffPool is { } pool )
		{
			foreach ( var person in pool.OfKind( _kind ) )
				_list.Add( new UiList.Row( person.Id, person.Name, person.Wage ) );
		}

		ShowMoney();
	}

	/// <summary>
	/// A candidate was highlighted. The original previews the cost here - message <c>0x401</c> adds
	/// this wage to the staff-costs row and takes it off the balance, so the player sees what hiring
	/// would do before they do it.
	/// </summary>
	private void Selected( int candidateId )
	{
		if ( Level.Current?.StaffPool is not { } pool )
			return;

		foreach ( var person in pool.Candidates )
		{
			if ( person.Id != candidateId )
				continue;

			_chosen.Text = $"{person.Name}   grade {person.Grade}   {person.Wage} a month";
			ShowMoney( person.Wage );

			return;
		}
	}

	/// <summary>
	/// A candidate was chosen. The original takes them out of the pool and carries them on the cursor
	/// - a "place staff" mode - and the worker is constructed on the next click at a cell.
	/// </summary>
	/// <remarks>
	/// <b>That last step needs the pointer, which is not built</b>, so this reports what it would do
	/// and counts the gap rather than inventing a cell to drop somebody on. <c>hire</c> from the
	/// console finishes the job, and is proven in a running park.
	/// </remarks>
	private void Chose( int candidateId )
	{
		Unimplemented.Report( "PLACE_STAFF_BY_POINTING" );

		if ( Level.Current?.StaffPool is not { } pool )
			return;

		foreach ( var person in pool.Candidates )
		{
			if ( person.Id == candidateId )
			{
				Log.Info( $"Hire screen: would carry {person.Name}, a grade {person.Grade} " +
					$"{ParkStaffPool.NameOfKind( person.Kind ).ToLowerInvariant()} at {person.Wage} a month - " +
					"putting them down needs the pointer; `hire` from the console does it now" );

				return;
			}
		}
	}

	/// <summary>
	/// The figures this game can actually answer, and no others.
	/// </summary>
	/// <param name="considering">
	/// A wage being previewed, which the original subtracts from the balance and adds to the staff
	/// costs so the effect of hiring is visible before it happens.
	/// </param>
	private void ShowMoney( int considering = 0 )
	{
		if ( Level.Current?.ParkState is not { } state )
			return;

		_money.Text = $"{state.Balance}";

		var monthly = considering;

		if ( ParkPeople.Current is { } people && Level.Current?.StaffPool is { } pool )
		{
			foreach ( var member in people.Staff )
			{
				var kind = ParkStaffPool.KindFor( member.Model );

				if ( kind >= 0 )
					monthly += pool.WageFor( kind, member.PayGrade );
			}
		}

		_staffCosts.Text = $"{monthly}";
	}

	protected internal override void Update() => ShowMoney();
}
