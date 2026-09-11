namespace OpenTPW.UI;

/// <summary>
/// The game menu Escape brings up: a column of choices down from the top of the dimmed screen.
///
/// <para>
/// GameMenu_Open (0x0048c830) builds it in code rather than from a layout stream - GameMenu_BuildLobby
/// (0x0048c600) in the lobby, GameMenu_BuildPark (0x0048c150) in a park, both on the same menu list. A
/// control over the whole screen wearing LOLIGHT.MD2 dims everything behind and takes the pointer
/// (0x00492e80), and MenuList_AddItem (0x00492f60) stacks the choices on it. Each is its text in font 0 on
/// the purple skin, centred across the screen, as wide as the text and as tall as a line of it and five
/// pixels more, turned from the screen's pixels into the layout's units. In the lobby the first starts five
/// units down and each of the others five below the one before.
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
	private const int FirstTop = 5;
	private const int Gap = 5;

	/// <summary>How fast the colour ticks - see the class remarks.</summary>
	private const float TicksPerSecond = 30f;

	private static readonly UiColour Resting = new( 0, 175, 190 );

	private readonly List<Choice> _choices = new();

	/// <summary>The font set the choices were last sized for.</summary>
	private int _laidOutFor = -1;

	private float _ticks;

	public GameMenu( FrontEnd frontEnd, IReadOnlyList<Item> items ) : base( frontEnd )
	{
		Modal = true;
		Pauses = true;

		Root = Backdrop();

		foreach ( var item in items )
			AddChoice( item );

		LayOut();
	}

	protected internal override void Update()
	{
		LayOut();

		_ticks = MathF.Min( _ticks + (Time.Delta * TicksPerSecond), TicksPerSecond );

		for ( ; _ticks >= 1f; _ticks -= 1f )
		{
			foreach ( var choice in _choices )
				Tick( choice );
		}
	}

	private void AddChoice( Item item )
	{
		var choice = Root.Add( new Choice
		{
			Id = item.Id,
			Text = Localization.Get( item.Text ),
			Font = ChoiceFont,
			TextColour = Resting,
			TextShadow = true
		} );

		choice.Entered = () => choice.Ramp = 1;
		choice.Exited = () => choice.Ramp = -1;
		choice.Clicked = () => Choose( item );

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
		var top = FirstTop;

		foreach ( var choice in _choices )
		{
			var width = ((font?.Measure( choice.Text ?? "" ) ?? 0) + 1) * VirtualScreen.Width / screenWidth;
			var height = ((font?.LineHeight ?? 0) + 5) * VirtualScreen.Height / screenHeight;
			var left = ((VirtualScreen.Width - 1) >> 1) - (width >> 1);

			choice.Rect = new UiRect( left, top, left + width, top + height );
			top += height + Gap;
		}
	}

	/// <summary>One tick of a choice's colour - 0x0048b220.</summary>
	private static void Tick( Choice choice )
	{
		if ( choice.Ramp > 0 )
		{
			var grey = choice.Ramp + 32;
			var next = grey;

			if ( grey > 255 )
			{
				grey = 255;
				next = 0;
			}

			choice.TextColour = new UiColour( (byte)grey, (byte)grey, (byte)grey );
			choice.Ramp = next;
		}
		else if ( choice.Ramp < 0 )
		{
			choice.TextColour = Resting;
			choice.Ramp = choice.Ramp + 223 < 0 ? 0 : choice.Ramp - 32;
		}
	}

	/// <summary>A choice clicked: the menu's click (effect 193), then whatever the choice does.</summary>
	private void Choose( Item item )
	{
		UiSounds.MenuChoice();
		item.Chosen( this );
	}

	/// <summary>A choice, and where its colour is going.</summary>
	private sealed class Choice : UiControl
	{
		/// <summary>
		/// What 0x0048b2a0 keeps in the control's second slot: above 0 the grey brightening, below 0
		/// counting down once the pointer has gone, 0 still.
		/// </summary>
		public int Ramp { get; set; }
	}
}
