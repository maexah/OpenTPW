using StbImageSharp;
using StbTrueTypeSharp;
using System.Diagnostics;

namespace OpenTPW;

/// <summary>
/// The "Welcome to Sim Theme Park" picture and its red bar, up while a level loads.
///
/// <para>
/// The original builds it in 0x00587380. The picture is Data\Init\&lt;width&gt;\welcome.tga, from
/// the folder matching the screen's width - 400, 512, 640, 800 or 1024, anything else taking
/// 640 - stretched over the whole screen. The first loading screen of a session is the odd one
/// out: it pastes legal_&lt;language&gt;.tga, the copyright strip, over the bottom of the picture,
/// draws no bar, and is held until three seconds after it appeared (0x005879c0). Before it, the
/// first call of all shows splash_&lt;language&gt;.tga, the Bullfrog logo, for at least two and a
/// half seconds. Every later screen pastes welcome_&lt;language&gt;.tga a little under halfway down
/// instead - the title, for a language that translates it; the American data has none, as its
/// title is part of the picture - and draws the bar. That later kind is the one here: it is what
/// the original shows while it loads the lobby.
/// </para>
///
/// <para>
/// The bar is a count, not a clock. Every call site passes 500 as the number of steps a load
/// takes, and the texture and mesh loaders take a step each while the screen is up, redrawing it
/// as they go (see <see cref="Asset.Register"/>). Past the expected number it stays full. Here a
/// step is any asset registering itself, and the caller says how many to expect, as none of
/// OpenTPW's loads match the original's.
/// </para>
///
/// <para>
/// Three things differ on purpose. The picture keeps its 4:3 shape, centred, rather than being
/// stretched across a wide window. It comes from the narrowest folder at least as wide as it is
/// drawn, where matching the width exactly would put the 640 picture on any window wider than
/// 1024. And the last line the log printed is written under the bar - the original says nothing
/// about what it is doing, so a load that hangs there gives no clue where.
/// </para>
///
/// <para>
/// Nothing but the load itself draws it. A load is one long call on the main thread, with no
/// frames running, so a step or a log line draws a frame on the way past - no more than
/// <see cref="FramesPerSecond"/> of them, as every frame presented is time taken from the load.
/// Disposing it takes it down for good: it stops listening to the log, stops counting steps,
/// and releases its textures.
/// </para>
/// </summary>
internal sealed class LoadingScreen : IDisposable
{
	/// <summary>The most frames a second it draws while loading.</summary>
	private const float FramesPerSecond = 30f;

	/// <summary>The widths the original has a picture for, narrowest first.</summary>
	private static readonly int[] PictureWidths = [400, 512, 640, 800, 1024];

	private const string FontPath = "content/fonts/Roboto-Medium.ttf";

	private static LoadingScreen? _current;

	private readonly string _what;
	private readonly int _expectedSteps;
	private int _steps;

	private readonly int _mainThread = Environment.CurrentManagedThreadId;
	private readonly Stopwatch _sinceFrame = new();
	private bool _drawing;

	// Where the picture goes, in pixels from the window's bottom-left corner. Worked out once, as
	// the window cannot be resized.
	private readonly int _left;
	private readonly int _bottom;
	private readonly int _width;
	private readonly int _height;

	private readonly Texture? _picture;
	private readonly Texture _darkRed;
	private readonly Texture _red;

	private readonly byte[] _font;
	private readonly Texture _status;
	private readonly byte[] _statusPixels;
	private readonly (int X, int Y, int Width, int Height) _statusBox;

	/// <summary>The last line logged. Written from whichever thread logged it.</summary>
	private volatile string _lastLine = "";
	private string? _lineShown;

	/// <param name="what">What is loading, for the log - "the lobby".</param>
	/// <param name="expectedSteps">How many steps fill the bar - see <see cref="Asset.Register"/>.</param>
	public LoadingScreen( string what, int expectedSteps )
	{
		_what = what;
		_expectedSteps = Math.Max( expectedSteps, 1 );

		// Its original 4:3, as large as the window takes it.
		_height = Math.Min( Screen.Size.Y, Screen.Size.X * 3 / 4 );
		_width = _height * 4 / 3;
		_left = (Screen.Size.X - _width) / 2;
		_bottom = (Screen.Size.Y - _height) / 2;

		// None of these count as steps: nothing is listening for them yet.
		_picture = LoadPicture( _width );
		_darkRed = new Texture( [0x80, 0x00, 0x00, 0xFF], 1, 1 );
		_red = new Texture( [0xFF, 0x00, 0x00, 0xFF], 1, 1 );

		// The strip between the bar's frame and the bottom of the picture - the frame is painted
		// into the picture, and ends 18 pixels short of the bottom at 640x480.
		var statusLeft = _width * 14 / 640;
		var statusTop = _height * 463 / 480;
		_statusBox = (statusLeft, statusTop, _width - (statusLeft * 2), _height - statusTop);

		_font = File.ReadAllBytes( FontPath );
		_statusPixels = new byte[_statusBox.Width * _statusBox.Height * 4];
		_status = new Texture( _statusPixels, _statusBox.Width, _statusBox.Height, TextureFlags.PointFilter );

		Logger.OnLog += OnLog;
		_current = this;

		Log.Info( $"Loading {what}" );
		Pump( force: true );
	}

	/// <summary>
	/// One more step of whatever is loading, redrawing the screen if it is due. Does nothing when
	/// no loading screen is up, which is almost always.
	/// </summary>
	public static void Step()
	{
		if ( _current is not { } screen )
			return;

		Interlocked.Increment( ref screen._steps );
		screen.Pump();
	}

	public void Dispose()
	{
		if ( _current != this )
			return;

		// One last frame, however recently the one before was drawn, so what stays on the screen
		// until the level's first frame replaces it is the finished count rather than one a few
		// steps behind.
		Pump( force: true );

		_current = null;
		Logger.OnLog -= OnLog;

		_picture?.Delete();
		_darkRed.Delete();
		_red.Delete();
		_status.Delete();

		// Says how far off the expected count has drifted, as the lobby loads more.
		Log.Info( $"Loaded {_what} in {_steps} steps - the loading bar expects {_expectedSteps}" );
	}

	/// <summary>
	/// The narrowest picture at least as wide as it will be drawn, or the widest there is.
	/// </summary>
	private static Texture? LoadPicture( int width )
	{
		string? chosen = null;

		foreach ( var candidate in PictureWidths )
		{
			var path = $"Init/{candidate}/Welcome.tga";

			if ( !FileSystem.FileExists( path ) )
				continue;

			chosen = path;

			if ( candidate >= width )
				break;
		}

		if ( chosen == null )
		{
			Log.Warning( "No loading screen picture in data/Init - loading on a black screen" );
			return null;
		}

		using var stream = FileSystem.OpenRead( chosen );
		var image = ImageResult.FromStream( stream, ColorComponents.RedGreenBlueAlpha );

		return new Texture( image.Data, image.Width, image.Height );
	}

	private void OnLog( Logger.Level severity, string text )
	{
		// Only the first line of a long message - the rest would have nowhere to go.
		var newline = text.IndexOf( '\n' );
		_lastLine = newline < 0 ? text : text[..newline];

		Pump();
	}

	private void Pump( bool force = false )
	{
		// A step can come from inside a frame of this screen - its material's first use registers
		// the material - and a log line from any thread, but only the main thread can draw.
		if ( _drawing || Environment.CurrentManagedThreadId != _mainThread )
			return;

		if ( !force && _sinceFrame.IsRunning && _sinceFrame.Elapsed.TotalSeconds < 1f / FramesPerSecond )
			return;

		_sinceFrame.Restart();
		_drawing = true;

		try
		{
			WriteStatus();
			Render.DrawLoadingFrame( Draw );
		}
		finally
		{
			_drawing = false;
		}
	}

	private void Draw()
	{
		using var scope = new Graphics.Scope( Screen.Size );
		var material = Material.UI;

		if ( _picture != null )
		{
			material.Set( "Color", _picture );
			Graphics.Quad( new Rectangle( _left, _bottom, _width, _height ), material );
		}

		DrawBar( material );

		material.Set( "Color", _status );
		Graphics.Quad( PictureRectangle( _statusBox.X, _statusBox.Y, _statusBox.Width, _statusBox.Height ), material );
	}

	/// <summary>
	/// The bar as 0x00587a70 draws it: 318 pixels long when full at 640x480, in three bands - a
	/// dark red fifth of its height above and below a bright red middle. The arithmetic is the
	/// original's, integer division and all, only on the size of the picture rather than of the
	/// screen, so it lands inside the frame painted on the picture at any size.
	///
	/// The one change is the percentage, worked out in integers. The original multiplies by a float
	/// 100 / steps and truncates, which is exact for its 500 but not in general - 2972 steps of
	/// 100 / 2972 come to 99.99999, and the bar would never quite fill.
	/// </summary>
	private void DrawBar( Material material )
	{
		var percent = _steps * 100 / _expectedSteps;
		var length = percent > 100 ? 318 : percent * 318 / 100;

		var left = _width * 14 / 640;
		var top = _height * 446 / 480;
		var height = _height * 11 / 480;
		var right = (((length + 1) * ((_width << 16) / 640)) >> 16) + left;

		DrawBand( material, _darkRed, left, right, top, top + (height / 5) );
		DrawBand( material, _red, left, right, top + (height / 5), top + (height * 4 / 5) );
		DrawBand( material, _darkRed, left, right, top + (height * 4 / 5), top + height );
	}

	private void DrawBand( Material material, Texture colour, int left, int right, int top, int bottom )
	{
		material.Set( "Color", colour );
		Graphics.Quad( PictureRectangle( left, top, right - left, bottom - top ), material );
	}

	/// <summary>
	/// A rectangle given the way the original gives them - pixels right and down from the
	/// picture's top-left corner - in the window's pixels up from its bottom-left.
	/// </summary>
	private Rectangle PictureRectangle( int x, int y, int width, int height )
		=> new( _left + x, _bottom + _height - y - height, width, height );

	/// <summary>
	/// Rasterises the last line logged into the status texture, in white with a drop shadow so it
	/// reads over the picture, cut short to fit.
	/// </summary>
	private unsafe void WriteStatus()
	{
		var line = _lastLine;

		if ( line == _lineShown )
			return;

		_lineShown = line;
		Array.Clear( _statusPixels );

		var info = new StbTrueType.stbtt_fontinfo();

		fixed ( byte* font = _font )
		{
			if ( StbTrueType.stbtt_InitFont( info, font, 0 ) == 0 )
				return;

			int ascent, descent, lineGap;
			StbTrueType.stbtt_GetFontVMetrics( info, &ascent, &descent, &lineGap );

			var scale = _statusBox.Height * 0.8f / (ascent - descent);
			var baseline = (int)((_statusBox.Height + ((ascent + descent) * scale)) * 0.5f);
			var shadow = Math.Max( 1, _height / 480 );

			var text = Fit( info, line, scale, _statusBox.Width - shadow );

			WriteLine( info, text, scale, shadow, baseline + shadow, 0x00 );
			WriteLine( info, text, scale, 0, baseline, 0xFF );
		}

		_status.UpdatePixels( _statusPixels );
	}

	/// <summary>The line as it is, or as much of it as fits with an ellipsis after.</summary>
	private static string Fit( StbTrueType.stbtt_fontinfo info, string line, float scale, int width )
	{
		if ( Measure( info, line, scale ) <= width )
			return line;

		const string Ellipsis = "…";
		var room = width - Measure( info, Ellipsis, scale );

		var length = line.Length;
		while ( length > 0 && Measure( info, line[..length], scale ) > room )
			length--;

		return line[..length].TrimEnd() + Ellipsis;
	}

	private static float Measure( StbTrueType.stbtt_fontinfo info, string text, float scale )
	{
		var width = 0f;

		for ( int i = 0; i < text.Length; ++i )
			width += Advance( info, text, i, scale );

		return width;
	}

	/// <summary>How far the pen moves past character i, kerning with the next one included.</summary>
	private static unsafe float Advance( StbTrueType.stbtt_fontinfo info, string text, int i, float scale )
	{
		int advance, leftSideBearing;
		StbTrueType.stbtt_GetCodepointHMetrics( info, text[i], &advance, &leftSideBearing );

		if ( i < text.Length - 1 )
			advance += StbTrueType.stbtt_GetCodepointKernAdvance( info, text[i], text[i + 1] );

		return advance * scale;
	}

	private unsafe void WriteLine( StbTrueType.stbtt_fontinfo info, string text, float scale, int x, int baseline, byte shade )
	{
		var penX = (float)x;

		for ( int i = 0; i < text.Length; ++i )
		{
			int x0, y0, x1, y1;
			StbTrueType.stbtt_GetCodepointBitmapBox( info, text[i], scale, scale, &x0, &y0, &x1, &y1 );

			var width = x1 - x0;
			var height = y1 - y0;

			if ( width > 0 && height > 0 )
			{
				var glyph = new byte[width * height];

				fixed ( byte* glyphPointer = glyph )
				{
					StbTrueType.stbtt_MakeCodepointBitmap( info, glyphPointer, width, height, width, scale, scale, text[i] );
					Composite( glyphPointer, width, height, (int)penX + x0, baseline + y0, shade );
				}
			}

			penX += Advance( info, text, i, scale );
		}
	}

	/// <summary>Lays one glyph's coverage over what is already in the status texture.</summary>
	private unsafe void Composite( byte* glyph, int width, int height, int x, int y, byte shade )
	{
		for ( int row = 0; row < height; ++row )
		{
			var targetY = y + row;

			if ( targetY < 0 || targetY >= _statusBox.Height )
				continue;

			for ( int column = 0; column < width; ++column )
			{
				var targetX = x + column;

				if ( targetX < 0 || targetX >= _statusBox.Width )
					continue;

				var alpha = glyph[(row * width) + column] / 255f;

				if ( alpha <= 0f )
					continue;

				var index = ((targetY * _statusBox.Width) + targetX) * 4;
				var below = _statusPixels[index + 3] / 255f;
				var combined = alpha + (below * (1f - alpha));

				for ( int channel = 0; channel < 3; ++channel )
				{
					var colour = (shade * alpha) + (_statusPixels[index + channel] * below * (1f - alpha));
					_statusPixels[index + channel] = (byte)(colour / combined);
				}

				_statusPixels[index + 3] = (byte)(combined * 255f);
			}
		}
	}
}
