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
/// Park and the gadget's postcard button, each counted where the player reaches it and only there; R, the shortcuts
/// table's research row (<c>0x0040c5b0</c>, into <c>FUN_004aa480</c>), heard in the same places; and the shortcuts
/// table's other rows, which open their screens or are counted. See <c>ParkFrontEnd.NotYet</c>, <c>ParkFrontEnd.ShortcutKey</c> and
/// <c>ParkGadget.SendPostcard</c>.
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
	/// <b>Ctrl+S puts the staff's locator out and the next Escape folds it away and opens no menu</b>; the Escape after
	/// opens the menu (<c>FUN_004816b0</c> ahead of the hand and the menu, <c>0x0040c1a0</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> Escape not asking the locator first opens the menu with it out.</remarks>
	[TestMethod]
	public void EscapeFoldsAnOpenLocatorFirst()
	{
		var stack = APark();
		var gadget = stack.Windows.OfType<UI.ParkGadget>().Single();
		var locator = gadget.Locator;

		WithControl( stack, Key.S );
		ParkGadgetPressTests.Settle( gadget );
		Assert.AreEqual( (UI.ParkLocator.Kind.Staff, true), (locator.Open, locator.StaffPanel.Visible) );

		Press( stack, Key.Escape );
		ParkGadgetPressTests.Settle( gadget );
		Assert.AreEqual( (UI.ParkLocator.Kind.None, false), (locator.Open, locator.StaffPanel.Visible) );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "no menu" );

		Press( stack, Key.Escape );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, GameMenu", Names( stack ), "the next opens it" );
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
	/// <b>R runs the research opener on its release, with no modifier held, over the park</b>, which in Instant Action
	/// is its box: held it is nothing, with Shift or Ctrl it is nothing (a row's modifiers are matched exactly,
	/// <c>FUN_0040c990</c>), and another key is nothing.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> read on the press opens while held; the modifier test dropped opens on Shift+R; any key
	/// opening opens on T.
	/// </remarks>
	[TestMethod]
	public void RLetGoOverTheParkRunsTheResearchOpener()
	{
		var stack = APark();

		Frame( stack, Down( Key.R ) );
		Frame( stack );
		Assert.IsFalse( Asked( stack ), "held: nothing" );

		Frame( stack, Up( Key.R ) );
		Assert.IsTrue( Asked( stack ), "let go: the box" );

		foreach ( var modifier in new[] { Key.ShiftLeft, Key.ControlLeft } )
		{
			Frame( stack, Down( modifier ), Down( Key.R ) );
			Frame( stack, Up( Key.R ) );
			Frame( stack, Up( modifier ) );
			Assert.IsFalse( Asked( stack ), modifier.ToString() );
		}

		Press( stack, Key.T );
		Assert.IsFalse( Asked( stack ), "T" );
		Assert.IsFalse( stack.Windows.Any( window => window is UI.ParkResearchScreen ), "Instant Action: the box, never the screen" );
	}

	/// <summary>
	/// <b>R is heard where the shortcuts' table is run and nowhere else</b>: under a park screen (its handler runs
	/// that table alone, <c>0x00488c13</c>), which the opener closes first, not under the game menu or another modal
	/// window, not under the full-screen view, and not in first person.
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> each gate taken out of <c>ShortcutKey</c>, or the full-screen view's branch reading R, opens on
	/// its line; a park screen refused leaves the first shut.
	/// </remarks>
	[TestMethod]
	public void RIsHeardOnlyWhereTheShortcutsTableIsRun()
	{
		var stack = APark();
		var screen = new AWindow( stack, modal: false, parkScreen: true );

		stack.Open( screen );
		Press( stack, Key.R );
		Assert.IsFalse( stack.Windows.Contains( screen ), "under a park screen: heard, and the screen closed" );
		Assert.IsTrue( Asked( stack ), "under a park screen: the box" );

		var box = new AWindow( stack, modal: true, parkScreen: false );

		stack.Open( box );
		Press( stack, Key.R );
		Assert.AreSame( box, stack.Windows[^1], "under a modal window: the window's" );
		stack.Close( box );

		OpenMenu( stack );
		Press( stack, Key.R );
		Assert.IsFalse( Asked( stack ), "under the game menu: the menu's" );
		Press( stack, Key.Escape );

		Press( stack, Key.F3 );
		Assert.IsTrue( stack.Covered );
		Press( stack, Key.R );
		Assert.IsFalse( Asked( stack ), "under the full-screen view: not heard" );
		Press( stack, Key.F3 );

		InFirstPerson( true );
		Press( stack, Key.R );
		Assert.IsFalse( Asked( stack ), "in first person: the viewfinder's layer, which has no such row" );
		InFirstPerson( false );

		Press( stack, Key.R );
		Assert.IsTrue( Asked( stack ), "and over the park again: the box" );
	}

	/// <summary>Whether the research opener's box is in front; if it is, it is closed.</summary>
	private static bool Asked( UI.WindowStack stack )
	{
		if ( stack.Windows.Count == 0 || stack.Windows[^1] is not UI.MessageBox box )
			return false;

		Assert.AreEqual( ParkResearchOpenerTests.InstantActionText, ParkResearchOpenerTests.TextOf( box ) );
		stack.Close( box );
		return true;
	}

	/// <summary>
	/// <b>A key let go matches the shortcuts table's row whose key and modifier byte both equal it</b> (<c>0x00748378</c>,
	/// <c>FUN_0040c990</c>): fourteen rows name no modifier, the two locators and the postcard Ctrl alone. All-items and
	/// all-staff are O and E, where the original's are A and S, which pan the camera here.
	/// </summary>
	/// <remarks><b>Mutations:</b> any row's key changed; Ctrl taken as no modifier; a modifier left out of the match; A or S
	/// given back its row.</remarks>
	[TestMethod]
	public void EachShortcutRowIsTheOriginalsKeyAndModifier()
	{
		Key[] plain = [Key.Escape, Key.B, Key.H, Key.I, Key.O, Key.E, Key.V, Key.F, Key.L, Key.T, Key.G, Key.R, Key.Space];

		for ( var row = 0; row < plain.Length; row++ )
			Assert.AreEqual( row, UI.ParkFrontEnd.ShortcutRow( new Input.KeyRelease( plain[row], Input.Modifiers.None ) ), plain[row].ToString() );

		Assert.AreEqual( 16, UI.ParkFrontEnd.ShortcutRow( new Input.KeyRelease( Key.C, Input.Modifiers.None ) ) );
		Assert.AreEqual( (13, 14, 15), (Row( Key.S, Input.Modifiers.Control ), Row( Key.V, Input.Modifiers.Control ), Row( Key.P, Input.Modifiers.Control )) );
		Assert.AreEqual( (-1, -1, -1, -1, -1, -1),
			(Row( Key.P, Input.Modifiers.None ), Row( Key.A, Input.Modifiers.Control ), Row( Key.A, Input.Modifiers.Shift ),
			Row( Key.S, Input.Modifiers.Alt ), Row( Key.S, Input.Modifiers.Control | Input.Modifiers.Shift ), Row( Key.W, Input.Modifiers.None )) );
		Assert.AreEqual( (-1, -1, -1, -1), (Row( Key.A, Input.Modifiers.None ), Row( Key.S, Input.Modifiers.None ),
			Row( Key.D, Input.Modifiers.None ), Row( Key.E, Input.Modifiers.Control )), "A and S pan the camera here" );
		Assert.AreEqual( (17, "allitems", "allstaff", "map"),
			(UI.ParkFrontEnd.ShortcutNames.Length, UI.ParkFrontEnd.ShortcutNames[4], UI.ParkFrontEnd.ShortcutNames[5], UI.ParkFrontEnd.ShortcutNames[12]) );

		static int Row( Key key, Input.Modifiers held ) => UI.ParkFrontEnd.ShortcutRow( new Input.KeyRelease( key, held ) );
	}

	/// <summary>
	/// <b>Each screen row opens the screen its category button would, in the open one's place, and leaves the
	/// category on it</b> (each opener's <c>FUN_004a0810</c>), the entry price's left as it is with it up; Space opens the
	/// map, which keeps its keys; the rows whose screen is not built are counted, park status leaving its category on 3,
	/// and loans do nothing in Instant Action (<c>0x0049fb8e</c>).
	/// </summary>
	/// <remarks>
	/// <b>Mutations:</b> a row sent to another screen; O and E exchanged; a category left where it was; the map row left
	/// out; the map's gate taken out; a counted row's name changed or its count left out; loans heard in Instant Action;
	/// the entry price built again with it up.
	/// </remarks>
	[TestMethod]
	public void EachScreenRowOpensTheScreenItsCategoryButtonWould()
	{
		var stack = APark();

		UI.ParkCategoryScreens.Seed();

		(Key Key, string Screen, int Category, int LeftOn)[] rows =
		[
			(Key.B, "ParkBuyScreen", UI.ParkCategoryScreens.Building, 1),
			(Key.H, "ParkHireScreen", UI.ParkCategoryScreens.Building, 2),
			(Key.O, "ParkItemsScreen", UI.ParkCategoryScreens.Information, 5),
			(Key.E, "ParkStaffScreen", UI.ParkCategoryScreens.Information, 4),
			(Key.V, "ParkVisitorsScreen", UI.ParkCategoryScreens.Information, 6),
			(Key.T, "ParkStaffCostsScreen", UI.ParkCategoryScreens.Finance, 9),
			(Key.G, "ParkEntryPriceScreen", UI.ParkCategoryScreens.Finance, 10)
		];

		foreach ( var row in rows )
		{
			Press( stack, row.Key );
			Assert.AreEqual( $"ParkGadget, ParkViewfinder, {row.Screen}", Names( stack ), row.Key.ToString() );
			Assert.AreEqual( row.LeftOn, UI.ParkCategoryScreens.LeftOn( row.Category ), row.Key.ToString() );
		}

		var price = stack.Windows[^1];
		Press( stack, Key.G );
		Assert.AreSame( price, stack.Windows[^1], "G with the entry price up: the screen that was open" );

		Press( stack, Key.Escape );
		Press( stack, Key.Space );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkMapScreen", Names( stack ), "Space: the map" );
		Press( stack, Key.O );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkMapScreen", Names( stack ), "O over the map: the map's" );
		Press( stack, Key.Escape );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ) );

		foreach ( var key in new[] { Key.I, Key.F, Key.L } )
			Press( stack, key );

		WithControl( stack, Key.S );
		WithControl( stack, Key.V );
		WithControl( stack, Key.P );

		Assert.IsTrue( Level.InstantAction, "no player: Instant Action" );
		Assert.AreEqual( (1, 1, 0, 1),
			(Times( "PARK_STATUS_SCREEN" ), Times( "FINANCE_SCREEN" ), Times( "LOANS_SCREEN" ), Times( "POSTCARD_SHORTCUT" )) );
		Assert.AreEqual( (3, 7), (UI.ParkCategoryScreens.LeftOn( UI.ParkCategoryScreens.Information ), UI.ParkCategoryScreens.LeftOn( UI.ParkCategoryScreens.Finance )), "loans left nothing" );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "nothing opened" );

		UI.ParkCategoryScreens.Seed();
	}

	/// <summary>
	/// <b>The screen rows are heard where R is and nowhere else</b>: not under a modal window, the game menu or the
	/// full-screen view; in first person only the postcard's row, which the viewfinder's layer answers (<c>0x00488a00</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> the modal gate, the menu gate or the first-person gate taken out; the postcard refused in first person.</remarks>
	[TestMethod]
	public void TheScreenRowsAreHeardOnlyWhereTheShortcutsTableIsRun()
	{
		var stack = APark();
		var box = new AWindow( stack, modal: true, parkScreen: false );

		stack.Open( box );
		Press( stack, Key.E );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, AWindow", Names( stack ), "under a modal window: the window's" );
		stack.Close( box );

		OpenMenu( stack );
		Press( stack, Key.E );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, GameMenu", Names( stack ), "under the game menu: the menu's" );
		Press( stack, Key.Escape );

		Press( stack, Key.F3 );
		Press( stack, Key.E );
		Assert.AreEqual( "ParkGadget, ParkViewfinder", Names( stack ), "under the full-screen view: not heard" );
		Press( stack, Key.F3 );

		InFirstPerson( true );
		Press( stack, Key.E );
		WithControl( stack, Key.P );
		Assert.AreEqual( ("ParkGadget, ParkViewfinder", 1), (Names( stack ), Times( "POSTCARD_SHORTCUT" )), "in first person: the postcard alone" );
		InFirstPerson( false );

		Press( stack, Key.E );
		Assert.AreEqual( "ParkGadget, ParkViewfinder, ParkStaffScreen", Names( stack ), "over the park: the staff" );

		UI.ParkCategoryScreens.Seed();
	}

	/// <summary>
	/// <b>W, S, A and D pan the orbit camera beside the keypad's 8, 2, 4 and 6, and Up and Down zoom beside the keypad's 1
	/// and 3</b>, each with no modifier held, as the original's camera rows (<c>0x00748158</c>); a held pan settles at
	/// <c>sqrt( zoom ) x 10</c> a second on the screen's axes, eases up by half every 31 ms and coasts out
	/// (<c>ParkOrbitCameraMode.PanInput</c>, <c>ZoomInput</c>, <c>PanTarget</c>, <c>StepScroll</c>).
	/// </summary>
	/// <remarks><b>Mutations:</b> a key's direction turned over; a modifier let through (Ctrl+S, Alt+S); a keypad key left
	/// out; Up and Down exchanged; the speed not by the root of the zoom; the yaw's turn the other way; the rise or the
	/// coast at another rate; the floor of 0.1 left out; the rise made half the gap a frame whatever <c>Time.Delta</c> is.</remarks>
	[TestMethod]
	public void TheKeysPanAndZoomTheOrbitCamera()
	{
		Assert.AreEqual( ((1f, 0f), (-1f, 0f), (0f, -1f), (0f, 1f)),
			(Pan( Key.W ), Pan( Key.S ), Pan( Key.A ), Pan( Key.D )), "W, S, A, D" );
		Assert.AreEqual( ((1f, 0f), (-1f, 0f), (0f, -1f), (0f, 1f)),
			(Pan( Key.Keypad8 ), Pan( Key.Keypad2 ), Pan( Key.Keypad4 ), Pan( Key.Keypad6 )), "the keypad's 8, 2, 4, 6" );
		Assert.AreEqual( ((0f, 0f), (0f, 0f), (0f, 0f), (0f, 0f), (1f, 0f), (1f, -1f)),
			(Pan( Key.ControlLeft, Key.S ), Pan( Key.AltLeft, Key.S ), Pan( Key.ShiftRight, Key.W ), Pan( Key.W, Key.S ),
			Pan( Key.W, Key.Keypad8 ), Pan( Key.W, Key.A )), "a modifier held; both ways; two keys one way; two axes" );
		Assert.AreEqual( (0f, 0f), Pan( Key.Up, Key.Left, Key.E, Key.O ), "the arrows and the moved shortcuts pan nothing" );

		Assert.AreEqual( (-1f, 1f, -1f, 1f), (Zoom( Key.Up ), Zoom( Key.Down ), Zoom( Key.Keypad1 ), Zoom( Key.Keypad3 )) );
		Assert.AreEqual( (0f, 0f, 0f, 0f), (Zoom( Key.Up, Key.Down ), Zoom( Key.ControlLeft, Key.Up ), Zoom( Key.W ), Zoom( Key.Left )) );

		AreNear( (0f, 104.881f), ParkOrbitCameraMode.PanTarget( 1f, 0f, 0f, 110f ), "forward at yaw 0, zoom 110" );
		AreNear( (-83.666f, 0f), ParkOrbitCameraMode.PanTarget( 0f, -1f, 0f, 70f ), "left at zoom 70" );
		AreNear( (-114.018f, 0f), ParkOrbitCameraMode.PanTarget( 1f, 0f, MathF.PI / 2f, 130f ), "forward a quarter turned" );

		var delta = Time.Delta;
		Time.Delta = 0.031f;

		try
		{
			AreNear( (50f, -25f), ParkOrbitCameraMode.StepScroll( Vector2.Zero, new Vector2( 100f, -50f ), true ), "half the gap in 31 ms" );
			AreNear( (73.345f, 0f), ParkOrbitCameraMode.StepScroll( new Vector2( 100f, 0.09f ), new Vector2( 5f, 5f ), false ), "the coast, and under 0.1 nought" );

			// The original halves the gap every frame, whatever the frame's length (not on dt); this rise is on Time.Delta,
			// so at half the frame it takes 29.29 % of the gap, not half.
			Time.Delta = 0.0155f;
			AreNear( (29.289f, -14.645f), ParkOrbitCameraMode.StepScroll( Vector2.Zero, new Vector2( 100f, -50f ), true ), "the rise in 15.5 ms" );
		}
		finally
		{
			Time.Delta = delta;
		}

		static (float, float) Pan( params Key[] down ) => ParkOrbitCameraMode.PanInput( down );

		static float Zoom( params Key[] down ) => ParkOrbitCameraMode.ZoomInput( down );

		static void AreNear( (float X, float Y) expected, Vector2 actual, string what )
		{
			Assert.AreEqual( expected.X, actual.X, 0.01f, what );
			Assert.AreEqual( expected.Y, actual.Y, 0.01f, what );
		}
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

	/// <summary>A key let go with Ctrl held, and Ctrl let go after it.</summary>
	private static void WithControl( UI.WindowStack stack, Key key )
	{
		Frame( stack, Down( Key.ControlLeft ), Down( key ) );
		Frame( stack, Up( key ) );
		Frame( stack, Up( Key.ControlLeft ) );
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
