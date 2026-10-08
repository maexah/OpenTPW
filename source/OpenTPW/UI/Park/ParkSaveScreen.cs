namespace OpenTPW.UI;

/// <summary>
/// Save Park, the park menu's second row: the player's saved parks for this theme, a box to name the save in, and an
/// OK button (<c>FUN_0049f280</c>, the handler at <c>0x0049ea30</c>; <c>docs/exe/saves.md</c>, "One screen, two
/// uses" to "What the handlers answer", and "OpenTPW's Save Park").
///
/// <para>
/// <b>The layout</b> is <see cref="ParkFileScreen"/>'s, which Load Park shares, with the stream's OK button and
/// name box.
/// </para>
/// <para>
/// <b>The name box</b> holds fifteen characters (<c>FUN_006662b7( 0x007ca720, 0xf )</c>), refuses the nine a file
/// name cannot hold (<c>0x00752588</c>), and opens on UITEXT 206, "New Save", all of it selected, with the keys: the
/// opener turns the keyboard shortcut tables off (<c>FUN_00486b70</c>), which here is the box having the focus.
/// Enter in it is the OK button and Escape the cancel button (<c>0x802</c>, <c>0x804</c>).
/// </para>
/// <para>
/// <b>A row's click</b> puts that save's name in the box, not selected, so what is typed next goes on its end.
/// </para>
/// <para>
/// <b>OK</b> reads the folder again and compares the box's text with each save's name, character for character
/// (<c>FUN_0067c290</c>, so "new save" is not "New Save"). A name in the folder is asked about first, UITEXT 205
/// handed the name; any other is saved at once, an empty one too. The name is not trimmed.
/// </para>
/// <para>
/// <b>The save is not built.</b> <c>FUN_0049e9b0</c> writes the player and then the park
/// (<c>FUN_005ac610</c>) and closes the screen; here it is counted, <c>SAVE_GAME_WRITER</c>, and the screen closes.
/// </para>
/// </summary>
internal sealed class ParkSaveScreen : ParkFileScreen
{
	/// <summary>What the name box refuses, the wide string at <c>0x00752588</c>.</summary>
	private const string NotInNames = "\\/*?:|<>\"";

	/// <summary>UITEXT 205, "{2} exists / Overwrite ?", whose parameter 2 is the name.</summary>
	private const int OverwriteQuestion = 205;

	private readonly UiEdit _name;

	public ParkSaveScreen( WindowStack stack, string theme ) : base( stack, theme, UIStrings.SavePark )
	{
		Frame.Add( new UiButton
		{
			Id = -1,
			Rect = new UiRect( 1660, 730, 1743, 813 ),
			Mesh = UiMesh.Get( "b_okay" ),
			Clicked = Accept
		} );

		_name = Frame.Add( new UiEdit
		{
			Id = 0x23bce2,
			Rect = new UiRect( 605, 811, 1343, 895 ),
			Mesh = UiMesh.Get( "!frame" ),
			TextRect = new UiRect( 638, 830, 1317, 874 ),
			Font = RowFont,
			TextAcross = TextAlign.Start,
			MaxLength = 15,
			Refused = NotInNames
		} );

		_name.SetText( Localization.Get( UIStrings.NewSave ), selectAll: true );
		Focus = _name;

		Fill( "Save Park" );
	}

	/// <summary>What the name box holds.</summary>
	internal string Name => _name.Value;

	/// <summary>
	/// A click on a row (<c>0x400</c>, <c>0x0049ebd0</c>): the folder is read again and the name of the entry at the
	/// row's place goes into the box, through the box's own setter, which leaves it unselected with the caret at its
	/// end (<c>FUN_00666308</c>). A place the folder no longer has changes nothing.
	/// </summary>
	protected override void RowClicked( int row )
	{
		var parks = Folder();

		if ( row < 0 || row >= parks.Count )
			return;

		_name.SetText( parks[row].Name, selectAll: false );
		Log.Info( $"Save Park: row {row}'s name, '{_name.Value}', is in the box" );
	}

	/// <summary>
	/// The OK button, and Enter in the box (<c>0x100</c> with -1, <c>0x0049eae1</c>).
	/// </summary>
	/// <remarks>
	/// The question takes the keys from the box and they are not handed back when it closes: after its cross the box
	/// shows no caret and hears nothing, Enter and Escape with it, until a click on the box (measured in the
	/// original, <c>docs/exe/saves.md</c>, "Measured (Q241d)").
	/// </remarks>
	protected internal override void Accept()
	{
		var name = _name.Value;

		if ( !Folder().Any( park => string.Equals( park.Name, name, StringComparison.Ordinal ) ) )
		{
			Save();
			return;
		}

		Log.Info( $"Save Park: '{name}' is in the folder, so the overwrite is asked about" );

		Focus = null;
		_name.HasFocus = false;

		Stack.Open( new MessageBox( Stack, Localization.Format( OverwriteQuestion, (2, name) ), Save ) );
	}

	/// <summary>
	/// Escape in the box (<c>0x804</c>), which posts the cancel button's own message (<c>0x0049ec43</c>), so it
	/// clicks as the button does.
	/// </summary>
	protected internal override void Cancel()
	{
		UiSounds.Click( toggle: false );
		Stack.Close( this );
	}

	/// <summary>
	/// <c>FUN_0049e9b0</c>: the save under the box's name, then the screen closes whatever the save answered. The
	/// save itself, the player's <c>gms.dat</c> and then <c>&lt;name&gt;.TPWS</c> in the player's folder for the
	/// theme, is counted and writes nothing.
	/// </summary>
	private void Save()
	{
		Unimplemented.Report( "SAVE_GAME_WRITER" );
		Log.Info( $"Save Park: '{_name.Value}' is not written - nothing writes a park back yet - and the screen closes" );

		Stack.Close( this );
	}
}
