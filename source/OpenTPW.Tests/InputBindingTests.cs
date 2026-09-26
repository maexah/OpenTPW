using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoVeldrid;
using OpenTPW;

namespace OpenTPW.Tests;

/// <summary>
/// A chord should fire one shortcut. The original compares a binding's modifiers for equality and
/// not for containment, so Ctrl+C is Close Park and nothing else, and a plain C is camcorder mode
/// and nothing else. Eight of the cases here fail against a rule that asks only for a
/// binding's own keys: the Ctrl+C/H/S/V/P rows, EitherControlKeyStandsForControl,
/// AModifierNobodyAskedForStopsABinding, and the last assert of CloneIsTheControlKeyBeingDown. The
/// rest pin behaviour that has to keep working - the Ctrl+O row has no plain-O shortcut to collide
/// with at all - and the two-frame tests at the bottom cover a hole the single-frame ones cannot
/// see.
/// </summary>
[TestClass]
public class InputBindingTests
{
	private static IReadOnlyCollection<Key> Held( params Key[] keys ) => keys;

	/// <summary>
	/// The six chords the game actually ships, each with the plain-key shortcut it must not also fire.
	/// Ctrl+O has no plain-O shortcut to collide with, so it stands as the control case.
	/// </summary>
	private static IEnumerable<object[]> Chords => new[]
	{
		new object[] { Key.O, InputButton.OpenPark, InputButton.OpenPark },
		new object[] { Key.C, InputButton.ClosePark, InputButton.CamcorderMode },
		new object[] { Key.H, InputButton.ToggleHelpBar, InputButton.HireStaff },
		new object[] { Key.S, InputButton.StaffLocator, InputButton.AllStaff },
		new object[] { Key.V, InputButton.VisitorLocator, InputButton.AllVisitors },
		new object[] { Key.P, InputButton.SendEmailPostcard, InputButton.Pause },
	};

	[TestMethod]
	[DynamicData( nameof( Chords ) )]
	public void AChordFiresItsOwnShortcutAndNotThePlainKeyOne( Key key, InputButton withControl, InputButton plain )
	{
		var chord = Held( Key.ControlLeft, key );

		Assert.IsTrue( Input.BindingMatches( withControl, chord ),
			$"Ctrl+{key} should be {withControl}" );

		if ( plain != withControl )
		{
			Assert.IsFalse( Input.BindingMatches( plain, chord ),
				$"Ctrl+{key} should not also be {plain}" );
		}
	}

	[TestMethod]
	[DynamicData( nameof( Chords ) )]
	public void APlainKeyFiresItsOwnShortcutAndNotTheChordOne( Key key, InputButton withControl, InputButton plain )
	{
		var alone = Held( key );

		Assert.IsFalse( Input.BindingMatches( withControl, alone ),
			$"{key} on its own should not be {withControl}" );

		if ( plain != withControl )
		{
			Assert.IsTrue( Input.BindingMatches( plain, alone ), $"{key} on its own should be {plain}" );
		}
	}

	/// <summary>
	/// Only modifiers are held against a binding. Camcorder mode is reached while walking, and the
	/// walk keys are down at the time, so a rule of "nothing else may be held" would be unusable.
	/// </summary>
	[TestMethod]
	public void AKeyThatIsNotAModifierDoesNotStopABinding()
	{
		Assert.IsTrue( Input.BindingMatches( InputButton.CamcorderMode, Held( Key.W, Key.A, Key.C ) ) );
	}

	/// <summary>
	/// The original reads the combined virtual keys - VK_CONTROL, not one side of it - so the right
	/// hand modifier is the same modifier. Only the left is bound, and that is what makes this worth
	/// asserting: the binding names ControlLeft and the right one still has to work.
	/// </summary>
	[TestMethod]
	public void EitherControlKeyStandsForControl()
	{
		Assert.IsTrue( Input.BindingMatches( InputButton.ClosePark, Held( Key.ControlRight, Key.C ) ) );
	}

	[TestMethod]
	public void AModifierNobodyAskedForStopsABinding()
	{
		Assert.IsFalse( Input.BindingMatches( InputButton.CamcorderMode, Held( Key.ShiftLeft, Key.C ) ) );
		Assert.IsFalse( Input.BindingMatches( InputButton.ClosePark, Held( Key.ControlLeft, Key.AltLeft, Key.C ) ) );
	}

	/// <summary>
	/// Clone is Ctrl with no key of its own, so it means "Ctrl is down" - which is what holding it to
	/// clone an object asks for, and why it still stands while Ctrl+C is being pressed. A choice, not
	/// a reading of the original: clone is in none of its six binding tables, because it looks at the
	/// control key directly rather than going through a shortcut.
	/// </summary>
	[TestMethod]
	public void CloneIsTheControlKeyBeingDown()
	{
		Assert.IsTrue( Input.BindingMatches( InputButton.Clone, Held( Key.ControlLeft ) ) );
		Assert.IsTrue( Input.BindingMatches( InputButton.Clone, Held( Key.ControlLeft, Key.C ) ) );
		Assert.IsFalse( Input.BindingMatches( InputButton.Clone, Held( Key.C ) ) );
		Assert.IsFalse( Input.BindingMatches( InputButton.Clone, Held( Key.ControlLeft, Key.ShiftLeft ) ) );
	}

	[TestMethod]
	public void NothingHeldFiresNothing()
	{
		foreach ( var button in System.Enum.GetValues<InputButton>() )
			Assert.IsFalse( Input.BindingMatches( button, Held() ), $"{button} fired with no keys held" );
	}

	private static IReadOnlyCollection<InputButton> Buttons( params InputButton[] buttons ) => buttons;

	/// <summary>
	/// Matching a chord correctly is not enough on its own, because the binding set is rebuilt from
	/// what is held every frame: letting go of Ctrl while C is still down makes the plain-C binding
	/// match for the first time, and that looks exactly like a fresh press: Ctrl+C would enter camcorder
	/// mode after all - on the release rather than the press.
	/// </summary>
	[TestMethod]
	public void LettingGoOfTheModifierFirstIsNotAPressOfThePlainKeyShortcut()
	{
		Assert.IsFalse( Input.PressedFrom(
			InputButton.CamcorderMode,
			Buttons( InputButton.CamcorderMode ),
			Buttons( InputButton.ClosePark, InputButton.Clone ),
			Held() ) );
	}

	/// <summary>The same hole from the other side: C already held, and then Ctrl pressed.</summary>
	[TestMethod]
	public void PressingTheModifierAfterTheKeyIsNotAPressOfTheChordShortcut()
	{
		Assert.IsFalse( Input.PressedFrom(
			InputButton.ClosePark,
			Buttons( InputButton.ClosePark, InputButton.Clone ),
			Buttons( InputButton.CamcorderMode ),
			Held( Key.ControlLeft ) ) );
	}

	[TestMethod]
	public void AKeyActuallyGoingDownIsStillAPress()
	{
		Assert.IsTrue( Input.PressedFrom(
			InputButton.CamcorderMode,
			Buttons( InputButton.CamcorderMode ),
			Buttons(),
			Held( Key.C ) ), "plain C" );

		Assert.IsTrue( Input.PressedFrom(
			InputButton.ClosePark,
			Buttons( InputButton.ClosePark, InputButton.Clone ),
			Buttons( InputButton.Clone ),
			Held( Key.C ) ), "C completing Ctrl+C" );
	}

	/// <summary>A held key repeats, and a repeat is not a new press.</summary>
	[TestMethod]
	public void AButtonThatWasAlreadyDownIsNotAPress()
	{
		Assert.IsFalse( Input.PressedFrom(
			InputButton.CamcorderMode,
			Buttons( InputButton.CamcorderMode ),
			Buttons( InputButton.CamcorderMode ),
			Held( Key.C ) ) );
	}

	/// <summary>
	/// A modifier let go of while the game is pumping events and dropping them - a loading screen, a
	/// minimised window - never arrives as a key-up. Because modifiers are matched exactly, one left
	/// held stops every binding that names no modifier, the Escape menu included.
	/// </summary>
	[TestMethod]
	public void ForgettingHeldKeysClearsAStuckModifier()
	{
		Input.Keyboard = new Input.KeyboardInfo( new List<Key> { Key.AltLeft, Key.Escape } );

		Assert.IsFalse( Input.BindingMatches( InputButton.Menu, Input.Keyboard.KeysDown ),
			"a stuck Alt blocking Escape is the hazard being guarded against" );

		Input.ForgetHeldKeys();

		Assert.AreEqual( 0, Input.Keyboard.KeysDown.Count );
		Assert.IsTrue( Input.BindingMatches( InputButton.Menu, Held( Key.Escape ) ) );
	}
}
