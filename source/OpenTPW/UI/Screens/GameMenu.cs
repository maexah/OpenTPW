namespace OpenTPW.UI;

/// <summary>
/// The game menu Escape brings up: a column of choices down from the top of the dimmed screen.
///
/// <para>
/// The original builds it in code rather than from a layout stream - GameMenu_BuildLobby in the lobby,
/// GameMenu_BuildPark in a park, both on the same menu list (docs/exe/ui.md, "Escape game menu"). A
/// control over the whole screen wearing LOLIGHT.MD2 dims everything behind and takes the pointer
/// (0x00492e80), and MenuList_AddItem (0x00492f60) stacks the choices on it. Each is its text in font 0 on
/// the purple skin, centred across the screen, as wide as the text and as tall as a line of it and five
/// pixels more, turned from the screen's pixels into the layout's units. In the lobby the first starts five
/// units down; in a park it starts at ten. Each of the others sits five below the one before, in both -
/// except in a park whose FUN_005b6450 answers non-zero: the first at 0, and no gap.
/// </para>
/// <para>
/// <b>The pointer.</b> A choice rests at (0, 175, 190), which it is given as it is shown (message 0x11).
/// When the pointer comes onto one it turns a grey of 33, and 32 brighter each tick until it is white;
/// when the pointer goes off it goes straight back (0x0048b2a0, 0x0048b220). The original ticks once a
/// frame (message 0x1e, 0x00658f97). Here that is taken as 30 ticks a second at any frame rate, which
/// is an assumption about the pace it ran at. A click plays effect 193 of the interface's sounds
/// (0x00492d80) and then does the choice.
/// </para>
/// <para>
/// <b>A deviation: the build and the update line.</b> The original's menu shows neither. This one letters the build bottom
/// right in <see cref="VersionLabel"/>'s style, in the lobby and in a park alike, and above it, when the update check has
/// found a newer weekly build, <see cref="UpdateNotice"/>'s line, which acts as a choice does (teal, white under the pointer)
/// and opens the release page when clicked (the menu's dimmer takes the pointer, so the line is wired here). Both are read as
/// the menu opens: a check that lands during play shows at the next menu, and neither is a choice (<see cref="_choices"/> holds
/// the menu's own).
/// </para>
/// <para>
/// <b>Engine and content.</b> The menu list is engine: its layout, its colours and its click, the same for
/// a park's menu as for the lobby's. The choices - their text, their ids and what each does - are content,
/// handed over by whoever opens it; the lobby's are the front end's.
/// </para>
/// </summary>
internal sealed class GameMenu : UiWindow
{
	/// <summary>One choice: its text, its id in the original's menu handler, and what choosing it does.</summary>
	internal readonly record struct Item( UIStrings Text, int Id, Action<GameMenu> Chosen );

	private const int ChoiceFont = 0;
	private const int Gap = 5;

	/// <summary>How far down the first choice starts, which is the scene's to say - see the class remarks.</summary>
	private readonly int _firstTop;

	private readonly List<MenuLine> _choices = new();

	/// <summary>The font set the choices were last sized for.</summary>
	private int _laidOutFor = -1;

	private float _ticks;

	/// <summary>The build, bottom right, and the update line beside it where there is a newer build.</summary>
	private readonly UiControl _build;

	private readonly MenuLine? _notice;

	/// <param name="firstTop">
	/// How far down the first choice starts: five in the lobby and ten in a park - see the class remarks.
	/// </param>
	public GameMenu( WindowStack stack, IReadOnlyList<Item> items, int firstTop ) : base( stack )
	{
		Modal = true;
		Pauses = true;

		// MenuList_Show closes a park's open screen before the menu is shown (0x00493171).
		ClosesParkScreen = true;
		_firstTop = firstTop;

		Root = Backdrop();

		foreach ( var item in items )
			AddChoice( item );

		_build = Root.Add( new UiControl
		{
			Id = VersionLabel.ControlId,
			Font = VersionLabel.LabelFont,
			Text = LaunchReport.RunningBuildLabel,
			TextColour = UiColour.White,
			TextAcross = TextAlign.Start,
			TextDown = TextAlign.Centre
		} );

		if ( UpdateChecker.Available != null )
			_notice = Root.Add( UpdateNotice.Make() );

		LayOut();
	}

	protected internal override void Update()
	{
		LayOut();

		_ticks = MathF.Min( _ticks + (Time.Delta * MenuLine.TicksPerSecond), MenuLine.TicksPerSecond );

		for ( ; _ticks >= 1f; _ticks -= 1f )
		{
			foreach ( var choice in _choices )
				choice.Tick();

			_notice?.Tick();
		}
	}

	private void AddChoice( Item item )
	{
		var choice = Root.Add( new MenuLine
		{
			Id = item.Id,
			Text = Localization.Get( item.Text ),
			Font = ChoiceFont,
			TextShadow = true,

			// One column down from the top of the screen, so they keep to the top together however
			// many there are - a longer menu would otherwise cross into the middle third and the
			// choices below that would part from the ones above on a tall window.
			PinDown = VerticalAnchor.Top
		} );

		// A row is not a button: it answers the base proc's click, whatever the button (MenuList_ChoiceCallback,
		// 0x00492d8f), so a press held half a second chooses nothing.
		choice.LeftClicked = choice.RightClicked = () => Choose( item );

		_choices.Add( choice );
	}

	/// <summary>Sizes and stacks the choices as MenuList_AddItem does, again whenever the window's size changes the font set.</summary>
	private void LayOut()
	{
		if ( UiFonts.SetIndex == _laidOutFor )
			return;

		_laidOutFor = UiFonts.SetIndex;

		var font = UiFonts.Get( ChoiceFont );
		var screenHeight = UiFonts.SetScreenHeight;
		var screenWidth = screenHeight * 4 / 3;
		var top = _firstTop;

		foreach ( var choice in _choices )
		{
			var width = ((font?.Measure( choice.Text ?? "" ) ?? 0) + 1) * VirtualScreen.Width / screenWidth;
			var height = ((font?.LineHeight ?? 0) + 5) * VirtualScreen.Height / screenHeight;
			var left = ((VirtualScreen.Width - 1) >> 1) - (width >> 1);

			choice.Rect = new UiRect( left, top, left + width, top + height );
			top += height + Gap;
		}

		if ( VersionLabel.LabelBox( _build.Text! ) is { } build )
			_build.Rect = build;

		if ( _notice != null && VersionLabel.NoticeBox( _build.Text!, _notice.Text! ) is { } notice )
			_notice.Rect = notice;
	}

	/// <summary>A choice clicked: the menu's click (effect 193), then whatever the choice does.</summary>
	private void Choose( Item item )
	{
		UiSounds.MenuChoice();
		item.Chosen( this );
	}
}
