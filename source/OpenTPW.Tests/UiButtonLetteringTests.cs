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
	/// <b>Plain, the pointer on it, pressed</b>: white where it lies, black (1, 1) with the pointer on it, white (2, 1)
	/// pressed, the pointer on it or not (states 0, 5 and 4).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the pointer's colour the plain one; the pointer's offset (0, 0); the pressed offset the pointer's
	/// (1, 1); the pointer tested before pressed (a pressed button with the pointer on it black at (1, 1)).
	/// </remarks>
	[TestMethod]
	public void AButtonLettersPlainPointedAndPressedEachItsOwnWay()
	{
		var button = Lettered();

		Assert.AreEqual( (UiColour.White, 0, 0), button.Lettering, "plain" );

		button.Hovered = true;
		Assert.AreEqual( (UiColour.Black, 1, 1), button.Lettering, "the pointer on it" );

		button.Pressed = true;
		Assert.AreEqual( (UiColour.White, 2, 1), button.Lettering, "pressed with the pointer on it" );

		button.Hovered = false;
		Assert.AreEqual( (UiColour.White, 2, 1), button.Lettering, "pressed, the pointer off it" );
	}

	/// <summary>
	/// <b>A down button reads as down, not pressed</b>: the getter (<c>0x00668820</c>) tests down before pressed, so a down
	/// switch letters plain, or the pointer's way with the pointer on it, pressed or not (states 2 and 3).
	/// </summary>
	/// <remarks><b>Mutations:</b> pressed tested before down; down with the pointer on it lettered plain.</remarks>
	[TestMethod]
	public void ADownButtonLettersPlainOrThePointersWayWhetherPressedOrNot()
	{
		var button = Lettered();
		button.IsDown = true;
		button.Pressed = true;

		Assert.AreEqual( (UiColour.White, 0, 0), button.Lettering, "down and pressed, the pointer off it" );

		button.Hovered = true;
		Assert.AreEqual( (UiColour.Black, 1, 1), button.Lettering, "down and pressed, the pointer on it" );

		button.Pressed = false;
		Assert.AreEqual( (UiColour.Black, 1, 1), button.Lettering, "down, the pointer on it" );
	}

	/// <summary>
	/// <b>A disabled button keeps the lettering it had</b>: state 1 sends its label no flags. One never lettered while
	/// enabled is lettered plain.
	/// </summary>
	/// <remarks><b>Mutations:</b> disabled lettered plain whatever it had; disabled lettered by its pointer and press.</remarks>
	[TestMethod]
	public void ADisabledButtonKeepsTheLetteringItHad()
	{
		var button = Lettered();
		button.Enabled = false;
		button.Hovered = true;

		Assert.AreEqual( (UiColour.White, 0, 0), button.Lettering, "disabled, never lettered" );

		button.Enabled = true;
		Assert.AreEqual( (UiColour.Black, 1, 1), button.Lettering, "the pointer on it" );

		button.Enabled = false;
		button.Hovered = false;
		button.Pressed = true;
		Assert.AreEqual( (UiColour.Black, 1, 1), button.Lettering, "disabled after the pointer" );
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
	/// <b>Mutations:</b> the heading's pointer colour left black; the framed heading not lettered by state; a bare heading
	/// lettered by state.
	/// </remarks>
	[TestMethod]
	public void AFramedHeadingLettersInItsColourMovedByState()
	{
		var list = new UiList { RowArea = new UiRect( 0, 0, 100, 440 ), Columns = [(0, 60), (60, 100)] };
		var framed = list.AddFramedHeading( 0, new UiRect( 0, 0, 60, 10 ), "Name", 0x77 );
		var bare = list.AddHeading( 1, new UiRect( 60, 0, 100, 10 ), "Wage" );

		Assert.AreEqual( (Yellow, 0, 0), framed.Lettering, "plain" );

		framed.Hovered = bare.Hovered = true;
		Assert.AreEqual( (Yellow, 1, 1), framed.Lettering, "the pointer on it" );
		Assert.AreEqual( (UiColour.White, 0, 0), bare.Lettering, "a bare heading, the pointer on it" );

		framed.Pressed = true;
		Assert.AreEqual( (Yellow, 2, 1), framed.Lettering, "pressed" );
	}

	/// <summary>
	/// <b>The offset is in the font's pixels</b>, each as many screen pixels as the text is drawn at: (2, 1) at a scale
	/// of 1.5 moves the text 3 right and 1.5 down, and keeps its size.
	/// </summary>
	/// <remarks><b>Mutations:</b> the offset in screen pixels (unscaled); across and down swapped; moved up.</remarks>
	[TestMethod]
	public void TheOffsetIsInTheFontsPixelsAtTheDrawnScale()
	{
		var placed = UiText.Nudge( new Rectangle( 10f, 20f, 30f, 40f ), 2, 1, 1.5f );

		Assert.AreEqual( new Rectangle( 13f, 21.5f, 30f, 40f ), placed );
	}
}
