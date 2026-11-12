namespace OpenTPW.UI;

/// <summary>
/// The staff member's window: one member's name, days employed, wage, skill, happiness, energy and status, with arrows
/// to the next and the previous member - one of the nine object windows (<c>docs/exe/hud.md</c>, "The person's
/// windows").
///
/// <para>
/// <b>Where the layout comes from.</b> <c>FUN_004b6080</c> builds it on the shared base <c>FUN_0048cea0</c> from the
/// stream at <c>0x00756f48</c>, with the handler <c>FUN_004b5cb0</c>. Every rect, id, mesh and help row is that
/// stream's. It is a park screen, so opening it closes the one open and folds the gadget's arm (<c>FUN_00485b70</c>).
/// </para>
///
/// <para>
/// <b>Who opens it</b>: the locator strip's left click on a member (<c>FUN_004b3c10</c>), a member's world click
/// (<c>FUN_004879d0</c>) and a row's <c>0x400</c> in the all-staff list (<c>0x00496088</c>). The staff room's list opens it
/// too (<c>0x004b5267</c>); no staff room's window is built.
/// </para>
///
/// <para>
/// <b>As it is built</b> the advisor's message <c>0xc0</c> is posted, and each show of a member with a patrol area
/// posts <c>0xd4</c>; the destroy withdraws both. The figures are filled as a member is shown and every four seconds of
/// real time while the window is up (timer <c>0x80080</c>).
/// </para>
///
/// <para>
/// <b>FIRE and PICK UP</b> (<c>FUN_004b5cb0</c>'s <c>0x50c</c> and <c>0x50e</c>) each close the window as they act. FIRE
/// dismisses the member (<see cref="ParkPeople.Fire"/>), behind a box on UITEXT 397 where the Confirmations option is on;
/// PICK UP puts them in the hand (<see cref="ParkPeople.PickUp"/>).
/// </para>
///
/// <para>
/// <b>The patrol switch</b> (<c>0x510</c>, <c>FUN_004b5b50</c>) hands over the patrol tool as it goes down and gives the
/// member the whole map as it comes up (<see cref="Patrol"/>).
/// </para>
///
/// <para>
/// <b>Counted, not built:</b> the preview's copy of the member's model (<c>FUN_00486530</c>, <c>STAFF_WINDOW_PREVIEW</c>;
/// the panel stands empty and its click still goes to the member), and the name's edit (<c>FUN_0048d290</c>,
/// <c>STAFF_WINDOW_NAME_EDIT</c>). The switch's second help row, 87 while it is down, is not shown: a button here has one.
/// </para>
/// </summary>
internal sealed class ParkStaffWindow : UiWindow, IObjectWindowSteps
{
	/// <summary>How often the figures are filled again, in milliseconds of real time: timer <c>0x80080</c>, armed with 4000.</summary>
	private const long RefreshEvery = 4000;

	/// <summary>UITEXT 443, "{P8} days", the days employed at any count.</summary>
	internal const int DaysText = 443;

	/// <summary>The stats' six labels: the label's id, the UITEXT row it letters, and its row's top and bottom (<c>FUN_004b53c0</c>).</summary>
	private static readonly (int Label, int Text, int Top, int Bottom)[] Labels =
	[
		(0x51a, 64, 297, 342),
		(0x51d, 65, 348, 393),
		(0x519, 67, 400, 444),
		(0x518, 69, 451, 496),
		(0x51c, 68, 502, 547),
		(0x516, 66, 553, 598),
	];

	/// <summary>The bars' inset from their row's top and bottom (<c>FUN_00485e30( bar, 0, 10 )</c>).</summary>
	private const int BarInset = 10;

	private readonly UiTimer _refresh = new( RefreshEvery );
	private readonly UiControl _title;
	private readonly UiControl _days;
	private readonly UiControl _wage;
	private readonly UiControl _status;
	private readonly UiControl _statusPicture;
	private readonly ParkObjectWindow.UiStatBar _skill;
	private readonly ParkObjectWindow.UiStatBar _happiness;
	private readonly ParkObjectWindow.UiStatBar _energy;
	private readonly UiButton _pickUp;
	private readonly UiButton _patrol;
	private readonly UiButton _up;
	private readonly UiButton _down;

	/// <summary>The park whose advisor was told of the window, which is the one told it is gone.</summary>
	private ParkState? _told;

	/// <summary>The member shown - the original's <c>[0x007c2658]</c>.</summary>
	internal int ThingId { get; private set; }

	private ParkStaffWindow( WindowStack stack, int thingId ) : base( stack )
	{
		// Neither the base nor the builder asks for a pause.
		ParkScreen = true;
		ThingId = thingId;

		Root = new UiControl
		{
			Id = 0x50a,
			Rect = new UiRect( 328, 130, 1720, 901 ),

			// The node "window1", in w_small.MD2.
			Mesh = UiMesh.Get( "w_small" )
		};

		// b_allstaff: the all-staff list on the member's kind, their row picked (FUN_00496620( FUN_00506490(), member )).
		Root.Add( new UiButton
		{
			Id = 0x50b,
			Rect = new UiRect( 1568, 518, 1670, 620 ),
			HelpText = 94,
			Mesh = UiMesh.Get( "b_allstaff" ),
			Clicked = AllStaff
		} );

		Root.Add( new UiButton
		{
			Id = 0x50c,
			Rect = new UiRect( 380, 694, 534, 847 ),
			HelpText = 89,
			Mesh = UiMesh.Get( "b_fire" ),
			Clicked = Fire
		} );

		// b_arup is FUN_0048cbe0 and b_ardown FUN_0048caf0, on the staff's mask 1 (Cycle).
		_up = Root.Add( new UiButton
		{
			Id = 0x50d,
			Rect = new UiRect( 1591, 234, 1674, 317 ),
			HelpText = 92,
			Mesh = UiMesh.Get( "b_arup" ),
			Clicked = () => Cycle( newer: true )
		} );

		Root.Add( new UiButton
		{
			Id = -1,
			Rect = new UiRect( 1591, 714, 1674, 797 ),
			HelpText = 1,
			Mesh = UiMesh.Get( "b_okay" ),
			Clicked = () => Stack.Close( this )
		} );

		_pickUp = Root.Add( new UiButton
		{
			Id = 0x50e,
			Rect = new UiRect( 539, 694, 692, 847 ),
			HelpText = 88,
			Mesh = UiMesh.Get( "b_pick" ),
			Clicked = PickUp
		} );

		_down = Root.Add( new UiButton
		{
			Id = 0x50f,
			Rect = new UiRect( 1580, 322, 1663, 406 ),
			HelpText = 91,
			Mesh = UiMesh.Get( "b_ardown" ),
			Clicked = () => Cycle( newer: false )
		} );

		// b_route, flags 0x11: a switch.
		_patrol = Root.Add( new UiButton
		{
			Id = 0x510,
			Rect = new UiRect( 1401, 694, 1555, 847 ),
			HelpText = 86,
			Mesh = UiMesh.Get( "b_route" ),
			Toggles = true,
			Clicked = Patrol
		} );

		// The builder's own preview handler, FUN_004b5c80: a left click (0x10006) puts the camera on the member and the
		// window stays (FUN_0048ce80).
		Root.Add( new UiControl
		{
			Id = 0x511,
			Rect = new UiRect( 386, 280, 650, 544 ),
			Mesh = UiMesh.Get( "!frame" ),
			LeftClicked = () => ParkOrbitCameraMode.GoToThing( ThingId )
		} );

		var stats = Root.Add( new UiControl
		{
			Id = 0x512,
			Rect = new UiRect( 665, 280, 1538, 623 ),
			Mesh = UiMesh.Get( "!frame" )
		} );

		// The labels (FUN_004b53c0), font 6, from the left as the label skins letter them.
		foreach ( var (label, text, top, bottom) in Labels )
			stats.Add( Label( label, new UiRect( 688, top, 1085, bottom ) ) ).Text = Localization.Text( text );

		_days = stats.Add( Label( 0x51b, new UiRect( 1193, 297, 1520, 342 ) ) );

		// A money cell (FUN_00490250), lettered as the visitor's window's cash.
		_wage = stats.Add( Label( 0x51e, new UiRect( 1193, 348, 1520, 393 ) ) );

		// Three type-9 bars, each inset 10 down: the skill and the energy in ridestatbar.wct, the happiness in happygrad.wct.
		_skill = stats.Add( Bar( 0x514, 400, 444, ParkObjectWindow.UiStatBar.RideStatBar ) );
		_happiness = stats.Add( Bar( 0x513, 451, 496, ParkObjectWindow.UiStatBar.HappyGrad ) );
		_energy = stats.Add( Bar( 0x515, 502, 547, ParkObjectWindow.UiStatBar.RideStatBar ) );

		_status = stats.Add( Label( 0x517, new UiRect( 1193, 553, 1523, 598 ) ) );

		// The status picture, type 6 in i_staffstat (the node staffstat).
		_statusPicture = stats.Add( new UiControl
		{
			Id = 0x51f,
			Rect = new UiRect( 1130, 553, 1175, 598 ),
			Mesh = UiMesh.Get( "i_staffstat" )
		} );

		// UI_SetTitle: font 5 in (234, 239, 102), centred in the stream's rect (749 to 1221) widened by half its width
		// either side. Its click is the name's edit (FUN_0048d290).
		_title = Root.Add( new UiControl
		{
			Id = 0x520,
			Rect = new UiRect( 513, 174, 1457, 253 ),
			HelpText = 85,
			Font = 5,
			TextColour = new UiColour( 234, 239, 102 ),
			TextShadow = true,
			LeftClicked = () => Verb( "STAFF_WINDOW_NAME_EDIT" )
		} );

		Root.Add( new UiButton
		{
			Id = 0x521,
			Rect = new UiRect( 1568, 411, 1670, 513 ),
			HelpText = 93,
			Mesh = UiMesh.Get( "b_locate" ),
			Clicked = Locate
		} );

		// The open, vtable +0 (0x004b5a60): with fewer than two staff both arrows are hidden (FUN_0048ca00's count).
		if ( (People?.Staff.Count ?? 0) < 2 )
		{
			_up.Visible = false;
			_down.Visible = false;
		}

		Show();
	}

	private static UiControl Label( int id, UiRect rect )
		=> new()
		{
			Id = id,
			Rect = rect,
			Font = 6,
			TextColour = UiColour.White,
			TextAcross = TextAlign.Start
		};

	private static ParkObjectWindow.UiStatBar Bar( int id, int top, int bottom, string skin )
		=> new()
		{
			Id = id,
			Rect = new UiRect( 1193, top + BarInset, 1520, bottom - BarInset ),
			Skin = skin
		};

	/// <summary>
	/// <c>FUN_004b6080( member )</c>: the window built on them where none is up, then the advisor's message <c>0xc0</c>.
	/// With one already up the base builds nothing and the window stays on its member.
	/// </summary>
	internal static void Open( WindowStack stack, int thingId )
	{
		if ( ParkPeople.Current?.IsStaff( thingId ) != true )
			return;

		if ( !stack.Windows.Any( window => window is ParkStaffWindow ) )
			stack.Open( new ParkStaffWindow( stack, thingId ) );

		if ( Level.Current?.ParkState is { AdvisorMessages: { } lines } park )
			lines.Post( ParkAdvisorMessages.StaffWindow, park.GameTick );

		if ( stack.Windows.LastOrDefault( window => window is ParkStaffWindow ) is ParkStaffWindow up )
			up._told ??= Level.Current?.ParkState;
	}

	private static ParkPeople? People => ParkPeople.Current;

	/// <summary>The member shown, or null where the park has none of that id.</summary>
	private Staff? Member => People?.Staff.FirstOrDefault( member => member.ThingId == ThingId );

	/// <summary>
	/// The show, vtable <c>+4</c> (<c>FUN_004b5ab0</c>): the name for the title, the preview, the figures, the patrol
	/// toggle down where the member has a patrol area (and the advisor's <c>0xd4</c> posted), and PICK UP enabled or not.
	/// </summary>
	private void Show()
	{
		if ( Member is not { } member )
		{
			Gone();
			return;
		}

		_title.Text = member.Name;

		Unimplemented.Report( "STAFF_WINDOW_PREVIEW" );

		Refresh();

		_patrol.IsDown = !member.PatrolsWholeMap;

		if ( _patrol.IsDown && Level.Current?.ParkState is { AdvisorMessages: { } lines } park )
		{
			lines.Post( ParkAdvisorMessages.StaffWindowPatrol, park.GameTick );
			_told ??= park;
		}

		_pickUp.Enabled = CanBePickedUp( member, State );

		Log.Info( $"Staff window: showing thing {ThingId} \"{_title.Text}\", patrol {(_patrol.IsDown ? "down" : "up")}, pick up {(_pickUp.Enabled ? "enabled" : "disabled")}" );
	}

	private static ParkState? State => People?.State;

	/// <summary>
	/// The refresh, vtable <c>+0x14</c> (<c>FUN_004b58b0</c>): the days employed (UITEXT 443 of <c>FUN_00505b70</c>), the
	/// wage (<c>FUN_00506870</c>), the skill's bar, the status's picture and word, the happiness's bar and the energy's.
	/// </summary>
	private void Refresh()
	{
		if ( Member is not { } member || State is not { } state )
		{
			Gone();
			return;
		}

		var figures = FiguresOf( member, state.CalendarNow, Level.Current?.StaffPool?.WageFor( ParkStaffPool.KindFor( member.Model ), member.PayGrade ) ?? 0 );

		_days.Text = figures.Days;
		_wage.Text = figures.Wage;
		_skill.Known = _happiness.Known = _energy.Known = true;
		_skill.Units = figures.Skill;
		_happiness.Units = figures.Happiness;
		_energy.Units = figures.Energy;
		_statusPicture.Frame = figures.Picture;
		_status.Text = figures.Status;

		Log.Info( $"Staff window: thing {ThingId} days \"{figures.Days}\" wage \"{figures.Wage}\" skill {figures.Skill}"
			+ $" happiness {figures.Happiness} energy {figures.Energy} picture {figures.Picture} status \"{figures.Status}\"" );
	}

	/// <summary>
	/// The figures as the refresh sets them: "{P8} days" of whole days since <c>mTimeHired</c> (<c>FUN_00505b70</c>, now
	/// less the stamp over 864,000,000,000), the wage as a money cell letters it, the skill's bar
	/// <c>(skill &lt;&lt; 10) / 100</c>, the happiness's and the energy's <c>((__ftol( v ) &amp; 0xff) &lt;&lt; 10) / 100</c> of
	/// <c>+0x1f8</c> and <c>+0x1fc</c>, the status picture's part of <c>FUN_005064f0</c> (<c>FUN_00485ec0</c>) and its
	/// <c>STAFFSTATES</c> word, a walk read as idle where the member patrols the whole map.
	/// </summary>
	internal static (string Days, string Wage, int Skill, int Happiness, int Energy, int Picture, string Status) FiguresOf(
		Staff member, DateTime now, int wage )
		=> (Localization.Format( DaysText, (8, $"{DaysEmployed( member, now )}") ),
			ParkItemsScreen.Money( wage ),
			(ParkStaffScreen.SkillOf( member ) << 10) / 100,
			ParkItemsScreen.Bar( (int)member.Happiness ),
			ParkItemsScreen.Bar( (int)member.Tiredness ),
			ParkLocator.StatusPart( member.StateGroup ),
			ParkStaffScreen.StatusOf( member ));

	/// <summary>
	/// Whole days since the member was made (<c>FUN_00505b70</c>): the calendar now less <c>mTimeHired</c> over
	/// 864,000,000,000, a signed 64-bit divide whose low 32 bits are the answer.
	/// </summary>
	internal static int DaysEmployed( Staff member, DateTime now )
		=> unchecked((int)((ParkWorld.StaffState.FileTimeOf( now ) - member.TimeHired) / TimeSpan.TicksPerDay));

	/// <summary>
	/// PICK UP's enabled state (<c>FUN_00505bd0</c>, at the show and on every frame, <c>0x1e</c>): not while going on
	/// strike, on strike, carried, repairing or in state <c>0x12</c>, and otherwise only on a cell whose type is
	/// nothing, path, 3, 9 or 10 (<c>FUN_004fa990</c> on the cell the member stands in, <see cref="PeepBehaviour.OnACountingCell(ParkState, FixedVector)"/>,
	/// so off the map, not at all).
	/// </summary>
	internal static bool CanBePickedUp( Staff member, ParkState? state )
	{
		if ( member.Activity is StaffActivity.GoingOnStrike or StaffActivity.OnStrike or StaffActivity.Held
			or StaffActivity.Repairing || (int)member.Activity == 0x12 )
			return false;

		if ( state == null )
			return false;

		return PeepBehaviour.OnACountingCell( state, member.Navigator.Position );
	}

	/// <summary>
	/// An arrow (<c>FUN_0048cbe0</c>, <c>FUN_0048caf0</c> on mask 1, the things whose model is 4 to 8): b_arup the next
	/// member towards the newest, round from the oldest; b_ardown the next towards the oldest, round from the newest
	/// (<see cref="ParkVisitorWindow.Next"/>). The member shown again shows nothing new.
	/// </summary>
	private void Cycle( bool newer )
	{
		if ( People is not { } people )
			return;

		var next = ParkVisitorWindow.Next( [.. people.Staff.Select( member => member.ThingId )], ThingId, newer );

		if ( next == 0 || next == ThingId )
			return;

		ThingId = next;
		Show();
	}

	/// <summary>The cursor Up and Down keys' step (<see cref="IObjectWindowSteps"/>): the arrows' own.</summary>
	public void Step( bool newer ) => Cycle( newer );

	/// <summary>b_locate (<c>FUN_004b3f60( member )</c>): the staff's locator out on the arm, on this member.</summary>
	private void Locate()
	{
		if ( Member is not { } member || Stack.Windows.OfType<ParkGadget>().FirstOrDefault() is not { } gadget )
			return;

		gadget.Locator.OpenOnMember( member, gadget.CarryLocatorPanel, () => Stack.CloseParkScreen() );
	}

	/// <summary>b_allstaff: the all-staff list on the member's kind, their row picked and brought into view.</summary>
	private void AllStaff()
	{
		if ( Member is not { } member )
			return;

		Stack.Open( new ParkStaffScreen( Stack, ParkStaffPool.KindFor( member.Model ), member.ThingId ) );
	}

	/// <summary>
	/// FIRE (<c>0x50c</c>): where the Confirmations option is on (<c>[0x0078d90f]</c>) a box on UITEXT 397 whose tick
	/// (<c>FUN_004b6070</c>) dismisses the member shown as it opened and whose cross does nothing; where it is off the
	/// dismissal at once (<c>FUN_00505790</c>). The window closes either way, behind the box.
	/// </summary>
	private void Fire()
	{
		var member = ThingId;

		if ( GameOptions.Current.Confirmations )
			Stack.Open( new MessageBox( Stack, Localization.Get( UIStrings.DismissEmployee ), () => Dismiss( member ) ) );
		else
			Dismiss( member );

		Log.Info( $"Staff window: FIRE on thing {member}, {(GameOptions.Current.Confirmations ? "the box of UITEXT 397 asks first" : "dismissed at once")}" );

		Stack.Close( this );
	}

	private static void Dismiss( int thingId )
	{
		if ( People?.Fire( thingId ) != true )
			Log.Info( $"Staff window: thing {thingId} is not on the staff, so nobody is dismissed" );
	}

	/// <summary>PICK UP (<c>0x50e</c>): the member kept (<c>FUN_0048c950</c>), put in the hand (<c>FUN_00505c50</c>), the window closed.</summary>
	private void PickUp()
	{
		var picked = People?.PickUp( ThingId ) == true;

		Log.Info( $"Staff window: PICK UP on thing {ThingId}, {(picked ? "in the hand" : "not on the staff")}" );

		Stack.Close( this );
	}

	/// <summary>A verb not built: counted, and the switch put back as it stood.</summary>
	private void Verb( string name )
	{
		Unimplemented.Report( name );
		Log.Info( $"Staff window: {name} for thing {ThingId} is not built" );
	}

	/// <summary>
	/// The patrol area's switch (<c>FUN_004b5b50</c>). Let up, the member's area is the whole map again
	/// (<see cref="Staff.ClearPatrolArea"/>) and the window stays. Put down, the member is kept for the patrol tool
	/// (<see cref="ParkPatrolTool"/>, mode <c>0x36</c>), the tool is put in the hand, the window closes and the advisor's
	/// message <c>0xc1</c> is posted, in that order.
	/// </summary>
	private void Patrol()
	{
		if ( Member is not { } member )
			return;

		if ( !_patrol.IsDown )
		{
			member.ClearPatrolArea();
			Log.Info( $"Staff window: patrol switch up on thing {ThingId}, its area the whole map" );

			return;
		}

		ParkBuildMode.Arm( ParkBuildMode.Patrol, ThingId );
		Stack.Close( this );

		if ( Level.Current?.ParkState is { AdvisorMessages: { } lines } park )
			lines.Post( ParkAdvisorMessages.PatrolTool, park.GameTick );

		Log.Info( $"Staff window: patrol switch down on thing {ThingId}, the patrol tool in the hand" );
	}

	/// <summary>
	/// The member is no longer on the staff. Only FIRE, which closes the window, takes one away in the original while it
	/// is up, so what it does otherwise is not decoded: counted, and the window closed.
	/// </summary>
	private void Gone()
	{
		Unimplemented.Report( "STAFF_WINDOW_MEMBER_GONE" );
		Log.Info( $"Staff window: thing {ThingId} is not on the staff, so the window closes" );
		Stack.Close( this );
	}

	protected internal override void Update()
	{
		// 0x1e, every frame: PICK UP enabled again.
		if ( Member is { } member )
			_pickUp.Enabled = CanBePickedUp( member, State );

		if ( _refresh.Owed( Stack.HoldsTimers ) == 0 )
			return;

		Refresh();
	}

	/// <summary>The destroy (<c>0x14</c>): the advisor's messages <c>0xc0</c> and <c>0xd4</c> withdrawn, which cuts him off where he says one.</summary>
	protected internal override void Closed()
	{
		var now = (long)(GameClock.Now * 1000);

		_told?.AdvisorMessages?.Withdraw( ParkAdvisorMessages.StaffWindow, now, () => Advisor.Current?.StopSpeaking() );
		_told?.AdvisorMessages?.Withdraw( ParkAdvisorMessages.StaffWindowPatrol, now, () => Advisor.Current?.StopSpeaking() );
	}

	/// <summary>The window, for the debug console's <c>staffwindow</c>.</summary>
	internal string Census()
		=> $"staff window: thing {ThingId} title \"{_title.Text}\" days=\"{_days.Text}\" wage=\"{_wage.Text}\" skill {_skill.Units}"
			+ $" happiness {_happiness.Units} energy {_energy.Units} picture {_statusPicture.Frame} status=\"{_status.Text}\""
			+ $" patrol {(_patrol.IsDown ? 1 : 0)} pickup {(_pickUp.Enabled ? 1 : 0)} arrows {(_up.Visible ? 1 : 0)}";
}
