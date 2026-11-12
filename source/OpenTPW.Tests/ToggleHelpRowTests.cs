using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// A toggle's help row: the button class's getter (<c>0x00669410</c>; <c>docs/exe/hud.md</c>, the tree stream's op 0x12)
/// answers the op 0x12 row while the state byte is exactly 1, on and not held down, and the op 0x11 row otherwise. The new
/// player dialog's two mode toggles carry 358 and 359 as op 0x11 and -1 as op 0x12 (Q772).
/// </summary>
[TestClass]
public class ToggleHelpRowTests
{
	private static readonly Vector2 OnInstantAction = new( 985, 440 );
	private static readonly Vector2 OnFullSimulation = new( 985, 533 );
	private static readonly Vector2 OnTheName = new( 900, 657 );

	private Point2 _screen;
	private WindowStack _stack = null!;

	/// <summary>
	/// <b>The on row only while on and not held</b>, the op 0x11 row off, held off and held on; a button whose op 0x12 this
	/// game does not carry keeps its op 0x11 row in every state.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the on row answered while held down too (the state byte taken as bit 0, not exactly 1); the rows
	/// swapped; the on row answered for a button that does not carry one.
	/// </remarks>
	[TestMethod]
	public void AToggleShowsItsSecondRowOnlyWhileItIsOnAndNotHeld()
	{
		var toggle = new UiButton { HelpText = 86, HelpWhenOn = 87, Toggles = true };

		Assert.AreEqual( 86, toggle.ShownHelp, "off: op 0x11" );

		toggle.Pressed = true;
		Assert.AreEqual( 86, toggle.ShownHelp, "off and held (state 2): op 0x11" );

		toggle.IsDown = true;
		Assert.AreEqual( 86, toggle.ShownHelp, "on and held (state 3): op 0x11" );

		toggle.Pressed = false;
		Assert.AreEqual( 87, toggle.ShownHelp, "on (state exactly 1): op 0x12" );

		var plain = new UiButton { HelpText = 30, IsDown = true };
		Assert.AreEqual( 30, plain.ShownHelp, "no op 0x12 carried: its row whatever its state" );
	}

	/// <summary>
	/// <b>The new player dialog's chosen toggle shows the empty bar</b>, and the other its row: before a pick both show
	/// theirs; the release that picks Instant Action empties the bar under the resting pointer; Full Simulation still shows
	/// 359, and once picked shows none while Instant Action shows 358 again; Full Simulation held down shows 359.
	/// </summary>
	/// <remarks><b>Mutations:</b> the dialog without its op 0x12 rows (the row shown over either); the held state ignored.</remarks>
	[TestMethod]
	public void TheNewPlayerDialogsChosenToggleShowsNoHelpRow()
	{
		Log ??= new();
		FileSystem = GameData.Required();

		_screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );
		Input.Mouse = new();
		Time.PinWall( 0 );

		try
		{
			_stack = new WindowStack();
			var frontEnd = (FrontEnd)RuntimeHelpers.GetUninitializedObject( typeof( FrontEnd ) );
			var dialog = new NewPlayerDialog( _stack, frontEnd, 0 );
			_stack.Open( dialog );

			int Mode( int id ) => dialog.Root.DrawOrder().OfType<UiButton>().Single( button => button.Id == id ) is { IsDown: true } ? 1 : 0;

			Frame( false, OnTheName, 100 );
			Assert.AreEqual( 356, _stack.HelpRow, "the name box: the pointer's start" );

			Frame( false, OnInstantAction, 200 );
			Assert.AreEqual( 358, _stack.HelpRow, "Instant Action, none chosen" );

			Frame( false, OnFullSimulation, 300 );
			Assert.AreEqual( 359, _stack.HelpRow, "Full Simulation, none chosen" );

			Frame( false, OnInstantAction, 400 );
			Frame( true, OnInstantAction, 500 );
			Frame( false, OnInstantAction, 600 );
			Frame( false, OnInstantAction, 700 );
			Assert.AreEqual( (1, -1), (Mode( 0x70d ), _stack.HelpRow), "Instant Action picked: the bar is empty over it" );

			Frame( false, OnFullSimulation, 800 );
			Assert.AreEqual( 359, _stack.HelpRow, "Full Simulation, the other: its row" );

			Frame( true, OnFullSimulation, 900 );
			Frame( false, OnFullSimulation, 1000 );
			Frame( false, OnFullSimulation, 1100 );
			Assert.AreEqual( (1, -1), (Mode( 0x70e ), _stack.HelpRow), "Full Simulation picked: empty" );

			Frame( false, OnInstantAction, 1200 );
			Assert.AreEqual( (0, 358), (Mode( 0x70d ), _stack.HelpRow), "Instant Action, no longer chosen: 358 again" );

			Frame( false, OnFullSimulation, 1300 );
			Frame( true, OnFullSimulation, 1400 );
			Assert.AreEqual( 359, _stack.HelpRow, "the chosen one held down (state 3): its op 0x11 row" );

			Frame( false, OnFullSimulation, 1500 );
			Frame( false, OnFullSimulation, 1600 );
			Assert.AreEqual( -1, _stack.HelpRow, "let go, still chosen: empty again" );
		}
		finally
		{
			Input.Mouse = new();
			Time.PinWall( 0 );
			new WindowStack().Update();
			Screen.Size = _screen;
		}
	}

	private void Frame( bool down, Vector2 at, long now )
	{
		Time.PinWall( now );
		Input.Mouse = new() { Left = down, LeftWentDown = down && !Input.Mouse.Left, Position = at };
		_stack.Update();
	}
}
