using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoVeldrid;
using OpenTPW.UI;
using System.Collections.Generic;

namespace OpenTPW.Tests;

/// <summary>
/// <b>The cursor Up and Down keys step an object window</b> (Q547; the base proc <c>FUN_0048d200</c>, <c>docs/exe/hud.md</c>,
/// "The shop, sideshow and ride windows"): each key down, a repeat included, is one step of the window in front, Up
/// <c>FUN_0048cbe0</c> towards the newest and Down <c>FUN_0048caf0</c> towards the oldest. Needs no game: a window of
/// its own records the steps. The real windows' steps are <see cref="ParkShopWindowTests"/>' and <see cref="ParkStaffWindowTests"/>'.
/// </summary>
[TestClass]
public class ObjectWindowKeysTests
{
	/// <summary>
	/// Up, Left, Down, the keypad's 8 and 2 (<c>0x26</c> and <c>0x28</c> with NumLock off, not <c>0x2600</c> and
	/// <c>0x2800</c>) and Up again, in one frame: three steps in the order they went down, newer, older, newer; the other
	/// keys none.
	/// </summary>
	/// <remarks><b>Mutations:</b> Up and Down swapped; the keypad's arrows taken; only the first key of a frame taken.</remarks>
	[TestMethod]
	public void EachCursorKeyDownIsOneStepOfItsWay()
	{
		Log ??= new();
		var window = new Stepping( new WindowStack() );

		var steps = ParkFrontEnd.ObjectWindowKeys( window, [Key.Up, Key.Left, Key.Down, Key.Keypad8, Key.Keypad2, Key.Up] );

		Assert.AreEqual( 3, steps );
		CollectionAssert.AreEqual( new[] { true, false, true }, window.Steps );
	}

	/// <summary>A window in front that is no object window (a message box over a shop's, the game menu) hears neither key, and none in front neither.</summary>
	/// <remarks><b>Mutations:</b> the keys handed to an object window behind the front one.</remarks>
	[TestMethod]
	public void OnlyAnObjectWindowInFrontHearsThem()
	{
		Log ??= new();
		var stack = new WindowStack();

		Assert.AreEqual( 0, ParkFrontEnd.ObjectWindowKeys( new NotAnObjectWindow( stack ), [Key.Up, Key.Down] ) );
		Assert.AreEqual( 0, ParkFrontEnd.ObjectWindowKeys( null, [Key.Down] ) );
	}

	private sealed class Stepping( WindowStack stack ) : UiWindow( stack ), IObjectWindowSteps
	{
		public List<bool> Steps { get; } = [];

		public void Step( bool newer ) => Steps.Add( newer );
	}

	private sealed class NotAnObjectWindow( WindowStack stack ) : UiWindow( stack );
}
