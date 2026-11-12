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
	/// <b>A text's bitmap is its box at the size it is drawn, cut toward nought, and stands at the box's corner</b> (Q771): a
	/// 100 x 25 text wanted 1.2 times in a 360 x 30 box at 1 screen pixel a font pixel is drawn at 1.2 into a bitmap of
	/// 300 x 25; in a 1000-square box at 2 it is 416 square (1000 / 2.4 = 416.67), 998.4 on the screen; at the original's
	/// size a 351.9 x 20.7 box is 351 x 20, not the 352 x 21 a rounding would make, nor the text's own 100 x 25.
	/// </summary>
	/// <remarks><b>Mutations:</b> the bitmap the text's size (the old rule); rounded, not cut; the size not used for the
	/// bitmap; the bitmap placed by the text's alignment, not at the box's corner.</remarks>
	[TestMethod]
	public void ATextsBitmapIsItsBoxAtTheSizeDrawn()
	{
		var (placed, size, _, width, height) = UiText.Plan( new PixelRect( 100, 200, 360, 30 ), 100, 25, 1f, 1.2f );
		Assert.AreEqual( 1.2f, size, 1e-5f );
		Assert.AreEqual( (300, 25), (width, height), "the box at 1.2 a font pixel" );
		Assert.AreEqual( (100f, 200f), (placed.X, placed.Y), "at the box's corner" );
		Assert.AreEqual( 360f, placed.Width, 1e-3f );
		Assert.AreEqual( 30f, placed.Height, 1e-3f );

		(placed, size, _, width, height) = UiText.Plan( new PixelRect( 0, 0, 1000, 1000 ), 100, 25, 2f, 1.2f );
		Assert.AreEqual( (1.2f, 416, 416), (size, width, height) );
		Assert.AreEqual( 998.4f, placed.Width, 1e-3f );

		(_, size, _, width, height) = UiText.Plan( new PixelRect( 0, 0, 351.9f, 20.7f ), 100, 25, 1f, 1f );
		Assert.AreEqual( (1f, 351, 20), (size, width, height), "cut toward nought, as 0x0048f47b and 0x0048f4a2" );
	}

	/// <summary>
	/// <b>Each line stands where the original's renderer puts it in the bitmap</b> (0x006ae550, 0x006aeb10; Q771): in a
	/// bitmap 101 wide a line 30 wide stands at 0 from the left and 71 from the right; centred, a wrapping one at 35 ((101 -
	/// 30) / 2 = 35.5 cut) and one that does not wrap at 35 (50 less 15); a line 31 wide at 35 and 34; one 131 wide, wider than
	/// its bitmap, at -30 from the right and -16 centred and unwrapped, so both its ends are cut; a block 3 lines of 12 in a
	/// bitmap 25 high stands at 0, -11 and -5 (-11 / 2 toward nought), so the top-aligned one loses its third line.
	/// </summary>
	/// <remarks><b>Mutations:</b> centre rounded to nearest; the unwrapped centre from the bitmap's own width, not its half;
	/// right-aligned held at 0; the block's middle held to the bitmap's top; halved toward minus infinity.</remarks>
	[TestMethod]
	public void EachLineStandsWhereTheOriginalsRendererPutsIt()
	{
		Assert.AreEqual( (0, 71), (UiText.LineLeft( 101, 30, TextAlign.Start, false ), UiText.LineLeft( 101, 30, TextAlign.End, false )) );
		Assert.AreEqual( (35, 35), (UiText.LineLeft( 101, 30, TextAlign.Centre, true ), UiText.LineLeft( 101, 30, TextAlign.Centre, false )) );
		Assert.AreEqual( (35, 34), (UiText.LineLeft( 101, 31, TextAlign.Centre, true ), UiText.LineLeft( 101, 31, TextAlign.Centre, false )) );
		Assert.AreEqual( (-30, -16), (UiText.LineLeft( 101, 131, TextAlign.End, false ), UiText.LineLeft( 101, 131, TextAlign.Centre, false )) );
		Assert.IsTrue( UiText.LineLeft( 101, 131, TextAlign.Centre, true ) >= 101, "a wrapped line wider than its box moves off it (the unsigned shift)" );
		Assert.AreEqual( (0, -11, -5), (UiText.BlockTop( 25, 36, TextAlign.Start ), UiText.BlockTop( 25, 36, TextAlign.End ),
			UiText.BlockTop( 25, 36, TextAlign.Centre )) );
		Assert.AreEqual( 6, UiText.BlockTop( 25, 12, TextAlign.Centre ), "(25 - 12) / 2 = 6.5, cut" );
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

			var (placed, drawn, copy, width, _) = UiText.Plan( new PixelRect( 0, 0, 360, 40 ), 100, 15, UiFonts.Scale, 1.2f );
			Assert.AreEqual( (1.2f, 2), (drawn, copy), "the box holds 1.2" );
			Assert.AreEqual( width * UiFonts.Scale * 1.2f, placed.Width, 1e-2f );

			(placed, drawn, copy, width, _) = UiText.Plan( new PixelRect( 0, 0, 360, 28 ), 100, 15, UiFonts.Scale, 1.2f );
			Assert.AreEqual( 28f / (15 * UiFonts.Scale), drawn, 1e-4f, "the box holds 1.058" );
			Assert.AreEqual( 1, copy, "drawn at 1.867 screen pixels a font pixel, so copied once" );
			Assert.IsTrue( placed.Width / width < 2f, "the picture is drawn smaller than two texels a pixel would be" );
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
