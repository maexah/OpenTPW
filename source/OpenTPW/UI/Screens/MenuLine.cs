namespace OpenTPW.UI;

/// <summary>
/// A line of the game menu's kind: its text in the menu's resting teal (or another resting colour its owner gives it), turning grey and then white while the pointer is on it,
/// and straight back when the pointer goes (<c>0x0048b2a0</c>, <c>0x0048b220</c>; <see cref="GameMenu"/>). The menu's choices are
/// these, and so is the update line (<see cref="UpdateNotice"/>), which behaves as a choice does so that it reads as clickable.
/// The owner ticks it 30 times a second (<see cref="TicksPerSecond"/>) and calls <see cref="Tick"/>.
/// </summary>
internal class MenuLine : UiControl
{
	/// <summary>How fast the colour ticks: one tick a frame in the original, taken here as 30 a second at any frame rate.</summary>
	public const float TicksPerSecond = 30f;

	/// <summary>The colour a line rests in: (0, 175, 190), which the menu gives a choice as it is shown (message 0x11).</summary>
	public static readonly UiColour Resting = new( 0, 175, 190 );

	/// <summary>
	/// What 0x0048b2a0 keeps in the control's second slot: above 0 the grey brightening, below 0
	/// counting down once the pointer has gone, 0 still.
	/// </summary>
	public int Ramp { get; set; }

	private UiColour _rest = Resting;

	/// <summary>The colour this line rests in and goes back to: <see cref="Resting"/> unless its owner sets another (the update line's gold).</summary>
	public UiColour RestColour
	{
		get => _rest;
		init
		{
			_rest = value;
			TextColour = value;
		}
	}

	public MenuLine()
	{
		TextColour = Resting;
		Entered = () => Ramp = 1;
		Exited = () => Ramp = -1;
	}

	/// <summary>One tick of the line's colour - 0x0048b220.</summary>
	public void Tick()
	{
		if ( Ramp > 0 )
		{
			var grey = Ramp + 32;
			var next = grey;

			if ( grey > 255 )
			{
				grey = 255;
				next = 0;
			}

			TextColour = new UiColour( (byte)grey, (byte)grey, (byte)grey );
			Ramp = next;
		}
		else if ( Ramp < 0 )
		{
			TextColour = _rest;
			Ramp = Ramp + 223 < 0 ? 0 : Ramp - 32;
		}
	}
}
