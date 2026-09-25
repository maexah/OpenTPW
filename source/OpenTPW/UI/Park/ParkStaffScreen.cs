namespace OpenTPW.UI;

/// <summary>
/// Everyone the park employs, by kind - the original's <c>allstaff</c>, and the screen the gadget's
/// Information button is seeded to open on after park status.
///
/// <para>
/// <b>Where the layout comes from.</b> <c>FUN_00496620</c> builds it from the compiled stream at
/// <c>0x00750e10</c>, handler <c>FUN_00495da0</c>, walked here opcode by opcode. Every rect, id and
/// mesh is that stream's; the five column headings are UITEXT <b>101-105</b>, which the builder hands
/// to the header children by id <c>0x10 + index</c>. See <c>docs/exe/hud.md</c>.
/// </para>
///
/// <para>
/// <b>Its five tabs are the same five kinds the hire screen hires</b>, in the same order and with the
/// same artwork - cleaners, mechanics, entertainers, guards, scientists. The builder's own switch maps
/// kind to tab id (<c>0x323</c>, <c>0x326</c>, <c>0x325</c>, <c>0x327</c>, <c>0x324</c>), which is why
/// the ids below look shuffled and are not.
/// </para>
///
/// <para>
/// <b>The two meters are <c>happygrad.wct</c>, not the gauge's skin.</b> <c>FUN_00496620</c> loads that
/// texture by name for both - <c>0x32c</c> the mean over all staff, <c>0x32d</c> the mean over the
/// chosen kind - and scales each by <c>(v &lt;&lt; 10) / 100</c>, so the bar's full scale is
/// <b>1024</b> against a percentage. That is reproduced rather than simplified.
/// </para>
///
/// <para>
/// <b>A NAME IS A DECLARED STAND-IN.</b> A placed member of staff carries no name in the save - only a
/// hire-pool candidate does, and the name is spent when they are taken out of the pool. So the first
/// column shows the kind and the thing id, in the game's own words for the kind
/// (<c>STAFF_TYPES.str</c>), rather than leaving the column blank or inventing a person. Everything
/// else in the row is real: the wage is the one the park is actually paying and the skill is the
/// grade.
/// </para>
/// </summary>
internal sealed class ParkStaffScreen : UiWindow
{
	/// <summary>The five tabs, by kind, with the id, help row and mesh the stream gives each.</summary>
	private static readonly (int Kind, int Id, int Help, string Mesh, UiRect Rect)[] Tabs =
	[
		(0, 0x323, 114, "b_shandy",    new UiRect( 1160, 191, 1262, 294 )),
		(1, 0x326, 115, "b_smech",     new UiRect( 1279, 191, 1381, 294 )),
		(2, 0x325, 116, "b_senter",    new UiRect( 1398, 191, 1500, 294 )),
		(3, 0x327, 117, "b_sguard",    new UiRect( 1516, 191, 1619, 294 )),

		// b_sresrcher, spelled correctly - the HIRE screen asks for b_sresrhcer, which is the
		// misspelling ui.wad actually ships. Both files exist; the two screens ask for different ones.
		(4, 0x324, 118, "b_sresrhcer", new UiRect( 1635, 191, 1738, 294 )),
	];

	/// <summary>The header rects, in column order, from the stream's <c>op 0xc</c> children.</summary>
	private static readonly UiRect[] Headings =
	[
		new( 294, 306, 716, 412 ), new( 722, 306, 1007, 412 ), new( 1013, 306, 1221, 412 ),
		new( 1226, 306, 1476, 411 ), new( 1483, 306, 1733, 411 )
	];

	/// <summary>
	/// The label under the per-kind meter, by kind. <b>NOT 107 to 111 in order.</b>
	/// </summary>
	/// <remarks>
	/// <c>FUN_00496620</c>'s switch is deliberately out of order - case 0 takes <c>0x6b</c>, case 1
	/// <c>0x6c</c>, case <b>2</b> takes <c>0x6e</c> and case <b>3</b> takes <c>0x6d</c>, case 4
	/// <c>0x6f</c> - because the string file lists guards before entertainers while the kinds run
	/// cleaners, mechanics, entertainers, guards, scientists. Written as a plain 107..111 run, the
	/// guards tab showed a list of guards under the heading "Entertainers' Happiness", which is how
	/// the transposition was caught: on screen, not in the decompile it had been read from.
	/// </remarks>
	private static readonly int[] KindHappiness = [107, 108, 110, 109, 111];

	/// <summary>The original's own meter scale - see the class remarks.</summary>
	private const int MeterFull = 1024;

	private readonly UiList _list;
	private readonly UiRadioGroup _tabs;
	private readonly UiControl _kindLabel;
	private readonly UiMeter _kindHappiness;
	private readonly UiMeter _allHappiness;

	private int _kind;

	/// <summary>Which tab the screen was last left on - the original keeps the same in a global.</summary>
	private static int _lastKind;

	public ParkStaffScreen( WindowStack stack ) : base( stack )
	{
		Modal = true;

		// Built onto the park's own layer (0x00496643), so a right press beside it is the park's.
		ParkScreen = true;

		// w_big, the node "window4" inside w_big.MD2 - the frame five screens share, and which the
		// tree recorded as unresolvable until the models' node names were read rather than their file
		// names. Without it this screen is a list floating over the park. See docs/exe/hud.md.
		Root = new UiControl
		{
			Id = 0x320,
			Rect = new UiRect( 186, 30, 2018, 1007 ),
			Mesh = UiMesh.Get( "w_big" )
		};

		Root.Add( new UiControl
		{
			Id = 0x328,
			Rect = new UiRect( 803, 81, 1276, 161 ),
			Font = 5,
			TextColour = UiColour.White,
			Text = Localization.Get( UIStrings.AllStaff )
		} );

		_list = Root.Add( new UiList
		{
			Id = 0x321,
			Rect = new UiRect( 272, 181, 1817, 801 ),
			HelpText = 124,
			Mesh = UiMesh.Get( "list_allstaff" ),
			RowArea = new UiRect( 294, 418, 1734, 777 ),

			// A right click on a row moves the camera to that member of staff and closes the screen
			// (0x0049602f; docs/QUEUE.md Q117). Not built: counted on the press.
			RightPressed = () => Unimplemented.Report( "LIST_ROW_RIGHT_CLICK" ),

			// The stream's own op 0xb record. The builder right-aligns columns 2, 3 and 4 and leaves
			// nought and one reading from the left - FUN_006636b2( n, rightAligned ) - which is what
			// UiList does by default for every column but the first, so only column 1 is overridden.
			Columns = [(299, 711), (729, 999), (1017, 1217), (1231, 1471), (1488, 1728)],
			ColumnAligns = [TextAlign.Start, TextAlign.Start, TextAlign.End, TextAlign.End, TextAlign.End]
		} );

		for ( var column = 0; column < Headings.Length; ++column )
			_list.AddHeading( column, Headings[column], Localization.Text( 101 + column ) );

		_tabs = _list.Add( new UiRadioGroup
		{
			Id = 0x322,
			Rect = new UiRect( 1157, 188, 1737, 297 )
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

		_tabs.SelectionChanged = () =>
		{
			foreach ( var tab in Tabs )
			{
				if ( tab.Id == _tabs.Selected )
					Show( tab.Kind );
			}
		};

		// The panel along the bottom: the chosen kind's mean happiness over one meter, and every
		// member of staff's over the other.
		var panel = Root.Add( new UiControl
		{
			Id = 0x329,
			Rect = new UiRect( 615, 815, 1589, 950 ),
			Mesh = UiMesh.Get( "!frame" )
		} );

		_kindLabel = panel.Add( Label( 0x32a, new UiRect( 654, 832, 1177, 877 ) ) );

		panel.Add( Label( 0x32b, new UiRect( 654, 883, 1177, 928 ) ) ).Text = Localization.Text( 106 );

		_kindHappiness = panel.Add( new UiMeter
		{
			Id = 0x32d,
			Rect = new UiRect( 1191, 832, 1567, 877 ),
			Skin = "ui/textures/happygrad.wct",
			Max = MeterFull
		} );

		_allHappiness = panel.Add( new UiMeter
		{
			Id = 0x32c,
			Rect = new UiRect( 1191, 883, 1567, 928 ),
			Skin = "ui/textures/happygrad.wct",
			Max = MeterFull
		} );

		Root.Add( CrossLink( 0x32f, 360, 125, "b_kids", 6 ) );
		Root.Add( CrossLink( 0x330, 467, 126, "b_allthings", 5 ) );
		Root.Add( CrossLink( 0x32e, 575, 127, "b_parkinfo", 3 ) );

		Root.Add( new UiButton
		{
			Id = -2,
			Rect = new UiRect( 1889, 818, 1972, 901 ),
			HelpText = 1,
			Mesh = UiMesh.Get( "b_okay" ),
			Clicked = () => Stack.Close( this )
		} );

		_list.Build();
		Show( _lastKind );
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

	private UiButton CrossLink( int id, int top, int help, string mesh, int screen )
		=> new()
		{
			Id = id,
			Rect = new UiRect( 1866, top, 1968, top + 102 ),
			HelpText = help,
			Mesh = UiMesh.Get( mesh ),
			Clicked = () =>
			{
				Stack.Close( this );
				ParkCategoryScreens.Show( Stack, ParkCategoryScreens.Information, screen );
			}
		};

	/// <summary>Fills the list with everyone of one kind - the original's <c>FUN_00496d20</c>.</summary>
	private void Show( int kind )
	{
		_kind = Math.Clamp( kind, 0, Tabs.Length - 1 );
		_lastKind = _kind;

		_tabs.Select( Tabs[_kind].Id );
		_kindLabel.Text = Localization.Text( KindHappiness[_kind] );

		_list.Clear();

		if ( ParkPeople.Current is not { } people )
			return;

		var pool = Level.Current?.StaffPool;

		foreach ( var member in people.Staff )
		{
			if ( ParkStaffPool.KindFor( member.Model ) != _kind )
				continue;

			// The name is a stand-in - see the class remarks. The wage and the grade are not.
			var name = $"{ParkStaffPool.NameOfKind( _kind )} {member.ThingId}";
			var wage = pool?.WageFor( _kind, member.PayGrade ) ?? 0;

			_list.Add( new UiList.Row( member.ThingId, name, 0, Values:
			[
				$"{member.Activity}",
				$"{wage}",
				$"{member.PayGrade}",
				$"{(int)member.Happiness}"
			] ) );
		}

		ShowMeters();

		// The status column shows the activity's own name rather than the game's word for it: the
		// original letters that column from a table this project has not found, and an English enum
		// name is a stand-in for it.
		Unimplemented.Report( "STAFF_STATUS_TEXT" );

		// And a placed member of staff carries no name index at all - see the class remarks.
		Unimplemented.Report( "STAFF_NAMES_NOT_IN_SAVE" );
	}

	/// <summary>
	/// The two meters: the chosen kind's mean happiness, and every member of staff's.
	/// </summary>
	/// <remarks>
	/// Both read nought where nobody is employed, rather than resting anywhere else - a mean over an
	/// empty set is not a reading, and the gauge on the gadget already takes the same view of a park
	/// with no guests in it.
	/// </remarks>
	private void ShowMeters()
	{
		if ( ParkPeople.Current is not { } people )
			return;

		_kindHappiness.Value = Scaled( people.Staff
			.Where( member => ParkStaffPool.KindFor( member.Model ) == _kind ) );

		_allHappiness.Value = Scaled( people.Staff );
	}

	/// <summary>A mean happiness on the original's own 0-1024 scale - <c>(v &lt;&lt; 10) / 100</c>.</summary>
	private static int Scaled( IEnumerable<Staff> staff )
	{
		var members = staff.ToList();

		return members.Count == 0
			? 0
			: (int)(members.Average( member => member.Happiness ) * MeterFull / 100);
	}

	protected internal override void Update() => ShowMeters();
}
