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
	/// The same left-, centre- and right-aligned.
	/// </summary>
	/// <remarks><b>Mutations:</b> the text moved right by the shadow's two (the old rule); the copy moved down only; the copy
	/// moved right; the copy's box moved by its left and top edges only (a centred copy moves one).</remarks>
	[TestMethod]
	public void TheGreyCopyIsMovedInsideTheSameBitmap()
	{
		var font = Font( 0 );
		var lines = new[] { "Load" };
		var (width, height) = (font.Measure( lines[0] ), font.LineHeight);
		var colour = new UiColour( 0, 255, 255 );

		foreach ( var across in new[] { TextAlign.Start, TextAlign.Centre, TextAlign.End } )
			CopyIsMovedInside( font, lines, width, height, colour, across );
	}

	private static void CopyIsMovedInside( BitmapFont font, string[] lines, int width, int height, UiColour colour, TextAlign across )
	{
		var plain = UiText.Letter( font, lines, width, height, colour, false, false, across, TextAlign.Start );
		var shadowed = UiText.Letter( font, lines, width, height, colour, true, false, across, TextAlign.Start );
		var greys = 0;

		Assert.AreEqual( plain.Length, shadowed.Length, "the same bitmap" );

		for ( int y = 0; y < height; ++y )
		{
			for ( int x = 0; x < width; ++x )
			{
				var at = ((y * width) + x) * 4;

				if ( plain[at + 3] == 255 )
				{
					Assert.AreEqual( (0, 255, 255, 255), (shadowed[at], shadowed[at + 1], shadowed[at + 2], shadowed[at + 3]), $"{across}: the text at ({x}, {y})" );
					continue;
				}

				// A pixel of the grey alone: the text has no ink here, so it is the copy's, from the text's (x + 2, y - 2).
				if ( plain[at + 3] == 0 && shadowed[at + 3] > 0 )
				{
					++greys;
					Assert.IsTrue( x + 2 < width && y >= 2 && Alpha( plain, width, x + 2, y - 2 ) > 0, $"{across}: the grey at ({x}, {y}) is the text's two right and two up" );
				}
			}
		}

		Assert.IsTrue( greys > 0, $"{across}: the copy is drawn" );
	}
	/// <summary>
	/// <b>A centred block is placed by its widest line, then each line in it</b> (0x006aec1b, 0x006ae675): two lines of font 0
	/// whose widths differ by an odd number, centred in a wrapping bitmap whose width less the wider line's is odd, each stand
	/// where the block step puts them, the narrower one column left of where centring it alone would; each row of the block
	/// is that line lettered from the left at its place.
	/// </summary>
	/// <remarks><b>Mutation:</b> each line centred about the box in one step (no block width).</remarks>
	[TestMethod]
	public void ACentredBlockOfTwoLinesStandsByItsWidestLine()
	{
		var font = Font( 0 );
		var words = new[] { "Load", "Save", "Quit", "Wonder Land", "Park", "Options", "Lost Kingdom" };
		var pair = (from a in words from b in words
			let wa = font.Measure( a ) let wb = font.Measure( b )
			where wa > wb && (wa - wb) % 2 == 1
			select new[] { a, b }).FirstOrDefault();

		Assert.IsNotNull( pair, "two words of font 0 whose widths differ by an odd number" );

		var (wide, narrow) = (font.Measure( pair[0] ), font.Measure( pair[1] ));
		var width = wide + 11;
		var height = font.LineHeight;
		var block = UiText.Letter( font, pair, width, height * 2, UiColour.White, false, true, TextAlign.Centre, TextAlign.Start );
		var places = new[] { 11 / 2, (11 / 2) + ((wide - narrow) / 2) };

		Assert.AreEqual( (width - narrow) / 2, places[1] + 1, "centred alone, the narrower line would stand a column right" );

		for ( int i = 0; i < 2; ++i )
		{
			var alone = UiText.Letter( font, new[] { pair[i] }, width, height, UiColour.White, false, true, TextAlign.Start, TextAlign.Start, places[i] );
			CollectionAssert.AreEqual( alone, block[(i * width * height * 4)..((i + 1) * width * height * 4)], $"line {i} \"{pair[i]}\" at {places[i]}" );
		}
	}

	/// <summary>
	/// <b>The box is the wrapping label's where its text was broken at a width</b> (<c>UiText.Letter</c> as a layout asks): a
	/// line of odd width w centred in a bitmap w + 10 wide stands at 5 when the layout wraps and at 4 about the unwrapped
	/// <c>0x4000</c> box when it does not, each the same as <c>Letter</c> told so.
	/// </summary>
	/// <remarks><b>Mutations:</b> the layout's lettering always wrapped; never wrapped.</remarks>
	[TestMethod]
	public void TheLayoutLettersInTheWrappingBoxOnlyWhereItWraps()
	{
		var font = Font( 0 );
		var line = new[] { "Load", "Save", "Quit", "Park", "Options" }.FirstOrDefault( word => font.Measure( word ) % 2 == 1 );

		Assert.IsNotNull( line, "a word of font 0 of odd width" );

		var lines = new[] { line };
		var width = font.Measure( line ) + 10;
		var height = font.LineHeight;
		var wrapped = UiText.Letter( font, lines, width, height, UiColour.White, false, true, TextAlign.Centre, TextAlign.Start );
		var unwrapped = UiText.Letter( font, lines, width, height, UiColour.White, false, false, TextAlign.Centre, TextAlign.Start );

		CollectionAssert.AreNotEqual( wrapped, unwrapped, "the two boxes place it a column apart" );
		CollectionAssert.AreEqual( UiText.Letter( font, lines, width, height, UiColour.White, false, true, TextAlign.Start, TextAlign.Start, 5 ), wrapped );

		UiText.Layout Layout( int wrapWidth ) => new( new UiText.Measure( line, 0, UiFonts.SetIndex, wrapWidth ), UiColour.White, false, TextAlign.Centre,
			TextAlign.Start, width, height );

		CollectionAssert.AreEqual( wrapped, UiText.Letter( font, lines, Layout( width ) ), "broken at a width" );
		CollectionAssert.AreEqual( unwrapped, UiText.Letter( font, lines, Layout( 0 ) ), "not broken" );
	}

	/// <summary>
	/// <b>The offset moves the text inside the bitmap, which stays the box</b> (<c>0x0048f8c5</c>..<c>0x0048fa0e</c>): "Load" in
	/// font 0 in a bitmap ten larger each way, with an offset (3, 2), is the same lettering without it moved 3 right and 2 down
	/// left- and top-aligned, 3 left and 2 up right- and bottom-aligned, and 3 left and not up right- and top-aligned (its top
	/// stays 0); the bitmap's size is the same.
	/// </summary>
	/// <remarks><b>Mutations:</b> the offset dropped from the layout; a right label moved right and down; its top moved.</remarks>
	[TestMethod]
	public void TheOffsetMovesTheTextInsideItsBitmap()
	{
		var font = Font( 0 );
		var lines = new[] { "Load" };
		var (width, height) = (font.Measure( lines[0] ) + 10, font.LineHeight + 10);

		foreach ( var (across, down, x, y) in new[] { (TextAlign.Start, TextAlign.Start, 3, 2), (TextAlign.End, TextAlign.End, -3, -2), (TextAlign.End, TextAlign.Start, -3, 0) } )
		{
			var plain = UiText.Letter( font, lines, width, height, UiColour.White, false, false, across, down );
			var moved = UiText.Letter( font, lines, width, height, UiColour.White, false, false, across, down, 3, 2 );

			var layout = new UiText.Layout( new UiText.Measure( lines[0], 0, UiFonts.SetIndex, 0 ), UiColour.White, false, across, down, width, height, 3, 2 );

			CollectionAssert.AreEqual( moved, UiText.Letter( font, lines, layout ), $"{across} {down}: the layout's offset" );
			Assert.AreEqual( plain.Length, moved.Length, $"{across} {down}: the same bitmap" );
			Assert.IsTrue( plain.Any( b => b > 0 ), $"{across} {down}: lettered" );

			for ( int row = 0; row < height; ++row )
			{
				for ( int column = 0; column < width; ++column )
				{
					var (fromX, fromY) = (column - x, row - y);
					var expected = fromX >= 0 && fromX < width && fromY >= 0 && fromY < height ? Alpha( plain, width, fromX, fromY ) : (byte)0;
					Assert.AreEqual( expected, Alpha( moved, width, column, row ), $"{across} {down}: ({column}, {row})" );
				}
			}
		}
	}
}
