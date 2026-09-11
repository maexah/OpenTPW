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
/// </para>
/// </summary>
internal sealed class UiText
{
	private const int ShadowOffset = 2;

	private Texture? _texture;
	private int _width;
	private int _height;
	private Layout _built;

	private readonly record struct Layout( string Text, int Font, int Set, UiColour Colour, bool Shadow, int WrapWidth, TextAlign Across );

	public void Draw( string? text, int font, UiColour colour, bool shadow, bool wrap, PixelRect area, TextAlign across, TextAlign down )
	{
		if ( string.IsNullOrEmpty( text ) || UiFonts.Get( font ) is not { } bitmap )
			return;

		var scale = UiFonts.Scale;
		var layout = new Layout( text, font, UiFonts.SetIndex, colour, shadow, wrap ? (int)(area.Width / scale) : 0, across );

		if ( _texture == null || layout != _built )
			Build( bitmap, layout );

		if ( _texture == null )
			return;

		// Aligned by the text itself; the shadow hangs off its left and bottom.
		var margin = shadow ? ShadowOffset : 0;
		var textWidth = (_width - margin) * scale;
		var textHeight = (_height - margin) * scale;

		var x = Align( area.X, area.Width, textWidth, across ) - (margin * scale);
		var y = Align( area.Y, area.Height, textHeight, down );

		var width = _width * scale;
		var height = _height * scale;

		Material.UI.Set( "Color", _texture );

		using ( _ = new Graphics.Scope( Screen.Size ) )
			Graphics.Quad( new Rectangle( MathF.Round( x ), MathF.Round( Screen.Height - y - height ), width, height ), Material.UI );
	}

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

		var lines = Lines( font, layout.Text, layout.WrapWidth );
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

		if ( _texture != null && _texture.Width == width && _texture.Height == height )
		{
			_texture.UpdatePixels( pixels );
		}
		else
		{
			_texture?.Delete();
			_texture = new Texture( pixels, width, height );
		}

		_width = width;
		_height = height;
	}

	/// <summary>The text's lines: split where it says, and where a line is wider than <paramref name="wrapWidth"/> if that is not 0.</summary>
	private static List<string> Lines( BitmapFont font, string text, int wrapWidth )
	{
		var lines = new List<string>();

		foreach ( var paragraph in text.Replace( "\r\n", "\n" ).Split( '\n' ) )
		{
			if ( wrapWidth <= 0 || font.Measure( paragraph ) <= wrapWidth )
			{
				lines.Add( paragraph );
				continue;
			}

			var line = "";

			foreach ( var word in paragraph.Split( ' ' ) )
			{
				var longer = line.Length == 0 ? word : $"{line} {word}";

				if ( line.Length > 0 && font.Measure( longer ) > wrapWidth )
				{
					lines.Add( line );
					line = word;
				}
				else
				{
					line = longer;
				}
			}

			lines.Add( line );
		}

		return lines;
	}
}

internal enum TextAlign
{
	Start,
	Centre,
	End
}
