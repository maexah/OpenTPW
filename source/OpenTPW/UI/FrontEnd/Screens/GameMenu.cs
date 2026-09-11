namespace OpenTPW.UI;

/// <summary>
/// The menu Escape brings up in the lobby: a column of choices down from the top of the dimmed screen.
///
/// <para>
/// GameMenu_Open (0x0048c830) builds it in code rather than from a layout stream - GameMenu_BuildLobby
/// (0x0048c600) in the lobby, GameMenu_BuildPark (0x0048c150) in a park. A control over the whole
/// screen wearing LOLIGHT.MD2 dims everything behind and takes the pointer (0x00492e80), and
/// MenuList_AddItem (0x00492f60) stacks the choices on it. Each is its text in font 0 on the purple
/// skin, centred across the screen, as wide as the text and as tall as a line of it and five pixels
/// more, turned from the screen's pixels into the layout's units. In the lobby the first starts five
/// units down and each of the others five below the one before.
/// </para>
/// <para>
/// <b>The lobby's choices</b>, top to bottom, with their ids in its handler (0x0048bd40): Go Online (9),
/// or Go Offline (10) while online, which is never here; Options (11), left out while the online side
/// is busy, which it never is; Select New Player (12), only when the front end's screen says so, taken
/// here to mean while someone is playing; then Resume Game (14) and Quit Game (15). Return To Park (13)
/// takes the place of the first three while visiting someone else's park online.
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
/// <b>What they do.</b> Go Online would start connecting (0x005b5cc0), but the online world is a dead
/// end here, so it only closes the menu. Options closes it and opens the <see cref="OptionsScreen"/>.
/// Select New Player and Quit Game ask first, in a message box over the menu (UITEXT 14 and 9), and the
/// menu stays until the tick (0x0048bc50, 0x0048bc30). Resume Game closes it, and so does Escape.
/// </para>
/// </summary>
internal sealed class GameMenu : UiWindow
{
	private const int ChoiceFont = 0;
	private const int FirstTop = 5;
	private const int Gap = 5;

	private const int GoOnlineId = 9;
	private const int OptionsId = 11;
	private const int SelectNewPlayerId = 12;
	private const int ResumeGameId = 14;
	private const int QuitGameId = 15;

	/// <summary>How fast the colour ticks - see the class remarks.</summary>
	private const float TicksPerSecond = 30f;

	private static readonly UiColour Resting = new( 0, 175, 190 );

	private readonly List<Choice> _choices = new();

	/// <summary>The font set the choices were last sized for.</summary>
	private int _laidOutFor = -1;

	private float _ticks;

	public GameMenu( FrontEnd frontEnd ) : base( frontEnd )
	{
		Modal = true;

		Root = Backdrop();

		AddChoice( UIStrings.GoOnline, GoOnlineId );
		AddChoice( UIStrings.Options, OptionsId );

		if ( frontEnd.Players.Current != null )
			AddChoice( UIStrings.SelectNewPlayer, SelectNewPlayerId );

		AddChoice( UIStrings.ResumeGame, ResumeGameId );
		AddChoice( UIStrings.QuitGame, QuitGameId );

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

	private void AddChoice( UIStrings text, int id )
	{
		var choice = Root.Add( new Choice
		{
			Id = id,
			Text = Localization.Get( text ),
			Font = ChoiceFont,
			TextColour = Resting,
			TextShadow = true
		} );

		choice.Entered = () => choice.Ramp = 1;
		choice.Exited = () => choice.Ramp = -1;
		choice.Clicked = () => Choose( id );

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

	private void Choose( int id )
	{
		UiSounds.MenuChoice();

		switch ( id )
		{
			case GoOnlineId:
				FrontEnd.Close( this );
				Log.Info( "Front end: Go Online - the online world is a dead end, so nothing more happens" );
				break;

			case OptionsId:
				FrontEnd.Close( this );
				FrontEnd.OpenOptions();
				break;

			case SelectNewPlayerId:
				FrontEnd.Open( new MessageBox( FrontEnd, Localization.Get( UIStrings.ConfirmNewPlayer ), () =>
				{
					FrontEnd.Close( this );
					FrontEnd.SelectNewPlayer();
				} ) );
				break;

			case ResumeGameId:
				FrontEnd.Close( this );
				break;

			case QuitGameId:
				FrontEnd.AskToQuit();
				break;
		}
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
