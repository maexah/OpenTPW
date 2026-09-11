namespace OpenTPW.UI;

/// <summary>
/// One control of the interface: a rectangle on the <see cref="VirtualScreen"/> and what it shows
/// there - a part of a mesh, some text, or both - with its children drawn over it.
///
/// <para>
/// The original builds its controls from layout data compiled into the executable: a stream of 16-bit
/// opcodes, read by 0x0065fd58. Op 0 opens a control with a type, flags, an id and a rectangle, and
/// everything up to op 5 belongs to it - op 1 its mesh, op 3 the rectangle its text goes in, op 0x11
/// its line of help, and further op 0s its children. The windows here are those streams written out
/// by hand, each id and rectangle exactly as the data has it.
/// </para>
/// </summary>
internal class UiControl
{
	private readonly UiText _text = new();
	private bool _followsParent;

	public int Id { get; init; }

	public UiRect Rect { get; set; }

	public UiControl? Parent { get; private set; }

	public List<UiControl> Children { get; } = new();

	/// <summary>Whether it and its children are drawn and can be pointed at.</summary>
	public bool Visible { get; set; } = true;

	public UiMesh? Mesh { get; set; }

	/// <summary>Which part of <see cref="Mesh"/> it shows, before its state is added - set by 0x0065d3a3.</summary>
	public int Frame { get; set; }

	/// <summary>The row of UIHELPTEXT.str the <see cref="HelpBar"/> shows over it, or -1 for none.</summary>
	public int HelpText { get; init; } = -1;

	public string? Text { get; set; }

	/// <summary>Which of the <see cref="UiFonts"/> the text is in.</summary>
	public int Font { get; set; } = -1;

	public UiColour TextColour { get; set; } = UiColour.White;

	public TextAlign TextAcross { get; set; } = TextAlign.Centre;

	public TextAlign TextDown { get; set; } = TextAlign.Centre;

	/// <summary>Where the text goes, when that is not the whole control.</summary>
	public UiRect? TextRect { get; init; }

	/// <summary>Whether the text has the purple skin's dark edge - see <see cref="UiText"/>.</summary>
	public bool TextShadow { get; init; }

	/// <summary>Whether text too wide for its rectangle carries on onto further lines.</summary>
	public bool TextWraps { get; init; }

	/// <summary>Stretched over the whole window instead of keeping the virtual screen's shape - the dimmer behind a dialog.</summary>
	public bool FillsWindow { get; init; }

	/// <summary>
	/// Whether its children keep to it even though it covers the whole screen. A screen laid out as one
	/// piece, like the options screen, needs this: pinned to the window's edges one by one, a row on the
	/// left and the switch that sits on it would drift apart on a wide window.
	/// </summary>
	public bool HoldsChildren { get; init; }

	/// <summary>
	/// Which edge of the window it keeps to across, when the third of the virtual screen it sits in
	/// is not what it belongs with. The four player slots are one column, and their thirds straddle
	/// the middle of the screen, so each says which edge it keeps to rather than being spread down a
	/// tall window. Null leaves it to <see cref="VirtualScreen.AnchorFor"/>.
	/// </summary>
	public Anchor? PinAcross { get; init; }

	/// <summary>Which edge it keeps to down the screen, when the thirds are not what it belongs with - see <see cref="PinAcross"/>.</summary>
	public VerticalAnchor? PinDown { get; init; }

	public Action? Clicked { get; set; }

	public Action? Entered { get; set; }

	public Action? Exited { get; set; }

	internal bool Hovered { get; set; }

	internal bool Pressed { get; set; }

	/// <summary>Which edge of the window it keeps to - see <see cref="VirtualScreen"/>.</summary>
	internal Anchor Anchor => PinAcross ?? (_followsParent && Parent != null ? Parent.Anchor : VirtualScreen.AnchorFor( Rect ));

	/// <summary>Which edge of the window it keeps to down the screen - see <see cref="VirtualScreen"/>.</summary>
	internal VerticalAnchor VerticalAnchor => PinDown ?? (_followsParent && Parent != null ? Parent.VerticalAnchor : VirtualScreen.VerticalAnchorFor( Rect ));

	internal PixelRect Pixels => FillsWindow
		? new PixelRect( 0, 0, Screen.Width, Screen.Height )
		: VirtualScreen.ToPixels( Rect, Anchor, VerticalAnchor );

	/// <summary>Which part of the mesh is added to <see cref="Frame"/> for the state it is in.</summary>
	internal virtual int State => 0;

	/// <summary>Whether the pointer stops at it, rather than passing through to whatever is under it.</summary>
	internal virtual bool TakesMouse => Clicked != null || Entered != null || HelpText >= 0;

	/// <summary>Where on the window it takes the pointer, when it does - all of it, unless its layout data says otherwise.</summary>
	internal virtual PixelRect HitArea => Pixels;

	public T Add<T>( T child ) where T : UiControl
	{
		child.Parent = this;
		child._followsParent = (HoldsChildren || !Rect.IsWholeScreen) && Rect.Contains( child.Rect );
		Children.Add( child );
		return child;
	}

	internal void Draw()
	{
		if ( !Visible )
			return;

		OnDraw();

		foreach ( var child in Children )
			child.Draw();
	}

	protected virtual void OnDraw()
	{
		Mesh?.Draw( Frame + State, Pixels, Rect );
		DrawText( Text );
	}

	protected PixelRect TextArea => TextRect is { } rect ? VirtualScreen.ToPixels( rect, Anchor, VerticalAnchor ) : Pixels;

	protected void DrawText( string? text )
		=> _text.Draw( text, Font, TextColour, TextShadow, TextWraps, TextArea, TextAcross, TextDown );

	/// <summary>The front-most control under a point that takes the pointer, if any.</summary>
	internal UiControl? HitTest( float x, float y )
	{
		if ( !Visible )
			return null;

		for ( int i = Children.Count - 1; i >= 0; --i )
		{
			if ( Children[i].HitTest( x, y ) is { } hit )
				return hit;
		}

		return TakesMouse && HitArea.Contains( x, y ) ? this : null;
	}

	/// <summary>The pointer went down on it, at (<paramref name="x"/>, <paramref name="y"/>) on the window.</summary>
	internal virtual void PointerPressed( float x, float y ) { }

	/// <summary>The pointer moved to (<paramref name="x"/>, <paramref name="y"/>) while held down after going down on it.</summary>
	internal virtual void PointerDragged( float x, float y ) { }

	/// <summary>The pointer came up after going down on it, wherever it is now.</summary>
	internal virtual void PointerReleased() { }

	internal bool IsWithin( UiControl ancestor )
	{
		for ( var control = this; control != null; control = control.Parent )
		{
			if ( control == ancestor )
				return true;
		}

		return false;
	}

	/// <summary>Lets go of the textures its text and its children's text were laid out into.</summary>
	internal void ReleaseText()
	{
		_text.Delete();

		foreach ( var child in Children )
			child.ReleaseText();
	}
}

/// <summary>
/// A button - control type 2.
///
/// Its mesh has a part for each way it can look, in the order b_dellog.md2 names them: normal,
/// disabled, highlighted, highlighted and pressed, held down, and down. A button that stays down - an
/// option in a <see cref="UiRadioGroup"/>, or a switch - shows "down" while it is.
/// </summary>
internal class UiButton : UiControl
{
	private const int Normal = 0;
	private const int Disabled = 1;
	private const int Highlighted = 2;
	private const int HighlightedDown = 3;
	private const int HeldDown = 4;
	private const int Down = 5;

	public bool Enabled { get; set; } = true;

	/// <summary>Whether it stays down on its own - the chosen option of a group, or a switch that is down.</summary>
	public bool IsDown { get; set; }

	/// <summary>
	/// A switch, flagged 0x10 in its layout data: a click puts it down if it was up and up if it was down,
	/// and whoever it tells reads which it is now (the options screen, 0x004a3480). The flag is also what
	/// makes UI_Init's hook play Select3 for its click rather than BUTTON01 (0x00485780).
	/// </summary>
	public bool Toggles { get; init; }

	/// <summary>Whether a click on it tells its parent, and so plays a click - see <see cref="UiSliderThumb"/> for one that does not.</summary>
	internal virtual bool Clicks => true;

	internal override bool TakesMouse => true;

	internal override int State => !Enabled ? Disabled
		: Pressed ? (Hovered ? HighlightedDown : HeldDown)
		: IsDown ? (Hovered ? HighlightedDown : Down)
		: Hovered ? Highlighted
		: Normal;
}

/// <summary>
/// A box to type into - control type 5.
///
/// It holds at most <see cref="MaxLength"/> characters: 0x006662b7 gives it its buffer, and
/// 0x00667833 stops inserting once the text is that long. Text put in with all of it selected is
/// replaced by the first thing typed - 0x006677ae selects the new player dialog's "Type your name"
/// that way - and the selection sits on hilight.wct, which the dialog hands it (0x0066656c).
/// </summary>
internal sealed class UiEdit : UiControl
{
	private static Texture? _highlight;

	private bool _allSelected;

	public int MaxLength { get; init; } = 16;

	/// <summary>Characters it will not take - the dialog refuses the ones a file name cannot hold (0x0066781a).</summary>
	public string Refused { get; init; } = "";

	public string Value { get; private set; } = "";

	/// <summary>Whether what is typed goes to it.</summary>
	internal bool HasFocus { get; set; }

	internal override bool TakesMouse => true;

	public void SetText( string text, bool selectAll )
	{
		Value = text.Length > MaxLength ? text[..MaxLength] : text;
		_allSelected = selectAll && Value.Length > 0;
	}

	internal void Type( string typed )
	{
		foreach ( var character in typed )
		{
			if ( char.IsControl( character ) || Refused.Contains( character ) )
				continue;

			if ( _allSelected )
			{
				Value = "";
				_allSelected = false;
			}

			if ( Value.Length < MaxLength )
				Value += character;
		}
	}

	internal void Backspace()
	{
		if ( _allSelected )
		{
			Value = "";
			_allSelected = false;
		}
		else if ( Value.Length > 0 )
		{
			Value = Value[..^1];
		}
	}

	protected override void OnDraw()
	{
		Mesh?.Draw( Frame + State, Pixels, Rect );

		if ( HasFocus && UiFonts.Get( Font ) is { } font )
		{
			var area = TextArea;
			var scale = UiFonts.Scale;
			var width = font.Measure( Value ) * scale;
			var height = font.LineHeight * scale;
			var top = area.Y + ((area.Height - height) * 0.5f);

			if ( _allSelected )
				DrawHighlight( area.X, top, width, height );
			else if ( Time.Now % 1f < 0.5f )
				DrawHighlight( area.X + width, top, MathF.Max( 2f, 2f * scale ), height );
		}

		DrawText( Value );
	}

	private static void DrawHighlight( float x, float y, float width, float height )
	{
		_highlight ??= new Texture( "ui/textures/hilight.wct" );
		Material.UI.Set( "Color", _highlight );

		using ( _ = new Graphics.Scope( Screen.Size ) )
			Graphics.Quad( new Rectangle( x, Screen.Height - y - height, width, height ), Material.UI );
	}
}

/// <summary>
/// Buttons of which at most one is down - control type 8.
///
/// It starts with none chosen - its constructor, 0x0066a1e8, sets the choice to -1 - and choosing one
/// puts that one down and lifts the rest (0x0066a295, 0x0066a1b0).
/// </summary>
internal sealed class UiRadioGroup : UiControl
{
	public int Selected { get; private set; } = -1;

	public Action? SelectionChanged { get; set; }

	public UiButton AddOption( UiButton option )
	{
		Add( option );
		option.Clicked = () => Select( option.Id );
		return option;
	}

	public void Select( int id )
	{
		if ( id == Selected )
			return;

		Selected = id;

		foreach ( var option in Children.OfType<UiButton>() )
			option.IsDown = option.Id == id;

		SelectionChanged?.Invoke();
	}
}

/// <summary>
/// A slider - control type 3: a thumb that slides along a track, for a whole number between two ends.
///
/// <para>
/// Its constructor (0x0066acf9) gives it a step of 1 and a page of 1, and lays it across unless it is
/// flagged 0x10; the options screen's all lie across, from 0 to 100 (0x0066b47d). Its layout data gives
/// it the track (op 3), the thumb - a button of its own, id 3 (op 8) - and the rectangle that takes the
/// pointer, which reaches a little past the track all round.
/// </para>
/// <para>
/// The thumb sits where the value puts it along the track, less its own width, in whole units
/// (0x0066b088). Dragging it moves it with the pointer as far as the track goes and reads the value off
/// where it is (0x0066af19, on the thumb's messages at 0x0066bb9b); letting go puts it exactly where
/// that value belongs. A press anywhere else in the rectangle moves a page towards the pointer
/// (0x0066b8aa), and the mouse wheel moves a step a notch, a notch away lowering it (message 0x1000d,
/// 0x0066ba22). Each change is told as it happens (message 0x800).
/// </para>
/// </summary>
internal sealed class UiSlider : UiControl
{
	private const int Step = 1;
	private const int Page = 1;

	/// <summary>How far into the thumb, across, the pointer took hold of it.</summary>
	private int _grab;

	/// <summary>What the thumb slides along.</summary>
	public UiRect Track { get; init; }

	/// <summary>What takes the pointer.</summary>
	public UiRect HitRect { get; init; }

	public int Minimum { get; init; }

	public int Maximum { get; init; } = 100;

	public int Value { get; private set; }

	public UiSliderThumb? Thumb { get; private set; }

	/// <summary>The pointer or the wheel changed the value - message 0x800.</summary>
	public Action? Moved { get; set; }

	internal override bool TakesMouse => true;

	internal override PixelRect HitArea => VirtualScreen.ToPixels( HitRect, Anchor, VerticalAnchor );

	public UiSliderThumb AddThumb( UiSliderThumb thumb )
	{
		Thumb = Add( thumb );
		thumb.Slider = this;
		PlaceThumb();
		return thumb;
	}

	/// <summary>Sets the value without telling anyone - whoever sets it shows it themselves.</summary>
	public void SetValue( int value )
	{
		Value = Math.Clamp( value, Minimum, Maximum );
		PlaceThumb();
	}

	/// <summary>The mouse wheel turned while the pointer was over it.</summary>
	internal void Scroll( float notches ) => MoveBy( -(int)notches * Step );

	/// <summary>A press on the slider but not on the thumb: a page towards the pointer.</summary>
	internal override void PointerPressed( float x, float y )
	{
		if ( Thumb == null )
			return;

		MoveBy( VirtualScreen.ToVirtualX( x, Anchor ) < Thumb.Rect.Left ? -Page : Page );
	}

	internal void Grab( float x )
	{
		if ( Thumb != null )
			_grab = (int)VirtualScreen.ToVirtualX( x, Anchor ) - Thumb.Rect.Left;
	}

	internal void Drag( float x )
	{
		if ( Thumb == null )
			return;

		var thumb = Thumb.Rect;
		var left = Math.Clamp( (int)VirtualScreen.ToVirtualX( x, Anchor ) - _grab, Track.Left, Track.Right - thumb.Width );
		Thumb.Rect = thumb with { Left = left, Right = left + thumb.Width };

		var travel = Track.Width - thumb.Width;
		var value = travel > 0 ? ((left - Track.Left) * (Maximum - Minimum) / travel) + Minimum : Minimum;

		if ( value == Value )
			return;

		Value = value;
		Moved?.Invoke();
	}

	internal void LetGo() => PlaceThumb();

	private void MoveBy( int amount )
	{
		var value = Math.Clamp( Value + amount, Minimum, Maximum );

		if ( value == Value )
			return;

		Value = value;
		PlaceThumb();
		Moved?.Invoke();
	}

	private void PlaceThumb()
	{
		if ( Thumb == null )
			return;

		var thumb = Thumb.Rect;
		var range = Maximum - Minimum;
		var left = range != 0 ? ((Value - Minimum) * (Track.Width - thumb.Width) / range) + Track.Left : Track.Left;

		Thumb.Rect = thumb with { Left = left, Right = left + thumb.Width };
	}
}

/// <summary>
/// A slider's thumb. It looks and lights up like any button, but pressing it takes hold of the slider
/// instead of clicking: its handler (0x0066bb9b) keeps the press to itself and tells nobody, so no
/// click plays.
/// </summary>
internal sealed class UiSliderThumb : UiButton
{
	internal UiSlider? Slider { get; set; }

	internal override bool Clicks => false;

	internal override void PointerPressed( float x, float y ) => Slider?.Grab( x );

	internal override void PointerDragged( float x, float y ) => Slider?.Drag( x );

	internal override void PointerReleased() => Slider?.LetGo();
}
