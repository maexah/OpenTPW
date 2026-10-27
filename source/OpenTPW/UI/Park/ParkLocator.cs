namespace OpenTPW.UI;

/// <summary>
/// The staff and visitor locators, Ctrl+S and Ctrl+V: a panel the gadget's arm carries out, whose buttons step the
/// camera from one member of a kind to the next, or from one visitor to the next, and a strip above it naming whoever
/// the camera is on (<c>docs/exe/hud.md</c>, "The staff and visitor locators").
///
/// <para>
/// <b>Where the layout comes from.</b> <c>FUN_004b3cf0</c> builds both when the park's interface is put together, from
/// the stream at <c>0x00756950</c> (two panels, <c>0x95c</c> with the five kinds' buttons and <c>0x963</c> with the
/// visitors' one, each in a group <c>0x95d</c> framed in <c>!f_plain</c>) and the stream at <c>0x00756ad8</c> (the strip
/// <c>0x146</c>), with the panels' handler <c>FUN_004b3790</c> and the strip's <c>FUN_004b3c10</c>. Every rectangle,
/// id and help row is theirs; a panel's are moved as far as the arm puts it out (<see cref="ParkGadget"/>).
/// </para>
/// <para>
/// <b>A button steps.</b> The kind it names is taken up from its first member, and the same button again takes the
/// next, round to the first after the last (<c>FUN_004b3340</c> on <c>FUN_004824d0</c>): a kind's members in their
/// list's order, newest first, and the visitors newest first. With somebody found the strip is shown, lettered and
/// the camera put on them; with nobody it is hidden and the camera left. Every frame after, the camera's point is
/// put where the person is drawn (<c>FUN_004b3640</c>, <c>FUN_00486860</c>): a snap, not an ease.
/// </para>
/// <para>
/// <b>The arm folding away closes it</b> (the panel's <c>0x1001d8</c>): nobody, the strip hidden, the camera let go,
/// and no locator open. Ctrl+S sets its locator open before it asks the arm for its panel
/// (<c>FUN_004b3f60</c>), and the arm folds what it carries first, so Ctrl+S with a locator already out leaves the
/// panel out and closed: its buttons go down and step nobody until it is opened again. Ctrl+V sets its own after
/// (<c>FUN_004b4280</c>) and does not.
/// </para>
/// <para>
/// <b>The advisor tells of each as it opens</b>: Ctrl+S posts message <c>0xc6</c> after the arm takes its panel and
/// Ctrl+V <c>0x11f</c> last (<c>FUN_00486b00</c>), and the arm folding either away withdraws both (<c>FUN_00486b40</c>),
/// which cuts him off where he is saying one. So Ctrl+S over a locator already out withdraws its line and posts it
/// again.
/// </para>
/// <para>
/// <b>A fenced member's patrol edges are drawn while followed</b>: each frame the build tools' blue square on every cell of
/// the patrol rectangle's outline (<see cref="PatrolEdgeCells"/>), laid by <see cref="ParkBuildMarkers"/>.
/// </para>
/// <para>
/// <b>The strip's click opens the person's own window</b>: a visitor's (<see cref="ParkVisitorWindow"/>) or a member's
/// (<see cref="ParkStaffWindow"/>), and Ctrl+V or Ctrl+S with one up opens its locator on that person
/// (<see cref="OpenOnVisitor"/>, <see cref="OpenOnMember"/>).
/// </para>
/// </summary>
internal sealed class ParkLocator
{
	/// <summary>Which locator is open - the original's <c>[0x007cc2f0]</c>: none, the staff's or the visitors'.</summary>
	internal enum Kind { None, Staff, Visitors }

	/// <summary>The visitors' role, <c>[0x007cc30c]</c> 6; the staff's are 1 to 5 (<see cref="RoleOf"/>).</summary>
	internal const int VisitorsRole = 6;

	/// <summary>The strip's name, font slot 11 (<c>FUN_00485a70( 0xb )</c>), white.</summary>
	private const int NameFont = 11;

	/// <summary>The thought cell's value with no thought, <c>0x29a</c>: past the last part, so nothing is drawn.</summary>
	private const int NoThought = 0x29a;

	/// <summary>The staff's panel <c>0x95c</c> and the visitors' <c>0x963</c>, each a child of the arm.</summary>
	internal UiControl StaffPanel { get; }
	internal UiControl VisitorPanel { get; }

	private readonly UiRadioGroup _staffButtons;
	private readonly UiRadioGroup _visitorButtons;

	/// <summary>The strip <c>0x146</c>, its name <c>0x147</c>, the status <c>0x148</c>, the hover <c>0x149</c> and the thought <c>0x14a</c>.</summary>
	internal UiControl Strip { get; }
	private readonly UiControl _name;
	private readonly UiControl _status;
	private readonly UiControl _hover;
	private readonly UiControl _thought;

	/// <summary>Which locator is open.</summary>
	internal Kind Open { get; private set; }

	/// <summary>The kind of person stepped through, <c>[0x007cc30c]</c>: 0 none yet, 1 to 5 a kind of staff, 6 the visitors.</summary>
	internal int Role { get; private set; }

	/// <summary>The thing the camera is on, <c>[0x007cc308]</c>, or nought.</summary>
	internal int Actor { get; private set; }

	/// <summary>Whether the camera follows <see cref="Actor"/> each frame - <c>gui_CameraFlags</c> bit <c>0x1000</c>.
	/// Ended by a pan of the orbit camera's keys, as any pan in the original ends it (<c>0x0042ba37</c>, <c>0x0042ba92</c>).</summary>
	internal bool Following { get; private set; }

	/// <summary>The cells this frame's patrol edges were laid on (<see cref="PatrolEdgeCells"/>), none unless a fenced member is followed.</summary>
	internal List<(int X, int Y)> PatrolEdges { get; private set; } = [];

	/// <summary>The people stepped through: the running park's, or what a test hands it.</summary>
	internal ParkPeople? People { get => _people ?? ParkPeople.Current; set => _people = value; }
	private ParkPeople? _people;

	/// <summary>The running park whose advisor hears of the locators: the level's.</summary>
	private static ParkState? Park => Level.Current?.ParkState;

	/// <param name="arm">The gadget's arm, which carries the two panels.</param>
	/// <param name="root">The gadget's root, which holds the strip.</param>
	/// <param name="across">How far the arm puts a panel across from its stream's place, staff then visitors.</param>
	/// <param name="down">How far down, the same for both: their middles on the arm's.</param>
	internal ParkLocator( UiControl arm, UiControl root, (int Staff, int Visitors) across, int down )
	{
		// Each panel at the arm's carried depth, its buttons two above the group's 5, at 7 with the retract button: which of
		// the two the original draws first at one depth is not decoded, and here the retract button, linked first, is.
		StaffPanel = arm.Add( new UiControl { Id = 0x95c, Rect = new UiRect( 474, 1159, 941, 1470 ), Depth = ParkGadget.CarriedDepth, PinAcross = Anchor.Left, PinDown = VerticalAnchor.Bottom } );
		_staffButtons = StaffPanel.Add( new UiRadioGroup
		{
			Id = 0x95d,
			Rect = new UiRect( 526, 1191, 893, 1439 ),
			Mesh = UiMesh.Get( "!f_plain" ),
			PinAcross = Anchor.Left,
			PinDown = VerticalAnchor.Bottom
		} );

		// The stream's order, which is not the roles': the scientist's button is the third and the guard's the fifth.
		Button( _staffButtons, 0x95e, new UiRect( 546, 1207, 649, 1310 ), 482, "b_shandy" );
		Button( _staffButtons, 0x95f, new UiRect( 659, 1207, 761, 1310 ), 483, "b_smech" );
		Button( _staffButtons, 0x960, new UiRect( 715, 1320, 817, 1422 ), 486, "b_sresrhcer" );
		Button( _staffButtons, 0x961, new UiRect( 602, 1320, 705, 1422 ), 484, "b_senter" );
		Button( _staffButtons, 0x962, new UiRect( 771, 1207, 873, 1310 ), 485, "b_sguard" );

		VisitorPanel = arm.Add( new UiControl { Id = 0x963, Rect = new UiRect( -37, 1159, 429, 1470 ), Depth = ParkGadget.CarriedDepth, PinAcross = Anchor.Left, PinDown = VerticalAnchor.Bottom } );
		_visitorButtons = VisitorPanel.Add( new UiRadioGroup
		{
			Id = 0x95d,
			Rect = new UiRect( 12, 1191, 379, 1439 ),
			Mesh = UiMesh.Get( "!f_plain" ),
			PinAcross = Anchor.Left,
			PinDown = VerticalAnchor.Bottom
		} );
		Button( _visitorButtons, 0x964, new UiRect( 144, 1263, 247, 1366 ), 489, "b_skids" );

		StaffPanel.Move( across.Staff, down );
		VisitorPanel.Move( across.Visitors, down );
		StaffPanel.Visible = false;
		VisitorPanel.Visible = false;

		// The strip, lettered by FUN_004b3490 and shown only with somebody to name. Its children take no pointer, so
		// the strip has it: the hover's frame on entering and leaving (0x10001, 0x10002), the window on a left click.
		Strip = root.Add( new UiControl
		{
			Id = 0x146,
			Rect = new UiRect( 448, 964, 1074, 1066 ),
			HelpText = 552,
			Mesh = UiMesh.Get( "!frame" ),
			Visible = false,
			PinAcross = Anchor.Left,
			PinDown = VerticalAnchor.Bottom,
			Entered = () => _hover!.Visible = true,
			Exited = () => _hover!.Visible = false,
			LeftClicked = OpenPersonWindow
		} );

		_name = Strip.Add( new UiControl
		{
			Id = 0x147,
			Rect = new UiRect( 483, 992, 986, 1037 ),
			Font = NameFont,
			TextAcross = TextAlign.Start,
			PinAcross = Anchor.Left,
			PinDown = VerticalAnchor.Bottom
		} );

		_status = Strip.Add( new UiControl
		{
			Id = 0x148,
			Rect = new UiRect( 998, 992, 1043, 1037 ),
			Mesh = UiMesh.Get( "i_staffstat" ),
			PinAcross = Anchor.Left,
			PinDown = VerticalAnchor.Bottom
		} );

		_hover = Strip.Add( new UiHighlight
		{
			Id = 0x149,
			Rect = new UiRect( 483, 992, 986, 1037 ),
			Visible = false,
			PinAcross = Anchor.Left,
			PinDown = VerticalAnchor.Bottom
		} );

		_thought = Strip.Add( new UiControl
		{
			Id = 0x14a,
			Rect = new UiRect( 988, 983, 1052, 1047 ),
			Mesh = UiMesh.Get( ParkVisitorsScreen.ThoughtsMesh ),
			PinAcross = Anchor.Left,
			PinDown = VerticalAnchor.Bottom
		} );
	}

	/// <summary>
	/// A kind's button. The group's handler (<c>LAB_004b3b90</c>) puts it down whether the click put it up or down, and
	/// tells the panel it was pressed: the same button again steps on.
	/// </summary>
	private void Button( UiRadioGroup group, int id, UiRect rect, int help, string mesh )
	{
		var button = group.AddOption( new UiButton { Id = id, Rect = rect, HelpText = help, Mesh = UiMesh.Get( mesh ), Toggles = true } );

		button.Clicked = () =>
		{
			group.Put( id );
			button.IsDown = true;
			Press( id );
		};
	}

	/// <summary>The role a button names (<c>FUN_004b3790</c>'s switch): handymen 1, mechanics 2, guards 3, entertainers 4, scientists 5, visitors 6.</summary>
	internal static int RoleOf( int button ) => button switch
	{
		0x95e => 1,
		0x95f => 2,
		0x962 => 3,
		0x961 => 4,
		0x960 => 5,
		0x964 => VisitorsRole,
		_ => 0
	};

	/// <summary>The thing model a staff role steps through (<c>FUN_004824d0</c>'s masks 2 to <c>0x20</c>, their lists' heads).</summary>
	internal static int ModelOf( int role ) => role switch
	{
		1 => ParkPeople.HandymanModel,
		2 => ParkPeople.MechanicModel,
		3 => ParkPeople.GuardModel,
		4 => ParkPeople.EntertainerModel,
		5 => ParkPeople.ResearcherModel,
		_ => 0
	};

	/// <summary>
	/// Ctrl+S with no member's window up, <c>FUN_004b3f60( 0 )</c>: the staff's locator open, its panel on the arm with no
	/// button down and nobody found, the status shown in the strip's corner, the advisor posted message <c>0xc6</c>, and
	/// the open park screen closed.
	/// </summary>
	/// <param name="putOnArm">The gadget's arm taking the panel, folding whatever it carries first.</param>
	/// <param name="closeScreen">The open park screen closed (<c>FUN_00485b40</c>).</param>
	internal void OpenStaff( Action<UiControl> putOnArm, Action closeScreen )
	{
		// Set before the arm is asked, so a locator the arm folds closes this one with it.
		Open = Kind.Staff;
		putOnArm( StaffPanel );

		Actor = 0;
		Role = 0;
		_staffButtons.Put( -1 );

		_status.Visible = true;
		_thought.Visible = false;

		Tell( ParkAdvisorMessages.StaffLocator );
		closeScreen();

		Log.Info( $"Park locator: staff open={Open}" );
	}

	/// <summary>
	/// Ctrl+V with no visitor's window up, <c>FUN_004b4280</c>: the panel on the arm first, then the visitors' locator
	/// open on their role with nobody found, the thought shown in the strip's corner, the open park screen closed, and
	/// last the advisor posted message <c>0x11f</c>.
	/// </summary>
	internal void OpenVisitors( Action<UiControl> putOnArm, Action closeScreen )
	{
		putOnArm( VisitorPanel );

		Role = VisitorsRole;
		Open = Kind.Visitors;
		_visitorButtons.Put( -1 );
		Actor = 0;

		_status.Visible = false;
		_thought.Visible = true;

		closeScreen();
		Tell( ParkAdvisorMessages.VisitorLocator );

		Log.Info( $"Park locator: visitors open={Open}" );
	}

	/// <summary>
	/// The arm folding a panel of this locator away (<c>0x1001d8</c>): nobody, the strip hidden, the camera let go, and
	/// no locator open, and the advisor's two lines withdrawn, the visitors' first.
	/// </summary>
	internal void Folded()
	{
		Actor = 0;
		Strip.Visible = false;
		Open = Kind.None;
		Following = false;

		if ( Park?.AdvisorMessages is { } lines )
		{
			var now = (long)(GameClock.Now * 1000);
			lines.Withdraw( ParkAdvisorMessages.VisitorLocator, now, () => Advisor.Current?.StopSpeaking() );
			lines.Withdraw( ParkAdvisorMessages.StaffLocator, now, () => Advisor.Current?.StopSpeaking() );
		}

		Log.Info( "Park locator: folded" );
	}

	/// <summary>The advisor posted a locator's message (<c>FUN_00486b00</c>) on the park's tick, where the park has one.</summary>
	private void Tell( int message )
	{
		if ( Park is { AdvisorMessages: { } lines } park )
			lines.Post( message, park.GameTick );
	}

	/// <summary>
	/// A button pressed, the panel's <c>0x101</c>: nothing with no locator open. The first press takes up its role from
	/// the role's first; another role's from its first; the same role's steps on. Nobody found leaves the strip hidden,
	/// or hides it and lets the camera go where somebody was; somebody shows it, letters it and follows them.
	/// </summary>
	internal void Press( int button )
	{
		var role = RoleOf( button );

		if ( Open == Kind.None || role == 0 )
		{
			Log.Info( $"Park locator: button {button:x} with no locator open" );
			return;
		}

		var before = Actor;

		if ( Role == 0 )
		{
			Role = role;
			Step();

			if ( Actor == 0 )
			{
				Log.Info( $"Park locator: role {Role} has nobody" );
				return;
			}

			Strip.Visible = true;
		}
		else
		{
			// The original drops whoever it was on where the role changes, so the new role starts from its first; here a
			// person not of the role's list steps from the first anyway (Step).
			Role = role;
			Step();

			if ( Actor == 0 )
			{
				Strip.Visible = false;
				Following = false;
				Log.Info( $"Park locator: role {Role} has nobody" );
				return;
			}

			if ( before == 0 )
				Strip.Visible = true;
		}

		Describe();
		Following = true;
	}

	/// <summary>
	/// The next of the role after <see cref="Actor"/>, or its first where there is none after (<c>FUN_004b3340</c>):
	/// round the list. A person no longer in the park is stepped on from the first.
	/// </summary>
	private void Step()
	{
		var people = Chain();
		var at = Actor == 0 ? -1 : people.IndexOf( Actor );

		Actor = at + 1 < people.Count ? people[at + 1] : people.Count > 0 ? people[0] : 0;
	}

	/// <summary>The role's people, in the order a button steps them: a kind's list, newest first, or every guest, newest first.</summary>
	internal List<int> Chain()
	{
		if ( People is not { } people )
			return [];

		if ( Role == VisitorsRole )
			return [.. people.Peeps.Select( peep => peep.ThingId )];

		var model = ModelOf( Role );

		return [.. people.Staff.Where( member => member.Model == model ).Select( member => member.ThingId )];
	}

	/// <summary>
	/// The strip lettered for <see cref="Actor"/> and the camera put on them (<c>FUN_004b3490</c>): a member's name, or
	/// "Visitor #" and their number, "New Visitor" at nought (UITEXT 456, 457); then the corner's picture.
	/// </summary>
	private void Describe()
	{
		if ( Find() is not var (member, guest) )
			return;

		_name.Text = member != null ? member.Name
			: guest!.VisitorNumber == 0 ? Localization.Text( 457 )
			: Localization.Format( 456, (24, $"{guest.VisitorNumber}") );

		ShowCorner( member, guest );
		Camera( member, guest );

		if ( member is { PatrolsWholeMap: false } )
			Log.Info( $"Park locator: thing {Actor} patrols {member.PatrolFrom}-{member.PatrolTo}, its edges {PatrolEdgeCells( member.PatrolFrom, member.PatrolTo ).Count} squares a frame" );

		Log.Info( $"Park locator: role {Role} thing {Actor} \"{_name.Text}\" status={(_status.Visible ? _status.Frame : -1)} thought={(_thought.Visible ? _thought.Frame : -1)}"
			+ $" camera ({ParkOrbitCameraMode.PointOfInterest.X:F2},{ParkOrbitCameraMode.PointOfInterest.Y:F2})" );
	}

	/// <summary>
	/// A frame, the panel's <c>0x1e</c>: the camera put on whoever it follows and a fenced member's patrol edges drawn
	/// (<c>FUN_004b3640</c>), then the corner's picture brought up to date (<c>FUN_004b3580</c>). First person takes the
	/// camera off them, as the camera's change of mode clears the follow (<c>0x0042ae52</c>), and so does a frame the orbit
	/// camera was panned by key or reset by Home or keypad 5 (<c>0x0042b54d</c>).
	/// </summary>
	internal void Update()
	{
		PatrolEdges = [];

		if ( Open == Kind.None || Actor == 0 )
			return;

		if ( ParkCamcorderCameraMode.Active )
			Following = false;

		if ( Following && ParkOrbitCameraMode.Panning )
		{
			Log.Info( $"Park locator: the pan ends the follow of thing {Actor}" );
			Following = false;
		}

		if ( Following && ParkOrbitCameraMode.WasReset )
		{
			Log.Info( $"Park locator: the camera's reset ends the follow of thing {Actor}" );
			Following = false;
		}

		// The original keeps the thing's address and reads whatever is left there: a person gone from the park is let go
		// of here instead, the strip hidden and the camera left, as a step that finds nobody does.
		if ( Find() is not var (member, guest) )
		{
			Log.Info( $"Park locator: thing {Actor} is gone" );
			Actor = 0;
			Strip.Visible = false;
			Following = false;
			return;
		}

		if ( Following )
		{
			Camera( member, guest );

			if ( member is { PatrolsWholeMap: false } )
			{
				PatrolEdges = PatrolEdgeCells( member.PatrolFrom, member.PatrolTo );
				ParkBuildMarkers.Current?.QueuePatrolEdges( PatrolEdges );
			}
		}

		ShowCorner( member, guest );
	}

	/// <summary>
	/// The strip's corner (<c>FUN_004b3580</c>): a member's status in <c>i_staffstat</c>, or a guest's last thought in
	/// <c>thoughts</c>, the thought less one and <see cref="NoThought"/> with none.
	/// </summary>
	private void ShowCorner( Staff? member, Peep? guest )
	{
		if ( member != null )
			_status.Frame = StatusPart( member.StateGroup );
		else
			_thought.Frame = guest!.Thoughts.Last is var thought and not 0 ? thought - 1 : NoThought;
	}

	/// <summary>
	/// The cells <c>FUN_004b3640</c> lays a square on for a patrol area, in its order: four lines of
	/// <c>FUN_00536100( 0x136, ... )</c>, the top row less its corners (both corners where the area is one cell wide), the
	/// right column from the top, the bottom row the same as the top, and the left column from the bottom. A row two cells
	/// wide has no middle, so its line runs from the right corner back to the left and both corners are laid twice, as the
	/// original lays them.
	/// </summary>
	internal static List<(int X, int Y)> PatrolEdgeCells( (int X, int Y) from, (int X, int Y) to )
	{
		var cells = new List<(int X, int Y)>();
		var (left, right) = from.X != to.X ? (from.X + 1, to.X - 1) : (from.X, to.X);

		cells.AddRange( Line( (left, from.Y), (right, from.Y) ) );
		cells.AddRange( Line( (to.X, from.Y), (to.X, to.Y) ) );
		cells.AddRange( Line( (left, to.Y), (right, to.Y) ) );
		cells.AddRange( Line( (from.X, to.Y), (from.X, from.Y) ) );

		return cells;
	}

	/// <summary>
	/// <c>FUN_00536100</c>'s walk: along X where it is the longer way, else along Y (a tie too), the other coordinate held
	/// at the start's, both ends included; a cell off the map is passed over (<c>FUN_004d8300</c>).
	/// </summary>
	private static IEnumerable<(int X, int Y)> Line( (int X, int Y) start, (int X, int Y) end )
	{
		var alongX = Math.Abs( end.Y - start.Y ) < Math.Abs( end.X - start.X );
		var (from, to) = alongX ? (start.X, end.X) : (start.Y, end.Y);
		var step = from <= to ? 1 : -1;

		for ( var at = from; ; at += step )
		{
			var (x, y) = alongX ? (at, start.Y) : (start.X, at);

			if ( x >= 0 && y >= 0 && x < ParkWorld.MapSize && y < ParkWorld.MapSize )
				yield return (x, y);

			if ( at == to )
				yield break;
		}
	}

	/// <summary>The status picture of a member's state group (<c>FUN_00485ec0</c> on <c>FUN_005064f0</c>): idle and walking 0, working 1, resting 2, striking 3, and 9, no part, picked up.</summary>
	internal static int StatusPart( int group ) => group switch
	{
		0 or 1 => 0,
		2 => 1,
		3 => 2,
		4 => 3,
		_ => 9
	};

	/// <summary>
	/// The camera's point put where the person is drawn this frame (<c>FUN_00486860</c>): <c>FUN_004f9f00</c>'s blend of
	/// where they stood as the sweep began and where they stand, at the frame's fraction of the sweep, truncated to the
	/// position's units and scaled ten to a cell, then written as <c>FUN_0042aab0</c> writes it, the spin and the zoom
	/// left as they are.
	/// </summary>
	private static void Camera( Staff? member, Peep? guest )
	{
		var navigator = member?.Navigator ?? guest!.Navigator;
		ParkOrbitCameraMode.PointOfInterest = PointOf( navigator.Previous, navigator.Position, ParkPeople.ThingTickFraction );
		ParkOrbitCameraMode.Scroll = Vector2.Zero;
	}

	/// <summary>Where <see cref="Camera"/> puts the point: each axis <c>prev + (cur - prev) * t</c>, truncated, times ten over <see cref="FixedVector.One"/>.</summary>
	internal static Vector3 PointOf( FixedVector previous, FixedVector position, float fraction )
	{
		var x = (int)(previous.X + (double)(position.X - previous.X) * fraction);
		var y = (int)(previous.Y + (double)(position.Y - previous.Y) * fraction);

		return new Vector3( x * 10f * (1f / FixedVector.One), y * 10f * (1f / FixedVector.One), 0f );
	}

	/// <summary>
	/// The strip's left click (<c>FUN_004b3c10</c>, <c>0x10006</c>): the person's own window, a visitor's
	/// (<see cref="OpensVisitorWindow"/>) or a member's (<see cref="OpensStaffWindow"/>).
	/// </summary>
	private void OpenPersonWindow()
	{
		if ( Find() is not var (member, _) )
			return;

		if ( member != null )
		{
			Log.Info( $"Park locator: the strip clicked for member {Actor}" );
			OpensStaffWindow?.Invoke( Actor );
			return;
		}

		Log.Info( $"Park locator: the strip clicked for visitor {Actor}" );
		OpensVisitorWindow?.Invoke( Actor );
	}

	/// <summary>What opens a visitor's window on a guest (<c>FUN_004b79b0</c>): the gadget's, set as it is built.</summary>
	internal Action<int>? OpensVisitorWindow { get; set; }

	/// <summary>What opens a staff member's window on a member (<c>FUN_004b6080</c>): the gadget's, set as it is built.</summary>
	internal Action<int>? OpensStaffWindow { get; set; }

	/// <summary>
	/// <c>FUN_004b3f60( member )</c>, Ctrl+S with a staff member's window up and its <c>b_locate</c>: the staff's locator
	/// open, set before its panel goes on the arm, the member's kind's button down and the strip on them, followed; the
	/// status shown in the strip's corner and the thought hidden, the advisor posted message <c>0xc6</c>, and the open park
	/// screen closed.
	/// </summary>
	internal void OpenOnMember( Staff member, Action<UiControl> putOnArm, Action closeScreen )
	{
		Open = Kind.Staff;
		putOnArm( StaffPanel );

		var button = ButtonOf( member.Model );

		Actor = member.ThingId;
		Role = RoleOf( button );
		_staffButtons.Put( button );

		Strip.Visible = true;
		Describe();
		Following = true;

		_status.Visible = true;
		_thought.Visible = false;

		Tell( ParkAdvisorMessages.StaffLocator );
		closeScreen();

		Log.Info( $"Park locator: staff open={Open} on thing {Actor}, role {Role}" );
	}

	/// <summary>The button <c>FUN_004b3f60</c> puts down for a member's thing model: 5 handyman <c>0x95e</c>, 4 mechanic <c>0x95f</c>, 7 guard <c>0x962</c>, 6 entertainer <c>0x961</c>, 8 researcher <c>0x960</c>.</summary>
	internal static int ButtonOf( int model ) => model switch
	{
		ParkPeople.HandymanModel => 0x95e,
		ParkPeople.MechanicModel => 0x95f,
		ParkPeople.GuardModel => 0x962,
		ParkPeople.EntertainerModel => 0x961,
		ParkPeople.ResearcherModel => 0x960,
		_ => -1
	};

	/// <summary>
	/// <c>FUN_004b3f60( guest )</c>, Ctrl+V with a visitor's window up and its <c>b_locate</c>: the visitors' locator
	/// open, its panel on the arm, the visitors' button down and the strip on the guest, followed; the thought shown in
	/// the strip's corner and the status hidden, the advisor posted message <c>0x11f</c>, and the open park screen closed.
	/// </summary>
	internal void OpenOnVisitor( int guest, Action<UiControl> putOnArm, Action closeScreen )
	{
		// Set before the arm is asked, as the staff's is (FUN_004b3f60 sets [0x007cc2f0] first).
		Open = Kind.Visitors;
		putOnArm( VisitorPanel );

		Actor = guest;
		Role = VisitorsRole;
		_visitorButtons.Put( 0x964 );

		Strip.Visible = true;
		Describe();
		Following = true;

		_status.Visible = false;
		_thought.Visible = true;

		Tell( ParkAdvisorMessages.VisitorLocator );
		closeScreen();

		Log.Info( $"Park locator: visitors open={Open} on thing {Actor}" );
	}

	/// <summary><see cref="Actor"/> as a member of staff or a guest, or null where the park has nobody of that id.</summary>
	private (Staff? Member, Peep? Guest)? Find()
	{
		if ( Actor == 0 || People is not { } people )
			return null;

		if ( people.Staff.FirstOrDefault( member => member.ThingId == Actor ) is { } found )
			return (found, null);

		return people.Guests.TryGetValue( Actor, out var guest ) ? (null, guest) : null;
	}

	/// <summary>The locator, for the debug console's <c>locator</c>.</summary>
	internal string Census()
		=> $"locator: open={Open} role={Role} actor={Actor} following={(Following ? 1 : 0)} strip={(Strip.Visible ? 1 : 0)} name=\"{_name.Text}\""
			+ $" status={(_status.Visible ? _status.Frame : -1)} thought={(_thought.Visible ? _thought.Frame : -1)}"
			+ $" staffPanel={(StaffPanel.Visible ? 1 : 0)}@{StaffPanel.Rect.Left},{StaffPanel.Rect.Top} visitorPanel={(VisitorPanel.Visible ? 1 : 0)}@{VisitorPanel.Rect.Left},{VisitorPanel.Rect.Top}"
			+ $" down={(_staffButtons.Selected >= 0 ? $"{_staffButtons.Selected:x}" : _visitorButtons.Selected >= 0 ? $"{_visitorButtons.Selected:x}" : "none")}"
			+ $" camera=({ParkOrbitCameraMode.PointOfInterest.X:F2},{ParkOrbitCameraMode.PointOfInterest.Y:F2}) patrol={PatrolEdges.Count}";

	/// <summary>The hover's frame: <c>hilight</c> laid over the name (<c>FUN_00659bc9( "hilight" )</c>), taking no pointer.</summary>
	private sealed class UiHighlight : UiControl
	{
		private static Texture? _highlight;

		protected override void OnDraw()
		{
			_highlight ??= new Texture( "ui/textures/hilight.wct" );
			Material.UI.Set( "Color", _highlight );

			var box = Pixels;

			using ( _ = new Graphics.Scope( Screen.Size ) )
				Graphics.Quad( new Rectangle( box.X, Screen.Height - box.Y - box.Height, box.Width, box.Height ), Material.UI );
		}
	}
}
