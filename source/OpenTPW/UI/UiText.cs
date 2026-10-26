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
/// The original's first pass sets the glyph colour to (0x80, 0x80, 0x80, 0x80); this halves the text's own
/// colour instead, and whether the original's grey replaces the colour or combines with it is not read.
/// docs/exe/ui.md, "The two-pass text class".
/// </para>
/// </summary>
internal sealed class UiText
{
	private const int ShadowOffset = 2;

	private Texture? _texture;
	private int _width;
	private int _height;
	private Layout _built;
	private List<string> _lines = [];

	private readonly record struct Layout( string Text, int Font, int Set, UiColour Colour, bool Shadow, int WrapWidth, TextAlign Across, int Prescale );

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

		var layout = new Layout( text, font, UiFonts.SetIndex, colour, shadow, wrap ? WrapWidthAt( area.Width, size ) : 0, across,
			PrescaleFor( UiFonts.Scale * size ) );
		var built = _texture == null || layout != _built;

		if ( built )
			Build( bitmap, layout );

		if ( _texture == null )
			return;

		var margin = shadow ? ShadowOffset : 0;
		var (placed, drawn) = Place( area, _width, _height, margin, UiFonts.Scale, size, across, down );

		if ( built && Census )
		{
			var (textWidth, textHeight) = (placed.Width - (margin * UiFonts.Scale * drawn), placed.Height - (margin * UiFonts.Scale * drawn));
			Log.Info( $"UI text: \"{text}\" font {font}, {textWidth:0} x {textHeight:0} in a box {area.Width:0} x {area.Height:0}, x {drawn:0.000}"
				+ (textWidth > area.Width + 0.5f || textHeight > area.Height + 0.5f ? " OVER" : "") );
		}

		Material.UI.Set( "Color", _texture );

		using ( _ = new Graphics.Scope( Screen.Size ) )
			Graphics.Quad( new Rectangle( MathF.Round( placed.X ), MathF.Round( Screen.Height - placed.Y - placed.Height ), placed.Width, placed.Height ), Material.UI );
	}

	/// <summary>
	/// Where a laid-out text of <paramref name="width"/> by <paramref name="height"/> of the font's pixels (its shadow's
	/// <paramref name="margin"/> among them) is drawn in <paramref name="area"/>, and at what size (<see cref="SizeIn"/>):
	/// <paramref name="fontScale"/> screen pixels a font pixel times that size, its top-left from the top of the screen,
	/// aligned by the text itself, the shadow hanging off its left and bottom.
	/// </summary>
	internal static (Rectangle Placed, float Size) Place( PixelRect area, int width, int height, int margin, float fontScale, float wanted,
		TextAlign across, TextAlign down )
	{
		var size = SizeIn( wanted, (width - margin) * fontScale, (height - margin) * fontScale, area.Width, area.Height );
		var scale = fontScale * size;
		var x = Align( area.X, area.Width, (width - margin) * scale, across ) - (margin * scale);
		var y = Align( area.Y, area.Height, (height - margin) * scale, down );

		return (new Rectangle( x, y, width * scale, height * scale ), size);
	}

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

	/// <summary>Whether each text laid out is logged with its drawn size and its box (<c>OPENTPW_TEXT_CENSUS=1</c>).</summary>
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
	/// blended. Never more than the scale, so a text is never drawn smaller than its texture and the sampler reads only
	/// the first level, the one <see cref="Texture.UpdatePixels"/> rewrites. A deviation of OpenTPW's own: the original
	/// never scales a font (Q644; <c>ui.md</c>, "<c>UI_SetTitle</c>").
	/// </summary>
	internal static int PrescaleFor( float scale ) => Math.Clamp( (int)MathF.Floor( scale + 0.001f ), 1, 4 );

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

	public int WrapWidth => _built.WrapWidth;

	/// <summary>Releases the texture. Drawing again lays the text out afresh.</summary>
	public void Delete()
	{
		_texture?.Delete();
		_texture = null;
	}

	private static float Align( float start, float space, float size, TextAlign align ) => align switch
	{
		TextAlign.Start => start,
		TextAlign.End => start + space - size,
		_ => start + ((space - size) * 0.5f)
	};

	private void Build( BitmapFont font, Layout layout )
	{
		_built = layout;

		var lines = _lines = Wrap( font, layout.Text, layout.WrapWidth );
		var margin = layout.Shadow ? ShadowOffset : 0;

		var textWidth = Math.Max( 1, lines.Max( line => font.Measure( line ) ) );
		var textHeight = Math.Max( 1, lines.Count * font.LineHeight );

		var width = textWidth + margin;
		var height = textHeight + margin;
		var pixels = new byte[width * height * 4];

		var shadow = new UiColour( (byte)(layout.Colour.R / 2), (byte)(layout.Colour.G / 2), (byte)(layout.Colour.B / 2) );

		for ( int i = 0; i < lines.Count; ++i )
		{
			var x = (int)Align( 0, textWidth, font.Measure( lines[i] ), layout.Across );
			var y = i * font.LineHeight;

			if ( layout.Shadow )
				font.Write( lines[i], pixels, width, height, x, y + margin, shadow, 0.5f );

			font.Write( lines[i], pixels, width, height, x + margin, y, layout.Colour, 1f );
		}

		var n = layout.Prescale;
		var texels = Prescaled( pixels, width, height, n );

		if ( _texture != null && _texture.Width == width * n && _texture.Height == height * n )
		{
			_texture.UpdatePixels( texels );
		}
		else
		{
			_texture?.Delete();
			_texture = new Texture( texels, width * n, height * n, TextureFlags.OwnPixels );
		}

		_width = width;
		_height = height;
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
