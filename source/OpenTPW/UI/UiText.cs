namespace OpenTPW.UI;

/// <summary>
/// A control's text, drawn in one of the interface's fonts.
///
/// <para>
/// The original renders a control's text into a buffer of its own and puts that on the screen, and
/// this does the same: the lettering is laid out once into a texture at the font's own pixel size,
/// scaled onto the window from there, and only laid out again when the text, font, colour or wrap
/// width changes.
/// </para>
/// <para>
/// The purple skin every big button and dialog title wears (0x00491ab0) draws its text twice: first
/// in grey at half strength, two pixels to the left and two down, and then at full strength where it
/// belongs, which gives the lettering its dark edge. The plain label skin (0x0048f830) draws it once.
/// The original's first pass sets the glyph colour to (0x80, 0x80, 0x80, 0x80); in its A4R4G4B4 bitmap that
/// is a grey of nibble 8 (0.533), not the half this uses, at alpha 17 x floor( c x 128 / 255 ), and the
/// string's own texels replace the grey's wherever they are set, where this composites Over: a deviation,
/// Q517. The texture is the box, as the original's bitmap is (<see cref="BitmapOf"/>), so text past it is cut (Q771).
/// docs/exe/ui.md, "The two-pass text class".
/// </para>
/// </summary>
internal sealed class UiText
{
	/// <summary>How far the purple skin's grey copy is moved: left and down by this (0x00491dcc..0x00491dfb).</summary>
	private const int ShadowOffset = 2;

	/// <summary>
	/// The width of the box the renderer is handed for a label that does not wrap (<c>LabelSkin_Draw</c> 0x0048f830,
	/// <c>+0xda</c> nought): left at the bitmap's 0, right at its width, centre about its half (0x0048f943).
	/// </summary>
	private const int UnwrappedBox = 0x4000;

	private Texture? _texture;
	private int _width;
	private int _height;
	private Layout _built;
	private Measure _measured;
	private List<string> _lines = [];
	private int _textWidth;
	private int _textHeight;

	/// <summary>The laid-out text's RGBA pixels at the font's own size, and how many texels a pixel is copied to in <see cref="_texture"/>.</summary>
	private byte[] _pixels = [];
	private int _prescale;

	private readonly record struct Measure( string Text, int Font, int Set, int WrapWidth );

	private readonly record struct Layout( Measure Measure, UiColour Colour, bool Shadow, TextAlign Across, TextAlign Down, int Width, int Height );

	/// <param name="size">
	/// How much larger than the original's the lettering is wanted (<see cref="UiControl.LetteringSize"/>): it is drawn
	/// at that size where the box holds it, and as large as the box holds where not, never smaller than the original's.
	/// A wrapping text is broken at the width it has at that size.
	/// </param>
	public void Draw( string? text, int font, UiColour colour, bool shadow, bool wrap, PixelRect area, TextAlign across, TextAlign down,
		float size = 1f )
	{
		if ( string.IsNullOrEmpty( text ) || UiFonts.Get( font ) is not { } bitmap )
			return;

		var measure = new Measure( text, font, UiFonts.SetIndex, wrap ? WrapWidthAt( area.Width, size ) : 0 );

		if ( measure != _measured || _lines.Count == 0 )
			Lay( bitmap, measure );

		var (placed, drawn, prescale, width, height) = Plan( area, _textWidth, _textHeight, UiFonts.Scale, size );

		// A box under one font pixel either way has no bitmap to letter into.
		if ( width <= 0 || height <= 0 )
			return;

		var layout = new Layout( measure, colour, shadow, across, down, width, height );
		var built = _texture == null || layout != _built;

		if ( built )
			Build( bitmap, layout );

		Upload( prescale, built );

		if ( _texture == null )
			return;

		if ( built && Census )
		{
			var (textWidth, textHeight) = (_textWidth * UiFonts.Scale * drawn, _textHeight * UiFonts.Scale * drawn);
			Log.Info( $"UI text: \"{text}\" font {font}, {textWidth:0} x {textHeight:0} in a box {area.Width:0} x {area.Height:0}, x {drawn:0.000}"
				+ (textWidth > area.Width + 0.5f || textHeight > area.Height + 0.5f ? " OVER" : "")
				+ $"; bitmap {width} x {height}, text {_textWidth} x {_textHeight}"
				+ (_textWidth + (shadow ? ShadowOffset : 0) > width || _textHeight + (shadow ? ShadowOffset : 0) > height ? ", cut" : "") );
		}

		Material.UI.Set( "Color", _texture );

		using ( _ = new Graphics.Scope( Screen.Size ) )
			Graphics.Quad( new Rectangle( MathF.Round( placed.X ), MathF.Round( Screen.Height - placed.Y - placed.Height ), placed.Width, placed.Height ), Material.UI );
	}

	/// <summary>
	/// How a text of <paramref name="textWidth"/> by <paramref name="textHeight"/> font pixels is drawn in
	/// <paramref name="area"/>: the size it is drawn at (<see cref="SizeIn"/>), its bitmap at that size
	/// (<see cref="BitmapOf"/>), that bitmap's place, its top-left at the box's from the top of the screen and
	/// <paramref name="fontScale"/> times the size screen pixels a bitmap pixel, and how many texels a font pixel is copied
	/// to (<see cref="PrescaleFor(float)"/>), chosen from the scale drawn at once the box has held the text, not the size wanted.
	/// </summary>
	internal static (Rectangle Placed, float Size, int Prescale, int Width, int Height) Plan( PixelRect area, int textWidth, int textHeight,
		float fontScale, float wanted )
	{
		var size = SizeIn( wanted, textWidth * fontScale, textHeight * fontScale, area.Width, area.Height );
		var scale = fontScale * size;
		var (width, height) = BitmapOf( area, scale );

		return (new Rectangle( area.X, area.Y, width * scale, height * scale ), size, PrescaleFor( scale ), width, height);
	}

	/// <summary>
	/// The bitmap a text control letters into, in font pixels: its box over <paramref name="scale"/> screen pixels a font
	/// pixel, each cut toward nought. At the original's size that is the original's own, made once at the control's size:
	/// (x1 - x0) x the screen width / 2048 (<c>0x0048f47b</c>..<c>0x0048f49e</c>, <c>+0x3c</c>) by (y1 - y0) x the screen height
	/// / 1536 (<c>0x0048f4a2</c>..<c>0x0048f4c5</c>, <c>+0x3e</c>), the same cut <see cref="WrapWidthOf"/> makes. Here it is made
	/// again whenever the box or the size changes, as a window can be resized here; the original's is made once (guard <c>+0x44</c>).
	/// A thousandth of a pixel is forgiven, so a text whose box held it at <see cref="SizeIn"/>'s size is not cut by a
	/// float's rounding (30 / 1.2 is 24.99999 in floats).
	/// </summary>
	internal static (int Width, int Height) BitmapOf( PixelRect area, float scale )
		=> ((int)((area.Width / scale) + 0.001f), (int)((area.Height / scale) + 0.001f));

	/// <summary>
	/// A line's left edge in a bitmap <paramref name="width"/> wide, as the renderer places it (<c>0x006ae550</c>; docs/exe/hud.md,
	/// "How a list's cells are aligned"): from the left at 0, from the right at the width less the line's, and centred
	/// about the middle of the box the renderer is handed, half the line's width cut toward nought. A wrapping label's box
	/// is the bitmap itself; a label that does not wrap is handed one <see cref="UnwrappedBox"/> wide about the bitmap's
	/// half (0x0048f943). The centred move is shifted unsigned, so a wrapped line wider than its box moves off the bitmap.
	/// </summary>
	internal static int LineLeft( int width, int lineWidth, TextAlign across, bool wraps )
	{
		switch ( across )
		{
			case TextAlign.Start:
				return 0;

			case TextAlign.End:
				return width - lineWidth;
		}

		var (left, box) = wraps ? (0, width) : ((width / 2) - (UnwrappedBox / 2), UnwrappedBox);
		var move = (long)((uint)(box - lineWidth) >> 1);

		return (int)Math.Min( left + move, int.MaxValue );
	}

	/// <summary>
	/// The top of a block of lines <paramref name="blockHeight"/> high in a bitmap <paramref name="height"/> high
	/// (<c>0x006aeb10</c>): at 0, at the bottom less the block, or in the middle, the difference halved toward nought and not
	/// held, so a block taller than its bitmap rises above it and is cut at both edges.
	/// </summary>
	internal static int BlockTop( int height, int blockHeight, TextAlign down ) => down switch
	{
		TextAlign.Start => 0,
		TextAlign.End => height - blockHeight,
		_ => (height - blockHeight) / 2
	};

	/// <summary>
	/// How much larger than the original's a text is drawn: <paramref name="wanted"/>, or less where its box
	/// (<paramref name="areaWidth"/> by <paramref name="areaHeight"/>) is too small for that, and never less than 1. The
	/// text's sizes are at the original's.
	/// </summary>
	internal static float SizeIn( float wanted, float textWidth, float textHeight, float areaWidth, float areaHeight )
	{
		var size = wanted;

		if ( textWidth > 0f )
			size = MathF.Min( size, areaWidth / textWidth );

		if ( textHeight > 0f )
			size = MathF.Min( size, areaHeight / textHeight );

		return MathF.Max( 1f, size );
	}

	/// <summary>
	/// Whether each text laid out is logged with its drawn size and its box, its bitmap and its text in font pixels, and
	/// whether the bitmap cuts it (<c>OPENTPW_TEXT_CENSUS=1</c>).
	/// </summary>
	private static readonly bool Census = Environment.GetEnvironmentVariable( "OPENTPW_TEXT_CENSUS" ) == "1";

	/// <summary>
	/// The width a wrapping control's text breaks at, in the font's pixels: the control's drawn width over the font's scale,
	/// cut toward nought as the original cuts it ((x1 - x0) x the screen width / 2048, <c>0x0048f47b</c>; docs/exe/hud.md,
	/// "The help bar's lettering").
	/// </summary>
	internal static int WrapWidthOf( float areaWidth ) => (int)(areaWidth / UiFonts.Scale);

	/// <summary>
	/// How many texels a font pixel is copied to before the sampler scales the text onto the screen: the whole number at
	/// or under <paramref name="scale"/>, 1 to 4. Enlarged straight from the font's pixels, the linear sampler spreads a
	/// one-pixel stroke over two and dims it; copied first, a stroke keeps its full strength and only its edges are
	/// blended. Handed the scale the text is drawn at (<see cref="Plan"/>), so never more than it: a text is never drawn
	/// smaller than its texture and the sampler reads only the first level, the one <see cref="Texture.UpdatePixels"/> rewrites. A deviation of OpenTPW's own: the original
	/// never scales a font (Q644; <c>ui.md</c>, "<c>UI_SetTitle</c>"). Turned off, so 1, by <c>graphics faithful</c>
	/// (<see cref="GameOptions.Faithful"/>, Q645): the sampler alone enlarges the text.
	/// </summary>
	internal static int PrescaleFor( float scale ) => PrescaleFor( scale, GameOptions.Current.Faithful );

	/// <summary><see cref="PrescaleFor(float)"/> with the toggle handed in: always 1 under <paramref name="faithful"/>.</summary>
	internal static int PrescaleFor( float scale, bool faithful )
		=> faithful ? 1 : Math.Clamp( (int)MathF.Floor( scale + 0.001f ), 1, 4 );

	/// <summary>The RGBA <paramref name="pixels"/> with each pixel copied into an <paramref name="n"/> by <paramref name="n"/> block.</summary>
	internal static byte[] Prescaled( byte[] pixels, int width, int height, int n )
	{
		if ( n == 1 )
			return pixels;

		var texels = new byte[pixels.Length * n * n];

		for ( int y = 0; y < height * n; ++y )
		{
			for ( int x = 0; x < width * n; ++x )
				Array.Copy( pixels, ((y / n * width) + (x / n)) * 4, texels, ((y * width * n) + x) * 4, 4 );
		}

		return texels;
	}

	/// <summary>The width a wrapping text breaks at when it is wanted <paramref name="size"/> times the original's.</summary>
	internal static int WrapWidthAt( float areaWidth, float size ) => WrapWidthOf( areaWidth / size );

	/// <summary>The lines the text was last laid out in, and the width in the font's pixels they were wrapped at (0 for none).</summary>
	public IReadOnlyList<string> Lines => _lines;

	public int WrapWidth => _measured.WrapWidth;

	/// <summary>Releases the texture. Drawing again lays the text out afresh.</summary>
	public void Delete()
	{
		_texture?.Delete();
		_texture = null;
	}

	/// <summary>Breaks the text into its lines and measures the block they make, in the font's pixels.</summary>
	private void Lay( BitmapFont font, Measure measure )
	{
		_measured = measure;
		_lines = Wrap( font, measure.Text, measure.WrapWidth );
		_textWidth = Math.Max( 1, _lines.Max( line => font.Measure( line ) ) );
		_textHeight = Math.Max( 1, _lines.Count * font.LineHeight );
	}

	private void Build( BitmapFont font, Layout layout )
	{
		_built = layout;
		_pixels = Letter( font, _lines, layout.Width, layout.Height, layout.Colour, layout.Shadow, layout.Measure.WrapWidth > 0, layout.Across,
			layout.Down );
		_width = layout.Width;
		_height = layout.Height;
	}

	/// <summary>
	/// The RGBA pixels of <paramref name="lines"/> lettered into a bitmap <paramref name="width"/> by <paramref name="height"/>,
	/// each line where the renderer puts it (<see cref="LineLeft"/>, <see cref="BlockTop"/>) and cut at the bitmap's edges. With
	/// <paramref name="shadow"/>, the grey copy first, its box moved two left and two down in the same bitmap, so its left two
	/// columns are cut where the text stands at the bitmap's left edge; then the text where it belongs (0x00491da1..0x00491e84).
	/// </summary>
	internal static byte[] Letter( BitmapFont font, IReadOnlyList<string> lines, int width, int height, UiColour colour, bool shadow,
		bool wraps, TextAlign across, TextAlign down )
	{
		var pixels = new byte[width * height * 4];
		var top = BlockTop( height, lines.Count * font.LineHeight, down );

		if ( shadow )
		{
			var grey = new UiColour( (byte)(colour.R / 2), (byte)(colour.G / 2), (byte)(colour.B / 2) );
			Write( font, lines, pixels, width, height, wraps, across, -ShadowOffset, top + ShadowOffset, grey, 0.5f );
		}

		Write( font, lines, pixels, width, height, wraps, across, 0, top, colour, 1f );

		return pixels;
	}

	private static void Write( BitmapFont font, IReadOnlyList<string> lines, byte[] pixels, int width, int height, bool wraps, TextAlign across,
		int moved, int top, UiColour colour, float opacity )
	{
		for ( int i = 0; i < lines.Count; ++i )
		{
			var left = LineLeft( width, font.Measure( lines[i] ), across, wraps ) + moved;

			if ( left < width )
				font.Write( lines[i], pixels, width, height, left, top + (i * font.LineHeight), colour, opacity );
		}
	}

	/// <summary>
	/// Puts the laid-out pixels in the texture, each copied to a <paramref name="prescale"/> by <paramref name="prescale"/> block:
	/// the first level is rewritten in place where the texture keeps its size. Nothing is done where the texture already
	/// holds them at that copy (<paramref name="rewrite"/> false).
	/// </summary>
	private void Upload( int prescale, bool rewrite )
	{
		if ( _texture != null && prescale == _prescale && !rewrite )
			return;

		var texels = Prescaled( _pixels, _width, _height, prescale );
		var (width, height) = (_width * prescale, _height * prescale);

		if ( _texture != null && _texture.Width == width && _texture.Height == height )
		{
			_texture.UpdatePixels( texels );
		}
		else
		{
			_texture?.Delete();
			_texture = new Texture( texels, width, height, TextureFlags.OwnPixels );
		}

		_prescale = prescale;
	}

	/// <summary>
	/// The text's lines: split where it says, and where a line is wider than <paramref name="wrapWidth"/> if that is not 0.
	/// The break is the renderer's word-wrapper's (0x006aec80): the pen's advances are added a character at a time, and
	/// the character that takes the sum past the width ends the line before the last space or tab at or after the line's
	/// second character, or before itself where there is none, a lone character keeping its line. Spaces and tabs before
	/// the break are left off the line and those after it off the next.
	///
	/// <para>
	/// <b>Where it differs from the original</b>, none of it reached by shipped text (dead by CONTENT: no row of either
	/// language folder ends in a line feed or holds a carriage return). The original lays each paragraph with its line
	/// feed (0x006aeb4b): blanks cut at a break that land before a line feed add one more, empty line (0x006aed53), a line
	/// feed that ends the text lays no further paragraph and an empty text lays no line (0x006aeb81, 0x006aeb32), a
	/// carriage return is a break of its own, so CR LF is two. Here the line feeds are split off first, a trailing empty
	/// paragraph and an empty text each lay one line, a lone carriage return is a character and CR LF is one break.
	/// </para>
	/// </summary>
	internal static List<string> Wrap( BitmapFont font, string text, int wrapWidth )
	{
		var lines = new List<string>();

		foreach ( var paragraph in text.Replace( "\r\n", "\n" ).Split( '\n' ) )
		{
			if ( wrapWidth <= 0 )
			{
				lines.Add( paragraph );
				continue;
			}

			var start = 0;
			var sum = 0;

			for ( int i = 0; i < paragraph.Length; ++i )
			{
				var advance = font.Measure( paragraph.AsSpan( i, 1 ) );

				// A line's first character stays on it however wide: the original gives it a line of its own, and the next
				// character, overflowing at once, then cuts before itself.
				if ( sum + advance <= wrapWidth || i == start )
				{
					sum += advance;
					continue;
				}

				var at = i;

				while ( at != start && !IsSpace( paragraph[at] ) )
					--at;

				var end = at == start ? i : at;
				var next = end;

				while ( at != start && end - 1 != start && IsSpace( paragraph[end - 1] ) )
					--end;

				while ( next < paragraph.Length && IsSpace( paragraph[next] ) )
					++next;

				lines.Add( paragraph[start..end] );
				start = next;
				sum = 0;
				i = next - 1;
			}

			if ( start < paragraph.Length || paragraph.Length == 0 )
				lines.Add( paragraph[start..] );
		}

		return lines;
	}

	private static bool IsSpace( char character ) => character is ' ' or '\t';
}

internal enum TextAlign
{
	Start,
	Centre,
	End
}
