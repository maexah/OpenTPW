using StbTrueTypeSharp;

namespace OpenTPW;

/// <summary>
/// Paints a park's name onto its sign board and hands back the two panel textures the model
/// wants.
///
/// The board is one 256x128 sheet - the artwork out of the park's <see cref="SignFile"/> - but
/// the gateway mesh draws it as two 128x128 panels sitting side by side, each taking a full 0..1
/// UV range, and each appearing twice so the sign reads correctly from either side. So the name
/// is laid out across the whole sheet and the sheet is then cut down the middle: left half to
/// the 'sign1' material, right half to 'sign2'.
///
/// The lettering itself is rasterised from the TrueType font the .sgn names, which ships in
/// fonts.wad - the same font the original game used for that park, and the reason each park's
/// sign looks different.
/// </summary>
public static class SignTexture
{
	/// <summary>Width of the whole board, across both panels.</summary>
	private const int BoardWidth = 256;

	/// <summary>
	/// Height of the board. The .sgn image decodes into a 256x256 buffer because that is the
	/// aligned size its wavelet decoder works at, but only the top half carries artwork.
	/// </summary>
	private const int BoardHeight = 128;

	private const int PanelSize = BoardHeight;

	// Keeps the lettering off the board's painted edges.
	private const float HorizontalMargin = 0.86f;
	private const float LineHeightFraction = 0.42f;

	/// <summary>
	/// Builds the two panel textures for a sign, or returns false and leaves the caller to fall
	/// back on the model's own placeholder textures.
	/// </summary>
	public static bool TryBuild( string signPath, string parkName, out Texture? left, out Texture? right )
	{
		left = null;
		right = null;

		var sign = new SignFile( signPath );

		if ( !sign.IsValid || sign.Image is not { } image )
			return false;

		var board = CropBoard( image );

		var (first, second) = SplitName( parkName );

		if ( string.IsNullOrEmpty( second ) )
		{
			// A one-word park - Space, Halloween - would look dropped if it stayed on the top line.
			DrawLine( board, first, sign, fontIndex: 0, centreY: BoardHeight * 0.5f );
		}
		else
		{
			DrawLine( board, first, sign, fontIndex: 0, centreY: BoardHeight * 0.30f );
			DrawLine( board, second, sign, fontIndex: 1, centreY: BoardHeight * 0.70f );
		}

		left = new Texture( CutPanel( board, 0 ), PanelSize, PanelSize );
		right = new Texture( CutPanel( board, PanelSize ), PanelSize, PanelSize );

		return true;
	}

	/// <summary>
	/// Splits a park name over the sign's two lines. "Lost Kingdom" reads as "Lost" above
	/// "Kingdom"; a single-word park keeps the whole name on the top line.
	/// </summary>
	private static (string First, string Second) SplitName( string parkName )
	{
		var space = parkName.IndexOf( ' ' );

		return space < 0
			? (parkName, string.Empty)
			: (parkName[..space], parkName[(space + 1)..]);
	}

	private static byte[] CropBoard( TextureData image )
	{
		var board = new byte[BoardWidth * BoardHeight * 4];

		for ( int y = 0; y < BoardHeight; ++y )
			Array.Copy( image.Data, y * image.Width * 4, board, y * BoardWidth * 4, BoardWidth * 4 );

		return board;
	}

	private static byte[] CutPanel( byte[] board, int x )
	{
		var panel = new byte[PanelSize * PanelSize * 4];

		for ( int y = 0; y < PanelSize; ++y )
			Array.Copy( board, ((y * BoardWidth) + x) * 4, panel, y * PanelSize * 4, PanelSize * 4 );

		return panel;
	}

	private static unsafe void DrawLine( byte[] board, string text, SignFile sign, int fontIndex, float centreY )
	{
		if ( string.IsNullOrWhiteSpace( text ) || sign.Fonts.Count == 0 )
			return;

		// Space's sign names the same font twice, and a park could name only one - fall back
		// rather than skipping the line.
		var font = sign.Fonts[Math.Min( fontIndex, sign.Fonts.Count - 1 )];

		var fontData = ReadFont( font.FileName );

		if ( fontData == null )
			return;

		var info = new StbTrueType.stbtt_fontinfo();

		fixed ( byte* fontPointer = fontData )
		{
			if ( StbTrueType.stbtt_InitFont( info, fontPointer, 0 ) == 0 )
			{
				Log.Warning( $"Could not read the sign font '{font.FileName}'" );
				return;
			}

			int ascent, descent, lineGap;
			StbTrueType.stbtt_GetFontVMetrics( info, &ascent, &descent, &lineGap );

			// Measured unscaled so a single scale can be solved for, rather than guessing a
			// point size and hoping the word fits the board.
			var advance = 0;
			for ( int i = 0; i < text.Length; ++i )
			{
				int characterAdvance, leftSideBearing;
				StbTrueType.stbtt_GetCodepointHMetrics( info, text[i], &characterAdvance, &leftSideBearing );
				advance += characterAdvance;

				if ( i < text.Length - 1 )
					advance += StbTrueType.stbtt_GetCodepointKernAdvance( info, text[i], text[i + 1] );
			}

			if ( advance <= 0 )
				return;

			var scale = MathF.Min(
				BoardWidth * HorizontalMargin / advance,
				BoardHeight * LineHeightFraction / (ascent - descent) );

			var penX = (BoardWidth - (advance * scale)) * 0.5f;
			var baseline = centreY + (((ascent + descent) * 0.5f) * scale);

			var colour = font.Colour;

			for ( int i = 0; i < text.Length; ++i )
			{
				int x0, y0, x1, y1;
				StbTrueType.stbtt_GetCodepointBitmapBox( info, text[i], scale, scale, &x0, &y0, &x1, &y1 );

				var width = x1 - x0;
				var height = y1 - y0;

				if ( width > 0 && height > 0 )
				{
					// Rasterised into our own buffer rather than stbtt's allocator, so there is
					// nothing to free.
					var glyph = new byte[width * height];

					fixed ( byte* glyphPointer = glyph )
					{
						StbTrueType.stbtt_MakeCodepointBitmap(
							info, glyphPointer, width, height, width, scale, scale, text[i] );

	Blend( board, glyphPointer, width, height, (int)(penX + x0), (int)(baseline + y0), colour );
					}
				}

				int characterAdvance, leftSideBearing;
				StbTrueType.stbtt_GetCodepointHMetrics( info, text[i], &characterAdvance, &leftSideBearing );
				penX += characterAdvance * scale;

				if ( i < text.Length - 1 )
					penX += StbTrueType.stbtt_GetCodepointKernAdvance( info, text[i], text[i + 1] ) * scale;
			}
		}
	}

	/// <summary>
	/// Composites one glyph's coverage over the board. The board is opaque - the sign panels have
	/// no alpha channel of their own - so the glyph tints the artwork rather than cutting it out.
	/// </summary>
	private static unsafe void Blend( byte[] board, byte* glyph, int width, int height, int x, int y,
		(float R, float G, float B) colour )
	{
		var r = colour.R * 255f;
		var g = colour.G * 255f;
		var b = colour.B * 255f;

		for ( int row = 0; row < height; ++row )
		{
			var targetY = y + row;

			if ( targetY < 0 || targetY >= BoardHeight )
				continue;

			for ( int column = 0; column < width; ++column )
			{
				var targetX = x + column;

				if ( targetX < 0 || targetX >= BoardWidth )
					continue;

				var coverage = glyph[(row * width) + column] / 255f;

				if ( coverage <= 0f )
					continue;

				var index = ((targetY * BoardWidth) + targetX) * 4;

				board[index] = (byte)((r * coverage) + (board[index] * (1f - coverage)));
				board[index + 1] = (byte)((g * coverage) + (board[index + 1] * (1f - coverage)));
				board[index + 2] = (byte)((b * coverage) + (board[index + 2] * (1f - coverage)));
			}
		}
	}

	private static byte[]? ReadFont( string fileName )
	{
		using var stream = FileSystem.OpenRead( $"fonts/{fileName}" );

		if ( stream == null )
		{
			Log.Warning( $"Sign font 'fonts/{fileName}' is missing" );
			return null;
		}

		using var memoryStream = new MemoryStream();
		stream.CopyTo( memoryStream );

		return memoryStream.ToArray();
	}
}
