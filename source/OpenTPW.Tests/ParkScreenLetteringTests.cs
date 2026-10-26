using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using OpenTPW.UI;

namespace OpenTPW.Tests;

/// <summary>
/// The buy and hire screens letter larger than the original's where each box holds it (Q644, a deviation at Alexah's
/// word): the size rule and its inheritance. The screens themselves are <c>ParkListScreenRewriteTests</c>'.
/// </summary>
[TestClass]
public class ParkScreenLetteringTests
{
	/// <summary>
	/// <b>A text is drawn at the size wanted where its box holds it, as large as the box holds where not, never under the
	/// original's.</b>
	/// </summary>
	/// <remarks><b>Mutations:</b> the width not held; the height not held; no floor at 1; the wanted size not a ceiling.</remarks>
	[TestMethod]
	public void ATextIsDrawnAsLargeAsWantedOrAsItsBoxHolds()
	{
		Assert.AreEqual( 1.2f, UiText.SizeIn( 1.2f, 100, 25, 360, 30 ), 1e-5f, "the box holds it" );
		Assert.AreEqual( 1.1f, UiText.SizeIn( 1.2f, 100, 25, 110, 40 ), 1e-5f, "the width holds 1.1" );
		Assert.AreEqual( 1.1f, UiText.SizeIn( 1.2f, 100, 25, 360, 27.5f ), 1e-5f, "the height holds 1.1" );
		Assert.AreEqual( 1f, UiText.SizeIn( 1.2f, 100, 25, 90, 30 ), 1e-5f, "never under the original's, even past its box" );
		Assert.AreEqual( 1f, UiText.SizeIn( 1f, 100, 25, 360, 300 ), 1e-5f, "the original's size wanted" );
	}

	/// <summary><b>A control letters at its own size, else its nearest parent's, else the original's.</b></summary>
	/// <remarks><b>Mutations:</b> the parent's size not inherited; a child's own size not kept; a lone control not at 1.</remarks>
	[TestMethod]
	public void AControlLettersAtItsOwnSizeOrItsNearestParents()
	{
		var root = new UiControl { Rect = new UiRect( 0, 0, 1000, 1000 ), TextSize = 1.3f };
		var list = root.Add( new UiControl { Rect = new UiRect( 10, 10, 500, 500 ) } );
		var row = list.Add( new UiControl { Rect = new UiRect( 20, 20, 100, 40 ) } );
		var plain = root.Add( new UiControl { Rect = new UiRect( 600, 10, 900, 500 ), TextSize = 1f } );
		var inPlain = plain.Add( new UiControl { Rect = new UiRect( 610, 20, 700, 40 ) } );

		Assert.AreEqual( 1.3f, row.LetteringSize, "a row two below the root" );
		Assert.AreEqual( 1f, inPlain.LetteringSize, "under a control of the original's size" );
		Assert.AreEqual( 1f, new UiControl().LetteringSize, "a control with no parent" );
	}

	/// <summary>
	/// <b>The text is drawn at its size and aligned by itself</b>: a 100 x 25 text wanted 1.2 times in a 360 x 30 box is
	/// 120 x 30, centred at 220; one with a shadow of 2 at 2 screen pixels a font pixel, to the end of a 1000-wide box, is
	/// 244.8 x 64.8 with its text's right edge on the box's.
	/// </summary>
	/// <remarks><b>Mutations:</b> the size not used in the draw; the size used for the box but not the shadow.</remarks>
	[TestMethod]
	public void TheTextIsDrawnAtItsSizeAlignedByItself()
	{
		var (placed, size) = UiText.Place( new PixelRect( 100, 200, 360, 30 ), 100, 25, 0, 1f, 1.2f, TextAlign.Centre, TextAlign.Centre );
		Assert.AreEqual( 1.2f, size, 1e-5f );
		Assert.AreEqual( (220, 200, 120, 30), ((int)MathF.Round( placed.X ), (int)MathF.Round( placed.Y ), (int)MathF.Round( placed.Width ),
			(int)MathF.Round( placed.Height )), "the box holds 1.2" );

		(placed, size) = UiText.Place( new PixelRect( 0, 0, 1000, 1000 ), 102, 27, 2, 2f, 1.2f, TextAlign.End, TextAlign.Start );
		Assert.AreEqual( 1.2f, size, 1e-5f );
		Assert.AreEqual( 755.2f, placed.X, 1e-3f, "the text's right edge on the box's, the shadow off its left" );
		Assert.AreEqual( 244.8f, placed.Width, 1e-3f, "the whole texture at 2.4 a font pixel" );
		Assert.AreEqual( 64.8f, placed.Height, 1e-3f );
	}

	/// <summary>
	/// <b>A wrapping text breaks at its width at the size wanted</b>: a 372-wide box at 2 screen pixels a font pixel breaks
	/// at 186 font pixels at the original's size and 155 at 1.2 times.
	/// </summary>
	/// <remarks><b>Mutation:</b> the break at the original's width whatever the size.</remarks>
	[TestMethod]
	public void AWrappingTextBreaksAtItsWidthAtTheSizeWanted()
	{
		var screen = Screen.Size;

		try
		{
			Screen.Size = new Point2( 2048, 1536 );
			Assert.AreEqual( 2f, UiFonts.Scale, "the layout's own size draws set 3 twice over" );
			Assert.AreEqual( 186, UiText.WrapWidthAt( 372, 1f ) );
			Assert.AreEqual( 155, UiText.WrapWidthAt( 372, 1.2f ) );
		}
		finally
		{
			Screen.Size = screen;
		}
	}

	/// <summary>
	/// <b>A text's copy is chosen from the scale it is drawn at, once its box has held it, not from the size wanted</b>: at
	/// 2552 x 1355 a font pixel is 1.764 screen pixels, a 100 x 15 text wanted 1.2 times in a box 40 high is drawn at 1.2
	/// (2.117, copied twice) and in a box 28 high at 1.058 (1.867, copied once, not drawn smaller than its texture).
	/// </summary>
	/// <remarks><b>Mutations:</b> <c>Plan</c> choosing from <c>fontScale</c> alone; from the size wanted; from the wanted
	/// scale (<c>fontScale * wanted</c>).</remarks>
	[TestMethod]
	public void ATextsCopyIsChosenFromTheSizeItIsDrawnAt()
	{
		var screen = Screen.Size;

		try
		{
			Screen.Size = new Point2( 2552, 1355 );
			Assert.AreEqual( 1.764f, UiFonts.Scale, 1e-3f );

			var (placed, drawn, copy) = UiText.Plan( new PixelRect( 0, 0, 360, 40 ), 100, 15, 0, UiFonts.Scale, 1.2f, TextAlign.Start, TextAlign.Start );
			Assert.AreEqual( (1.2f, 2), (drawn, copy), "the box holds 1.2" );
			Assert.AreEqual( 100 * UiFonts.Scale * 1.2f, placed.Width, 1e-2f );

			(placed, drawn, copy) = UiText.Plan( new PixelRect( 0, 0, 360, 28 ), 100, 15, 0, UiFonts.Scale, 1.2f, TextAlign.Start, TextAlign.Start );
			Assert.AreEqual( 28f / (15 * UiFonts.Scale), drawn, 1e-4f, "the box holds 1.058" );
			Assert.AreEqual( 1, copy, "drawn at 1.867 screen pixels a font pixel, so copied once" );
			Assert.IsTrue( placed.Width / 100 < 2f, "the picture is drawn smaller than two texels a pixel would be" );
		}
		finally
		{
			Screen.Size = screen;
		}
	}

	/// <summary>
	/// <b>A text's pixels are copied to the whole number of texels at or under its drawn scale, 1 to 4</b>, before the
	/// sampler enlarges it: under 2 stays, x 2.12 takes 2, x 3.5 takes 3, anything past 4 takes 4.
	/// </summary>
	/// <remarks><b>Mutations:</b> rounded up; rounded to nearest; no ceiling of 4; a 2 by 2 picture's rows copied wrong.</remarks>
	[TestMethod]
	public void ATextsPixelsAreCopiedToTheWholeScaleUnderItFirst()
	{
		Assert.AreEqual( (1, 1, 1, 1, 2, 3, 4), (UiText.PrescaleFor( 1f ), UiText.PrescaleFor( 0.6f ), UiText.PrescaleFor( 1.2f ),
			UiText.PrescaleFor( 1.764f ), UiText.PrescaleFor( 2.117f ), UiText.PrescaleFor( 3.5f ), UiText.PrescaleFor( 9f )) );
		Assert.AreEqual( 2, UiText.PrescaleFor( 1.9999f ), "a scale of 2 a float's width short is 2" );

		byte[] two = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16];
		var copied = UiText.Prescaled( two, 2, 2, 2 );
		Assert.AreEqual( 64, copied.Length );
		byte[] top = [1, 2, 3, 4, 1, 2, 3, 4, 5, 6, 7, 8, 5, 6, 7, 8];
		byte[] bottom = [9, 10, 11, 12, 9, 10, 11, 12, 13, 14, 15, 16, 13, 14, 15, 16];
		CollectionAssert.AreEqual( top, copied[..16], "the first row" );
		CollectionAssert.AreEqual( top, copied[16..32], "the first row again" );
		CollectionAssert.AreEqual( bottom, copied[32..48], "the second row" );
		CollectionAssert.AreEqual( bottom, copied[48..], "the second row again" );
		Assert.AreSame( two, UiText.Prescaled( two, 2, 2, 1 ), "x 1 is the picture itself" );
	}
}
