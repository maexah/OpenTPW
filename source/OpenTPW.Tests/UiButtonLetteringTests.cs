using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.Linq;

namespace OpenTPW.Tests;

/// <summary>
/// A text button's lettering by its state, as the original's button class (vtable <c>0x00705a50</c>) letters its label
/// (<c>0x00668ad3</c>; <c>docs/exe/ui.md</c>, "A text button's lettering by state"). Nothing here needs the game or a
/// graphics device.
/// </summary>
[TestClass]
public class UiButtonLetteringTests
{
	private static readonly UiColour Yellow = new( 255, 239, 0 );

	private static UiButton Lettered() => new()
	{
		Text = "Name",
		TextColour = UiColour.White,
		LettersByState = true
	};

	/// <summary>
	/// <b>Plain, the pointer on it, pressed</b>: white where it lies, the pointer on it or not (states 0 and 2: the pointer's
	/// setter <c>0x00668c99</c> never re-letters); white (2, 1) pressed with the pointer on it (state 4); dragged off while
	/// held, plain again (leaving clears <c>+0x138</c> bit 1, <c>0x00669278</c>).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the pointer lettered (1, 1); the pressed offset (1, 1); pressed lettered (2, 1) with the pointer off it.
	/// </remarks>
	[TestMethod]
	public void AButtonLettersPlainUnderThePointerAndMovedOnlyWhilePressedOnIt()
	{
		var button = Lettered();

		Assert.AreEqual( (UiColour.White, 0, 0), button.Lettering, "plain" );

		button.Hovered = true;
		Assert.AreEqual( (UiColour.White, 0, 0), button.Lettering, "the pointer on it" );

		button.Pressed = true;
		Assert.AreEqual( (UiColour.White, 2, 1), button.Lettering, "pressed with the pointer on it" );

		button.Hovered = false;
		Assert.AreEqual( (UiColour.White, 0, 0), button.Lettering, "pressed, the pointer dragged off it" );
	}

	/// <summary>
	/// <b>A down button letters in its down colour moved (1, 1)</b>, the pointer on it or not (states 5 and 3), and pressed
	/// with the pointer on it as pressed (state 4: the press clears the pointer's flag, <c>0x006690a0</c>); dragged off while
	/// held, down again.
	/// </summary>
	/// <remarks><b>Mutations:</b> down lettered plain; down tested before pressed; down lettered only with the pointer on it.</remarks>
	[TestMethod]
	public void ADownButtonLettersDownWhereverThePointerIsAndPressedOnlyWhilePressedOnIt()
	{
		var button = Lettered();
		button.IsDown = true;

		Assert.AreEqual( (UiColour.Black, 1, 1), button.Lettering, "down, the pointer off it" );

		button.Hovered = true;
		Assert.AreEqual( (UiColour.Black, 1, 1), button.Lettering, "down, the pointer on it" );

		button.Pressed = true;
		Assert.AreEqual( (UiColour.White, 2, 1), button.Lettering, "down and pressed, the pointer on it" );

		button.Hovered = false;
		Assert.AreEqual( (UiColour.Black, 1, 1), button.Lettering, "down and pressed, the pointer dragged off it" );
	}

	/// <summary>
	/// <b>A disabled button keeps the lettering it had</b>: state 1 sends its label no flags. One never lettered while
	/// enabled is lettered plain.
	/// </summary>
	/// <remarks><b>Mutations:</b> disabled lettered plain whatever it had; disabled lettered by its state.</remarks>
	[TestMethod]
	public void ADisabledButtonKeepsTheLetteringItHad()
	{
		var button = Lettered();
		button.Enabled = false;
		button.IsDown = true;

		Assert.AreEqual( (UiColour.White, 0, 0), button.Lettering, "disabled, never lettered" );

		button.Enabled = true;
		Assert.AreEqual( (UiColour.Black, 1, 1), button.Lettering, "down" );

		button.Enabled = false;
		button.IsDown = false;
		button.Hovered = true;
		button.Pressed = true;
		Assert.AreEqual( (UiColour.Black, 1, 1), button.Lettering, "disabled after down" );
	}

	/// <summary>
	/// <b>A button that does not letter by state keeps its one colour where it lies</b>, the pointer on it or pressed.
	/// </summary>
	/// <remarks><b>Mutations:</b> every button lettered by state.</remarks>
	[TestMethod]
	public void AButtonThatDoesNotLetterByStateKeepsItsColourAndPlace()
	{
		var button = new UiButton { Text = "Load", TextColour = UiColour.Black, Hovered = true, Pressed = true };

		Assert.AreEqual( (UiColour.Black, 0, 0), button.Lettering );
	}

	/// <summary>
	/// <b>A framed list heading letters in its own colour in every state, moved by state</b>: <c>FUN_00486660</c> sets both
	/// colours to its caller's (255, 239, 0 from every list opener). A bare heading does not letter by state.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the heading's down colour left black; the framed heading not lettered by state; a bare heading
	/// lettered by state; the pointer lettered (1, 1).
	/// </remarks>
	[TestMethod]
	public void AFramedHeadingLettersInItsColourMovedByState()
	{
		var list = new UiList { RowArea = new UiRect( 0, 0, 100, 440 ), Columns = [(0, 60), (60, 100)] };
		var framed = list.AddFramedHeading( 0, new UiRect( 0, 0, 60, 10 ), "Name", 0x77 );
		var bare = list.AddHeading( 1, new UiRect( 60, 0, 100, 10 ), "Wage" );

		Assert.AreEqual( (Yellow, 0, 0), framed.Lettering, "plain" );

		framed.Hovered = bare.Hovered = true;
		Assert.AreEqual( (Yellow, 0, 0), framed.Lettering, "the pointer on it" );

		framed.Pressed = bare.Pressed = true;
		Assert.AreEqual( (Yellow, 2, 1), framed.Lettering, "pressed with the pointer on it" );
		Assert.AreEqual( (UiColour.White, 0, 0), bare.Lettering, "a bare heading, pressed with the pointer on it" );

		framed.Pressed = framed.Hovered = false;
		framed.IsDown = true;
		Assert.AreEqual( (Yellow, 1, 1), framed.Lettering, "down" );
	}

	/// <summary>
	/// <b>The offset is in interface units, cut toward nought at the font set's screen</b> (<c>0x0048f8c5</c>, <c>0x0048f8ff</c>):
	/// (2, 1) is 1 bitmap pixel across and 0 down at 1024 x 768, nothing at 800 x 600, and -3 units at 1024 wide is -1 (-1.5 cut
	/// toward nought); drawn 2 times larger, 2 units at 1024 wide is half a bitmap pixel, so 0.
	/// </summary>
	/// <remarks><b>Mutations:</b> the offset not scaled (units as pixels); floored; the drawn size not used.</remarks>
	[TestMethod]
	public void TheOffsetIsInInterfaceUnitsAtTheSetsScreen()
	{
		Assert.AreEqual( (1, 0), (UiText.OffsetIn( 2, UiText.SetScreenWidth( 768 ), 2048, 1f ), UiText.OffsetIn( 1, 768, 1536, 1f )), "1024 x 768" );
		Assert.AreEqual( (0, 0), (UiText.OffsetIn( 2, UiText.SetScreenWidth( 600 ), 2048, 1f ), UiText.OffsetIn( 1, 600, 1536, 1f )), "800 x 600" );
		Assert.AreEqual( -1, UiText.OffsetIn( -3, 1024, 2048, 1f ), "toward nought" );
		Assert.AreEqual( 0, UiText.OffsetIn( 2, 1024, 2048, 2f ), "drawn twice the size" );
	}

	/// <summary>
	/// <b>The offset moves edges of the renderer's box inside the bitmap, never the bitmap</b> (<c>0x0048f8c5</c>..<c>0x0048fa0e</c>),
	/// each case naming its down alignment, in a wrapping bitmap 100 x 40 with a line 30 x 12 and an offset (2, 3): a left label
	/// moves 2 across, 3 down top-aligned and none bottom-aligned; a centred one moves half across (35 to 36: the left edge
	/// alone moves) and 1 down middle-aligned ((37 - 12) / 2 = 12, then 3 + 12 = 15 from 14); a right one moves 2 left, and up
	/// by the whole 3 bottom-aligned, 1 middle-aligned and none top-aligned (its top stays 0).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a centred text moved the whole offset (both edges moved); a right-aligned text moved right and down; the
	/// top edge moved for a right-aligned text; the bottom edge moved for a left-aligned one.
	/// </remarks>
	[TestMethod]
	public void TheOffsetMovesEdgesOfTheTextsBox()
	{
		(int Left, int Top) At( TextAlign across, TextAlign down, int x, int y )
		{
			var box = UiText.BoxOf( 100, 40, true, across, x, y );
			return (UiText.LineLeft( box, 30, 30, across ), UiText.BlockTop( box, 12, down ));
		}

		Assert.AreEqual( (0, 0), At( TextAlign.Start, TextAlign.Start, 0, 0 ) );
		Assert.AreEqual( (2, 3), At( TextAlign.Start, TextAlign.Start, 2, 3 ), "left, top" );
		Assert.AreEqual( (2, 28), At( TextAlign.Start, TextAlign.End, 2, 3 ), "left, bottom" );
		Assert.AreEqual( ((35, 14), (36, 15)), (At( TextAlign.Centre, TextAlign.Centre, 0, 0 ), At( TextAlign.Centre, TextAlign.Centre, 2, 3 )), "centre, middle" );
		Assert.AreEqual( ((70, 28), (68, 25)), (At( TextAlign.End, TextAlign.End, 0, 0 ), At( TextAlign.End, TextAlign.End, 2, 3 )), "right, bottom" );
		Assert.AreEqual( (68, 12), At( TextAlign.End, TextAlign.Centre, 2, 3 ), "right, middle: (37 - 12) / 2 from 14" );
		Assert.AreEqual( (68, 0), At( TextAlign.End, TextAlign.Start, 2, 3 ), "right, top" );
	}

	/// <summary>
	/// <b>The offset across narrows a wrapping text's box by itself</b>, in bitmap pixels cut toward nought: the renderer breaks
	/// at the box's right less its left (<c>0x006ae152</c>), and the offset has moved one of those edges in. At 2048 x 1536 (set
	/// 768, a font pixel 2 screen pixels) a box 372 wide wraps at 186; with 2 units across at 185; with 1 at 186 (half a pixel cut).
	/// </summary>
	/// <remarks><b>Mutations:</b> the wrap left at the whole box; the offset not cut; narrowed by the offset down.</remarks>
	[TestMethod]
	public void TheOffsetAcrossNarrowsTheWrap()
	{
		var screen = Screen.Size;

		try
		{
			Screen.Size = new Point2( 2048, 1536 );
			Assert.AreEqual( 768, UiFonts.SetScreenHeight );
			Assert.AreEqual( 186, UiText.WrapWidthIn( 372, 1f, 0, 768 ) );
			Assert.AreEqual( 185, UiText.WrapWidthIn( 372, 1f, 2, 768 ) );
			Assert.AreEqual( 186, UiText.WrapWidthIn( 372, 1f, 1, 768 ) );
		}
		finally
		{
			Screen.Size = screen;
		}
	}
}
