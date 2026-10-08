using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoVeldrid;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OpenTPW.Tests;

/// <summary>
/// The park's reached paths that are counted and do nothing more: the game menu's Save Game and Publish
/// Park, the gadget's postcard button, and R, the shortcuts table's research row (<c>0x0040c5b0</c>, into
/// <c>FUN_004aa480</c>). Each is counted where the player reaches it and only there. See <c>ParkFrontEnd.NotYet</c>,
/// <c>ParkFrontEnd.ResearchKey</c> and <c>ParkGadget.SendPostcard</c>.
///
/// <para>
/// <b>The frame is the game's own</b>: SDL's key events through <see cref="Input.UpdateFrom"/> and the real stack's
/// whole update under the real park front end; a menu row and the postcard button are chosen through the click the
/// stack would send them.
/// </para>
/// </summary>
[TestClass]
public class ParkCountedPathsTests
{
	/// <summary>A point of the park no control is over.</summary>
	private static readonly Vector2 Grass = new( 1100, 800 );

	private Point2 _screen;
	private Level? _level;

	[TestInitialize]
	public void StartWithNothingHeld()
	{
		Log ??= new();
		FileSystem = GameData.Required();

		_screen = Screen.Size;
		Screen.Size = new Point2( 2048, 1536 );
		_level = Level.Current;

		Input.ForgetHeldKeys();
		Input.Mouse = new();
		Input.TextCaptured = false;
		Unimplemented.Forget();
		Time.Now = 100f;
	}

	[TestCleanup]
	public void PutItAllBack()
	{
		ParkBuildMode.Forget();
		ParkCamcorderCameraMode.Forget();
		Level.Current = _level!;
		Input.ForgetHeldKeys();
		Input.Mouse = new();
		Input.TextCaptured = false;
		Unimplemented.Forget();

		Screen.Size = _screen;
	}

	/// <summary>
	/// <b>Publish Park closes the menu and is counted under its own name</b>, once a choice, and the choices that
	/// work are not counted.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> the report taken out of <c>NotYet</c> counts nothing; two rows sharing a name leaves one at
	/// nought; a report in Resume Game's row counts a fourth.
	/// </remarks>
	[TestMethod]
	[DataRow( 3, "PUBLISH_PARK" )]
	public void AMenuChoiceThatIsNotBuiltIsCounted( int row, string name )
	{
		var stack = APark();

		OpenMenu( stack );

		Choose( stack, row );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "the menu closes" );
		Assert.AreEqual( 1, Times( name ), "and the choice is counted" );
		Assert.AreEqual( name, string.Join( ", ", Unimplemented.Summary.Select( entry => entry.What ) ), "and nothing else is" );

		OpenMenu( stack );
		Choose( stack, row );
		Assert.AreEqual( 2, Times( name ), "chosen again: counted again" );
	}

	/// <summary><b>Resume Game is built and counts nothing.</b></summary>
	/// <remarks><b>Mutations:</b> a report on every choice counts here.</remarks>
	[TestMethod]
	public void AMenuChoiceThatIsBuiltIsNotCounted()
	{
		var stack = APark();

		OpenMenu( stack );
		Choose( stack, 5 );

		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "Resume Game closes the menu" );
		Assert.IsFalse( Unimplemented.Any, "and counts nothing" );
	}

	/// <summary>
	/// <b>The gadget's postcard button is counted</b> each click, through the click the stack sends a button.
	/// </summary>
	/// <remarks><b>Mutations:</b> the report taken out of <c>SendPostcard</c> counts nothing.</remarks>
	[TestMethod]
	public void ThePostcardButtonIsCounted()
	{
		var stack = APark();
		var postcard = Everything( stack.Windows.OfType<UI.ParkGadget>().Single().Root ).Single( control => control.Id == 0x64 );

		postcard.Clicked!();
		Assert.AreEqual( 1, Times( "POSTCARD_BUTTON" ) );

		postcard.Clicked!();
		Assert.AreEqual( 2, Times( "POSTCARD_BUTTON" ) );
		Assert.AreEqual( 0, Times( "FULL_SCREEN_VIEW_POSTCARD" ), "the key's count under the full-screen view is another" );
	}

	/// <summary>
	/// <b>R is counted on its release, with no modifier held, over the park</b>: held it is nothing, with Shift or
	/// Ctrl it is nothing (a row's modifiers are matched exactly, <c>FUN_0040c990</c>), and another key is nothing.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> read on the press counts while held; the modifier test dropped counts Shift+R; any key
	/// counted counts T.
	/// </remarks>
	[TestMethod]
	public void RLetGoOverTheParkIsCountedAsResearch()
	{
		var stack = APark();

		Frame( stack, Down( Key.R ) );
		Frame( stack );
		Assert.AreEqual( 0, Times( "RESEARCH_SHORTCUT" ), "held: nothing" );

		Frame( stack, Up( Key.R ) );
		Assert.AreEqual( 1, Times( "RESEARCH_SHORTCUT" ), "let go: counted" );

		foreach ( var modifier in new[] { Key.ShiftLeft, Key.ControlLeft } )
		{
			Frame( stack, Down( modifier ), Down( Key.R ) );
			Frame( stack, Up( Key.R ) );
			Frame( stack, Up( modifier ) );
		}

		Press( stack, Key.T );
		Assert.AreEqual( 1, Times( "RESEARCH_SHORTCUT" ), "Shift+R, Ctrl+R and T: nothing" );
	}

	/// <summary>
	/// <b>R is heard where the shortcuts' table is run and nowhere else</b>: under a park screen (its handler runs
	/// that table alone, <c>0x00488c13</c>), not under the game menu or another modal window, not under the
	/// full-screen view, and not in first person.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> each gate taken out of <c>ResearchKey</c>, or the full-screen view's branch reading R, counts
	/// on its line; a park screen refused leaves the first at nought.
	/// </remarks>
	[TestMethod]
	public void RIsHeardOnlyWhereTheShortcutsTableIsRun()
	{
		var stack = APark();
		var screen = new AWindow( stack, modal: false, parkScreen: true );

		stack.Open( screen );
		Press( stack, Key.R );
		Assert.AreEqual( 1, Times( "RESEARCH_SHORTCUT" ), "under a park screen: counted" );
		stack.Close( screen );

		var box = new AWindow( stack, modal: true, parkScreen: false );

		stack.Open( box );
		Press( stack, Key.R );
		Assert.AreEqual( 1, Times( "RESEARCH_SHORTCUT" ), "under a modal window: the window's" );
		stack.Close( box );

		OpenMenu( stack );
		Press( stack, Key.R );
		Assert.AreEqual( 1, Times( "RESEARCH_SHORTCUT" ), "under the game menu: the menu's" );
		Press( stack, Key.Escape );

		Press( stack, Key.F3 );
		Assert.IsTrue( stack.Covered );
		Press( stack, Key.R );
		Assert.AreEqual( 1, Times( "RESEARCH_SHORTCUT" ), "under the full-screen view: not heard" );
		Press( stack, Key.F3 );

		InFirstPerson( true );
		Press( stack, Key.R );
		Assert.AreEqual( 1, Times( "RESEARCH_SHORTCUT" ), "in first person: the viewfinder's layer, which has no such row" );
		InFirstPerson( false );

		Press( stack, Key.R );
		Assert.AreEqual( 2, Times( "RESEARCH_SHORTCUT" ), "and over the park again: counted" );
	}

	/// <summary>
	/// <b>The boot's effect 210 asks for a volume of nought</b> in the shipped category, where the click, effect 31,
	/// asks for the whole of it; an effect with no header reads nought.
	/// </summary>
	/// <remarks><b>Mutations:</b> the high bound read, the divisor changed or a constant returned fails one of the three.</remarks>
	[TestMethod]
	public void TheBootsSoundIsPlayedAtItsVariationsVolume()
	{
		var category = new SoundCategory( "global", "global/sound", "ui" );

		Assert.AreEqual( 1, category.VariationsOf( 210 ).Count, "effect 210 has one variation, and its header walks" );
		Assert.AreEqual( (0, 0), category.VariationsOf( 210 )[0].Volume );
		Assert.AreEqual( 0f, UI.UiSounds.BootVolume( category.VariationsOf( 210 ) ), "the boot's sound: silent" );
		Assert.AreEqual( 1f, UI.UiSounds.BootVolume( category.VariationsOf( 31 ) ), "the click, the same sample: whole" );
		Assert.AreEqual( 0.4f, UI.UiSounds.BootVolume( [new SoundCategoryFile.Variation( 1, 0, 0, 0, 65535, [], (40, 90) )] ), "the low bound of a hundred" );
		Assert.AreEqual( 0f, UI.UiSounds.BootVolume( [] ), "no header: nought" );
	}

	/// <summary>A park's stack as entering one makes it: the gadget and the viewfinder under the real park front end.</summary>
	private static UI.WindowStack APark()
	{
		var stack = new UI.WindowStack();

		_ = new UI.ParkFrontEnd( stack, "jungle" );

		return stack;
	}

	/// <summary>
	/// Opens the game menu with Escape, as the player does: one that empties a full hand first, then the one that opens it.
	/// </summary>
	private static void OpenMenu( UI.WindowStack stack )
	{
		Press( stack, Key.Escape );

		if ( !stack.Windows.OfType<UI.GameMenu>().Any() )
			Press( stack, Key.Escape );

		Assert.AreEqual( "ParkGadget, ParkViewfinder, GameMenu", Names( stack ), "the menu is open" );
	}

	/// <summary>Chooses the open game menu's row, as a click let go on it does.</summary>
	private static void Choose( UI.WindowStack stack, int row )
	{
		var menu = stack.Windows.OfType<UI.GameMenu>().Single();
		var choices = (IList)typeof( UI.GameMenu ).GetField( "_choices", BindingFlags.Instance | BindingFlags.NonPublic )!.GetValue( menu )!;

		((UI.UiControl)choices[row]!).LeftClicked!();
	}

	private static IEnumerable<UI.UiControl> Everything( UI.UiControl control )
		=> control.Children.SelectMany( Everything ).Prepend( control );

	/// <summary>One frame of the game with these key events and the pointer on the grass: the input, then the stack's update.</summary>
	private static void Frame( UI.WindowStack stack, params KeyEvent[] keys )
	{
		Input.UpdateFrom( new Snapshot( keys, Grass ) );
		stack.Update();
	}

	/// <summary>A key down in one frame and up in the next.</summary>
	private static void Press( UI.WindowStack stack, Key key )
	{
		Frame( stack, Down( key ) );
		Frame( stack, Up( key ) );
	}

	/// <summary>Puts first person up or down, as entering and leaving it do, without a camera to move.</summary>
	private static void InFirstPerson( bool active )
		=> typeof( ParkCamcorderCameraMode ).GetProperty( nameof( ParkCamcorderCameraMode.Active ) )!.SetValue( null, active );

	private static int Times( string what ) => Unimplemented.Summary.FirstOrDefault( entry => entry.What == what ).Times;

	private static string Names( UI.WindowStack stack ) => string.Join( ", ", stack.Windows.Select( window => window.GetType().Name ) );

	private static KeyEvent Down( Key key ) => new( key, true, ModifierKeys.None );

	private static KeyEvent Up( Key key ) => new( key, false, ModifierKeys.None );

	/// <summary>A bare window, modal or not, a park screen or not.</summary>
	private sealed class AWindow : UI.UiWindow
	{
		public AWindow( UI.WindowStack stack, bool modal, bool parkScreen ) : base( stack )
		{
			Root = new UI.UiControl { Rect = new UI.UiRect( 600, 300, 1000, 600 ) };
			Modal = modal;
			ParkScreen = parkScreen;
		}
	}

	/// <summary>SDL's events for one frame: the keys, where the pointer is and which buttons are down.</summary>
	private sealed class Snapshot( KeyEvent[] keys, Vector2 at ) : InputSnapshot
	{
		public IReadOnlyList<KeyEvent> KeyEvents => keys;
		public IReadOnlyList<MouseEvent> MouseEvents => [];
		public IReadOnlyList<char> KeyCharPresses => [];
		public System.Numerics.Vector2 MousePosition => new( at.X, at.Y );
		public float WheelDelta => 0f;
		public bool IsMouseDown( MouseButton button ) => false;
	}
}
