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
	/// <b>The offset is in interface units, cut toward nought at the screen's size, and moves edges of the text's box</b>
	/// (<c>0x0048f8c5</c>..<c>0x0048fa0e</c>), each case naming its down alignment: at 1024 x 768 (half a pixel a unit) (2, 1)
	/// is 1 pixel across and 0 down, and a text centred both ways moves half that; at 800 x 600 nothing moves; a left- and
	/// top-aligned text moves the whole offset; a right-aligned one moves left, and up by the whole offset bottom-aligned,
	/// half middle-aligned and not at all top-aligned; a left one bottom-aligned does not move down. Its size is kept.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the offset in the font's pixels (unscaled, or by the font's scale); not cut toward nought; a centred
	/// text moved the whole offset; a right-aligned text moved right and down; the top edge moved for a right-aligned text.
	/// </remarks>
	[TestMethod]
	public void TheOffsetIsInInterfaceUnitsAndMovesEdgesOfTheTextsBox()
	{
		var placed = new Rectangle( 10f, 20f, 30f, 40f );

		Assert.AreEqual( new Rectangle( 10.5f, 20f, 30f, 40f ), UiText.Nudge( placed, 2, 1, 0.5f, TextAlign.Centre, TextAlign.Centre ), "1024 x 768, centre, middle" );
		Assert.AreEqual( placed, UiText.Nudge( placed, 2, 1, 600f / 1536f, TextAlign.Centre, TextAlign.Centre ), "800 x 600, centre, middle" );
		Assert.AreEqual( new Rectangle( 12f, 21f, 30f, 40f ), UiText.Nudge( placed, 2, 1, 1f, TextAlign.Start, TextAlign.Start ), "2048 x 1536, left, top" );
		Assert.AreEqual( new Rectangle( 12f, 20f, 30f, 40f ), UiText.Nudge( placed, 2, 1, 1f, TextAlign.Start, TextAlign.End ), "2048 x 1536, left, bottom" );
		Assert.AreEqual( new Rectangle( 8f, 19f, 30f, 40f ), UiText.Nudge( placed, 2, 1, 1f, TextAlign.End, TextAlign.End ), "2048 x 1536, right, bottom" );
		Assert.AreEqual( new Rectangle( 8f, 19.5f, 30f, 40f ), UiText.Nudge( placed, 2, 1, 1f, TextAlign.End, TextAlign.Centre ), "2048 x 1536, right, middle" );
		Assert.AreEqual( new Rectangle( 8f, 20f, 30f, 40f ), UiText.Nudge( placed, 2, 1, 1f, TextAlign.End, TextAlign.Start ), "2048 x 1536, right, top" );
		Assert.AreEqual( new Rectangle( 11.5f, 21f, 30f, 40f ), UiText.Nudge( placed, 2, 1, 1.5f, TextAlign.Centre, TextAlign.Start ), "3072 x 2304, centre, top" );
	}

	/// <summary>
	/// <b>The offset across narrows a wrapping text's box by itself</b>, in pixels cut toward nought: the renderer breaks at
	/// the box's right less its left (<c>0x006ae152</c>), and the offset has moved one of those edges in. A box 100 wide
	/// wraps at 99 at 1024 x 768, 100 at 800 x 600, 97 at 3072 x 2304.
	/// </summary>
	/// <remarks><b>Mutations:</b> the wrap left at the whole box; the offset not cut; narrowed by the offset down.</remarks>
	[TestMethod]
	public void TheOffsetAcrossNarrowsTheWrap()
	{
		Assert.AreEqual( 99f, UiText.WrapAreaWidth( 100f, 2, 0.5f ) );
		Assert.AreEqual( 100f, UiText.WrapAreaWidth( 100f, 2, 600f / 1536f ) );
		Assert.AreEqual( 97f, UiText.WrapAreaWidth( 100f, 2, 1.5f ) );
		Assert.AreEqual( 100f, UiText.WrapAreaWidth( 100f, 0, 1.5f ) );
	}
}
