using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTPW.UI;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OpenTPW.Tests;

/// <summary>
/// A toggle's help row: the button class's getter (<c>0x00669410</c>; <c>docs/exe/hud.md</c>, the tree stream's op 0x12)
/// answers the op 0x12 row while the state byte is exactly 1 and the op 0x11 row otherwise, and no post reads the held state.
/// The new player dialog's two mode toggles carry 358 and 359 as op 0x11 and -1 as op 0x12 (Q772).
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
	/// <b>The on row while on, held or not</b> (no post reads the held bit: a press posts nothing, the pointer coming on posts
	/// before it sets the bit, <c>0x006692b2</c> before <c>0x0066930f</c>), the op 0x11 row off and held off; a button whose op
	/// 0x12 this game does not carry keeps its op 0x11 row in every state.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the held state read (the getter's state 3 posted); the rows swapped; the on row answered for a button
	/// that does not carry one.
	/// </remarks>
	[TestMethod]
	public void AToggleShowsItsSecondRowWhileItIsOn()
	{
		var toggle = new UiButton { HelpText = 86, HelpWhenOn = 87, Toggles = true };

		Assert.AreEqual( 86, toggle.ShownHelp, "off: op 0x11" );

		toggle.Pressed = true;
		Assert.AreEqual( 86, toggle.ShownHelp, "off and held (state 2): op 0x11" );

		toggle.IsDown = true;
		Assert.AreEqual( 87, toggle.ShownHelp, "on and held: the row the pointer coming on posts, before the held bit" );

		toggle.Pressed = false;
		Assert.AreEqual( 87, toggle.ShownHelp, "on (state exactly 1): op 0x12" );

		var plain = new UiButton { HelpText = 30, IsDown = true };
		Assert.AreEqual( 30, plain.ShownHelp, "no op 0x12 carried: its row whatever its state" );
	}

	/// <summary>
	/// <b>The new player dialog's chosen toggle shows the empty bar</b>, and the other its row: before a pick both show
	/// theirs; the release that picks Instant Action empties the bar under the resting pointer; Full Simulation still shows
	/// 359, and once picked shows none while Instant Action shows 358 again. Full Simulation pressed again posts nothing
	/// (<c>0x10005</c>), so the bar stays empty while it is held; let go, the release posts state 0's row, 359
	/// (<c>0x00669172</c>), before the group puts it down again (<c>0x0066a468</c>), and 359 stands. Left while held and come
	/// back to, it posts -1 again: the leave clears the held bit (<c>0x00669278</c>) and the pointer coming on posts before
	/// setting it.
	/// </summary>
	/// <remarks><b>Mutations:</b> the dialog without its op 0x12 rows (the row shown over either); the held state read (359
	/// held and on coming back); the row re-posted on a state change (-1 after the let-go); the release's post left out.</remarks>
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
			Assert.AreEqual( -1, _stack.HelpRow, "the chosen one held down: the press posts nothing" );

			Frame( false, OnFullSimulation, 1500 );
			Frame( false, OnFullSimulation, 1600 );
			Assert.AreEqual( (1, 359), (Mode( 0x70e ), _stack.HelpRow), "let go, still chosen: state 0's row, posted before the group's restore" );

			Frame( true, OnFullSimulation, 1700 );
			Frame( true, OnTheName, 1800 );
			Assert.AreEqual( 356, _stack.HelpRow, "held, the pointer on the name box" );

			Frame( true, OnFullSimulation, 1900 );
			Assert.AreEqual( -1, _stack.HelpRow, "held, the pointer back on: posted before the held bit is set" );

			Frame( false, OnFullSimulation, 2000 );
			Frame( false, OnFullSimulation, 2100 );
			Assert.AreEqual( (1, 359), (Mode( 0x70e ), _stack.HelpRow), "let go: 359 again" );
		}
		finally
		{
			Input.Mouse = new();
			Time.PinWall( 0 );
			new WindowStack().Update();
			Screen.Size = _screen;
		}
	}

	/// <summary>
	/// <b>The new player dialog's mode toggles click <c>Select3</c> (189)</b>: each tells its group, <c>0x70c</c>, flags
	/// <c>0x11</c> (stream <c>0x00753f50</c>), and the click hook picks the sound by the receiver's flag <c>0x10</c>
	/// (<c>0x004857cb</c>), where the map's and the options screen's switches play <c>BUTTON01</c> (31).
	/// </summary>
	/// <remarks><b>Mutations:</b> the group's <c>Flag10</c> left off (both 31); the receiver's flag not read (the same).</remarks>
	[TestMethod]
	public void TheModeTogglesClickSelect3()
	{
		Log ??= new();
		FileSystem = GameData.Required();

		_screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );
		Input.Mouse = new();

		try
		{
			_stack = new WindowStack();
			var frontEnd = (FrontEnd)RuntimeHelpers.GetUninitializedObject( typeof( FrontEnd ) );
			var dialog = new NewPlayerDialog( _stack, frontEnd, 0 );
			_stack.Open( dialog );

			int? Click( int id )
			{
				var at = dialog.Root.DrawOrder().OfType<UiButton>().Single( button => button.Id == id ).Pixels;
				UiSounds.LastClick = null;
				_stack.ClickAt( at.X + (at.Width / 2f), at.Y + (at.Height / 2f) );
				return UiSounds.LastClick;
			}

			Assert.AreEqual( (189, 189, 189), (Click( 0x70d ), Click( 0x70e ), Click( 0x70d )), "Instant Action, Full Simulation, Instant Action" );
		}
		finally
		{
			Input.Mouse = new();
			new WindowStack().Update();
			Screen.Size = _screen;
			UiSounds.LastClick = null;
		}
	}

	private void Frame( bool down, Vector2 at, long now )
	{
		Time.PinWall( now );
		Input.Mouse = new() { Left = down, LeftWentDown = down && !Input.Mouse.Left, Position = at };
		_stack.Update();
	}
}
