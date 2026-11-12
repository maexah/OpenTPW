using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using OpenTPW.UI;

namespace OpenTPW.Tests;

/// <summary>
/// A text control's bitmap is its box, and what is lettered past it is cut (Q771; docs/exe/ui.md, "The two-pass text
/// class"): the shipped fonts lettered by <see cref="UiText.Letter"/>.
/// </summary>
[TestClass]
public class TextBitmapCutTests
{
	private BaseFileSystem? oldData;
	private Point2 oldScreen;
	private bool isolated;

	[TestInitialize]
	public void Isolate()
	{
		var data = GameData.Required();
		Log ??= new();
		oldData = FileSystemOrNull;
		FileSystem = data;
		oldScreen = Screen.Size;
		Screen.Size = new Point2( 1024, 768 );
		isolated = true;
	}

	[TestCleanup]
	public void PutBack()
	{
		if ( !isolated )
			return;

		FileSystemOrNull = oldData;
		Screen.Size = oldScreen;
	}

	private static BitmapFont Font( int slot ) => UiFonts.Get( slot ) ?? throw new AssertFailedException( $"font {slot} did not load" );

	private static byte Alpha( byte[] pixels, int width, int x, int y ) => pixels[(((y * width) + x) * 4) + 3];

	/// <summary>
	/// <b>A line wider than its bitmap loses its end, or its start from the right</b>: "Wonder Land" in font 0 lettered into
	/// a bitmap ten columns narrower than it is the first or the last columns of the same line in a bitmap its own width,
	/// pixel for pixel, and its ink reaches the narrow bitmap's cut edge.
	/// </summary>
	/// <remarks><b>Mutations:</b> the bitmap sized to the text (the old rule, the comparison fails on the width); the line
	/// placed from the right of the text, not of the bitmap.</remarks>
	[TestMethod]
	public void ALineWiderThanItsBitmapLosesItsEnd()
	{
		var font = Font( 0 );
		var lines = new[] { "Wonder Land" };
		var whole = font.Measure( lines[0] );
		var height = font.LineHeight;
		var cut = whole - 10;

		foreach ( var (across, from) in new[] { (TextAlign.Start, 0), (TextAlign.End, 10) } )
		{
			var full = UiText.Letter( font, lines, whole, height, UiColour.White, false, false, across, TextAlign.Start );
			var part = UiText.Letter( font, lines, cut, height, UiColour.White, false, false, across, TextAlign.Start );

			Assert.AreEqual( cut * height * 4, part.Length, $"{across}: the bitmap is the box" );

			for ( int y = 0; y < height; ++y )
			{
				for ( int x = 0; x < cut; ++x )
				{
					for ( int c = 0; c < 4; ++c )
						Assert.AreEqual( full[(((y * whole) + x + from) * 4) + c], part[(((y * cut) + x) * 4) + c], $"{across}: ({x}, {y}) channel {c}" );
				}
			}
		}

		var ends = UiText.Letter( font, lines, cut, height, UiColour.White, false, false, TextAlign.Start, TextAlign.Start );
		Assert.IsTrue( Enumerable.Range( 0, height ).Any( y => Alpha( ends, cut, cut - 1, y ) > 0 ), "ink at the cut edge: the 'd' runs on past it" );
	}

	/// <summary>
	/// <b>A block taller than its bitmap loses its lower lines</b>: three lines of font 0 lettered from the top into a bitmap
	/// a line and a half high are the first line and a half of the same block in a bitmap three lines high.
	/// </summary>
	/// <remarks><b>Mutation:</b> the block put in the middle whatever its height.</remarks>
	[TestMethod]
	public void ABlockTallerThanItsBitmapLosesItsLowerLines()
	{
		var font = Font( 0 );
		var lines = new[] { "Load", "Save", "Quit" };
		var width = lines.Max( line => font.Measure( line ) );
		var tall = font.LineHeight * 3;
		var held = font.LineHeight * 3 / 2;

		var full = UiText.Letter( font, lines, width, tall, UiColour.White, false, false, TextAlign.Start, TextAlign.Start );
		var part = UiText.Letter( font, lines, width, held, UiColour.White, false, false, TextAlign.Start, TextAlign.Start );

		CollectionAssert.AreEqual( full[..(width * held * 4)], part );
	}

	/// <summary>
	/// <b>The grey copy is moved two left and two down inside the same bitmap, and the text is not moved</b>: "Load" in font 0
	/// with its shadow in a bitmap exactly its own size keeps every pixel the text fills fully as the text alone has it, and
	/// every pixel of the copy is two left and two down of one of the text's own; nothing is added for the shadow's margin.
	/// </summary>
	/// <remarks><b>Mutations:</b> the text moved right by the shadow's two (the old rule); the copy moved down only; the copy
	/// moved right.</remarks>
	[TestMethod]
	public void TheGreyCopyIsMovedInsideTheSameBitmap()
	{
		var font = Font( 0 );
		var lines = new[] { "Load" };
		var (width, height) = (font.Measure( lines[0] ), font.LineHeight);
		var colour = new UiColour( 0, 255, 255 );

		var plain = UiText.Letter( font, lines, width, height, colour, false, false, TextAlign.Start, TextAlign.Start );
		var shadowed = UiText.Letter( font, lines, width, height, colour, true, false, TextAlign.Start, TextAlign.Start );
		var greys = 0;

		Assert.AreEqual( plain.Length, shadowed.Length, "the same bitmap" );

		for ( int y = 0; y < height; ++y )
		{
			for ( int x = 0; x < width; ++x )
			{
				var at = ((y * width) + x) * 4;

				if ( plain[at + 3] == 255 )
				{
					Assert.AreEqual( (0, 255, 255, 255), (shadowed[at], shadowed[at + 1], shadowed[at + 2], shadowed[at + 3]), $"the text at ({x}, {y})" );
					continue;
				}

				// A pixel of the grey alone: the text has no ink here, so it is the copy's, from the text's (x + 2, y - 2).
				if ( plain[at + 3] == 0 && shadowed[at + 3] > 0 )
				{
					++greys;
					Assert.IsTrue( x + 2 < width && y >= 2 && Alpha( plain, width, x + 2, y - 2 ) > 0, $"the grey at ({x}, {y}) is the text's two right and two up" );
				}
			}
		}

		Assert.IsTrue( greys > 0, "the copy is drawn" );
	}
}
